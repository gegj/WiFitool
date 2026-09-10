using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class AdbdTftpServer : IDisposable
    {
        private const int DefaultBlockSize = 512;
        private readonly IPAddress address;
        private readonly byte[] file;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> downloaded = new TaskCompletionSource<bool>();
        private UdpClient listener;

        public Task Downloaded { get { return downloaded.Task; } }

        public AdbdTftpServer(IPAddress address, string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("缺少内置 adbd 文件。", filePath);
            this.address = address;
            file = File.ReadAllBytes(filePath);
        }

        public void Start()
        {
            // 监听所有本机地址，避免设备请求到网卡地址时被绑定范围限制。
            listener = new UdpClient(new IPEndPoint(IPAddress.Any, 69));
            _ = ListenAsync();
        }

        private async Task ListenAsync()
        {
            while (!cancellation.IsCancellationRequested)
            {
                UdpReceiveResult request;
                try { request = await listener.ReceiveAsync(); }
                catch (ObjectDisposedException) { break; }
                catch (SocketException)
                {
                    if (cancellation.IsCancellationRequested) break;
                    continue;
                }

                Dictionary<string, string> options;
                if (!TryParseReadRequest(request.Buffer, out options))
                {
                    continue;
                }
                _ = Task.Run(() => TransferAsync(request.RemoteEndPoint, options));
            }
        }

        private static bool TryParseReadRequest(byte[] request, out Dictionary<string, string> options)
        {
            options = null;
            if (request == null || request.Length < 4 || request[0] != 0 || request[1] != 1) return false;
            var fields = new List<string>();
            var start = 2;
            for (var index = 2; index < request.Length; index++)
            {
                if (request[index] != 0) continue;
                fields.Add(Encoding.ASCII.GetString(request, start, index - start));
                start = index + 1;
            }
            if (start < request.Length) fields.Add(Encoding.ASCII.GetString(request, start, request.Length - start));
            if (fields.Count < 2 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1])) return false;
            options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 2; index + 1 < fields.Count; index += 2)
                if (!string.IsNullOrWhiteSpace(fields[index])) options[fields[index]] = fields[index + 1];
            return true;
        }

        private async Task TransferAsync(IPEndPoint remote, Dictionary<string, string> options)
        {
            try
            {
                using (var transfer = new UdpClient(new IPEndPoint(address, 0)))
                {
                    transfer.Connect(remote);
                    var blockSize = GetBlockSize(options);
                    var timeoutMilliseconds = GetTimeoutMilliseconds(options);

                    var optionPacket = BuildOptionPacket(options, blockSize);
                    if (optionPacket != null)
                    {
                        if (!await SendAndWaitForAckAsync(transfer, optionPacket, 0, timeoutMilliseconds))
                        {
                            return;
                        }
                    }

                    var block = 1;
                    var offset = 0;
                    while (!cancellation.IsCancellationRequested)
                    {
                        var size = Math.Min(blockSize, file.Length - offset);
                        var packet = new byte[size + 4];
                        packet[1] = 3;
                        packet[2] = (byte)(block >> 8);
                        packet[3] = (byte)block;
                        if (size > 0) Buffer.BlockCopy(file, offset, packet, 4, size);
                        if (!await SendAndWaitForAckAsync(transfer, packet, block, timeoutMilliseconds))
                        {
                            return;
                        }
                        offset += size;
                        if (size < blockSize)
                        {
                            downloaded.TrySetResult(true);
                            return;
                        }
                        block = (block + 1) & 0xffff;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
            catch (Exception) { }
        }

        private async Task<bool> SendAndWaitForAckAsync(UdpClient transfer, byte[] packet, int expectedBlock, int timeoutMilliseconds)
        {
            for (var attempt = 1; attempt <= 4 && !cancellation.IsCancellationRequested; attempt++)
            {
                await transfer.SendAsync(packet, packet.Length);
                transfer.Client.ReceiveTimeout = timeoutMilliseconds;
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    var ack = transfer.Receive(ref remote);
                    if (ack.Length >= 4 && ack[0] == 0 && ack[1] == 4 && ack[2] == (byte)(expectedBlock >> 8) && ack[3] == (byte)expectedBlock) return true;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                }
            }
            return false;
        }

        private static int GetBlockSize(Dictionary<string, string> options)
        {
            string value;
            int blockSize;
            if (options != null && options.TryGetValue("blksize", out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out blockSize) && blockSize >= 8 && blockSize <= 65464) return blockSize;
            return DefaultBlockSize;
        }

        private static int GetTimeoutMilliseconds(Dictionary<string, string> options)
        {
            string value;
            int seconds;
            if (options != null && options.TryGetValue("timeout", out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) && seconds >= 1 && seconds <= 255) return seconds * 1000;
            return 2500;
        }

        private byte[] BuildOptionPacket(Dictionary<string, string> options, int blockSize)
        {
            if (options == null || options.Count == 0) return null;
            var fields = new List<string>();
            if (options.ContainsKey("blksize")) { fields.Add("blksize"); fields.Add(blockSize.ToString(CultureInfo.InvariantCulture)); }
            if (options.ContainsKey("tsize")) { fields.Add("tsize"); fields.Add(file.Length.ToString(CultureInfo.InvariantCulture)); }
            if (options.ContainsKey("timeout")) { fields.Add("timeout"); fields.Add((GetTimeoutMilliseconds(options) / 1000).ToString(CultureInfo.InvariantCulture)); }
            if (fields.Count == 0) return null;
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0); stream.WriteByte(6);
                foreach (var field in fields)
                {
                    var bytes = Encoding.ASCII.GetBytes(field);
                    stream.Write(bytes, 0, bytes.Length); stream.WriteByte(0);
                }
                return stream.ToArray();
            }
        }

        public void Dispose()
        {
            cancellation.Cancel();
            try { if (listener != null) listener.Close(); } catch { }
            cancellation.Dispose();
        }
    }
}

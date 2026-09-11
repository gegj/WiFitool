using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, byte> activeTransfers = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private UdpClient listener;

        public Task Downloaded { get { return downloaded.Task; } }

        public AdbdTftpServer(IPAddress address, string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("缺少内置 adbd 文件。", filePath);
            this.address = address;
            file = File.ReadAllBytes(filePath);
            LogService.Instance.Info("TFTP", "已加载 adbd 文件，大小 " + file.Length + " 字节，本机地址 " + address);
        }

        public void Start()
        {
            // 监听所有本机地址，避免设备请求到网卡地址时被绑定范围限制。
            listener = new UdpClient(new IPEndPoint(IPAddress.Any, 69));
            LogService.Instance.Info("TFTP", "临时 TFTP 服务已启动，监听 UDP 69");
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
                    LogService.Instance.Warn("TFTP", "监听 UDP 69 时发生 Socket 异常");
                    continue;
                }

                Dictionary<string, string> options;
                if (!TryParseReadRequest(request.Buffer, out options))
                {
                    LogService.Instance.Debug("TFTP", "忽略无效或非读取请求，来源 " + request.RemoteEndPoint);
                    continue;
                }
                var transferKey = request.RemoteEndPoint.Address + ":" + request.RemoteEndPoint.Port;
                if (!activeTransfers.TryAdd(transferKey, 0))
                {
                    LogService.Instance.Debug("TFTP", "忽略重复的 adbd 读取请求，来源 " + request.RemoteEndPoint);
                    continue;
                }
                LogService.Instance.Info("TFTP", "收到 adbd 读取请求，来源 " + request.RemoteEndPoint + "，选项 " + FormatOptions(options));
                _ = Task.Run(async delegate
                {
                    try { await TransferAsync(request.RemoteEndPoint, options); }
                    finally { byte ignored; activeTransfers.TryRemove(transferKey, out ignored); }
                });
            }
            LogService.Instance.Debug("TFTP", "临时 TFTP 监听循环已结束");
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
                    LogService.Instance.Debug("TFTP", "开始传输 adbd，目标 " + remote + "，块大小 " + blockSize + "，超时 " + timeoutMilliseconds + " ms");

                    var optionPacket = BuildOptionPacket(options, blockSize);
                    if (optionPacket != null)
                    {
                        LogService.Instance.Debug("TFTP", "发送 TFTP 选项确认包，目标 " + remote);
                        if (!await SendAndWaitForAckAsync(transfer, optionPacket, 0, timeoutMilliseconds))
                        {
                            LogService.Instance.Warn("TFTP", "TFTP 选项确认超时，目标 " + remote);
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
                            LogService.Instance.Warn("TFTP", "数据块 " + block + " 未收到确认，目标 " + remote);
                            return;
                        }
                        offset += size;
                        if (size < blockSize)
                        {
                            downloaded.TrySetResult(true);
                            LogService.Instance.Info("TFTP", "adbd 传输完成，目标 " + remote + "，总大小 " + file.Length + " 字节");
                            return;
                        }
                        block = (block + 1) & 0xffff;
                    }
                }
            }
            catch (OperationCanceledException) { LogService.Instance.Debug("TFTP", "adbd 传输被取消，目标 " + remote); }
            catch (SocketException ex) { LogService.Instance.Warn("TFTP", "adbd 传输 Socket 失败，目标 " + remote, ex); }
            catch (Exception ex) { LogService.Instance.Error("TFTP", "adbd 传输失败，目标 " + remote, ex); }
        }

        private async Task<bool> SendAndWaitForAckAsync(UdpClient transfer, byte[] packet, int expectedBlock, int timeoutMilliseconds)
        {
            for (var attempt = 1; attempt <= 4 && !cancellation.IsCancellationRequested; attempt++)
            {
                LogService.Instance.Debug("TFTP", "发送数据包，块 " + expectedBlock + "，第 " + attempt + " 次尝试");
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
                    LogService.Instance.Debug("TFTP", "等待块 " + expectedBlock + " 确认超时，第 " + attempt + " 次尝试");
                }
            }
            return false;
        }

        private static string FormatOptions(Dictionary<string, string> options)
        {
            if (options == null || options.Count == 0) return "无";
            var values = new List<string>();
            foreach (var item in options) values.Add(item.Key + "=" + item.Value);
            return string.Join(", ", values);
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
            LogService.Instance.Debug("TFTP", "临时 TFTP 服务已停止");
        }
    }
}

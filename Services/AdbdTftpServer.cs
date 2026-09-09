using System;
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
        private readonly IPAddress address;
        private readonly byte[] file;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private UdpClient listener;
        private Task listening;
        private readonly TaskCompletionSource<bool> downloaded = new TaskCompletionSource<bool>();

        public Task Downloaded { get { return downloaded.Task; } }

        public AdbdTftpServer(IPAddress address, string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("缺少内置 adbd 文件。", filePath);
            this.address = address;
            file = File.ReadAllBytes(filePath);
        }

        public void Start()
        {
            listener = new UdpClient(new IPEndPoint(address, 69));
            listening = ListenAsync();
        }

        private async Task ListenAsync()
        {
            while (!cancellation.IsCancellationRequested)
            {
                UdpReceiveResult request;
                try { request = await listener.ReceiveAsync(); }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (cancellation.IsCancellationRequested) break; continue; }
                if (IsAdbdReadRequest(request.Buffer)) _ = Task.Run(() => TransferAsync(request.RemoteEndPoint));
            }
        }

        private static bool IsAdbdReadRequest(byte[] request)
        {
            if (request == null || request.Length < 7 || request[0] != 0 || request[1] != 1) return false;
            var end = Array.IndexOf(request, (byte)0, 2);
            return end > 2 && string.Equals(Encoding.ASCII.GetString(request, 2, end - 2), "adbd", StringComparison.OrdinalIgnoreCase);
        }

        private async Task TransferAsync(IPEndPoint remote)
        {
            try
            {
                using (var transfer = new UdpClient(new IPEndPoint(address, 0)))
                {
                    transfer.Connect(remote);
                    var block = 1;
                    var offset = 0;
                    while (!cancellation.IsCancellationRequested)
                    {
                        var size = Math.Min(512, file.Length - offset);
                        var packet = new byte[size + 4];
                        packet[1] = 3;
                        packet[2] = (byte)(block >> 8);
                        packet[3] = (byte)block;
                        if (size > 0) Buffer.BlockCopy(file, offset, packet, 4, size);
                        var acknowledged = false;
                        for (var attempt = 0; attempt < 3 && !acknowledged; attempt++)
                        {
                            await transfer.SendAsync(packet, packet.Length);
                            var receive = transfer.ReceiveAsync();
                            if (await Task.WhenAny(receive, Task.Delay(2500, cancellation.Token)) != receive) continue;
                            var ack = receive.Result.Buffer;
                            acknowledged = ack.Length >= 4 && ack[0] == 0 && ack[1] == 4 && ack[2] == packet[2] && ack[3] == packet[3];
                        }
                        if (!acknowledged) return;
                        offset += size;
                        if (size < 512) { downloaded.TrySetResult(true); return; }
                        block = (block + 1) & 0xffff;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
        }

        public void Dispose()
        {
            cancellation.Cancel();
            try { if (listener != null) listener.Close(); } catch { }
            cancellation.Dispose();
        }
    }
}

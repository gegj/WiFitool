using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
            // 与 tftpd32 一致监听所有本机地址，避免设备请求到网卡地址时被绑定范围限制。
            listener = new UdpClient(new IPEndPoint(IPAddress.Any, 69));
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
                if (IsAdbdReadRequest(request.Buffer))
                {
                    _ = Task.Run(() => TransferAsync(request.RemoteEndPoint));
                }
            }
        }

        private static bool IsAdbdReadRequest(byte[] request)
        {
            if (request == null || request.Length < 7 || request[0] != 0 || request[1] != 1) return false;
            var end = Array.IndexOf(request, (byte)0, 2);
            // BusyBox 版本可能携带路径或大小写差异；工具只有一个文件，任意 RRQ 都提供 adbd。
            return end > 2;
        }

        private async Task TransferAsync(IPEndPoint remote)
        {
            try
            {
                using (var transfer = new UdpClient(new IPEndPoint(address, 0)))
                {
                    transfer.Connect(remote);
                    // 设备端 BusyBox tftp 与原工具包服务端一样不使用 OACK 选项协商。
                    const int blockSize = 512;
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
                        if (size < blockSize) { downloaded.TrySetResult(true); return; }
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

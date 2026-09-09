using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class TftpRouteReceiver : IDisposable
    {
        private readonly UdpClient listener = new UdpClient(new IPEndPoint(IPAddress.Any, 69));
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly TaskCompletionSource<string> received = new TaskCompletionSource<string>();

        public Task<string> Received { get { return received.Task; } }

        public void Start() { _ = ListenAsync(); }

        private async Task ListenAsync()
        {
            while (!cancellation.IsCancellationRequested)
            {
                UdpReceiveResult request;
                try { request = await listener.ReceiveAsync(); }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { if (cancellation.IsCancellationRequested) return; continue; }
                if (IsRouteWriteRequest(request.Buffer)) _ = Task.Run(() => ReceiveAsync(request.RemoteEndPoint));
            }
        }

        private static bool IsRouteWriteRequest(byte[] request)
        {
            if (request == null || request.Length < 12 || request[0] != 0 || request[1] != 2) return false;
            var end = Array.IndexOf(request, (byte)0, 2);
            return end > 2 && string.Equals(Encoding.ASCII.GetString(request, 2, end - 2), "route.txt", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ReceiveAsync(IPEndPoint remote)
        {
            try
            {
                using (var transfer = new UdpClient())
                {
                    transfer.Connect(remote);
                    await transfer.SendAsync(new byte[] { 0, 4, 0, 0 }, 4);
                    var content = new StringBuilder();
                    var expected = 1;
                    while (!cancellation.IsCancellationRequested)
                    {
                        var receive = transfer.ReceiveAsync();
                        if (await Task.WhenAny(receive, Task.Delay(3000, cancellation.Token)) != receive) return;
                        var data = receive.Result.Buffer;
                        if (data.Length < 4 || data[0] != 0 || data[1] != 3) return;
                        var block = (data[2] << 8) | data[3];
                        if (block != expected) return;
                        if (data.Length > 4) content.Append(Encoding.ASCII.GetString(data, 4, data.Length - 4));
                        await transfer.SendAsync(new[] { (byte)0, (byte)4, data[2], data[3] }, 4);
                        if (data.Length < 516) { received.TrySetResult(content.ToString()); return; }
                        expected = (expected + 1) & 0xffff;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
        }

        public void Dispose() { cancellation.Cancel(); listener.Close(); cancellation.Dispose(); }
    }
}

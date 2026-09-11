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
        private Task listenTask;

        public Task<string> Received { get { return received.Task; } }

        public void Start()
        {
            LogService.Instance.Debug("TFTP", "路由文件接收服务已启动，监听 UDP 69");
            listenTask = ListenAsync();
        }

        private async Task ListenAsync()
        {
            try
            {
                while (!cancellation.IsCancellationRequested && !received.Task.IsCompleted)
                {
                    UdpReceiveResult request;
                    try { request = await listener.ReceiveAsync(); }
                    catch (ObjectDisposedException) { return; }
                    catch (SocketException ex) { if (cancellation.IsCancellationRequested) return; LogService.Instance.Warn("TFTP", "接收路由文件请求时发生 Socket 异常", ex); continue; }
                    if (!IsRouteWriteRequest(request.Buffer)) continue;
                    LogService.Instance.Info("TFTP", "收到 route.txt 上传请求，来源 " + request.RemoteEndPoint);
                    await ReceiveAsync(request.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { LogService.Instance.Error("TFTP", "路由文件监听失败", ex); }
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
                    LogService.Instance.Debug("TFTP", "开始接收 route.txt，来源 " + remote);
                    await transfer.SendAsync(new byte[] { 0, 4, 0, 0 }, 4);
                    var content = new StringBuilder();
                        var expected = 1;
                        while (!cancellation.IsCancellationRequested)
                        {
                            var receive = transfer.ReceiveAsync();
                            if (await Task.WhenAny(receive, Task.Delay(3000, cancellation.Token)) != receive)
                            {
                                try { transfer.Close(); } catch { }
                                try { await receive; } catch (OperationCanceledException) { } catch (ObjectDisposedException) { } catch (SocketException) { }
                                if (cancellation.IsCancellationRequested) LogService.Instance.Debug("TFTP", "route.txt 接收被取消，来源 " + remote);
                                else LogService.Instance.Warn("TFTP", "等待 route.txt 数据块超时，来源 " + remote);
                                return;
                            }
                            var data = (await receive).Buffer;
                        if (data.Length < 4 || data[0] != 0 || data[1] != 3)
                        {
                            LogService.Instance.Warn("TFTP", "收到无效 route.txt 数据包，来源 " + remote);
                            return;
                        }
                        var block = (data[2] << 8) | data[3];
                        if (block != expected)
                        {
                            LogService.Instance.Warn("TFTP", "route.txt 数据块序号异常，期望 " + expected + "，实际 " + block);
                            return;
                        }
                        if (data.Length > 4) content.Append(Encoding.ASCII.GetString(data, 4, data.Length - 4));
                        await transfer.SendAsync(new[] { (byte)0, (byte)4, data[2], data[3] }, 4);
                        if (data.Length < 516)
                        {
                            var value = content.ToString();
                            received.TrySetResult(value);
                            LogService.Instance.Info("TFTP", "route.txt 接收完成，长度 " + value.Length + " 字符");
                            return;
                        }
                        expected = (expected + 1) & 0xffff;
                    }
                }
            }
            catch (OperationCanceledException) { LogService.Instance.Debug("TFTP", "route.txt 接收被取消"); }
            catch (SocketException ex) { LogService.Instance.Warn("TFTP", "route.txt 接收 Socket 失败", ex); }
            catch (Exception ex) { LogService.Instance.Error("TFTP", "route.txt 接收失败", ex); }
        }

        public void Dispose()
        {
            cancellation.Cancel();
            listener.Close();
            if (listenTask == null || listenTask.IsCompleted) cancellation.Dispose();
            else listenTask.ContinueWith(delegate { cancellation.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            LogService.Instance.Debug("TFTP", "路由文件接收服务已停止");
        }
    }
}

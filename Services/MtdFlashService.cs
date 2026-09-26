using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class MtdFlashService
    {
        private const string LoginPath = "/reqproc/proc_post?goformId=LOGIN&password=YWRtaW4%3D";
        private const string RestoreFactoryPath = "/reqproc/proc_post?goformId=RESTORE_FACTORY_SETTINGS";
        private readonly AdbService adbService;

        public MtdFlashService(AdbService adbService)
        {
            this.adbService = adbService;
        }

        public static void ValidateFirmware(string firmwarePath)
        {
            if (!File.Exists(firmwarePath)) throw new FileNotFoundException("找不到所选固件。", firmwarePath);
            var fileLength = new FileInfo(firmwarePath).Length;
            if (fileLength == 0) throw new InvalidDataException("所选固件为空。");
            if (fileLength < 96) throw new InvalidDataException("所选文件不是有效的 SquashFS 镜像。");

            var header = new byte[96];
            using (var stream = File.OpenRead(firmwarePath))
            {
                var offset = 0;
                while (offset < header.Length)
                {
                    var count = stream.Read(header, offset, header.Length - offset);
                    if (count == 0) throw new InvalidDataException("无法读取 SquashFS 镜像头部。");
                    offset += count;
                }
            }

            if (header[0] != (byte)'h' || header[1] != (byte)'s' || header[2] != (byte)'q' || header[3] != (byte)'s')
                throw new InvalidDataException("所选文件不是 SquashFS 镜像。");

            var blockSize = ReadUInt32(header, 12);
            if (blockSize < 4096 || blockSize > 1024 * 1024 || (blockSize & (blockSize - 1)) != 0)
                throw new InvalidDataException("SquashFS 镜像块大小无效。");

            var usedBytes = ReadUInt64(header, 40);
            if (usedBytes < 96 || usedBytes > (ulong)fileLength)
                throw new InvalidDataException("SquashFS 镜像大小信息无效。");
        }

        public static string GetBackupPath(string firmwarePath)
        {
            var directory = Path.GetDirectoryName(firmwarePath);
            var name = Path.GetFileNameWithoutExtension(firmwarePath);
            var extension = Path.GetExtension(firmwarePath);
            return Path.Combine(directory, name + "_bak" + extension);
        }

        public async Task RunAsync(string serial, string firmwarePath, string backupPath, Func<string> resolveDeviceIp, CancellationToken token, Action<string> reportStatus)
        {
            ValidateFirmware(firmwarePath);
            if (string.Equals(Path.GetFullPath(firmwarePath), Path.GetFullPath(backupPath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("备份文件不能与所选固件相同。");

            var toolDirectory = Path.Combine(ToolEnvironment.Root, "mtd");
            var writerPath = Path.Combine(toolDirectory, "MTDWriter");
            var checkerPath = Path.Combine(toolDirectory, "MTDChecker");
            if (!File.Exists(writerPath) || !File.Exists(checkerPath)) throw new FileNotFoundException("工具环境中缺少 MTD 刷写文件，请先等待工具环境准备完成。", toolDirectory);
            var firmwareLength = new FileInfo(firmwarePath).Length;

            Step(reportStatus, "正在读取 /dev/mtd4 分区参数…");
            var mtd4Info = await ReadMtd4InfoAsync(serial, firmwareLength, token);
            Step(reportStatus, "已识别 mtd4 刷写范围：0-" + mtd4Info.LastBlock);

            var localMd5 = CalculateMd5(firmwarePath);
            Step(reportStatus, "正在上传固件并校验…");
            await adbService.UploadFileAsync(serial, "/tmp/mtd4.bin", firmwarePath, token);
            var deviceMd5 = await ReadMd5Async(serial, "/tmp/mtd4.bin", token);
            LogService.Instance.Info("MTD", "固件 MD5：本地 " + localMd5 + "，设备 " + deviceMd5);
            if (!string.Equals(localMd5, deviceMd5, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("固件上传校验失败。");

            Step(reportStatus, "正在备份设备 /dev/mtd4…");
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            await adbService.DownloadFileAsync(serial, "/dev/mtd4", backupPath, token);

            Step(reportStatus, "正在释放设备资源…");
            await RunShellAsync(serial, "cat /sbin/fota_release_space.sh|grep -v adbd|sh 2>/dev/null; killall -q goahead udhcpd dnsmasq iccid_check rmc zte_mifi zte_ufi zte_cpe 2>/dev/null", token, false);

            Step(reportStatus, "正在上传刷写工具…");
            await adbService.UploadFileAsync(serial, "/tmp/MTDWriter", writerPath, token);
            await RunRequiredShellAsync(serial, "chmod 777 /tmp/MTDWriter", "设置刷写工具权限失败。", token);
            await adbService.UploadFileAsync(serial, "/tmp/MTDChecker", checkerPath, token);
            await RunRequiredShellAsync(serial, "chmod 777 /tmp/MTDChecker", "设置校验工具权限失败。", token);

            Step(reportStatus, "正在准备刷写环境…");
            await RunShellAsync(serial, "killall -9 zte_ufi zte_mifi zte_cpe goahead 2>/dev/null", token, false);
            await adbService.UploadFileAsync(serial, "/tmp/new", firmwarePath, token);
            var deviceNewMd5 = await ReadMd5Async(serial, "/tmp/new", token);
            LogService.Instance.Info("MTD", "实际刷写文件 MD5：本地 " + localMd5 + "，设备 " + deviceNewMd5);
            if (!string.Equals(localMd5, deviceNewMd5, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("实际刷写文件上传校验失败。");
            await RunRequiredShellAsync(serial, "sync", "刷写前同步设备数据失败。", token);

            Step(reportStatus, "正在刷写 /dev/mtd4，请勿断开设备…");
            var writerResult = await RunRequiredShellAsync(serial, "/tmp/MTDWriter /dev/mtd4 0 " + mtd4Info.LastBlock, "mtd4 刷写失败。", token);
            EnsureMtdWriterSucceeded(writerResult);

            Step(reportStatus, "正在校验刷写结果…");
            await RunRequiredShellAsync(serial, "/tmp/MTDChecker -N -a /dev/mtd4 /tmp/new", "刷写结果校验失败。", token);

            Step(reportStatus, "正在同步刷写结果…");
            await RunRequiredShellAsync(serial, "sync", "同步刷写结果失败。", token);

            if (firmwareLength == mtd4Info.PartitionSize)
            {
                Step(reportStatus, "正在独立校验 /dev/mtd4 MD5…");
                string deviceMtdMd5;
                try { deviceMtdMd5 = await ReadMd5Async(serial, "/dev/mtd4", token); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { throw new InvalidOperationException("独立读取 /dev/mtd4 校验失败，设备可能已在校验后掉线。" + ex.Message, ex); }
                LogService.Instance.Info("MTD", "独立 mtd4 MD5：本地 " + localMd5 + "，设备 " + deviceMtdMd5);
                if (!string.Equals(localMd5, deviceMtdMd5, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("独立 MD5 校验失败：设备 /dev/mtd4 与所选固件不一致。");
            }
            else
            {
                LogService.Instance.Warn("MTD", "固件大小 " + firmwareLength + " 字节与 mtd4 分区大小 " + mtd4Info.PartitionSize + " 字节不同，跳过整分区 MD5 校验。");
                Step(reportStatus, "固件不是完整 mtd4 分区，跳过独立整分区 MD5 校验…");
            }

            Step(reportStatus, "正在清理临时文件…");
            await RunRequiredShellAsync(serial, "rm -rf /tmp/new /tmp/MTDWriter /tmp/MTDChecker /tmp/mtd4.bin 2>/dev/null", "清理临时文件失败，设备可能已在重启前掉线。", token);

            Step(reportStatus, "正在等待设备文件系统稳定…");
            await Task.Delay(3000, token);

            Step(reportStatus, "刷写完成，正在重启设备…");
            await adbService.RebootAsync(serial, token);
            LogService.Instance.Info("MTD", "MTD 刷写完成并已发送重启命令");
            await RequestFactoryResetAsync(resolveDeviceIp, token, reportStatus);
        }

        private async Task RequestFactoryResetAsync(Func<string> resolveDeviceIp, CancellationToken token, Action<string> reportStatus)
        {
            Step(reportStatus, "设备正在重启，等待 Web 服务恢复…");
            await Task.Delay(5000, token);

            Step(reportStatus, "正在重新获取设备 IP…");
            var deviceIp = "";
            for (var attempt = 0; attempt < 30; attempt++)
            {
                token.ThrowIfCancellationRequested();
                deviceIp = resolveDeviceIp();
                if (!string.IsNullOrWhiteSpace(deviceIp)) break;
                await Task.Delay(1000, token);
            }
            if (string.IsNullOrWhiteSpace(deviceIp)) throw new InvalidOperationException("设备重启后未获取到 IP，无法请求恢复出厂设置。");

            var cookies = new CookieContainer();
            Step(reportStatus, "正在登录设备 Web 接口…");
            var loginDeadline = DateTime.UtcNow.AddSeconds(5);
            var loginSuccess = false;
            while (!loginSuccess && DateTime.UtcNow < loginDeadline)
            {
                token.ThrowIfCancellationRequested();
                var remaining = (int)Math.Max(1, Math.Min(3000, (loginDeadline - DateTime.UtcNow).TotalMilliseconds));
                loginSuccess = await TrySendHttpGetAsync(deviceIp, LoginPath, cookies, token, "登录接口", true, remaining);
                if (!loginSuccess)
                {
                    var delay = (int)Math.Max(1, Math.Min(500, (loginDeadline - DateTime.UtcNow).TotalMilliseconds));
                    if (DateTime.UtcNow < loginDeadline) await Task.Delay(delay, token);
                }
            }
            if (!loginSuccess) Step(reportStatus, "登录接口等待 5 秒未确认成功，继续请求恢复出厂设置…");

            Step(reportStatus, "正在请求恢复出厂设置…");
            await TrySendHttpGetAsync(deviceIp, RestoreFactoryPath, cookies, token, "恢复出厂接口");
            Step(reportStatus, "恢复出厂设置请求已发起，已恢复出厂设置。");
        }

        private static async Task<bool> TrySendHttpGetAsync(string host, string path, CookieContainer cookies, CancellationToken token, string name, bool requireLoginSuccess = false, int timeoutMilliseconds = 3000)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://" + host + path);
                request.Method = "GET";
                request.Proxy = null;
                request.KeepAlive = false;
                request.Timeout = timeoutMilliseconds;
                request.ReadWriteTimeout = timeoutMilliseconds;
                request.CookieContainer = cookies;
                var responseTask = request.GetResponseAsync();
                var completed = await Task.WhenAny(responseTask, Task.Delay(timeoutMilliseconds, token));
                if (completed != responseTask)
                {
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException(name + "请求超时。");
                }

                using (var response = (HttpWebResponse)await responseTask)
                using (var stream = response.GetResponseStream())
                {
                    if (!requireLoginSuccess)
                    {
                        LogService.Instance.Info("MTD", name + "已发起：" + host + path);
                        return true;
                    }

                    var body = "";
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var bodyTask = reader.ReadToEndAsync();
                            var bodyCompleted = await Task.WhenAny(bodyTask, Task.Delay(timeoutMilliseconds, token));
                            if (bodyCompleted != bodyTask)
                            {
                                token.ThrowIfCancellationRequested();
                                throw new TimeoutException(name + "响应读取超时。");
                            }
                            body = await bodyTask;
                        }
                    }
                    if (requireLoginSuccess && !IsLoginSuccess(body))
                    {
                        LogService.Instance.Warn("MTD", name + "返回结果未确认成功：" + host + path);
                        return false;
                    }
                    LogService.Instance.Info("MTD", name + "已发送：" + host + path);
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("MTD", name + "请求失败：" + host + path, ex);
                return false;
            }
        }

        private static bool IsLoginSuccess(string body)
        {
            var compact = Regex.Replace(body ?? "", "\\s+", "");
            return compact.IndexOf("\"result\":\"0\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   compact.IndexOf("\"result\":0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   compact.IndexOf("\"result\":\"4\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   compact.IndexOf("\"result\":4", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task<ToolResult> RunRequiredShellAsync(string serial, string command, string errorMessage, CancellationToken token)
        {
            var result = await RunShellAsync(serial, command, token, true);
            if (result.ExitCode != 0) throw new InvalidOperationException(errorMessage + GetErrorText(result));
            return result;
        }

        private async Task<Mtd4Info> ReadMtd4InfoAsync(string serial, long firmwareLength, CancellationToken token)
        {
            var result = await RunShellAsync(serial, "cat /proc/mtd", token, true);
            if (result.ExitCode != 0) throw new InvalidOperationException("无法读取 /proc/mtd：" + GetErrorText(result));

            var match = Regex.Match(result.StandardOutput ?? "", @"(?m)^\s*mtd4:\s+([0-9a-fA-F]+)\s+([0-9a-fA-F]+)\s+""[^""]*""");
            if (!match.Success) throw new InvalidDataException("无法从 /proc/mtd 找到 mtd4 分区，已停止刷写。");

            long partitionSize;
            long eraseBlockSize;
            if (!long.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out partitionSize)
                || !long.TryParse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out eraseBlockSize)
                || partitionSize <= 0 || eraseBlockSize <= 0 || partitionSize % eraseBlockSize != 0)
                throw new InvalidDataException("/proc/mtd 返回的 mtd4 参数无效，已停止刷写。");

            if (firmwareLength > partitionSize)
                throw new InvalidDataException("所选固件超过 mtd4 分区容量，已停止刷写。");

            var blockCount = partitionSize / eraseBlockSize;
            if (blockCount < 1 || blockCount > int.MaxValue)
                throw new InvalidDataException("mtd4 擦除块数量无效，已停止刷写。");

            var lastBlock = (int)blockCount - 1;
            LogService.Instance.Info("MTD", "mtd4 容量 " + partitionSize + " 字节，擦除块 " + eraseBlockSize + " 字节，共 " + blockCount + " 块，结束块 " + lastBlock);
            return new Mtd4Info { PartitionSize = partitionSize, LastBlock = lastBlock };
        }

        private sealed class Mtd4Info
        {
            public long PartitionSize { get; set; }
            public int LastBlock { get; set; }
        }

        private static void EnsureMtdWriterSucceeded(ToolResult result)
        {
            var text = (result.StandardOutput ?? "") + "\n" + (result.StandardError ?? "");
            if (!Regex.IsMatch(text, @"(?i)(bad eraseblock number|too many bad blocks|ioctl failed|cannot write|mtd\s+(?:write|erase)(?:oob)?\s+failure)")) return;
            var detail = Regex.Replace(text.Trim(), @"\s+", " ");
            if (detail.Length > 500) detail = detail.Substring(0, 500);
            throw new InvalidOperationException("mtd4 刷写工具报告错误：" + detail);
        }

        private async Task<ToolResult> RunShellAsync(string serial, string command, CancellationToken token, bool logFailure)
        {
            var result = await adbService.ExecuteShellCommandAsync(serial, "/", command, token);
            if (logFailure && result.ExitCode != 0) LogService.Instance.Warn("MTD", "设备命令失败：" + command + "，" + GetErrorText(result));
            return result;
        }

        private async Task<string> ReadMd5Async(string serial, string path, CancellationToken token)
        {
            var result = await RunShellAsync(serial, "md5sum " + path, token, true);
            if (result.ExitCode != 0) throw new InvalidOperationException("无法读取设备固件 MD5：" + GetErrorText(result));
            var match = Regex.Match(result.StandardOutput ?? "", @"\b[0-9a-fA-F]{32}\b");
            if (!match.Success) throw new InvalidOperationException("设备返回的 MD5 格式无效。");
            return match.Value;
        }

        private static string GetErrorText(ToolResult result)
        {
            var text = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            return string.IsNullOrWhiteSpace(text) ? "" : " " + text.Trim();
        }

        private static string CalculateMd5(string path)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
        }

        private static ulong ReadUInt64(byte[] data, int offset)
        {
            return ReadUInt32(data, offset) | ((ulong)ReadUInt32(data, offset + 4) << 32);
        }

        private static void Step(Action<string> reportStatus, string message)
        {
            LogService.Instance.Info("MTD", message);
            if (reportStatus != null) reportStatus(message);
        }
    }
}

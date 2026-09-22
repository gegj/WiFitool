using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class MtdFlashService
    {
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

        public async Task RunAsync(string serial, string firmwarePath, string backupPath, CancellationToken token, Action<string> reportStatus)
        {
            ValidateFirmware(firmwarePath);
            if (string.Equals(Path.GetFullPath(firmwarePath), Path.GetFullPath(backupPath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("备份文件不能与所选固件相同。");

            var toolDirectory = Path.Combine(ToolEnvironment.Root, "mtd");
            var writerPath = Path.Combine(toolDirectory, "MTDWriter");
            var checkerPath = Path.Combine(toolDirectory, "MTDChecker");
            if (!File.Exists(writerPath) || !File.Exists(checkerPath)) throw new FileNotFoundException("工具环境中缺少 MTD 刷写文件，请先等待工具环境准备完成。", toolDirectory);

            var localMd5 = CalculateMd5(firmwarePath);
            Step(reportStatus, "正在上传固件并校验…");
            await adbService.UploadFileAsync(serial, "/tmp/mtd4.bin", firmwarePath, false, token);
            var deviceMd5 = await ReadMd5Async(serial, "/tmp/mtd4.bin", token);
            LogService.Instance.Info("MTD", "固件 MD5：本地 " + localMd5 + "，设备 " + deviceMd5);
            if (!string.Equals(localMd5, deviceMd5, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("固件上传校验失败。");

            Step(reportStatus, "正在备份设备 /dev/mtd4…");
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            await adbService.DownloadFileAsync(serial, "/dev/mtd4", backupPath, token);

            Step(reportStatus, "正在释放设备资源…");
            await RunShellAsync(serial, "cat /sbin/fota_release_space.sh|grep -v adbd|sh 2>/dev/null; killall -q goahead udhcpd dnsmasq iccid_check rmc zte_mifi zte_ufi zte_cpe 2>/dev/null", token, false);

            Step(reportStatus, "正在上传刷写工具…");
            await adbService.UploadFileAsync(serial, "/tmp/MTDWriter", writerPath, false, token);
            await RunRequiredShellAsync(serial, "chmod 777 /tmp/MTDWriter", "设置刷写工具权限失败。", token);
            await adbService.UploadFileAsync(serial, "/tmp/MTDChecker", checkerPath, false, token);
            await RunRequiredShellAsync(serial, "chmod 777 /tmp/MTDChecker", "设置校验工具权限失败。", token);

            Step(reportStatus, "正在准备刷写环境…");
            await RunShellAsync(serial, "killall -9 zte_ufi zte_mifi zte_cpe goahead 2>/dev/null", token, false);
            await adbService.UploadFileAsync(serial, "/tmp/new", firmwarePath, false, token);

            Step(reportStatus, "正在刷写 /dev/mtd4，请勿断开设备…");
            await RunRequiredShellAsync(serial, "/tmp/MTDWriter /dev/mtd4 0 999", "mtd4 刷写失败。", token);

            Step(reportStatus, "正在校验刷写结果…");
            await RunRequiredShellAsync(serial, "/tmp/MTDChecker -N -a /dev/mtd4 /tmp/new", "刷写结果校验失败。", token);

            Step(reportStatus, "正在清理临时文件…");
            await RunShellAsync(serial, "rm -rf /tmp/new /tmp/MTDWriter /tmp/MTDChecker /tmp/mtd4.bin 2>/dev/null", token, false);

            Step(reportStatus, "刷写完成，正在重启设备…");
            await adbService.RebootAsync(serial, token);
            LogService.Instance.Info("MTD", "MTD 刷写完成并已发送重启命令");
        }

        private async Task RunRequiredShellAsync(string serial, string command, string errorMessage, CancellationToken token)
        {
            var result = await RunShellAsync(serial, command, token, true);
            if (result.ExitCode != 0) throw new InvalidOperationException(errorMessage + GetErrorText(result));
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

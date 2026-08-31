using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal static class ToolEnvironment
    {
        // 工具包下载地址；主地址失败时使用兜底地址。
        private const string PrimaryDownloadUrl = "https://ilz.ly93.cc/531/39559978731/tools.zip";
        private const string FallbackDownloadUrl = "https://github.com/gegj/WiFitool/raw/refs/heads/main/tools.zip";
        private const string ToolsPackageSha256 = "C88259495BAD8FEFB3BE9A0011198336148756809AAC5EA4C37D6D5666B2A6C7";

        private static readonly string[] requiredFiles =
        {
            @"adb\adb.exe",
            @"adb\AdbWinApi.dll",
            @"adb\AdbWinUsbApi.dll",
            @"adbd\adbd",
            @"atweb\atweb",
            @"atweb\at.html",
            @"atweb\libamt.so",
            @"atweb\libcpnv.so",
            @"squashfs\unsquashfs.exe",
            @"squashfs\mksquashfs.exe",
            @"squashfs\msys-2.0.dll",
            @"squashfs\msys-gcc_s-seh-1.dll",
            @"squashfs\msys-lz4-1.dll",
            @"squashfs\msys-lzma-5.dll",
            @"squashfs\msys-lzo2-2.dll",
            @"squashfs\msys-z.dll",
            @"squashfs\msys-zstd-1.dll",
            @"mtd-utils\mkfs.jffs2.exe",
            @"mtd-utils\msys-2.0.dll",
            @"mtd-utils\msys-lzo2-2.dll",
            @"mtd-utils\msys-z.dll",
            @"jefferson\jefferson.exe"
        };
        private static readonly HttpClient client = CreateClient();

        public static string Root
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool", "tools"); }
        }

        public static bool IsReady()
        {
            foreach (var relative in requiredFiles)
            {
                if (!File.Exists(Path.Combine(Root, relative))) return false;
            }
            return true;
        }

        public static async Task EnsureReadyAsync(IProgress<int> progress = null)
        {
            if (IsReady()) return;
            var lastError = "";
            foreach (var url in new[] { PrimaryDownloadUrl, FallbackDownloadUrl })
            {
                try
                {
                    await DownloadAndExtractAsync(url, progress);
                    if (IsReady()) return;
                }
                catch (Exception ex) { lastError = ex.Message; }
            }
            throw new InvalidOperationException("工具下载失败：" + (string.IsNullOrEmpty(lastError) ? "无法连接下载地址" : lastError));
        }

        private static async Task DownloadAndExtractAsync(string url, IProgress<int> progress)
        {
            var folder = Path.GetDirectoryName(Root);
            Directory.CreateDirectory(folder);
            var zipPath = Path.Combine(folder, "tools-" + Guid.NewGuid().ToString("N") + ".zip");
            var extractDir = Path.Combine(folder, ".tools-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (progress != null) progress.Report(0);
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength;
                    using (var source = await response.Content.ReadAsStreamAsync())
                    using (var target = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[65536];
                        long copied = 0;
                        int read;
                        while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await target.WriteAsync(buffer, 0, read);
                            copied += read;
                            if (total.HasValue && total.Value > 0 && progress != null) progress.Report((int)Math.Min(100, copied * 100 / total.Value));
                        }
                    }
                }
                if (progress != null) progress.Report(100);
                if (!VerifyPackageHash(zipPath)) throw new InvalidOperationException("工具包校验失败，已拒绝使用该文件。");
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                var nested = Path.Combine(extractDir, "tools");
                var sourceDir = Directory.Exists(nested) ? nested : extractDir;
                if (Directory.Exists(Root)) TryDeleteDirectory(Root);
                Directory.Move(sourceDir, Root);
                if (!IsReady()) throw new InvalidOperationException("工具包内容不完整。");
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                TryDeleteDirectory(extractDir);
            }
        }

        private static HttpClient CreateClient()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WiFitool", "1.0"));
            return httpClient;
        }

        private static bool VerifyPackageHash(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "");
                return string.Equals(hash, ToolsPackageSha256, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
    }
}

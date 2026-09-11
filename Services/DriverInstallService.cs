using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class DriverInstallPackage
    {
        public DriverInstallPackage(string name, string description, string url, string plan, string[] orderedFiles = null, bool numericOrder = false, string[] silentFiles = null)
        {
            Name = name;
            Description = description;
            Url = url;
            Plan = plan;
            OrderedFiles = orderedFiles;
            NumericOrder = numericOrder;
            SilentFiles = silentFiles ?? new string[0];
        }

        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Url { get; private set; }
        public string Plan { get; private set; }
        public string[] OrderedFiles { get; private set; }
        public bool NumericOrder { get; private set; }
        public string[] SilentFiles { get; private set; }
    }

    internal sealed class DriverInstallService
    {
        private static readonly HttpClient client = CreateClient();
        private static readonly string cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool", "Drivers");
        private static readonly DriverInstallPackage[] packages =
        {
            new DriverInstallPackage(
                "中某微驱动",
                "安装中某微开发驱动，并自动运行无界面的串口驱动程序。",
                "https://github.com/gegj/WiFitool/raw/refs/heads/main/tools/drive/%E4%B8%AD%E6%9F%90%E5%BE%AE%E9%A9%B1%E5%8A%A8.zip",
                "ZXIC_Develop_Driver.exe 完成后，再运行 zxicser.exe。",
                new[] { "ZXIC_Develop_Driver.exe", "zxicser.exe" },
                false,
                new[] { "zxicser.exe" }),
            new DriverInstallPackage(
                "移远ASR专用驱动",
                "安装移远 ASR 设备使用的 Windows USB 驱动。",
                "https://github.com/gegj/WiFitool/raw/refs/heads/main/tools/drive/%E7%A7%BB%E8%BF%9CASR%E4%B8%93%E7%94%A8%E9%A9%B1%E5%8A%A8.zip",
                "按压缩包中的安装程序顺序执行。"),
            new DriverInstallPackage(
                "紫光驱动",
                "安装紫光设备的完整驱动包。",
                "https://github.com/gegj/WiFitool/raw/refs/heads/main/tools/drive/%E7%B4%AB%E5%85%89%E9%A9%B1%E5%8A%A8.zip",
                "按文件名中的数字编号顺序安装，每个程序完成后再继续。",
                null,
                true),
            new DriverInstallPackage(
                "通用安卓ADB驱动",
                "安装通用 Android ADB 驱动。",
                "https://github.com/gegj/WiFitool/raw/refs/heads/main/tools/drive/%E9%80%9A%E7%94%A8%E5%AE%89%E5%8D%93ADB%E9%A9%B1%E5%8A%A8.zip",
                "按压缩包中的安装程序执行。")
        };

        public IEnumerable<DriverInstallPackage> Packages
        {
            get { return packages; }
        }

        public async Task InstallAsync(DriverInstallPackage package, CancellationToken token, Action<string> reportStatus = null)
        {
            if (package == null) throw new ArgumentNullException("package");

            var workRoot = Path.Combine(Path.GetTempPath(), "WiFitool", "Drivers", Guid.NewGuid().ToString("N"));
            var zipPath = Path.Combine(workRoot, "driver.zip");
            var extractPath = Path.Combine(workRoot, "extracted");
            var cachedZipPath = GetCachedZipPath(package);
            try
            {
                Directory.CreateDirectory(workRoot);
                Directory.CreateDirectory(cacheDirectory);
                if (IsUsableArchive(cachedZipPath))
                {
                    zipPath = cachedZipPath;
                    Report(reportStatus, "正在使用已缓存的 " + package.Name + "…");
                    LogService.Instance.Info("DriverInstall", "使用已缓存驱动包：" + cachedZipPath);
                }
                else
                {
                    if (File.Exists(cachedZipPath)) TryDeleteFile(cachedZipPath);
                    Report(reportStatus, "正在下载 " + package.Name + "…");
                    LogService.Instance.Info("DriverInstall", "开始下载驱动包：" + package.Name);
                    LogService.Instance.Debug("DriverInstall", "下载地址：" + package.Url + "，临时目录：" + workRoot);
                    await DownloadAsync(package.Url, zipPath, token, reportStatus);
                    File.Move(zipPath, cachedZipPath);
                    zipPath = cachedZipPath;
                    LogService.Instance.Info("DriverInstall", "驱动包已缓存：" + cachedZipPath + "，大小 " + new FileInfo(zipPath).Length + " 字节");
                }

                Report(reportStatus, "正在解压 " + package.Name + "…");
                ExtractZip(zipPath, extractPath);
                LogService.Instance.Info("DriverInstall", "驱动包解压完成：" + package.Name);

                var installers = FindInstallers(package, extractPath);
                if (installers.Count == 0) throw new FileNotFoundException("驱动压缩包中没有找到 EXE 安装程序。", package.Name);
                LogService.Instance.Info("DriverInstall", "找到 " + installers.Count + " 个安装程序：" + string.Join(", ", installers.Select(Path.GetFileName)));

                for (var index = 0; index < installers.Count; index++)
                {
                    token.ThrowIfCancellationRequested();
                    var installer = installers[index];
                    var fileName = Path.GetFileName(installer);
                    var silent = package.SilentFiles.Any(file => string.Equals(file, fileName, StringComparison.OrdinalIgnoreCase));
                    Report(reportStatus, silent
                        ? "正在运行 " + fileName + "（无安装界面）…"
                        : "正在安装 " + fileName + "（" + (index + 1) + "/" + installers.Count + "）…");
                    LogService.Instance.Info("DriverInstall", "启动安装程序：" + package.Name + " / " + fileName + (silent ? "（无界面）" : ""));
                    var exitCode = await RunInstallerAsync(installer, token);
                    LogService.Instance.Info("DriverInstall", "安装程序已退出：" + fileName + "，退出码 " + exitCode);
                    if (!IsSuccessfulExitCode(exitCode))
                        throw new InvalidOperationException(fileName + " 安装失败，退出码：" + exitCode);
                }

                Report(reportStatus, package.Name + "安装完成");
                LogService.Instance.Info("DriverInstall", "驱动安装完成：" + package.Name);
            }
            catch (OperationCanceledException)
            {
                LogService.Instance.Warn("DriverInstall", "用户取消或程序关闭了驱动安装：" + package.Name);
                throw;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("DriverInstall", "驱动安装失败：" + package.Name, ex);
                throw;
            }
            finally
            {
                TryDeleteDirectory(workRoot);
            }
        }

        private static async Task DownloadAsync(string url, string destination, CancellationToken token, Action<string> reportStatus)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength;
                using (var source = await response.Content.ReadAsStreamAsync())
                using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
                {
                    var buffer = new byte[65536];
                    long copied = 0;
                    var lastPercent = -1;
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        await target.WriteAsync(buffer, 0, read, token);
                        copied += read;
                        if (total.HasValue && total.Value > 0)
                        {
                            var percent = (int)Math.Min(100, copied * 100 / total.Value);
                            if (percent != lastPercent && (percent % 5 == 0 || percent == 100))
                            {
                                lastPercent = percent;
                                Report(reportStatus, "正在下载驱动包… " + percent + "%");
                            }
                        }
                    }
                }
            }
        }

        private static void ExtractZip(string zipPath, string extractPath)
        {
            Directory.CreateDirectory(extractPath);
            var root = Path.GetFullPath(extractPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    var entryName = (entry.FullName ?? "").Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(entryName)) continue;
                    var destination = Path.GetFullPath(Path.Combine(extractPath, entryName));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("驱动压缩包包含无效路径：" + entry.FullName);
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }
                    var directory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    using (var source = entry.Open())
                    using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        source.CopyTo(target);
                    }
                }
            }
        }

        private static List<string> FindInstallers(DriverInstallPackage package, string extractPath)
        {
            var all = Directory.GetFiles(extractPath, "*.exe", SearchOption.AllDirectories).ToList();
            if (package.OrderedFiles != null)
            {
                var ordered = new List<string>();
                foreach (var fileName in package.OrderedFiles)
                {
                    var match = all.Where(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase)).OrderBy(path => path.Length).FirstOrDefault();
                    if (match == null) throw new FileNotFoundException("驱动压缩包中缺少文件：" + fileName, package.Name);
                    ordered.Add(match);
                }
                return ordered;
            }
            if (package.NumericOrder)
            {
                return all.OrderBy(GetFirstNumber).ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToList();
            }
            return all.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static int GetFirstNumber(string path)
        {
            var match = Regex.Match(Path.GetFileName(path) ?? "", @"\d+");
            int value;
            return match.Success && int.TryParse(match.Value, out value) ? value : int.MaxValue;
        }

        private static Task<int> RunInstallerAsync(string path, CancellationToken token)
        {
            return Task.Run(delegate
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                };
                using (var process = new Process { StartInfo = startInfo })
                {
                    if (!process.Start()) throw new InvalidOperationException("无法启动安装程序：" + Path.GetFileName(path));
                    while (!process.WaitForExit(200))
                    {
                        if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                    }
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }, token);
        }

        private static bool IsSuccessfulExitCode(int exitCode)
        {
            return exitCode == 0 || exitCode == 3010;
        }

        private static void Report(Action<string> reportStatus, string message)
        {
            if (reportStatus != null) reportStatus(message);
        }

        private static HttpClient CreateClient()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WiFitool", "0.49.0"));
            return httpClient;
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            try
            {
                Directory.Delete(path, true);
                LogService.Instance.Debug("DriverInstall", "临时驱动文件已清理：" + path);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("DriverInstall", "临时驱动文件清理失败：" + path, ex);
            }
        }

        private static string GetCachedZipPath(DriverInstallPackage package)
        {
            var fileName = string.IsNullOrWhiteSpace(package.Name) ? "driver" : package.Name;
            foreach (var invalid in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(invalid, '_');
            return Path.Combine(cacheDirectory, fileName + ".zip");
        }

        private static bool IsUsableArchive(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using (var archive = ZipFile.OpenRead(path)) return archive.Entries.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { LogService.Instance.Warn("DriverInstall", "缓存驱动包清理失败：" + path, ex); }
        }
    }
}

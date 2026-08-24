using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class UpdateService
    {
        // 发布仓库地址；GitHub 用户名或仓库名变更时只需修改这里。
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/gegj/WiFitool/releases/latest";
        private const string UpdateAssetName = "WiFitool.exe";
        private static readonly HttpClient client = CreateClient();

        public bool IsAutomaticCheckDue()
        {
            try
            {
                var cachePath = GetCachePath();
                if (!File.Exists(cachePath)) return true;
                long ticks;
                if (!long.TryParse(File.ReadAllText(cachePath), out ticks)) return true;
                return DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) >= TimeSpan.FromMinutes(10);
            }
            catch { return true; }
        }

        public void MarkCheckCompleted()
        {
            try
            {
                var cachePath = GetCachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllText(cachePath, DateTime.UtcNow.Ticks.ToString());
            }
            catch { }
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync(Version currentVersion)
        {
            try
            {
                var json = await client.GetStringAsync(LatestReleaseApiUrl);
                var release = ReadRelease(json);
                SemanticVersion remoteVersion;
                if (release == null || !SemanticVersion.TryParse(release.TagName, out remoteVersion)) return UpdateCheckResult.Failed("最新版本信息格式无效。");

                var localVersion = SemanticVersion.FromAssemblyVersion(currentVersion);
                if (remoteVersion.CompareTo(localVersion) <= 0) return UpdateCheckResult.NoUpdate();

                var asset = release.Assets == null ? null : release.Assets.FirstOrDefault(x => string.Equals(x.Name, UpdateAssetName, StringComparison.OrdinalIgnoreCase));
                if (asset == null || string.IsNullOrWhiteSpace(asset.DownloadUrl)) return UpdateCheckResult.Failed("最新版本缺少更新文件。");

                return UpdateCheckResult.Available(new UpdateInfo
                {
                    Version = remoteVersion.ToString(),
                    DownloadUrl = asset.DownloadUrl,
                    Notes = release.Body ?? ""
                });
            }
            catch (Exception ex) { return UpdateCheckResult.Failed("无法连接更新服务：" + ex.Message); }
        }

        public async Task<string> DownloadUpdateAsync(UpdateInfo update, IProgress<int> progress)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool", "Updates");
            Directory.CreateDirectory(folder);
            var temporaryPath = Path.Combine(folder, "WiFitool-" + Guid.NewGuid().ToString("N") + ".download");
            var downloadPath = Path.Combine(folder, "WiFitool-" + Guid.NewGuid().ToString("N") + ".exe");
            try
            {
                using (var response = await client.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength;
                    using (var source = await response.Content.ReadAsStreamAsync())
                    using (var target = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
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
                File.Move(temporaryPath, downloadPath);
                if (progress != null) progress.Report(100);
                return downloadPath;
            }
            catch
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                throw;
            }
        }

        public void ReplaceAfterExit(string downloadedPath)
        {
            if (!File.Exists(downloadedPath)) throw new FileNotFoundException("找不到已下载的更新文件。", downloadedPath);
            using (var current = Process.GetCurrentProcess())
            {
                var targetPath = current.MainModule.FileName;
                EnsureDirectoryWritable(Path.GetDirectoryName(targetPath));
                var scriptPath = Path.Combine(Path.GetTempPath(), "wifitool-update-" + Guid.NewGuid().ToString("N") + ".cmd");
                var backupPath = targetPath + ".wifitool-backup-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(scriptPath, CreateUpdateScript(current.Id, targetPath, downloadedPath, backupPath), Encoding.GetEncoding(936));
                Process.Start(new ProcessStartInfo { FileName = scriptPath, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            }
        }

        private static HttpClient CreateClient()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WiFitool", "1.0"));
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return httpClient;
        }

        private static GithubRelease ReadRelease(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(GithubRelease));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) return serializer.ReadObject(stream) as GithubRelease;
        }

        private static string GetCachePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool", "update-check.txt");
        }

        private static void EnsureDirectoryWritable(string directory)
        {
            var probe = Path.Combine(directory, ".wifitool-update-" + Guid.NewGuid().ToString("N"));
            try { using (File.Create(probe)) { } }
            catch { throw new UnauthorizedAccessException("软件所在文件夹没有写入权限，请将 WiFitool.exe 移到可写入的文件夹后再更新。"); }
            finally { try { if (File.Exists(probe)) File.Delete(probe); } catch { } }
        }

        private static string CreateUpdateScript(int processId, string targetPath, string downloadedPath, string backupPath)
        {
            return "@echo off\r\nchcp 936 >nul\r\nsetlocal\r\n:wait\r\ntasklist /FI \"PID eq " + processId + "\" /NH | find \"" + processId + "\" >nul\r\nif not errorlevel 1 (\r\n  timeout /t 1 /nobreak >nul\r\n  goto wait\r\n)\r\nmove /Y \"" + EscapeBatchValue(targetPath) + "\" \"" + EscapeBatchValue(backupPath) + "\" >nul 2>nul\r\nmove /Y \"" + EscapeBatchValue(downloadedPath) + "\" \"" + EscapeBatchValue(targetPath) + "\" >nul 2>nul\r\nif errorlevel 1 (\r\n  move /Y \"" + EscapeBatchValue(backupPath) + "\" \"" + EscapeBatchValue(targetPath) + "\" >nul 2>nul\r\n  del /Q \"" + EscapeBatchValue(downloadedPath) + "\" >nul 2>nul\r\n  start \"\" \"" + EscapeBatchValue(targetPath) + "\"\r\n  del \"%~f0\" >nul 2>nul\r\n  exit /b 1\r\n)\r\ndel /Q \"" + EscapeBatchValue(backupPath) + "\" >nul 2>nul\r\nstart \"\" \"" + EscapeBatchValue(targetPath) + "\"\r\ndel \"%~f0\" >nul 2>nul\r\n";
        }

        private static string EscapeBatchValue(string value) { return value.Replace("%", "%%"); }
    }

    internal sealed class UpdateInfo
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Notes { get; set; }
    }

    internal sealed class UpdateCheckResult
    {
        public UpdateInfo Update { get; private set; }
        public string Error { get; private set; }
        public bool HasUpdate { get { return Update != null; } }

        public static UpdateCheckResult Available(UpdateInfo update) { return new UpdateCheckResult { Update = update }; }
        public static UpdateCheckResult NoUpdate() { return new UpdateCheckResult(); }
        public static UpdateCheckResult Failed(string error) { return new UpdateCheckResult { Error = error }; }
    }

    [DataContract]
    internal sealed class GithubRelease
    {
        [DataMember(Name = "tag_name")]
        public string TagName { get; set; }

        [DataMember(Name = "body")]
        public string Body { get; set; }

        [DataMember(Name = "assets")]
        public List<GithubReleaseAsset> Assets { get; set; }
    }

    [DataContract]
    internal sealed class GithubReleaseAsset
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "browser_download_url")]
        public string DownloadUrl { get; set; }
    }

    internal sealed class SemanticVersion : IComparable<SemanticVersion>
    {
        private readonly int major;
        private readonly int minor;
        private readonly int patch;
        private readonly string[] prerelease;

        private SemanticVersion(int major, int minor, int patch, string[] prerelease)
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
            this.prerelease = prerelease;
        }

        public static SemanticVersion FromAssemblyVersion(Version version) { return new SemanticVersion(version.Major, version.Minor, Math.Max(0, version.Build), null); }

        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)) normalized = normalized.Substring(1);
            var metadataIndex = normalized.IndexOf('+');
            if (metadataIndex >= 0) normalized = normalized.Substring(0, metadataIndex);
            var prereleaseIndex = normalized.IndexOf('-');
            var prerelease = prereleaseIndex < 0 ? null : normalized.Substring(prereleaseIndex + 1).Split('.');
            var core = (prereleaseIndex < 0 ? normalized : normalized.Substring(0, prereleaseIndex)).Split('.');
            int major; int minor; int patch;
            if (core.Length != 3 || !int.TryParse(core[0], out major) || !int.TryParse(core[1], out minor) || !int.TryParse(core[2], out patch) || major < 0 || minor < 0 || patch < 0 || (prerelease != null && prerelease.Any(string.IsNullOrWhiteSpace))) return false;
            version = new SemanticVersion(major, minor, patch, prerelease);
            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            if (other == null) return 1;
            var result = major.CompareTo(other.major); if (result != 0) return result;
            result = minor.CompareTo(other.minor); if (result != 0) return result;
            result = patch.CompareTo(other.patch); if (result != 0) return result;
            if (prerelease == null && other.prerelease == null) return 0;
            if (prerelease == null) return 1;
            if (other.prerelease == null) return -1;
            for (var index = 0; index < Math.Min(prerelease.Length, other.prerelease.Length); index++)
            {
                long leftNumber; long rightNumber;
                var leftNumeric = long.TryParse(prerelease[index], out leftNumber);
                var rightNumeric = long.TryParse(other.prerelease[index], out rightNumber);
                if (leftNumeric && rightNumeric) { result = leftNumber.CompareTo(rightNumber); }
                else if (leftNumeric) result = -1;
                else if (rightNumeric) result = 1;
                else result = string.CompareOrdinal(prerelease[index], other.prerelease[index]);
                if (result != 0) return result;
            }
            return prerelease.Length.CompareTo(other.prerelease.Length);
        }

        public override string ToString()
        {
            var value = major + "." + minor + "." + patch;
            return prerelease == null ? value : value + "-" + string.Join(".", prerelease);
        }
    }
}

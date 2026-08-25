using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WiFitool.Models;

namespace WiFitool.Services
{
    internal sealed class AdbService
    {
        private readonly ToolRunner runner;
        private readonly string adbPath;
        private readonly string adbDirectory;
        private const int MaxStartupReferenceFiles = 64;
        private const int MaxStartupReferencePasses = 4;
        private static readonly string[] StartupScanFiles =
        {
            "/etc/inittab", "/etc/rc", "/etc/rcS", "/etc/rc.local", "/etc/init.d/rcS", "/etc/rc.d/rcS",
            "/etc_ro/inittab", "/etc_ro/rc", "/etc_ro/rcS", "/etc_ro/rc.local", "/etc_ro/init.d/rcS", "/etc_ro/rc.d/rcS",
            "/etc_rw/inittab", "/etc_rw/rc", "/etc_rw/rcS", "/etc_rw/rc.local", "/etc_rw/init.d/rcS", "/etc_rw/rc.d/rcS",
            "/etc/init.d/*", "/etc_ro/init.d/*", "/etc_rw/init.d/*",
            "/init.rc", "/system/etc/init.rc", "/system/etc/init/hw/init.rc", "/system/etc/init/*.rc",
            "/vendor/etc/init.rc", "/vendor/etc/init/hw/init.rc", "/vendor/etc/init/*.rc"
        };

        public AdbService(ToolRunner runner)
        {
            this.runner = runner; adbDirectory = Path.Combine(ToolEnvironment.Root, "adb"); adbPath = Path.Combine(adbDirectory, "adb.exe");
        }

        // 只结束本程序 tools 目录下的 adb.exe，不碰其他目录的 adb server。
        public void StopOwnedAdbServer()
        {
            if (!File.Exists(adbPath)) return;
            foreach (var process in Process.GetProcessesByName("adb"))
            {
                try
                {
                    if (process.MainModule != null && string.Equals(process.MainModule.FileName, adbPath, StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill();
                    }
                }
                catch { }
                finally { process.Dispose(); }
            }
        }

        public async Task<AdbStatusInfo> CheckStatusAsync(CancellationToken token)
        {
            if (!File.Exists(adbPath)) return new AdbStatusInfo { DeviceState = "no-port" };
            var result = await runner.RunAsync(adbPath, new[] { "devices", "-l" }, adbDirectory, token, null);
            var devices = ParseDevices(result.StandardOutput); var selected = devices.FirstOrDefault(d => d.State == "device") ?? devices.FirstOrDefault();
            if (selected == null) return new AdbStatusInfo { PortConnected = result.ExitCode == 0, DeviceState = result.ExitCode == 0 ? "no-device" : "no-port" };
            if (selected.State != "device") return new AdbStatusInfo { PortConnected = true, DeviceState = "offline", Serial = selected.Serial, TransportId = selected.TransportId };
            var target = selected.Serial;
            var deviceType = await ReadDeviceTypeAsync(target, token);
            var version = await ReadSoftwareVersionAsync(target, token);
            var spaces = await ReadSpacesAsync(target, token);
            return new AdbStatusInfo { PortConnected = true, DeviceState = "online", Serial = selected.Serial, TransportId = selected.TransportId, DeviceType = deviceType, SoftwareVersion = version, System = spaces.FirstOrDefault(x => x.Mount == "/system") ?? spaces.FirstOrDefault(x => x.Mount == "/"), Userdata = spaces.FirstOrDefault(x => x.Mount == "/mnt/userdata") ?? spaces.FirstOrDefault(x => x.Mount == "/userdata") ?? spaces.FirstOrDefault(x => x.Mount == "/data") };
        }

        public async Task<List<ProcessInfo>> ListProcessesAsync(string serial, CancellationToken token)
        {
            ValidateSerial(serial);
            var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "ps", "-A", "-o", "PID,PPID,USER,STAT,NAME,ARGS" }, adbDirectory, token, null);
            var list = ParseProcessOutput(result);
            if (result.ExitCode != 0 || list.Count == 0)
            {
                result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "ps" }, adbDirectory, token, null);
                list = ParseProcessOutput(result);
            }
            if (result.ExitCode != 0) throw new InvalidOperationException("无法读取设备进程：" + result.StandardError);
            return list.OrderBy(x => x.IsCoreProcess).ThenBy(x => x.Pid).ToList();
        }

        private static List<ProcessInfo> ParseProcessOutput(ToolResult result)
        {
            var list = new List<ProcessInfo>();
            foreach (var line in result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var item = ParseProcess(line);
                if (item != null) list.Add(item);
            }
            return list;
        }

        public async Task<ProcessScanResult> ListProcessesWithStartupAsync(string serial, CancellationToken token)
        {
            ValidateSerial(serial);
            var result = new ProcessScanResult();
            result.Processes.AddRange(await ListProcessesAsync(serial, token));
            var startupSources = await ScanStartupSourcesAsync(serial, token);
            foreach (var process in result.Processes)
            {
                var matches = startupSources.Where(x => StartupMatchesProcess(x.MatchedText, process)).ToList();
                if (matches.Count > 0) result.StartupSources[process.Pid] = matches;
            }
            return result;
        }

        public async Task StopProcessAsync(string serial, int pid, CancellationToken token)
        {
            ValidateSerial(serial); if (pid <= 1) throw new InvalidOperationException("PID 1 和系统根进程受保护，不能停止。"); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "kill", "-TERM", pid.ToString(CultureInfo.InvariantCulture) }, adbDirectory, token, null); if (result.ExitCode != 0) throw new InvalidOperationException("停止进程失败：" + result.StandardError);
        }

        private async Task<List<StartupSource>> ScanStartupSourcesAsync(string serial, CancellationToken token)
        {
            var result = new List<StartupSource>();
            var resultKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queuedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>();

            var initialSources = await ReadStartupSourcesBatchAsync(serial, StartupScanFiles, token);
            AddStartupSources(result, resultKeys, scannedPaths, initialSources);
            QueueStartupReferences(initialSources, scannedPaths, queuedPaths, pending);

            var referenceCount = 0;
            for (var pass = 0; pass < MaxStartupReferencePasses && pending.Count > 0 && referenceCount < MaxStartupReferenceFiles; pass++)
            {
                var batch = new List<string>();
                while (pending.Count > 0 && batch.Count < MaxStartupReferenceFiles - referenceCount)
                {
                    var path = pending.Dequeue();
                    if (scannedPaths.Add(path))
                    {
                        batch.Add(path);
                        referenceCount++;
                    }
                }
                if (batch.Count == 0) break;
                var batchSources = await ReadStartupSourcesBatchAsync(serial, batch, token);
                AddStartupSources(result, resultKeys, scannedPaths, batchSources);
                QueueStartupReferences(batchSources, scannedPaths, queuedPaths, pending);
            }
            return result;
        }

        private async Task<List<StartupSource>> ReadStartupSourcesBatchAsync(string serial, IEnumerable<string> paths, CancellationToken token)
        {
            var pathList = paths.Where(x => !string.IsNullOrWhiteSpace(x)).Select(QuoteStartupPath).ToList();
            if (pathList.Count == 0) return new List<StartupSource>();
            var command = "for file in " + string.Join(" ", pathList) + "; do [ -f \"$file\" ] && awk 'NF && $0 !~ /^[[:space:]]*#/ { print FILENAME \":\" FNR \":\" $0 }' \"$file\" 2>/dev/null; done | head -n 2000";
            var scan = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", command }, adbDirectory, token, null);
            if (scan.ExitCode != 0) throw new InvalidOperationException("读取启动来源失败：" + scan.StandardError);

            var result = new List<StartupSource>();
            foreach (var rawLine in scan.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Regex.Match(rawLine, @"^(.+?):([0-9]+):(.*)$");
                if (!match.Success) continue;
                int lineNumber;
                if (!int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out lineNumber)) continue;
                var path = NormalizeStartupPath(match.Groups[1].Value.Trim());
                var text = match.Groups[3].Value.Trim();
                if (path == null || lineNumber < 1 || string.IsNullOrWhiteSpace(text) || text.StartsWith("#", StringComparison.Ordinal)) continue;
                result.Add(new StartupSource { FilePath = path, LineNumber = lineNumber, MatchedText = text, MatchType = Path.GetFileName(path).IndexOf("init", StringComparison.OrdinalIgnoreCase) >= 0 ? "init" : "rc", Context = text });
            }
            return result;
        }

        private static void AddStartupSources(List<StartupSource> target, HashSet<string> keys, HashSet<string> scannedPaths, IEnumerable<StartupSource> sources)
        {
            foreach (var source in sources)
            {
                scannedPaths.Add(source.FilePath);
                var key = source.FilePath + "|" + source.LineNumber.ToString(CultureInfo.InvariantCulture);
                if (keys.Add(key)) target.Add(source);
            }
        }

        private static void QueueStartupReferences(IEnumerable<StartupSource> sources, HashSet<string> scannedPaths, HashSet<string> queuedPaths, Queue<string> pending)
        {
            foreach (var source in sources)
            {
                foreach (var reference in ExtractStartupReferences(source.MatchedText))
                {
                    foreach (var candidate in ResolveStartupReferencePaths(source.FilePath, reference))
                    {
                        if (!IsAllowedStartupPath(candidate) || scannedPaths.Contains(candidate) || !queuedPaths.Add(candidate)) continue;
                        pending.Enqueue(candidate);
                    }
                }
            }
        }

        private static bool IsAllowedStartupPath(string path)
        {
            return path == "/init.rc"
                || path.StartsWith("/etc/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/etc_ro/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/etc_rw/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/sbin/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/system/etc/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/vendor/etc/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartupMatchesProcess(string startupText, ProcessInfo process)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddProcessName(names, process.Name);
            AddProcessName(names, process.ExecutablePath);
            AddProcessName(names, FirstCommandToken(process.Arguments));
            foreach (var name in names)
            {
                if (!Regex.IsMatch(name, @"^[A-Za-z0-9_.+-]{2,}$")) continue;
                var pattern = @"(^|[/\s=""'])" + Regex.Escape(name) + @"(?=$|[\s""';&|()])";
                if (Regex.IsMatch(startupText ?? "", pattern, RegexOptions.IgnoreCase)) return true;
            }
            return false;
        }

        private static void AddProcessName(HashSet<string> names, string value)
        {
            var token = FirstCommandToken(value);
            if (string.IsNullOrWhiteSpace(token)) return;
            token = token.TrimStart('-').Replace('\\', '/');
            var slash = token.LastIndexOf('/');
            var name = slash >= 0 ? token.Substring(slash + 1) : token;
            if (!IsGenericLauncher(name)) names.Add(name);
        }

        private static bool IsGenericLauncher(string name)
        {
            return Regex.IsMatch(name ?? "", @"^(sh|ash|bash|dash|busybox|env|nohup|service|start-stop-daemon)$", RegexOptions.IgnoreCase);
        }

        private static IEnumerable<string> ExtractStartupReferences(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal)) yield break;
            var code = Regex.Replace(line, @"\s+#.*$", "");
            var references = new HashSet<string>(StringComparer.Ordinal);
            var interpreterMatches = Regex.Matches(code, @"(?:^|[\s;&|()])(?:source|sh|bash|\.)[\s]+[""']?([^""'\s;&|()]+)", RegexOptions.IgnoreCase);
            foreach (Match match in interpreterMatches) references.Add(match.Groups[1].Value);
            var scriptMatches = Regex.Matches(code, @"(?:^|[\s;&|()])[""']?([^""'\s;&|()]*\.sh)[""']?(?=$|[\s;&|()])", RegexOptions.IgnoreCase);
            foreach (Match match in scriptMatches) references.Add(match.Groups[1].Value);
            foreach (var reference in references) yield return reference;
        }

        private static IEnumerable<string> ResolveStartupReferencePaths(string sourceFile, string reference)
        {
            var value = (reference ?? "").Trim().Trim('\'', '"').TrimEnd(';', '&', '|', ')');
            if (value.Length == 0 || value.IndexOfAny(new[] { '$', '*', '?' }) >= 0) yield break;
            var slash = sourceFile.LastIndexOf('/');
            var directory = slash <= 0 ? "/" : sourceFile.Substring(0, slash);
            var candidates = value.StartsWith("/", StringComparison.Ordinal)
                ? new[] { value }
                : new[] { directory.TrimEnd('/') + "/" + value, "/etc/" + value, "/etc_ro/" + value, "/etc_rw/" + value, "/sbin/" + value };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                var normalized = NormalizeStartupPath(candidate);
                if (normalized != null && seen.Add(normalized)) yield return normalized;
            }
        }

        private static string NormalizeStartupPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var parts = new List<string>();
            foreach (var part in path.Replace('\\', '/').Split('/'))
            {
                if (string.IsNullOrWhiteSpace(part) || part == ".") continue;
                if (part == "..") return null;
                parts.Add(part);
            }
            return "/" + string.Join("/", parts.ToArray());
        }

        public async Task<List<WorkspaceEntry>> ListDirectoryAsync(string serial, string virtualPath, CancellationToken token)
        {
            ValidateSerial(serial); var path = NormalizeRemotePath(virtualPath); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "ls -lane " + QuoteShellArgument(path) }, adbDirectory, token, null); if (result.ExitCode != 0) throw new InvalidOperationException("无法读取设备目录：" + result.StandardError);
            var list = new List<WorkspaceEntry>();
            foreach (var rawLine in result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = Regex.Replace(rawLine.Trim(), "\\x1B\\[[0-9;]*[A-Za-z]", "");
                var m = Regex.Match(line, @"^([dl-])([rwxstST-]{9})\s+(\d+)\s+(\S+)\s+(\S+)\s+(\d+)\s+(.+)$"); if (!m.Success) continue;
                var dateAndName = m.Groups[7].Value.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); if (dateAndName.Length < 6) continue;
                var modified = string.Join(" ", dateAndName.Skip(1).Take(4));
                var name = string.Join(" ", dateAndName.Skip(5)); if (name == "." || name == ".." || name.EndsWith(" .", StringComparison.Ordinal) || name.EndsWith(" ..", StringComparison.Ordinal)) continue;
                var bareName = name; string target = null; var linkIndex = name.IndexOf(" -> ", StringComparison.Ordinal); if (linkIndex >= 0) { bareName = name.Substring(0, linkIndex); target = name.Substring(linkIndex + 4); }
                var mode = ParseMode(m.Groups[1].Value + m.Groups[2].Value); var childPath = path == "/" ? "/" + bareName : path + "/" + bareName;
                list.Add(new WorkspaceEntry { Name = bareName, Path = childPath, Kind = m.Groups[1].Value == "d" ? "目录" : m.Groups[1].Value == "l" ? "符号链接" : "文件", Size = long.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture), UnixMode = mode, UnixModeText = m.Groups[1].Value + m.Groups[2].Value, Owner = m.Groups[4].Value + ":" + m.Groups[5].Value, Modified = modified, Target = target });
            }
            return list.OrderBy(x => x.Kind == "符号链接" ? 2 : x.Kind == "目录" ? 0 : 1).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<byte[]> ReadFileAsync(string serial, string virtualPath, CancellationToken token)
        {
            ValidateSerial(serial);
            var path = NormalizeRemotePath(virtualPath);
            var tempDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool", "Temp");
            Directory.CreateDirectory(tempDirectory);
            var local = Path.Combine(tempDirectory, "adb-read-" + Guid.NewGuid().ToString("N"));
            try
            {
                var pull = await runner.RunAsync(adbPath, new[] { "-s", serial, "pull", path, local }, adbDirectory, token, null);
                if (pull.ExitCode != 0) throw new InvalidOperationException("读取设备文件失败：" + pull.StandardError);
                return File.ReadAllBytes(local);
            }
            finally
            {
                try { if (File.Exists(local)) File.Delete(local); } catch { }
            }
        }

        public async Task<bool> RemoteFileExistsAsync(string serial, string virtualPath, CancellationToken token)
        {
            ValidateSerial(serial);
            var path = NormalizeRemotePath(virtualPath);
            var command = "if [ -f " + QuoteShellArgument(path) + " ]; then echo __WIFITOOL_EXISTS__; else echo __WIFITOOL_MISSING__; fi";
            var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", command }, adbDirectory, token, null);
            return result.StandardOutput.IndexOf("__WIFITOOL_EXISTS__", StringComparison.Ordinal) >= 0;
        }

        public async Task<bool> RemoteDirectoryExistsAsync(string serial, string virtualPath, CancellationToken token)
        {
            ValidateSerial(serial);
            var path = NormalizeRemotePath(virtualPath);
            var command = "if [ -d " + QuoteShellArgument(path) + " ]; then echo __WIFITOOL_DIRECTORY__; else echo __WIFITOOL_MISSING__; fi";
            var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", command }, adbDirectory, token, null);
            return result.StandardOutput.IndexOf("__WIFITOOL_DIRECTORY__", StringComparison.Ordinal) >= 0;
        }

        public async Task<long> GetFreeBytesAsync(string serial, string virtualPath, CancellationToken token)
        {
            ValidateSerial(serial);
            var path = NormalizeRemotePath(virtualPath);
            var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "df -k " + QuoteShellArgument(path) }, adbDirectory, token, null);
            if (result.ExitCode != 0) throw new InvalidOperationException("无法读取设备可用空间：" + result.StandardError);
            foreach (var line in result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Reverse())
            {
                var match = Regex.Match(line.Trim(), @"^\S+\s+\d+\s+\d+\s+(\d+)\s+\d+%\s+.+$");
                if (match.Success) return long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 1024;
            }
            throw new InvalidOperationException("无法识别设备可用空间。");
        }

        public async Task WriteFileAsync(string serial, string virtualPath, byte[] bytes, CancellationToken token)
        {
            ValidateSerial(serial); var tempDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool", "Temp"); Directory.CreateDirectory(tempDirectory); var local = Path.Combine(tempDirectory, "adb-upload-" + Guid.NewGuid().ToString("N"));
            try { File.WriteAllBytes(local, bytes); await UploadFileAsync(serial, virtualPath, local, false, token); }
            finally { try { if (File.Exists(local)) File.Delete(local); } catch { } }
        }

        public async Task CreateFileAsync(string serial, string virtualPath, byte[] bytes, CancellationToken token)
        {
            ValidateSerial(serial); var path = NormalizeRemotePath(virtualPath); var tempDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool", "Temp"); Directory.CreateDirectory(tempDirectory); var local = Path.Combine(tempDirectory, "adb-hosts-" + Guid.NewGuid().ToString("N")); try { File.WriteAllBytes(local, bytes); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "push", local, path }, adbDirectory, token, null); if (result.ExitCode != 0) throw new InvalidOperationException("创建设备文件失败：" + result.StandardError); var chmod = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "chmod", "0644", path }, adbDirectory, token, null); if (chmod.ExitCode != 0) throw new InvalidOperationException("设置 hosts 权限失败：" + chmod.StandardError); } finally { try { if (File.Exists(local)) File.Delete(local); } catch { } }
        }

        public async Task UploadNewFileAsync(string serial, string virtualDirectory, string localPath, CancellationToken token)
        {
            var remote = CombineRemotePath(NormalizeRemotePath(virtualDirectory), Path.GetFileName(localPath));
            await UploadFileAsync(serial, remote, localPath, false, token);
        }

        public async Task UploadFileAsync(string serial, string virtualPath, string localPath, bool direct, CancellationToken token)
        {
            ValidateSerial(serial);
            if (!File.Exists(localPath)) throw new FileNotFoundException("找不到要上传的本地文件。", localPath);
            var remote = NormalizeRemotePath(virtualPath);
            var originalMode = await ReadRemoteModeAsync(serial, remote, token);
            var tempRemote = direct ? remote : CombineRemotePath(ParentRemotePath(remote), ".wifitool-upload-" + Guid.NewGuid().ToString("N"));
            try
            {
                var push = await runner.RunAsync(adbPath, new[] { "-s", serial, "push", localPath, tempRemote }, adbDirectory, token, null);
                if (push.ExitCode != 0) throw new InvalidOperationException("上传设备文件失败：" + push.StandardError);
                if (!direct)
                {
                    var move = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "mv " + QuoteShellArgument(tempRemote) + " " + QuoteShellArgument(remote) }, adbDirectory, token, null);
                    if (move.ExitCode != 0) throw new InvalidOperationException("替换设备文件失败：" + move.StandardError);
                }
                var mode = originalMode > 0 ? originalMode : Convert.ToInt32("755", 8);
                var chmod = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "chmod " + Convert.ToString(mode, 8) + " " + QuoteShellArgument(remote) }, adbDirectory, token, null);
                if (chmod.ExitCode != 0) throw new InvalidOperationException("设置文件权限失败：" + chmod.StandardError);
            }
            finally
            {
                if (!direct) try { runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "rm -f " + QuoteShellArgument(tempRemote) }, adbDirectory, CancellationToken.None, null).Wait(3000); } catch { }
            }
        }

        public async Task CreateDirectoryAsync(string serial, string virtualDirectory, string name, CancellationToken token)
        {
            ValidateSerial(serial); if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '/', '\\', '\r', '\n' }) >= 0) throw new InvalidOperationException("目录名称包含不允许的字符。"); var path = CombineRemotePath(NormalizeRemotePath(virtualDirectory), name); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "mkdir -p " + QuoteShellArgument(path) }, adbDirectory, token, null); if (result.ExitCode != 0) throw new InvalidOperationException("创建设备目录失败：" + result.StandardError); var chmod = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "chmod 755 " + QuoteShellArgument(path) }, adbDirectory, token, null); if (chmod.ExitCode != 0) throw new InvalidOperationException("设置目录权限失败：" + chmod.StandardError);
        }

        public async Task SetModeAsync(string serial, string virtualPath, int mode, CancellationToken token)
        {
            ValidateSerial(serial); if (mode < 0 || mode > 511) throw new InvalidOperationException("Unix 权限无效。"); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "chmod", Convert.ToString(mode, 8), NormalizeRemotePath(virtualPath) }, adbDirectory, token, null); if (result.ExitCode != 0) throw new InvalidOperationException("设置设备权限失败：" + result.StandardError);
        }

        private async Task<int> ReadRemoteModeAsync(string serial, string virtualPath, CancellationToken token)
        {
            var path = NormalizeRemotePath(virtualPath); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "ls -ld " + QuoteShellArgument(path) }, adbDirectory, token, null); if (result.ExitCode != 0) return 0; var line = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(); if (string.IsNullOrWhiteSpace(line)) return 0; var match = Regex.Match(line.Trim(), @"^([dl-][rwxstST-]{9})"); return match.Success ? ParseMode(match.Groups[1].Value) : 0;
        }

        public async Task DeleteRemoteAsync(string serial, string virtualPath, bool directory, CancellationToken token)
        {
            ValidateSerial(serial); var path = NormalizeRemotePath(virtualPath); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "rm", directory ? "-rf" : "-f", path }, adbDirectory, token, null); if (result.ExitCode != 0) throw new InvalidOperationException("删除设备文件失败：" + result.StandardError);
        }

        public async Task RenameAsync(string serial, string virtualPath, string newName, CancellationToken token)
        {
            ValidateSerial(serial); if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(new[] { '/', '\\', '\r', '\n' }) >= 0 || newName == "." || newName == "..") throw new InvalidOperationException("名称无效。");
            var oldPath = NormalizeRemotePath(virtualPath); var newPath = CombineRemotePath(ParentRemotePath(oldPath), newName);
            var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "mv " + QuoteShellArgument(oldPath) + " " + QuoteShellArgument(newPath) }, adbDirectory, token, null);
            if (result.ExitCode != 0) throw new InvalidOperationException("重命名失败：" + result.StandardError);
        }

        public async Task DownloadFileAsync(string serial, string virtualPath, string localPath, CancellationToken token)
        {
            ValidateSerial(serial); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "pull", NormalizeRemotePath(virtualPath), localPath }, adbDirectory, token, null); if (result.ExitCode != 0) throw new InvalidOperationException("下载设备文件失败：" + result.StandardError);
        }

        public async Task DownloadDirectoryAsync(string serial, string virtualPath, string localDirectory, CancellationToken token)
        {
            ValidateSerial(serial);
            Directory.CreateDirectory(localDirectory);
            var entries = await ListDirectoryAsync(serial, virtualPath, token);
            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                var localPath = CombineLocalPath(localDirectory, entry.Name);
                if (entry.Kind == "目录") await DownloadDirectoryAsync(serial, entry.Path, localPath, token);
                else await DownloadFileAsync(serial, entry.Path, localPath, token);
            }
        }

        public async Task RemountRootAsync(string serial, CancellationToken token)
        {
            ValidateSerial(serial); var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "mount", "-o", "remount,rw", "/" }, adbDirectory, token, null); if (result.ExitCode != 0) throw new InvalidOperationException("无法将系统根分区挂载为读写：" + result.StandardError);
        }

        public async Task<string> ExportMtdImageAsync(string serial, string softwareVersion, string folder, CancellationToken token)
        {
            ValidateSerial(serial);
            var proc = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "cat", "/proc/mtd" }, adbDirectory, token, null);
            if (proc.ExitCode != 0) throw new InvalidOperationException("无法读取设备 /proc/mtd：" + proc.StandardError);
            var entries = new List<MtdEntry>();
            foreach (var line in proc.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Regex.Match(line.Trim(), @"^mtd(\d+):\s+([0-9a-fA-F]+)\s+([0-9a-fA-F]+)\s+""([^""]*)"""); if (!match.Success) continue;
                long size; if (!long.TryParse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out size) || size <= 0) continue;
                entries.Add(new MtdEntry(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), size, match.Groups[4].Value));
            }
            if (entries.Count == 0) throw new InvalidDataException("设备没有可导出的 MTD 分区。");
            var offsets = new Dictionary<int, long>(); var sequential = 0L;
            foreach (var entry in entries)
            {
                var offsetText = await ReadFirstAsync(serial, new[] { "cat /sys/class/mtd/mtd" + entry.Number + "/offset", "cat /sys/class/mtd/mtd" + entry.Number + "/mtd_offset" }, token);
                long offset; if (!long.TryParse(offsetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset)) offset = sequential; offsets[entry.Number] = offset; sequential = Math.Max(sequential, offset + entry.Size);
            }
            var total = entries.Max(x => offsets[x.Number] + x.Size); Directory.CreateDirectory(folder); var cleanVersion = CleanFileName(string.IsNullOrWhiteSpace(softwareVersion) ? "未知版本" : softwareVersion); var outputPath = Path.Combine(folder, cleanVersion + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".bin"); if (File.Exists(outputPath)) throw new IOException("目标镜像已存在，请先移动或删除该文件：" + outputPath); var temp = outputPath + ".wifitool-tmp-" + Guid.NewGuid().ToString("N"); var partTemp = temp + ".part";
            try
            {
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.SetLength(total); output.Position = 0; var fill = Enumerable.Repeat((byte)0xFF, 1024 * 1024).ToArray(); var transfer = new byte[1024 * 1024]; for (var left = total; left > 0;) { var count = (int)Math.Min(fill.Length, left); output.Write(fill, 0, count); left -= count; }
                    foreach (var entry in entries)
                    {
                        try
                        {
                            token.ThrowIfCancellationRequested(); var pull = await runner.RunAsync(adbPath, new[] { "-s", serial, "pull", "/dev/mtd" + entry.Number, partTemp }, adbDirectory, token, null); if (pull.ExitCode != 0) throw new InvalidDataException("下载 MTD mtd" + entry.Number + " 失败：" + pull.StandardError);
                            using (var input = new FileStream(partTemp, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                if (input.Length < entry.Size) throw new InvalidDataException("读取 MTD mtd" + entry.Number + " 数据不完整。"); output.Position = offsets[entry.Number]; for (var left = entry.Size; left > 0;) { var wanted = (int)Math.Min(transfer.Length, left); var read = input.Read(transfer, 0, wanted); if (read <= 0) throw new InvalidDataException("读取 MTD mtd" + entry.Number + " 数据不完整。"); output.Write(transfer, 0, read); left -= read; }
                            }
                        }
                        finally
                        {
                            try { if (File.Exists(partTemp)) File.Delete(partTemp); } catch { }
                        }
                    }
                    output.Flush(true);
                }
                File.Move(temp, outputPath); return outputPath;
            }
            catch { try { if (File.Exists(temp)) File.Delete(temp); } catch { } throw; }
        }

        private async Task<string> ReadFirstAsync(string serial, string[] commands, CancellationToken token)
        {
            foreach (var command in commands) { var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", command }, adbDirectory, token, null); var value = result.StandardOutput.Trim(); if (result.ExitCode == 0 && value.Length > 0) return value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Last().Trim(); }
            return serial;
        }

        private async Task<string> ReadDeviceTypeAsync(string serial, CancellationToken token)
        {
            var value = await ReadNvValueAsync(serial, "zcgmi", token);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            return await ReadNvValueAsync(serial, "rootdev_modelname", token);
        }

        private async Task<string> ReadSoftwareVersionAsync(string serial, CancellationToken token)
        {
            var nvVersion = await ReadNvValueAsync(serial, "cr_version", token);
            if (!string.IsNullOrWhiteSpace(nvVersion)) return nvVersion;
            var forward = await runner.RunAsync(adbPath, new[] { "-s", serial, "forward", "tcp:0", "tcp:80" }, adbDirectory, token, null); var match = Regex.Match(forward.StandardOutput, @"(?m)^\s*(\d+)\s*$"); if (forward.ExitCode != 0 || !match.Success) return ""; var port = match.Groups[1].Value;
            try { return await Task.Run(delegate { var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/goform/goform_get_cmd_process?cmd=cr_version"); request.Timeout = 2500; using (var response = request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) { var text = reader.ReadToEnd(); var version = Regex.Match(text, "\\\"cr_version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\""); return version.Success ? version.Groups[1].Value.Trim() : ""; } }, token); }
            catch { return ""; }
            finally { try { runner.RunAsync(adbPath, new[] { "-s", serial, "forward", "--remove", "tcp:" + port }, adbDirectory, CancellationToken.None, null).Wait(3000); } catch { } }
        }

        private async Task<string> ReadNvValueAsync(string serial, string name, CancellationToken token)
        {
            var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "nv", "get", name }, adbDirectory, token, null);
            if (result.ExitCode != 0) return "";
            return result.StandardOutput.Trim();
        }

        private async Task<List<AdbPartitionSpace>> ReadSpacesAsync(string serial, CancellationToken token)
        {
            var result = await runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "df", "-k" }, adbDirectory, token, null); var list = new List<AdbPartitionSpace>(); foreach (var line in result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) { var m = Regex.Match(line.Trim(), @"^\S+\s+(\d+)\s+(\d+)\s+(\d+)\s+\d+%\s+(.+)$"); if (m.Success) list.Add(new AdbPartitionSpace { TotalBytes = long.Parse(m.Groups[1].Value) * 1024, UsedBytes = long.Parse(m.Groups[2].Value) * 1024, FreeBytes = long.Parse(m.Groups[3].Value) * 1024, Mount = m.Groups[4].Value.Trim() }); } return list;
        }

        private static ProcessInfo ParseProcess(string line)
        {
            var text = Regex.Replace(line.Trim(), "\\x1B\\[[0-9;]*[A-Za-z]", ""); if (!char.IsDigit(text.Length == 0 ? ' ' : text[0])) return null; var parts = Regex.Split(text, @"\s+"); if (parts.Length < 4) return null; int pid; if (!int.TryParse(parts[0], out pid)) return null;
            int ppid = 0; string user; string state; string name; string args;
            if (parts.Length >= 6 && int.TryParse(parts[1], out ppid)) { user = parts[2]; state = parts[3]; name = parts[4]; args = string.Join(" ", parts.Skip(5)); }
            else { user = parts.Length > 1 ? parts[1] : "?"; state = "?"; name = parts.Length > 3 ? parts[3] : parts[parts.Length - 1]; args = string.Join(" ", parts.Skip(3)); }
            var executablePath = FirstCommandToken(args);
            var core = pid == 1 || name == "init" || name.StartsWith("[", StringComparison.Ordinal) || name == "ueventd" || name == "servicemanager" || name.StartsWith("/sbin/", StringComparison.Ordinal) || name.StartsWith("/system/bin/", StringComparison.Ordinal) || name.StartsWith("/vendor/bin/", StringComparison.Ordinal); return new ProcessInfo { Pid = pid, ParentPid = ppid, User = user, State = state, Name = name, Arguments = args, ExecutablePath = executablePath, IsCoreProcess = core, RiskLevel = pid <= 1 ? "Critical" : core ? "Warning" : "Normal" };
        }
        private static string FirstCommandToken(string command) { var match = Regex.Match(command ?? "", @"^\s*([^\s]+)"); return match.Success ? match.Groups[1].Value.Trim('"', '\'') : ""; }
        private static List<AdbDeviceLine> ParseDevices(string output) { var list = new List<AdbDeviceLine>(); foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) { if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue; var parts = Regex.Split(line.Trim(), @"\s+"); if (parts.Length >= 2) { var transport = parts.FirstOrDefault(x => x.StartsWith("transport_id:", StringComparison.OrdinalIgnoreCase)); list.Add(new AdbDeviceLine { Serial = parts[0], State = parts[1], TransportId = transport == null ? "" : transport.Substring("transport_id:".Length) }); } } return list; }
        private static int ParseMode(string text) { var mode = 0; var values = new[] { 256, 128, 64, 32, 16, 8, 4, 2, 1 }; for (var i = 0; i < Math.Min(9, text.Length - 1); i++) { var c = text[i + 1]; if (c != '-') mode += values[i]; } return mode; }
        private static string QuoteShellArgument(string value) { return "'" + (value ?? "").Replace("'", "'\"'\"'") + "'"; }
        private static string CombineRemotePath(string directory, string name) { var parent = NormalizeRemotePath(directory); return parent == "/" ? "/" + name : parent.TrimEnd('/') + "/" + name; }
        private static string ParentRemotePath(string path) { var normalized = NormalizeRemotePath(path).TrimEnd('/'); var index = normalized.LastIndexOf('/'); return index <= 0 ? "/" : normalized.Substring(0, index); }
        private static string CombineLocalPath(string directory, string name) { if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidOperationException("设备文件名无法保存到 Windows：" + name); return Path.Combine(directory, name); }
        private static string QuoteStartupPath(string value) { return value.IndexOfAny(new[] { '*', '?' }) >= 0 ? value : QuoteShellArgument(value); }
        private static string NormalizeRemotePath(string path) { if (string.IsNullOrWhiteSpace(path)) return "/"; var value = "/" + path.Replace('\\', '/').Trim('/'); if (value.Contains("..")) throw new InvalidOperationException("设备路径包含不允许的内容。"); return value == "/" ? "/" : value; }
        private static void ValidateSerial(string serial) { if (string.IsNullOrWhiteSpace(serial) || serial.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"', '\'', ';' }) >= 0 || (serial.StartsWith("transport:", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(serial.Substring("transport:".Length), @"^\d+$"))) throw new InvalidOperationException("ADB 设备选择器无效。"); }
        private sealed class AdbDeviceLine { public string Serial; public string State; public string TransportId; }
        private sealed class MtdEntry { public int Number; public long Size; public string Name; public MtdEntry(int number, long size, string name) { Number = number; Size = size; Name = name; } }
        private static string CleanFileName(string value) { foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_'); return value; }
    }
}

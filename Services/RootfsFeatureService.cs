using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WiFitool.Models;

namespace WiFitool.Services
{
    internal sealed class RootfsFeatureService
    {
        private readonly string tools = ToolEnvironment.Root;
        private static readonly string[] AtWebLegacyFiles =
        {
            "/sbin/daemon.sh", "/sbin/zte_webadmin.sh", "/sbin/webserver", "/sbin/atweb",
            "/bin/at_server", "/sbin/zte_webdaemon", "/sbin/zte_webadmin",
            "/etc_ro/web/at.html", "/etc_ro/web/debug.html", "/etc_ro/web/at_info.html",
            "/etc_ro/web/atweb.html", "/etc_ro/web/tools.html"
        };
        private static readonly string[] AtWebStartupKeywords =
        {
            "webserver", "daemon.sh", "zte_webadmin.sh", "atweb", "zte_webadmin", "zte_webdaemon"
        };

        public Task ApplyAdbdAsync(PartitionInfo partition, WorkspaceSession session)
        {
            return Task.Run(delegate
            {
                string root; if (!session.ExtractedDirectories.TryGetValue(partition.Name, out root)) throw new InvalidOperationException("请先解包 rootfs。");
                var source = Path.Combine(tools, "adbd", "adbd"); if (!File.Exists(source)) throw new FileNotFoundException("adbd 工具不存在。", source);
                var target = Path.Combine(root, "bin", "adbd"); Directory.CreateDirectory(Path.GetDirectoryName(target)); if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal); File.Copy(source, target, true);
                new WorkspaceMetadataService().Update(root, "/bin/adbd", new WorkspaceMetadata { Kind = "file", Mode = Convert.ToInt32("775", 8), Owner = "0:0", Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                var rc = Path.Combine(root, "etc", "rc"); if (File.Exists(rc)) { var text = File.ReadAllText(rc); if (text.IndexOf("/bin/adbd", StringComparison.OrdinalIgnoreCase) < 0) File.AppendAllText(rc, "\n/bin/adbd &\n", Encoding.UTF8); }
                partition.Modified = true;
            });
        }

        public Task ApplyAtWebAsync(PartitionInfo partition, WorkspaceSession session)
        {
            return Task.Run(delegate
            {
                string root; if (!session.ExtractedDirectories.TryGetValue(partition.Name, out root)) throw new InvalidOperationException("请先解包 rootfs。");
                var binary = Path.Combine(tools, "atweb", "atweb"); var html = Path.Combine(tools, "atweb", "at.html"); if (!File.Exists(binary) || !File.Exists(html)) throw new FileNotFoundException("ATWeb 资源不完整。");
                var metadataService = new WorkspaceMetadataService();
                var metadata = metadataService.Load(root);
                foreach (var virtualPath in AtWebLegacyFiles) DeleteFile(root, virtualPath, metadata);
                CleanStartupFile(root, "/etc/rc");
                CleanStartupFile(root, "/sbin/rm_dev.sh");

                var libDirectory = Path.Combine(root, "lib"); Directory.CreateDirectory(libDirectory);
                CopyIfMissing(Path.Combine(tools, "atweb", "libamt.so"), Path.Combine(libDirectory, "libamt.so"));
                CopyIfMissing(Path.Combine(tools, "atweb", "libcpnv.so"), Path.Combine(libDirectory, "libcpnv.so"));

                var targetBinary = Path.Combine(root, "sbin", "atweb"); var targetHtml = Path.Combine(root, "etc_ro", "web", "at.html"); Directory.CreateDirectory(Path.GetDirectoryName(targetBinary)); Directory.CreateDirectory(Path.GetDirectoryName(targetHtml)); File.Copy(binary, targetBinary, true); File.Copy(html, targetHtml, true);
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                metadata[WorkspaceMetadataService.Normalize("/sbin/atweb")] = new WorkspaceMetadata { Kind = "file", Mode = Convert.ToInt32("775", 8), Owner = "0:0", Modified = now };
                metadata[WorkspaceMetadataService.Normalize("/etc_ro/web/at.html")] = new WorkspaceMetadata { Kind = "file", Mode = Convert.ToInt32("775", 8), Owner = "0:0", Modified = now };
                InsertMenuEntries(root);
                metadataService.Save(root, metadata);

                var script = Path.Combine(root, "sbin", "rm_dev.sh"); if (File.Exists(script)) { var text = File.ReadAllText(script); var line = "(sleep 20; /sbin/atweb >/dev/null 2>&1) &"; if (text.IndexOf(line, StringComparison.Ordinal) < 0) File.AppendAllText(script, "\n" + line + "\n", Encoding.UTF8); }
                partition.Modified = true;
            });
        }

        private static void DeleteFile(string root, string virtualPath, Dictionary<string, WorkspaceMetadata> metadata)
        {
            var path = Path.Combine(root, virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); }
            metadata.Remove(WorkspaceMetadataService.Normalize(virtualPath));
        }

        private static void CleanStartupFile(string root, string virtualPath)
        {
            var path = Path.Combine(root, virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path).ToList();
            var remove = new HashSet<int>();
            for (var i = 0; i < lines.Count; i++)
            {
                if (!AtWebStartupKeywords.Any(x => lines[i].IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                remove.Add(i);
                for (var previous = i - 1; previous >= 0; previous--)
                {
                    if (string.IsNullOrWhiteSpace(lines[previous])) continue;
                    if (Regex.IsMatch(lines[previous].Trim(), @"^(?:sleep\s+\d+|\(\s*sleep\s+\d+[^\)]*\)\s*;?)", RegexOptions.IgnoreCase)) remove.Add(previous);
                    break;
                }
            }
            if (remove.Count == 0) return;
            File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllLines(path, lines.Where((line, index) => !remove.Contains(index)), new UTF8Encoding(false));
        }

        private static void CopyIfMissing(string source, string target)
        {
            if (File.Exists(target)) return;
            if (!File.Exists(source)) throw new FileNotFoundException("ATWeb 依赖库不存在。", source);
            File.Copy(source, target, false);
        }

        private static void InsertMenuEntries(string root)
        {
            TryInsertMenu(root, "/etc_ro/web/subpg/main.html", "data-trans=\"quick_setting\" class=\"cFFCE2B\"></a></li>");
            TryInsertMenu(root, "/etc_ro/web/subpg/sim_abnormal.html", "href=\"#wlan_sleep\"></a></li>");
            TryInsertMenu(root, "/etc_ro/web/tmpl/home.html", "data-trans=\"quick_setting\"></a></li>");
            TryInsertMenu(root, "/etc_ro/web/tmpl/nosimcard.html", "data-trans=\"advanced_settings\"></a></li>");
        }

        private static void TryInsertMenu(string root, string virtualPath, string marker)
        {
            var path = Path.Combine(root, virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return;
            var text = File.ReadAllText(path);
            if (text.IndexOf(":9090/at.html", StringComparison.OrdinalIgnoreCase) >= 0) return;
            var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return;
            var insert = "<li><a href=\":9090/at.html\" data-trans=\"AT_WEB\" class=\"c008AFF\"></a></li>";
            var position = index + marker.Length;
            File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllText(path, text.Substring(0, position) + insert + text.Substring(position), Encoding.UTF8);
        }
    }
}

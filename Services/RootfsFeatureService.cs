using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WiFitool.Models;

namespace WiFitool.Services
{
    internal sealed class RootfsFeatureService
    {
        private readonly string tools = ToolEnvironment.Root;

        public Task ApplyAdbdAsync(PartitionInfo partition, WorkspaceSession session)
        {
            return Task.Run(delegate
            {
                string root; if (!session.ExtractedDirectories.TryGetValue(partition.Name, out root)) throw new InvalidOperationException("请先解包 rootfs。");
                var source = Path.Combine(tools, "adbd", "adbd"); if (!File.Exists(source)) throw new FileNotFoundException("adbd 工具不存在。", source);
                var target = Path.Combine(root, "bin", "adbd"); Directory.CreateDirectory(Path.GetDirectoryName(target)); if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal); File.Copy(source, target, true);
                new WorkspaceMetadataService().Update(root, "/bin/adbd", new WorkspaceMetadata { Kind = "file", Mode = Convert.ToInt32("755", 8), Owner = "0:0", Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
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
                var targetBinary = Path.Combine(root, "sbin", "atweb"); var targetHtml = Path.Combine(root, "etc_ro", "web", "at.html"); Directory.CreateDirectory(Path.GetDirectoryName(targetBinary)); Directory.CreateDirectory(Path.GetDirectoryName(targetHtml)); File.Copy(binary, targetBinary, true); File.Copy(html, targetHtml, true); var metadata = new WorkspaceMetadataService(); metadata.Update(root, "/sbin/atweb", new WorkspaceMetadata { Kind = "file", Mode = Convert.ToInt32("755", 8), Owner = "0:0", Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }); metadata.Update(root, "/etc_ro/web/at.html", new WorkspaceMetadata { Kind = "file", Mode = Convert.ToInt32("644", 8), Owner = "0:0", Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                var script = Path.Combine(root, "sbin", "rm_dev.sh"); if (File.Exists(script)) { var text = File.ReadAllText(script); var line = "(sleep 20; /sbin/atweb >/dev/null 2>&1) &"; if (text.IndexOf(line, StringComparison.Ordinal) < 0) File.AppendAllText(script, "\n" + line + "\n", Encoding.UTF8); }
                partition.Modified = true;
            });
        }
    }
}

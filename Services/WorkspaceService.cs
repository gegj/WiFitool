using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WiFitool.Models;

namespace WiFitool.Services
{
    internal sealed class WorkspaceService
    {
        private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool");
        private string Workspaces { get { return Path.Combine(root, "Workspaces"); } }

        public Task<WorkspaceSession> CreateAsync(ImageInfo image, CancellationToken token)
        {
            return Task.Run(delegate
            {
                Directory.CreateDirectory(Workspaces);
                var safe = SafeName(Path.GetFileNameWithoutExtension(image.Name));
                var path = Path.Combine(Workspaces, safe + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path);
                var session = new WorkspaceSession { RootPath = path, OriginalImagePath = image.Path };
                try
                {
                    var partitionDir = Path.Combine(path, "partitions"); Directory.CreateDirectory(partitionDir);
                    using (var source = new FileStream(image.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        for (var index = 0; index < image.Partitions.Count; index++)
                        {
                            token.ThrowIfCancellationRequested(); var p = image.Partitions[index]; var target = Path.Combine(partitionDir, index.ToString("D2") + "-" + SafeName(p.Name) + ".bin");
                            CopyRange(source, target, p.Offset, p.Size, token); session.PartitionFiles[p.Name] = target;
                        }
                    }
                    Directory.CreateDirectory(Path.Combine(path, "extracted")); Directory.CreateDirectory(Path.Combine(path, "repacked")); Directory.CreateDirectory(Path.Combine(path, "metadata"));
                    return session;
                }
                catch { Cleanup(session); throw; }
            }, token);
        }

        public void Cleanup(WorkspaceSession session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.RootPath)) return;
            var parent = Path.GetFullPath(Workspaces).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(session.RootPath).TrimEnd(Path.DirectorySeparatorChar);
            if (path.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) TryDelete(path);
        }

        public void CleanupStaleWorkspaces()
        {
            try
            {
                if (!Directory.Exists(Workspaces)) return;
                foreach (var directory in Directory.GetDirectories(Workspaces))
                {
                    try
                    {
                        var info = new DirectoryInfo(directory);
                        if (DateTime.Now - info.LastWriteTime > TimeSpan.FromDays(1)) Directory.Delete(directory, true);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public Task ExportAsync(ImageInfo image, WorkspaceSession session, string destination, CancellationToken token)
        {
            return Task.Run(delegate
            {
                var source = Path.GetFullPath(image.Path); var target = Path.GetFullPath(destination);
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("导出路径不能覆盖原始镜像。");
                Directory.CreateDirectory(Path.GetDirectoryName(target)); var temp = target + ".wifitool-tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.Copy(source, temp, false);
                    using (var output = new FileStream(temp, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        foreach (var part in image.Partitions.Where(p => p.Repacked))
                        {
                            token.ThrowIfCancellationRequested(); string repacked;
                            if (!session.RepackedFiles.TryGetValue(part.Name, out repacked) || !File.Exists(repacked)) throw new InvalidDataException("分区重打包文件不存在：" + part.Name);
                            var length = new FileInfo(repacked).Length; if (length > part.Size) throw new InvalidDataException("分区 " + part.Name + " 重打包后超过容量。");
                            output.Position = part.Offset; using (var input = new FileStream(repacked, FileMode.Open, FileAccess.Read, FileShare.Read)) CopyStream(input, output, length, token); Fill(output, part.Size - length, token);
                        }
                        output.Flush(true);
                    }
                    ReplaceFile(temp, target);
                }
                catch { TryDeleteFile(temp); throw; }
            }, token);
        }

        public Task ExportPartitionAsync(string sourceImage, PartitionInfo partition, string destination, CancellationToken token)
        {
            return Task.Run(delegate
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)); var temp = destination + ".wifitool-tmp-" + Guid.NewGuid().ToString("N");
                try { using (var source = new FileStream(sourceImage, FileMode.Open, FileAccess.Read, FileShare.Read)) { source.Position = partition.Offset; using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) CopyStream(source, output, partition.Size, token); } ReplaceFile(temp, destination); }
                catch { TryDeleteFile(temp); throw; }
            }, token);
        }

        private static void CopyRange(FileStream source, string target, long offset, long length, CancellationToken token) { source.Position = offset; using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None)) CopyStream(source, output, length, token); }
        private static void CopyStream(Stream source, Stream output, long length, CancellationToken token) { var buffer = new byte[1024 * 1024]; var remaining = length; while (remaining > 0) { token.ThrowIfCancellationRequested(); var count = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining)); if (count == 0) throw new EndOfStreamException(); output.Write(buffer, 0, count); remaining -= count; } }
        private static void Fill(Stream output, long length, CancellationToken token) { var buffer = Enumerable.Repeat((byte)0xFF, 1024 * 1024).ToArray(); while (length > 0) { token.ThrowIfCancellationRequested(); var count = (int)Math.Min(buffer.Length, length); output.Write(buffer, 0, count); length -= count; } }
        private static void ReplaceFile(string temp, string target) { if (File.Exists(target)) File.Delete(target); File.Move(temp, target); }
        private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
        private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
        private static string SafeName(string value) { var invalid = Path.GetInvalidFileNameChars(); return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c)); }
    }
}

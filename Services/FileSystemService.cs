using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WiFitool.Models;

namespace WiFitool.Services
{
    internal sealed class FileSystemService
    {
        private readonly ToolRunner runner;
        private readonly string toolsRoot;
        private readonly WorkspaceMetadataService metadataService = new WorkspaceMetadataService();

        public FileSystemService(ToolRunner runner)
        {
            this.runner = runner;
            toolsRoot = ToolEnvironment.Root;
        }

        public Task ExtractAsync(PartitionInfo partition, WorkspaceSession session, CancellationToken token, Action<string, bool> output)
        {
            return Task.Run(async delegate
            {
                string imagePath;
                if (!session.PartitionFiles.TryGetValue(partition.Name, out imagePath)) throw new InvalidOperationException("分区切片不存在：" + partition.Name);
                var destination = Path.Combine(session.RootPath, "extracted", SafeName(partition.Name));
                if (Directory.Exists(destination)) throw new InvalidOperationException("分区已经解包：" + partition.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                ToolResult result;
                if (partition.FileSystem == "SquashFS")
                {
                    var unsquashfs = Path.Combine(toolsRoot, "squashfs", "unsquashfs.exe");
                    var pseudo = Path.Combine(session.RootPath, "metadata", SafeName(partition.Name) + ".pseudo");
                    var pseudoResult = await runner.RunAsync(unsquashfs, new[] { "-pf", pseudo, imagePath }, Path.GetDirectoryName(unsquashfs), token, output);
                    if ((pseudoResult.ExitCode != 0 && pseudoResult.ExitCode != 2) || !File.Exists(pseudo)) throw new InvalidDataException("SquashFS 元数据导出失败：" + pseudoResult.StandardError);
                    session.MetadataFiles[partition.Name] = pseudo;
                    result = await runner.RunAsync(unsquashfs, new[] { "-d", destination, "-ignore-errors", imagePath }, Path.GetDirectoryName(unsquashfs), token, output);
                }
                else if (partition.FileSystem == "JFFS2")
                {
                    var jefferson = Path.Combine(toolsRoot, "jefferson", "jefferson.exe");
                    result = await runner.RunAsync(jefferson, new[] { "-d", destination, imagePath }, Path.GetDirectoryName(jefferson), token, output, Encoding.Default);
                    var jffsOutput = (result.StandardOutput ?? "") + "\n" + (result.StandardError ?? "");
                    if (result.ExitCode != 0 || !Directory.Exists(destination) || jffsOutput.IndexOf("Decompression error", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        TryDeleteDirectory(destination);
                        throw new InvalidDataException("JFFS2 分区解包失败：" + jffsOutput.Trim());
                    }
                    var devtable = Path.Combine(session.RootPath, "metadata", SafeName(partition.Name) + ".devtable");
                    var symlinkTargets = CreateJffs2DevTable(imagePath, devtable, destination, partition.LittleEndian);
                    if (File.Exists(devtable)) { session.MetadataFiles[partition.Name] = devtable; CreateDevMetadata(destination, devtable, symlinkTargets); }
                }
                else throw new InvalidOperationException("分区文件系统不支持解包：" + partition.FileSystem);
                if ((result.ExitCode != 0 && result.ExitCode != 2) || !Directory.Exists(destination)) throw new InvalidDataException("分区解包失败：" + result.StandardError);
                if (partition.FileSystem == "SquashFS") CreateSquashMetadata(destination, session.MetadataFiles[partition.Name]);
                NormalizeExtractedSymlinks(destination);
                session.ExtractedDirectories[partition.Name] = destination;
                partition.Extracted = true;
            }, token);
        }

        public Task RepackAsync(PartitionInfo partition, WorkspaceSession session, CancellationToken token)
        {
            return Task.Run(async delegate
            {
                string source;
                if (!session.ExtractedDirectories.TryGetValue(partition.Name, out source)) throw new InvalidOperationException("分区尚未解包：" + partition.Name);
                var target = Path.Combine(session.RootPath, "repacked", SafeName(partition.Name) + ".bin");
                ToolResult result;
                if (partition.FileSystem == "SquashFS")
                {
                    var tool = Path.Combine(toolsRoot, "squashfs", "mksquashfs.exe");
                    var metadata = metadataService.Load(source);
                    var staging = CreateStagingDirectory(source, Path.Combine(session.RootPath, "repacked"), null);
                    RemoveStagingSymlinks(staging, metadata);
                    var args = new List<string> { staging, target, "-noappend", "-processors", "1" };
                    if (partition.BlockSize > 0) { args.Add("-b"); args.Add(partition.BlockSize.ToString()); }
                    if (!string.IsNullOrWhiteSpace(partition.Compression) && partition.Compression != "--" && !partition.Compression.StartsWith("未知")) { args.Add("-comp"); args.Add(partition.Compression); }
                    string pseudo, repackPseudo = null;
                    try
                    {
                        if (!session.MetadataFiles.TryGetValue(partition.Name, out pseudo) || !File.Exists(pseudo)) throw new InvalidDataException("缺少 SquashFS 元数据，拒绝不保真的重打包。");
                        repackPseudo = CreateRepackSquashPseudo(pseudo, source, staging, Path.Combine(session.RootPath, "repacked"));
                        args.Add("-pf"); args.Add(repackPseudo); args.Add("-pseudo-override");
                        result = await runner.RunAsync(tool, args, Path.GetDirectoryName(tool), token, null);
                    }
                    finally { TryDeleteFile(repackPseudo); DeleteStagingDirectory(staging); }
                }
                else if (partition.FileSystem == "JFFS2")
                {
                    var tool = Path.Combine(toolsRoot, "mtd-utils", "mkfs.jffs2.exe");
                    var staging = CreateStagingDirectory(source, Path.Combine(session.RootPath, "repacked"), metadataService.Load(source));
                    var args = new List<string> { "-r", staging, "-o", target, partition.LittleEndian ? "-l" : "-b", "-e", "0x" + InferEraseBlock(partition).ToString("X"), "-s", "0x" + InferPageSize(partition).ToString("X") };
                    string devtable, repackDevtable = null;
                    try
                    {
                        if (session.MetadataFiles.TryGetValue(partition.Name, out devtable) && File.Exists(devtable))
                        {
                            NormalizeJffs2DevTable(devtable);
                            repackDevtable = CreateRepackJffs2DevTable(devtable, source, staging, Path.Combine(session.RootPath, "repacked"));
                            args.Add("-D"); args.Add(repackDevtable);
                        }
                        result = await runner.RunAsync(tool, args, Path.GetDirectoryName(tool), token, null);
                    }
                    finally { TryDeleteFile(repackDevtable); DeleteStagingDirectory(staging); }
                }
                else throw new InvalidOperationException("分区文件系统不支持重新打包：" + partition.FileSystem);
                if (result.ExitCode != 0 || !File.Exists(target)) throw new InvalidDataException("重新打包失败：" + result.StandardError);
                session.RepackedFiles[partition.Name] = target; partition.Repacked = true;
            }, token);
        }

        private static string SafeName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars(); var result = "";
            foreach (var c in value) result += invalid.Contains(c) ? '_' : c;
            return result;
        }

        private static int InferEraseBlock(PartitionInfo partition)
        {
            if (partition.Jffs2EraseBlockSize >= 4096) return partition.Jffs2EraseBlockSize;
            return 64 * 1024;
        }

        private static int InferPageSize(PartitionInfo partition) { return partition.Jffs2PageSize >= 1024 ? partition.Jffs2PageSize : 4096; }

        private static void NormalizeJffs2DevTable(string path)
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            var changed = false;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (line.Length == 0) continue;
                var fields = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 5) throw new InvalidDataException("JFFS2 设备表第 " + (index + 1) + " 行格式无效。");
                var entryPath = fields[0].Replace('\\', '/').Trim();
                if (!entryPath.StartsWith("/", StringComparison.Ordinal)) { entryPath = "/" + entryPath.TrimStart('/'); changed = true; }
                if (entryPath == "/") { entryPath = "/."; changed = true; }
                fields[0] = entryPath;
                var normalized = string.Join("\t", fields);
                if (lines[index] != normalized) { lines[index] = normalized; changed = true; }
            }
            if (changed) File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }

        private string CreateRepackSquashPseudo(string sourcePath, string sourceRoot, string staging, string destinationFolder)
        {
            var metadata = metadataService.Load(sourceRoot);
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>();
            var source = File.ReadAllBytes(sourcePath);
            var marker = Encoding.ASCII.GetBytes("# START OF DATA - DO NOT MODIFY\n");
            var dataOffset = IndexOfBytes(source, marker);
            var headerLength = dataOffset < 0 ? source.Length : dataOffset;
            var header = Encoding.UTF8.GetString(source, 0, headerLength);
            foreach (var line in header.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.None))
            {
                if (line.StartsWith("#", StringComparison.Ordinal)) { lines.Add(line); continue; }
                string[] fields;
                if (!TryParseSquashPseudoEntry(line, out fields) || !SquashPseudoEntryExists(staging, sourceRoot, metadata, fields[0], fields[1])) continue;
                var path = NormalizeMetadataPath(fields[0]);
                WorkspaceMetadata entry = null;
                if (metadata.TryGetValue(path, out entry)) ApplySquashMetadata(fields, entry);
                if (entry != null && entry.Kind == "symlink" && !string.IsNullOrWhiteSpace(entry.Target)) fields[fields.Length - 1] = entry.Target;
                if ((fields[1] == "R" || fields[1] == "M"))
                {
                    string physical;
                    if (TryGetStagingPath(staging, fields[0], out physical) && File.Exists(physical))
                        fields[2] = new DateTimeOffset(File.GetLastWriteTimeUtc(physical)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                }
                existing.Add(path);
                lines.Add(string.Join("\t", fields));
            }
            foreach (var item in metadata.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (existing.Contains(item.Key)) continue;
                string path;
                if (!TryGetStagingPath(staging, item.Key, out path) || (!File.Exists(path) && !Directory.Exists(path))) continue;
                var isDirectory = Directory.Exists(path);
                int uid, gid; GetOwner(item.Value, out uid, out gid);
                var mode = GetMode(item.Value, isDirectory ? Convert.ToInt32("755", 8) : Convert.ToInt32("644", 8));
                var time = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
                lines.Add(item.Key + "\t" + (isDirectory ? "D" : "M") + "\t" + time + "\t" + Convert.ToString(mode, 8) + "\t" + uid + "\t" + gid);
            }
            return WriteRepackMetadata(lines, destinationFolder, ".pseudo-", dataOffset < 0 ? null : source.Skip(dataOffset).ToArray());
        }

        private string CreateRepackJffs2DevTable(string sourcePath, string sourceRoot, string staging, string destinationFolder)
        {
            var metadata = metadataService.Load(sourceRoot);
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>();
            foreach (var line in File.ReadAllLines(sourcePath, Encoding.UTF8))
            {
                var fields = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 5) continue;
                if (!Jffs2DevTableEntryExists(staging, metadata, fields[0], fields[1])) continue;
                var path = NormalizeMetadataPath(fields[0]);
                WorkspaceMetadata entry;
                if (metadata.TryGetValue(path, out entry)) ApplyJffs2Metadata(fields, entry);
                existing.Add(path);
                lines.Add(string.Join("\t", fields));
            }
            foreach (var item in metadata.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (existing.Contains(item.Key)) continue;
                string path;
                if (!TryGetStagingPath(staging, item.Key, out path) || (!File.Exists(path) && !Directory.Exists(path))) continue;
                var isDirectory = Directory.Exists(path);
                int uid, gid; GetOwner(item.Value, out uid, out gid);
                lines.Add(item.Key + "\t" + (isDirectory ? "d" : "f") + "\t" + Convert.ToString(GetMode(item.Value, isDirectory ? Convert.ToInt32("755", 8) : Convert.ToInt32("644", 8)), 8) + "\t" + uid + "\t" + gid + "\t-\t-\t-\t-\t-");
            }
            return WriteRepackMetadata(lines, destinationFolder, ".devtable-");
        }

        private static bool Jffs2DevTableEntryExists(string root, Dictionary<string, WorkspaceMetadata> metadata, string entryPath, string kind)
        {
            string path;
            if (!TryGetStagingPath(root, entryPath, out path)) return false;
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return true;
            if (kind == "d") return Directory.Exists(path);
            if (kind == "l")
            {
                WorkspaceMetadata entry;
                if (metadata.TryGetValue(NormalizeMetadataPath(entryPath), out entry) && entry != null && entry.Kind == "symlink") return true;
            }
            if (kind == "f" || kind == "l") return File.Exists(path);
            return Directory.Exists(Path.GetDirectoryName(path));
        }

        private static bool SquashPseudoEntryExists(string root, string sourceRoot, Dictionary<string, WorkspaceMetadata> metadata, string entryPath, string kind)
        {
            string path;
            if (!TryGetStagingPath(root, entryPath, out path)) return false;
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return true;
            if (kind == "D") return Directory.Exists(path);
            if (kind == "S")
            {
                WorkspaceMetadata entry;
                if (metadata.TryGetValue(NormalizeMetadataPath(entryPath), out entry) && entry != null && entry.Kind == "symlink") return true;
                string cookie;
                return TryGetStagingPath(sourceRoot, entryPath, out cookie) && File.Exists(cookie);
            }
            if (kind == "B" || kind == "C" || kind == "I") return Directory.Exists(Path.GetDirectoryName(path));
            return File.Exists(path);
        }

        private static bool TryGetStagingPath(string root, string entryPath, out string path)
        {
            var relative = (entryPath ?? "").Trim().TrimStart('/').Replace('\\', '/');
            if (relative.Length == 0 || relative == ".") { path = root; return true; }
            return TryGetSafeWindowsPath(root, relative, out path);
        }

        private static void ApplySquashMetadata(string[] fields, WorkspaceMetadata entry)
        {
            fields[3] = Convert.ToString(GetMode(entry, Convert.ToInt32("644", 8)), 8);
            int uid, gid;
            if (TryGetOwner(entry, out uid, out gid)) { fields[4] = uid.ToString(); fields[5] = gid.ToString(); }
        }

        private static void ApplyJffs2Metadata(string[] fields, WorkspaceMetadata entry)
        {
            fields[2] = Convert.ToString(GetMode(entry, Convert.ToInt32("644", 8)), 8);
            int uid, gid;
            if (TryGetOwner(entry, out uid, out gid)) { fields[3] = uid.ToString(); fields[4] = gid.ToString(); }
        }

        private static int GetMode(WorkspaceMetadata entry, int fallback) { return entry != null && entry.Mode > 0 ? entry.Mode & 0xFFF : fallback; }

        private static void GetOwner(WorkspaceMetadata entry, out int uid, out int gid)
        {
            if (!TryGetOwner(entry, out uid, out gid)) { uid = 0; gid = 0; }
        }

        private static bool TryGetOwner(WorkspaceMetadata entry, out int uid, out int gid)
        {
            uid = 0; gid = 0;
            if (entry == null || string.IsNullOrWhiteSpace(entry.Owner)) return false;
            var fields = entry.Owner.Split(':');
            return fields.Length == 2 && int.TryParse(fields[0], out uid) && uid >= 0 && int.TryParse(fields[1], out gid) && gid >= 0;
        }

        private static string WriteRepackMetadata(IEnumerable<string> lines, string destinationFolder, string prefix, byte[] trailingData = null)
        {
            Directory.CreateDirectory(destinationFolder);
            var path = Path.Combine(destinationFolder, prefix + Guid.NewGuid().ToString("N"));
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
            if (trailingData != null && trailingData.Length > 0) using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None)) stream.Write(trailingData, 0, trailingData.Length);
            return path;
        }

        private static int IndexOfBytes(byte[] source, byte[] value)
        {
            if (source == null || value == null || value.Length == 0 || source.Length < value.Length) return -1;
            for (var index = 0; index <= source.Length - value.Length; index++)
            {
                var match = true;
                for (var offset = 0; offset < value.Length; offset++) if (source[index + offset] != value[offset]) { match = false; break; }
                if (match) return index;
            }
            return -1;
        }

        private static string CreateStagingDirectory(string source, string parent, Dictionary<string, WorkspaceMetadata> symlinks)
        {
            var staging = Path.Combine(parent, ".staging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                CopyWithoutCookies(source, staging, symlinks != null);
                if (symlinks != null) CreateStagingSymlinks(staging, symlinks);
                return staging;
            }
            catch { DeleteStagingDirectory(staging); throw; }
        }

        private static void CopyWithoutCookies(string source, string destination, bool keepLegacyJffsCookies)
        {
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target;
                if (Path.GetFileName(file).Equals(".wifitool.metadata", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsCookie(file, out target) && (!keepLegacyJffsCookies || !IsJffsCookie(file))) continue;
                var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destinationFile = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Copy(file, destinationFile, true);
                if (keepLegacyJffsCookies && IsJffsCookie(file)) try { File.SetAttributes(destinationFile, FileAttributes.System); } catch { }
            }
        }

        private static void RemoveStagingSymlinks(string root, Dictionary<string, WorkspaceMetadata> metadata)
        {
            foreach (var item in metadata)
            {
                if (item.Value == null || item.Value.Kind != "symlink") continue;
                string path;
                if (!TryGetStagingPath(root, item.Key, out path)) continue;
                if (File.Exists(path)) TryDeleteFile(path);
                else if (Directory.Exists(path)) TryDeleteDirectory(path);
            }
        }

        private void NormalizeExtractedSymlinks(string root)
        {
            var metadata = metadataService.Load(root);
            foreach (var item in metadata)
            {
                if (item.Value == null || item.Value.Kind != "symlink") continue;
                string path;
                if (!TryGetStagingPath(root, item.Key, out path)) continue;
                if (File.Exists(path)) TryDeleteFile(path);
                else if (Directory.Exists(path)) TryDeleteDirectory(path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var cookie = Encoding.UTF8.GetBytes("WIFITOOL_SYMLINK\n" + (item.Value.Target ?? ""));
                File.WriteAllBytes(path, cookie);
            }
        }

        private static void CreateStagingSymlinks(string root, Dictionary<string, WorkspaceMetadata> metadata)
        {
            foreach (var item in metadata)
            {
                if (item.Value == null || item.Value.Kind != "symlink" || string.IsNullOrWhiteSpace(item.Value.Target)) continue;
                string path;
                if (!TryGetStagingPath(root, item.Key, out path)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path)) File.Delete(path);
                File.WriteAllBytes(path, CombineBytes(Encoding.ASCII.GetBytes("!<symlink>"), CombineBytes(Encoding.UTF8.GetBytes(item.Value.Target), new byte[] { 0 })));
                try { File.SetAttributes(path, FileAttributes.System); } catch { }
            }
        }

        private static void DeleteStagingDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); } } catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        private static bool IsCookie(string path, out string target)
        {
            target = null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                var marker = Encoding.ASCII.GetBytes("WIFITOOL_SYMLINK\n"); var jffsMarker = Encoding.ASCII.GetBytes("!<symlink>");
                if (bytes.Length >= marker.Length && marker.SequenceEqual(bytes.Take(marker.Length))) target = Encoding.UTF8.GetString(bytes, marker.Length, bytes.Length - marker.Length).TrimEnd('\r', '\n', '\0');
                else if (bytes.Length >= jffsMarker.Length && jffsMarker.SequenceEqual(bytes.Take(jffsMarker.Length))) target = Encoding.UTF8.GetString(bytes, jffsMarker.Length, bytes.Length - jffsMarker.Length).TrimEnd('\r', '\n', '\0');
                else return false;
                return true;
            }
            catch { return false; }
        }

        private static bool IsJffsCookie(string path)
        {
            try { var bytes = File.ReadAllBytes(path); var marker = Encoding.ASCII.GetBytes("!<symlink>"); return bytes.Length >= marker.Length && marker.SequenceEqual(bytes.Take(marker.Length)); } catch { return false; }
        }

        private static byte[] CombineBytes(byte[] first, byte[] second) { var result = new byte[first.Length + second.Length]; Buffer.BlockCopy(first, 0, result, 0, first.Length); Buffer.BlockCopy(second, 0, result, first.Length, second.Length); return result; }

        private void CreateSquashMetadata(string root, string pseudo)
        {
            var entries = new Dictionary<string, WorkspaceMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(pseudo))
            {
                string[] fields;
                if (!TryParseSquashPseudoEntry(line, out fields)) continue;
                int mode; TryParseOctal(fields[3], out mode);
                var kind = fields[1] == "D" ? "directory" : fields[1] == "S" ? "symlink" : fields[1] == "C" ? "device" : "file";
                entries[NormalizeMetadataPath(fields[0])] = new WorkspaceMetadata { Kind = kind, Mode = mode, Owner = fields[4] + ":" + fields[5], Target = fields[1] == "S" ? fields[fields.Length - 1] : "", Modified = fields[2] };
            }
            metadataService.Save(root, entries);
        }

        private void CreateDevMetadata(string root, string devtable, Dictionary<string, string> symlinkTargets)
        {
            var entries = new Dictionary<string, WorkspaceMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(devtable))
            {
                var fields = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); if (fields.Length < 5) continue;
                int mode; if (!TryParseOctal(fields[2], out mode)) continue;
                var kind = fields[1] == "d" ? "directory" : fields[1] == "l" ? "symlink" : "file";
                string target;
                symlinkTargets.TryGetValue(NormalizeMetadataPath(fields[0]), out target);
                entries[NormalizeMetadataPath(fields[0])] = new WorkspaceMetadata { Kind = kind, Mode = mode, Owner = fields[3] + ":" + fields[4], Target = target, Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            }
            metadataService.Save(root, entries);
        }

        private static string NormalizeMetadataPath(string value) { if (string.IsNullOrWhiteSpace(value) || value == "/") return "/"; return "/" + value.Trim('/').Replace('\\', '/'); }
        private static bool TryParseOctal(string value, out int number) { number = 0; if (string.IsNullOrWhiteSpace(value)) return false; foreach (var c in value) { if (c < '0' || c > '7') return false; number = number * 8 + c - '0'; } return true; }
        private static bool TryParseSquashPseudoEntry(string line, out string[] fields)
        {
            fields = (line ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 6 || (fields[0] != "/" && fields[0].IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)) return false;
            if (fields[1] != "D" && fields[1] != "R" && fields[1] != "S" && fields[1] != "B" && fields[1] != "C" && fields[1] != "I") return false;
            long timestamp; int mode; int uid; int gid;
            if (!long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp) || !TryParseOctal(fields[3], out mode) || !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out uid) || !int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out gid) || uid < 0 || gid < 0) return false;
            return fields[1] != "S" || fields.Length >= 7;
        }

        private static Dictionary<string, string> CreateJffs2DevTable(string imagePath, string outputPath, string root, bool littleEndian)
        {
            var bytes = File.ReadAllBytes(imagePath);
            var dirents = new Dictionary<string, JffsDirent>();
            var inodes = new Dictionary<uint, JffsInodeMeta>();
            for (var offset = 0; offset + 40 <= bytes.Length; offset += 4)
            {
                if (Read16(bytes, offset, littleEndian) != 0x1985) continue;
                var type = Read16(bytes, offset + 2, littleEndian); var total = Read32(bytes, offset + 4, littleEndian);
                if (total < 40 || offset + total > bytes.Length) continue;
                if ((type & 0x3FFF) == 0x0001)
                {
                    var parent = Read32(bytes, offset + 12, littleEndian); var version = Read32(bytes, offset + 16, littleEndian); var inode = Read32(bytes, offset + 20, littleEndian); var nameSize = bytes[offset + 28];
                    if (offset + 40 + nameSize <= bytes.Length)
                    {
                        var name = Encoding.UTF8.GetString(bytes, offset + 40, nameSize); var key = parent + "|" + name;
                        JffsDirent previous; if (!dirents.TryGetValue(key, out previous) || previous.Version < version) dirents[key] = new JffsDirent(parent, inode, name, version);
                    }
                }
                else if ((type & 0x3FFF) == 0x0002 && total >= 68)
                {
                    var inode = Read32(bytes, offset + 12, littleEndian); var version = Read32(bytes, offset + 16, littleEndian); var mode = Read32(bytes, offset + 20, littleEndian); var uid = Read16(bytes, offset + 24, littleEndian); var gid = Read16(bytes, offset + 26, littleEndian);
                    var dataSize = Read32(bytes, offset + 52, littleEndian); var target = "";
                    if ((mode & 0xF000) == 0xA000 && dataSize > 0 && offset + 68 + dataSize <= bytes.Length) target = Encoding.UTF8.GetString(bytes, offset + 68, (int)dataSize).TrimEnd('\0');
                    JffsInodeMeta previous; if (!inodes.TryGetValue(inode, out previous) || previous.Version < version) inodes[inode] = new JffsInodeMeta(inode, version, mode, uid, gid, target);
                }
                offset += (int)((total + 3) & ~3) - 4;
            }
            var lines = new List<string> { "/.\td\t755\t0\t0\t-\t-\t-\t-\t-" };
            var symlinkTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var inode in inodes.Values.OrderBy(x => x.Number))
            {
                var path = FindJffsPath(inode.Number, dirents, new HashSet<uint>()); string localPath;
                if (string.IsNullOrEmpty(path) || !TryGetSafeWindowsPath(root, path, out localPath)) continue;
                var fileType = inode.Mode & 0xF000; var kind = fileType == 0x4000 ? 'd' : fileType == 0xA000 ? 'l' : fileType == 0x2000 ? 'c' : fileType == 0x6000 ? 'b' : fileType == 0x1000 ? 'p' : 'f';
                var mode = Convert.ToString((int)(inode.Mode & 0xFFF), 8);
                var metadataPath = "/" + path;
                if (kind == 'l' && !string.IsNullOrWhiteSpace(inode.Target)) symlinkTargets[metadataPath] = inode.Target;
                lines.Add(metadataPath + "\t" + kind + "\t" + mode + "\t" + inode.Uid + "\t" + inode.Gid + "\t-\t-\t-\t\t-");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)); File.WriteAllLines(outputPath, lines, new UTF8Encoding(false));
            return symlinkTargets;
        }

        private static bool TryGetSafeWindowsPath(string root, string relative, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("/", StringComparison.Ordinal)) return false;
            foreach (var segment in relative.Split('/')) if (!IsSafeWindowsSegment(segment)) return false;
            try
            {
                var rootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!candidate.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || candidate.Length >= 260) return false;
                path = candidate;
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (PathTooLongException) { return false; }
        }

        private static bool IsSafeWindowsSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".." || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.EndsWith(".", StringComparison.Ordinal) || value.EndsWith(" ", StringComparison.Ordinal)) return false;
            var stem = value.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL") return false;
            return stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '1' && stem[3] <= '9' ? false : true;
        }

        private static string FindJffsPath(uint inode, Dictionary<string, JffsDirent> links, HashSet<uint> visited)
        {
            if (inode == 1) return ""; if (!visited.Add(inode)) return null; foreach (var item in links.Values) if (item.Inode == inode) { var parent = FindJffsPath(item.Parent, links, visited); if (parent != null) return string.IsNullOrEmpty(parent) ? item.Name : parent + "/" + item.Name; } return null;
        }
        private static ushort Read16(byte[] b, int o, bool little) { return little ? (ushort)(b[o] | b[o + 1] << 8) : (ushort)(b[o] << 8 | b[o + 1]); }
        private static uint Read32(byte[] b, int o, bool little) { return little ? (uint)(b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24) : (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]); }
        private sealed class JffsInodeMeta { public uint Number; public uint Version; public uint Mode; public ushort Uid; public ushort Gid; public string Target; public JffsInodeMeta(uint number, uint version, uint mode, ushort uid, ushort gid, string target) { Number = number; Version = version; Mode = mode; Uid = uid; Gid = gid; Target = target; } }
        private sealed class JffsDirent { public uint Parent; public uint Inode; public string Name; public uint Version; public JffsDirent(uint parent, uint inode, string name, uint version) { Parent = parent; Inode = inode; Name = name; Version = version; } }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WiFitool.Models;

namespace WiFitool.Services
{
    internal sealed class TextFileData
    {
        public string Text { get; set; }
        public string EncodingName { get; set; }
        public string LineEnding { get; set; }
        public byte[] RawBytes { get; set; }
    }

    internal sealed class WorkspaceFileService
    {
        private readonly WorkspaceMetadataService metadataService = new WorkspaceMetadataService();

        public List<WorkspaceEntry> ListDirectory(string rootPath, string virtualPath)
        {
            var directory = Resolve(rootPath, virtualPath, true);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            var list = new List<WorkspaceEntry>();
            var metadata = metadataService.Load(rootPath);
            var physicalEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos().OrderBy(x => !Directory.Exists(x.FullName)).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (entry.Name == ".wifitool.metadata") continue;
                var isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;
                string target = null;
                var cookie = !isDirectory && IsCookie(entry.FullName, out target);
                var virtualEntryPath = Combine(virtualPath, entry.Name);
                physicalEntries.Add(WorkspaceMetadataService.Normalize(virtualEntryPath));
                WorkspaceMetadata saved;
                metadata.TryGetValue(WorkspaceMetadataService.Normalize(virtualEntryPath), out saved);
                var mode = saved != null ? saved.Mode : isDirectory ? Convert.ToInt32("755", 8) : cookie ? Convert.ToInt32("120777", 8) : Convert.ToInt32("775", 8);
                var kind = isDirectory ? "目录" : cookie ? "符号链接" : "文件";
                var size = isDirectory ? 0 : ((FileInfo)entry).Length;
                list.Add(new WorkspaceEntry { Name = entry.Name, Path = virtualEntryPath, Kind = kind, Size = cookie ? 0 : size, UnixMode = mode, UnixModeText = ModeText(mode, isDirectory ? 'd' : cookie ? 'l' : '-'), Owner = saved == null ? "未知:未知" : saved.Owner, Modified = saved == null ? entry.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : saved.Modified, Target = cookie ? target : saved == null ? null : saved.Target });
            }
            var currentDirectory = WorkspaceMetadataService.Normalize(virtualPath);
            foreach (var item in metadata)
            {
                if (item.Value == null || item.Value.Kind != "symlink" || physicalEntries.Contains(item.Key) || !IsDirectChild(item.Key, currentDirectory)) continue;
                var name = item.Key.Substring(currentDirectory == "/" ? 1 : currentDirectory.Length + 1);
                var mode = item.Value.Mode > 0 ? item.Value.Mode : Convert.ToInt32("120777", 8);
                list.Add(new WorkspaceEntry { Name = name, Path = Combine(virtualPath, name), Kind = "符号链接", Size = 0, UnixMode = mode, UnixModeText = ModeText(mode, 'l'), Owner = string.IsNullOrWhiteSpace(item.Value.Owner) ? "未知:未知" : item.Value.Owner, Modified = item.Value.Modified, Target = item.Value.Target });
            }
            return list.OrderBy(x => x.Kind == "符号链接" ? 2 : x.Kind == "目录" ? 0 : 1).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public TextFileData ReadText(string rootPath, string virtualPath)
        {
            var metadata = metadataService.Load(rootPath); WorkspaceMetadata saved;
            if (metadata.TryGetValue(WorkspaceMetadataService.Normalize(virtualPath), out saved) && saved != null && saved.Kind == "symlink") throw new InvalidDataException("符号链接不能作为普通文本编辑。");
            var path = Resolve(rootPath, virtualPath, true); string target; if (IsCookie(path, out target)) throw new InvalidDataException("符号链接不能作为普通文本编辑。"); var bytes = File.ReadAllBytes(path);
            Encoding encoding = new UTF8Encoding(false, true); var name = "UTF-8"; var skip = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { encoding = new UTF8Encoding(false, true); name = "UTF-8 BOM"; skip = 3; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { encoding = new UnicodeEncoding(false, false, true); name = "UTF-16 LE"; skip = 2; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { encoding = new UnicodeEncoding(true, false, true); name = "UTF-16 BE"; skip = 2; }
            string text;
            try { text = encoding.GetString(bytes, skip, bytes.Length - skip); }
            catch (DecoderFallbackException) { encoding = Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); name = "GB18030"; text = encoding.GetString(bytes); }
            if ((name != "UTF-16 LE" && name != "UTF-16 BE" && bytes.Any(c => c == 0)) || text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t' && c != '\f')) throw new InvalidDataException("该文件被识别为二进制，不能使用文本编辑器打开。");
            return new TextFileData { Text = text, EncodingName = name, LineEnding = text.Contains("\r\n") ? "CRLF" : text.Contains('\n') ? "LF" : text.Contains('\r') ? "CR" : "无换行", RawBytes = bytes };
        }

        public Task SaveTextAsync(string rootPath, string virtualPath, TextFileData original, string text)
        {
            return Task.Run(delegate
            {
                var encoding = EncodingFor(original.EncodingName); var normalized = NormalizeLineEndings(text, original.LineEnding); var bytes = encoding.GetBytes(normalized);
                if (original.EncodingName == "UTF-8 BOM") bytes = Prepend(new byte[] { 0xEF, 0xBB, 0xBF }, bytes);
                if (original.EncodingName == "UTF-16 LE") bytes = Prepend(new byte[] { 0xFF, 0xFE }, bytes);
                if (original.EncodingName == "UTF-16 BE") bytes = Prepend(new byte[] { 0xFE, 0xFF }, bytes);
                var path = Resolve(rootPath, virtualPath, true);
                var metadata = metadataService.Load(rootPath); WorkspaceMetadata saved;
                if (metadata.TryGetValue(WorkspaceMetadataService.Normalize(virtualPath), out saved) && saved != null && saved.Kind == "symlink") throw new InvalidDataException("符号链接不能作为普通文本编辑。");
                var temp = path + ".wifitool-tmp-" + Guid.NewGuid().ToString("N");
                File.WriteAllBytes(temp, bytes);
                if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                WorkspaceMetadata entry;
                if (!metadata.TryGetValue(WorkspaceMetadataService.Normalize(virtualPath), out entry)) entry = new WorkspaceMetadata { Mode = Convert.ToInt32("775", 8), Kind = "file", Owner = "0:0" };
                entry.Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                metadata[WorkspaceMetadataService.Normalize(virtualPath)] = entry;
                metadataService.Save(rootPath, metadata);
            });
        }

        public Task UploadFileAsync(string rootPath, string virtualDirectory, string sourcePath, bool overwrite)
        {
            return Task.Run(delegate
            {
                var targetVirtual = Combine(virtualDirectory, Path.GetFileName(sourcePath));
                var target = Resolve(rootPath, targetVirtual, false);
                if (Directory.Exists(target)) throw new IOException("目标路径是文件夹，不能上传同名文件。");
                if (File.Exists(target) && !overwrite) throw new IOException("目标文件已存在，请使用覆盖操作。");
                var metadata = metadataService.Load(rootPath);
                WorkspaceMetadata original;
                metadata.TryGetValue(WorkspaceMetadataService.Normalize(targetVirtual), out original);
                if (original != null && original.Kind == "symlink") throw new InvalidDataException("目标路径是符号链接，不能直接上传覆盖。请先修改链接目标对应的真实文件。");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);
                File.Copy(sourcePath, target, overwrite);
                metadata[WorkspaceMetadataService.Normalize(targetVirtual)] = new WorkspaceMetadata
                {
                    Mode = original == null ? Convert.ToInt32("775", 8) : original.Mode,
                    Kind = "file",
                    Owner = original == null ? "0:0" : original.Owner,
                    Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                metadataService.Save(rootPath, metadata);
            });
        }

        public Task CreateDirectoryAsync(string rootPath, string virtualDirectory, string name)
        {
            return Task.Run(delegate { var path = Combine(virtualDirectory, name); Directory.CreateDirectory(Resolve(rootPath, path, false)); metadataService.Update(rootPath, path, new WorkspaceMetadata { Mode = Convert.ToInt32("755", 8), Kind = "directory", Owner = "0:0", Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }); });
        }

        public Task DeleteAsync(string rootPath, string virtualPath)
        {
            return Task.Run(delegate
            {
                var normalized = WorkspaceMetadataService.Normalize(virtualPath); var metadata = metadataService.Load(rootPath); WorkspaceMetadata saved;
                var path = Resolve(rootPath, virtualPath, false);
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); }
                else if (!metadata.TryGetValue(normalized, out saved)) throw new FileNotFoundException("路径不存在。", path);
                var prefix = normalized.TrimEnd('/') + "/";
                foreach (var key in metadata.Keys.Where(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase) || x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList()) metadata.Remove(key);
                metadataService.Save(rootPath, metadata);
            });
        }

        public Task RenameAsync(string rootPath, string virtualPath, string newName)
        {
            return Task.Run(delegate
            {
                if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || newName == "." || newName == "..") throw new InvalidDataException("名称无效。");
                var normalized = WorkspaceMetadataService.Normalize(virtualPath);
                if (normalized == "/") throw new InvalidDataException("根目录不能重命名。");
                var targetVirtual = Combine(ParentVirtual(normalized), newName);
                var targetNormalized = WorkspaceMetadataService.Normalize(targetVirtual);
                var metadata = metadataService.Load(rootPath);
                var physical = Resolve(rootPath, virtualPath, false);
                if (File.Exists(physical) || Directory.Exists(physical))
                {
                    var destination = Resolve(rootPath, targetVirtual, false);
                    if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("目标已存在同名文件或目录：" + newName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if (Directory.Exists(physical)) Directory.Move(physical, destination);
                    else { File.SetAttributes(physical, FileAttributes.Normal); File.Move(physical, destination); }
                }
                else if (!metadata.ContainsKey(normalized)) throw new FileNotFoundException("路径不存在。", physical);

                var oldPrefix = normalized.TrimEnd('/') + "/";
                var targetPrefix = targetNormalized.TrimEnd('/') + "/";
                foreach (var key in metadata.Keys.Where(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase) || x.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    var value = metadata[key];
                    metadata.Remove(key);
                    metadata[key.Equals(normalized, StringComparison.OrdinalIgnoreCase) ? targetNormalized : targetPrefix + key.Substring(oldPrefix.Length)] = value;
                }
                metadataService.Save(rootPath, metadata);
            });
        }

        public Task ExportAllAsync(string rootPath, string destination, bool includeEmptyDirectories, bool includeSymlinks)
        {
            return Task.Run(delegate { CopyDirectoryWithMetadata(rootPath, destination, includeEmptyDirectories, includeSymlinks); });
        }

        public Task DownloadDirectoryAsync(string rootPath, string virtualPath, string destination)
        {
            return Task.Run(delegate
            {
                var source = Resolve(rootPath, virtualPath, true);
                if (!Directory.Exists(source)) throw new InvalidOperationException("目标不是文件夹。");
                CopyDirectory(source, destination, true);
            });
        }

        public List<WorkspaceEntry> SearchFiles(string rootPath, string keyword)
        {
            var root = Resolve(rootPath, "/", false);
            var list = new List<WorkspaceEntry>();
            foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path).Equals(".wifitool.metadata", StringComparison.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileName(path);
                if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var virtualPath = "/" + relative.Replace(Path.DirectorySeparatorChar, '/');
                var isDirectory = Directory.Exists(path);
                list.Add(new WorkspaceEntry { Name = virtualPath, Path = virtualPath, Kind = isDirectory ? "目录" : "文件", UnixModeText = isDirectory ? "d--------" : "----------", Owner = "未知:未知", Modified = isDirectory ? "" : File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss") });
            }
            return list;
        }

        public void SetPermissions(string rootPath, string virtualPath, int mode, string owner, bool recursive)
        {
            var metadata = metadataService.Load(rootPath); WorkspaceMetadata saved;
            var physical = Resolve(rootPath, virtualPath, false);
            string target;
            var normalized = WorkspaceMetadataService.Normalize(virtualPath);
            var kind = metadata.TryGetValue(normalized, out saved) && saved != null ? saved.Kind : Directory.Exists(physical) ? "directory" : IsCookie(physical, out target) ? "symlink" : "file";
            if (kind == "symlink") throw new InvalidOperationException("符号链接权限由目标文件系统元数据决定，不能单独修改。");
            var ownerValue = string.IsNullOrWhiteSpace(owner) ? "未知:未知" : owner;
            var modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (recursive && Directory.Exists(physical))
            {
                foreach (var entryPath in Directory.GetFileSystemEntries(physical, "*", SearchOption.AllDirectories))
                {
                    var relative = entryPath.Substring(physical.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var virtualEntry = normalized == "/" ? "/" + relative : normalized.TrimEnd('/') + "/" + relative;
                    string cookieTarget;
                    if (!Directory.Exists(entryPath) && IsCookie(entryPath, out cookieTarget)) continue;
                    metadata[WorkspaceMetadataService.Normalize(virtualEntry)] = new WorkspaceMetadata { Mode = mode, Kind = Directory.Exists(entryPath) ? "directory" : "file", Owner = ownerValue, Target = "", Modified = modified };
                }
            }
            metadata[normalized] = new WorkspaceMetadata { Mode = mode, Kind = kind, Owner = ownerValue, Target = "", Modified = modified };
            metadataService.Save(rootPath, metadata);
        }

        public string Resolve(string rootPath, string virtualPath, bool mustExist)
        {
            var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar); var relative = (virtualPath ?? "/").Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar); var full = Path.GetFullPath(Path.Combine(root, relative));
            if (!string.Equals(full, root, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("路径越界。");
            if (mustExist && !File.Exists(full) && !Directory.Exists(full)) throw new FileNotFoundException("路径不存在。", full);
            return full;
        }

        private static string Combine(string directory, string name) { return directory == "/" ? "/" + name : directory.TrimEnd('/') + "/" + name; }
        private static string ParentVirtual(string path) { var normalized = (path ?? "/").TrimEnd('/'); var index = normalized.LastIndexOf('/'); return index <= 0 ? "/" : normalized.Substring(0, index); }
        private static bool IsDirectChild(string path, string parent)
        {
            if (parent == "/") return path.Length > 1 && path.IndexOf('/', 1) < 0;
            var prefix = parent.TrimEnd('/') + "/";
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.IndexOf('/', prefix.Length) < 0;
        }
        private static bool IsCookie(string path, out string target) { target = null; try { var bytes = File.ReadAllBytes(path); var marker = Encoding.ASCII.GetBytes("WIFITOOL_SYMLINK\n"); var jffs = Encoding.ASCII.GetBytes("!<symlink>"); if (bytes.Length >= marker.Length && marker.SequenceEqual(bytes.Take(marker.Length))) { target = Encoding.UTF8.GetString(bytes, marker.Length, bytes.Length - marker.Length); return true; } if (bytes.Length >= jffs.Length && jffs.SequenceEqual(bytes.Take(jffs.Length))) { target = Encoding.UTF8.GetString(bytes, jffs.Length, bytes.Length - jffs.Length).TrimEnd('\0'); return true; } } catch { } return false; }
        private static string ModeText(int mode, char type) { return type + ((mode & 256) != 0 ? "r" : "-") + ((mode & 128) != 0 ? "w" : "-") + ((mode & 64) != 0 ? "x" : "-") + ((mode & 32) != 0 ? "r" : "-") + ((mode & 16) != 0 ? "w" : "-") + ((mode & 8) != 0 ? "x" : "-") + ((mode & 4) != 0 ? "r" : "-") + ((mode & 2) != 0 ? "w" : "-") + ((mode & 1) != 0 ? "x" : "-"); }
        private static Encoding EncodingFor(string name) { if (name == "UTF-16 LE") return new UnicodeEncoding(false, false); if (name == "UTF-16 BE") return new UnicodeEncoding(true, false); if (name == "GB18030") return Encoding.GetEncoding(54936); return new UTF8Encoding(false); }
        private static byte[] Prepend(byte[] prefix, byte[] value) { var result = new byte[prefix.Length + value.Length]; Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length); Buffer.BlockCopy(value, 0, result, prefix.Length, value.Length); return result; }
        private static string NormalizeLineEndings(string text, string lineEnding) { var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n'); if (lineEnding == "CRLF") return normalized.Replace("\n", "\r\n"); if (lineEnding == "CR") return normalized.Replace('\n', '\r'); if (lineEnding == "无换行") return normalized.Replace("\n", ""); return normalized; }
        private static void CopyDirectory(string source, string destination, bool includeEmptyDirectories, HashSet<string> excludePaths = null)
        {
            Directory.CreateDirectory(destination);
            if (includeEmptyDirectories) foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) if (Path.GetFileName(directory) != ".wifitool.metadata") Directory.CreateDirectory(directory.Replace(source, destination));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == ".wifitool.metadata") continue;
                if (excludePaths != null && excludePaths.Contains(file)) continue;
                var target = file.Replace(source, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private void CopyDirectoryWithMetadata(string source, string destination, bool includeEmptyDirectories, bool includeSymlinks)
        {
            var metadata = metadataService.Load(source);
            var symlinkPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in metadata)
            {
                if (item.Value == null || item.Value.Kind != "symlink") continue;
                var relative = item.Key.Trim('/').Replace('/', Path.DirectorySeparatorChar);
                if (relative.Length == 0) continue;
                var physical = Path.Combine(source, relative);
                if (File.Exists(physical) || Directory.Exists(physical)) symlinkPaths.Add(physical);
            }
            CopyDirectory(source, destination, includeEmptyDirectories, symlinkPaths);
            if (!includeSymlinks) return;
            foreach (var item in metadata)
            {
                if (item.Value == null || item.Value.Kind != "symlink") continue;
                var relative = item.Key.Trim('/').Replace('/', Path.DirectorySeparatorChar);
                if (relative.Length == 0) continue;
                var target = Path.Combine(destination, relative);
                if (Directory.Exists(target)) Directory.Delete(target, true);
                if (File.Exists(target))
                {
                    File.SetAttributes(target, FileAttributes.Normal);
                    File.Delete(target);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                var cookie = Encoding.UTF8.GetBytes("WIFITOOL_SYMLINK\n" + (item.Value.Target ?? ""));
                File.WriteAllBytes(target, cookie);
            }
        }
    }
}

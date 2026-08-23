using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WiFitool.Services
{
    internal sealed class WorkspaceMetadata
    {
        public string Kind { get; set; }
        public int Mode { get; set; }
        public string Owner { get; set; }
        public string Target { get; set; }
        public string Modified { get; set; }
    }

    internal sealed class WorkspaceMetadataService
    {
        private const string FileName = ".wifitool.metadata";

        public Dictionary<string, WorkspaceMetadata> Load(string rootPath)
        {
            var result = new Dictionary<string, WorkspaceMetadata>(StringComparer.OrdinalIgnoreCase);
            var path = Path.Combine(rootPath, FileName);
            if (!File.Exists(path)) return result;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                var fields = line.Split('\t');
                if (fields.Length < 5) continue;
                int mode;
                if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out mode)) continue;
                result[fields[0]] = new WorkspaceMetadata { Mode = mode, Kind = fields[2], Owner = fields[3], Target = fields[4], Modified = fields.Length > 5 ? fields[5] : "" };
            }
            return result;
        }

        public void Save(string rootPath, Dictionary<string, WorkspaceMetadata> entries)
        {
            var path = Path.Combine(rootPath, FileName);
            var lines = new List<string>();
            foreach (var item in entries)
            {
                var value = item.Value ?? new WorkspaceMetadata();
                lines.Add(item.Key + "\t" + value.Mode.ToString(CultureInfo.InvariantCulture) + "\t" + (value.Kind ?? "file") + "\t" + (value.Owner ?? "未知:未知") + "\t" + (value.Target ?? "") + "\t" + (value.Modified ?? ""));
            }
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
            try { File.SetAttributes(path, FileAttributes.Hidden); } catch { }
        }

        public void Update(string rootPath, string virtualPath, WorkspaceMetadata metadata)
        {
            var entries = Load(rootPath);
            entries[Normalize(virtualPath)] = metadata;
            Save(rootPath, entries);
        }

        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "/") return "/";
            return "/" + path.Replace('\\', '/').Trim('/');
        }
    }
}

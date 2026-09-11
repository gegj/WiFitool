using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using WiFitool.Models;

namespace WiFitool.Services
{
    public sealed class ToolboxService
    {
        private readonly string filePath;
        public ToolboxService() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WiFitool", "tools.json")) { }
        public ToolboxService(string path) { filePath = path; }

        public List<ToolboxItem> Load()
        {
            if (!File.Exists(filePath)) return new List<ToolboxItem>();
            using (var stream = File.OpenRead(filePath))
            {
                var items = (List<ToolboxItem>)new DataContractJsonSerializer(typeof(List<ToolboxItem>)).ReadObject(stream);
                items = items == null ? new List<ToolboxItem>() : items.Where(item => item != null && item.Type == "exe" && !string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.ExecutablePath) && Path.IsPathRooted(item.ExecutablePath) && string.Equals(Path.GetExtension(item.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase)).ToList();
                return items;
            }
        }

        public void Save(IEnumerable<ToolboxItem> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var temporaryPath = filePath + ".tmp";
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(List<ToolboxItem>)).WriteObject(stream, items.Where(item => item.Type != "builtin").ToList());
                stream.Flush(true);
            }
            // 完整写入后再替换，避免写入失败破坏原配置。
            if (File.Exists(filePath)) File.Replace(temporaryPath, filePath, null);
            else File.Move(temporaryPath, filePath);
        }
    }
}



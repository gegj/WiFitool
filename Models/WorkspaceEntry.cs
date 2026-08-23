using System;
using System.Globalization;

namespace WiFitool.Models
{
    internal sealed class WorkspaceEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Kind { get; set; }
        public string Icon { get { return Kind == "目录" ? "📁" : Kind == "符号链接" ? "🔗" : "📄"; } }
        public long Size { get; set; }
        public int UnixMode { get; set; }
        public string UnixModeDisplay { get { return Convert.ToString(UnixMode & 511, 8); } }
        public string UnixModeText { get; set; }
        public string Owner { get; set; }
        public string Modified { get; set; }
        public string ModifiedDisplay
        {
            get
            {
                var value = (Modified ?? "").Trim();
                long timestamp;
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp))
                {
                    try
                    {
                        var date = Math.Abs(timestamp) >= 100000000000L ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime : DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                        return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    }
                    catch (ArgumentOutOfRangeException) { }
                }
                DateTime parsed;
                if (DateTime.TryParseExact(value, "MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed) || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed) || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                {
                    return parsed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                }
                return value;
            }
        }
        public string Encoding { get; set; }
        public string LineEnding { get; set; }
        public string Target { get; set; }
        public bool IsAdb { get; set; }
        public bool CanWrite { get; set; }
    }
}

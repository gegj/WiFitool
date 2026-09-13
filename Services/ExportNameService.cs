using System.IO;

namespace WiFitool.Services
{
    internal static class ExportNameService
    {
        public static string GetBaseName(string value, string fallback)
        {
            var name = Path.GetFileNameWithoutExtension(value ?? "");
            if (string.IsNullOrWhiteSpace(name)) name = fallback;
            name = CleanFileName(name.Trim());
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        public static string CleanFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Trim();
        }
    }
}

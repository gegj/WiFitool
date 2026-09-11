using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WiFitool.Services
{
    internal enum LogLevel
    {
        Off = 0,
        Error = 1,
        Warn = 2,
        Info = 3,
        Debug = 4
    }

    internal sealed class LogService
    {
        private static readonly Lazy<LogService> InstanceHolder = new Lazy<LogService>(() => new LogService());
        private readonly object syncRoot = new object();
        private bool initialized;

        private LogService()
        {
            Level = LogLevel.Off;
        }

        public static LogService Instance { get { return InstanceHolder.Value; } }

        public LogLevel Level { get; private set; }

        private static string AppDataRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFitool"); }
        }

        private static string LogDirectory
        {
            get { return Path.Combine(AppDataRoot, "Logs"); }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(AppDataRoot, "logging.ini"); }
        }

        public void Initialize()
        {
            lock (syncRoot)
            {
                if (initialized) return;
                Level = LoadLevel();
                CleanupOldLogsCore();
                initialized = true;
            }
        }

        public void SetLevel(LogLevel level)
        {
            Initialize();
            lock (syncRoot)
            {
                Level = level;
                try
                {
                    Directory.CreateDirectory(AppDataRoot);
                    File.WriteAllText(SettingsPath, "Level=" + level, new UTF8Encoding(false));
                }
                catch
                {
                }
            }
        }

        public void ClearLogs()
        {
            Initialize();
            lock (syncRoot)
            {
                if (!Directory.Exists(LogDirectory)) return;
                foreach (var file in Directory.GetFiles(LogDirectory, "*.log", SearchOption.TopDirectoryOnly))
                {
                    try { File.Delete(file); }
                    catch { }
                }
            }
        }

        public void Debug(string source, string message) { Write(LogLevel.Debug, source, message, null); }
        public void Debug(string source, string message, Exception exception) { Write(LogLevel.Debug, source, message, exception); }
        public void Info(string source, string message) { Write(LogLevel.Info, source, message, null); }
        public void Warn(string source, string message) { Write(LogLevel.Warn, source, message, null); }
        public void Warn(string source, string message, Exception exception) { Write(LogLevel.Warn, source, message, exception); }
        public void Error(string source, string message) { Write(LogLevel.Error, source, message, null); }
        public void Error(string source, string message, Exception exception) { Write(LogLevel.Error, source, message, exception); }

        private void Write(LogLevel level, string source, string message, Exception exception)
        {
            Initialize();
            lock (syncRoot)
            {
                if (Level == LogLevel.Off || Level < level) return;
                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    var now = DateTime.Now;
                    var text = Sanitize(message ?? "");
                    if (exception != null) text += Environment.NewLine + Sanitize(exception.ToString());
                    if (text.Length > 12000) text = text.Substring(0, 12000) + Environment.NewLine + "[日志内容已截断]";
                    var line = now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] [" + (source ?? "App") + "] " + text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
                    var path = Path.Combine(LogDirectory, "wifitool-" + now.ToString("yyyyMMdd") + ".log");
                    File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
                }
                catch
                {
                }
            }
        }

        private static LogLevel LoadLevel()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return LogLevel.Off;
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    if (!line.StartsWith("Level=", StringComparison.OrdinalIgnoreCase)) continue;
                    LogLevel level;
                    return Enum.TryParse(line.Substring(6).Trim(), true, out level)
                        && Enum.IsDefined(typeof(LogLevel), level) ? level : LogLevel.Off;
                }
            }
            catch
            {
            }
            return LogLevel.Off;
        }

        private static void CleanupOldLogsCore()
        {
            try
            {
                if (!Directory.Exists(LogDirectory)) return;
                var today = DateTime.Today;
                foreach (var file in Directory.GetFiles(LogDirectory, "*.log", SearchOption.TopDirectoryOnly))
                    try { if (File.GetLastWriteTime(file).Date < today) File.Delete(file); }
                    catch { }
            }
            catch
            {
            }
        }

        private static string Sanitize(string value)
        {
            var result = Regex.Replace(value ?? "", @"(?i)(password|token|api[_-]?key|secret)\s*[:=]\s*[^\s,&]+", "$1=***");
            return Regex.Replace(result, @"(?i)(authorization\s*:\s*bearer\s+)[^\s]+", "$1***");
        }
    }
}

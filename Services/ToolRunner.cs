using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class ToolResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
    }

    internal sealed class ToolRunner
    {
        public Task<ToolResult> RunWithInputAsync(string executable, IEnumerable<string> arguments, string workingDirectory, byte[] input, CancellationToken token)
        {
            return Task.Run(() =>
            {
                var info = new ProcessStartInfo { FileName = executable, Arguments = BuildArguments(arguments), WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = new Process { StartInfo = info })
                {
                    if (!process.Start()) throw new InvalidOperationException("无法启动工具：" + executable);
                    process.StandardInput.BaseStream.Write(input, 0, input.Length); process.StandardInput.Close();
                    var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
                    return new ToolResult { ExitCode = process.ExitCode, StandardOutput = output, StandardError = error };
                }
            }, token);
        }
        public Task<ToolResult> RunAsync(string executable, IEnumerable<string> arguments, string workingDirectory, CancellationToken token, Action<string, bool> output = null, Encoding textEncoding = null)
        {
            return Task.Run(() => Run(executable, arguments, workingDirectory, token, output, textEncoding), token);
        }

        private static ToolResult Run(string executable, IEnumerable<string> arguments, string workingDirectory, CancellationToken token, Action<string, bool> output, Encoding textEncoding)
        {
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException("缺少外部工具：" + Path.GetFileName(executable), executable);
            }

            var encoding = textEncoding ?? Encoding.UTF8;
            var info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = BuildArguments(arguments),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = encoding,
                StandardErrorEncoding = encoding
            };
            using (var process = new Process { StartInfo = info })
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (args.Data != null) { stdout.AppendLine(args.Data); if (output != null) output(args.Data, false); }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (args.Data != null) { stderr.AppendLine(args.Data); if (output != null) output(args.Data, true); }
                };
                if (!process.Start()) throw new InvalidOperationException("无法启动工具：" + executable);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                while (!process.WaitForExit(100))
                {
                    if (token.IsCancellationRequested)
                    {
                        TryKillTree(process);
                        token.ThrowIfCancellationRequested();
                    }
                }
                process.WaitForExit();
                return new ToolResult { ExitCode = process.ExitCode, StandardOutput = stdout.ToString(), StandardError = stderr.ToString() };
            }
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        }

        private static void TryKillTree(Process process)
        {
            try
            {
                if (process.HasExited) return;
                var taskkill = new ProcessStartInfo { FileName = "taskkill.exe", Arguments = "/PID " + process.Id + " /T /F", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var killer = Process.Start(taskkill)) { if (killer != null) killer.WaitForExit(3000); }
            }
            catch { TryKill(process); }
        }

        internal static string BuildArguments(IEnumerable<string> arguments)
        {
            var normalizedArguments = new List<string>(arguments);
            for (var index = 0; index + 1 < normalizedArguments.Count; index++)
            {
                if (normalizedArguments[index] == "-s" && normalizedArguments[index + 1].StartsWith("transport:", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedArguments[index] = "-t";
                    normalizedArguments[index + 1] = normalizedArguments[index + 1].Substring("transport:".Length);
                }
            }
            var builder = new StringBuilder();
            foreach (var argument in normalizedArguments)
            {
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(QuoteArgument(argument ?? ""));
            }
            return builder.ToString();
        }

        private static string QuoteArgument(string argument)
        {
            if (argument.Length == 0) return "\"\"";
            var needsQuotes = false;
            for (var i = 0; i < argument.Length; i++)
            {
                if (char.IsWhiteSpace(argument[i]) || argument[i] == '"') { needsQuotes = true; break; }
            }
            if (!needsQuotes) return argument;
            var builder = new StringBuilder("\"");
            var slashes = 0;
            foreach (var ch in argument)
            {
                if (ch == '\\') { slashes++; continue; }
                if (ch == '"') { builder.Append(new string('\\', slashes * 2 + 1)); builder.Append('"'); slashes = 0; continue; }
                if (slashes > 0) { builder.Append(new string('\\', slashes)); slashes = 0; }
                builder.Append(ch);
            }
            if (slashes > 0) builder.Append(new string('\\', slashes * 2));
            builder.Append('"');
            return builder.ToString();
        }
    }
}

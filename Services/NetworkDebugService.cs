using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class NetworkDebugService
    {
        private const int PayloadLength = 128;
        private const int ProbeTimeout = 2000;
        private const int RequestTimeout = 3000;
        private const string StandardGetPath = "/goform/goform_get_cmd_process?cmd=imei";
        private const string StandardSetPath = "/goform/goform_set_cmd_process";
        private const string RemoPostPath = "/reqproc/proc_post";
        private static readonly byte[] AesKey = HexToBytes("9d4d6f47f025c03a3838f2796d8a43e3");
        private static readonly Func<string, string, string, string>[] RemoKeyBuilders =
        {
            (imei, timestamp, version) => Md5Hex("fang" + imei + "po" + timestamp + "jie" + version + "666"),
            (imei, timestamp, version) => Md5Hex("xinxun8888" + imei + timestamp + version + "xinxun6666"),
            (imei, timestamp, version) => Md5Hex("zk333" + timestamp + imei + version + "zk444")
        };

        public async Task EnableDebugAndRebootAsync(string host, CancellationToken token, Action<string> reportStatus)
        {
            var targetHost = NormalizeHost(host);
            Report(reportStatus, "正在识别设备协议…");

            var probe = await ProbeAsync(targetHost, token);
            if (probe == null)
            {
                throw new InvalidOperationException("无法识别设备，请检查 IP 和网络连接。");
            }

            if (probe.IsRemo)
            {
                await EnableRemoAsync(targetHost, probe, token, reportStatus);
            }
            else
            {
                await EnableStandardAsync(targetHost, probe.Imei, token, reportStatus);
            }

            // 重启接口只需确认请求已发出，提示交给上层立即显示。
            Report(reportStatus, "重启指令已发送。");
        }

        private async Task<ProbeResult> ProbeAsync(string host, CancellationToken token)
        {
            var remo = await Task.Run(() => ProbeRemo(host), token);
            return remo ?? await Task.Run(() => ProbeStandard(host), token);
        }

        private static ProbeResult ProbeRemo(string host)
        {
            try
            {
                var fields = new Dictionary<string, string> { { "goformId", "Getdebuginfo" } };
                var response = SendHttp(host, "POST", RemoPostPath, EncodeForm(fields), ProbeTimeout);
                var imei = ExtractField("imei", response.Body);
                var version = ExtractField("version", response.Body);
                var timestamp = ExtractField("debug_info", response.Body);
                return string.IsNullOrWhiteSpace(imei) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(timestamp)
                    ? null
                    : new ProbeResult(true, imei, version, timestamp);
            }
            catch
            {
                return null;
            }
        }

        private static ProbeResult ProbeStandard(string host)
        {
            try
            {
                var response = SendHttp(host, "GET", StandardGetPath, null, ProbeTimeout);
                var imei = ExtractField("imei", response.Body);
                return string.IsNullOrWhiteSpace(imei) ? null : new ProbeResult(false, imei, "", "");
            }
            catch
            {
                return null;
            }
        }

        private async Task EnableStandardAsync(string host, string imei, CancellationToken token, Action<string> reportStatus)
        {
            var suffix = string.IsNullOrWhiteSpace(imei) ? "" : "&imei=" + imei;
            var enabled = await Task.Run(() => SendStandardPayload(host, "debug_enable=1" + suffix, true), token);
            if (!enabled)
            {
                throw new InvalidOperationException("设备未接受开启调试请求。");
            }

            Report(reportStatus, "调试开启成功，正在发送重启指令…");
            var rebooted = await Task.Run(() => SendStandardPayload(host, "reboot_now=1" + suffix, false, false), token);
            if (!rebooted)
            {
                throw new InvalidOperationException("开启调试成功，但重启指令发送失败。");
            }
        }

        private async Task EnableRemoAsync(string host, ProbeResult probe, CancellationToken token, Action<string> reportStatus)
        {
            var enabled = false;
            foreach (var makeKey in RemoKeyBuilders)
            {
                token.ThrowIfCancellationRequested();
                Report(reportStatus, "正在尝试开启调试…");
                var fields = new Dictionary<string, string>
                {
                    { "goformId", "SysCtlUtal" },
                    { "action", "System_MODE" },
                    { "debug_enable", "1" },
                    { "key", makeKey(probe.Imei, probe.Timestamp, probe.Version) }
                };
                var response = await Task.Run(() => SendHttp(host, "POST", RemoPostPath, EncodeForm(fields), RequestTimeout), token);
                if (IsRemoSuccess(response.Body))
                {
                    enabled = true;
                    break;
                }
            }

            if (!enabled)
            {
                throw new InvalidOperationException("设备未接受开启调试请求。");
            }

            Report(reportStatus, "调试开启成功，正在发送重启指令…");
            var rebootFields = new Dictionary<string, string>
            {
                { "isTest", "false" },
                { "goformId", "REBOOT_DEVICE" }
            };
            var rebooted = await Task.Run(() => SendHttp(host, "POST", RemoPostPath, EncodeForm(rebootFields), RequestTimeout, false), token);
            if (!rebooted.RequestSent)
            {
                throw new InvalidOperationException("开启调试成功，但重启指令发送失败。");
            }
        }

        private static bool SendStandardPayload(string host, string plaintext, bool requirePass, bool readResponse = true)
        {
            string parameters;
            string md5Check;
            BuildPayload(plaintext, out parameters, out md5Check);

            var fields = new Dictionary<string, string>
            {
                { "goformId", "tw_telnet_config" },
                { "params", parameters },
                { "md5_check", md5Check }
            };
            var response = SendHttp(host, "POST", StandardSetPath, EncodeForm(fields), RequestTimeout, readResponse);
            return requirePass
                ? response.Body.IndexOf("pass", StringComparison.OrdinalIgnoreCase) >= 0
                : response.RequestSent;
        }

        private static RawResponse SendHttp(string host, string method, string path, string body, int timeout, bool readResponse = true)
        {
            var bodyBytes = body == null ? new byte[0] : Encoding.UTF8.GetBytes(body);
            var header = new StringBuilder();
            header.Append(method).Append(" ").Append(path).Append(" HTTP/1.0\r\n");
            header.Append("Host: ").Append(host).Append("\r\n");
            header.Append("Connection: close\r\n");
            if (bodyBytes.Length > 0)
            {
                header.Append("Content-Type: application/x-www-form-urlencoded\r\n");
                header.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            }
            header.Append("\r\n");

            using (var client = new TcpClient())
            {
                client.SendTimeout = timeout;
                client.ReceiveTimeout = timeout;
                var requestSent = false;
                try
                {
                    client.Connect(host, 80);
                    using (var stream = client.GetStream())
                    {
                        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
                        stream.Write(headerBytes, 0, headerBytes.Length);
                        if (bodyBytes.Length > 0)
                        {
                            stream.Write(bodyBytes, 0, bodyBytes.Length);
                        }
                        requestSent = true;
                        if (!readResponse)
                        {
                            return new RawResponse(true, "");
                        }

                        using (var response = new MemoryStream())
                        {
                            var buffer = new byte[4096];
                            try
                            {
                                int count;
                                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    response.Write(buffer, 0, count);
                                }
                            }
                            catch (IOException) { }
                            catch (SocketException) { }
                            return new RawResponse(requestSent, DecodeBody(response.ToArray()));
                        }
                    }
                }
                catch (SocketException)
                {
                    if (!requestSent) throw;
                    return new RawResponse(true, "");
                }
                catch (IOException)
                {
                    if (!requestSent) throw;
                    return new RawResponse(true, "");
                }
            }
        }

        private static string DecodeBody(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes ?? new byte[0]);
            var separator = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            return separator < 0 ? text : text.Substring(separator + 4);
        }

        private static void BuildPayload(string plaintext, out string parameters, out string md5Check)
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext ?? "");
            if (bytes.Length >= PayloadLength)
            {
                throw new ArgumentException("调试参数过长");
            }

            var padded = new byte[PayloadLength];
            Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
            byte[] encrypted;
            using (var aes = Aes.Create())
            {
                aes.Key = AesKey;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                using (var encryptor = aes.CreateEncryptor())
                {
                    encrypted = encryptor.TransformFinalBlock(padded, 0, padded.Length);
                }
            }

            parameters = ToHex(encrypted);
            md5Check = Md5Hex(bytes);
        }

        private static string ExtractField(string key, string text)
        {
            var pattern = "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"";
            var match = Regex.Match(text ?? "", pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private static string EncodeForm(Dictionary<string, string> fields)
        {
            var values = new List<string>();
            foreach (var field in fields)
            {
                values.Add(Uri.EscapeDataString(field.Key) + "=" + Uri.EscapeDataString(field.Value ?? ""));
            }
            return string.Join("&", values.ToArray());
        }

        private static string NormalizeHost(string host)
        {
            var value = (host ?? "").Trim();
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) value = value.Substring(7);
            if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) value = value.Substring(8);
            value = value.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("请输入设备 IP");
            return value;
        }

        private static bool IsRemoSuccess(string body)
        {
            var compact = Regex.Replace(body ?? "", "\\s+", "");
            return compact.IndexOf("\"result\":\"0\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   compact.IndexOf("\"result\":0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (body ?? "").IndexOf("successfully", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Report(Action<string> reportStatus, string message)
        {
            if (reportStatus != null) reportStatus(message);
        }

        private static string Md5Hex(string text)
        {
            return Md5Hex(Encoding.UTF8.GetBytes(text ?? ""));
        }

        private static string Md5Hex(byte[] bytes)
        {
            using (var md5 = MD5.Create()) return ToHex(md5.ComputeHash(bytes));
        }

        private static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes) builder.Append(value.ToString("X2"));
            return builder.ToString();
        }

        private sealed class ProbeResult
        {
            public ProbeResult(bool isRemo, string imei, string version, string timestamp)
            {
                IsRemo = isRemo;
                Imei = imei;
                Version = version;
                Timestamp = timestamp;
            }

            public bool IsRemo { get; private set; }
            public string Imei { get; private set; }
            public string Version { get; private set; }
            public string Timestamp { get; private set; }
        }

        private sealed class RawResponse
        {
            public RawResponse(bool requestSent, string body)
            {
                RequestSent = requestSent;
                Body = body ?? "";
            }

            public bool RequestSent { get; private set; }
            public string Body { get; private set; }
        }
    }
}


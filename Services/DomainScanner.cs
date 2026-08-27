using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using WiFitool.Models;

namespace WiFitool.Services
{
    internal sealed class DomainScanner
    {
        private static readonly Regex Token = new Regex(@"(?:(?:https?|mqtt)://)?(?:[A-Za-z0-9][A-Za-z0-9.-]*\.[A-Za-z]{2,}|(?:\d{1,3}\.){3}\d{1,3})(?::\d{1,5})?(?:[/A-Za-z0-9._~:/?#\[\]@!$&'()*+,;=%-]*)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly HashSet<string> PublicDns = LoadSet("ignore_ips");
        private static readonly HashSet<string> TimeHosts = LoadSet("ignore_hosts");
        private static readonly HashSet<string> ImageExtensions = LoadSet("ignore_extensions");
        private static readonly HashSet<string> IgnoredExtensions = ImageExtensions;
        private static readonly HashSet<string> IgnoredDirectories = LoadSet("ignore_directories");
        private static readonly Lazy<HashSet<string>> KnownTlds = new Lazy<HashSet<string>>(LoadTlds);

        public List<DomainScanResult> Scan(string root, CancellationToken token, Action<string> progress)
        {
            var results = new List<DomainScanResult>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var webDirectory = Path.Combine(root, "__domain_scan_no_directory_filter__");
            var webPrefix = Path.GetFullPath(webDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(x => !Path.GetFileName(x).Equals(".wifitool.metadata", StringComparison.OrdinalIgnoreCase) && !Path.GetFullPath(x).StartsWith(webPrefix, StringComparison.OrdinalIgnoreCase) && !IsIgnoredPath(root, x));
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested(); if (progress != null) progress(file);
                if (IsSymlinkCookie(file)) continue;
                if (ImageExtensions.Contains(Path.GetExtension(file))) continue;
                ScanFile(root, file, results, seen, token);
            }
            return results;
        }

        public void ValidateDns(IEnumerable<DomainScanResult> results, CancellationToken token, Action<string> progress)
        {
            foreach (var group in results.Where(x => !x.IsIp).GroupBy(x => x.Address, StringComparer.OrdinalIgnoreCase))
            {
                var item = group.First();
                token.ThrowIfCancellationRequested(); if (progress != null) progress(item.Address);
                var status = "解析失败";
                try { Dns.GetHostAddresses(item.Address); status = "解析成功"; }
                catch (WebException) { }
                catch (SocketException) { }
                catch { status = "查询失败"; }
                foreach (var result in group) result.DnsStatus = status;
            }
            foreach (var item in results.Where(x => x.IsIp)) item.DnsStatus = "不适用";
        }

        public void Rewrite(DomainScanResult item)
        {
            var newAddress = item.IsIp ? RewriteIp(item.Address) : RewriteDomain(item.Address);
            var oldBytes = Encoding.ASCII.GetBytes(item.Address); var replacement = Encoding.ASCII.GetBytes(newAddress);
            if (replacement.Length != oldBytes.Length) throw new InvalidDataException("改写结果长度不一致。");
            foreach (var group in item.Occurrences.GroupBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase))
            {
                var bytes = File.ReadAllBytes(group.Key);
                foreach (var occurrence in group.OrderByDescending(x => x.Offset))
                {
                    var offset = checked((int)occurrence.Offset);
                    if (offset < 0 || offset + oldBytes.Length > bytes.Length || !oldBytes.SequenceEqual(bytes.Skip(offset).Take(oldBytes.Length))) throw new InvalidDataException("文件内容已变化，无法定位命中。");
                    Buffer.BlockCopy(replacement, 0, bytes, offset, replacement.Length);
                }
                File.WriteAllBytes(group.Key, bytes);
            }
            item.Address = newAddress;
        }

        private static void ScanFile(string root, string file, List<DomainScanResult> results, HashSet<string> seen, CancellationToken token)
        {
            var bytes = File.ReadAllBytes(file);
            var start = 0;
            while (start < bytes.Length)
            {
                while (start < bytes.Length && (bytes[start] < 32 || bytes[start] > 126)) start++;
                if (start >= bytes.Length) break;
                var end = start;
                while (end < bytes.Length && bytes[end] >= 32 && bytes[end] <= 126) end++;
                var text = Encoding.ASCII.GetString(bytes, start, end - start);
                foreach (Match match in Token.Matches(text))
                {
                    token.ThrowIfCancellationRequested();
                    var raw = match.Value.TrimEnd('.', ',', ';', ')', ']', '}');
                    var host = raw; var scheme = Regex.Match(raw, @"^(https?|mqtt)://", RegexOptions.IgnoreCase);
                    if (scheme.Success) host = raw.Substring(scheme.Length).Split(new[] { '/', '?' }, 2)[0]; else host = raw.Split(new[] { '/', '?' }, 2)[0];
                    var hostOnly = host.Split(':')[0];
                    IPAddress ip; var isIp = IPAddress.TryParse(hostOnly, out ip) && ip.AddressFamily == AddressFamily.InterNetwork;
                    if (isIp && (!IsPublic(ip) || PublicDns.Contains(hostOnly) || IsSingleDigitIpv4(hostOnly))) continue;
                    if (!isIp && !IsValidDomain(hostOnly)) continue;
                    if (!isIp && (TimeHosts.Contains(hostOnly) || hostOnly.IndexOf("ntp", StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    if (IsImageCandidate(raw)) continue;
                    var relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
                    var hostIndex = raw.IndexOf(hostOnly, StringComparison.OrdinalIgnoreCase);
                    var offset = start + match.Index + Math.Max(0, hostIndex);
                    DomainScanResult result; if (!seen.Contains(hostOnly)) { result = new DomainScanResult { Address = hostOnly, FilePath = relative, SourcePath = file, Offset = offset, IsIp = isIp, DnsStatus = "未验证" }; result.Occurrences.Add(new DomainScanOccurrence { SourcePath = file, FilePath = relative, Offset = offset }); results.Add(result); seen.Add(hostOnly); } else { result = results.First(x => x.Address.Equals(hostOnly, StringComparison.OrdinalIgnoreCase)); result.Occurrences.Add(new DomainScanOccurrence { SourcePath = file, FilePath = relative, Offset = offset }); }
                }
                start = end;
            }
        }
        private static bool IsValidDomain(string value)
        {
            var labels = value.Split('.');
            if (labels.Length < 2 || labels.Any(x => x.Length == 0 || x.Length > 63 || x[0] == '-' || x[x.Length - 1] == '-' || !Regex.IsMatch(x, @"^[A-Za-z0-9-]+$"))) return false;
            var tld = labels[labels.Length - 1];
            var subject = labels[labels.Length - 2];
            return subject.Length > 5 && labels.Any(x => x.Any(char.IsLetter)) && KnownTlds.Value.Contains(tld);
        }
        private static HashSet<string> LoadTlds()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "domain_scan_rules.txt");
            if (!File.Exists(path)) return result;
            var section = ""; foreach (var line in File.ReadAllLines(path)) { var value = line.Trim(); if (value.Length == 0 || value.StartsWith("#")) continue; if (value.StartsWith("[") && value.EndsWith("]")) { section = value.Substring(1, value.Length - 2); continue; } if (section == "tlds") result.Add(value); }
            return result;
        }
        private static HashSet<string> LoadSet(string section)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "domain_scan_rules.txt"); if (!File.Exists(path)) return result; var current = "";
            foreach (var line in File.ReadAllLines(path)) { var value = line.Trim(); if (value.Length == 0 || value.StartsWith("#")) continue; if (value.StartsWith("[") && value.EndsWith("]")) { current = value.Substring(1, value.Length - 2); continue; } if (current == section) result.Add(value); }
            return result;
        }
        private static bool IsPublic(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224) return false;
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;
            if (b[0] == 169 && b[1] == 254) return false;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;
            if (b[0] == 192 && (b[1] == 0 || b[1] == 168)) return false;
            if (b[0] == 198 && (b[1] == 18 || b[1] == 19 || b[1] == 51)) return false;
            if (b[0] == 203 && b[1] == 0 && b[2] == 113) return false;
            return true;
        }
        private static bool IsSingleDigitIpv4(string value) { var parts = value.Split('.'); return parts.Length == 4 && parts.All(x => x.Length == 1); }
        private static bool IsImageCandidate(string value)
        {
            var valueWithoutQuery = value.Split(new[] { '?', '#' }, 2)[0];
            var extension = Path.GetExtension(valueWithoutQuery);
            return ImageExtensions.Contains(extension) || IgnoredExtensions.Contains(extension) || string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsIgnoredPath(string root, string file)
        {
            if (IgnoredDirectories.Count == 0) return false;
            var relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
            var parts = relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rule in IgnoredDirectories)
            {
                var normalized = rule.Trim().Trim('/').Replace('\\', '/');
                if (normalized.Length == 0) continue;
                if (relative.Equals(normalized, StringComparison.OrdinalIgnoreCase) || relative.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase)) return true;
                if (parts.Any(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase))) return true;
            }
            return false;
        }
        private static bool IsSymlinkCookie(string file) { try { var b = File.ReadAllBytes(file); return Encoding.ASCII.GetBytes("WIFITOOL_SYMLINK\n").SequenceEqual(b.Take(15)) || Encoding.ASCII.GetBytes("!<symlink>").SequenceEqual(b.Take(10)); } catch { return false; } }
        private static string RewriteDomain(string value) { var dot = value.LastIndexOf('.'); if (dot < 0 || dot == value.Length - 1) throw new InvalidDataException("域名格式不支持改写。"); var chars = value.ToCharArray(); chars[value.Length - 1] = 'y'; return new string(chars); }
        private static string RewriteIp(string value) { var chars = value.ToCharArray(); var index = value.IndexOf('.'); if (index < 0) throw new InvalidDataException("IPv4 格式不支持改写。"); chars[index == 0 ? value.Length - 1 : index - 1] = 'y'; return new string(chars); }
    }
}

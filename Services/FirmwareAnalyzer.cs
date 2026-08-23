using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WiFitool.Models;

namespace WiFitool.Services
{
    internal sealed class FirmwareAnalyzer
    {
        private const int RecordSize = 40;
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("vHY1WF7520");

        public ImageInfo Analyze(string path)
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            var info = new FileInfo(fullPath);
            if (!info.Exists) throw new FileNotFoundException("固件镜像不存在。", fullPath);
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var scanLength = (int)Math.Min(stream.Length, 4L * 1024 * 1024);
                var scan = new byte[scanLength];
                ReadExactly(stream, scan, 0, scan.Length);
                for (var i = 0; i <= scan.Length - Signature.Length; i++)
                {
                    var matched = true;
                    for (var j = 0; j < Signature.Length; j++) if (scan[i + j] != Signature[j]) { matched = false; break; }
                    if (!matched) continue;
                    var parts = ReadTable(stream, i, info.Length);
                    if (parts == null) continue;
                    foreach (var part in parts) DetectFileSystem(stream, part);
                    var eraseBlockSize = InferEraseBlock(parts);
                    foreach (var part in parts) if (part.FileSystem == "JFFS2") part.Jffs2EraseBlockSize = eraseBlockSize;
                    return new ImageInfo { Path = fullPath, Name = info.Name, Size = info.Length, TableOffset = i, EraseBlockSize = eraseBlockSize, Partitions = parts, IsStandalone = false };
                }

                var standalone = new PartitionInfo { Name = "rootfs", Media = "独立 rootfs", Offset = 0, Size = info.Length, FileSystem = "Raw", Compression = "--", Filter = "--", Exportable = true, Duplicates = true, LittleEndian = true };
                DetectFileSystem(stream, standalone);
                if (standalone.FileSystem != "Raw")
                {
                    var eraseBlockSize = InferEraseBlock(new List<PartitionInfo> { standalone });
                    standalone.Jffs2EraseBlockSize = eraseBlockSize;
                    return new ImageInfo { Path = fullPath, Name = info.Name, Size = info.Length, TableOffset = 0, EraseBlockSize = eraseBlockSize, Partitions = new List<PartitionInfo> { standalone }, IsStandalone = true };
                }
            }
            throw new InvalidDataException("未找到有效的 vHY1WF7520 分区表，也未识别出独立 rootfs。");
        }

        private static List<PartitionInfo> ReadTable(FileStream stream, long tableOffset, long fileLength)
        {
            if (tableOffset + 32 > fileLength) return null;
            var header = new byte[32]; stream.Position = tableOffset; ReadExactly(stream, header, 0, header.Length);
            var count = ReadUInt32(header, 24);
            if (count == 0 || count > 64 || tableOffset + 32 + count * RecordSize > fileLength) return null;
            var result = new List<PartitionInfo>();
            var records = new byte[count * RecordSize]; ReadExactly(stream, records, 0, records.Length);
            for (var i = 0; i < count; i++)
            {
                var offset = i * RecordSize;
                var name = ReadAscii(records, offset, 16); var media = ReadAscii(records, offset + 16, 16);
                var start = ReadUInt32(records, offset + 32); var size = ReadUInt32(records, offset + 36);
                if (name.Length == 0 || media.Length == 0) return null;
                if (!string.Equals(media, "nand", StringComparison.OrdinalIgnoreCase)) continue;
                if (size == 0 || (long)start + size > fileLength) return null;
                result.Add(new PartitionInfo { Name = name, Media = media, Offset = start, Size = size, FileSystem = "Raw", Compression = "--", Filter = "--", Exportable = true, Duplicates = true, LittleEndian = true });
            }
            if (result.Count == 0 || result.Select(p => p.Name.ToLowerInvariant()).Distinct().Count() != result.Count) return null;
            var ordered = result.OrderBy(p => p.Offset).ToList();
            for (var i = 1; i < ordered.Count; i++) if (ordered[i - 1].Offset + ordered[i - 1].Size > ordered[i].Offset) return null;
            return result;
        }

        private static void DetectFileSystem(FileStream stream, PartitionInfo part)
        {
            var length = (int)Math.Min(part.Size, 512L * 1024); var data = new byte[length]; stream.Position = part.Offset; ReadExactly(stream, data, 0, data.Length);
            if (length >= 96 && data[0] == (byte)'h' && data[1] == (byte)'s' && data[2] == (byte)'q' && data[3] == (byte)'s')
            {
                part.FileSystem = "SquashFS"; part.SquashFsCreationTime = ReadUInt32(data, 8); part.BlockSize = (int)ReadUInt32(data, 12); part.Compression = CompressionName(ReadUInt16(data, 20)); part.DictionarySize = part.BlockSize; part.UsedBytes = (long)ReadUInt64(data, 40); return;
            }
            for (var i = 0; i + 12 <= data.Length; i += 4)
            {
                var little = data[i] == 0x85 && data[i + 1] == 0x19; var big = data[i] == 0x19 && data[i + 1] == 0x85; if (!little && !big) continue;
                var type = little ? ReadUInt16(data, i + 2) : ReadUInt16Big(data, i + 2); var nodeLength = little ? ReadUInt32(data, i + 4) : ReadUInt32Big(data, i + 4);
                if ((type == 0xE001 || type == 0xE002 || type == 0x2003) && nodeLength >= 12 && nodeLength <= part.Size - i) { part.FileSystem = "JFFS2"; part.Compression = "原节点/LZO"; part.LittleEndian = little; part.Jffs2PageSize = InferJffs2PageSize(stream, part, little); return; }
            }
        }

        private static int InferJffs2PageSize(FileStream stream, PartitionInfo part, bool littleEndian)
        {
            var originalPosition = stream.Position;
            try
            {
                var buffer = new byte[65536 + 68];
                var position = part.Offset;
                var end = part.Offset + part.Size;
                var carry = 0;
                uint maxDataSize = 0;
                while (position < end)
                {
                    var count = (int)Math.Min(65536, end - position);
                    stream.Position = position;
                    ReadExactly(stream, buffer, carry, count);
                    var length = carry + count;
                    for (var offset = 0; offset + 68 <= length; offset += 4)
                    {
                        var magicMatches = littleEndian ? buffer[offset] == 0x85 && buffer[offset + 1] == 0x19 : buffer[offset] == 0x19 && buffer[offset + 1] == 0x85;
                        if (!magicMatches) continue;
                        var type = littleEndian ? ReadUInt16(buffer, offset + 2) : ReadUInt16Big(buffer, offset + 2);
                        if (type != 0xE002) continue;
                        var nodeLength = littleEndian ? ReadUInt32(buffer, offset + 4) : ReadUInt32Big(buffer, offset + 4);
                        var absoluteOffset = position - carry + offset;
                        if (nodeLength < 68 || absoluteOffset + nodeLength > end) continue;
                        var dataSize = littleEndian ? ReadUInt32(buffer, offset + 52) : ReadUInt32Big(buffer, offset + 52);
                        if (dataSize > maxDataSize) maxDataSize = dataSize;
                    }
                    carry = Math.Min(68, length);
                    Buffer.BlockCopy(buffer, length - carry, buffer, 0, carry);
                    position += count;
                }
                var pageSize = 1024;
                while (pageSize < maxDataSize && pageSize < 65536) pageSize *= 2;
                return maxDataSize == 0 ? 4096 : pageSize;
            }
            finally { stream.Position = originalPosition; }
        }

        private static string CompressionName(ushort value)
        {
            switch (value) { case 1: return "gzip"; case 2: return "lzma"; case 3: return "lzo"; case 4: return "xz"; case 5: return "lz4"; case 6: return "zstd"; default: return "未知(" + value + ")"; }
        }

        private static int InferEraseBlock(IEnumerable<PartitionInfo> parts)
        {
            long value = 0; foreach (var p in parts) { value = Gcd(value, p.Size); if (p.Offset > 0) value = Gcd(value, p.Offset); }
            return value >= 4096 && value <= 1024 * 1024 ? (int)value : 64 * 1024;
        }
        private static long Gcd(long a, long b) { while (b != 0) { var t = a % b; a = b; b = t; } return Math.Abs(a); }
        private static string ReadAscii(byte[] data, int offset, int length) { var end = 0; while (end < length && data[offset + end] != 0) { if (data[offset + end] < 32 || data[offset + end] > 126) return ""; end++; } return Encoding.ASCII.GetString(data, offset, end).Trim(); }
        private static ushort ReadUInt16(byte[] b, int o) { return (ushort)(b[o] | b[o + 1] << 8); }
        private static ushort ReadUInt16Big(byte[] b, int o) { return (ushort)(b[o] << 8 | b[o + 1]); }
        private static uint ReadUInt32(byte[] b, int o) { return (uint)(b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24); }
        private static uint ReadUInt32Big(byte[] b, int o) { return (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]); }
        private static ulong ReadUInt64(byte[] b, int o) { uint low = ReadUInt32(b, o); uint high = ReadUInt32(b, o + 4); return low | ((ulong)high << 32); }
        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count) { while (count > 0) { var read = stream.Read(buffer, offset, count); if (read == 0) throw new EndOfStreamException(); offset += read; count -= read; } }
    }
}

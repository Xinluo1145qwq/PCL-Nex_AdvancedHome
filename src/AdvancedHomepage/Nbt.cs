using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 极简 NBT 读取器。只为读 <c>level.dat</c> / <c>servers.dat</c> 这类小文件服务，
/// 不做写回，也不追求完整性；遇到异常一律返回 null，绝不抛给调用方。
/// </summary>
internal static class Nbt
{
    private const int MaxArrayLength = 8 * 1024 * 1024;   // 防御坏文件把内存吃光
    private const int MaxDepth = 32;

    /// <summary>读取 gzip 压缩的 NBT 文件（level.dat 格式）。</summary>
    public static Dictionary<string, object>? ReadGzipFile(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var gz = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new BinaryReader(gz, Encoding.UTF8);
            return ReadRoot(reader);
        }
        catch (Exception ex)
        {
            Log.Warn($"读取 NBT 失败（{Path.GetFileName(path)}）：{ex.Message}");
            return null;
        }
    }

    /// <summary>读取未压缩的 NBT 文件（servers.dat 格式）。</summary>
    public static Dictionary<string, object>? ReadPlainFile(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var reader = new BinaryReader(file, Encoding.UTF8);
            return ReadRoot(reader);
        }
        catch (Exception ex)
        {
            Log.Warn($"读取 NBT 失败（{Path.GetFileName(path)}）：{ex.Message}");
            return null;
        }
    }

    /// <summary>自动判断是否 gzip（servers.dat 有版本会压缩，level.dat 一律压缩）。</summary>
    public static Dictionary<string, object>? ReadAutoFile(string path)
    {
        try
        {
            using var raw = File.OpenRead(path);
            int first = raw.ReadByte();
            int second = raw.ReadByte();
            raw.Position = 0;

            if (first == 0x1F && second == 0x8B)
            {
                using var gz = new GZipStream(raw, CompressionMode.Decompress);
                using var reader = new BinaryReader(gz, Encoding.UTF8);
                return ReadRoot(reader);
            }

            using var plain = new BinaryReader(raw, Encoding.UTF8);
            return ReadRoot(plain);
        }
        catch (Exception ex)
        {
            Log.Warn($"读取 NBT 失败（{Path.GetFileName(path)}）：{ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, object>? ReadRoot(BinaryReader reader)
    {
        byte type = reader.ReadByte();
        if (type != 10) return null;      // 根必须是 TAG_Compound
        ReadString(reader);               // 根名，通常为空
        return ReadCompound(reader, 0);
    }

    private static Dictionary<string, object> ReadCompound(BinaryReader reader, int depth)
    {
        if (depth > MaxDepth) throw new InvalidDataException("NBT 嵌套过深");

        var map = new Dictionary<string, object>(StringComparer.Ordinal);
        while (true)
        {
            byte type = reader.ReadByte();
            if (type == 0) break;         // TAG_End
            string name = ReadString(reader);
            map[name] = ReadPayload(reader, type, depth);
        }

        return map;
    }

    // NBT 全程大端序，BinaryReader 默认小端，所以多字节一律自己按大端拼。

    private static ushort ReadUInt16BE(BinaryReader reader)
    {
        int hi = reader.ReadByte();
        int lo = reader.ReadByte();
        return (ushort)((hi << 8) | lo);
    }

    private static int ReadInt32BE(BinaryReader reader)
    {
        uint value = 0;
        for (int i = 0; i < 4; i++) value = (value << 8) | reader.ReadByte();
        return unchecked((int)value);
    }

    private static long ReadInt64BE(BinaryReader reader)
    {
        ulong value = 0;
        for (int i = 0; i < 8; i++) value = (value << 8) | reader.ReadByte();
        return unchecked((long)value);
    }

    private static object ReadPayload(BinaryReader reader, byte type, int depth)
    {
        switch (type)
        {
            case 1: return reader.ReadByte();
            case 2: return (short)ReadUInt16BE(reader);
            case 3: return ReadInt32BE(reader);
            case 4: return ReadInt64BE(reader);
            case 5: return BitConverter.Int32BitsToSingle(ReadInt32BE(reader));
            case 6: return BitConverter.Int64BitsToDouble(ReadInt64BE(reader));

            case 7:
            {
                int length = CheckLength(ReadInt32BE(reader));
                return reader.ReadBytes(length);
            }

            case 8:
                return ReadString(reader);

            case 9:
            {
                byte elementType = reader.ReadByte();
                int length = CheckLength(ReadInt32BE(reader));
                var list = new List<object>(Math.Min(length, 1024));
                for (int i = 0; i < length; i++) list.Add(ReadPayload(reader, elementType, depth + 1));
                return list;
            }

            case 10:
                return ReadCompound(reader, depth + 1);

            case 11:
            {
                int length = CheckLength(ReadInt32BE(reader));
                var array = new int[length];
                for (int i = 0; i < length; i++) array[i] = ReadInt32BE(reader);
                return array;
            }

            case 12:
            {
                int length = CheckLength(ReadInt32BE(reader));
                var array = new long[length];
                for (int i = 0; i < length; i++) array[i] = ReadInt64BE(reader);
                return array;
            }

            default:
                throw new InvalidDataException($"未知 NBT 类型 {type}");
        }
    }

    private static int CheckLength(int length)
    {
        if (length < 0 || length > MaxArrayLength)
            throw new InvalidDataException($"NBT 长度异常：{length}");
        return length;
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = ReadUInt16BE(reader);
        if (length == 0) return "";
        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    // ─────────────────────────── 便捷取值 ───────────────────────────

    public static Dictionary<string, object>? Object(Dictionary<string, object>? map, string key)
        => map != null && map.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;

    public static string String(Dictionary<string, object>? map, string key, string fallback = "")
        => map != null && map.TryGetValue(key, out var value) && value is string text ? text : fallback;

    public static long Long(Dictionary<string, object>? map, string key, long fallback = 0)
        => map != null && map.TryGetValue(key, out var value) && value is long number ? number : fallback;

    public static int Int(Dictionary<string, object>? map, string key, int fallback = 0)
        => map != null && map.TryGetValue(key, out var value) && value is int number ? number : fallback;

    /// <summary>取 TAG_List，元素通常是 Dictionary（compound）。</summary>
    public static List<object> List(Dictionary<string, object>? map, string key)
        => map != null && map.TryGetValue(key, out var value) && value is List<object> list ? list : new List<object>();
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PCLN.AdvancedHomepage;

/// <summary>一次服务器状态查询的结果。</summary>
internal sealed class ServerStatus
{
    public bool Ok;
    public int Online;
    public int Max;
    public string Version = "";
    public string Motd = "";

    /// <summary>卡片上的那行字：「103 人在线」/「离线」。</summary>
    public string Text => Ok
        ? (Max > 0 ? $"{Online} 人在线" : "在线")
        : "离线";
}

/// <summary>
/// Minecraft 服务器状态查询（Server List Ping）：先查 <c>_minecraft._tcp</c> 的 SRV 记录，
/// 再按协议做一次握手 + status 请求，解析返回的 JSON 拿到在线人数。
/// 全部跑在后台线程，失败不抛异常，只在卡片上显示「离线」。
/// </summary>
internal static class ServerPing
{
    private const int TimeoutMs = 2200;
    private static readonly TimeSpan CacheAge = TimeSpan.FromSeconds(60);

    private static readonly object Gate = new();
    private static readonly Dictionary<string, (ServerStatus Status, DateTime Time)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>取状态（带 60 秒缓存），阻塞调用，别在 UI 线程直接用。</summary>
    public static ServerStatus Get(string address, bool force = false)
    {
        var key = (address ?? "").Trim();
        if (key.Length == 0) return new ServerStatus();

        lock (Gate)
        {
            if (!force && Cache.TryGetValue(key, out var cached) && DateTime.Now - cached.Time < CacheAge)
                return cached.Status;
        }

        var status = Query(key);

        lock (Gate) Cache[key] = (status, DateTime.Now);
        Log.Info($"服务器状态 {key}：{(status.Ok ? $"{status.Online}/{status.Max}，{status.Version}" : "离线")}");
        return status;
    }

    /// <summary>后台查询，完成后回到 UI 线程回调。</summary>
    public static void GetAsync(string address, Action<ServerStatus> onDone)
    {
        Task.Run(() => Get(address)).ContinueWith(task =>
        {
            if (task.IsFaulted || task.Result == null) return;
            var status = task.Result;
            onDone(status);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ─────────────────────────── 协议 ───────────────────────────

    private static ServerStatus Query(string address)
    {
        var status = new ServerStatus();

        try
        {
            var (host, port) = Split(address);
            if (host.Length == 0) return status;

            var (target, finalPort) = Resolve(host, port);

            using var client = new TcpClient();
            var watch = Stopwatch.StartNew();

            var connect = client.ConnectAsync(target, finalPort);
            if (!connect.Wait(TimeoutMs)) return status;
            if (!client.Connected) return status;

            watch.Restart();
            using var stream = client.GetStream();
            stream.ReadTimeout = TimeoutMs;
            stream.WriteTimeout = TimeoutMs;

            // handshake：协议号随便给个 47，服务器状态查询不看这个
            var handshake = new List<byte>();
            WriteVarInt(handshake, 0x00);
            WriteVarInt(handshake, 47);
            WriteString(handshake, host);
            handshake.Add((byte)(finalPort >> 8));
            handshake.Add((byte)(finalPort & 0xFF));
            WriteVarInt(handshake, 1);          // next state = status

            WritePacket(stream, handshake);
            WritePacket(stream, new List<byte> { 0x00 });   // status request

            int length = ReadVarInt(stream);
            if (length <= 0 || length > 4 * 1024 * 1024) return status;

            int packetId = ReadVarInt(stream);
            if (packetId != 0x00) return status;

            var json = ReadString(stream);
            if (string.IsNullOrWhiteSpace(json)) return status;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("players", out var players))
            {
                if (players.TryGetProperty("online", out var online) && online.ValueKind == JsonValueKind.Number)
                    status.Online = online.GetInt32();
                if (players.TryGetProperty("max", out var max) && max.ValueKind == JsonValueKind.Number)
                    status.Max = max.GetInt32();
            }

            if (root.TryGetProperty("version", out var version) &&
                version.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String)
            {
                status.Version = name.GetString() ?? "";
            }

            if (root.TryGetProperty("description", out var description))
            {
                status.Motd = Description(description);
            }

            status.Ok = watch.ElapsedMilliseconds < TimeoutMs * 2;
            return status;
        }
        catch (Exception ex)
        {
            Log.Warn($"查询服务器状态失败（{address}）：{ex.Message}");
            return status;
        }
    }

    /// <summary>description 可能是字符串，也可能是带 extra 的聊天组件。</summary>
    private static string Description(JsonElement element)
    {
        try
        {
            if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? "";

            var text = new StringBuilder();
            if (element.TryGetProperty("text", out var basic) && basic.ValueKind == JsonValueKind.String)
                text.Append(basic.GetString());

            if (element.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in extra.EnumerateArray())
                {
                    if (part.ValueKind == JsonValueKind.String) text.Append(part.GetString());
                    else if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        text.Append(t.GetString());
                }
            }

            return text.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static (string Host, int Port) Split(string address)
    {
        var text = address.Trim();
        if (text.Length == 0) return ("", 25565);

        // [::1]:25565 这种先不放眼里，只处理 host:port
        int colon = text.LastIndexOf(':');
        if (colon > 0 && text.IndexOf(':') == colon)
        {
            var portText = text[(colon + 1)..];
            if (int.TryParse(portText, out var port) && port > 0 && port <= 65535)
                return (text[..colon], port);
        }

        return (text, 25565);
    }

    /// <summary>域名先查 SRV（很多国内服务器只挂 SRV，域名本身不解析）。</summary>
    private static (string Host, int Port) Resolve(string host, int port)
    {
        if (IPAddress.TryParse(host, out _)) return (host, port);

        try
        {
            var srv = DnsSrv.Lookup("_minecraft._tcp." + host);
            if (srv != null)
            {
                Log.Info($"服务器 {host} 使用 SRV：{srv.Value.Target}:{srv.Value.Port}");
                return srv.Value;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"查询 {host} 的 SRV 失败：{ex.Message}");
        }

        return (host, port);
    }

    // ─────────────────────────── 包读写 ───────────────────────────

    private static void WriteVarInt(List<byte> buffer, int value)
    {
        uint v = unchecked((uint)value);
        do
        {
            byte b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80;
            buffer.Add(b);
        } while (v != 0);
    }

    private static void WriteString(List<byte> buffer, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        WriteVarInt(buffer, bytes.Length);
        buffer.AddRange(bytes);
    }

    private static void WritePacket(Stream stream, List<byte> payload)
    {
        var packet = new List<byte>();
        WriteVarInt(packet, payload.Count);
        packet.AddRange(payload);
        stream.Write(packet.ToArray(), 0, packet.Count);
        stream.Flush();
    }

    private static int ReadVarInt(Stream stream)
    {
        int value = 0, shift = 0;
        while (true)
        {
            int b = stream.ReadByte();
            if (b < 0) throw new EndOfStreamException("连接已关闭");
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return value;
            shift += 7;
            if (shift > 35) throw new InvalidDataException("VarInt 过长");
        }
    }

    private static string ReadString(Stream stream)
    {
        int length = ReadVarInt(stream);
        if (length <= 0) return "";
        var buffer = new byte[length];
        int read = 0;
        while (read < length)
        {
            int count = stream.Read(buffer, read, length - read);
            if (count <= 0) break;
            read += count;
        }

        return Encoding.UTF8.GetString(buffer, 0, read);
    }
}

/// <summary>极简 DNS SRV 查询（.NET 没有内置 SRV，只能自己发 UDP 包）。</summary>
internal static class DnsSrv
{
    public static (string Target, int Port)? Lookup(string name)
    {
        foreach (var server in Servers())
        {
            try
            {
                var result = Query(server, name);
                if (result != null) return result;
            }
            catch
            {
                // 换下一个 DNS
            }
        }

        return null;
    }

    private static IEnumerable<IPAddress> Servers()
    {
        var list = new List<IPAddress>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var address in nic.GetIPProperties().DnsAddresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork && !list.Contains(address))
                        list.Add(address);
                }
            }
        }
        catch
        {
            // 拿不到网卡信息就用公共 DNS
        }

        foreach (var fallback in new[] { "223.5.5.5", "119.29.29.29", "8.8.8.8" })
        {
            if (IPAddress.TryParse(fallback, out var ip) && !list.Contains(ip)) list.Add(ip);
        }

        return list;
    }

    private static (string Target, int Port)? Query(IPAddress server, string name)
    {
        var packet = new List<byte>();

        // ── 头部 ──
        var id = (ushort)Random.Shared.Next(0, ushort.MaxValue);
        packet.Add((byte)(id >> 8));
        packet.Add((byte)(id & 0xFF));
        packet.Add(0x01); packet.Add(0x00);   // 标准查询，递归
        packet.Add(0x00); packet.Add(0x01);   // QDCOUNT = 1
        for (int i = 0; i < 6; i++) packet.Add(0x00);

        // ── 问题 ──
        foreach (var label in name.TrimEnd('.').Split('.'))
        {
            if (label.Length == 0) continue;
            var bytes = Encoding.ASCII.GetBytes(label);
            packet.Add((byte)bytes.Length);
            packet.AddRange(bytes);
        }

        packet.Add(0x00);                      // 名字结束
        packet.Add(0x00); packet.Add(0x21);    // QTYPE = SRV (33)
        packet.Add(0x00); packet.Add(0x01);    // QCLASS = IN

        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 1600;
        udp.Connect(server, 53);
        udp.Send(packet.ToArray(), packet.Count);

        var endpoint = new IPEndPoint(IPAddress.Any, 0);
        var response = udp.Receive(ref endpoint);
        if (response.Length < 12) return null;

        int offset = 12;
        int questions = (response[4] << 8) | response[5];
        int answers = (response[6] << 8) | response[7];

        for (int i = 0; i < questions; i++)
        {
            SkipName(response, ref offset);
            offset += 4;
        }

        (string Target, int Port)? best = null;
        int bestPriority = int.MaxValue;

        for (int i = 0; i < answers && offset + 10 <= response.Length; i++)
        {
            SkipName(response, ref offset);
            if (offset + 10 > response.Length) break;

            int type = (response[offset] << 8) | response[offset + 1];
            int length = (response[offset + 8] << 8) | response[offset + 9];
            offset += 10;

            if (type == 33 && length >= 7)
            {
                int priority = (response[offset] << 8) | response[offset + 1];
                int port = (response[offset + 4] << 8) | response[offset + 5];
                int nameOffset = offset + 6;
                var target = ReadName(response, ref nameOffset).TrimEnd('.');

                if (port > 0 && target.Length > 0 && priority < bestPriority)
                {
                    bestPriority = priority;
                    best = (target, port);
                }
            }

            offset += length;
        }

        return best;
    }

    private static void SkipName(byte[] data, ref int offset)
    {
        while (offset < data.Length)
        {
            int length = data[offset];
            if (length == 0)
            {
                offset++;
                return;
            }

            if ((length & 0xC0) == 0xC0)
            {
                offset += 2;
                return;
            }

            offset += length + 1;
        }
    }

    private static string ReadName(byte[] data, ref int offset)
    {
        var parts = new List<string>();
        int guard = 0;

        while (offset < data.Length && guard++ < 64)
        {
            int length = data[offset];
            if (length == 0)
            {
                offset++;
                break;
            }

            if ((length & 0xC0) == 0xC0)
            {
                if (offset + 1 >= data.Length) break;
                int pointer = ((length & 0x3F) << 8) | data[offset + 1];
                offset += 2;

                int resume = offset;
                int pointed = pointer;
                parts.Add(ReadName(data, ref pointed));
                offset = resume;
                break;
            }

            offset++;
            if (offset + length > data.Length) break;
            parts.Add(Encoding.ASCII.GetString(data, offset, length));
            offset += length;
        }

        return string.Join(".", parts.Where(p => p.Length > 0));
    }
}

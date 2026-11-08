using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace PCLN.AdvancedHomepage;

/// <summary>用户在游戏里加过的一个服务器（servers.dat 里的一项）。</summary>
internal sealed class ServerEntry
{
    public string Name = "";
    public string Ip = "";
    public string Instance = "";
    public BitmapSource? Icon;
}

/// <summary>
/// 读 <c>servers.dat</c>。这个文件是游戏自己维护的（NBT 格式，存档里带 base64 图标），
/// 每个实例独立一份，根目录 <c>.minecraft/servers.dat</c> 是所有实例共用的那份。
/// </summary>
internal static class ServerStore
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, (List<ServerEntry> List, DateTime Time)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>某个实例里加过的服务器；实例自己没有 servers.dat 时用全局那份。</summary>
    public static List<ServerEntry> ForInstance(string instance, bool force = false)
    {
        var key = instance ?? "";

        lock (Gate)
        {
            if (!force && Cache.TryGetValue(key, out var cached) &&
                (DateTime.Now - cached.Time).TotalSeconds < 30)
            {
                return cached.List;
            }

            var list = Read(instance);
            Cache[key] = (list, DateTime.Now);
            return list;
        }
    }

    private static List<ServerEntry> Read(string instance)
    {
        var result = new List<ServerEntry>();

        var mc = WorldStore.McFolder;
        if (mc == null) return result;

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(instance))
            candidates.Add(Path.Combine(mc, "versions", instance, "servers.dat"));
        candidates.Add(Path.Combine(mc, "servers.dat"));

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;

            var root = Nbt.ReadAutoFile(path);
            var servers = Nbt.List(root, "servers");
            if (servers.Count == 0) continue;

            foreach (var item in servers)
            {
                if (item is not Dictionary<string, object> server) continue;

                var name = Nbt.String(server, "name");
                var ip = Nbt.String(server, "ip");
                if (string.IsNullOrWhiteSpace(ip)) continue;

                result.Add(new ServerEntry
                {
                    Name = string.IsNullOrWhiteSpace(name) ? ip : name,
                    Ip = ip,
                    Instance = instance ?? "",
                    Icon = DecodeIcon(Nbt.String(server, "icon"))
                });
            }

            // 实例自己那份读到了就不再往下找
            if (result.Count > 0) break;
        }

        Log.Info($"服务器列表（{(string.IsNullOrWhiteSpace(instance) ? "全局" : instance)}）：{result.Count} 个");
        return result;
    }

    /// <summary>servers.dat 里的图标是 base64 的 PNG，可能带 data URI 前缀。</summary>
    private static BitmapSource? DecodeIcon(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;

        try
        {
            var text = base64.Trim();
            int comma = text.IndexOf(",", StringComparison.Ordinal);
            if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
                text = text.Substring(comma + 1);

            var bytes = Convert.FromBase64String(text);
            if (bytes.Length == 0) return null;

            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = 112;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>按地址找某个服务器（卡片刷新时用）。</summary>
    public static ServerEntry? Find(string instance, string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        return ForInstance(instance).FirstOrDefault(s => string.Equals(s.Ip, ip, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>按地址找某个实例（世界、服务器快捷卡片都要靠实例名启动）。</summary>
    public static InstanceEntry? FindInstance(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return InstanceStore.Scan()
            .FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}

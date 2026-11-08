using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PCLN.AdvancedHomepage;

/// <summary>一个 Minecraft 实例（versions 下的一个版本目录）。</summary>
public sealed class InstanceEntry
{
    public string Name = "";
    public string Folder = "";
    public string Version = "";
    public string Loader = "";
    public DateTime LastPlayed;
    public string IconPath = "";

    /// <summary>启动器给这个实例算出来的图标键（比如 Blocks/Fabric.png），没有自定义 Logo 时用它。</summary>
    public string LogoKey = "";

    public bool Starred;
    public int LaunchCount;

    /// <summary>「Fabric 1.20.1」这种展示串，没有加载器时只显示版本号。</summary>
    public string VersionText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Loader)) return string.IsNullOrWhiteSpace(Version) ? "未知版本" : Version;
            return string.IsNullOrWhiteSpace(Version) ? Loader : $"{Loader} {Version}";
        }
    }
}

/// <summary>
/// 扫描 .minecraft/versions 得到实例列表。和 <see cref="WorldStore"/> 一样只读文件系统，
/// 不依赖启动器内部缓存，因此 PCL 还没加载完实例列表时也能用。
/// </summary>
internal static class InstanceStore
{
    private static readonly object Gate = new();
    private static List<InstanceEntry> _cache = new();
    private static DateTime _cacheTime = DateTime.MinValue;

    public static IReadOnlyList<InstanceEntry> Recent(int max = 4)
    {
        var all = Scan();
        return all
            .OrderByDescending(e => e.Starred)              // 收藏的排在前面，和启动器的星标一致
            .ThenByDescending(e => e.LastPlayed)
            .Take(max)
            .ToList();
    }

    public static int Count => Scan().Count;

    public static List<InstanceEntry> Scan(bool force = false)
    {
        lock (Gate)
        {
            if (!force && _cacheTime != DateTime.MinValue &&
                (DateTime.Now - _cacheTime).TotalSeconds < 30)
            {
                return _cache;
            }

            var list = new List<InstanceEntry>();
            var mc = WorldStore.McFolder;
            if (mc == null)
            {
                _cache = list;
                _cacheTime = DateTime.Now;
                return list;
            }

            var versions = Path.Combine(mc, "versions");
            if (!Directory.Exists(versions))
            {
                _cache = list;
                _cacheTime = DateTime.Now;
                return list;
            }

            foreach (var dir in SafeDirs(versions))
            {
                try
                {
                    var entry = Read(dir);
                    if (entry != null) list.Add(entry);
                }
                catch (Exception ex)
                {
                    Log.Warn($"读取实例失败（{Path.GetFileName(dir)}）：{ex.Message}");
                }
            }

            _cache = list;
            _cacheTime = DateTime.Now;
            Log.Info($"实例扫描完成：{list.Count} 个（{versions}）");
            return list;
        }
    }

    private static InstanceEntry? Read(string dir)
    {
        var folder = Path.GetFileName(dir);
        var jsonFile = Path.Combine(dir, folder + ".json");
        if (!File.Exists(jsonFile))
        {
            jsonFile = SafeFiles(dir, "*.json")
                .FirstOrDefault(p => !Path.GetFileName(p).StartsWith("iris", StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(jsonFile) || !File.Exists(jsonFile))
        {
            // 没有版本 json 的目录（比如只有 jar）不算实例
            return null;
        }

        var entry = new InstanceEntry { Name = folder, Folder = dir };

        using (var doc = JsonDocument.Parse(File.ReadAllText(jsonFile)))
        {
            var root = doc.RootElement;

            // 目录里常混着 usercache.json / ponders_watched.json 这类文件，
            // 只有真正的版本 json 才带 mainClass / id / libraries 之一
            if (!IsVersionJson(root)) return null;

            entry.Version = Str(root, "clientVersion");
            if (string.IsNullOrWhiteSpace(entry.Version)) entry.Version = Str(root, "inheritsFrom");
            if (string.IsNullOrWhiteSpace(entry.Version)) entry.Version = GuessVersion(folder);

            entry.Loader = DetectLoader(root, folder);
        }

        ReadMetadata(dir, entry);
        entry.LastPlayed = LastWrite(dir);
        entry.IconPath = ResolveIcon(dir, entry.Name, entry.IconPath);
        return entry;
    }

    private static bool IsVersionJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;

        foreach (var name in new[] { "mainClass", "id", "libraries", "inheritsFrom", "clientVersion" })
        {
            if (root.TryGetProperty(name, out _)) return true;
        }

        return false;
    }

    /// <summary>PCL 自己的实例元数据：星标、启动次数、自定义图标。</summary>
    private static void ReadMetadata(string dir, InstanceEntry entry)
    {
        var meta = Path.Combine(dir, "PCL", "InstanceMetadata.json");
        if (!File.Exists(meta)) return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(meta));
            var root = doc.RootElement;
            entry.Starred = Bool(root, "isStarred");
            entry.LaunchCount = Int(root, "launchCount");
            entry.IconPath = Str(root, "logoPath");
        }
        catch (Exception ex)
        {
            Log.Warn($"读取实例元数据失败（{entry.Name}）：{ex.Message}");
        }
    }

    private static string ResolveIcon(string dir, string name, string preferred)
    {
        // PCL 给实例设的自定义图标就存在 <实例>/PCL/Logo.png，优先用它
        var logo = Path.Combine(dir, "PCL", "Logo.png");
        if (File.Exists(logo)) return logo;

        if (!string.IsNullOrWhiteSpace(preferred) && File.Exists(preferred)) return preferred;

        var local = Path.Combine(dir, "icon.png");
        if (File.Exists(local)) return local;

        // PCL 的自定义图标通常落在数据目录的 Pictures\InstanceIcon 下
        try
        {
            var data = Path.GetDirectoryName(WorldStore.McFolder);
            if (!string.IsNullOrWhiteSpace(data))
            {
                foreach (var candidate in new[]
                {
                    Path.Combine(data!, "Pictures", "InstanceIcon", name + ".png"),
                    Path.Combine(data!, "PCL", "Pictures", "InstanceIcon", name + ".png")
                })
                {
                    if (File.Exists(candidate)) return candidate;
                }
            }
        }
        catch
        {
            // 忽略：没有自定义图标就走占位图
        }

        return "";
    }

    /// <summary>最后游玩时间：优先看日志与 options.txt 的写入时间。</summary>
    private static DateTime LastWrite(string dir)
    {
        var latest = DateTime.MinValue;
        foreach (var name in new[] { "logs/latest.log", "options.txt", "logs/debug.log" })
        {
            try
            {
                var file = Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file)) continue;
                var time = File.GetLastWriteTime(file);
                if (time > latest) latest = time;
            }
            catch
            {
                // 文件被占用时跳过
            }
        }

        return latest;
    }

    private static string DetectLoader(JsonElement root, string folder)
    {
        var names = new List<string>();
        try
        {
            if (root.TryGetProperty("libraries", out var libs) && libs.ValueKind == JsonValueKind.Array)
            {
                foreach (var lib in libs.EnumerateArray())
                {
                    var n = Str(lib, "name");
                    if (!string.IsNullOrWhiteSpace(n)) names.Add(n.ToLowerInvariant());
                }
            }
        }
        catch
        {
            // 结构异常就只靠目录名判断
        }

        string haystack = string.Join("\n", names) + "\n" + folder.ToLowerInvariant();

        if (haystack.Contains("neoforge")) return "NeoForge";
        if (haystack.Contains("quilt-loader") || haystack.Contains("org.quiltmc")) return "Quilt";
        if (haystack.Contains("fabric-loader") || haystack.Contains("net.fabricmc")) return "Fabric";
        if (haystack.Contains("minecraftforge") || haystack.Contains("forge")) return "Forge";
        if (haystack.Contains("optifine")) return "OptiFine";
        return "";
    }

    /// <summary>从目录名里抠版本号，例如「1.20.1-Forge_47.4.23」→ 1.20.1。</summary>
    private static string GuessVersion(string folder)
    {
        var text = folder;
        for (int i = 0; i + 3 <= text.Length; i++)
        {
            if (text[i] != '1' || text[i + 1] != '.') continue;

            int j = i + 2;
            while (j < text.Length && (char.IsDigit(text[j]) || text[j] == '.')) j++;
            var token = text.Substring(i, j - i).TrimEnd('.');
            if (token.Count(c => c == '.') >= 1) return token;
        }

        return "";
    }

    private static string Str(JsonElement element, string name)
    {
        try
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? "";
            }
        }
        catch
        {
            // 忽略字段类型不符
        }

        return "";
    }

    private static bool Bool(JsonElement element, string name)
    {
        try
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(name, out var value))
            {
                return value.ValueKind == JsonValueKind.True;
            }
        }
        catch
        {
            // 忽略
        }

        return false;
    }

    private static int Int(JsonElement element, string name)
    {
        try
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.Number)
            {
                return value.GetInt32();
            }
        }
        catch
        {
            // 忽略
        }

        return 0;
    }

    private static IEnumerable<string> SafeDirs(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeFiles(string path, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(path, pattern).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

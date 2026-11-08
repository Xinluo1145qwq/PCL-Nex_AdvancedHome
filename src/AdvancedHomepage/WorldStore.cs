using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCLN.AdvancedHomepage;

/// <summary>一个存档（世界）的摘要信息。</summary>
internal sealed class WorldEntry
{
    public string Folder = "";
    public string Name = "";
    public string IconPath = "";
    public DateTime LastPlayed;
    public string Version = "";
    public string Instance = "";

    /// <summary>0 生存 / 1 创造 / 2 冒险 / 3 旁观，-1 表示读不到。</summary>
    public int GameType = -1;

    /// <summary>「创造模式」这种说法，读不到时为空串。</summary>
    public string GameModeText => GameType switch
    {
        0 => "生存模式",
        1 => "创造模式",
        2 => "冒险模式",
        3 => "旁观模式",
        _ => ""
    };
}

/// <summary>
/// 扫描 .minecraft 下的存档。数据全部来自文件系统（level.dat / icon.png），
/// 不依赖启动器内部结构，因此版本升级后依然可用。
/// </summary>
internal static class WorldStore
{
    private static readonly object Gate = new();
    private static List<WorldEntry> _cache = new();
    private static DateTime _cacheTime = DateTime.MinValue;

    /// <summary>当前使用的 .minecraft 目录。</summary>
    public static string? McFolder
    {
        get
        {
            try
            {
                var folder = PCL.ModFolder.mcFolderSelected;
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)) return folder;
            }
            catch (Exception ex)
            {
                Log.Warn("读取 MC 目录失败：" + ex.Message);
            }

            foreach (var guess in new[]
            {
                Path.Combine(AppContext.BaseDirectory, ".minecraft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft")
            })
            {
                if (Directory.Exists(guess)) return guess;
            }

            return null;
        }
    }

    /// <summary>取最近游玩的存档，按最后游玩时间倒序。</summary>
    public static IReadOnlyList<WorldEntry> Recent(int max = 4, TimeSpan? maxAge = null)
    {
        var all = Scan();
        IEnumerable<WorldEntry> query = all;
        if (maxAge is { } age)
        {
            var floor = DateTime.Now - age;
            query = query.Where(w => w.LastPlayed >= floor);
        }

        return query.OrderByDescending(w => w.LastPlayed).Take(max).ToList();
    }

    /// <summary>扫描结果带 30 秒缓存，避免每次进主页都去读盘。</summary>
    public static List<WorldEntry> Scan(bool force = false)
    {
        lock (Gate)
        {
            if (!force && _cacheTime != DateTime.MinValue &&
                (DateTime.Now - _cacheTime).TotalSeconds < 30)
            {
                return _cache;
            }

            var list = new List<WorldEntry>();
            var mc = McFolder;
            if (mc == null)
            {
                Log.Warn("未找到 .minecraft 目录，跳过存档扫描");
                _cache = list;
                _cacheTime = DateTime.Now;
                return list;
            }

            var savesRoot = Path.Combine(mc, "saves");
            Collect(list, savesRoot, "全局");

            var versions = Path.Combine(mc, "versions");
            if (Directory.Exists(versions))
            {
                foreach (var instance in SafeDirs(versions))
                {
                    Collect(list, Path.Combine(instance, "saves"), Path.GetFileName(instance));
                }
            }

            _cache = list;
            _cacheTime = DateTime.Now;
            Log.Info($"存档扫描完成：{list.Count} 个（{mc}）");
            return list;
        }
    }

    /// <summary>只扫某个实例自己的存档目录（「选择世界」时用），按最后游玩时间倒序。</summary>
    public static List<WorldEntry> ScanInstance(string instance)
    {
        var list = new List<WorldEntry>();
        if (string.IsNullOrWhiteSpace(instance)) return list;

        var mc = McFolder;
        if (mc == null) return list;

        Collect(list, Path.Combine(mc, "versions", instance, "saves"), instance);
        return list.OrderByDescending(w => w.LastPlayed).ToList();
    }

    private static void Collect(List<WorldEntry> target, string savesRoot, string instance)
    {
        if (!Directory.Exists(savesRoot)) return;

        foreach (var folder in SafeDirs(savesRoot))
        {
            try
            {
                var levelFile = Path.Combine(folder, "level.dat");
                if (!File.Exists(levelFile)) continue;

                var root = Nbt.ReadGzipFile(levelFile);
                var data = Nbt.Object(root, "Data");
                if (data == null) continue;

                var entry = new WorldEntry
                {
                    Folder = folder,
                    Name = Nbt.String(data, "LevelName", Path.GetFileName(folder)),
                    Instance = instance,
                    Version = Nbt.String(Nbt.Object(data, "Version"), "Name"),
                    GameType = Nbt.Int(data, "GameType", -1)
                };

                long lastPlayed = Nbt.Long(data, "LastPlayed");
                entry.LastPlayed = lastPlayed > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(lastPlayed).LocalDateTime
                    : File.GetLastWriteTime(levelFile);

                var icon = Path.Combine(folder, "icon.png");
                if (File.Exists(icon)) entry.IconPath = icon;

                if (string.IsNullOrWhiteSpace(entry.Name)) entry.Name = Path.GetFileName(folder);
                target.Add(entry);
            }
            catch (Exception ex)
            {
                Log.Warn($"读取存档失败（{Path.GetFileName(folder)}）：{ex.Message}");
            }
        }
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

    /// <summary>把时间转成「刚刚 / 3天前 / 2个月前」这种说法（中文不夹空格，和启动器一致）。</summary>
    public static string Humanize(DateTime time)
    {
        if (time <= DateTime.MinValue.AddYears(1)) return "从未游玩";

        var span = DateTime.Now - time;
        if (span.TotalMinutes < 1) return "刚刚";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}分钟前";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}小时前";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}天前";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)}个月前";
        return $"{(int)(span.TotalDays / 365)}年前";
    }
}

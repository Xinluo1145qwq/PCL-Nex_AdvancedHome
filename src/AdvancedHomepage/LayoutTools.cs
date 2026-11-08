using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 布局的导出 / 备份 / 重置。设置窗口「重置与备份」那张卡片的背后逻辑都放这里，
/// 窗口只负责按钮和提示文字。
/// </summary>
internal static class LayoutTools
{
    /// <summary>三套主页固定按这个顺序导出。</summary>
    public static readonly string[] Styles = { "fancy", "minimal", "notes" };

    public static string StyleName(string? style) => Cfg.NormalizeStyle(style) switch
    {
        "minimal" => "极简",
        "notes" => "便签",
        _ => "花样"
    };

    /// <summary>组件 id → 中文名。导出成 txt 给人看，写 id 没有意义。</summary>
    public static string WidgetName(string id)
    {
        return id switch
        {
            "greeting" => "问候",
            "calendar" => "日历",
            "clock" => "时钟",
            "weather" => "天气",
            "calculator" => "计算器",
            "minimal" => "标语卡片",
            "note" => "便签",
            "todo" => "待办事项",
            "sites" => "多个网站",
            "download" => "下载自定义文件",
            "instances" => "最近游玩的实例",
            "worlds" => "最近游玩的世界",
            _ when id.StartsWith("pin-", StringComparison.Ordinal) => "快捷入口",
            _ when id.StartsWith("skin-", StringComparison.Ordinal) => "皮肤预览",
            _ => id
        };
    }

    /// <summary>把三套主页的布局写成人能看懂的文本。</summary>
    public static string ExportText()
    {
        string version;
        try
        {
            version = typeof(LayoutTools).Assembly.GetName().Version is { } v
                ? $"{v.Major}.{v.Minor}.{v.Build}"
                : "?";
        }
        catch
        {
            version = "?";
        }

        var sb = new StringBuilder();
        sb.AppendLine("高级主页 · 布局导出");
        sb.AppendLine("版本   v" + version);
        sb.AppendLine("时间   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("说明   列 / 行从 1 开始；网格共 " + Cfg.GridCols + " 列。");
        sb.AppendLine();

        foreach (var style in Styles)
        {
            var list = Cfg.SnapshotLayout(style)
                .OrderBy(c => c.Row).ThenBy(c => c.Col)
                .ToList();

            sb.AppendLine("[" + StyleName(style) + "]  " + list.Count + " 个组件");
            if (list.Count == 0) sb.AppendLine("  （空）");

            foreach (var card in list)
            {
                sb.AppendLine(
                    $"  {WidgetName(card.Id),-12} 列 {card.Col + 1,2}  行 {card.Row + 1,2}  " +
                    $"宽 {card.ColSpan}  高 {card.RowSpan}    ({card.Id})");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>把 config.json 复制到数据目录的 backups 里，返回备份文件路径。</summary>
    public static string Backup()
    {
        var source = Paths.ConfigFile;
        if (!File.Exists(source))
            throw new FileNotFoundException("还没有生成配置文件", source);

        var dir = Path.Combine(Paths.DataDir, "backups");
        Directory.CreateDirectory(dir);

        string name = "config-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
        var target = Path.Combine(dir, name);

        // 同一秒点两次也别互相覆盖
        int i = 2;
        while (File.Exists(target))
        {
            target = Path.Combine(dir, $"config-{DateTime.Now:yyyyMMdd-HHmmss}-{i}.json");
            i++;
        }

        File.Copy(source, target, true);
        return target;
    }

    /// <summary>写文本文件：带 BOM 的 UTF-8，记事本打开不会乱码。</summary>
    public static void WriteText(string path, string text)
    {
        File.WriteAllText(path, text, new UTF8Encoding(true));
    }

    // ───────────────────────── 导入 / 恢复 ─────────────────────────

    /// <summary>备份文件夹里的一个文件，给「从备份恢复」的列表用。</summary>
    public sealed class BackupEntry
    {
        public string Path { get; init; } = "";
        public DateTime Time { get; init; }
        public long Size { get; init; }
        public string Style { get; init; } = "fancy";

        /// <summary>按 Styles 顺序（花样 / 极简 / 便签）的卡片数。</summary>
        public int[] Counts { get; init; } = Array.Empty<int>();

        public int Total => Counts.Sum();

        public string Title => Time.ToString("yyyy-MM-dd HH:mm:ss");

        public string Detail => Counts.Length == 3
            ? $"花样 {Counts[0]} / 极简 {Counts[1]} / 便签 {Counts[2]}，共 {Total} 个组件"
            : $"{Total} 个组件";
    }

    /// <summary>一份可以被写回配置的布局。</summary>
    public sealed class LayoutBundle
    {
        public Dictionary<string, List<CardPlacement>> Layouts { get; } = new();
        public string? Style { get; set; }
        public string Source { get; set; } = "";

        public int Total => Layouts.Values.Sum(v => v.Count);

        /// <summary>「花样 14 · 极简 1 · 便签 2」这种一眼能看懂的一行摘要。</summary>
        public string Summary => string.Join(" · ", Styles.Select(style =>
            Layouts.TryGetValue(style, out var list) ? $"{StyleName(style)} {list.Count}" : $"{StyleName(style)} –"));

        /// <summary>这份布局里当前版本不认识、照样写进配置但不会显示的组件数。</summary>
        public int UnknownCount => Layouts.Values.SelectMany(v => v).Count(c => !IsKnownWidget(c.Id));
    }

    private static readonly HashSet<string> KnownWidgets = new(StringComparer.Ordinal)
    {
        "greeting", "calendar", "clock", "weather", "calculator", "minimal",
        "note", "todo", "sites", "download", "instances", "worlds"
    };

    public static bool IsKnownWidget(string id) =>
        KnownWidgets.Contains(id)
        || id.StartsWith("pin-", StringComparison.Ordinal)
        || id.StartsWith("skin-", StringComparison.Ordinal);

    /// <summary>备份文件夹里的所有可读文件，最新的排最前。</summary>
    public static List<BackupEntry> ListBackups()
    {
        var result = new List<BackupEntry>();
        var dir = Path.Combine(Paths.DataDir, "backups");
        if (!Directory.Exists(dir)) return result;

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var bundle = ReadJson(file);
                result.Add(new BackupEntry
                {
                    Path = file,
                    Time = File.GetLastWriteTime(file),
                    Size = new FileInfo(file).Length,
                    Style = bundle.Style ?? "fancy",
                    Counts = Styles
                        .Select(style => bundle.Layouts.TryGetValue(style, out var list) ? list.Count : 0)
                        .ToArray()
                });
            }
            catch (Exception ex)
            {
                // 单个坏文件不该让整个列表空掉
                Log.Warn($"备份文件读不出来，已跳过：{Path.GetFileName(file)} · {ex.Message}");
            }
        }

        return result.OrderByDescending(e => e.Time).ToList();
    }

    /// <summary>读一个布局文件：.json（配置备份）走 json，其它按导出的 txt 解析。</summary>
    public static LayoutBundle LoadBundle(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到这个文件", path);

        bool json = string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);
        var bundle = json ? ReadJson(path) : ReadText(path);

        if (bundle.Total == 0)
            throw new InvalidDataException("这个文件里没有任何组件位置，可能选错了文件。");

        bundle.Source = path;
        return bundle;
    }

    /// <summary>把布局写回配置。<paramref name="restoreStyle"/> = 连当前主页一起切回备份时的样子。</summary>
    public static void Apply(LayoutBundle bundle, bool restoreStyle)
    {
        foreach (var pair in bundle.Layouts)
            Cfg.RestoreLayout(pair.Key, pair.Value);

        if (restoreStyle && !string.IsNullOrWhiteSpace(bundle.Style))
            Cfg.SetStyle(bundle.Style!);

        Cfg.Save();
    }

    private static LayoutBundle ReadJson(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("不是有效的 json 对象。");

        if (!TryProp(root, "layouts", out var layouts) || layouts.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("这个 json 里没有 layouts 段，不像是主页布局备份。");

        var bundle = new LayoutBundle();

        string? style = StringProp(root, "style");
        if (!string.IsNullOrWhiteSpace(style)) bundle.Style = Cfg.NormalizeStyle(style);

        foreach (var prop in layouts.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;

            var list = new List<CardPlacement>();
            foreach (var item in prop.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                string? id = StringProp(item, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (list.Any(c => c.Id == id)) continue;

                list.Add(new CardPlacement
                {
                    Id = id.Trim(),
                    Col = NumberProp(item, "col"),
                    Row = NumberProp(item, "row"),
                    ColSpan = Math.Max(1, NumberProp(item, "colSpan")),
                    RowSpan = Math.Max(1, NumberProp(item, "rowSpan"))
                });
            }

            bundle.Layouts[Cfg.NormalizeStyle(prop.Name)] = list;
        }

        return bundle;
    }

    private static bool TryProp(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = prop.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static string? StringProp(JsonElement obj, string name)
    {
        return TryProp(obj, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int NumberProp(JsonElement obj, string name)
    {
        if (!TryProp(obj, name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsed)) return parsed;
        return 0;
    }

    // 导出格式里每一行长这样：
    //   问候          列  7  行  1  宽 3  高 1    (greeting)
    private static readonly Regex CardLine = new(
        @"列\s*(?<col>\d+)\s+行\s*(?<row>\d+)\s+宽\s*(?<cs>\d+)\s+高\s*(?<rs>\d+)\s*\((?<id>[^)]+)\)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex SectionLine = new(@"^\[(?<name>[^\]]+)\]", RegexOptions.Compiled);

    private static LayoutBundle ReadText(string path)
    {
        var bundle = new LayoutBundle();
        string style = "fancy";

        foreach (var raw in File.ReadAllLines(path))
        {
            var section = SectionLine.Match(raw.Trim());
            if (section.Success)
            {
                style = StyleFromName(section.Groups["name"].Value);
                continue;
            }

            var match = CardLine.Match(raw);
            if (!match.Success) continue;

            string id = match.Groups["id"].Value.Trim();
            if (id.Length == 0) continue;

            if (!bundle.Layouts.TryGetValue(style, out var list))
                bundle.Layouts[style] = list = new List<CardPlacement>();

            if (list.Any(c => c.Id == id)) continue;

            int Number(string group) =>
                int.TryParse(match.Groups[group].Value, out int value) ? value : 1;

            list.Add(new CardPlacement
            {
                Id = id,
                Col = Math.Max(0, Number("col") - 1),
                Row = Math.Max(0, Number("row") - 1),
                ColSpan = Math.Max(1, Number("cs")),
                RowSpan = Math.Max(1, Number("rs"))
            });
        }

        return bundle;
    }

    private static string StyleFromName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Contains("极简")) return "minimal";
        if (name.Contains("便签")) return "notes";
        return "fancy";
    }
}

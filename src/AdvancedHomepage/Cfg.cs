using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCLN.AdvancedHomepage;

internal sealed class CardPlacement
{
    public string Id { get; set; } = "";
    public int Col { get; set; }
    public int Row { get; set; }
    public int ColSpan { get; set; } = 3;
    public int RowSpan { get; set; } = 4;
}

internal sealed class CalendarOptions
{
    /// <summary>0 = 周日, 1 = 周一</summary>
    public int WeekStart { get; set; } = 1;

    /// <summary>标题格式，支持 .NET 日期格式串</summary>
    public string HeaderFormat { get; set; } = "yyyy年M月";

    /// <summary>8 档蓝色，由浅到深</summary>
    public List<string> HeatColors { get; set; } = new()
    {
        "#92cfef", "#76bff4", "#63acf9", "#5d95fc",
        "#627cfb", "#6c60f5", "#763fe9", "#7d06d6"
    };
}

internal sealed class GreetingOptions
{
    /// <summary>auto = 仅自动问候；mix = 文字 + 自动问候；text = 仅自定义文字</summary>
    public string Mode { get; set; } = "auto";

    /// <summary>问候大字号的基准像素（窗口自适应时会再乘缩放系数）</summary>
    public int FontSize { get; set; } = 22;

    /// <summary>轮换间隔（秒）</summary>
    public int Interval { get; set; } = 8;

    /// <summary>文字颜色：auto = 跟随主题；white = 白色；black = 黑色。</summary>
    public string Color { get; set; } = "auto";

    /// <summary>自定义问候语，留空则用内置标语</summary>
    public List<string> Texts { get; set; } = new();
}

/// <summary>收藏的一个网站：名称 + 地址 + 自定义图标。</summary>
internal sealed class SiteOptions
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>网址，没写协议时按 https 处理。</summary>
    public string Url { get; set; } = "";

    /// <summary>图标：本地图片路径或 http(s) 图片地址，留空用内置地球图标。</summary>
    public string Icon { get; set; } = "";
}

/// <summary>单项快捷入口：卡片上钉死的一个实例 / 存档 / 服务器 / 网站。</summary>
internal sealed class PinOptions
{
    /// <summary>卡片唯一 id，同时是布局里的键。</summary>
    public string Id { get; set; } = "";

    /// <summary>instance / world / server / site</summary>
    public string Kind { get; set; } = "instance";

    /// <summary>所属主页样式（花样 / 极简）。</summary>
    public string Style { get; set; } = "fancy";

    /// <summary>实例目录名；世界与服务器都用它定位。</summary>
    public string Instance { get; set; } = "";

    /// <summary>卡片上显示的名字。</summary>
    public string Name { get; set; } = "";

    /// <summary>存档目录名、服务器地址，或网站的网址。</summary>
    public string Target { get; set; } = "";

    /// <summary>网站卡片的自定义图标（本地路径或图片地址）。</summary>
    public string Icon { get; set; } = "";
}

/// <summary>便签主页里的一条待办。</summary>
internal sealed class TodoItem
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public long Created { get; set; }
}

/// <summary>卡片外观：背景图片 + 透明度，每张卡片独立保存。</summary>
internal sealed class CardLook
{
    /// <summary>背景图片的本地路径，留空表示用启动器原生卡片底色。</summary>
    public string Bg { get; set; } = "";

    /// <summary>背景图片的不透明度，0 ~ 1。</summary>
    public double BgOpacity { get; set; } = 1.0;

    /// <summary>整张卡片底色（含背景图）的不透明度，0 = 完全透出壁纸，1 = 启动器原生底色。</summary>
    public double Opacity { get; set; } = 1.0;
}

/// <summary>皮肤预览卡片：一张卡对应一个皮肤。</summary>
internal sealed class SkinOptions
{
    /// <summary>卡片唯一 id，同时是布局里的键。</summary>
    public string Id { get; set; } = "";

    /// <summary>所属主页样式。</summary>
    public string Style { get; set; } = "fancy";

    /// <summary>卡片标题（玩家名 / 自定义名称 / Steve）。</summary>
    public string Name { get; set; } = "";

    /// <summary>来源：player（正版玩家名）/ file（本地图片）/ builtin（Steve、Alex）。</summary>
    public string Source { get; set; } = "builtin";

    /// <summary>玩家名、本地图片路径，或 steve / alex。</summary>
    public string Value { get; set; } = "steve";

    /// <summary>
    /// 手臂模型：classic（经典 4px）/ slim（细 3px，Alex 那种）。
    /// 留空表示按来源自动判断：Steve 经典、Alex 细、正版玩家看 Mojang 返回的 model、本地图片用经典。
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>头部跟随鼠标方向转动。</summary>
    public bool HeadFollow { get; set; } = true;

    /// <summary>原地走路动画：手臂和腿前后摆动。</summary>
    public bool Walking { get; set; } = true;

    /// <summary>
    /// 弹跳动画：整个人上下弹、左右轻晃，四肢不动。
    /// 新加的卡片默认只走路，弹跳留给用户自己去开，免得两个动作叠在一起像在蹦着走。
    /// </summary>
    public bool Bouncing { get; set; } = false;

    /// <summary>拖动查看不同角度。</summary>
    public bool DragRotate { get; set; } = true;
}

/// <summary>时钟组件的显示选项。</summary>
internal sealed class ClockOptions
{
    /// <summary>marks（刻度表盘，默认）/ cookie（饼干表盘）/ digital（电子钟）</summary>
    public string Style { get; set; } = "marks";

    public bool ShowDate { get; set; } = true;

    public bool ShowSeconds { get; set; }
}

/// <summary>
/// 全局界面设置（设置窗口里的「通用 / 外观」两页）。
/// 都放在这里而不是各处零散字段，是为了设置窗口能整块读写、配置也好迁移。
/// </summary>
internal sealed class UiOptions
{
    /// <summary>配色方案：blue（默认）/ orange / green / monet（壁纸取色）。</summary>
    public string Scheme { get; set; } = "blue";

    /// <summary>莫奈取色算出来的种子色（#RRGGBB），scheme = monet 时用。</summary>
    public string MonetSeed { get; set; } = "";

    /// <summary>自定义种子色（#RRGGBB），scheme = custom 时用。</summary>
    public string CustomColor { get; set; } = "#8A5CF6";

    /// <summary>全部组件的底色不透明度倍率，1 = 每张卡自己设置的值。</summary>
    public double CardOpacity { get; set; } = 1;

    /// <summary>主页字体，空 = 跟随启动器。</summary>
    public string FontFamily { get; set; } = "";

    /// <summary>问候文字字号，16–32。</summary>
    public double FontSize { get; set; } = 22;

    /// <summary>顶部菜单是否显示「极简」。</summary>
    public bool ShowMinimal { get; set; } = true;

    /// <summary>顶部菜单是否显示「便签」。</summary>
    public bool ShowNotes { get; set; } = true;

    /// <summary>顶部是否显示编辑布局按钮。</summary>
    public bool ShowEditButton { get; set; } = true;

    /// <summary>设置窗口的左侧导航是否折叠（只留图标）。</summary>
    public bool NavCollapsed { get; set; }

    /// <summary>新手教程是否已经看过（首次安装后自动弹一次，之后只在设置里手动重开）。</summary>
    public bool TutorialSeen { get; set; }
}

/// <summary>天气组件的显示选项。</summary>
internal sealed class WeatherOptions
{
    /// <summary>md3（默认）/ immersive（铺满 + 天气底色）</summary>
    public string Style { get; set; } = "md3";

    /// <summary>城市名，走 Open-Meteo 的地理编码。</summary>
    public string City { get; set; } = "北京";

    /// <summary>显示湿度 / 风速这一行。</summary>
    public bool ShowDetails { get; set; } = true;
}

internal sealed class AppConfig
{
    /// <summary>默认开启：插件一旦在插件管理器里启用，主页即生效；禁用插件即恢复原版主页。</summary>
    public bool Enabled { get; set; } = true;

    public string Template { get; set; } = "calendar";

    /// <summary>主页样式：fancy（花样，默认）/ minimal（极简）/ notes（便签）</summary>
    public string Style { get; set; } = "fancy";

    /// <summary>便签主页上的正文。</summary>
    public string NoteText { get; set; } = "";

    /// <summary>便签主页上的待办清单。</summary>
    public List<TodoItem> Todos { get; set; } = new();

    /// <summary>按样式分别保存的布局：键为样式名，值为该样式下各组件的位置。</summary>
    public Dictionary<string, List<CardPlacement>> Layouts { get; set; } = new();

    /// <summary>已经铺过默认组件的样式。用户删掉组件后不会被自动补回来。</summary>
    public List<string> Seeded { get; set; } = new();

    /// <summary>已经做过的一次性修复标记，防止每次启动重复处理。</summary>
    public List<string> Fixes { get; set; } = new();

    /// <summary>旧版单一布局（两种样式共用同一套位置），仅用于迁移，迁移后不再写出。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<CardPlacement>? Cards { get; set; }

    public CalendarOptions Calendar { get; set; } = new();

    public GreetingOptions Greeting { get; set; } = new();

    public ClockOptions Clock { get; set; } = new();

    public WeatherOptions Weather { get; set; } = new();

    /// <summary>全局界面设置：配色、字体、主页菜单显隐。</summary>
    public UiOptions Ui { get; set; } = new();

    /// <summary>极简样式轮播的标语，可自由增删</summary>
    public List<string> Slogans { get; set; } = new();

    /// <summary>标语切换间隔（秒）</summary>
    public int SloganInterval { get; set; } = 6;

    /// <summary>yyyy-MM-dd -&gt; 启动次数</summary>
    public Dictionary<string, int> History { get; set; } = new();

    /// <summary>组件自己的选项，键形如 <c>worlds.count</c>（列表显示条数）。</summary>
    public Dictionary<string, int> WidgetOptions { get; set; } = new();

    /// <summary>用户钉在主页上的单项快捷入口。</summary>
    public List<PinOptions> Pins { get; set; } = new();

    /// <summary>收藏的网站（「多个网站」组件展示这一份列表）。</summary>
    public List<SiteOptions> Sites { get; set; } = new();

    /// <summary>皮肤预览卡片。</summary>
    public List<SkinOptions> Skins { get; set; } = new();

    /// <summary>每张卡片的背景外观，键为卡片 id。</summary>
    public Dictionary<string, CardLook> Looks { get; set; } = new();

    /// <summary>布局所在的网格列数，用于旧配置迁移。老配置没有这个字段，默认 6。</summary>
    public int GridColumns { get; set; } = 6;
}

internal static class Cfg
{
    /// <summary>主页网格的列数。</summary>
    public const int GridCols = 9;

    /// <summary>一格高 = 1.5 格宽，所以行高按「宽度 / 6」算，和 6 列时的观感一致。</summary>
    public const double RowUnitDivisor = 6.0;

    /// <summary>网格行数的下限与搜索空位时的上限（行数本身按内容自适应，这里只是搜索边界）。</summary>
    public const int MinRows = 2;
    public const int MaxSearchRows = 60;

    private static readonly object Gate = new();
    private static AppConfig _data;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static event Action Changed;

    public static AppConfig Data
    {
        get
        {
            lock (Gate)
            {
                if (_data == null) _data = LoadInternal();
                return _data;
            }
        }
    }

    public static bool Enabled
    {
        get => Data.Enabled;
        set
        {
            if (Data.Enabled == value) return;
            Data.Enabled = value;
            Save();
        }
    }

    public static CalendarOptions Calendar => Data.Calendar ??= new CalendarOptions();

    public static ClockOptions Clock => Data.Clock ??= new ClockOptions();

    public static WeatherOptions Weather => Data.Weather ??= new WeatherOptions();

    public static UiOptions Ui => Data.Ui ??= new UiOptions();

    public static GreetingOptions Greeting => Data.Greeting ??= new GreetingOptions();

    /// <summary>已钉住的单项快捷入口。</summary>
    public static List<PinOptions> Pins => Data.Pins ??= new List<PinOptions>();

    /// <summary>收藏的网站列表。</summary>
    public static List<SiteOptions> Sites => Data.Sites ??= new List<SiteOptions>();

    /// <summary>新增一个网站，返回带 id 的记录。</summary>
    public static SiteOptions AddSite(string name, string url, string icon = "")
    {
        var site = new SiteOptions
        {
            Id = "site-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = name ?? "",
            Url = url ?? "",
            Icon = icon ?? ""
        };

        Sites.Add(site);
        SaveSilently();
        return site;
    }

    /// <summary>修改网站的显示信息。</summary>
    public static void UpdateSite(string id, string name, string url, string icon)
    {
        var site = Sites.FirstOrDefault(s => s.Id == id);
        if (site == null) return;
        site.Name = name ?? "";
        site.Url = url ?? "";
        site.Icon = icon ?? "";
        SaveSilently();
    }

    public static bool RemoveSite(string id) => Sites.RemoveAll(s => s.Id == id) > 0 && SaveAndReport();

    public static SiteOptions? FindSite(string id) => Sites.FirstOrDefault(s => s.Id == id);

    // ─────────────────────────── 皮肤预览 ───────────────────────────

    public static List<SkinOptions> Skins => Data.Skins ??= new List<SkinOptions>();

    public static SkinOptions AddSkin(string style, string name, string source, string value, string model = "")
    {
        var skin = new SkinOptions
        {
            Id = "skin-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Style = NormalizeStyle(style),
            Name = string.IsNullOrWhiteSpace(name) ? "皮肤" : name,
            Source = source is "player" or "file" or "builtin" ? source : "builtin",
            Value = value ?? "steve",
            Model = model ?? ""
        };

        Skins.Add(skin);
        SaveSilently();
        return skin;
    }

    public static void UpdateSkin(string id, Action<SkinOptions> edit)
    {
        var skin = FindSkin(id);
        if (skin == null) return;
        edit(skin);
        Save();
    }

    public static bool RemoveSkin(string id) => Skins.RemoveAll(s => s.Id == id) > 0 && SaveAndReport();

    public static SkinOptions? FindSkin(string id) => Skins.FirstOrDefault(s => s.Id == id);

    // ─────────────────────────── 每卡外观 ───────────────────────────

    /// <summary>取一张卡片的背景外观；没有时返回 null（用启动器原生底色）。</summary>
    public static CardLook? Look(string cardId)
    {
        var looks = Data.Looks;
        if (looks == null || string.IsNullOrEmpty(cardId)) return null;
        return looks.TryGetValue(cardId, out var look) ? look : null;
    }

    /// <summary>写入卡片背景：<paramref name="bg"/> 为空表示恢复原生底色。</summary>
    public static void SetLook(string cardId, string bg, double opacity, double? cardOpacity = null)
    {
        if (string.IsNullOrEmpty(cardId)) return;
        var looks = Data.Looks ??= new Dictionary<string, CardLook>();

        double value = Math.Clamp(opacity, 0, 1);
        double card = Math.Clamp(cardOpacity ?? value, 0, 1);

        // 没有背景图、也没有明确指定整卡透明度时，直接回到原生卡片，不留空记录。
        // 滑块调过来的一定带 cardOpacity，不能删——否则调到 100% 会退回「默认透明」，
        // 问候组件就会突然消失。
        if (string.IsNullOrEmpty(bg) && card >= 0.999 && cardOpacity == null)
        {
            looks.Remove(cardId);
            Save();
            return;
        }

        looks[cardId] = new CardLook { Bg = bg, BgOpacity = value, Opacity = card };
        Save();
    }

    private static bool SaveAndReport()
    {
        SaveSilently();
        return true;
    }

    /// <summary>新增一个快捷入口卡片（自动分配唯一 id 并落盘）。</summary>
    public static PinOptions AddPin(string kind, string style, string instance, string name, string target, string icon = "")
    {
        var pin = new PinOptions
        {
            Id = "pin-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Kind = kind is "world" or "server" or "site" ? kind : "instance",
            Style = NormalizeStyle(style),
            Instance = instance ?? "",
            Name = name ?? "",
            Target = target ?? "",
            Icon = icon ?? ""
        };

        Pins.Add(pin);
        SaveSilently();
        return pin;
    }

    /// <summary>更新快捷入口的目标（「更换目标…」「自定义网站…」用）。</summary>
    public static void UpdatePin(string id, string instance, string name, string target, string? icon = null)
    {
        var pin = Pins.FirstOrDefault(p => p.Id == id);
        if (pin == null) return;
        pin.Instance = instance ?? "";
        pin.Name = name ?? "";
        pin.Target = target ?? "";
        if (icon != null) pin.Icon = icon;
        SaveSilently();
    }

    /// <summary>删除快捷入口。</summary>
    public static bool RemovePin(string id)
    {
        var pins = Pins;
        if (pins.RemoveAll(p => p.Id == id) == 0) return false;
        SaveSilently();
        return true;
    }

    /// <summary>某个快捷键卡片对应的配置。</summary>
    public static PinOptions? FindPin(string id) => Pins.FirstOrDefault(p => p.Id == id);

    /// <summary>问候组件的显示方式，非法值回落到 auto。</summary>
    public static string GreetingMode
    {
        get
        {
            var mode = Greeting.Mode;
            return mode is "mix" or "text" ? mode : "auto";
        }
    }

    /// <summary>问候组件要轮换的文字：自定义优先，没填就用内置标语。</summary>
    public static IReadOnlyList<string> GreetingTexts
    {
        get
        {
            var custom = Greeting.Texts;
            if (custom != null)
            {
                var cleaned = custom.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
                if (cleaned.Count > 0) return cleaned;
            }

            return DefaultSlogans;
        }
    }

    /// <summary>按一天中的时间给一句自动问候。</summary>
    public static string AutoGreeting(DateTime now)
    {
        int hour = now.Hour;
        if (hour < 5) return "夜里的世界很安静。";
        if (hour < 9) return "安静的早晨适合大工程。";
        if (hour < 12) return "上午适合把计划往前推一点。";
        if (hour < 14) return "中午了，先去补个饱食度。";
        if (hour < 18) return "下午的光线正好，出门转转吧。";
        if (hour < 22) return "夜晚适合整理箱子，也适合冒险。";
        return "再玩一会儿就睡吧。";
    }

    internal static readonly string[] DefaultSlogans =
    {
        "今天的方块，也要一块一块地堆。",
        "别急，世界生成需要一点时间。",
        "存档可以重开，日子不行。",
        "你挖的每一块矿，都算数。",
        "天黑之前，记得回家。",
        "修好镐子再下矿。",
        "慢一点也没关系，方向对就行。",
        "世界很大，先去把这片树林走完。",
        "死亡不掉落，是给自己留的温柔。",
        "每个存档，都是另一种可能的人生。",
        "今天也要好好活着（并保持饥饿值）。",
        "想做什么就去做，苦力怕不会等你。"
    };

    public static IReadOnlyList<string> Slogans
    {
        get
        {
            var list = Data.Slogans;
            if (list == null || list.Count == 0) return DefaultSlogans;
            return list.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }
    }

    public static int SloganInterval => Data.SloganInterval <= 0 ? 6 : Data.SloganInterval;

    /// <summary>是否为极简样式。</summary>
    public static bool IsMinimal => string.Equals(Data.Style, "minimal", StringComparison.OrdinalIgnoreCase);

    /// <summary>是否为便签样式。</summary>
    public static bool IsNotes => string.Equals(Data.Style, "notes", StringComparison.OrdinalIgnoreCase);

    public static void SetMinimal(bool minimal) => SetStyle(minimal ? "minimal" : "fancy");

    /// <summary>切换主页模板（fancy / minimal / notes）。</summary>
    public static void SetStyle(string style)
    {
        style = NormalizeStyle(style);
        if (string.Equals(Data.Style, style, StringComparison.Ordinal)) return;
        Data.Style = style;
        Save();
    }

    /// <summary>便签正文：改动后由调用方决定什么时候落盘（打字时按 600ms 防抖）。</summary>
    public static string NoteText
    {
        get => Data.NoteText ?? "";
        set
        {
            if (string.Equals(Data.NoteText, value, StringComparison.Ordinal)) return;
            Data.NoteText = value ?? "";
        }
    }

    public static List<TodoItem> Todos => Data.Todos ??= new List<TodoItem>();

    public static void SaveNotes() => Save();

    public static IReadOnlyList<string> HeatColors
    {
        get
        {
            var list = Calendar.HeatColors;
            if (list == null || list.Count == 0) return DefaultHeat;
            return list;
        }
    }

    private static readonly string[] DefaultHeat =
    {
        "#92cfef", "#76bff4", "#63acf9", "#5d95fc",
        "#627cfb", "#6c60f5", "#763fe9", "#7d06d6"
    };

    public static string NormalizeStyle(string? style) => (style ?? "").Trim().ToLowerInvariant() switch
    {
        "minimal" => "minimal",
        "notes" => "notes",
        _ => "fancy"
    };

    /// <summary>
    /// 实际能用的样式：设置里把「极简 / 便签」藏了以后，
    /// 这两个样式要回落到花样，否则会切到一个没有入口的主页上。
    /// </summary>
    public static string VisibleStyle(string? style)
    {
        string key = NormalizeStyle(style);
        if (key == "minimal" && !Ui.ShowMinimal) return "fancy";
        if (key == "notes" && !Ui.ShowNotes) return "fancy";
        return key;
    }

    /// <summary>当前生效的样式名。</summary>
    public static string CurrentStyle => VisibleStyle(Data.Style);

    /// <summary>
    /// 首次接触某个样式时铺上默认组件。只做一次：用户删掉的组件不会被自动补回来。
    /// </summary>
    public static void SeedLayout(string style, IEnumerable<CardPlacement> defaults)
    {
        style = NormalizeStyle(style);
        var seeded = Data.Seeded ??= new List<string>();
        if (seeded.Contains(style)) return;

        var layouts = Data.Layouts ??= new Dictionary<string, List<CardPlacement>>();
        if (!layouts.TryGetValue(style, out var list) || list == null)
            layouts[style] = list = new List<CardPlacement>();

        foreach (var def in defaults)
        {
            if (list.Any(c => c.Id == def.Id)) continue;
            list.Add(NewPlacement(def.Id, def.Col, def.Row, def.ColSpan, def.RowSpan));
        }

        foreach (var card in list) Clamp(card);
        seeded.Add(style);
        SaveSilently();      // 记住「已初始化」，否则删掉的组件下次启动会自己回来
    }

    /// <summary>该样式下是否已经有这个组件。</summary>
    public static bool HasCard(string style, string id)
    {
        style = NormalizeStyle(style);
        var layouts = Data.Layouts;
        return layouts != null && layouts.TryGetValue(style, out var list)
               && list != null && list.Any(c => c.Id == id);
    }

    /// <summary>添加组件，自动找一块不重叠的空位。</summary>
    public static bool AddCard(string style, string id, int colSpan, int rowSpan)
    {
        style = NormalizeStyle(style);
        var layouts = Data.Layouts ??= new Dictionary<string, List<CardPlacement>>();
        if (!layouts.TryGetValue(style, out var list) || list == null)
            layouts[style] = list = new List<CardPlacement>();

        if (list.Any(c => c.Id == id)) return false;

        var (col, row) = FindFreeSpot(list, colSpan, rowSpan);
        list.Add(NewPlacement(id, col, row, colSpan, rowSpan));
        SaveSilently();
        return true;
    }

    /// <summary>移除组件。</summary>
    public static bool RemoveCard(string style, string id)
    {
        style = NormalizeStyle(style);
        var layouts = Data.Layouts;
        if (layouts == null || !layouts.TryGetValue(style, out var list) || list == null) return false;
        if (list.RemoveAll(c => c.Id == id) == 0) return false;
        SaveSilently();
        return true;
    }

    /// <summary>复制一份当前布局，供「删除布局」的撤销使用。</summary>
    public static List<CardPlacement> SnapshotLayout(string style)
    {
        style = NormalizeStyle(style);
        var layouts = Data.Layouts;
        if (layouts == null || !layouts.TryGetValue(style, out var list) || list == null)
            return new List<CardPlacement>();

        return list.Select(c => new CardPlacement
        {
            Id = c.Id,
            Col = c.Col,
            Row = c.Row,
            ColSpan = c.ColSpan,
            RowSpan = c.RowSpan
        }).ToList();
    }

    /// <summary>清空某个样式的布局（组件定义本身不动，随时可以恢复）。</summary>
    public static void ClearLayout(string style)
    {
        style = NormalizeStyle(style);
        var layouts = Data.Layouts ??= new Dictionary<string, List<CardPlacement>>();
        if (!layouts.TryGetValue(style, out var list) || list == null)
            layouts[style] = list = new List<CardPlacement>();

        list.Clear();
        SaveSilently();
    }

    /// <summary>把快照写回布局（撤销「删除布局」）。</summary>
    public static void RestoreLayout(string style, IEnumerable<CardPlacement> snapshot)
    {
        style = NormalizeStyle(style);
        var layouts = Data.Layouts ??= new Dictionary<string, List<CardPlacement>>();
        if (!layouts.TryGetValue(style, out var list) || list == null)
            layouts[style] = list = new List<CardPlacement>();

        list.Clear();
        foreach (var card in snapshot)
        {
            if (card == null || string.IsNullOrEmpty(card.Id)) continue;
            list.Add(new CardPlacement
            {
                Id = card.Id,
                Col = card.Col,
                Row = card.Row,
                ColSpan = card.ColSpan,
                RowSpan = card.RowSpan
            });
        }

        foreach (var card in list) Clamp(card);
        SaveSilently();
    }

    /// <summary>
    /// 把某个样式的布局恢复成默认排列：先清空，再按默认位置放回去。
    /// 默认位置撞车（好几个组件的 DefaultCol/Row 都是 0）时用 FindFreeSpot 找空位，
    /// 这样重置出来不会出现几张卡叠在一起。其它样式不动。
    /// </summary>
    public static void ResetLayoutToDefaults(string style, IEnumerable<CardPlacement> defaults)
    {
        style = NormalizeStyle(style);
        var layouts = Data.Layouts ??= new Dictionary<string, List<CardPlacement>>();
        if (!layouts.TryGetValue(style, out var list) || list == null)
            layouts[style] = list = new List<CardPlacement>();

        list.Clear();

        foreach (var def in defaults)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) continue;
            if (list.Any(c => c.Id == def.Id)) continue;

            int colSpan = Math.Clamp(def.ColSpan, 1, GridCols);
            int rowSpan = Math.Clamp(def.RowSpan, 1, MaxSearchRows);
            int col = Math.Clamp(def.Col, 0, GridCols - colSpan);
            int row = Math.Max(0, def.Row);

            if (list.Any(c => Overlaps(c, col, row, colSpan, rowSpan)))
                (col, row) = FindFreeSpot(list, colSpan, rowSpan);

            list.Add(NewPlacement(def.Id, col, row, colSpan, rowSpan));
        }

        // 重置完把「已铺过默认」标记补上，否则下次 ApplyStyle 会再铺一遍
        var seeded = Data.Seeded ??= new List<string>();
        seeded.RemoveAll(x => string.Equals(x, style, StringComparison.OrdinalIgnoreCase));
        seeded.Add(style);

        SaveSilently();
    }

    private static bool Overlaps(CardPlacement card, int col, int row, int colSpan, int rowSpan)
    {
        return col < card.Col + card.ColSpan && card.Col < col + colSpan &&
               row < card.Row + card.RowSpan && card.Row < row + rowSpan;
    }

    /// <summary>在网格里找第一块能放下且不与现有组件重叠的位置（行数会按需向下延伸）。</summary>
    private static (int Col, int Row) FindFreeSpot(List<CardPlacement> list, int colSpan, int rowSpan)
    {
        colSpan = Math.Clamp(colSpan, 1, GridCols);
        rowSpan = Math.Clamp(rowSpan, 1, MaxSearchRows);

        for (int row = 0; row <= MaxSearchRows - rowSpan; row++)
        {
            for (int col = 0; col <= GridCols - colSpan; col++)
            {
                bool clash = false;
                foreach (var card in list)
                {
                    if (col < card.Col + card.ColSpan && card.Col < col + colSpan &&
                        row < card.Row + card.RowSpan && card.Row < row + rowSpan)
                    {
                        clash = true;
                        break;
                    }
                }

                if (!clash) return (col, row);
            }
        }

        return (0, 0);
    }

    /// <summary>取某个样式下某个组件的位置；不存在时按默认值补一条。</summary>
    public static CardPlacement GetCard(string style, string id)
    {
        style = NormalizeStyle(style);
        var layouts = Data.Layouts ??= new Dictionary<string, List<CardPlacement>>();
        if (!layouts.TryGetValue(style, out var list) || list == null)
            layouts[style] = list = new List<CardPlacement>();

        var card = list.FirstOrDefault(c => c.Id == id);
        if (card == null)
        {
            card = NewPlacement(id, 0, 0, 3, 4);
            list.Add(card);
        }

        return card;
    }

    /// <summary>写入某个样式下某个组件的位置。样式之间彼此独立。</summary>
    public static void SetCard(string style, string id, int col, int row, int colSpan, int rowSpan)
    {
        var card = GetCard(style, id);
        card.ColSpan = Math.Clamp(colSpan, 1, GridCols);
        card.RowSpan = Math.Clamp(rowSpan, 1, MaxSearchRows);
        card.Col = Math.Clamp(col, 0, GridCols - card.ColSpan);
        card.Row = Math.Max(0, row);
        Save();
    }

    private static CardPlacement NewPlacement(string id, int col, int row, int colSpan, int rowSpan)
    {
        var card = new CardPlacement { Id = id, Col = col, Row = row, ColSpan = colSpan, RowSpan = rowSpan };
        Clamp(card);
        return card;
    }

    private static void Clamp(CardPlacement card)
    {
        card.ColSpan = Math.Clamp(card.ColSpan, 1, GridCols);
        card.RowSpan = Math.Clamp(card.RowSpan, 1, MaxSearchRows);
        card.Col = Math.Clamp(card.Col, 0, GridCols - card.ColSpan);
        card.Row = Math.Max(0, card.Row);
    }

    public static string Key(DateTime date) => date.ToString("yyyy-MM-dd");

    /// <summary>读组件选项（例如列表显示条数），没有时用默认值。</summary>
    public static int GetOption(string key, int fallback)
    {
        var options = Data.WidgetOptions;
        if (options == null) return fallback;
        return options.TryGetValue(key, out var value) && value > 0 ? value : fallback;
    }

    /// <summary>写组件选项并落盘（不广播重绘，由调用方自己刷新对应组件）。</summary>
    public static void SetOption(string key, int value)
    {
        var options = Data.WidgetOptions ??= new Dictionary<string, int>();
        options[key] = value;
        SaveSilently();
    }

    public static int CountOf(DateTime date)
    {
        var history = Data.History;
        if (history == null) return 0;
        return history.TryGetValue(Key(date), out var count) ? count : 0;
    }

    public static int TodayCount => CountOf(DateTime.Today);

    public static int TotalCount
    {
        get
        {
            var history = Data.History;
            return history == null ? 0 : history.Values.Sum();
        }
    }

    public static void AddLaunch(DateTime date)
    {
        lock (Gate)
        {
            var history = Data.History ??= new Dictionary<string, int>();
            var key = Key(date);
            history.TryGetValue(key, out var count);
            history[key] = count + 1;
            SaveInternal();
        }

        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error("计数后通知 UI 失败", ex);
        }
    }

    public static void ResetHistory()
    {
        lock (Gate)
        {
            Data.History = new Dictionary<string, int>();
            SaveInternal();
        }

        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error("重置后通知 UI 失败", ex);
        }
    }

    private static AppConfig LoadInternal()
    {
        try
        {
            var path = Paths.ConfigFile;
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var cfg = JsonSerializer.Deserialize<AppConfig>(text, JsonOptions);
                    if (cfg != null)
                    {
                        cfg.Layouts ??= new Dictionary<string, List<CardPlacement>>();
                        cfg.Calendar ??= new CalendarOptions();
                        cfg.Greeting ??= new GreetingOptions();
                        cfg.Greeting.Texts ??= new List<string>();
                        cfg.History ??= new Dictionary<string, int>();
                        cfg.Slogans ??= new List<string>();
                        cfg.Seeded ??= new List<string>();
                        cfg.Fixes ??= new List<string>();
                        cfg.WidgetOptions ??= new Dictionary<string, int>();
                        cfg.Pins ??= new List<PinOptions>();
                        cfg.Pins = cfg.Pins.Where(p => p != null && !string.IsNullOrEmpty(p.Id)).ToList();
                        cfg.Sites ??= new List<SiteOptions>();
                        cfg.Sites = cfg.Sites.Where(s => s != null && !string.IsNullOrEmpty(s.Id)).ToList();

                        // 迁移后立刻按新格式写回，避免每次启动重复迁移
                        bool migrated = MigrateLegacyLayouts(cfg);
                        if (cfg.GridColumns != GridCols) migrated |= MigrateGrid(cfg);
                        migrated |= CleanSkinNames(cfg);
                        migrated |= NormalizeSkinAnim(cfg);
                        migrated |= ApplyM3Cards(cfg);
                        if (migrated) SaveConfig(cfg);
                        return cfg;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("读取配置失败，使用默认配置", ex);
        }

        return new AppConfig();
    }

    /// <summary>
    /// 2.0.0：换成 M3 Expressive 版式后，卡片是「色调容器 + 12dp 圆角」，
    /// 底色全透明就没有容器可言了。这里把之前拖到 0 的卡片恢复成 92%，
    /// 用户想再透出去，在组件设置里拖回去即可（问候卡保持透明，它是刻意无底板的）。
    /// </summary>
    private static bool ApplyM3Cards(AppConfig cfg)
    {
        const string key = "m3-cards-2.0";
        if (cfg.Fixes.Contains(key)) return false;

        cfg.Fixes.Add(key);
        if (cfg.Looks == null) return true;

        foreach (var pair in cfg.Looks)
        {
            if (string.Equals(pair.Key, "greeting", StringComparison.Ordinal)) continue;
            if (pair.Value == null || pair.Value.Opacity > 0.02) continue;

            pair.Value.Opacity = 0.92;
            if (pair.Value.BgOpacity <= 0.02) pair.Value.BgOpacity = 0.92;
        }

        return true;
    }

    /// <summary>
    /// 1.6.3：走路动画的角度一直被当成弧度写进了 AxisAngleRotation3D（实际只转了不到 1 度），
    /// 而「弹跳」当时又是默认开的，所以卡片看起来只有「整个人上下压」。
    /// 走路修好之后，把同时开着的弹跳关掉，恢复成干净的原位走路；
    /// 想要弹跳的人在组件设置里自己打开就行。
    /// </summary>
    private static bool NormalizeSkinAnim(AppConfig cfg)
    {
        const string key = "skin-anim-1.6.3";
        if (cfg.Fixes.Contains(key)) return false;

        cfg.Fixes.Add(key);

        if (cfg.Skins != null)
        {
            foreach (var skin in cfg.Skins)
            {
                if (skin.Walking && skin.Bouncing) skin.Bouncing = false;
            }
        }

        return true;
    }

    /// <summary>
    /// 老版本把本地皮肤的文件名塞进了卡片标题，这里把那种「名字就是文件名」的条目清空，
    /// 标题栏回到空白（用户自己起的名字不受影响）。
    /// </summary>
    private static bool CleanSkinNames(AppConfig cfg)
    {
        if (cfg.Skins == null) return false;

        bool changed = false;
        foreach (var skin in cfg.Skins)
        {
            if (skin == null || skin.Source != "file" || string.IsNullOrWhiteSpace(skin.Name)) continue;

            string fileName = "";
            try
            {
                fileName = System.IO.Path.GetFileNameWithoutExtension(skin.Value ?? "");
            }
            catch
            {
                // 路径异常就当没有文件名
            }

            if (fileName.Length == 0) continue;
            if (!string.Equals(skin.Name.Trim(), fileName.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

            skin.Name = "";
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// 旧版只有一套 <c>cards</c>，两种样式共用同一批位置——这正是「在花样里挪动、
    /// 极简里的卡片跟着跑」的原因。这里把它迁移到「花样」样式下，极简样式各用默认位置，
    /// 之后两边完全独立。
    /// </summary>
    private static bool MigrateLegacyLayouts(AppConfig cfg)
    {
        var legacy = cfg.Cards;
        if (legacy == null || legacy.Count == 0) return false;

        if (!cfg.Layouts.TryGetValue("fancy", out var fancy) || fancy == null || fancy.Count == 0)
        {
            cfg.Layouts["fancy"] = legacy
                .Where(c => c != null && !string.IsNullOrEmpty(c.Id))
                .Select(c => new CardPlacement
                {
                    Id = c.Id,
                    Col = c.Col,
                    Row = c.Row,
                    ColSpan = c.ColSpan,
                    RowSpan = c.RowSpan
                })
                .ToList();
        }

        cfg.Cards = null;   // 迁移完成，旧字段不再写回配置
        return true;
    }

    /// <summary>
    /// 6 列的老布局搬到 9 列：左边界和右边界<b>分别</b>按比例换算，再倒推出跨度。
    /// 这样老布局整体等比铺满新网格，既不会被压到左边留一片空白，也不会出现缝隙或重叠。
    /// </summary>
    private static bool MigrateGrid(AppConfig cfg)
    {
        int from = cfg.GridColumns <= 0 ? 6 : cfg.GridColumns;
        double ratio = (double)GridCols / from;

        foreach (var list in cfg.Layouts.Values)
        {
            if (list == null) continue;

            foreach (var card in list)
            {
                if (card == null) continue;

                int span = Math.Clamp(card.ColSpan, 1, from);
                int left = Math.Clamp(card.Col, 0, from - span);

                int l = (int)Math.Round(left * ratio, MidpointRounding.AwayFromZero);
                int r = (int)Math.Round((left + span) * ratio, MidpointRounding.AwayFromZero);

                l = Math.Clamp(l, 0, GridCols - 1);
                r = Math.Clamp(r, l + 1, GridCols);

                card.Col = l;
                card.ColSpan = r - l;
                card.Row = Math.Max(0, card.Row);
                card.RowSpan = Math.Clamp(card.RowSpan, 1, MaxSearchRows);
            }
        }

        cfg.GridColumns = GridCols;
        return true;
    }

    public static void Reload()
    {
        lock (Gate)
        {
            _data = LoadInternal();
        }

        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error("重载后通知 UI 失败", ex);
        }
    }

    /// <summary>只写盘，不触发 Changed（用于布局结构的内部维护）。</summary>
    public static void SaveSilently()
    {
        lock (Gate) SaveInternal();
    }

    public static void Save()
    {
        lock (Gate) SaveInternal();
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error("保存后通知 UI 失败", ex);
        }
    }

    private static void SaveInternal() => SaveConfig(_data);

    private static void SaveConfig(AppConfig data)
    {
        try
        {
            var path = Paths.ConfigFile;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOptions));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception ex)
        {
            Log.Error("写入配置失败", ex);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// Material 3 Expressive 设计令牌与基础组件。
///
/// 颜色：按官方 Tonal Palette 生成——源色 → OkLCh 色相/彩度 → 0~100 色阶（tone = L*），
///       再按官方 tone 表映射角色（primary 40/80、container 90/30、surface 98/6 …），
///       所以拿到的是 Material Theme Builder 那套蓝，而不是 HSL 近似出来的灰紫。
/// 形状：官方 10 档圆角，卡片 12dp、对话框 28dp、按钮全圆。
/// 字号：官方 15 档 typescale，emphasized 只改字重不改字号。
/// 动效：Expressive 方案的空间弹簧（0.38,1.21,0.22,1.00 / 350ms），用 BackEase 近似；
///       颜色与透明度走 effects（不回弹）。
/// </summary>
internal static class M3
{
    // ─────────────────────────── 形状 ───────────────────────────

    public const double CornerNone = 0;
    public const double CornerXs = 4;
    public const double CornerS = 8;
    public const double CornerM = 12;
    public const double CornerL = 16;
    public const double CornerXl = 28;
    public const double CornerXxl = 48;

    public const double CardRadius = CornerM;      // 官方：卡片 12dp
    public const double DialogRadius = CornerL;    // 弹窗 16dp：和卡片同一套观感，28 显得太圆
    public const double ButtonHeight = 40;         // 官方：按钮 40dp
    public const double IconButtonSize = 40;

    // ─────────────────────────── 颜色角色 ───────────────────────────

    public sealed class Scheme
    {
        public Brush Primary = null!, OnPrimary = null!, PrimaryContainer = null!, OnPrimaryContainer = null!;
        public Brush Secondary = null!, OnSecondary = null!, SecondaryContainer = null!, OnSecondaryContainer = null!;
        public Brush Tertiary = null!, OnTertiary = null!, TertiaryContainer = null!, OnTertiaryContainer = null!;
        public Brush Error = null!, OnError = null!, ErrorContainer = null!, OnErrorContainer = null!;
        public Brush Surface = null!, OnSurface = null!, OnSurfaceVariant = null!;
        public Brush SurfaceLowest = null!, SurfaceLow = null!, SurfaceContainer = null!, SurfaceHigh = null!, SurfaceHighest = null!;

        // 官方叫法：surface container lowest / low / (container) / high / highest
        public Brush SurfaceContainerLowest => SurfaceLowest;
        public Brush SurfaceContainerLow => SurfaceLow;
        public Brush SurfaceContainerHigh => SurfaceHigh;
        public Brush SurfaceContainerHighest => SurfaceHighest;
        public Brush Outline = null!, OutlineVariant = null!;
        public Brush InverseSurface = null!, InverseOnSurface = null!, InversePrimary = null!;
        public Brush Scrim = null!;
    }

    /// <summary>
    /// 从启动器当前主题色生成 M3 角色表。
    /// M3 的「自定义品牌色」就是这条路：一个源色 → 各色调（tone）→ 角色。
    /// 这里用 HSL 近似 tone（同一色相，靠明度分层），保证：
    ///   · 强调色永远来自用户选的龙猫蓝 / 天空蓝 / 死机蓝；
    ///   · 文字与底色始终是成对的 on 角色，对比度不会崩。
    /// </summary>
    private static Scheme Build(bool dark, Color seed, double? primaryTone = null)
    {
        var (hue, chroma) = ToOkLch(seed);

        // 彩度：M3 的 primary 调色板取源色彩度，再夹到「够蓝但不刺眼」的区间；
        // 种子偏灰（死机蓝 C=0.04）时抬到下限，不然整套色板会变成灰片。
        double primaryChroma = Math.Clamp(chroma, 0.048, 0.170);
        double secondChroma = Math.Max(0.020, primaryChroma * 0.34);   // secondary 同色相、低彩度
        double tertChroma = Math.Max(0.026, primaryChroma * 0.50);
        double neutralChroma = 0.010;                                   // 中性色只留一点点色相倾向
        double variantChroma = 0.026;

        // tertiary：官方取「色相 +60°」。蓝系种子一转就落进紫区（#6750A4 那一套的紫），
        // 所以真进紫区就翻到另一侧，得到青绿 tertiary。
        double tertHue = (hue + 60) % 360;
        if (tertHue is > 268 and < 340) tertHue = (hue - 60 + 360) % 360;

        Brush T(double h, double c, double tone) => BrushOf(Tone(h, c, tone));

        var scheme = new Scheme();

        if (!dark)
        {
            // ── 官方 light 角色 → tone 表 ──
            scheme.Primary = T(hue, primaryChroma, primaryTone ?? 40);
            scheme.OnPrimary = T(hue, 0, 100);
            scheme.PrimaryContainer = T(hue, primaryChroma, 90);
            scheme.OnPrimaryContainer = T(hue, primaryChroma, 10);

            scheme.Secondary = T(hue, secondChroma, 40);
            scheme.OnSecondary = T(hue, 0, 100);
            scheme.SecondaryContainer = T(hue, secondChroma, 90);
            scheme.OnSecondaryContainer = T(hue, secondChroma, 10);

            scheme.Tertiary = T(tertHue, tertChroma, 40);
            scheme.OnTertiary = T(tertHue, 0, 100);
            scheme.TertiaryContainer = T(tertHue, tertChroma, 90);
            scheme.OnTertiaryContainer = T(tertHue, tertChroma, 10);

            scheme.Error = Palette.Solid("#B3261E");
            scheme.OnError = Palette.Solid("#FFFFFF");
            scheme.ErrorContainer = Palette.Solid("#F9DEDC");
            scheme.OnErrorContainer = Palette.Solid("#410E0B");

            scheme.Surface = T(hue, neutralChroma, 98);
            scheme.OnSurface = T(hue, neutralChroma, 10);
            scheme.OnSurfaceVariant = T(hue, variantChroma, 30);
            scheme.SurfaceLowest = T(hue, neutralChroma, 100);
            scheme.SurfaceLow = T(hue, neutralChroma, 96);
            scheme.SurfaceContainer = T(hue, neutralChroma, 94);
            scheme.SurfaceHigh = T(hue, neutralChroma, 92);
            scheme.SurfaceHighest = T(hue, neutralChroma, 90);

            scheme.Outline = T(hue, variantChroma, 50);
            scheme.OutlineVariant = T(hue, variantChroma, 80);

            scheme.InverseSurface = T(hue, neutralChroma, 20);
            scheme.InverseOnSurface = T(hue, neutralChroma, 95);
            scheme.InversePrimary = T(hue, primaryChroma, 80);
        }
        else
        {
            // ── 官方 dark 角色 → tone 表 ──
            scheme.Primary = T(hue, primaryChroma, 80);
            scheme.OnPrimary = T(hue, primaryChroma, 20);
            scheme.PrimaryContainer = T(hue, primaryChroma, 30);
            scheme.OnPrimaryContainer = T(hue, primaryChroma, 90);

            scheme.Secondary = T(hue, secondChroma, 80);
            scheme.OnSecondary = T(hue, secondChroma, 20);
            scheme.SecondaryContainer = T(hue, secondChroma, 30);
            scheme.OnSecondaryContainer = T(hue, secondChroma, 90);

            scheme.Tertiary = T(tertHue, tertChroma, 80);
            scheme.OnTertiary = T(tertHue, tertChroma, 20);
            scheme.TertiaryContainer = T(tertHue, tertChroma, 30);
            scheme.OnTertiaryContainer = T(tertHue, tertChroma, 90);

            scheme.Error = Palette.Solid("#F2B8B5");
            scheme.OnError = Palette.Solid("#601410");
            scheme.ErrorContainer = Palette.Solid("#8C1D18");
            scheme.OnErrorContainer = Palette.Solid("#F9DEDC");

            scheme.Surface = T(hue, neutralChroma, 6);
            scheme.OnSurface = T(hue, neutralChroma, 90);
            scheme.OnSurfaceVariant = T(hue, variantChroma, 80);
            scheme.SurfaceLowest = T(hue, neutralChroma, 4);
            scheme.SurfaceLow = T(hue, neutralChroma, 10);
            scheme.SurfaceContainer = T(hue, neutralChroma, 12);
            scheme.SurfaceHigh = T(hue, neutralChroma, 17);
            scheme.SurfaceHighest = T(hue, neutralChroma, 22);

            scheme.Outline = T(hue, variantChroma, 60);
            scheme.OutlineVariant = T(hue, variantChroma, 30);

            scheme.InverseSurface = T(hue, neutralChroma, 90);
            scheme.InverseOnSurface = T(hue, neutralChroma, 20);
            scheme.InversePrimary = T(hue, primaryChroma, 40);
        }

        scheme.Scrim = Palette.Solid("#000000");
        return scheme;
    }

    private static SolidColorBrush BrushOf(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    // ── OkLab / OkLCh：M3 的 tone 就是感知亮度，用 OkLab 的 L 直接承载 tone，
    //    Hue 在这里比 HSL 稳得多（HSL 的蓝会随明度偏紫），容器色才不会一水儿薰衣草。

    /// <summary>源色 → (色相, 彩度)。色相单位是度，彩度是 OkLCh 的 C（0~0.4）。</summary>
    private static (double Hue, double Chroma) ToOkLch(Color color)
    {
        static double Linear(byte v)
        {
            double x = v / 255.0;
            return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        double r = Linear(color.R), g = Linear(color.G), b = Linear(color.B);

        double l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        double m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        double s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

        double a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        double bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

        double hue = Math.Atan2(bb, a) * 180 / Math.PI;
        if (hue < 0) hue += 360;
        return (hue, Math.Sqrt(a * a + bb * bb));
    }

    /// <summary>
    /// 从色相 + 彩度取出官方调色板里的第 N 档 tone。
    /// tone 走 L*（0=黑, 100=白），再换算成 OkLab 的 L——这样 40/80/90 才是官方那几档亮度，
    /// 而不是 HSL 里随手写的 0.36 / 0.78。彩度按 sRGB 色域二分裁剪，避免溢出成脏色。
    /// </summary>
    private static Color Tone(double hue, double chroma, double tone)
    {
        // L* → 相对亮度 Y → OkLab L（中性轴上 L = ∛Y）
        double fy = (tone + 16) / 116.0;
        double y = tone > 8 ? fy * fy * fy : tone / 903.3;
        double lightness = Math.Cbrt(y);

        double rad = hue * Math.PI / 180;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);

        double lo = 0, hi = 0.4;
        for (int i = 0; i < 22; i++)
        {
            double mid = (lo + hi) / 2;
            if (InGamut(lightness, mid * cos, mid * sin)) lo = mid;
            else hi = mid;
        }

        double c = Math.Min(chroma, lo);
        return FromOkLab(lightness, c * cos, c * sin);
    }

    private static bool InGamut(double l, double a, double b, double tolerance = 1e-4)
    {
        var (r, g, bb) = OkLabToLinear(l, a, b);
        return r >= -tolerance && r <= 1 + tolerance
            && g >= -tolerance && g <= 1 + tolerance
            && bb >= -tolerance && bb <= 1 + tolerance;
    }

    private static (double R, double G, double B) OkLabToLinear(double l, double a, double b)
    {
        double lc = l + 0.3963377774 * a + 0.2158037573 * b;
        double mc = l - 0.1055613458 * a - 0.0638541728 * b;
        double sc = l - 0.0894841775 * a - 1.2914855480 * b;

        double l3 = lc * lc * lc, m3 = mc * mc * mc, s3 = sc * sc * sc;

        return (
            4.0767416621 * l3 - 3.3077115913 * m3 + 0.2309699292 * s3,
            -1.2684380046 * l3 + 2.6097574011 * m3 - 0.3413193965 * s3,
            -0.0041960863 * l3 - 0.7034186147 * m3 + 1.7076147010 * s3);
    }

    private static Color FromOkLab(double l, double a, double b)
    {
        var (r, g, bb) = OkLabToLinear(l, a, b);

        static byte Encode(double v)
        {
            v = Math.Clamp(v, 0, 1);
            double s = v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
            return (byte)Math.Round(Math.Clamp(s, 0, 1) * 255);
        }

        return Color.FromRgb(Encode(r), Encode(g), Encode(bb));
    }

    /// <summary>
    /// 配色种子：设置窗口里选的方案（蓝 / 橙 / 绿 / 壁纸取色）。
    /// 以前是直接吃启动器的 ColorBrush3，现在由插件自己管，默认就是那套 M3 蓝。
    /// </summary>
    private static Color Seed() => AppTheme.SeedColor();

    private static Scheme? _cached;
    private static Color _cachedSeed;
    private static bool _cachedDark;
    private static double? _cachedTone;

    /// <summary>当前主题的角色表：主题色或明暗一变就重建，其余时候复用。</summary>
    public static Scheme C
    {
        get
        {
            var seed = Seed();
            bool dark = Palette.IsDark;
            double? tone = AppTheme.SeedTone;

            if (_cached != null && dark == _cachedDark && seed == _cachedSeed && tone == _cachedTone) return _cached;

            _cached = Build(dark, seed, tone);
            _cachedSeed = seed;
            _cachedDark = dark;
            _cachedTone = tone;

            Log.Info($"[M3] 主题色 #{seed.R:X2}{seed.G:X2}{seed.B:X2}（{(dark ? "深色" : "浅色")}）→ primary {(BrushOfColor(_cached.Primary))}");
            return _cached;
        }
    }

    private static string BrushOfColor(Brush brush) =>
        brush is SolidColorBrush solid ? $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}" : "-";

    private static Scheme? _lightCached;
    private static Color _lightSeed;
    private static double? _lightTone;

    /// <summary>
    /// 始终按浅色生成的同一套配色。给「底色写死是浅色」的界面用（高级主页设置窗口
    /// 的背景是 #E5E8EF、侧栏 #D7DAE1）：如果跟着启动器切深色，
    /// OnSurface 会变成浅色文字，落在浅底上直接看不见。
    /// </summary>
    public static Scheme Light
    {
        get
        {
            var seed = Seed();
            double? tone = AppTheme.SeedTone;
            if (_lightCached != null && seed == _lightSeed && tone == _lightTone) return _lightCached;

            _lightCached = Build(false, seed, tone);
            _lightSeed = seed;
            _lightTone = tone;
            return _lightCached;
        }
    }

    /// <summary>主题色变了（启动器里换颜色）时调用，下一次取角色会重新生成。</summary>
    public static void Invalidate()
    {
        _cached = null;
        _lightCached = null;
    }

    /// <summary>
    /// 热力图色阶：从 primary container 一路走到 primary（tone 92 → 40）。
    /// 以前这里是一组写死的蓝紫，换主题色时日历纹丝不动，最高档还是 M3 baseline 的紫。
    /// </summary>
    public static SolidColorBrush Ramp(int index, int count)
    {
        count = Math.Max(count, 2);
        double t = Math.Clamp(index, 0, count - 1) / (double)(count - 1);

        var (hue, chroma) = ToOkLch(Seed());
        double c = Math.Clamp(chroma, 0.048, 0.170);
        return BrushOf(Tone(hue, c, 92 - 52 * t));
    }

    /// <summary>
    /// 按任意来源色取指定 tone：Expressive 组件要 tone 78 / 90 这种不在官方角色表里的中间调
    /// （时钟的饼干盘就是「比容器色更实、比主色更浅」的一档）。
    /// </summary>
    public static SolidColorBrush Tint(Brush source, double tone)
    {
        var (hue, chroma) = ToOkLch(Of(source));
        return BrushOf(Tone(hue, chroma, tone));
    }

    // ─────────────────────────── 字号 ───────────────────────────

    public enum TypeRole
    {
        DisplayLarge, DisplayMedium, DisplaySmall,
        HeadlineLarge, HeadlineMedium, HeadlineSmall,
        TitleLarge, TitleMedium, TitleSmall,
        BodyLarge, BodyMedium, BodySmall,
        LabelLarge, LabelMedium, LabelSmall
    }

    /// <summary>官方 typescale：(字号, 行高, 字重)。emphasized 只加重字重，字号保持不变。</summary>
    public static (double Size, double Line, FontWeight Weight) Type(TypeRole role, bool emphasized = false)
    {
        var (size, line, weight) = role switch
        {
            TypeRole.DisplayLarge => (57.0, 64.0, FontWeights.Regular),
            TypeRole.DisplayMedium => (45.0, 52.0, FontWeights.Regular),
            TypeRole.DisplaySmall => (36.0, 44.0, FontWeights.Regular),
            TypeRole.HeadlineLarge => (32.0, 40.0, FontWeights.Regular),
            TypeRole.HeadlineMedium => (28.0, 36.0, FontWeights.Regular),
            TypeRole.HeadlineSmall => (24.0, 32.0, FontWeights.Regular),
            TypeRole.TitleLarge => (22.0, 28.0, FontWeights.Regular),
            TypeRole.TitleMedium => (16.0, 24.0, FontWeights.Medium),
            TypeRole.TitleSmall => (14.0, 20.0, FontWeights.Medium),
            TypeRole.BodyLarge => (16.0, 24.0, FontWeights.Regular),
            TypeRole.BodyMedium => (14.0, 20.0, FontWeights.Regular),
            TypeRole.BodySmall => (12.0, 16.0, FontWeights.Regular),
            TypeRole.LabelLarge => (14.0, 20.0, FontWeights.Medium),
            TypeRole.LabelMedium => (12.0, 16.0, FontWeights.Medium),
            _ => (11.0, 16.0, FontWeights.Medium)
        };

        if (!emphasized) return (size, line, weight);

        // display / headline / title large / body：Regular → Medium；title medium 及以下 → Bold
        var heavy = role is TypeRole.TitleMedium or TypeRole.TitleSmall
            or TypeRole.LabelLarge or TypeRole.LabelMedium or TypeRole.LabelSmall
            ? FontWeights.Bold
            : FontWeights.Medium;

        return (size, line, heavy);
    }

    /// <summary>按卡片缩放系数生成文字块，行高用官方值。</summary>
    public static TextBlock Text(string text, TypeRole role, Brush foreground, double scale = 1, bool emphasized = false)
    {
        var (size, line, weight) = Type(role, emphasized);
        return new TextBlock
        {
            Text = text,
            FontSize = size * scale,
            LineHeight = line * scale,
            FontWeight = weight,
            Foreground = foreground,
            TextWrapping = TextWrapping.NoWrap
        };
    }

    /// <summary>弹窗里的卡片：浅色下比弹窗更白、深色下更亮一档，都是「浮起来」的方向。</summary>
    public static Brush DialogCard => Palette.IsDark ? C.SurfaceHighest : C.SurfaceLowest;

    // ─────────────────────────── 动效 ───────────────────────────

    /// <summary>Expressive 空间弹簧的前半段（有回弹），用于位移 / 尺寸 / 圆角。</summary>
    public static IEasingFunction Spatial => new BackEase { Amplitude = 0.32, EasingMode = EasingMode.EaseOut };

    /// <summary>effects 弹簧不回弹，用于颜色与透明度。</summary>
    public static IEasingFunction Effects => new CubicEase { EasingMode = EasingMode.EaseOut };

    public static readonly TimeSpan FastSpatial = TimeSpan.FromMilliseconds(350);
    public static readonly TimeSpan DefaultSpatial = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan FastEffects = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan DefaultEffects = TimeSpan.FromMilliseconds(200);

    // ─────────────────────────── 阴影（官方 6 级，取 1~3） ───────────────────────────

    public static DropShadowEffect Elevation(int level)
    {
        return level switch
        {
            1 => new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.16, Color = Colors.Black, Direction = 270 },
            2 => new DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.20, Color = Colors.Black, Direction = 270 },
            3 => new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Opacity = 0.24, Color = Colors.Black, Direction = 270 },
            _ => new DropShadowEffect { BlurRadius = 2, ShadowDepth = 0, Opacity = 0.10, Color = Colors.Black }
        };
    }

    // ─────────────────────────── 状态层 ───────────────────────────

    /// <summary>把前景色以 alpha 叠到背景色上（M3 没有 elevation overlay，状态层只能这么算）。</summary>
    public static SolidColorBrush Layer(Color background, Color foreground, double alpha)
    {
        byte a = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
        double k = alpha;
        var mixed = Color.FromRgb(
            (byte)Math.Round(background.R * (1 - k) + foreground.R * k),
            (byte)Math.Round(background.G * (1 - k) + foreground.G * k),
            (byte)Math.Round(background.B * (1 - k) + foreground.B * k));
        var brush = new SolidColorBrush(mixed);
        brush.Freeze();
        return brush;
    }

    public static Color Of(Brush brush) => brush is SolidColorBrush solid ? solid.Color : Colors.Gray;

    /// <summary>同一个颜色带透明度——浮在壁纸上的容器要「看得清又透一点」，不能直接改 Opacity（会连子元素一起淡掉）。</summary>
    public static SolidColorBrush Alpha(Brush brush, double alpha)
    {
        var color = Of(brush);
        var faded = new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B));
        faded.Freeze();
        return faded;
    }

    /// <summary>
    /// 底色切换走 effects 曲线（150ms 淡入淡出）。
    /// 直接换 Brush 会「啪」地跳一下，M3 的状态层是渐变的。
    /// </summary>
    public static void AnimateFill(Border target, Brush to, TimeSpan? duration = null)
    {
        if (target == null) return;

        if (target.Background is not SolidColorBrush brush || brush.IsFrozen)
        {
            brush = new SolidColorBrush(Of(target.Background ?? Brushes.Transparent));
            target.Background = brush;
        }

        brush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(Of(to), duration ?? FastEffects) { EasingFunction = Effects });
    }

    /// <summary>悬停 8% / 按下 12%：官方的 state layer 不透明度。</summary>
    public const double HoverAlpha = 0.08;
    public const double PressAlpha = 0.12;

    // ─────────────────────────── 图标 ───────────────────────────

    /// <summary>24dp 图标（路径数据按 24×24 画），描边风格统一 1.8。</summary>
    public static Path Icon(string data, Brush stroke, double size = 24, bool filled = false)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            Stroke = stroke,
            StrokeThickness = filled ? 0 : 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = filled ? stroke : Brushes.Transparent,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        return path;
    }
}

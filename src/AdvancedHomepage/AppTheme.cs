using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 配色方案的唯一入口。
///
/// M3 的 Dynamic Color 就是「一个种子色 → 一整套角色」，
/// 所以这里只负责给 M3 一个种子：预设的蓝 / 橙 / 绿，或者从壁纸里取出来的莫奈色。
/// 换方案时把 M3 的缓存清掉，再让主页重建一次即可。
/// </summary>
internal static class AppTheme
{
    internal sealed record Preset(string Key, string Name, string Hex, string Hint);

    private static readonly Preset[] Presets =
    {
        new("blue", "蓝", "#4263DE", "默认，官方 M3 蓝"),
        new("orange", "橙", "#C2410C", "暖橙，夜间偏暗"),
        new("green", "绿", "#4C8C3F", "浅绿，清爽"),
        new("monet", "莫奈", "", "从当前壁纸取一个主色")
    };

    public static IReadOnlyList<Preset> All => Presets;

    /// <summary>自定义方案的默认种子色（还没调过时用）。</summary>
    public static Color CustomSeed => Parse(Cfg.Ui.CustomColor) ?? Color.FromRgb(0x8A, 0x5C, 0xF6);

    public static string CurrentKey
    {
        get
        {
            string key = (Cfg.Ui.Scheme ?? "blue").ToLowerInvariant();
            if (key == "custom") return "custom";
            return Presets.Any(p => p.Key == key) ? key : "blue";
        }
    }

    public static Color SeedColor()
    {
        string key = CurrentKey;
        if (key == "custom") return CustomSeed;

        if (key == "monet")
        {
            var custom = Parse(Cfg.Ui.MonetSeed);
            if (custom is Color c) return c;
            // 还没取过色：先按蓝算，取色成功后会替换
            key = "blue";
        }

        var preset = Presets.First(p => p.Key == key);
        return Parse(preset.Hex) ?? Color.FromRgb(0x42, 0x63, 0xDE);
    }

    /// <summary>
    /// 莫奈方案专用：把壁纸本身的明暗也当成「调性」带进配色。
    ///
    /// 只取色相的话，深海军蓝的壁纸和 #4263DE 算出来是同一套蓝——用户点了莫奈会觉得「没反应」。
    /// 这里按种子色的感知亮度给一个主色色调（tone 28~48），深壁纸出深一点的强调色。
    /// 其他方案固定 tone 40，保持官方观感。
    /// </summary>
    public static double? SeedTone
    {
        get
        {
            if (CurrentKey != "monet") return null;
            if (Parse(Cfg.Ui.MonetSeed) is not Color color) return null;

            double luma = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
            return Math.Clamp(luma / 255.0 * 100.0 + 8, 28, 48);
        }
    }

    /// <summary>用取色器选出来的颜色当种子。</summary>
    public static void ApplyCustom(Color color)
    {
        Cfg.Ui.CustomColor = Hex(color);
        Cfg.Ui.Scheme = "custom";
        Cfg.Save();
        M3.Invalidate();
        HomeInjector.RequestHomeRefresh();
        Log.Info($"[配色] 自定义 {Cfg.Ui.CustomColor}");
    }

    /// <summary>切方案：写配置 + 清主题缓存 + 重建主页。莫奈取不到色时返回 false，调用方负责提示。</summary>
    public static bool Apply(string key)
    {
        key = (key ?? "blue").ToLowerInvariant();
        if (key == "monet")
        {
            var picked = Monet.Pick();
            if (picked is not Color color)
            {
                Log.Warn("[配色] 莫奈取色失败：没找到壁纸，保持当前方案");
                return false;
            }

            Cfg.Ui.MonetSeed = Hex(color);
        }

        Cfg.Ui.Scheme = key;
        Cfg.Save();
        M3.Invalidate();
        HomeInjector.RequestHomeRefresh();
        Log.Info($"[配色] 切换为 {key}，种子 {(Cfg.Ui.Scheme == "monet" ? Cfg.Ui.MonetSeed : SeedColor().ToString())}");
        return true;
    }

    /// <summary>重新从壁纸取一次色（已经在莫奈方案下时用）。</summary>
    public static bool RepickMonet()
    {
        var picked = Monet.Pick();
        if (picked is not Color color) return false;

        Cfg.Ui.MonetSeed = Hex(color);
        Cfg.Ui.Scheme = "monet";
        Cfg.Save();
        M3.Invalidate();
        HomeInjector.RequestHomeRefresh();
        Log.Info($"[配色] 莫奈取色 → {Cfg.Ui.MonetSeed}");
        return true;
    }

    public static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static Color? Parse(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        string text = hex.Trim().TrimStart('#');
        if (text.Length != 6) return null;

        try
        {
            return Color.FromRgb(
                Convert.ToByte(text.Substring(0, 2), 16),
                Convert.ToByte(text.Substring(2, 2), 16),
                Convert.ToByte(text.Substring(4, 2), 16));
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 壁纸取色（Monet 的简化实现）：
///
/// 1. 找壁纸：遍历启动器主窗体，**按控件实际显示面积**挑最大的那张来自文件的图。
///    壁纸铺满整窗，卡片背景图只有一张卡那么大，所以「谁大谁是壁纸」比「谁浅谁先找到」稳得多。
/// 2. 取色：把图缩到 96px，按色相分 24 个桶累计「彩度 × 亮度」权重，再把相邻桶并成色簇。
/// 3. 挑色簇：默认取权重最大的那个；但如果它和 M3 默认蓝的色相几乎一样（用户会觉得「点了没反应」），
///    就改取另外一个有一定面积、色相明显不同的色簇——壁纸里的极光、晚霞、花丛往往就在这一类里。
/// </summary>
internal static class Monet
{
    /// <summary>M3 默认蓝 #4263DE 的色相：取出来的主色和它太像时换一个色簇。</summary>
    private const double BaselineHue = 227;

    private sealed class Cluster
    {
        public double Weight;
        public Color Color;
    }

    public static Color? Pick()
    {
        try
        {
            var (image, name) = FindWallpaper();
            if (image == null)
            {
                Log.Warn("莫奈取色：没找到壁纸");
                return null;
            }

            var result = Analyze(Downscale(image));
            Log.Info($"莫奈取色：{name} → {(result is Color c ? AppTheme.Hex(c) : "无")}");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error("莫奈取色失败", ex);
            return null;
        }
    }

    // ─────────────────────────── 找壁纸 ───────────────────────────

    /// <summary>
    /// 找壁纸：按「显示面积」挑主窗体里最大的那张位图，直接拿它的像素来算。
    ///
    /// 以前是找「图片文件的路径」，可启动器的壁纸常常是从流里读进来再铺到 Border 上的，
    /// UriSource 为空 → 认不出来 → 一路退到 Pictures 里最新的一张；
    /// 换壁纸没反应、永远拿旧图算色，就是这么来的。现在不认路径，只认像素。
    /// 真找不到（比如壁纸是视频）再退到 Pictures。
    /// </summary>
    private static (BitmapSource? Image, string Name) FindWallpaper()
    {
        try
        {
            var window = PCL.ModMain.frmMain;
            if (window != null)
            {
                var best = Largest(window);
                if (best.Image != null)
                {
                    string name = string.IsNullOrEmpty(best.Path)
                        ? $"界面壁纸（{best.Area:0} 像素）"
                        : System.IO.Path.GetFileName(best.Path);

                    Log.Info($"莫奈取色：命中 {name}，显示面积 {best.Area:0}");
                    return (best.Image, name);
                }
            }
            else
            {
                Log.Warn("莫奈取色：主窗体还没建好，退回 Pictures");
            }
        }
        catch (Exception ex)
        {
            Log.Warn("查找壁纸控件失败：" + ex.Message);
        }

        try
        {
            string pictures = System.IO.Path.Combine(Paths.Root, "Pictures");
            if (!Directory.Exists(pictures)) return (null, "");

            string? fallback = Directory.EnumerateFiles(pictures)
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (fallback == null) return (null, "");

            Log.Info($"莫奈取色：退回 Pictures，用 {System.IO.Path.GetFileName(fallback)}");
            return (LoadSmall(fallback), System.IO.Path.GetFileName(fallback));
        }
        catch
        {
            return (null, "");
        }
    }

    /// <summary>遍历可视树，返回显示面积最大的那张位图。</summary>
    private static (BitmapSource? Image, string? Path, double Area) Largest(DependencyObject root)
    {
        BitmapSource? bestImage = null;
        string? bestPath = null;
        double bestArea = 0;

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        int guard = 0;
        while (queue.Count > 0 && guard++ < 60000)
        {
            var node = queue.Dequeue();

            var (source, path) = PictureOf(node);
            if (source != null && node is FrameworkElement element)
            {
                // 只算真正参与布局的那部分；被隐藏的、还没量出尺寸的跳过
                double area = element.ActualWidth * element.ActualHeight;
                if (area <= 0) area = element.Width * element.Height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestImage = source;
                    bestPath = path;
                }
            }

            int count;
            try
            {
                count = VisualTreeHelper.GetChildrenCount(node);
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                try
                {
                    queue.Enqueue(VisualTreeHelper.GetChild(node, i));
                }
                catch
                {
                    // 个别子节点还没进可视树时取不到，跳过
                }
            }
        }

        return (bestImage, bestPath, bestArea);
    }

    /// <summary>这个元素上如果画着一张位图，就返回位图（有文件路径就一并带上）。</summary>
    private static (BitmapSource? Source, string? Path) PictureOf(DependencyObject node)
    {
        if (node is Image image && image.Source is BitmapSource fromImage)
        {
            return (fromImage, Local(fromImage));
        }

        Brush? brush = node switch
        {
            Panel panel => panel.Background,
            Border border => border.Background,
            Control control => control.Background,     // Window 也算 Control
            System.Windows.Shapes.Shape shape => shape.Fill,
            _ => null
        };

        if (brush is ImageBrush { ImageSource: BitmapSource fromBrush })
        {
            return (fromBrush, Local(fromBrush));
        }

        return (null, null);
    }

    /// <summary>缩到 96px 长边再统计，几千万像素的壁纸也不会卡一下。</summary>
    private static BitmapSource Downscale(BitmapSource source)
    {
        try
        {
            int width = source.PixelWidth;
            if (width <= 128) return source;

            double scale = 96.0 / width;
            var small = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            small.Freeze();
            return small;
        }
        catch
        {
            // 有些源不支持变换（例如已经冻结的索引色位图），原样返回
            return source;
        }
    }

    private static string? Local(BitmapSource source)
    {
        try
        {
            if (source is BitmapImage image)
            {
                string? uri = image.UriSource?.LocalPath;
                if (!string.IsNullOrEmpty(uri) && File.Exists(uri)) return uri;
            }

            // 解码器直接给的帧（BitmapFrame）拿不到 UriSource，用 ToString 里带的路径兜一下
            if (source is BitmapFrame frame)
            {
                string text = frame.ToString() ?? "";
                int cut = text.IndexOf("://", StringComparison.Ordinal);
                if (cut < 0) return null;

                string uri = text;
                if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
                {
                    string local = parsed.LocalPath;
                    if (File.Exists(local)) return local;
                }
            }
        }
        catch
        {
            // 略
        }

        return null;
    }

    private static BitmapSource? LoadSmall(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.DecodePixelWidth = 96;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    // ─────────────────────────── 取色 ───────────────────────────

    private static Color? Analyze(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        if (width <= 0 || height <= 0) return null;

        int stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        const int bins = 24;
        var weight = new double[bins];
        var sumR = new double[bins];
        var sumG = new double[bins];
        var sumB = new double[bins];
        var maxChroma = new double[bins];

        for (int i = 0; i < pixels.Length; i += 4)
        {
            double b = pixels[i] / 255.0;
            double g = pixels[i + 1] / 255.0;
            double r = pixels[i + 2] / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double value = max;
            double delta = max - min;
            double saturation = max <= 0.001 ? 0 : delta / max;

            // 太黑 / 太白 / 太灰的像素不参与，否则取出来永远是背景色
            if (value < 0.16 || value > 0.95 || saturation < 0.14) continue;
            if (delta <= 0.0001) continue;

            double hue;
            if (Math.Abs(max - r) < 0.0001) hue = 60 * (((g - b) / delta) % 6);
            else if (Math.Abs(max - g) < 0.0001) hue = 60 * ((b - r) / delta + 2);
            else hue = 60 * ((r - g) / delta + 4);
            if (hue < 0) hue += 360;

            int bin = Math.Clamp((int)(hue / (360.0 / bins)), 0, bins - 1);
            double w = saturation * (1 - Math.Abs(value - 0.55));
            if (w <= 0) continue;

            weight[bin] += w;
            sumR[bin] += r * w;
            sumG[bin] += g * w;
            sumB[bin] += b * w;
            maxChroma[bin] = Math.Max(maxChroma[bin], saturation);
        }

        double total = weight.Sum();
        if (total <= 0) return null;

        // 相邻的桶并成色簇（色相 0 和 360 首尾相接）
        var clusters = new List<Cluster>();
        var used = new List<int>();

        // 从第一个「有料且前一个为空」的桶开始，保证首尾相邻的情况只算一个簇
        int start = -1;
        for (int i = 0; i < bins; i++)
        {
            if (weight[i] <= 0) continue;
            if (weight[(i - 1 + bins) % bins] <= 0)
            {
                start = i;
                break;
            }
        }

        if (start < 0) return null;

        for (int step = 0; step < bins; step++)
        {
            int bin = (start + step) % bins;
            if (weight[bin] <= 0) continue;

            double w = 0, r = 0, g = 0, b = 0;
            int index = bin;
            while (weight[index] > 0 && !used.Contains(index))
            {
                used.Add(index);
                w += weight[index];
                r += sumR[index];
                g += sumG[index];
                b += sumB[index];
                index = (index + 1) % bins;
                if (index == bin) break;
            }

            if (w <= 0) continue;

            clusters.Add(new Cluster
            {
                Weight = w,
                Color = Color.FromRgb(
                    (byte)Math.Round(Math.Clamp(r / w, 0, 1) * 255),
                    (byte)Math.Round(Math.Clamp(g / w, 0, 1) * 255),
                    (byte)Math.Round(Math.Clamp(b / w, 0, 1) * 255))
            });
        }

        if (clusters.Count == 0) return null;

        var ordered = clusters.OrderByDescending(c => c.Weight).ToList();
        Log.Info("莫奈取色：色簇 " + string.Join(" | ", ordered.Take(4).Select(c =>
        {
            RgbToHsv(c.Color, out double h, out _, out _);
            return $"{h:0}° {c.Weight / total * 100:0.0}% #{c.Color.R:X2}{c.Color.G:X2}{c.Color.B:X2}";
        })));
        var primary = ordered[0];
        var chosen = primary;

        RgbToHsv(primary.Color, out double primaryHue, out _, out _);
        if (HueDistance(primaryHue, BaselineHue) < 20)
        {
            // 主色和默认蓝几乎同色 → 找个够面积、色相差得开的色簇，避免「点了没反应」
            var vivid = ordered.Skip(1)
                .Where(c => c.Weight >= total * 0.015)
                .Select(c =>
                {
                    RgbToHsv(c.Color, out double h, out _, out _);
                    return (Cluster: c, Hue: h, Distance: HueDistance(h, primaryHue));
                })
                .Where(c => c.Distance >= 18)
                .OrderByDescending(c => c.Cluster.Weight)
                .FirstOrDefault();

            if (vivid.Cluster != null)
            {
                chosen = vivid.Cluster;
                Log.Info($"莫奈取色：主色相 {primaryHue:0}° 与默认蓝太近，" +
                         $"改用色相 {vivid.Hue:0}° 的色簇（占 {vivid.Cluster.Weight / total * 100:0.0}%）");
            }
        }

        return Normalize(chosen.Color);
    }

    private static double HueDistance(double a, double b)
    {
        double diff = Math.Abs(a - b) % 360;
        return diff > 180 ? 360 - diff : diff;
    }

    /// <summary>把取到的颜色拉回「能当种子」的区间：彩度别太低，亮度别太极端。</summary>
    private static Color Normalize(Color color)
    {
        RgbToHsv(color, out double h, out double s, out double v);
        s = Math.Clamp(Math.Max(s, 0.62), 0, 0.92);
        v = Math.Clamp(v, 0.52, 0.78);
        return HsvToRgb(h, s, v);
    }

    private static void RgbToHsv(Color color, out double h, out double s, out double v)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        v = max;
        s = max <= 0.001 ? 0 : delta / max;

        if (delta <= 0.0001) h = 0;
        else if (Math.Abs(max - r) < 0.0001) h = 60 * (((g - b) / delta) % 6);
        else if (Math.Abs(max - g) < 0.0001) h = 60 * ((b - r) / delta + 2);
        else h = 60 * ((r - g) / delta + 4);

        if (h < 0) h += 360;
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;

        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Color.FromRgb(
            (byte)Math.Round(Math.Clamp(r + m, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(g + m, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(b + m, 0, 1) * 255));
    }
}

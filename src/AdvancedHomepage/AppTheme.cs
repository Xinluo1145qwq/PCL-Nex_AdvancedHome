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
/// 把壁纸缩到 64px 长边，只统计「不太黑、不太白、有彩度」的像素，
/// 按色相分 24 个桶累计「彩度 × 亮度」权重，取最重的桶平均出主色，
/// 最后把彩度/亮度拉回可用区间，避免种子里出来一片灰。
/// </summary>
internal static class Monet
{
    public static Color? Pick()
    {
        try
        {
            string path = FindWallpaper();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Log.Warn("莫奈取色：没找到壁纸文件");
                return null;
            }

            var image = LoadSmall(path);
            if (image == null) return null;

            var result = Analyze(image);
            Log.Info($"莫奈取色：{Path.GetFileName(path)} → {(result is Color c ? AppTheme.Hex(c) : "无")}");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error("莫奈取色失败", ex);
            return null;
        }
    }

    /// <summary>
    /// 先找启动器当前真正显示的那张壁纸，找不到再退到 Pictures 里最新的一张。
    ///
    /// 用广度优先：壁纸一般挂得离窗口很近，而卡片背景图埋在很深的地方，
    /// 深度优先很容易先撞上某张卡片的背景图，取出来的「主色」就成了那张图的颜色。
    /// </summary>
    private static string? FindWallpaper()
    {
        try
        {
            var window = PCL.ModMain.frmMain;
            if (window != null)
            {
                string? found = Walk(window);
                if (!string.IsNullOrEmpty(found)) return found;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("查找壁纸控件失败：" + ex.Message);
        }

        try
        {
            string pictures = Path.Combine(Paths.Root, "Pictures");
            if (!Directory.Exists(pictures)) return null;

            return Directory.EnumerateFiles(pictures)
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? Walk(DependencyObject root)
    {
        var queue = new Queue<(DependencyObject Node, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (node, depth) = queue.Dequeue();
            if (depth > 24) continue;

            string? path = WallpaperOf(node);
            if (!string.IsNullOrEmpty(path)) return path;

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
                    queue.Enqueue((VisualTreeHelper.GetChild(node, i), depth + 1));
                }
                catch
                {
                    // 个别子节点还没进可视树时取不到，跳过
                }
            }
        }

        return null;
    }

    /// <summary>这个元素上如果有「来自文件的图片」，就返回它的路径。</summary>
    private static string? WallpaperOf(DependencyObject node)
    {
        if (node is Image image && image.Source is BitmapSource source)
        {
            string? fromImage = Local(source);
            if (fromImage != null) return fromImage;
        }

        Brush? brush = node switch
        {
            Panel panel => panel.Background,
            Border border => border.Background,
            Control control => control.Background,     // Window 也算 Control
            System.Windows.Shapes.Shape shape => shape.Fill,
            _ => null
        };

        if (brush is ImageBrush { ImageSource: BitmapSource brushSource })
        {
            string? fromBrush = Local(brushSource);
            if (fromBrush != null) return fromBrush;
        }

        return null;
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
        bitmap.DecodePixelWidth = 64;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

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

            double hue;
            if (delta <= 0.0001) continue;
            else if (Math.Abs(max - r) < 0.0001) hue = 60 * (((g - b) / delta) % 6);
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
        }

        int best = -1;
        for (int i = 0; i < bins; i++)
        {
            if (weight[i] <= 0) continue;
            if (best < 0 || weight[i] > weight[best]) best = i;
        }

        if (best < 0) return null;

        double total = weight[best];
        var color = Color.FromRgb(
            (byte)Math.Round(Math.Clamp(sumR[best] / total, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(sumG[best] / total, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(sumB[best] / total, 0, 1) * 255));

        return Normalize(color);
    }

    /// <summary>把取到的颜色拉回「能当种子」的区间：彩度别太低，亮度别太极端。</summary>
    private static Color Normalize(Color color)
    {
        RgbToHsv(color, out double h, out double s, out double v);
        s = Math.Clamp(Math.Max(s, 0.55), 0, 0.9);
        v = Math.Clamp(v, 0.55, 0.82);
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

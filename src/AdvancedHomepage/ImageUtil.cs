using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>存档缩略图 / 实例图标的加载与占位绘制。</summary>
internal static class ImageUtil
{
    /// <summary>在后台线程解码 PNG 并冻结，UI 线程只做赋值。</summary>
    public static BitmapSource? Load(string path, int decodeWidth = 112)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = decodeWidth;
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

    /// <summary>
    /// 读随 DLL 一起打包的图片（关于页的作者头像 / 背景 / 平台图标）。
    /// 资源名形如 <c>PCLN.AdvancedHomepage.Assets.Author.avatar.jpg</c>。
    /// </summary>
    public static BitmapSource? Embedded(string name, int decodeWidth = 0)
    {
        try
        {
            var assembly = typeof(ImageUtil).Assembly;
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodeWidth > 0) bitmap.DecodePixelWidth = decodeWidth;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Warn($"读取内置图片失败（{name}）：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 没有缩略图时用的占位图：一块像素风草方块（天空渐变 + 草皮 + 带颗粒的泥土）。
    /// 只有两三个纯色块会显得很假，所以按 8×8 像素格子画，颜色层次多一点。
    /// </summary>
    public static FrameworkElement CreateGrassIcon(double size)
    {
        // 8×8 像素图案：S 天空、g 草皮、G 草皮阴影、d 泥土、D 泥土深色颗粒
        string[] pattern =
        {
            "SSSSSSSS",
            "SSSSSSSS",
            "SSsSSsSS",
            "gggggggg",
            "GGGGGGGG",
            "dDddddDd",
            "dddddDdd",
            "dDddddDd"
        };

        string[] colors = { "#A9D6F3", "#8FC5EC", "#7CB342", "#5E8C34", "#8D6E4A", "#6E5539" };
        var map = new System.Collections.Generic.Dictionary<char, Brush>
        {
            ['S'] = Palette.Solid(colors[0]),
            ['s'] = Palette.Solid(colors[1]),
            ['g'] = Palette.Solid(colors[2]),
            ['G'] = Palette.Solid(colors[3]),
            ['d'] = Palette.Solid(colors[4]),
            ['D'] = Palette.Solid(colors[5])
        };

        double cell = size / 8.0;
        var canvas = new Canvas { Width = size, Height = size };

        for (int row = 0; row < pattern.Length; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (!map.TryGetValue(pattern[row][col], out var brush)) continue;
                var pixel = new Rectangle
                {
                    Width = cell + 0.6,       // 多出 0.6px，避免格子之间露白线
                    Height = cell + 0.6,
                    Fill = brush
                };
                Canvas.SetLeft(pixel, col * cell);
                Canvas.SetTop(pixel, row * cell);
                canvas.Children.Add(pixel);
            }
        }

        double radius = size * 0.16;
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(radius),
            Clip = new RectangleGeometry(new Rect(0, 0, size, size), radius, radius),
            Child = canvas
        };
    }

    /// <summary>
    /// 启动器内置的方块图标（Fabric 是布卷、NeoForge 是铁砧、原版是草方块…）。
    /// 这几个图片以 WPF 资源形式打包在启动器程序集里，直接按 pack URI 取。
    /// </summary>
    public static BitmapSource? LoadPclBlock(string loader, int decodeWidth = 112)
    {
        var name = loader switch
        {
            "Fabric" => "Fabric",
            "Quilt" => "Quilt",
            "NeoForge" => "NeoForge",
            "Forge" => "Anvil",
            "OptiFine" => "RedstoneBlock",
            _ => "Grass"
        };

        var uris = new[]
        {
            $"pack://application:,,,/images/Blocks/{name}.png",
            $"pack://application:,,,/Plain Craft Launcher 2;component/images/Blocks/{name}.png",
            $"pack://application:,,,/Blocks/{name}.png"
        };

        foreach (var text in uris)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.DecodePixelWidth = decodeWidth;
                bitmap.UriSource = new Uri(text, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                // 换下一个候选路径
            }
        }

        return null;
    }

    /// <summary>服务器没有图标时用的占位：深蓝方块 + 信号柱。</summary>
    public static FrameworkElement CreateServerIcon(double size)
    {
        var glyph = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M6,20 L6,15 M10,20 L10,11 M14,20 L14,7 M18,20 L18,3"),
            Stroke = Palette.Solid("#8FB7E8"),
            StrokeThickness = 1.9,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform,
            Width = size * 0.52,
            Height = size * 0.44,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.16),
            Background = Palette.Solid("#39506B"),
            Child = glyph
        };
    }

    /// <summary>网站没有自定义图标时用的占位：深色方块 + 地球线框。</summary>
    public static FrameworkElement CreateGlobeIcon(double size)
    {
        var glyph = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M12,3 A9,9 0 1,1 11.99,3 Z M3,12 H21 M12,3 C15.4,6.4 15.4,17.6 12,21 C8.6,17.6 8.6,6.4 12,3"),
            Stroke = Palette.Solid("#8FB7E8"),
            StrokeThickness = 1.7,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform,
            Width = size * 0.58,
            Height = size * 0.58,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.16),
            Background = Palette.Solid("#2F3F57"),
            Child = glyph
        };
    }

    /// <summary>圆形播放按钮里的三角形。</summary>
    public static FrameworkElement CreatePlayIcon(double size, Brush brush)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 0,0 L 10,6 L 0,12 Z"),
            Fill = brush,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            // 三角形视觉重心偏左，往右挪一点点看起来才居中
            Margin = new Thickness(size * 0.18, 0, 0, 0)
        };
        return path;
    }

    /// <summary>三个点的「更多」图标。</summary>
    public static FrameworkElement CreateMoreIcon(double size, Brush brush)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        double dot = Math.Max(1.6, size * 0.2);
        for (int i = 0; i < 3; i++)
        {
            panel.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = dot,
                Height = dot,
                Fill = brush,
                Margin = new Thickness(0, i == 0 ? 0 : dot * 0.8, 0, 0)
            });
        }

        return panel;
    }
}

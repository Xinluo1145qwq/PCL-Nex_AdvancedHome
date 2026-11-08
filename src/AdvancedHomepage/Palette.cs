using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PCLN.AdvancedHomepage;

/// <summary>主题色适配：优先复用启动器资源键，取不到时按明暗模式回退。</summary>
internal static class Palette
{
    private static bool _dumped;

    public static bool IsDark
    {
        get
        {
            try
            {
                return PCL.ThemeManager.IsDarkMode;
            }
            catch
            {
                return true;
            }
        }
    }

    private static ResourceDictionary Resources
    {
        get
        {
            try
            {
                return PCL.ThemeManager.AppResources ?? Application.Current?.Resources;
            }
            catch
            {
                return Application.Current?.Resources;
            }
        }
    }

    public static Brush Find(params string[] keys)
    {
        var res = Resources;
        if (res == null || keys == null) return null;
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key)) continue;
            if (TryGet(res, key, out var value) && value is Brush brush) return brush;
        }

        return null;
    }

    private static bool TryGet(ResourceDictionary dict, string key, out object value, int depth = 0)
    {
        value = null;
        if (dict == null || depth > 6) return false;
        if (dict.Contains(key))
        {
            value = dict[key];
            return true;
        }

        foreach (var merged in dict.MergedDictionaries)
        {
            if (TryGet(merged, key, out value, depth + 1)) return true;
        }

        return false;
    }

    /// <summary>把元素属性绑定到启动器主题资源；资源不存在时使用回退画刷。</summary>
    public static void Bind(DependencyObject element, DependencyProperty property, Brush fallback, params string[] keys)
    {
        if (element == null || property == null) return;
        var res = Resources;
        if (res != null)
        {
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                if (!TryGet(res, key, out _)) continue;
                if (element is FrameworkElement fe)
                {
                    fe.SetResourceReference(property, key);
                    return;
                }

                if (element is FrameworkContentElement fce)
                {
                    fce.SetResourceReference(property, key);
                    return;
                }
            }
        }

        if (fallback != null && element is DependencyObject dep) dep.SetValue(property, fallback);
    }

    // ─────────────────────────── M3 角色映射 ───────────────────────────
    // 老视图里到处在用这些名字，这里统一接到 M3 色彩角色上：
    // 文字只用 on surface / on surface variant，强调色只用 primary / tertiary，
    // 这样所有界面都落在同一套角色里，不会出现「蓝色标题配紫色按钮」。
    public static Brush TextPrimary => M3.C.OnSurface;
    public static Brush TextSecondary => M3.C.OnSurfaceVariant;
    public static Brush TextTertiary => M3.Layer(Of(M3.C.OnSurfaceVariant), Of(M3.C.Surface), 0.18);
    public static Brush TextSub => M3.C.OnSurfaceVariant;

    /// <summary>卡片里的小块填充：surface container 叠 4% on surface。</summary>
    public static Brush CellFill => M3.Layer(Of(M3.C.SurfaceContainer), Of(M3.C.OnSurface), 0.04);

    public static Brush AccentBlue => M3.C.Primary;

    /// <summary>强调色（原启动器 ColorBrush3）：M3 里对应 primary。</summary>
    public static Brush AccentBrush => M3.C.Primary;

    /// <summary>卡片底色（原启动器 ColorBrushBg1）：M3 里对应 surface container low。</summary>
    public static Brush CardBackground => M3.C.SurfaceLow;

    public static Brush CardBorder => M3.C.OutlineVariant;

    /// <summary>装饰性强调：M3 的 tertiary，给需要一点特别注意力的小元素。</summary>
    public static Brush Accent => M3.C.Tertiary;

    public static Color Of(Brush brush) => brush is SolidColorBrush solid ? solid.Color : Colors.Gray;

    public static SolidColorBrush Solid(string hex)
    {
        var color = Parse(hex, Colors.Gray);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static Color Parse(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var text = hex.Trim();
        if (!text.StartsWith("#")) text = "#" + text;
        try
        {
            return (Color)ColorConverter.ConvertFromString(text);
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>把颜色逐档合并到基准色上（用于卡片半透明底色）。</summary>
    public static SolidColorBrush WithOpacity(Color color, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>首次运行把可用主题色键写入日志，便于后续微调。</summary>
    public static void DumpKeysOnce()
    {
        if (_dumped) return;
        _dumped = true;
        try
        {
            var res = Resources;
            if (res == null)
            {
                Log.Info("主题资源不可用（Application.Resources 为空）");
                return;
            }

            var keys = new List<string>();
            CollectKeys(res, keys, 0);
            var picked = keys.Where(k => k.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0
                                         || k.IndexOf("Theme", StringComparison.OrdinalIgnoreCase) >= 0)
                             .Distinct()
                             .OrderBy(k => k)
                             .Take(80)
                             .ToList();
            Log.Info("主题键（前 80）：" + string.Join(", ", picked));
        }
        catch (Exception ex)
        {
            Log.Error("导出主题键失败", ex);
        }
    }

    private static void CollectKeys(ResourceDictionary dict, List<string> keys, int depth)
    {
        if (dict == null || depth > 4) return;
        foreach (var key in dict.Keys)
        {
            if (key is string s) keys.Add(s);
        }

        foreach (var merged in dict.MergedDictionaries) CollectKeys(merged, keys, depth + 1);
    }
}

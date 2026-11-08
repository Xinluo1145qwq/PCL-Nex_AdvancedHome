using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace PCLN.AdvancedHomepage;

/// <summary>零散的小样式工具：目前只有圆角悬停提示。</summary>
internal static class UiStyles
{
    /// <summary>
    /// 卡片标题字号。日历卡是按 315 宽设计稿排的（标题 18 号），
    /// 其它组件的标题一律跟它对齐，不再各用一套基准。
    /// </summary>
    public static double CardTitleSize(double cardWidth)
    {
        if (double.IsNaN(cardWidth) || double.IsInfinity(cardWidth) || cardWidth <= 1) cardWidth = 315;

        // M3 typescale 的 title large（22sp），按卡片宽度等比缩放
        double scale = Math.Clamp(cardWidth / 315.0, 0.66, 1.15);
        return Math.Round(M3.Type(M3.TypeRole.TitleLarge).Size * scale, 1);
    }

    /// <summary>M3 提示气泡：inverse surface 底 + 8dp 圆角，贴在指针旁边。</summary>
    public static ToolTip Tooltip(string text, double scale = 1)
    {
        return RoundedToolTip(text, M3.CornerS);
    }

    /// <summary>
    /// 圆角悬停提示。启动器默认的气泡是直角方块，放在卡片上很突兀，
    /// 这里给一个和卡片同圆角的深色气泡。
    /// </summary>
    public static ToolTip RoundedToolTip(object content, double radius = 8)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.BackgroundProperty, M3.C.InverseSurface);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        border.SetValue(Border.PaddingProperty, new Thickness(9, 5, 9, 5));
        border.SetValue(TextElement.ForegroundProperty, M3.C.InverseOnSurface);
        border.SetValue(TextElement.FontSizeProperty, 12.0);
        border.SetValue(TextElement.FontWeightProperty, FontWeights.Medium);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        border.AppendChild(presenter);

        return new ToolTip
        {
            Content = content,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HasDropShadow = false,
            Template = new ControlTemplate(typeof(ToolTip)) { VisualTree = border }
        };
    }
}

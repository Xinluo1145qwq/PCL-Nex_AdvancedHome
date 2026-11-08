using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>在「设置 - 启动器选项」页插入「高级主页」配置卡片。</summary>
internal static class SettingsPanel
{
    private const string TagId = "PCLN.AdvancedHomepage.SettingsCard";
    private static bool _statsHooked;

    public static void EnsureInjected(object page)
    {
        var pan = Reflect.Field(page, "PanMain") as Panel;
        if (pan == null)
        {
            Log.Warn("设置页结构变化：未找到 PanMain 字段");
            return;
        }

        if (pan.Children.OfType<FrameworkElement>().Any(c => Equals(c.Tag, TagId))) return;

        var host = page as UIElement;

        FrameworkElement card = null;
        try
        {
            card = BuildNativeCard(host);
        }
        catch (Exception ex)
        {
            Log.Error("构建原生设置卡片失败，改用回退样式", ex);
        }

        card ??= BuildFallbackCard(host);
        card.Tag = TagId;
        pan.Children.Add(card);
        Log.Info("设置页已插入「高级主页」选项");
    }

    private static FrameworkElement BuildNativeCard(UIElement host)
    {
        var card = new PCL.MyCard
        {
            Title = "高级主页",
            Margin = new Thickness(0, 0, 0, 15)
        };

        var body = new StackPanel { Margin = new Thickness(25, 38, 23, 15) };

        body.Children.Add(Text("用 9 列网格卡片替代原版主页内容；字体、顶部菜单和配色都在设置窗口里调。",
            12.5, 0.68, new Thickness(0, 0, 0, 4)));

        var settingsButton = CreateEntryButton("高级主页设置…", UiSettingsDialog.Show, host);
        settingsButton.Margin = new Thickness(0, 16, 0, 0);
        body.Children.Add(settingsButton);

        var stats = Text($"共记录 {Cfg.TotalCount} 次启动 · 今天 {Cfg.TodayCount} 次",
            11.5, 0.55, new Thickness(0, 14, 0, 2));
        body.Children.Add(stats);
        body.Children.Add(Text($"数据文件：{Paths.ConfigFile}", 11, 0.42, new Thickness(0, 2, 0, 0)));

        if (!_statsHooked)
        {
            _statsHooked = true;
            Cfg.Changed += () =>
            {
                try
                {
                    if (!stats.Dispatcher.CheckAccess())
                    {
                        stats.Dispatcher.BeginInvoke(new Action(() =>
                            stats.Text = $"共记录 {Cfg.TotalCount} 次启动 · 今天 {Cfg.TodayCount} 次"));
                        return;
                    }

                    stats.Text = $"共记录 {Cfg.TotalCount} 次启动 · 今天 {Cfg.TodayCount} 次";
                }
                catch
                {
                    // 卡片可能已卸载
                }
            };
        }

        card.BorderChild = body;
        return card;
    }


    /// <summary>
    /// 设置卡里的入口按钮。这里不用 PCL.MyButton：它的 Click 在动态插入的卡片里不可靠
    /// （启动器的滚动容器会在 MouseUp 之前抓走鼠标），所以改成在 PreviewMouseDown 阶段就响应；
    /// 再往页面根部挂一层「按坐标命中」的兜底，按钮自己的命中测试被吞掉也能打开。
    /// </summary>
    private static Border CreateEntryButton(string text, Action onClick, UIElement host)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 12.5,
            Foreground = Palette.AccentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var button = new Border
        {
            Height = 36,
            Padding = new Thickness(20, 0, 20, 0),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            BorderBrush = Palette.CardBorder,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            Child = label,
            ToolTip = UiStyles.Tooltip("字体、顶部菜单与配色")
        };

        var hover = M3.Layer(Palette.Of(Palette.CardBackground), Palette.Of(Palette.AccentBrush), 0.08);
        button.MouseEnter += (_, _) => button.Background = hover;
        button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;

        bool busy = false;
        void Fire()
        {
            if (busy) return;
            busy = true;

            Log.Info("[设置卡] 打开高级主页设置");

            // 等这次鼠标事件彻底走完再开模态，否则 MouseUp 会落在新窗口的导航项上
            // （之前就是因此一打开设置就去开 GitHub）。
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    onClick();
                }
                catch (Exception ex)
                {
                    Log.Error("打开高级主页设置失败", ex);
                }
                finally
                {
                    busy = false;
                }
            }), DispatcherPriority.Background);
        }

        // 页面级兜底：启动器设置页的滚动容器会抢鼠标，按钮收不到 MouseUp。
        if (host != null)
        {
            host.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (!button.IsVisible || button.ActualWidth <= 0) return;

                Point point;
                try
                {
                    point = e.GetPosition(button);
                }
                catch
                {
                    return;
                }

                if (point.X < 0 || point.Y < 0 || point.X > button.ActualWidth || point.Y > button.ActualHeight) return;

                e.Handled = true;
                Log.Info("[设置卡] 命中入口，打开设置窗口");
                Fire();
            };
        }

        // 按钮自己能收到事件时（正常情况）也直接响应，不等 MouseUp。
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Fire();
        };
        button.MouseLeftButtonUp += (_, e) => e.Handled = true;

        return button;
    }

    private static FrameworkElement BuildFallbackCard(UIElement host)
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(Text("高级主页", 14, 0.95, new Thickness(0, 0, 0, 8)));
        panel.Children.Add(Text("用 9 列网格卡片替代原版主页内容。", 12.5, 0.7, new Thickness(0, 0, 0, 4)));

        var fallbackButton = CreateEntryButton("高级主页设置…", UiSettingsDialog.Show, host);
        fallbackButton.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(fallbackButton);

        panel.Children.Add(Text($"数据文件：{Paths.ConfigFile}", 11, 0.5, new Thickness(0, 8, 0, 0)));

        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 15),
            Child = panel
        };
        Palette.Bind(border, Border.BackgroundProperty, Palette.CardBackground, "ColorBrushBg1", "ColorBrushBg0");
        Palette.Bind(border, Border.BorderBrushProperty, Palette.CardBorder, "ColorBrushGray2", "ColorBrushGray3");
        return border;
    }

    private static void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + Paths.DataDir + "\"",
                UseShellExecute = true
            });
            Log.Info("已打开数据目录：" + Paths.DataDir);
        }
        catch (Exception ex)
        {
            Log.Error("打开数据目录失败", ex);
        }
    }

    private static TextBlock Text(string value, double size, double opacity, Thickness margin)
    {
        var text = new TextBlock
        {
            Text = value,
            FontSize = size,
            Opacity = opacity,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin,
            VerticalAlignment = VerticalAlignment.Center
        };
        Palette.Bind(text, TextBlock.ForegroundProperty, Palette.TextSecondary, "ColorBrush3");
        return text;
    }
}

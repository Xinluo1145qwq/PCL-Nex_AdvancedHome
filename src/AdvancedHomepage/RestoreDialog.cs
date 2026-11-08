using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「从备份恢复」的选择窗口：列出 AdvancedHomepage\backups 里的每一份备份，
/// 选一份就把三套主页的组件位置还原回去。默认选中最新的一份。
///
/// 只动布局，不动配色 / 字体这些外观设置——用户点「恢复」时想找回的通常是摆位。
/// </summary>
internal sealed class RestoreDialog : Window
{
    private const double CardWidth = 720;

    private readonly List<RowItem> _rows = new();
    private string? _selected;
    private string? _result;

    private sealed class RowItem
    {
        public string Path = "";
        public Border Host = null!;
        public TextBlock Title = null!;
        public TextBlock Detail = null!;
        public Border Dot = null!;
    }

    private RestoreDialog(List<LayoutTools.BackupEntry> entries)
    {
        Dlg.Setup(this, "从备份恢复", CardWidth, scrim: true);
        SizeToContent = SizeToContent.Manual;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Width = CardWidth - 56 };

        // ── 标题 ──
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titles = new StackPanel();
        titles.Children.Add(new TextBlock
        {
            Text = "从备份恢复",
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink
        });
        titles.Children.Add(new TextBlock
        {
            Text = "选一份备份，把三套主页的组件位置还原回去。配色、字体这些外观设置不会变。",
            FontSize = 12.5,
            LineHeight = 18,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim
        });
        head.Children.Add(titles);

        var close = M3IconButton.Create("M6,6 L18,18 M18,6 L6,18", () => DialogResult = false, "关闭",
            scheme: UiSkin.L);
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(8, -6, -8, 0);
        Grid.SetColumn(close, 1);
        head.Children.Add(close);

        panel.Children.Add(head);

        // ── 列表 ──
        var list = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        foreach (var entry in entries)
            list.Children.Add(BuildRow(entry));

        var scroll = new ScrollViewer
        {
            Content = list,
            MaxHeight = 356,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 6, 0)
        };

        try
        {
            var bar = UiSkin.ThinScrollBar();
            if (bar != null) scroll.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = bar;
        }
        catch (Exception ex)
        {
            Log.Warn("恢复窗口滚动条样式失败：" + ex.Message);
        }

        panel.Children.Add(scroll);

        // 默认选中最新的一份，用户直接点「恢复」就是刚才那次备份
        if (_rows.Count > 0) Select(_rows[0]);

        // ── 底部动作 ──
        var footer = new Grid { Margin = new Thickness(0, 22, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var openDir = M3Button.Create("打开备份文件夹", OpenBackupFolder, M3ButtonKind.Text, scheme: UiSkin.L);
        openDir.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(openDir);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var cancel = M3Button.Create("取消", () => DialogResult = false, M3ButtonKind.Text, scheme: UiSkin.L);
        var restore = M3Button.Create("恢复", Confirm, M3ButtonKind.Filled, scheme: UiSkin.L);
        restore.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(cancel);
        actions.Children.Add(restore);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);

        panel.Children.Add(footer);

        var surface = new Border
        {
            CornerRadius = new CornerRadius(M3.DialogRadius),
            Background = UiSkin.Card,
            Padding = new Thickness(28, 24, 28, 22),
            Child = panel,
            Effect = M3.Elevation(3)
        };

        surface.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.95, 0.95);
        surface.RenderTransform = scale;
        surface.Opacity = 0;

        surface.Loaded += (_, _) =>
        {
            surface.BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, M3.FastEffects) { EasingFunction = M3.Effects });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
        };

        Dlg.Mount(this, surface);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
            else if (e.Key == Key.Enter) Confirm();
        };
    }

    private Border BuildRow(LayoutTools.BackupEntry entry)
    {
        var title = new TextBlock
        {
            Text = entry.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink
        };

        var detail = new TextBlock
        {
            Text = entry.Detail,
            FontSize = 11.5,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = UiSkin.Dim
        };

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title);
        text.Children.Add(detail);

        var size = new TextBlock
        {
            Text = SizeText(entry.Size),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 14, 0),
            Foreground = UiSkin.Dim
        };

        var dot = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1.6),
            BorderBrush = UiSkin.FieldLine,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(text);
        Grid.SetColumn(size, 1);
        grid.Children.Add(size);
        Grid.SetColumn(dot, 2);
        grid.Children.Add(dot);

        var host = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(14, 11, 14, 11),
            Margin = new Thickness(0, 0, 0, 6),
            Cursor = Cursors.Hand,
            Child = grid
        };

        var item = new RowItem
        {
            Path = entry.Path,
            Host = host,
            Title = title,
            Detail = detail,
            Dot = dot
        };

        host.MouseEnter += (_, _) =>
        {
            if (_selected == entry.Path) return;
            host.Background = UiSkin.Hover(UiSkin.Card);
            host.BorderBrush = UiSkin.Line;
        };

        host.MouseLeave += (_, _) =>
        {
            if (_selected == entry.Path) return;
            host.Background = Brushes.Transparent;
            host.BorderBrush = Brushes.Transparent;
        };

        host.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Select(item);
        };

        _rows.Add(item);
        return host;
    }

    private void Select(RowItem item)
    {
        _selected = item.Path;

        foreach (var row in _rows)
        {
            bool on = ReferenceEquals(row, item);

            row.Host.Background = on ? UiSkin.L.SecondaryContainer : Brushes.Transparent;
            row.Host.BorderBrush = on ? UiSkin.L.Primary : Brushes.Transparent;
            row.Title.Foreground = on ? UiSkin.L.OnSecondaryContainer : UiSkin.Ink;
            row.Detail.Foreground = on ? UiSkin.L.OnSecondaryContainer : UiSkin.Dim;

            row.Dot.BorderBrush = on ? UiSkin.L.Primary : UiSkin.FieldLine;
            row.Dot.Background = on ? UiSkin.L.Primary : Brushes.Transparent;
            row.Dot.Child = on
                ? new Border
                {
                    Width = 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(3.5),
                    Background = UiSkin.L.OnPrimary,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
                : null;
        }
    }

    private void Confirm()
    {
        if (_selected == null) return;
        _result = _selected;
        DialogResult = true;
    }

    private static void OpenBackupFolder()
    {
        try
        {
            var dir = Path.Combine(Paths.DataDir, "backups");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn("打开备份文件夹失败：" + ex.Message);
        }
    }

    private static string SizeText(long bytes) =>
        bytes >= 1024 ? $"{bytes / 1024.0:0.#} KB" : bytes + " B";

    /// <summary>列出备份让用户挑，返回选中的备份路径；取消返回 null。</summary>
    public static string? Ask(Window? owner, List<LayoutTools.BackupEntry> entries)
    {
        try
        {
            var dialog = new RestoreDialog(entries);
            if (owner != null) dialog.Owner = owner;

            return dialog.ShowDialog() == true ? dialog._result : null;
        }
        catch (Exception ex)
        {
            Log.Error("打开恢复备份窗口失败", ex);
            return null;
        }
    }
}

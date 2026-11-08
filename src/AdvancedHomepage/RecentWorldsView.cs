using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「最近的项目」卡片：列出最近游玩的存档，每行是缩略图 + 名字 + 上次游玩时间 + 版本。
/// 数据来自文件系统，扫描放在后台线程，先出空壳再填充，不阻塞主页。
/// </summary>
internal sealed class RecentWorldsView : Grid
{
    private const double RefWidth = 270;
    private const double HeightRatio = 4.0 / 3.0;   // 3 列宽 ≈ 4 行高
    private const string CountKey = "worlds.count";
    private const int DefaultCount = 4;

    private readonly StackPanel _root = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _title = new();
    private readonly StackPanel _rows = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _footer = new();

    private readonly List<WorldEntry> _entries = new();
    private readonly Dictionary<string, BitmapSource> _icons = new(StringComparer.OrdinalIgnoreCase);

    private double _width = RefWidth;
    private double _height = RefWidth * HeightRatio;
    private double _scale = 1;
    private double _rowHeight = 40;
    private int _rowsFit = 4;
    private bool _loading;

    public RecentWorldsView()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;

        _title.Text = "从最近的项目开始";
        _title.FontWeight = FontWeights.SemiBold;
        _title.Foreground = Palette.TextPrimary;
        _footer.Opacity = 0.55;
        _footer.Margin = new Thickness(0, 6, 0, 0);
        _footer.Foreground = Palette.TextTertiary;

        _root.Children.Add(_title);
        _root.Children.Add(_rows);
        _root.Children.Add(_footer);
        Children.Add(_root);

        Build(RefWidth, RefWidth * HeightRatio);
        Loaded += (_, _) => Reload();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = _width;

        // 卡片被拉高时跟着变高，能放下的条数也就更多
        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * HeightRatio;
        h = Math.Max(h, w * HeightRatio * 0.5);

        if (Math.Abs(w - _width) > 0.4 || Math.Abs(h - _height) > 0.4) Build(w, h);

        var size = new Size(w, double.IsInfinity(availableSize.Height) ? h : Math.Min(h, availableSize.Height));
        base.MeasureOverride(size);
        return size;
    }

    /// <summary>按卡片尺寸等比换算所有尺寸，窗口缩放时版式比例不变。</summary>
    private void Build(double width, double height)
    {
        _width = width;
        _height = height;
        _scale = width / RefWidth;
        double s = _scale;

        _root.Margin = new Thickness(16 * s, 16 * s, 16 * s, 16 * s);
        _title.FontSize = UiStyles.CardTitleSize(width);
        _footer.FontSize = 11 * s;
        _rowHeight = 61 * s;

        // 标题与页脚之外的空间能放几行就算几行，不滚动、不溢出
        double usable = height - 20 * s - 14 * s - 26 * s - 24 * s;
        int fit = (int)Math.Floor(usable / _rowHeight);
        _rowsFit = Math.Clamp(fit, 1, 12);

        RenderRows();
    }

    /// <summary>重新扫描并填充（30 秒内复用缓存）。</summary>
    public void Reload()
    {
        if (_loading) return;
        _loading = true;

        Task.Run(() =>
        {
            int count = Cfg.GetOption(CountKey, DefaultCount);
            var entries = new List<WorldEntry>(WorldStore.Recent(count, TimeSpan.FromDays(3650)));
            var icons = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var bitmap = ImageUtil.Load(entry.IconPath);
                if (bitmap != null) icons[entry.IconPath] = bitmap;
            }

            return (Entries: entries, Icons: icons);
        }).ContinueWith(task =>
        {
            _loading = false;
            if (task.IsFaulted)
            {
                Log.Error("加载最近存档失败", task.Exception?.GetBaseException());
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _entries.Clear();
                _entries.AddRange(task.Result.Entries);
                _icons.Clear();
                foreach (var pair in task.Result.Icons) _icons[pair.Key] = pair.Value;
                RenderRows();
            }));
        }, TaskScheduler.Default);
    }

    private void RenderRows()
    {
        _rows.Children.Clear();
        double s = _scale;

        if (_entries.Count == 0)
        {
            _footer.Text = _loading ? "正在读取存档…" : "还没有存档记录。";
            return;
        }

        // 只画放得下的条数，多出来的用页脚提示，避免溢出卡片
        int shown = Math.Min(_entries.Count, _rowsFit);
        _footer.Text = _entries.Count > shown ? $"还有 {_entries.Count - shown} 个存档，把卡片拉高可显示更多" : "";

        for (int i = 0; i < shown; i++)
        {
            _rows.Children.Add(BuildRow(_entries[i], s, i));
        }
    }

    private FrameworkElement BuildRow(WorldEntry entry, double s, int index)
    {
        var grid = new Grid { Margin = new Thickness(0, index == 0 ? 8 * s : 0, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 缩略图
        double thumb = 42 * s;
        var frame = new Border
        {
            Width = thumb,
            Height = thumb,
            CornerRadius = new CornerRadius(7 * s),
            Background = Palette.Solid(Palette.IsDark ? "#1BFFFFFF" : "#10000000"),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true
        };

        if (_icons.TryGetValue(entry.IconPath, out var icon))
        {
            frame.Background = new ImageBrush(icon)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
        }
        else
        {
            frame.Child = ImageUtil.CreateGrassIcon(thumb);
        }

        Grid.SetColumn(frame, 0);
        grid.Children.Add(frame);

        // 文字
        var texts = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(11 * s, 0, 8 * s, 0)
        };

        var name = new TextBlock
        {
            Text = entry.Name,
            FontSize = 14 * s,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Palette.TextPrimary
        };

        var meta = new TextBlock
        {
            FontSize = 11 * s,
            Margin = new Thickness(0, 2.5 * s, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Palette.TextTertiary
        };

        var parts = new List<string> { "上次游玩于" + WorldStore.Humanize(entry.LastPlayed) };
        if (!string.IsNullOrWhiteSpace(entry.Version)) parts.Add(entry.Version);
        meta.Text = string.Join(" · ", parts);

        texts.Children.Add(name);
        texts.Children.Add(meta);
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        // 播放：直接进入这个存档
        var play = BuildPlayButton(entry, s);
        Grid.SetColumn(play, 2);
        grid.Children.Add(play);

        var row = new Border
        {
            CornerRadius = new CornerRadius(8 * s),
            Padding = new Thickness(8 * s, 6.5 * s, 6 * s, 6.5 * s),
            Margin = new Thickness(-8 * s, 3 * s, -6 * s, 3 * s),
            Background = Brushes.Transparent,
            Child = grid
        };

        ToolTipService.SetToolTip(row, UiStyles.RoundedToolTip(entry.Folder));
        ToolTipService.SetInitialShowDelay(row, 400);

        var hover = Palette.Solid(Palette.IsDark ? "#16FFFFFF" : "#0C000000");
        row.MouseEnter += (_, _) => row.Background = hover;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

        return row;
    }

    private FrameworkElement BuildPlayButton(WorldEntry entry, double s)
    {
        double size = 28 * s;
        var icon = ImageUtil.CreatePlayIcon(11 * s, Palette.TextSecondary);

        var button = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = Palette.Solid(Palette.IsDark ? "#14FFFFFF" : "#0B000000"),
            BorderThickness = new Thickness(1),
            BorderBrush = Palette.Solid(Palette.IsDark ? "#1FFFFFFF" : "#12000000"),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = icon
        };

        ToolTipService.SetToolTip(button, UiStyles.RoundedToolTip("启动并进入这个存档"));
        ToolTipService.SetInitialShowDelay(button, 350);

        button.MouseEnter += (_, _) =>
        {
            button.Background = Palette.AccentBrush;
            button.BorderBrush = Palette.AccentBrush;
            if (icon is System.Windows.Shapes.Shape shape) shape.Fill = Palette.Solid("#FFFFFF");
        };
        button.MouseLeave += (_, _) =>
        {
            button.Background = Palette.Solid(Palette.IsDark ? "#14FFFFFF" : "#0B000000");
            button.BorderBrush = Palette.Solid(Palette.IsDark ? "#1FFFFFFF" : "#12000000");
            if (icon is System.Windows.Shapes.Shape shape) shape.Fill = Palette.TextSecondary;
        };

        button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            // 「全局」表示存档放在 .minecraft/saves 下，跟着当前选中的实例启动
            var instance = entry.Instance == "全局" ? "" : entry.Instance;
            LaunchBridge.Launch(instance, System.IO.Path.GetFileName(entry.Folder));
        };

        return button;
    }
}

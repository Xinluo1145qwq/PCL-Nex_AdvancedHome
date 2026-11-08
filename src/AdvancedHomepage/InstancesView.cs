using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「最近游玩的实例」卡片：图标 + 名称 + 上次游玩时间 + 版本标签 + 播放按钮，
/// 播放按钮直接走启动器的启动入口，效果和「开始游戏」一致。
/// </summary>
internal sealed class InstancesView : Grid
{
    private const double RefWidth = 270;
    private const double HeightRatio = 4.0 / 3.0;
    private const string CountKey = "instances.count";
    private const int DefaultCount = 4;

    private readonly StackPanel _root = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _title = new();
    private readonly StackPanel _rows = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _footer = new();

    private readonly List<InstanceEntry> _entries = new();
    private readonly Dictionary<string, BitmapSource> _icons = new(StringComparer.OrdinalIgnoreCase);

    private double _width = RefWidth;
    private double _height = RefWidth * HeightRatio;
    private double _scale = 1;
    private double _rowHeight = 42;
    private int _rowsFit = 4;
    private bool _loading;

    public InstancesView()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;

        _title.Text = "最近游玩的实例";
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

        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * HeightRatio;
        h = Math.Max(h, w * HeightRatio * 0.5);

        if (Math.Abs(w - _width) > 0.4 || Math.Abs(h - _height) > 0.4) Build(w, h);

        var size = new Size(w, double.IsInfinity(availableSize.Height) ? h : Math.Min(h, availableSize.Height));
        base.MeasureOverride(size);
        return size;
    }

    private void Build(double width, double height)
    {
        _width = width;
        _height = height;
        _scale = width / RefWidth;
        double s = _scale;

        _root.Margin = new Thickness(16 * s, 16 * s, 16 * s, 16 * s);
        _title.FontSize = UiStyles.CardTitleSize(width);
        _footer.FontSize = 11 * s;
        _rowHeight = 59 * s;

        double usable = height - 20 * s - 14 * s - 26 * s - 24 * s;
        int fit = (int)Math.Floor(usable / _rowHeight);
        _rowsFit = Math.Clamp(fit, 1, 12);

        RenderRows();
    }

    public void Reload()
    {
        if (_loading) return;
        _loading = true;

        int count = Cfg.GetOption(CountKey, DefaultCount);

        System.Threading.Tasks.Task.Run(() =>
        {
            var entries = new List<InstanceEntry>(InstanceStore.Recent(count));
            var icons = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var bitmap = ImageUtil.Load(entry.IconPath);
                if (bitmap != null)
                {
                    icons[entry.IconPath] = bitmap;
                    continue;
                }

                // 没有自定义 Logo 的实例，用启动器内置的方块图标
                var block = ImageUtil.LoadPclBlock(entry.Loader);
                if (block != null) icons[BlockKey(entry)] = block;
            }

            return (Entries: entries, Icons: icons);
        }).ContinueWith(task =>
        {
            _loading = false;
            if (task.IsFaulted)
            {
                Log.Error("加载实例列表失败", task.Exception?.GetBaseException());
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
        }, System.Threading.Tasks.TaskScheduler.Default);
    }

    private void RenderRows()
    {
        _rows.Children.Clear();

        if (_entries.Count == 0)
        {
            _footer.Text = _loading ? "正在读取实例…" : "还没有可用的实例。";
            return;
        }

        _footer.Text = "";
        int shown = Math.Min(_entries.Count, _rowsFit);
        if (_entries.Count > shown)
            _footer.Text = $"还有 {_entries.Count - shown} 个实例，把卡片拉高可显示更多";

        for (int i = 0; i < shown; i++) _rows.Children.Add(BuildRow(_entries[i], i));
    }

    private FrameworkElement BuildRow(InstanceEntry entry, int index)
    {
        double s = _scale;

        var grid = new Grid { Margin = new Thickness(0, index == 0 ? 8 * s : 0, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // ── 图标 ──
        double thumb = 40 * s;
        var frame = new Border
        {
            Width = thumb,
            Height = thumb,
            CornerRadius = new CornerRadius(7 * s),
            Background = Palette.Solid(Palette.IsDark ? "#1BFFFFFF" : "#10000000"),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true
        };

        if (!string.IsNullOrEmpty(entry.IconPath) && _icons.TryGetValue(entry.IconPath, out var icon))
        {
            frame.Background = new ImageBrush(icon) { Stretch = Stretch.UniformToFill };
        }
        else if (_icons.TryGetValue(BlockKey(entry), out var block))
        {
            frame.Background = new ImageBrush(block) { Stretch = Stretch.UniformToFill };
        }
        else
        {
            frame.Child = ImageUtil.CreateGrassIcon(thumb);
        }

        Grid.SetColumn(frame, 0);
        grid.Children.Add(frame);

        // ── 文字 ──
        var texts = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(11 * s, 0, 8 * s, 0)
        };

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(new TextBlock
        {
            Text = entry.Name,
            FontSize = 14 * s,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Palette.TextPrimary
        });

        if (entry.Starred)
        {
            nameRow.Children.Add(BuildStar(10 * s));
        }

        var meta = new TextBlock
        {
            FontSize = 11 * s,
            Margin = new Thickness(0, 2 * s, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Palette.TextTertiary,
            Text = (entry.LastPlayed > DateTime.MinValue
                       ? "游玩于" + WorldStore.Humanize(entry.LastPlayed)
                       : "还没启动过")
                   + " · " + entry.VersionText
        };

        texts.Children.Add(nameRow);
        texts.Children.Add(meta);
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        // ── 播放 ──
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        right.Children.Add(BuildPlayButton(entry.Name, s));

        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        var row = new Border
        {
            CornerRadius = new CornerRadius(8 * s),
            Padding = new Thickness(8 * s, 6.5 * s, 6 * s, 6.5 * s),
            Margin = new Thickness(-8 * s, 3 * s, -6 * s, 3 * s),
            Background = Brushes.Transparent,
            Child = grid
        };

        ToolTipService.SetToolTip(row, UiStyles.RoundedToolTip($"{entry.Name}\n{entry.VersionText}" +
                                                             (entry.LaunchCount > 0 ? $"\n已启动 {entry.LaunchCount} 次" : "")));
        ToolTipService.SetInitialShowDelay(row, 400);

        var hover = Palette.Solid(Palette.IsDark ? "#16FFFFFF" : "#0C000000");
        row.MouseEnter += (_, _) => row.Background = hover;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

        return row;
    }

    private static string BlockKey(InstanceEntry entry) => "block:" + entry.Loader;

    private FrameworkElement BuildPlayButton(string instance, double s)
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
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = icon
        };

        ToolTipService.SetToolTip(button, UiStyles.RoundedToolTip("启动这个实例"));
        ToolTipService.SetInitialShowDelay(button, 350);

        button.MouseEnter += (_, _) =>
        {
            button.Background = Palette.AccentBrush;
            button.BorderBrush = Palette.AccentBrush;
            if (icon is Shape shape) shape.Fill = Palette.Solid("#FFFFFF");
        };
        button.MouseLeave += (_, _) =>
        {
            button.Background = Palette.Solid(Palette.IsDark ? "#14FFFFFF" : "#0B000000");
            button.BorderBrush = Palette.Solid(Palette.IsDark ? "#1FFFFFFF" : "#12000000");
            if (icon is Shape shape) shape.Fill = Palette.TextSecondary;
        };

        button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            LaunchBridge.Launch(instance);
        };

        return button;
    }

    private FrameworkElement BuildStar(double size)
    {
        var path = new Path
        {
            Data = Geometry.Parse("M 6,0 L 7.6,4 L 12,4.3 L 8.6,7 L 9.7,11.3 L 6,8.9 L 2.3,11.3 L 3.4,7 L 0,4.3 L 4.4,4 Z"),
            Fill = Palette.AccentBlue,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        return path;
    }
}

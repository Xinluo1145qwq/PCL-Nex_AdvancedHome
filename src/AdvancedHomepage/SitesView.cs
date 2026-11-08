using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「多个网站」卡片：列出收藏的网址，点一行（或右边的圆钮）用默认浏览器打开。
/// 列表在「⋯ → 管理网站…」里维护。
/// </summary>
internal sealed class SitesView : Grid
{
    private const double RefWidth = 350;
    private const double HeightRatio = 0.72;
    private const string CountKey = "sites.count";

    private readonly StackPanel _root = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _title = new();
    private readonly StackPanel _rows = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _footer = new();

    private readonly List<SiteOptions> _entries = new();
    private readonly Dictionary<string, BitmapSource> _icons = new(StringComparer.OrdinalIgnoreCase);

    private double _width = RefWidth;
    private double _height = RefWidth * HeightRatio;
    private double _scale = 1;
    private int _rowsFit = 3;

    public SitesView()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;

        _title.Text = "网站";
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

        double rowHeight = 58 * s;
        double usable = height - 16 * s - 12 * s - 29 * s - 22 * s;
        _rowsFit = Math.Clamp((int)Math.Floor(usable / rowHeight), 1, 12);

        RenderRows();
    }

    public void Reload()
    {
        _entries.Clear();
        _entries.AddRange(Cfg.Sites);

        _icons.Clear();
        foreach (var site in _entries)
        {
            var bitmap = Sites.LoadIcon(site.Icon);
            if (bitmap != null) _icons[IconKey(site)] = bitmap;
        }

        RenderRows();
    }

    private void RenderRows()
    {
        _rows.Children.Clear();

        if (_entries.Count == 0)
        {
            _footer.Text = "还没有网站，选中卡片后点 ⋯ → 管理网站…添加。";
            return;
        }

        int count = Math.Max(1, Cfg.GetOption(CountKey, 4));
        int shown = Math.Min(Math.Min(_entries.Count, count), _rowsFit);
        _footer.Text = _entries.Count > shown
            ? $"还有 {_entries.Count - shown} 个，把卡片拉高可显示更多"
            : "";

        for (int i = 0; i < shown; i++) _rows.Children.Add(BuildRow(_entries[i], i));
    }

    private FrameworkElement BuildRow(SiteOptions site, int index)
    {
        double s = _scale;

        var grid = new Grid { Margin = new Thickness(0, index == 0 ? 6 * s : 0, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        double thumb = 42 * s;
        var frame = new Border
        {
            Width = thumb,
            Height = thumb,
            CornerRadius = new CornerRadius(M3.CornerS),
            Background = Palette.Solid(Palette.IsDark ? "#1BFFFFFF" : "#10000000"),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true
        };

        if (_icons.TryGetValue(IconKey(site), out var icon))
        {
            frame.Background = new ImageBrush(icon) { Stretch = Stretch.UniformToFill };
        }
        else
        {
            frame.Child = SiteDialog.GlobeIcon(thumb * 0.52, Palette.TextTertiary);
        }

        Grid.SetColumn(frame, 0);
        grid.Children.Add(frame);

        var texts = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10 * s, 0, 6 * s, 0)
        };
        texts.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(site.Name) ? Sites.Host(site.Url) : site.Name,
            FontSize = 14 * s,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Palette.TextPrimary
        });
        texts.Children.Add(new TextBlock
        {
            Text = Sites.Host(site.Url),
            FontSize = 12 * s,
            Margin = new Thickness(0, 2 * s, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Palette.TextTertiary
        });
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        var open = BuildOpenButton(site, s);
        Grid.SetColumn(open, 2);
        grid.Children.Add(open);

        var row = new Border
        {
            CornerRadius = new CornerRadius(8 * s),
            Padding = new Thickness(7 * s, 5.5 * s, 5 * s, 5.5 * s),
            Margin = new Thickness(-7 * s, 2.5 * s, -5 * s, 2.5 * s),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = grid
        };

        ToolTipService.SetToolTip(row, UiStyles.RoundedToolTip($"{site.Name}\n{site.Url}"));
        ToolTipService.SetInitialShowDelay(row, 400);

        var hover = Palette.Solid(Palette.IsDark ? "#16FFFFFF" : "#0C000000");
        row.MouseEnter += (_, _) => row.Background = hover;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        row.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Sites.Open(site.Url);
        };

        return row;
    }

    private FrameworkElement BuildOpenButton(SiteOptions site, double s)
    {
        double size = 30 * s;
        var arrow = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M6,12 H16 M12,8 L16,12 L12,16"),
            Stroke = Palette.TextSecondary,
            StrokeThickness = 1.7 * s,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            Width = 13.5 * s,
            Height = 13.5 * s,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

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
            Child = arrow
        };

        ToolTipService.SetToolTip(button, UiStyles.RoundedToolTip("用浏览器打开 " + Sites.Host(site.Url)));
        ToolTipService.SetInitialShowDelay(button, 350);

        button.MouseEnter += (_, _) => button.Background = Palette.AccentBlue;
        button.MouseLeave += (_, _) => button.Background = Palette.Solid(Palette.IsDark ? "#14FFFFFF" : "#0B000000");
        button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Sites.Open(site.Url);
        };

        return button;
    }

    private static string IconKey(SiteOptions site) => site.Id + "|" + site.Icon;
}

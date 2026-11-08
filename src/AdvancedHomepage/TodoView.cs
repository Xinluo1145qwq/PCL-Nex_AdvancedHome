using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「便签」主页的待办卡片（M3 Expressive）：
/// 标题 + 完成进度条，下面是「添加一条」输入框和可勾选的清单。
/// 勾掉一条会划线降透明度，进度条跟着长，删掉的条目立刻落盘。
/// </summary>
internal sealed class TodoView : Grid
{
    private const double RefWidth = 320;
    private const int MaxItems = 200;

    private readonly TextBlock _title = new();
    private readonly TextBlock _count = new();
    private readonly Grid _progress = new();
    private readonly Border _track;
    private readonly Border _fill;
    private readonly Border _addBox;
    private readonly TextBox _input;
    private readonly TextBlock _placeholder;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _list = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _empty;

    private double _width = RefWidth;
    private double _scale = 1;
    private bool _editable = true;

    public TodoView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        ClipToBounds = false;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ── 标题 + 计数 ──
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _title.Text = "待办";
        _title.FontWeight = FontWeights.SemiBold;
        _title.Foreground = M3.C.OnSurface;
        _title.VerticalAlignment = VerticalAlignment.Bottom;
        header.Children.Add(_title);

        _count.Foreground = M3.C.OnSurfaceVariant;
        _count.VerticalAlignment = VerticalAlignment.Bottom;
        _count.Margin = new Thickness(0, 0, 1, 3);
        Grid.SetColumn(_count, 1);
        header.Children.Add(_count);

        Children.Add(header);

        // ── 进度条 ──
        _track = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = M3.C.SurfaceContainerHighest,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        _fill = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = M3.C.Primary,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        _progress.Margin = new Thickness(0, 8, 0, 0);
        _progress.Children.Add(_track);
        _progress.Children.Add(_fill);
        _progress.SizeChanged += (_, _) => UpdateProgress(false);
        Grid.SetRow(_progress, 1);
        Children.Add(_progress);

        // ── 添加一条 ──
        _input = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = M3.C.OnSurface,
            CaretBrush = M3.C.Primary,
            SelectionBrush = M3.Layer(M3.Of(M3.C.Primary), M3.Of(M3.C.Surface), 0.35),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null
        };
        _input.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AddFromInput();
        };

        _placeholder = new TextBlock
        {
            Text = "添加一条待办，回车确认",
            Foreground = M3.C.OnSurfaceVariant,
            Opacity = 0.55,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center
        };

        var inputBody = new Grid();
        inputBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputBody.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputBody.Children.Add(_input);
        inputBody.Children.Add(_placeholder);

        // 输入框尾部的 icon button：M3 是 40dp，不是缩小的 FAB
        var plus = M3.Icon("M12,5.5 V18.5 M5.5,12 H18.5", M3.C.OnPrimary, 20);

        var addButton = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = M3.C.Primary,
            Cursor = Cursors.Hand,
            Child = plus,
            VerticalAlignment = VerticalAlignment.Center
        };
        addButton.MouseEnter += (_, _) =>
            addButton.Background = M3.Layer(M3.Of(M3.C.Primary), M3.Of(M3.C.OnPrimary), M3.HoverAlpha);
        addButton.MouseLeave += (_, _) => addButton.Background = M3.C.Primary;
        addButton.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            AddFromInput();
        };
        Grid.SetColumn(addButton, 1);
        inputBody.Children.Add(addButton);

        _addBox = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            // 和便签的输入区用同一档容器色，两张卡的输入控件才不会一个深一个浅
            Background = M3.C.SurfaceContainerHighest,
            BorderThickness = new Thickness(1),
            BorderBrush = M3.C.OutlineVariant,
            Child = inputBody
        };
        _input.TextChanged += (_, _) =>
            _placeholder.Visibility = string.IsNullOrEmpty(_input.Text) ? Visibility.Visible : Visibility.Collapsed;
        _input.GotKeyboardFocus += (_, _) => _addBox.BorderBrush = M3.C.Primary;
        _input.LostKeyboardFocus += (_, _) => _addBox.BorderBrush = M3.C.OutlineVariant;

        Grid.SetRow(_addBox, 2);
        Children.Add(_addBox);

        // ── 清单 ──
        _empty = new TextBlock
        {
            Text = "还没有待办。先加一条，今天就从这一件开始。",
            Foreground = M3.C.OnSurfaceVariant,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 10, 2, 0),
            Visibility = Visibility.Collapsed
        };

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Content = _list,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(0)
        };

        var listHost = new Grid();
        listHost.Children.Add(_scroll);
        listHost.Children.Add(_empty);
        Grid.SetRow(listHost, 3);
        Children.Add(listHost);

        Build(RefWidth);
        Reload();
    }

    public void SetEditing(bool editing)
    {
        _editable = !editing;
        _input.IsHitTestVisible = !editing;
        _addBox.IsHitTestVisible = !editing;
        _scroll.IsHitTestVisible = !editing;
        Opacity = editing ? 0.92 : 1;
    }

    public void ApplyTheme()
    {
        _title.Foreground = M3.C.OnSurface;
        _count.Foreground = M3.C.OnSurfaceVariant;
        _track.Background = M3.C.SurfaceContainerHighest;
        _fill.Background = M3.C.Primary;
        _addBox.Background = M3.C.SurfaceContainerHighest;
        _input.Foreground = M3.C.OnSurface;
        _input.CaretBrush = M3.C.Primary;
        _input.SelectionBrush = M3.Layer(M3.Of(M3.C.Primary), M3.Of(M3.C.Surface), 0.35);
        _placeholder.Foreground = M3.C.OnSurfaceVariant;
        _empty.Foreground = M3.C.OnSurfaceVariant;
        Reload();
    }

    // ─────────────────────────── 数据 ───────────────────────────

    public void Reload()
    {
        _list.Children.Clear();

        var items = Cfg.Todos;
        foreach (var item in items.ToList()) _list.Children.Add(BuildRow(item));

        _empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 一条都没有时进度条会变成一条灰线，看着像分割线，直接收起来
        _progress.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        int done = items.Count(x => x.Done);
        _count.Text = items.Count == 0 ? "" : $"{done}/{items.Count}";
        UpdateProgress(false);
    }

    private void AddFromInput()
    {
        if (!_editable) return;

        string text = (_input.Text ?? "").Trim();
        if (text.Length == 0) return;

        var todos = Cfg.Todos;
        if (todos.Count >= MaxItems) return;

        todos.Insert(0, new TodoItem
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8),
            Text = text.Length > 120 ? text.Substring(0, 120) : text,
            Created = DateTime.Now.Ticks
        });

        Cfg.SaveNotes();
        _input.Text = "";
        Reload();
    }

    private void Toggle(TodoItem item)
    {
        item.Done = !item.Done;
        Cfg.SaveNotes();
        Reload();
    }

    private void Remove(TodoItem item)
    {
        Cfg.Todos.RemoveAll(x => x.Id == item.Id);
        Cfg.SaveNotes();
        Reload();
    }

    private void UpdateProgress(bool animate)
    {
        int total = Cfg.Todos.Count;
        double ratio = total == 0 ? 0 : Cfg.Todos.Count(x => x.Done) / (double)total;

        double width = Math.Max(0, _track.ActualWidth * ratio);

        if (!animate)
        {
            _fill.Width = width;
            return;
        }

        _fill.BeginAnimation(WidthProperty, new DoubleAnimation(width, M3.FastSpatial) { EasingFunction = M3.Spatial });
    }

    // ─────────────────────────── 行 ───────────────────────────

    private FrameworkElement BuildRow(TodoItem item)
    {
        double s = _scale;

        // M3 checkbox：18dp、2dp 圆角。圆角给大了会看成单选按钮
        var box = new Border
        {
            Width = 18 * s,
            Height = 18 * s,
            CornerRadius = new CornerRadius(2 * s),
            BorderThickness = new Thickness(2),
            BorderBrush = item.Done ? M3.C.Primary : M3.C.OnSurfaceVariant,
            Background = item.Done ? M3.C.Primary : Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            Child = item.Done
                ? M3.Icon("M4.5,12.5 L9.5,17.5 L19,7", M3.C.OnPrimary, 12 * s)
                : null
        };

        var text = new TextBlock
        {
            Text = item.Text,
            FontSize = 14 * s,
            Foreground = item.Done ? M3.C.OnSurfaceVariant : M3.C.OnSurface,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10 * s, 0, 8 * s, 0),
            Opacity = item.Done ? 0.62 : 1
        };

        if (item.Done) text.TextDecorations = TextDecorations.Strikethrough;

        var cross = new TextBlock
        {
            Text = "✕",
            FontSize = 11 * s,
            Foreground = M3.C.OnSurfaceVariant,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            Cursor = Cursors.Hand
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(box);
        row.Children.Add(text);
        Grid.SetColumn(text, 1);
        row.Children.Add(cross);
        Grid.SetColumn(cross, 2);

        var host = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerS),
            Background = Brushes.Transparent,
            Padding = new Thickness(6 * s, 6 * s, 6 * s, 6 * s),
            Margin = new Thickness(0, 1 * s, 0, 1 * s),
            Cursor = Cursors.Hand,
            Child = row
        };

        var hover = M3.Layer(M3.Of(M3.C.SurfaceLow), M3.Of(M3.C.OnSurface), 0.06);
        host.MouseEnter += (_, _) =>
        {
            host.Background = hover;
            cross.Opacity = 0.8;
        };
        host.MouseLeave += (_, _) =>
        {
            host.Background = Brushes.Transparent;
            cross.Opacity = 0;
        };
        host.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Toggle(item);
        };

        cross.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Remove(item);
        };

        return host;
    }

    // ─────────────────────────── 尺寸 ───────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = _width;

        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * 0.72;

        if (Math.Abs(w - _width) > 0.4) Build(w);

        var size = new Size(w, double.IsInfinity(availableSize.Height) ? h : Math.Min(h, availableSize.Height));
        base.MeasureOverride(size);
        return size;
    }

    private void Build(double width)
    {
        if (width <= 0) return;

        _width = width;
        _scale = Math.Clamp(width / RefWidth, 0.72, 2.2);
        double s = _scale;

        Margin = new Thickness(16 * s, 14 * s, 16 * s, 14 * s);
        _title.FontSize = UiStyles.CardTitleSize(width);
        _count.FontSize = 11 * s;

        _addBox.Margin = new Thickness(0, 12 * s, 0, 0);
        _addBox.Padding = new Thickness(16 * s, 4 * s, 4 * s, 4 * s);
        _addBox.MinHeight = 48 * s;
        _input.FontSize = 14 * s;
        _placeholder.FontSize = 14 * s;

        _empty.FontSize = 12 * s;
    }
}

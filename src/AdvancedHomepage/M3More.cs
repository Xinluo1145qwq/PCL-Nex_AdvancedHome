using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>M3 复选框：18dp、4dp 圆角；勾上时整块填 primary 再画对勾。</summary>
internal sealed class M3Checkbox : Border
{
    private readonly Path _check;
    private readonly M3.Scheme _scheme;
    private bool _checked;

    public event Action<bool> Changed;

    public M3Checkbox(bool value, M3.Scheme scheme = null, double scale = 1)
    {
        _scheme = scheme ?? M3.C;
        _checked = value;

        Width = 18 * scale;
        Height = 18 * scale;
        CornerRadius = new CornerRadius(4 * scale);
        BorderThickness = new Thickness(2 * scale);
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;

        _check = new Path
        {
            Data = Geometry.Parse("M3.2,8.6 L6.7,12.1 L13,4.8"),
            Stroke = _scheme.OnPrimary,
            StrokeThickness = 2.1,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(1.6 * scale),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        Child = _check;

        Apply(0);

        MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            IsChecked = !_checked;
            Changed?.Invoke(_checked);
        };
    }

    public bool IsChecked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Apply(1);
        }
    }

    private void Apply(double animate)
    {
        var fill = _checked ? _scheme.Primary : Brushes.Transparent;
        if (animate > 0) M3.AnimateFill(this, fill);
        else Background = fill;

        BorderBrush = _checked ? _scheme.Primary : _scheme.Outline;
        _check.Visibility = _checked ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>M3 单选框：20dp 圆，选中是 primary 圆环 + 10dp 实心点。</summary>
internal sealed class M3RadioButton : Border
{
    private readonly Border _dot;
    private readonly M3.Scheme _scheme;
    private bool _checked;

    public event Action<bool> Changed;

    public M3RadioButton(bool value, M3.Scheme scheme = null, double scale = 1)
    {
        _scheme = scheme ?? M3.C;
        _checked = value;

        Width = 20 * scale;
        Height = 20 * scale;
        CornerRadius = new CornerRadius(10 * scale);
        BorderThickness = new Thickness(2 * scale);
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;

        _dot = new Border
        {
            Width = 10 * scale,
            Height = 10 * scale,
            CornerRadius = new CornerRadius(5 * scale),
            Background = _scheme.Primary,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = value ? Visibility.Visible : Visibility.Collapsed,
            IsHitTestVisible = false
        };
        Child = _dot;

        Apply(false);

        MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (_checked) return;
            IsChecked = true;
            Changed?.Invoke(true);
        };
    }

    public bool IsChecked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Apply(true);
        }
    }

    private void Apply(bool animate)
    {
        BorderBrush = _checked ? _scheme.Primary : _scheme.Outline;
        _dot.Visibility = _checked ? Visibility.Visible : Visibility.Collapsed;

        if (!animate) return;

        // 选中时那个点「弹」一下，比直接显隐更像 M3 的 motion
        _dot.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = _dot.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        _dot.RenderTransform = scale;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(_checked ? 1 : 0.2, M3.FastSpatial) { EasingFunction = M3.Spatial });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(_checked ? 1 : 0.2, M3.FastSpatial) { EasingFunction = M3.Spatial });
    }
}

/// <summary>M3 徽标：要么是一个 8dp 小圆点，要么是带数字的小药丸。</summary>
internal static class M3Badge
{
    public static Border Dot(Brush brush = null, double size = 8) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = brush ?? M3.C.Error,
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalAlignment = HorizontalAlignment.Right
    };

    public static Border Count(int count, M3.Scheme scheme = null, double scale = 1)
    {
        scheme ??= M3.C;
        return new Border
        {
            MinWidth = 18 * scale,
            Height = 18 * scale,
            CornerRadius = new CornerRadius(9 * scale),
            Background = scheme.Error,
            Padding = new Thickness(5 * scale, 0, 5 * scale, 0),
            Child = new TextBlock
            {
                Text = count > 99 ? "99+" : count.ToString(),
                FontSize = 10.5 * scale,
                FontWeight = FontWeights.SemiBold,
                Foreground = scheme.OnError,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
}

/// <summary>M3 标签页：文字 + 选中项底下的 3dp 指示条，切换有淡入。</summary>
internal sealed class M3Tabs : StackPanel
{
    private readonly List<TabItem> _tabs = new();
    private readonly M3.Scheme _scheme;
    private int _selected;

    private sealed class TabItem
    {
        public Border Host = null!;
        public TextBlock Label = null!;
        public Border Underline = null!;
    }

    public event Action<int> Changed;

    public M3Tabs(string[] labels, int selected, M3.Scheme scheme = null, double scale = 1)
    {
        Orientation = Orientation.Horizontal;
        _scheme = scheme ?? M3.C;
        _selected = Math.Clamp(selected, 0, Math.Max(0, labels.Length - 1));

        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;

            var label = new TextBlock
            {
                Text = labels[i],
                FontSize = 13 * scale,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = _scheme.OnSurfaceVariant
            };

            var underline = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = _scheme.Primary,
                Margin = new Thickness(0, 9, 0, 0),
                Opacity = 0
            };

            var inner = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            inner.Children.Add(label);
            inner.Children.Add(underline);

            var host = new Border
            {
                Padding = new Thickness(16 * scale, 10, 16 * scale, 0),
                MinWidth = 64 * scale,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = inner
            };

            host.MouseEnter += (_, _) =>
            {
                if (_selected != index) host.Background = UiSkin.Hover(UiSkin.Card);
            };
            host.MouseLeave += (_, _) =>
            {
                if (_selected != index) host.Background = Brushes.Transparent;
            };
            host.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Select(index);
                Changed?.Invoke(index);
            };

            var item = new TabItem { Host = host, Label = label, Underline = underline };
            _tabs.Add(item);
            Children.Add(host);
        }

        Apply(false);
    }

    public void Select(int index) => Select(index, true);

    private void Select(int index, bool animate)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _selected = index;
        Apply(animate);
    }

    private void Apply(bool animate)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool on = i == _selected;
            var item = _tabs[i];

            item.Label.Foreground = on ? _scheme.Primary : _scheme.OnSurfaceVariant;
            item.Host.Background = Brushes.Transparent;

            if (!animate)
            {
                item.Underline.Opacity = on ? 1 : 0;
                continue;
            }

            item.Underline.BeginAnimation(OpacityProperty,
                new DoubleAnimation(on ? 1 : 0, M3.FastEffects) { EasingFunction = M3.Effects });
        }
    }
}

/// <summary>
/// M3 扩展面板：一行标题 + 展开箭头，点开露出内容。
/// 内容用 MaxHeight 做过渡，所以收起时不会「啪」地消失。
/// </summary>
internal sealed class M3Expansion : Border
{
    private readonly Border _body;
    private readonly Path _arrow;
    private bool _open;

    public M3Expansion(string title, string subtitle, FrameworkElement content, bool open = false,
        M3.Scheme scheme = null, double scale = 1)
    {
        scheme ??= M3.C;

        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13.5 * scale,
            FontWeight = FontWeights.Medium,
            Foreground = scheme.OnSurface
        });

        if (!string.IsNullOrEmpty(subtitle))
        {
            titles.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5 * scale,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = scheme.OnSurfaceVariant
            });
        }

        _arrow = new Path
        {
            Data = Geometry.Parse("M7,10 L12,15 L17,10"),
            Stroke = scheme.OnSurfaceVariant,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 20,
            Height = 20,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        _arrow.RenderTransform = new RotateTransform(open ? 180 : 0);

        var header = new Grid { Margin = new Thickness(16 * scale, 12 * scale, 14 * scale, 12 * scale) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(titles);
        Grid.SetColumn(_arrow, 1);
        header.Children.Add(_arrow);

        var headerHost = new Border
        {
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = header
        };
        headerHost.MouseEnter += (_, _) => headerHost.Background = UiSkin.Hover(UiSkin.Card);
        headerHost.MouseLeave += (_, _) => headerHost.Background = Brushes.Transparent;
        headerHost.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            IsOpen = !_open;
        };

        _body = new Border
        {
            Padding = new Thickness(16 * scale, 0, 16 * scale, 14 * scale),
            Child = content,
            Visibility = open ? Visibility.Visible : Visibility.Collapsed,
            Opacity = open ? 1 : 0
        };

        var stack = new StackPanel();
        stack.Children.Add(headerHost);
        stack.Children.Add(_body);

        Background = scheme.SurfaceContainerLow;
        BorderBrush = scheme.OutlineVariant;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(M3.CornerM);
        Child = stack;
        ClipToBounds = true;
        _open = open;
    }

    public bool IsOpen
    {
        get => _open;
        set
        {
            if (_open == value) return;
            _open = value;

            var rotate = (RotateTransform)_arrow.RenderTransform;
            rotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(value ? 180 : 0, M3.FastSpatial) { EasingFunction = M3.Spatial });

            if (value)
            {
                _body.Visibility = Visibility.Visible;
                _body.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(1, M3.DefaultEffects) { EasingFunction = M3.Effects });
            }
            else
            {
                var fade = new DoubleAnimation(0, M3.FastEffects) { EasingFunction = M3.Effects };
                fade.Completed += (_, _) =>
                {
                    if (!_open) _body.Visibility = Visibility.Collapsed;
                };
                _body.BeginAnimation(OpacityProperty, fade);
            }
        }
    }
}

/// <summary>M3 Snackbar：inverse surface 底、左侧一句话、右侧一个文字动作。</summary>
internal static class M3Snackbar
{
    public static Border Create(string text, string action, Action onAction, M3.Scheme scheme = null,
        double scale = 1)
    {
        scheme ??= M3.C;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12.5 * scale,
            Foreground = scheme.InverseOnSurface,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrEmpty(action))
        {
            var button = new Border
            {
                Padding = new Thickness(12 * scale, 8 * scale, 12 * scale, 8 * scale),
                Margin = new Thickness(12 * scale, 0, 0, 0),
                CornerRadius = new CornerRadius(8 * scale),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = action,
                    FontSize = 12.5 * scale,
                    FontWeight = FontWeights.Medium,
                    Foreground = scheme.InversePrimary
                }
            };
            button.MouseEnter += (_, _) => button.Background = M3.Layer(M3.Of(scheme.InverseSurface),
                M3.Of(scheme.InverseOnSurface), M3.HoverAlpha);
            button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;
            button.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onAction?.Invoke();
            };

            Grid.SetColumn(button, 1);
            grid.Children.Add(button);
        }

        return new Border
        {
            Background = scheme.InverseSurface,
            CornerRadius = new CornerRadius(M3.CornerS),
            Padding = new Thickness(16 * scale, 10 * scale, 8 * scale, 10 * scale),
            Child = grid,
            Effect = M3.Elevation(3)
        };
    }
}

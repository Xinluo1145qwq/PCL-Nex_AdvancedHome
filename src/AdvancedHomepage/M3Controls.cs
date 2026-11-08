using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// M3 Expressive 卡片：色调容器 + 12dp 圆角 + 1 级阴影 + 状态层。
/// 取代启动器的 MyCard——那边是启动器自己的视觉语言（11dp、半透明、内描边），
/// 放到 M3 版式里会显得「两层皮」。
/// </summary>
internal sealed class M3Card : Grid
{
    private readonly Border _fill;
    private readonly Border _image;
    private readonly Border _state;
    private readonly Border _outline;

    public Grid Body { get; } = new();

    public double Radius { get; }

    private double _fillOpacity = 1;
    private readonly bool _filled;
    private readonly bool _outlined;

    public M3Card(double radius = M3.CardRadius, bool filled = false, bool interactive = false, bool outline = false)
    {
        Radius = radius;
        _filled = filled;
        _outlined = outline;
        ClipToBounds = true;

        _fill = new Border
        {
            CornerRadius = new CornerRadius(radius),
            Background = filled ? M3.C.SurfaceHighest : M3.C.SurfaceLow,
            IsHitTestVisible = false
        };
        Children.Add(_fill);

        _image = new Border
        {
            CornerRadius = new CornerRadius(radius),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Children.Add(_image);

        Children.Add(Body);

        _state = new Border
        {
            CornerRadius = new CornerRadius(radius),
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Children.Add(_state);

        _outline = new Border
        {
            CornerRadius = new CornerRadius(radius),
            BorderThickness = new Thickness(1),
            BorderBrush = outline ? M3.C.OutlineVariant : Brushes.Transparent,
            IsHitTestVisible = false
        };
        Children.Add(_outline);

        Effect = M3.Elevation(1);

        if (interactive)
        {
            MouseEnter += (_, _) => ShowState(M3.HoverAlpha);
            MouseLeave += (_, _) =>
            {
                ShowState(0);
                _state.Background = Brushes.Transparent;
            };
            PreviewMouseLeftButtonDown += (_, _) => ShowState(M3.PressAlpha);
            PreviewMouseLeftButtonUp += (_, _) => ShowState(M3.HoverAlpha);
        }

        SizeChanged += (_, _) => UpdateClip();
    }

    /// <summary>卡片底色不透明度：0 = 完全透出壁纸，1 = 完整色调容器。</summary>
    public double FillOpacity
    {
        get => _fillOpacity;
        set
        {
            _fillOpacity = Math.Clamp(value, 0, 1);
            _fill.Opacity = _fillOpacity;
            _outline.Opacity = _fillOpacity;
        }
    }

    /// <summary>主题切换后重取角色色（M3 的颜色是构造时定的，不重取会停在旧主题）。</summary>
    public void RefreshTheme()
    {
        _fill.Background = _filled ? M3.C.SurfaceHighest : M3.C.SurfaceLow;
        _outline.BorderBrush = _outlined ? M3.C.OutlineVariant : Brushes.Transparent;
        Effect = M3.Elevation(1);
        _state.Background = Brushes.Transparent;
        _state.Visibility = Visibility.Collapsed;
    }

    public Brush FillBrush
    {
        get => _fill.Background;
        set => _fill.Background = value;
    }

    public void SetImage(Brush brush, double opacity)
    {
        if (brush == null)
        {
            _image.Visibility = Visibility.Collapsed;
            _image.Background = null;
            return;
        }

        _image.Background = brush;
        _image.Opacity = Math.Clamp(opacity, 0, 1);
        _image.Visibility = Visibility.Visible;
    }

    private void ShowState(double alpha)
    {
        if (alpha <= 0)
        {
            _state.Visibility = Visibility.Collapsed;
            return;
        }

        var baseColor = M3.Of(FillBrush);
        _state.Background = M3.Layer(baseColor, M3.Of(M3.C.OnSurface), alpha);
        _state.Visibility = Visibility.Visible;
    }

    private void UpdateClip()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), Radius, Radius);
    }
}

internal enum M3ButtonKind
{
    Filled,
    Tonal,
    Outlined,
    Text,
    Elevated
}

/// <summary>
/// M3 Expressive 按钮：40dp 高，静止时 12dp 圆角矩形，鼠标移上去圆角弹到全圆角（药丸）。
/// 这是 Expressive 的 shape morph——形状本身也在回应指针，不只是换个底色。
/// </summary>
internal static class M3Button
{
    public static Border Create(string text, Action onClick, M3ButtonKind kind = M3ButtonKind.Filled,
        bool enabled = true, double scale = 1, bool danger = false, string iconData = null,
        M3.Scheme scheme = null, bool morph = true)
    {
        double height = M3.ButtonHeight * scale;
        double pillRadius = height / 2;
        double restRadius = morph ? Math.Min(M3.CornerM * scale, pillRadius) : pillRadius;
        double hoverRadius = morph ? pillRadius : pillRadius;

        scheme ??= M3.C;
        (Brush bg, Brush fg, Brush border) = kind switch
        {
            M3ButtonKind.Filled => (danger ? scheme.Error : scheme.Primary, scheme.OnPrimary, (Brush)null),
            M3ButtonKind.Tonal => (scheme.SecondaryContainer, scheme.OnSecondaryContainer, (Brush)null),
            M3ButtonKind.Outlined => (Brushes.Transparent, scheme.Primary, scheme.Outline),
            M3ButtonKind.Elevated => (scheme.SurfaceContainerLow, scheme.Primary, (Brush)null),
            _ => (Brushes.Transparent, danger ? scheme.Error : scheme.Primary, (Brush)null)
        };

        if (!enabled)
        {
            // M3 的 disabled filled 是 onSurface 12% 底，不是一整块实心黑
            bg = kind == M3ButtonKind.Filled
                ? M3.Layer(M3.Of(scheme.Surface), M3.Of(scheme.OnSurface), 0.12)
                : Brushes.Transparent;
            fg = M3.Layer(M3.Of(scheme.Surface), M3.Of(scheme.OnSurface), 0.38);
        }

        var label = M3.Text(text, M3.TypeRole.LabelLarge, fg, scale, emphasized: true);
        label.VerticalAlignment = VerticalAlignment.Center;

        double padX = (kind is M3ButtonKind.Text or M3ButtonKind.Outlined ? 12 : 20) * scale;
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(padX, 0, padX, 0)
        };

        if (!string.IsNullOrEmpty(iconData))
        {
            content.Children.Add(new Viewbox
            {
                Width = 18 * scale,
                Height = 18 * scale,
                Margin = new Thickness(0, 0, 8 * scale, 0),
                Child = M3.Icon(iconData, fg, 24),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        content.Children.Add(label);

        // 底色和边框都画在这一层上，所以圆角才动得起来
        var surface = new M3ShapeFill
        {
            Fill = bg,
            Stroke = border,
            StrokeThickness = border == null ? 0 : 1,
            Radius = restRadius,
            IsHitTestVisible = false
        };

        var root = new Grid();
        root.Children.Add(surface);
        root.Children.Add(content);

        var button = new Border
        {
            Height = height,
            // 透明而不是 null：null 的 Border 只有子元素（文字/图标）能命中，
            // 点在 padding 上完全没反应，这类按钮在弹窗/列表里特别容易「点不动」。
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
            Child = root,
            MinWidth = 48 * scale,
            HorizontalAlignment = HorizontalAlignment.Left,
            Effect = kind == M3ButtonKind.Elevated ? M3.Elevation(1) : null
        };

        if (!enabled) return button;

        var hoverColor = M3.Layer(M3.Of(bg), M3.Of(fg), M3.HoverAlpha);
        var pressColor = M3.Layer(M3.Of(bg), M3.Of(fg), M3.PressAlpha);

        button.MouseEnter += (_, _) =>
        {
            surface.AnimateFill(hoverColor);
            surface.AnimateRadius(hoverRadius);
        };
        button.MouseLeave += (_, _) =>
        {
            surface.AnimateFill(bg);
            surface.AnimateRadius(restRadius, M3.DefaultSpatial);
            AnimateScale(button, 1);
        };
        button.PreviewMouseLeftButtonDown += (_, _) =>
        {
            surface.AnimateFill(pressColor);
            surface.AnimateRadius(hoverRadius - 2 * scale, M3.FastSpatial);
            AnimateScale(button, 0.97);
        };
        button.PreviewMouseLeftButtonUp += (_, _) =>
        {
            surface.AnimateRadius(hoverRadius);
            AnimateScale(button, 1);
        };
        button.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onClick?.Invoke();
        };

        return button;
    }

    private static void AnimateScale(UIElement element, double target)
    {
        if (element.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = scale;
        }

        var anim = new DoubleAnimation(target, M3.FastSpatial) { EasingFunction = M3.Spatial };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }
}

/// <summary>M3 图标按钮：40dp 圆形，图标 24dp，透明底 + 状态层。</summary>
internal static class M3IconButton
{
    public static Border Create(string iconData, Action onClick, string tooltip = null,
        double scale = 1, bool filled = false, bool danger = false, M3.Scheme scheme = null)
    {
        double size = M3.IconButtonSize * scale;
        scheme ??= M3.C;
        var fg = danger ? scheme.Error : (filled ? scheme.OnPrimary : scheme.OnSurfaceVariant);
        var bg = filled ? (danger ? scheme.Error : scheme.Primary) : (Brush)Brushes.Transparent;

        var icon = M3.Icon(iconData, fg, 24 * scale);
        var button = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = bg,
            Cursor = Cursors.Hand,
            Child = icon,
            ToolTip = tooltip == null ? null : UiStyles.Tooltip(tooltip)
        };

        if (tooltip != null) button.ToolTip = UiStyles.Tooltip(tooltip);

        var hover = M3.Layer(M3.Of(bg), M3.Of(fg), M3.HoverAlpha);
        var press = M3.Layer(M3.Of(bg), M3.Of(fg), M3.PressAlpha);

        button.MouseEnter += (_, _) => M3.AnimateFill(button, hover);
        button.MouseLeave += (_, _) => M3.AnimateFill(button, bg);
        button.PreviewMouseLeftButtonDown += (_, _) => M3.AnimateFill(button, press);
        button.MouseLeftButtonUp += (_, e) =>
        {
            M3.AnimateFill(button, bg);
            e.Handled = true;
            onClick?.Invoke();
        };

        return button;
    }
}

/// <summary>M3 extended FAB：56dp 高、16dp 圆角、primary container 底。</summary>
internal static class M3Fab
{
    public static Border Create(string text, string iconData, Action onClick, double scale = 1,
        M3.Scheme scheme = null)
    {
        double height = 56 * scale;
        scheme ??= M3.C;

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.Children.Add(new Viewbox
        {
            Width = 20 * scale,
            Height = 20 * scale,
            Margin = new Thickness(0, 0, 10 * scale, 0),
            Child = M3.Icon(iconData, scheme.OnPrimaryContainer, 24),
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(M3.Text(text, M3.TypeRole.LabelLarge, scheme.OnPrimaryContainer, scale, true));

        var fab = new Border
        {
            Height = height,
            CornerRadius = new CornerRadius(16 * scale),
            Background = scheme.PrimaryContainer,
            Padding = new Thickness(18 * scale, 0, 20 * scale, 0),
            Cursor = Cursors.Hand,
            Child = content,
            Effect = M3.Elevation(2)
        };

        var hover = M3.Layer(M3.Of(scheme.PrimaryContainer), M3.Of(scheme.OnPrimaryContainer), M3.HoverAlpha);
        fab.MouseEnter += (_, _) => M3.AnimateFill(fab, hover);
        fab.MouseLeave += (_, _) => M3.AnimateFill(fab, scheme.PrimaryContainer);
        fab.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onClick?.Invoke();
        };

        return fab;
    }
}

/// <summary>M3 分段按钮：一个圆角容器里放两段，选中段用 secondary container。</summary>
internal sealed class M3Segmented : Border
{
    private readonly Border[] _segments;
    private readonly TextBlock[] _labels;
    private readonly M3.Scheme _scheme;
    private int _selected;

    public M3Segmented(string[] labels, int selected, Action<int> onSelect, double scale = 1,
        M3.Scheme scheme = null)
    {
        scheme ??= M3.C;
        _scheme = scheme;
        double height = M3.ButtonHeight * scale;

        _segments = new Border[labels.Length];
        _labels = new TextBlock[labels.Length];

        var row = new Grid();
        for (int i = 0; i < labels.Length; i++)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        for (int i = 0; i < labels.Length; i++)
        {
            var label = M3.Text(labels[i], M3.TypeRole.LabelLarge, scheme.OnSurfaceVariant, scale, true);
            label.VerticalAlignment = VerticalAlignment.Center;
            _labels[i] = label;

            // 选中态靠 secondary container 底色表达，不再额外塞一个对勾
            // （顶部主页切换那条三段太窄，对勾会把文字挤掉）
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(label);

            var segment = new Border
            {
                Height = height - 4,
                CornerRadius = new CornerRadius((height - 4) / 2),
                Padding = new Thickness(14 * scale, 0, 16 * scale, 0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = content
            };

            int index = i;
            segment.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                SetSelected(index, true);
            };
            segment.MouseEnter += (_, _) =>
            {
                if (index != _selected)
                    M3.AnimateFill(segment, M3.Layer(M3.Of(scheme.Surface), M3.Of(scheme.OnSurface), M3.HoverAlpha));
            };
            segment.MouseLeave += (_, _) =>
            {
                if (index != _selected) M3.AnimateFill(segment, Brushes.Transparent);
            };

            Grid.SetColumn(segment, i);
            row.Children.Add(segment);
            _segments[i] = segment;
        }

        Height = height;
        CornerRadius = new CornerRadius(height / 2);
        BorderThickness = new Thickness(1);
        BorderBrush = scheme.Outline;
        Padding = new Thickness(2);
        // 壁纸当背景时，纯描边的分段按钮会看不清边界，铺一层 surface container
        Background = scheme.SurfaceContainer;
        Child = row;

        _selected = -1;
        SetSelected(selected, false);
        _onSelect = onSelect;
    }

    private readonly Action<int> _onSelect;

    public void SetSelected(int index, bool notify)
    {
        if (index < 0 || index >= _segments.Length) return;
        _selected = index;
        var scheme = _scheme;

        for (int i = 0; i < _segments.Length; i++)
        {
            bool active = i == index;
            M3.AnimateFill(_segments[i], active ? scheme.SecondaryContainer : Brushes.Transparent);
            _labels[i].Foreground = active ? scheme.OnSecondaryContainer : scheme.OnSurfaceVariant;
        }

        if (notify) _onSelect?.Invoke(index);
    }
}

/// <summary>M3 开关：52×32 轨道，选中是 primary，未选中是 surface container highest。</summary>
internal sealed class M3Switch : Grid
{
    private readonly Border _track;
    private readonly Border _thumb;
    private readonly M3.Scheme _scheme;
    private bool _checked;

    public event Action<bool> Changed;

    /// <param name="scheme">固定配色（设置窗口是写死的浅色底，不能跟着启动器进深色）。</param>
    public M3Switch(bool value, double scale = 1, M3.Scheme scheme = null)
    {
        Width = 52 * scale;
        Height = 32 * scale;
        Cursor = Cursors.Hand;
        Background = Brushes.Transparent;
        _scheme = scheme ?? M3.C;

        _track = new Border
        {
            CornerRadius = new CornerRadius(16 * scale),
            Background = _scheme.SurfaceHighest,
            BorderThickness = new Thickness(1),
            BorderBrush = _scheme.Outline,
            IsHitTestVisible = false
        };
        Children.Add(_track);

        _thumb = new Border
        {
            Width = 16 * scale,
            Height = 16 * scale,
            CornerRadius = new CornerRadius(8 * scale),
            Background = _scheme.Outline,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8 * scale, 0, 0, 0),
            IsHitTestVisible = false
        };
        Children.Add(_thumb);

        _checked = value;
        Apply(false);

        MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            _checked = !_checked;
            Apply(true);
            Changed?.Invoke(_checked);
        };
    }

    public bool IsChecked => _checked;

    public void Set(bool value, bool animate = true)
    {
        _checked = value;
        Apply(animate);
    }

    private void Apply(bool animate)
    {
        double scale = Height / 32.0;
        var scheme = _scheme;

        var trackColor = _checked ? scheme.Primary : scheme.SurfaceHighest;
        var thumbColor = _checked ? scheme.OnPrimary : scheme.Outline;
        double thumbSize = (_checked ? 24 : 16) * scale;
        double left = _checked ? 52 * scale - thumbSize - 4 * scale : 8 * scale;

        _track.Background = trackColor;
        _track.BorderBrush = _checked ? scheme.Primary : scheme.Outline;
        _thumb.Background = thumbColor;

        if (!animate)
        {
            _thumb.Width = thumbSize;
            _thumb.Height = thumbSize;
            _thumb.CornerRadius = new CornerRadius(thumbSize / 2);
            _thumb.Margin = new Thickness(left, 0, 0, 0);
            return;
        }

        var dur = M3.FastSpatial;
        _thumb.BeginAnimation(WidthProperty, new DoubleAnimation(thumbSize, dur) { EasingFunction = M3.Spatial });
        _thumb.BeginAnimation(HeightProperty, new DoubleAnimation(thumbSize, dur) { EasingFunction = M3.Spatial });
        _thumb.BeginAnimation(MarginProperty,
            new ThicknessAnimation(new Thickness(left, 0, 0, 0), dur) { EasingFunction = M3.Spatial });
        _thumb.CornerRadius = new CornerRadius(thumbSize / 2);
    }
}

/// <summary>M3 滑块：4dp 轨道 + 竖条手柄（4×44），拖动时手柄变宽。</summary>
internal sealed class M3Slider : Grid
{
    private readonly Border _active;
    private readonly Border _inactive;
    private readonly Border _thumb;
    private readonly M3.Scheme _scheme;
    private readonly double _min;
    private readonly double _max;
    private double _value;
    private bool _dragging;

    public event Action<double> Changed;

    /// <param name="scheme">固定配色（设置窗口是写死的浅色底）。</param>
    public M3Slider(double value, double min, double max, double scale = 1, M3.Scheme scheme = null)
    {
        _min = min;
        _max = max;
        _scheme = scheme ?? M3.C;
        _value = Math.Clamp(value, min, max);

        Height = 44 * scale;
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;

        var rail = new Grid { VerticalAlignment = VerticalAlignment.Center, Height = 4 * scale };
        Children.Add(rail);

        _inactive = new Border
        {
            Height = 4 * scale,
            CornerRadius = new CornerRadius(2 * scale),
            Background = _scheme.SecondaryContainer
        };
        rail.Children.Add(_inactive);

        _active = new Border
        {
            Height = 4 * scale,
            CornerRadius = new CornerRadius(2 * scale),
            Background = _scheme.Primary,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        rail.Children.Add(_active);

        _thumb = new Border
        {
            Width = 4 * scale,
            Height = 44 * scale,
            CornerRadius = new CornerRadius(2 * scale),
            Background = _scheme.Primary,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        Children.Add(_thumb);

        SizeChanged += (_, _) => Place(0);

        MouseLeftButtonDown += (_, e) =>
        {
            _dragging = true;
            CaptureMouse();
            Update(e.GetPosition(this).X);
        };
        MouseMove += (_, e) =>
        {
            if (_dragging) Update(e.GetPosition(this).X);
        };
        MouseLeftButtonUp += (_, _) => EndDrag();
        MouseLeave += (_, _) => EndDrag();
    }

    public double Value => _value;

    public void Set(double value, bool animate = true)
    {
        _value = Math.Clamp(value, _min, _max);
        Place(animate ? 120 : 0);
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        _thumb.BeginAnimation(WidthProperty,
            new DoubleAnimation(4 * (Height / 44), TimeSpan.FromMilliseconds(120)) { EasingFunction = M3.Spatial });
    }

    private void Update(double x)
    {
        double pad = 8 * (Height / 44);
        double width = Math.Max(1, ActualWidth - pad * 2);
        double ratio = Math.Clamp((x - pad) / width, 0, 1);
        _value = _min + (_max - _min) * ratio;
        Place(0);
        _thumb.BeginAnimation(WidthProperty,
            new DoubleAnimation(8 * (Height / 44), TimeSpan.FromMilliseconds(120)) { EasingFunction = M3.Spatial });
        Changed?.Invoke(_value);
    }

    private void Place(double animateMs)
    {
        double scale = Height / 44;
        double pad = 8 * scale;
        double width = Math.Max(1, ActualWidth - pad * 2);
        double ratio = _max <= _min ? 0 : (_value - _min) / (_max - _min);
        double x = pad + width * ratio - 2 * scale;

        _active.Width = Math.Max(0, width * ratio);
        _thumb.Margin = new Thickness(x, 0, 0, 0);

        if (animateMs > 0)
        {
            _thumb.BeginAnimation(MarginProperty,
                new ThicknessAnimation(new Thickness(x, 0, 0, 0), TimeSpan.FromMilliseconds(animateMs))
                {
                    EasingFunction = M3.Spatial
                });
        }
    }
}

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 一块可以动画圆角的实心面。
///
/// 为什么不用 Border：<c>Border.CornerRadius</c> 不是依赖属性，DoubleAnimation 打不上去，
/// 而 RectangleGeometry 的 RadiusX/RadiusY 虽然可动画、却会把描边斜着切掉一角。
/// 这里直接自己画——圆角是一个 DP，动画它就能得到 M3 Expressive 的 shape morph。
/// 按钮、FAB 这类需要「静止圆角矩形 / 悬停变圆角」的控件都铺这层当底。
/// </summary>
internal sealed class M3ShapeFill : FrameworkElement
{
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(double), typeof(M3ShapeFill),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    private Brush _fill;
    private Brush _stroke;
    private double _strokeThickness;

    public Brush Fill
    {
        get => _fill;
        set
        {
            if (ReferenceEquals(_fill, value)) return;
            _fill = value;
            InvalidateVisual();
        }
    }

    public Brush Stroke
    {
        get => _stroke;
        set
        {
            if (ReferenceEquals(_stroke, value)) return;
            _stroke = value;
            InvalidateVisual();
        }
    }

    public double StrokeThickness
    {
        get => _strokeThickness;
        set
        {
            if (Math.Abs(_strokeThickness - value) < 0.01) return;
            _strokeThickness = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth, height = ActualHeight;
        if (width <= 0.5 || height <= 0.5) return;

        double thickness = Math.Max(0, _strokeThickness);
        double inset = thickness / 2;
        double radius = Math.Max(0, Math.Min(Radius, Math.Min(width, height) / 2));

        var rect = new Rect(inset, inset, Math.Max(0.01, width - thickness), Math.Max(0.01, height - thickness));

        Pen pen = null;
        if (_stroke != null && thickness > 0)
        {
            pen = new Pen(_stroke, thickness);
            if (pen.CanFreeze) pen.Freeze();
        }

        dc.DrawRoundedRectangle(_fill ?? Brushes.Transparent, pen, rect, radius, radius);
    }

    /// <summary>底色渐变到目标色。目标不是纯色时直接换掉，不做动画。</summary>
    public void AnimateFill(Brush target, TimeSpan? duration = null)
    {
        if (target is not SolidColorBrush solid)
        {
            Fill = target;
            return;
        }

        if (_fill is not SolidColorBrush current || current.IsFrozen)
        {
            current = new SolidColorBrush(solid.Color);
            _fill = current;
            InvalidateVisual();
        }

        var animation = new ColorAnimation(solid.Color, duration ?? M3.FastEffects)
        {
            EasingFunction = M3.Effects
        };
        current.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    /// <summary>圆角变形：默认走 M3 的 spatial 弹簧，和按钮那种「吸过去」的手感一致。</summary>
    public void AnimateRadius(double target, TimeSpan? duration = null, IEasingFunction easing = null)
    {
        if (double.IsNaN(target) || double.IsInfinity(target)) return;

        var animation = new DoubleAnimation(target, duration ?? M3.FastSpatial)
        {
            EasingFunction = easing ?? M3.Spatial
        };
        BeginAnimation(RadiusProperty, animation);
    }

    public void SetRadius(double target)
    {
        BeginAnimation(RadiusProperty, null);
        Radius = target;
    }
}

/// <summary>
/// M3 Expressive 的波浪进度条：轨道和进度都是正弦波，相位随时间缓慢推进。
///
/// 确定性模式（<see cref="IsIndeterminate"/> = false）按 <see cref="Value"/> 截断；
/// 不确定模式画一段固定长度的波，自己从左边游到右边。
/// </summary>
internal sealed class M3WavyProgress : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(M3WavyProgress),
        new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly System.Windows.Threading.DispatcherTimer _ticker;
    private double _phase;
    private double _swim;

    private Brush _track = M3.Light.SecondaryContainer;
    private Brush _active = M3.Light.Primary;

    public M3WavyProgress()
    {
        Height = 22;

        _ticker = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _ticker.Tick += (_, _) => Advance();

        Loaded += (_, _) => _ticker.Start();
        Unloaded += (_, _) => _ticker.Stop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _ticker.Start();
            else _ticker.Stop();
        };
    }

    /// <summary>0–1。不确定模式下忽略。</summary>
    public double Value
    {
        get => Math.Clamp((double)GetValue(ValueProperty), 0, 1);
        set => SetValue(ValueProperty, Math.Clamp(value, 0, 1));
    }

    public bool IsIndeterminate { get; set; }

    /// <summary>波幅（dp）。默认 3.2，正好在 22dp 高里留出上下留白。</summary>
    public double Amplitude { get; set; } = 3.0;

    public double Wavelength { get; set; } = 46;

    public Brush TrackBrush
    {
        get => _track;
        set { _track = value; InvalidateVisual(); }
    }

    public Brush ActiveBrush
    {
        get => _active;
        set { _active = value; InvalidateVisual(); }
    }

    private void Advance()
    {
        // 相位推进得很慢：波纹在「流动」，但不至于让人盯着看分心
        _phase += 0.045;
        if (_phase > Math.PI * 2) _phase -= Math.PI * 2;

        if (IsIndeterminate)
        {
            // 从左边完全出界游到右边完全出界，再从头来，中间不会出现空档
            _swim += 0.012;
            if (_swim >= 1) _swim -= 1;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth, height = ActualHeight;
        if (width < 4 || height < 4) return;

        double centerY = height / 2;
        double amplitude = Math.Min(Amplitude, Math.Max(0, (height - 8) / 2));

        // 轨道：细一点的波
        var trackPen = new Pen(_track, 3.6)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        if (trackPen.CanFreeze) trackPen.Freeze();
        dc.DrawGeometry(null, trackPen, BuildWave(0, width, centerY, amplitude));

        if (IsIndeterminate)
        {
            // 一段 28% 宽的波浪在轨道上循环游动：起点从 -length 走到 width，
            // 全程至少有一段在视野里，不会突然空掉。
            double length = width * 0.28;
            double start = _swim * (width + length) - length;
            double end = start + length;

            var activePen = new Pen(_active, 4.8)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (activePen.CanFreeze) activePen.Freeze();

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
            dc.DrawGeometry(null, activePen, BuildWave(start, end, centerY, amplitude));
            dc.Pop();
            return;
        }

        double target = width * Value;
        if (target < 1) return;

        var fillPen = new Pen(_active, 4.8)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        if (fillPen.CanFreeze) fillPen.Freeze();

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
        dc.DrawGeometry(null, fillPen, BuildWave(0, target, centerY, amplitude));
        dc.Pop();
    }

    private Geometry BuildWave(double from, double to, double centerY, double amplitude)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double span = to - from;
            // 每 2dp 采样一个点，肉眼已经看不出折线
            int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(span) / 2));
            double step = span / steps;

            for (int i = 0; i <= steps; i++)
            {
                double x = from + step * i;
                double y = centerY + amplitude * Math.Sin(x / Wavelength * Math.PI * 2 + _phase);
                if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }
}

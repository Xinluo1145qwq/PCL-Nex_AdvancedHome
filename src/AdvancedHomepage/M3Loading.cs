using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// M3 环形进度指示器。
///
/// 不确定模式按 MD3 的节奏走：一个周期 5333ms 正好转四圈（即 1333ms / 圈），
/// 弧长在周期内从 10° 涨到 288° 再收回去——头部先跑、尾部慢慢追，
/// 所以看起来是「一段弧在追自己」，而不是一根固定长度的弧原地转。
/// 弧尾是 head - sweep，周期首尾都收在 10°，接缝处不会跳。
/// </summary>
internal sealed class M3CircularProgress : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(M3CircularProgress),
        new FrameworkPropertyMetadata(0.68, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double CycleMs = 5333;   // 四圈
    private const double MinSweep = 10;    // 收尾时的最短弧
    private const double MaxSweep = 288;   // 中段最长弧

    private readonly DispatcherTimer _ticker;
    private readonly System.Diagnostics.Stopwatch _watch = new();

    private bool _indeterminate;
    private Brush _track = M3.Light.SecondaryContainer;
    private Brush _active = M3.Light.Primary;

    public M3CircularProgress()
    {
        Width = 48;
        Height = 48;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _ticker.Tick += (_, _) => InvalidateVisual();

        Loaded += (_, _) => SyncTicker();
        Unloaded += (_, _) => _ticker.Stop();
        IsVisibleChanged += (_, _) => SyncTicker();
    }

    public double Value
    {
        get => Math.Clamp((double)GetValue(ValueProperty), 0, 1);
        set => SetValue(ValueProperty, Math.Clamp(value, 0, 1));
    }

    /// <summary>不确定模式：没有确定进度时用，弧会自己转并伸缩。</summary>
    public bool IsIndeterminate
    {
        get => _indeterminate;
        set
        {
            if (_indeterminate == value) return;
            _indeterminate = value;
            SyncTicker();
            InvalidateVisual();
        }
    }

    /// <summary>MD3 规范是 4dp。</summary>
    public double Thickness { get; set; } = 4;

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

    private void SyncTicker()
    {
        if (_indeterminate && IsLoaded && IsVisible)
        {
            _watch.Start();
            _ticker.Start();
        }
        else
        {
            _ticker.Stop();
            _watch.Stop();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 8) return;

        double thickness = Math.Min(Thickness, size / 2);
        double radius = (size - thickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        if (_track != null)
        {
            var trackPen = new Pen(_track, thickness);
            if (trackPen.CanFreeze) trackPen.Freeze();
            dc.DrawEllipse(null, trackPen, center, radius, radius);
        }

        if (_indeterminate)
        {
            double p = (_watch.Elapsed.TotalMilliseconds % CycleMs) / CycleMs;
            double sweep = MinSweep + (MaxSweep - MinSweep) * Math.Sin(Math.PI * p);
            double head = p * 1440;   // 四整圈
            DrawArc(dc, center, radius, thickness, head - sweep, sweep);
            return;
        }

        if (Value <= 0.001) return;
        DrawArc(dc, center, radius, thickness, 0, Value * 360);
    }

    private void DrawArc(DrawingContext dc, Point center, double radius, double thickness,
        double startDeg, double sweepDeg)
    {
        sweepDeg = Math.Min(sweepDeg, 359.9);
        if (sweepDeg <= 0.01) return;

        var pen = new Pen(_active, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        if (pen.CanFreeze) pen.Freeze();

        Point At(double deg) => new(
            center.X + radius * Math.Sin(deg * Math.PI / 180),
            center.Y - radius * Math.Cos(deg * Math.PI / 180));

        var figure = new PathFigure { StartPoint = At(startDeg), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(At(startDeg + sweepDeg), new Size(radius, radius), 0,
            sweepDeg > 180, SweepDirection.Clockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);
    }
}

/// <summary>
/// M3 Expressive 的波浪环形指示器：弧线沿圆周起伏，同时整圈在转。
/// 轨道也带同样的波纹，所以静止时也能看出是「波浪圆」而不是普通圆。
/// </summary>
internal sealed class M3WavyCircularProgress : FrameworkElement
{
    private const double CycleMs = 5333;

    private readonly DispatcherTimer _ticker;
    private readonly System.Diagnostics.Stopwatch _watch = new();

    private Brush _track = M3.Light.SecondaryContainer;
    private Brush _active = M3.Light.Primary;

    public M3WavyCircularProgress()
    {
        Width = 56;
        Height = 56;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _ticker.Tick += (_, _) => InvalidateVisual();

        Loaded += (_, _) => { _watch.Start(); _ticker.Start(); };
        Unloaded += (_, _) => _ticker.Stop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { _watch.Start(); _ticker.Start(); }
            else _ticker.Stop();
        };
    }

    public double Thickness { get; set; } = 4;

    /// <summary>波幅：相对圆周半径的起伏量，单位 dp。</summary>
    public double Amplitude { get; set; } = 1.4;

    /// <summary>绕一圈有几个波峰。</summary>
    public int Waves { get; set; } = 11;

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

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 12) return;

        double thickness = Math.Min(Thickness, size / 4);
        double radius = (size - thickness) / 2 - Amplitude;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        double p = (_watch.Elapsed.TotalMilliseconds % CycleMs) / CycleMs;
        double spin = p * 1440;
        double sweep = 12 + (250 - 12) * Math.Sin(Math.PI * p);
        double phase = _watch.Elapsed.TotalMilliseconds / 420.0;

        if (_track != null)
        {
            var trackPen = new Pen(_track, Math.Max(1.5, thickness - 1))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (trackPen.CanFreeze) trackPen.Freeze();
            dc.DrawGeometry(null, trackPen, BuildArc(center, radius, 0, 360, phase));
        }

        var pen = new Pen(_active, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        if (pen.CanFreeze) pen.Freeze();

        dc.DrawGeometry(null, pen, BuildArc(center, radius, spin - sweep, sweep, phase));
    }

    private Geometry BuildArc(Point center, double radius, double startDeg, double sweepDeg, double phase)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            int steps = Math.Max(24, (int)Math.Ceiling(sweepDeg / 3));
            bool begun = false;

            for (int i = 0; i <= steps; i++)
            {
                double deg = startDeg + sweepDeg * i / steps;
                double rad = deg * Math.PI / 180;
                double r = radius + Amplitude * Math.Sin(Waves * rad + phase);
                var point = new Point(center.X + r * Math.Sin(rad), center.Y - r * Math.Cos(rad));

                if (!begun)
                {
                    ctx.BeginFigure(point, false, false);
                    begun = true;
                }
                else
                {
                    ctx.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }
}

/// <summary>
/// M3 线性进度条（4dp 圆角轨道）。
/// 不确定模式下是一段会伸缩的进度块从左边跑到右边，跑完从右边出去再从左边进来。
/// </summary>
internal sealed class M3LinearProgress : FrameworkElement
{
    private const double LoopMs = 1800;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(M3LinearProgress),
        new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DispatcherTimer _ticker;
    private readonly System.Diagnostics.Stopwatch _watch = new();

    private bool _indeterminate;
    private Brush _track = M3.Light.SecondaryContainer;
    private Brush _active = M3.Light.Primary;

    public M3LinearProgress()
    {
        Height = 4;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _ticker.Tick += (_, _) => InvalidateVisual();

        Loaded += (_, _) => SyncTicker();
        Unloaded += (_, _) => _ticker.Stop();
        IsVisibleChanged += (_, _) => SyncTicker();
    }

    public double Value
    {
        get => Math.Clamp((double)GetValue(ValueProperty), 0, 1);
        set => SetValue(ValueProperty, Math.Clamp(value, 0, 1));
    }

    public bool IsIndeterminate
    {
        get => _indeterminate;
        set
        {
            if (_indeterminate == value) return;
            _indeterminate = value;
            SyncTicker();
            InvalidateVisual();
        }
    }

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

    private void SyncTicker()
    {
        if (_indeterminate && IsLoaded && IsVisible)
        {
            _watch.Start();
            _ticker.Start();
        }
        else
        {
            _ticker.Stop();
            _watch.Stop();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth, height = ActualHeight;
        if (width < 4 || height < 1) return;

        double radius = height / 2;

        if (_track != null)
        {
            dc.DrawRoundedRectangle(_track, null, new Rect(0, 0, width, height), radius, radius);
        }

        if (_indeterminate)
        {
            double p = (_watch.Elapsed.TotalMilliseconds % LoopMs) / LoopMs;
            double w = width * (0.25 + 0.35 * Math.Sin(Math.PI * p));  // 25%–60%
            double left = -w + p * (width + w);                          // 从左侧出界跑到右侧出界

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
            dc.DrawRoundedRectangle(_active, null, new Rect(left, 0, w, height), radius, radius);
            dc.Pop();
            return;
        }

        double active = width * Value;
        if (active < 0.5) return;
        dc.DrawRoundedRectangle(_active, null, new Rect(0, 0, active, height), radius, radius);
    }
}

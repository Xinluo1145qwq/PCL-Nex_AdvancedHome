using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 时钟组件。两种指针表盘共用一套自绘内核：
///   · marks  —— 超大刻度数字压在盘边（刻度出血）+ 粗圆头时针 + 细秒针；
///   · cookie —— 扇贝形「饼干」盘、顶部斜排日期、两根短粗指针 + 一个小圆点；
/// 另外可以切成电子钟。尺寸变化只重排文字，不重建视觉树。
/// </summary>
internal sealed class ClockView : Grid
{
    private readonly ClockFace _face = new();
    private readonly Border _panel = new();
    private readonly StackPanel _stack = new();
    private readonly TextBlock _time = new();
    private readonly TextBlock _date = new();
    private readonly DispatcherTimer _timer;

    private string _style = "marks";
    private bool _showDate = true;
    private bool _showSeconds;
    private string _lastTime = "";
    private string _lastDate = "";

    public ClockView()
    {
        Children.Add(_face);

        _stack.Orientation = Orientation.Vertical;
        _stack.HorizontalAlignment = HorizontalAlignment.Center;
        _stack.VerticalAlignment = VerticalAlignment.Center;
        _stack.Children.Add(_time);
        _stack.Children.Add(_date);

        _panel.Child = _stack;
        _panel.HorizontalAlignment = HorizontalAlignment.Stretch;
        _panel.VerticalAlignment = VerticalAlignment.Stretch;
        _panel.Margin = new Thickness(6);
        _panel.Visibility = Visibility.Collapsed;
        Children.Add(_panel);

        SizeChanged += (_, e) => ApplyScale(e.NewSize.Width, e.NewSize.Height);

        // 首屏时 OnShown 不一定被调用，样式得自己读
        Refresh();

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateTime(DateTime.Now);
        Loaded += (_, _) => { UpdateTime(DateTime.Now); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    /// <summary>
    /// 两种样式都吃满卡片：电子钟的内容只有一小条，如果按内容量高度，
    /// 卡片会被「顶端对齐」的宿主压成一条横条。
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height;
        var bounded = new Size(w, h);
        base.MeasureOverride(bounded);
        return bounded;
    }

    /// <summary>配置变了：切版式并立刻刷新一次。</summary>
    public void Refresh()
    {
        var options = Cfg.Clock;
        string style = (options.Style ?? "marks").ToLowerInvariant();
        _style = style == "digital" ? "digital" : style == "cookie" ? "cookie" : "marks";
        _showDate = options.ShowDate;
        _showSeconds = options.ShowSeconds;

        bool analog = _style != "digital";
        _face.Visibility = analog ? Visibility.Visible : Visibility.Collapsed;
        _panel.Visibility = analog ? Visibility.Collapsed : Visibility.Visible;

        _face.Style = _style;
        _face.ShowDate = _showDate;
        _face.ShowSeconds = _showSeconds;

        PaintDigital();
        ApplyScale(ActualWidth, ActualHeight);

        _lastTime = "";
        _lastDate = "";
        UpdateTime(DateTime.Now);
    }

    public void ApplyTheme()
    {
        PaintDigital();
        _face.Invalidate();
    }

    private void PaintDigital()
    {
        _panel.Background = M3.C.InverseSurface;

        _time.Foreground = M3.C.InversePrimary;
        _time.FontWeight = FontWeights.Bold;
        _time.FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
        _time.HorizontalAlignment = HorizontalAlignment.Center;

        _date.Foreground = M3.Alpha(M3.C.InverseOnSurface, 0.72);
        _date.FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
        _date.HorizontalAlignment = HorizontalAlignment.Center;
        _date.Visibility = _showDate ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>只调字号和内距，不碰视觉树。</summary>
    private void ApplyScale(double width, double height)
    {
        if (width <= 0 || height <= 0) return;

        _panel.CornerRadius = new CornerRadius(Math.Min(M3.CornerXl, Math.Min(width, height) * 0.14));
        _panel.Padding = new Thickness(0);

        // 等宽数字按位数收字号：HH:mm:ss 和 HH:mm 都得塞得下
        int length = Math.Max(4, _time.Text?.Length ?? 5);
        double byWidth = (width - 40) / (length * 0.60);
        double byHeight = height * 0.30;
        _time.FontSize = Math.Clamp(Math.Min(byWidth, byHeight), 14, 110);

        _date.FontSize = Math.Clamp(height * 0.075, 10, 17);
        _date.Margin = new Thickness(0, height * 0.018, 0, 0);
    }

    private void UpdateTime(DateTime now)
    {
        _face.Now = now;

        if (_style == "digital")
        {
            string text = _showSeconds ? now.ToString("HH:mm:ss", Zh) : now.ToString("HH:mm", Zh);
            if (text != _lastTime)
            {
                _lastTime = text;
                _time.Text = text;
                ApplyScale(ActualWidth, ActualHeight);
            }

            string date = now.ToString("M月d日", Zh) + " " + M3ClockText.Weekday(now.DayOfWeek);
            if (date != _lastDate)
            {
                _lastDate = date;
                _date.Text = date;
            }
        }
    }

    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");
}

/// <summary>星期文案，指针表盘和电子钟共用。</summary>
internal static class M3ClockText
{
    public static string Weekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日"
    };
}

/// <summary>
/// 表盘的全部绘制。两种指针样式各自画自己的「盘」，刻度/日期的 FormattedText
/// 只在尺寸或开关变化时重建，秒针每秒只重画线条。
/// </summary>
internal sealed class ClockFace : FrameworkElement
{
    private static readonly Typeface DigitType = new(
        new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

    private static readonly Typeface LabelType = new(
        new FontFamily("Microsoft YaHei UI, Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

    private static readonly string[] Marks = { "12", "3", "6", "9" };
    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");

    private readonly FormattedText[] _marks = new FormattedText[4];
    private readonly Point[] _markAt = new Point[4];

    private DateTime _now = DateTime.Now;
    private string _style = "marks";
    private bool _showDate = true;
    private bool _showSeconds = true;

    private FormattedText? _stamp;
    private Point _stampAt;
    private double _stampSize = 12;

    private Point _center;
    private double _radius;
    private double _width;
    private double _height;
    private bool _dirty = true;
    // 小卡片（2×1 这种）：指针要缩短、日期要下移，否则分针会从日期文字中间穿过去
    private bool _compact;

    public DateTime Now
    {
        get => _now;
        set
        {
            if (value == _now) return;
            // 跨天时日期文案要重排
            if (value.Date != _now.Date) _dirty = true;
            _now = value;
            InvalidateVisual();
        }
    }

    /// <summary>marks / cookie（digital 时整个元素是隐藏的）。</summary>
    public string Style
    {
        get => _style;
        set
        {
            if (string.Equals(_style, value, StringComparison.Ordinal)) return;
            _style = value;
            _dirty = true;
            InvalidateVisual();
        }
    }

    public bool ShowDate
    {
        get => _showDate;
        set
        {
            if (_showDate == value) return;
            _showDate = value;
            _dirty = true;
            InvalidateVisual();
        }
    }

    public bool ShowSeconds
    {
        get => _showSeconds;
        set
        {
            if (_showSeconds == value) return;
            _showSeconds = value;
            InvalidateVisual();
        }
    }

    /// <summary>主题色变了：颜色是每次绘制现取的，重画一遍就够。</summary>
    public void Invalidate()
    {
        _dirty = true;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // 自绘元素没有子元素，默认量出来是 0——卡片会跟着塌成一条线。
        // 这里直接吃满卡片给的格子。
        double w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height;
        return new Size(w, h);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        _dirty = true;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth, h = ActualHeight;
        if (w < 24 || h < 24) return;

        if (string.Equals(_style, "cookie", StringComparison.Ordinal)) DrawCookie(dc, w, h);
        else DrawMarks(dc, w, h);
    }

    // ─────────────────────────── 刻度表盘 ───────────────────────────

    private void DrawMarks(DrawingContext dc, double w, double h)
    {
        EnsureLayout(w, h);

        var faceBrush = new LinearGradientBrush(
            M3.Of(M3.C.PrimaryContainer),
            M3.Of(M3.Layer(M3.Of(M3.C.PrimaryContainer), M3.Of(M3.C.Primary), 0.07)),
            new Point(0, 0), new Point(1, 1));
        faceBrush.Freeze();

        dc.DrawEllipse(faceBrush, null, _center, _radius, _radius);

        // 刻度数字：压在圆盘边缘，超出的部分被裁掉
        var clip = new EllipseGeometry(_center, _radius, _radius);
        dc.PushClip(clip);
        for (int i = 0; i < 4; i++)
        {
            var mark = _marks[i];
            if (mark == null) continue;
            dc.DrawText(mark, new Point(_markAt[i].X - mark.Width / 2, _markAt[i].Y - mark.Baseline + MarkSize * 0.34));
        }

        dc.Pop();

        DrawHands(dc);

        if (_stamp != null)
        {
            dc.DrawText(_stamp, new Point(_stampAt.X - _stamp.Width / 2, _stampAt.Y - _stamp.Baseline + _stampSize * 0.34));
        }
    }

    // 小卡上刻度再收一点，否则 12 会被圆盘切掉大半个字，看着像渲染错位
    private double MarkSize => _radius * (_compact ? 0.46 : 0.56);
    private double StampSize => Math.Clamp(_height * 0.050, 9.5, 16);

    private void DrawHands(DrawingContext dc)
    {
        double hour = (_now.Hour % 12 + _now.Minute / 60.0) * 30;
        double minute = (_now.Minute + _now.Second / 60.0) * 6;

        // 时针：粗圆头，压在最下面
        DrawHand(dc, hour, _radius * (_compact ? 0.38 : 0.52), Math.Max(4, _radius * 0.135), M3.C.Primary);
        // 分针：细一些，压在时针上
        DrawHand(dc, minute, _radius * (_compact ? 0.52 : 0.77), Math.Max(1.8, _radius * 0.046), M3.C.OnPrimaryContainer);

        if (_showSeconds)
        {
            double second = _now.Second * 6;
            double length = _radius * (_compact ? 0.58 : 0.84);
            DrawHand(dc, second, length, Math.Max(1, _radius * 0.014), M3.C.Tertiary, tail: _radius * 0.14);

            var tip = PointOn(second, length);
            dc.DrawEllipse(M3.C.Tertiary, null, tip, Math.Max(1.6, _radius * 0.026), Math.Max(1.6, _radius * 0.026));
        }

        // 轴心：深色圆点 + 一点高光
        dc.DrawEllipse(M3.C.OnPrimaryContainer, null, _center, _radius * 0.044, _radius * 0.044);
        dc.DrawEllipse(M3.C.PrimaryContainer, null, _center, _radius * 0.016, _radius * 0.016);
    }

    private void DrawHand(DrawingContext dc, double degrees, double length, double thickness, Brush brush, double tail = 0)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();

        dc.DrawLine(pen, tail > 0 ? PointOn(degrees, -tail) : _center, PointOn(degrees, length));
    }

    private Point PointOn(double degrees, double distance)
    {
        double rad = (degrees - 90) * Math.PI / 180;
        return new Point(_center.X + Math.Cos(rad) * distance, _center.Y + Math.Sin(rad) * distance);
    }

    private void EnsureLayout(double w, double h)
    {
        if (!_dirty && Math.Abs(w - _width) < 0.3 && Math.Abs(h - _height) < 0.3) return;

        _dirty = false;
        _width = w;
        _height = h;

        // 表盘吃满卡片，和天气圆盘同直径（两张卡看起来才一样大）
        double diameter = Math.Max(16, Math.Min(w, h) - 2);
        _radius = diameter / 2;
        _center = new Point(w / 2, h / 2);
        // 半径 56 以下（约 2×1 的卡）就是「小卡」：刻度也小，指针必须让开日期
        _compact = _radius < 56;

        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var markBrush = M3.Alpha(M3.C.OnPrimaryContainer, Palette.IsDark ? 0.36 : 0.42);
        double size = MarkSize;

        for (int i = 0; i < 4; i++)
        {
            // 6 点那格让给日期（和腕表的日历窗一个位置），关了日期再把 6 补回来
            bool takenByDate = _showDate && i == 2;
            _marks[i] = takenByDate
                ? null
                : new FormattedText(Marks[i], Zh, FlowDirection.LeftToRight, DigitType, size, markBrush, ppd);
        }

        _markAt[0] = new Point(_center.X, _center.Y - _radius * 0.57);
        _markAt[1] = new Point(_center.X + _radius * 0.55, _center.Y);
        _markAt[2] = new Point(_center.X, _center.Y + _radius * 0.57);
        _markAt[3] = new Point(_center.X - _radius * 0.55, _center.Y);

        if (_showDate)
        {
            string text = _now.ToString("M月d日", Zh) + " " + M3ClockText.Weekday(_now.DayOfWeek);
            _stampSize = _compact ? Math.Clamp(_height * 0.048, 8.5, 11) : StampSize;
            // 小卡上日期挪到更靠边的一格，给缩短后的指针让出位置
            double distance = _compact ? 0.76 : 0.57;
            _stamp = Fit(text, LabelType, _stampSize, _radius * 1.42, M3.Alpha(M3.C.OnPrimaryContainer, 0.72), ppd);
            _stampAt = new Point(_center.X, _center.Y + _radius * distance);
        }
        else
        {
            _stamp = null;
        }
    }

    // ─────────────────────────── 饼干表盘 ───────────────────────────

    private void DrawCookie(DrawingContext dc, double w, double h)
    {
        double s = Math.Min(w, h);
        var center = new Point(w / 2, h / 2);
        double r = s / 2 - 1;

        bool dark = Palette.IsDark;
        var fill = M3.Tint(M3.C.Tertiary, dark ? 34 : 74);
        var ink = M3.Tint(M3.C.Tertiary, dark ? 95 : 16);
        var pale = M3.Tint(M3.C.Tertiary, dark ? 78 : 97);
        var dot = M3.Tint(M3.C.Tertiary, dark ? 66 : 91);

        dc.DrawGeometry(fill, null, Scallop(center, r));

        // 日期：右上斜排，跟着扇贝的走势
        if (_showDate)
        {
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double size = Math.Clamp(s * 0.105, 12, 32);
            string text = M3ClockText.Weekday(_now.DayOfWeek) + " " + _now.Day + "日";
            var stamp = Fit(text, LabelType, size, r * 1.35, ink, ppd);

            double x = center.X + r * 0.15;
            double y = center.Y - r * 0.44;
            dc.PushTransform(new RotateTransform(10, x, y));
            dc.DrawText(stamp, new Point(x - stamp.Width / 2, y - stamp.Baseline + size * 0.34));
            dc.Pop();
        }

        double hour = (_now.Hour % 12 + _now.Minute / 60.0) * 30;
        double minute = _now.Minute * 6;

        // 两根短粗指针：分针（深）在下，时针（浅）压在上面，和参考图一样在中心交叠
        CookieHand(dc, center, minute, r * 0.46, r * 0.13, ink);
        CookieHand(dc, center, hour, r * 0.38, r * 0.155, pale);

        // 底部的小圆点
        dc.DrawEllipse(dot, null, new Point(center.X, center.Y + r * 0.70), r * 0.055, r * 0.055);
    }

    private static void CookieHand(DrawingContext dc, Point center, double degrees, double length, double thickness, Brush brush)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();

        double rad = (degrees - 90) * Math.PI / 180;
        var tip = new Point(center.X + Math.Cos(rad) * length, center.Y + Math.Sin(rad) * length);
        // 内端缩回中心一点点，两根指针交叠处更厚，像参考图
        var tail = new Point(center.X - Math.Cos(rad) * length * 0.10, center.Y - Math.Sin(rad) * length * 0.10);
        dc.DrawLine(pen, tail, tip);
    }

    /// <summary>
    /// 扇贝形轮廓：每个花瓣用一段二次贝塞尔向外鼓，相邻花瓣在「谷」处形成尖角——
    /// 直接用正弦波会鼓成齿轮，贝塞尔才有参考图那种「圆瓣 + 窄缺口」。
    /// </summary>
    private static Geometry Scallop(Point center, double radius)
    {
        const int lobes = 10;
        const double valley = 0.87;
        double step = 2 * Math.PI / lobes;
        double control = 2 - valley;   // 让贝塞尔中点正好落在 radius 上

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < lobes; i++)
            {
                double a0 = i * step;
                double a1 = (i + 1) * step;
                double am = a0 + step / 2;

                var start = Polar(center, a0, radius * valley);
                var end = Polar(center, a1, radius * valley);
                var pull = Polar(center, am, radius * control);

                if (i == 0) ctx.BeginFigure(start, true, true);
                else ctx.LineTo(start, true, false);
                ctx.QuadraticBezierTo(pull, end, true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point Polar(Point center, double angle, double radius) =>
        new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);

    // ─────────────────────────── 小工具 ───────────────────────────

    private static FormattedText Fit(string text, Typeface type, double size, double maxWidth, Brush brush, double ppd)
    {
        var formatted = new FormattedText(text, Zh, FlowDirection.LeftToRight, type, size, brush, ppd);
        if (formatted.Width <= maxWidth || formatted.Width <= 0) return formatted;

        return new FormattedText(text, Zh, FlowDirection.LeftToRight, type, size * (maxWidth / formatted.Width), brush, ppd);
    }
}

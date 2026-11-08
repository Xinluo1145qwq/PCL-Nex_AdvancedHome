using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 把 M3 的 easing 曲线做成 EasingFunction：cubic-bezier 用二分法解 x→t，
/// 再取 y。这样 DoubleAnimation 也能用官方曲线，不必绕成 KeyFrame。
/// </summary>
internal sealed class M3BezierEase : EasingFunctionBase
{
    private readonly double _x1, _y1, _x2, _y2;

    public M3BezierEase(double x1, double y1, double x2, double y2)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
    }

    /// <summary>M3 standard：cubic-bezier(0.2, 0, 0, 1)</summary>
    public static M3BezierEase Standard() => new(0.2, 0, 0, 1);

    /// <summary>M3 decelerate：cubic-bezier(0, 0, 0, 1)</summary>
    public static M3BezierEase Decelerate() => new(0, 0, 0, 1);

    /// <summary>M3 accelerate：cubic-bezier(0.3, 0, 1, 1)</summary>
    public static M3BezierEase Accelerate() => new(0.3, 0, 1, 1);

    /// <summary>M3 emphasized decelerate：cubic-bezier(0.05, 0.7, 0.1, 1)</summary>
    public static M3BezierEase Emphasized() => new(0.05, 0.7, 0.1, 1);

    protected override double EaseInCore(double normalizedTime)
    {
        double t = Math.Clamp(normalizedTime, 0, 1);
        double lo = 0, hi = 1, u = t;

        for (int i = 0; i < 10; i++)
        {
            double x = Curve(u, _x1, _x2);
            if (Math.Abs(x - t) < 0.0005) break;
            if (x < t) lo = u;
            else hi = u;
            u = (lo + hi) / 2;
        }

        return Curve(u, _y1, _y2);
    }

    /// <summary>一维三次贝塞尔（P0=0, P3=1）。</summary>
    private static double Curve(double u, double a, double b)
    {
        double v = 1 - u;
        return 3 * v * v * u * a + 3 * v * u * u * b + u * u * u;
    }

    protected override Freezable CreateInstanceCore() => new M3BezierEase(_x1, _y1, _x2, _y2);
}

/// <summary>四条 M3 官方缓动曲线的对照：点「播放」看同一个位移用不同曲线跑到终点。</summary>
internal sealed class M3EasingDemo : StackPanel
{
    private const double TrackWidth = 300;
    private const double DotSize = 14;
    private const double RunMs = 620;

    private readonly List<(TranslateTransform T, double Distance)> _dots = new();

    public M3EasingDemo(M3.Scheme scheme, double scale = 1)
    {
        var lanes = new (string Name, string Hint, M3BezierEase Ease)[]
        {
            ("Standard", "cubic-bezier(.2,0,0,1)", M3BezierEase.Standard()),
            ("Decelerate", "cubic-bezier(0,0,0,1)", M3BezierEase.Decelerate()),
            ("Accelerate", "cubic-bezier(.3,0,1,1)", M3BezierEase.Accelerate()),
            ("Emphasized", "cubic-bezier(.05,.7,.1,1)", M3BezierEase.Emphasized())
        };

        foreach (var lane in lanes)
        {
            var row = new Grid { Height = 30 * scale, Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96 * scale) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = lane.Name,
                FontSize = 12 * scale,
                Foreground = scheme.OnSurfaceVariant,
                VerticalAlignment = VerticalAlignment.Center
            });

            var track = new Grid
            {
                Width = TrackWidth * scale,
                Height = 26 * scale,
                VerticalAlignment = VerticalAlignment.Center
            };

            track.Children.Add(new Border
            {
                Height = 2,
                CornerRadius = new CornerRadius(1),
                Background = scheme.SecondaryContainer,
                VerticalAlignment = VerticalAlignment.Center
            });

            var translate = new TranslateTransform(0, 0);
            var dot = new Border
            {
                Width = DotSize * scale,
                Height = DotSize * scale,
                CornerRadius = new CornerRadius(DotSize * scale / 2),
                Background = scheme.Primary,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = translate,
                IsHitTestVisible = false
            };
            track.Children.Add(dot);

            Grid.SetColumn(track, 1);
            row.Children.Add(track);

            var hint = new TextBlock
            {
                Text = lane.Hint,
                FontSize = 10.5 * scale,
                FontFamily = new FontFamily("Consolas"),
                Foreground = scheme.Outline,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14 * scale, 0, 0, 0)
            };
            Grid.SetColumn(hint, 2);
            row.Children.Add(hint);

            _dots.Add((translate, TrackWidth * scale - DotSize * scale));
            Children.Add(row);
        }

        var play = M3Button.Create("播放", Play, M3ButtonKind.Tonal, scale: 0.9, scheme: scheme);
        play.HorizontalAlignment = HorizontalAlignment.Left;
        play.Margin = new Thickness(0, 10 * scale, 0, 0);
        Children.Add(play);

        Loaded += (_, _) => Play();
    }

    public void Play()
    {
        var lanes = new[]
        {
            M3BezierEase.Standard(), M3BezierEase.Decelerate(),
            M3BezierEase.Accelerate(), M3BezierEase.Emphasized()
        };

        for (int i = 0; i < _dots.Count && i < lanes.Length; i++)
        {
            var (translate, distance) = _dots[i];
            var animation = new DoubleAnimation(0, distance, TimeSpan.FromMilliseconds(RunMs))
            {
                EasingFunction = lanes[i]
            };
            translate.BeginAnimation(TranslateTransform.XProperty, animation);
        }
    }
}

/// <summary>
/// Container transform：点卡片从小卡长成大卡，圆角同时从 12dp 变成 16dp。
/// 这就是 M3 里「卡片 → 详情页」的简化版；尺寸用 emphasized 曲线，别用弹簧，否则会抖。
/// </summary>
internal sealed class M3ContainerTransformDemo : Grid
{
    private const double ClosedW = 232;
    private const double ClosedH = 92;
    private const double OpenW = 392;
    private const double OpenH = 196;

    private readonly Grid _host;
    private readonly M3ShapeFill _fill;
    private readonly StackPanel _detail;
    private readonly TextBlock _hint;
    private readonly double _scale;
    private bool _open;

    public M3ContainerTransformDemo(M3.Scheme scheme, double scale = 1)
    {
        _scale = scale;
        Height = 214 * scale;

        _fill = new M3ShapeFill { Radius = M3.CornerM, Fill = scheme.PrimaryContainer };

        var title = new TextBlock
        {
            Text = "生存存档",
            FontSize = 15 * scale,
            FontWeight = FontWeights.SemiBold,
            Foreground = scheme.OnPrimaryContainer
        };

        var sub = new TextBlock
        {
            Text = "上次游玩 3 天前",
            FontSize = 11.5 * scale,
            Margin = new Thickness(0, 3 * scale, 0, 0),
            Foreground = scheme.OnPrimaryContainer,
            Opacity = 0.72
        };

        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(title);
        titles.Children.Add(sub);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var avatar = new Border
        {
            Width = 40 * scale,
            Height = 40 * scale,
            CornerRadius = new CornerRadius(20 * scale),
            Background = scheme.Primary,
            Child = M3.Icon("M4,15 L9,9 L13,13 L16,10 L20,15 M4,19 H20", scheme.OnPrimary, 20),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12 * scale, 0)
        };
        header.Children.Add(avatar);
        Grid.SetColumn(titles, 1);
        header.Children.Add(titles);

        _detail = new StackPanel
        {
            Opacity = 0,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 16 * scale, 0, 0)
        };
        _detail.Children.Add(new TextBlock
        {
            Text = "1.20.1 · Forge 47.2.0\n128 个模组 · 4.6 GB",
            FontSize = 12 * scale,
            LineHeight = 19 * scale,
            Foreground = scheme.OnPrimaryContainer,
            TextWrapping = TextWrapping.Wrap
        });

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(_detail);

        var content = new Border
        {
            Padding = new Thickness(16 * scale, 14 * scale, 16 * scale, 14 * scale),
            Child = body
        };

        _host = new Grid
        {
            Width = ClosedW * scale,
            Height = ClosedH * scale,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true,
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent
        };
        _host.Children.Add(_fill);
        _host.Children.Add(content);
        _host.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Toggle();
        };
        Children.Add(_host);

        _hint = new TextBlock
        {
            Text = "点卡片展开",
            FontSize = 11.5 * scale,
            Foreground = scheme.OnSurfaceVariant,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(2 * scale, 0, 0, 0)
        };
        Children.Add(_hint);
    }

    private void Toggle()
    {
        _open = !_open;
        var ease = M3BezierEase.Emphasized();
        var duration = TimeSpan.FromMilliseconds(450);

        _host.BeginAnimation(WidthProperty,
            new DoubleAnimation(_open ? OpenW * _scale : ClosedW * _scale, duration) { EasingFunction = M3BezierEase.Emphasized() });
        _host.BeginAnimation(HeightProperty,
            new DoubleAnimation(_open ? OpenH * _scale : ClosedH * _scale, duration) { EasingFunction = M3BezierEase.Emphasized() });

        _fill.AnimateRadius(_open ? M3.CornerL : M3.CornerM, duration, ease);

        if (_open)
        {
            _detail.Visibility = Visibility.Visible;
            _detail.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260))
            {
                BeginTime = TimeSpan.FromMilliseconds(120),
                EasingFunction = M3BezierEase.Decelerate()
            });
        }
        else
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = M3BezierEase.Accelerate()
            };
            fade.Completed += (_, _) =>
            {
                if (!_open) _detail.Visibility = Visibility.Collapsed;
            };
            _detail.BeginAnimation(OpacityProperty, fade);
        }

        _hint.Text = _open ? "点卡片收起" : "点卡片展开";
    }
}

/// <summary>
/// Shared axis：同层级页面切换——旧页加速往左退场，新页减速从右边进来。
/// 进和出用不同曲线，是这个转场「不晕」的关键。
/// </summary>
internal sealed class M3SharedAxisDemo : Border
{
    private readonly Border[] _pages = new Border[2];
    private readonly TranslateTransform[] _moves = new TranslateTransform[2];
    private int _index;
    private bool _busy;

    public M3SharedAxisDemo(M3.Scheme scheme, double scale = 1)
    {
        Width = 420 * scale;
        Background = scheme.SurfaceLow;
        CornerRadius = new CornerRadius(M3.CornerM);
        Padding = new Thickness(16 * scale, 12 * scale, 16 * scale, 12 * scale);

        var stage = new Grid { Height = 86 * scale, ClipToBounds = true };

        for (int i = 0; i < 2; i++)
        {
            var move = new TranslateTransform(0, 0);
            var page = new Border
            {
                Opacity = i == 0 ? 1 : 0,
                Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed,
                RenderTransform = move,
                Child = i == 0 ? PageOne(scheme, scale) : PageTwo(scheme, scale)
            };

            _pages[i] = page;
            _moves[i] = move;
            stage.Children.Add(page);
        }

        var nav = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8 * scale, 0, 0)
        };

        var prev = M3Button.Create("上一页", () => Go(-1), M3ButtonKind.Text, scale: 0.85, scheme: scheme);
        var next = M3Button.Create("下一页", () => Go(1), M3ButtonKind.Text, scale: 0.85, scheme: scheme);
        next.Margin = new Thickness(4 * scale, 0, 0, 0);
        nav.Children.Add(prev);
        nav.Children.Add(next);

        var stack = new StackPanel();
        stack.Children.Add(stage);
        stack.Children.Add(nav);
        Child = stack;
    }

    private static FrameworkElement PageOne(M3.Scheme scheme, double scale)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "概览",
            FontSize = 15 * scale,
            FontWeight = FontWeights.SemiBold,
            Foreground = scheme.OnSurface
        });
        stack.Children.Add(new TextBlock
        {
            Text = "同层级页面切换用 shared axis：\n横向滑动，方向表示前后关系。",
            FontSize = 12 * scale,
            LineHeight = 19 * scale,
            Margin = new Thickness(0, 6 * scale, 0, 0),
            Foreground = scheme.OnSurfaceVariant,
            TextWrapping = TextWrapping.Wrap
        });
        return stack;
    }

    private static FrameworkElement PageTwo(M3.Scheme scheme, double scale)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "实例",
            FontSize = 15 * scale,
            FontWeight = FontWeights.SemiBold,
            Foreground = scheme.OnSurface
        });
        stack.Children.Add(new TextBlock
        {
            Text = "退场 180ms 加速曲线，进场 300ms 减速曲线，\n两段叠起来就不会有突然的顿挫。",
            FontSize = 12 * scale,
            LineHeight = 19 * scale,
            Margin = new Thickness(0, 6 * scale, 0, 0),
            Foreground = scheme.OnSurfaceVariant,
            TextWrapping = TextWrapping.Wrap
        });
        return stack;
    }

    private void Go(int dir)
    {
        if (_busy) return;

        int target = _index + dir;
        if (target < 0 || target >= _pages.Length) return;

        _busy = true;
        var outgoing = _pages[_index];
        var incoming = _pages[target];
        var outMove = _moves[_index];
        var inMove = _moves[target];

        incoming.Visibility = Visibility.Visible;
        incoming.Opacity = 0;
        inMove.BeginAnimation(TranslateTransform.XProperty, null);
        inMove.X = 42 * dir;

        const double outMs = 180, inMs = 300;

        outMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-42 * dir,
            TimeSpan.FromMilliseconds(outMs)) { EasingFunction = M3BezierEase.Accelerate() });
        outgoing.BeginAnimation(OpacityProperty, new DoubleAnimation(0,
            TimeSpan.FromMilliseconds(outMs)) { EasingFunction = M3BezierEase.Accelerate() });

        inMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0,
            TimeSpan.FromMilliseconds(inMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(60),
            EasingFunction = M3BezierEase.Decelerate()
        });
        incoming.BeginAnimation(OpacityProperty, new DoubleAnimation(1,
            TimeSpan.FromMilliseconds(inMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(60),
            EasingFunction = M3BezierEase.Decelerate()
        });

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(inMs + 80)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            outgoing.Visibility = Visibility.Collapsed;
            outMove.X = 0;
            _index = target;
            _busy = false;
        };
        timer.Start();
    }
}

/// <summary>交错入场：一组条目按 60ms 间隔依次淡入上移。</summary>
internal sealed class M3StaggerDemo : StackPanel
{
    private readonly List<FrameworkElement> _rows = new();

    public M3StaggerDemo(M3.Scheme scheme, double scale = 1)
    {
        string[] labels = { "问候组件", "日历", "最近游玩", "时钟" };

        foreach (string label in labels)
        {
            var row = new Border
            {
                Height = 34 * scale,
                Margin = new Thickness(0, 0, 0, 6 * scale),
                CornerRadius = new CornerRadius(M3.CornerS),
                Background = scheme.SurfaceHigh,
                Padding = new Thickness(14 * scale, 0, 14 * scale, 0),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 12.5 * scale,
                    Foreground = scheme.OnSurface,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            row.RenderTransform = new TranslateTransform(0, 0);
            _rows.Add(row);
            Children.Add(row);
        }

        var play = M3Button.Create("重播", Play, M3ButtonKind.Tonal, scale: 0.85, scheme: scheme);
        play.HorizontalAlignment = HorizontalAlignment.Left;
        play.Margin = new Thickness(0, 4 * scale, 0, 0);
        Children.Add(play);

        Loaded += (_, _) => Play();
    }

    public void Play()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var move = (TranslateTransform)row.RenderTransform;
            var delay = TimeSpan.FromMilliseconds(i * 70);

            row.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                BeginTime = delay,
                EasingFunction = M3BezierEase.Decelerate()
            });

            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0,
                TimeSpan.FromMilliseconds(260))
            {
                BeginTime = delay,
                EasingFunction = M3BezierEase.Decelerate()
            });
        }
    }
}

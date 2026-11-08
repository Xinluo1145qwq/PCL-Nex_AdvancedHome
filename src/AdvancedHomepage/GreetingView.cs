using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 问候组件：上面一行日期（11月1日星期日），下面一句问候。
/// 背景透明，直接浮在壁纸上；文字可以是按时段自动生成的，也可以是用户自定义的轮换标语。
/// </summary>
internal sealed class GreetingView : Grid
{
    private const double RefWidth = 315.0;
    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");

    private readonly TextBlock _date = new();
    private readonly TextBlock _text = new();
    private readonly StackPanel _root = new();
    private readonly DispatcherTimer _timer;

    private double _width = RefWidth;
    private bool _active;
    private int _tick;
    private int _index = -1;
    private bool _showingAuto;
    private int _lastHour = -1;

    public GreetingView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        ClipToBounds = false;

        _date.Foreground = Palette.TextSub;
        _date.Opacity = 0.85;
        _date.FontWeight = FontWeights.Normal;

        _text.Foreground = Palette.TextPrimary;
        _text.FontWeight = FontWeights.Bold;
        _text.TextWrapping = TextWrapping.Wrap;
        _text.Margin = new Thickness(0, 6, 0, 0);

        _root.Orientation = Orientation.Vertical;
        _root.HorizontalAlignment = HorizontalAlignment.Left;
        _root.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_root);
        _root.Children.Add(_date);
        _root.Children.Add(_text);

        ApplyScale(RefWidth);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = _width;
        if (Math.Abs(w - _width) > 0.4) ApplyScale(w);

        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * 0.5;

        var size = new Size(w, h);
        base.MeasureOverride(size);
        return size;
    }

    /// <summary>字号跟着卡片宽度轻微缩放：窄了不至于挤，宽了也不至于夸张。</summary>
    private void ApplyScale(double width)
    {
        _width = width;
        double scale = Math.Clamp(width / RefWidth, 0.85, 1.55);
        int baseSize = Math.Clamp(Cfg.Greeting.FontSize, 12, 48);

        _date.FontSize = Math.Max(10, baseSize * 0.52) * scale;
        _text.FontSize = baseSize * scale;
        _root.Margin = new Thickness(16 * scale, 12 * scale, 16 * scale, 12 * scale);
        _text.Margin = new Thickness(0, 5 * scale, 0, 0);

        ApplyColor();
    }

    /// <summary>
    /// 文字颜色：跟随主题 / 白色 / 黑色。
    /// 壁纸偏亮时白字会糊，偏暗时黑字会糊，所以给一个手动开关，日期行用同色降透明度。
    /// </summary>
    private void ApplyColor()
    {
        switch ((Cfg.Greeting.Color ?? "auto").ToLowerInvariant())
        {
            case "white":
                _text.Foreground = Palette.Solid("#FFFFFF");
                _date.Foreground = Palette.Solid("#E6FFFFFF");
                _date.Opacity = 1;
                break;

            case "black":
                _text.Foreground = Palette.Solid("#121212");
                _date.Foreground = Palette.Solid("#C4121212");
                _date.Opacity = 1;
                break;

            default:
                _text.Foreground = Palette.TextPrimary;
                _date.Foreground = Palette.TextSub;
                _date.Opacity = 0.85;
                break;
        }
    }

    /// <summary>组件显示/隐藏时调用：显示时开始轮换，隐藏时停表。</summary>
    public void SetActive(bool active)
    {
        if (_active == active)
        {
            if (active) Refresh();
            return;
        }

        _active = active;
        if (active)
        {
            Refresh();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _text.BeginAnimation(OpacityProperty, null);
            _text.Opacity = 1;
        }
    }

    /// <summary>配置改动后重新读取（切换显示方式、改字号、改标语）。</summary>
    public void Refresh()
    {
        ApplyScale(_width);
        RefreshDate();
        _lastHour = DateTime.Now.Hour;
        _showingAuto = false;
        _tick = 0;
        ShowNext(animate: false);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        RefreshDate();

        var now = DateTime.Now;
        if (now.Hour != _lastHour)
        {
            _lastHour = now.Hour;
            if (Cfg.GreetingMode != "text") ShowNext(animate: true);
        }

        int interval = Math.Clamp(Cfg.Greeting.Interval, 3, 3600);
        _tick++;
        if (_tick < interval) return;

        _tick = 0;
        ShowNext(animate: true);
    }

    private void RefreshDate()
    {
        var text = DateTime.Now.ToString("M月d日dddd", Zh);
        if (!string.Equals(_date.Text, text, StringComparison.Ordinal)) _date.Text = text;
    }

    private void ShowNext(bool animate)
    {
        var mode = Cfg.GreetingMode;
        string text;

        if (mode == "auto")
        {
            text = Cfg.AutoGreeting(DateTime.Now);
        }
        else if (mode == "mix")
        {
            // 自定义文字与自动问候交替：先看自定义，再看自动
            _showingAuto = !_showingAuto;
            text = _showingAuto ? Cfg.AutoGreeting(DateTime.Now) : PickCustom();
        }
        else
        {
            text = PickCustom();
        }

        FitTextSize(text);

        if (!animate)
        {
            _text.BeginAnimation(OpacityProperty, null);
            _text.Text = text;
            _text.Opacity = 1;
            return;
        }

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            _text.Text = text;
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            _text.BeginAnimation(OpacityProperty, fadeIn);
        };
        _text.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// 卡片被拖窄之后，固定字号会把一句问候折成两行、正好压到壁纸上很难看。
    /// 这里按可用宽度自动缩字号，优先保证一句话在同一行里。
    /// </summary>
    private void FitTextSize(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        double scale = Math.Clamp(_width / RefWidth, 0.85, 1.55);
        double size = Math.Clamp(Cfg.Greeting.FontSize, 12, 48) * scale;
        double avail = Math.Max(80, _width - 36 * scale);

        double dpi = 1.0;
        try
        {
            dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }
        catch
        {
            // 还没挂到可视化树上，用默认 DPI
        }

        var typeface = new Typeface(_text.FontFamily, _text.FontStyle, _text.FontWeight, _text.FontStretch);

        while (size > 12)
        {
            var probe = new FormattedText(text, Zh, FlowDirection.LeftToRight, typeface, size, Brushes.White, dpi);
            if (probe.WidthIncludingTrailingWhitespace <= avail) break;
            size -= 0.5;
        }

        _text.FontSize = Math.Max(12, size);
    }

    private string PickCustom()
    {
        var list = Cfg.GreetingTexts;
        if (list.Count == 0) return Cfg.AutoGreeting(DateTime.Now);
        if (list.Count == 1) return list[0];

        // 顺序轮换，避免随机连出同一条
        _index = (_index + 1) % list.Count;
        return list[_index];
    }
}

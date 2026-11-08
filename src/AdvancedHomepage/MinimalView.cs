using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 极简样式：卡片里只放一个居中的日期（yyyy:MM:dd）和一行轮播标语。
/// 尺寸与日历卡保持一致，切换样式时卡片不会跳动。
/// </summary>
internal sealed class MinimalView : Grid
{
    private const double RefWidth = 315.0;
    private const double HeightRatio = 370.0 / 315.0;

    private readonly TextBlock _date;
    private readonly TextBlock _slogan;
    private readonly DispatcherTimer _timer;
    private readonly Random _rng = new();

    private int _index = -1;
    private int _tick;
    private bool _active;

    public MinimalView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;
        ClipToBounds = false;

        var stack = new VerticalAlignmentPanel();

        _date = new TextBlock
        {
            Text = Today(),
            FontSize = 40,
            FontWeight = FontWeights.Light,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        _date.SetValue(TextBlock.ForegroundProperty, Palette.TextPrimary);

        _slogan = new TextBlock
        {
            Text = "",
            FontSize = 14,
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75
        };
        _slogan.SetValue(TextBlock.ForegroundProperty, Palette.TextTertiary);

        stack.Children.Add(_date);
        stack.Children.Add(_slogan);
        Children.Add(stack);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = RefWidth;

        // 字号跟着卡片宽度走，和日历卡的缩放逻辑一致
        double s = w / RefWidth;
        _date.FontSize = 40 * s;
        _slogan.FontSize = 14 * s;
        _slogan.Margin = new Thickness(w * 0.08, 16 * s, w * 0.08, 0);

        base.MeasureOverride(new Size(w, w * HeightRatio));
        return new Size(w, w * HeightRatio);
    }

    /// <summary>切换样式时调用：启用后开始轮播，停用后停表省电。</summary>
    public void SetActive(bool active)
    {
        if (_active == active)
        {
            if (active) RefreshDate();
            return;
        }

        _active = active;
        if (active)
        {
            RefreshDate();
            ShowNext(animate: false);
            _tick = 0;
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _slogan.BeginAnimation(OpacityProperty, null);
            _slogan.Opacity = 0.75;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        RefreshDate();

        int interval = Math.Clamp(Cfg.SloganInterval, 3, 600);
        _tick++;
        if (_tick >= interval)
        {
            _tick = 0;
            ShowNext(animate: true);
        }
    }

    private void RefreshDate()
    {
        var now = Today();
        if (!string.Equals(_date.Text, now, StringComparison.Ordinal)) _date.Text = now;
    }

    private static string Today() => DateTime.Now.ToString("yyyy:MM:dd");

    private void ShowNext(bool animate)
    {
        var list = Cfg.Slogans;
        if (list.Count == 0)
        {
            _slogan.Text = "";
            return;
        }

        if (list.Count > 1)
        {
            int next;
            do
            {
                next = _rng.Next(list.Count);
            } while (next == _index);

            _index = next;
        }
        else
        {
            _index = 0;
        }

        var text = list[_index];
        if (!animate)
        {
            _slogan.BeginAnimation(OpacityProperty, null);
            _slogan.Text = text;
            _slogan.Opacity = 0.75;
            return;
        }

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            _slogan.Text = text;
            var fadeIn = new DoubleAnimation(0.75, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            _slogan.BeginAnimation(OpacityProperty, fadeIn);
        };
        _slogan.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>把内容整体压在卡片正中。</summary>
    private sealed class VerticalAlignmentPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            double w = 0, h = 0;
            foreach (UIElement child in Children)
            {
                child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
                w = Math.Max(w, child.DesiredSize.Width);
                h += child.DesiredSize.Height;
            }

            return new Size(w, h);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double total = 0;
            foreach (UIElement child in Children) total += child.DesiredSize.Height;

            double y = Math.Max(0, (finalSize.Height - total) / 2);
            foreach (UIElement child in Children)
            {
                double h = child.DesiredSize.Height;
                child.Arrange(new Rect(0, y, finalSize.Width, h));
                y += h;
            }

            return finalSize;
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 自定义种子色取色器（M3 小弹窗）：
/// 一条色相轨道 + 饱和度 / 明度两条渐变轨道，右下角实时预览。
/// 选出来的颜色会交给 <see cref="AppTheme.ApplyCustom"/> 生成整套 M3 配色。
/// </summary>
internal sealed class ColorPickerDialog : Window
{
    private readonly Border _preview;
    private readonly TextBlock _hex;
    private readonly GradientSlider _hue;
    private readonly GradientSlider _sat;
    private readonly GradientSlider _val;

    private double _h;
    private double _s;
    private double _v;
    private Color _result;

    private ColorPickerDialog(Color seed)
    {
        Dlg.Setup(this, "自定义颜色", 520);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;

        (_h, _s, _v) = ToHsv(seed);
        _result = seed;

        var panel = new StackPanel { Width = 424 };

        // ── 标题 ──
        panel.Children.Add(Dlg.Header("自定义颜色", () => DialogResult = false,
            "选一个种子色，M3 会按色调表生成整套配色。"));

        // ── 预览 ──
        _preview = new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(36),
            Background = new SolidColorBrush(seed),
            BorderThickness = new Thickness(1),
            BorderBrush = M3.Alpha(M3.C.OutlineVariant, 0.9),
            VerticalAlignment = VerticalAlignment.Center
        };

        _hex = new TextBlock
        {
            Text = AppTheme.Hex(seed),
            FontSize = 15,
            FontWeight = FontWeights.Medium,
            Foreground = M3.C.OnSurface,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };

        var hint = new TextBlock
        {
            Text = "拖动下面三条轨道调色",
            FontSize = 11.5,
            Foreground = M3.C.OnSurfaceVariant,
            Margin = new Thickness(16, 4, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var previewRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 18)
        };
        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(_hex);
        textStack.Children.Add(hint);
        previewRow.Children.Add(_preview);
        previewRow.Children.Add(textStack);
        panel.Children.Add(previewRow);

        // ── 三条轨道 ──
        _hue = new GradientSlider(_h, 0, 360, HueBrush());
        _sat = new GradientSlider(_s * 100, 0, 100, Brushes.Gray);
        _val = new GradientSlider(_v * 100, 0, 100, Brushes.Black);

        panel.Children.Add(SliderRow("色相", _hue, v => { _h = v; Refresh(); }));
        panel.Children.Add(SliderRow("饱和度", _sat, v => { _s = v / 100.0; Refresh(); }));
        panel.Children.Add(SliderRow("明度", _val, v => { _v = v / 100.0; Refresh(); }));

        // ── 底部按钮 ──
        var cancel = M3Button.Create("取消", () => DialogResult = false, M3ButtonKind.Text);
        var confirm = M3Button.Create("使用此颜色", Confirm, M3ButtonKind.Filled);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        buttons.Children.Add(cancel);
        confirm.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(confirm);
        panel.Children.Add(buttons);

        Dlg.Mount(this, Dlg.Shell(panel));

        Refresh();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
        };
    }

    public static Color? Pick(Color seed, Window? owner = null)
    {
        try
        {
            var dialog = new ColorPickerDialog(seed);
            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            return dialog.ShowDialog() == true ? dialog._result : null;
        }
        catch (Exception ex)
        {
            Log.Error("打开取色器失败", ex);
            return null;
        }
    }

    private void Confirm()
    {
        _result = FromHsv(_h, _s, _v);
        DialogResult = true;
    }

    private FrameworkElement SliderRow(string label, GradientSlider slider, Action<double> changed)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Width = 52,
            Foreground = M3.C.OnSurfaceVariant,
            VerticalAlignment = VerticalAlignment.Center
        };

        var value = new TextBlock
        {
            Text = "0",
            FontSize = 11.5,
            Width = 34,
            TextAlignment = TextAlignment.Right,
            Foreground = M3.C.OnSurfaceVariant,
            VerticalAlignment = VerticalAlignment.Center
        };

        slider.Changed += v =>
        {
            value.Text = $"{v:0}";
            changed(v);
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 2)
        };
        row.Children.Add(text);
        slider.Width = 268;
        slider.Margin = new Thickness(0, 0, 10, 0);
        row.Children.Add(slider);
        row.Children.Add(value);

        // 初值同步到右侧数字
        value.Text = $"{slider.Value:0}";
        return row;
    }

    private void Refresh()
    {
        var color = FromHsv(_h, _s, _v);
        _preview.Background = new SolidColorBrush(color);
        _hex.Text = AppTheme.Hex(color);

        _sat.SetBrush(new LinearGradientBrush(
            FromHsv(_h, 0, _v), FromHsv(_h, 1, _v), 0));

        _val.SetBrush(new LinearGradientBrush(
            Colors.Black, FromHsv(_h, _s, 1), 0));
    }

    private static Brush HueBrush()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++)
        {
            brush.GradientStops.Add(new GradientStop(FromHsv(i * 60, 1, 1), i / 6.0));
        }
        return brush;
    }

    // ── HSV ↔ RGB ──

    private static (double H, double S, double V) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double v = max;
        double s = max <= 0.001 ? 0 : delta / max;

        double h;
        if (delta <= 0.0001) h = 0;
        else if (Math.Abs(max - r) < 0.0001) h = 60 * (((g - b) / delta) % 6);
        else if (Math.Abs(max - g) < 0.0001) h = 60 * ((b - r) / delta + 2);
        else h = 60 * ((r - g) / delta + 4);
        if (h < 0) h += 360;

        return (h, s, v);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);

        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;

        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Color.FromRgb(
            (byte)Math.Round(Math.Clamp(r + m, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(g + m, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(b + m, 0, 1) * 255));
    }
}

/// <summary>带渐变轨道的滑块：12dp 轨道 + 22dp 圆形手柄，拖到哪取到哪。</summary>
internal sealed class GradientSlider : Grid
{
    private readonly Border _track;
    private readonly Border _thumb;
    private readonly double _min;
    private readonly double _max;
    private double _value;

    public event Action<double>? Changed;

    public GradientSlider(double value, double min, double max, Brush track)
    {
        _min = min;
        _max = max;
        _value = Math.Clamp(value, min, max);

        Height = 34;
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;

        _track = new Border
        {
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = track,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        Children.Add(_track);

        _thumb = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = Brushes.White,
            BorderThickness = new Thickness(2),
            BorderBrush = M3.C.SurfaceHigh,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Effect = M3.Elevation(1)
        };
        Children.Add(_thumb);

        SizeChanged += (_, _) => Place();
        MouseLeftButtonDown += (_, e) =>
        {
            CaptureMouse();
            SetFromX(e.GetPosition(this).X);
        };
        MouseMove += (_, e) =>
        {
            if (IsMouseCaptured) SetFromX(e.GetPosition(this).X);
        };
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
    }

    public double Value => _value;

    public void SetBrush(Brush brush) => _track.Background = brush;

    private void SetFromX(double x)
    {
        double pad = 11;
        double width = Math.Max(1, ActualWidth - pad * 2);
        double ratio = Math.Clamp((x - pad) / width, 0, 1);
        _value = _min + (_max - _min) * ratio;
        Place();
        Changed?.Invoke(_value);
    }

    private void Place()
    {
        double pad = 11;
        double width = Math.Max(1, ActualWidth - pad * 2);
        double ratio = _max <= _min ? 0 : (_value - _min) / (_max - _min);
        _thumb.Margin = new Thickness(pad + width * ratio - 11, 0, 0, 0);
    }
}

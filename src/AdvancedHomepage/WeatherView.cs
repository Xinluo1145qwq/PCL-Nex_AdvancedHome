using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 天气组件。默认是 M3 Expressive 圆盘：一整块浅色圆盘 + 超大温度数字 + 太阳/云朵图标（云压在太阳上），
/// 条件与城市压在底部；可以切成「铺满」样式，整块卡片换成当前天气的底色。
/// 数据来自 Open-Meteo，不需要 API key；10 分钟缓存一次。
/// </summary>
internal sealed class WeatherView : Grid
{
    private sealed class Snapshot
    {
        public double Temp;
        public double Feels;
        public double Humidity;
        public double Wind;
        public int Code;
        public string Place = "";
        public bool Ok;
    }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static Snapshot? _cache;
    private static DateTime _cacheAt;
    private static string _cacheCity = "";
    private static bool _busy;
    private static DateTime _failedAt;

    private readonly Border _backdrop = new();
    private readonly WeatherFace _face = new();

    private string _style = "md3";
    private double _backdropOpacity = 1;
    private bool _showDetails = true;

    public WeatherView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        // 铺满样式的底色层：圆角跟着卡片走，只盖住卡片本身
        _backdrop.CornerRadius = new CornerRadius(M3.CardRadius);
        _backdrop.Visibility = Visibility.Collapsed;
        _backdrop.IsHitTestVisible = false;
        Children.Add(_backdrop);
        Children.Add(_face);

        // 首屏时 OnShown 不一定触发，构造完先按配置画一次
        Refresh();

        Loaded += (_, _) => Kick(force: false);
    }

    public void Refresh()
    {
        var options = Cfg.Weather;
        _style = string.Equals(options.Style, "immersive", StringComparison.OrdinalIgnoreCase) ? "immersive" : "md3";
        _showDetails = options.ShowDetails;

        if (!string.IsNullOrWhiteSpace(options.City) &&
            !string.Equals(options.City, _cacheCity, StringComparison.OrdinalIgnoreCase))
        {
            Kick(force: true);
        }
        else
        {
            Paint();
        }
    }

    public void ApplyTheme()
    {
        _face.Invalidate();
        Paint();
    }

    /// <summary>背景透明度只作用在底色块上，文字与图标永远是实的。</summary>
    public void SetBackdropOpacity(double opacity)
    {
        _backdropOpacity = Math.Clamp(opacity, 0, 1);
        _backdrop.Opacity = _backdropOpacity;
    }

    // ─────────────────────────── 绘制状态 ───────────────────────────

    private void Paint()
    {
        var data = _cache;
        bool immersive = _style == "immersive";

        _face.Immersive = immersive;

        if (data == null || !data.Ok)
        {
            _face.Temp = "--°";
            _face.Condition = string.IsNullOrWhiteSpace(Cfg.Weather.City) ? "未设置城市" : "天气暂不可用";
            _face.Place = "";
            _face.Detail = "";
            _face.Code = -1;

            _backdrop.Background = immersive
                ? Gradient(Color.FromRgb(0x5C, 0x65, 0x77), Color.FromRgb(0x35, 0x3D, 0x4B))
                : (Brush)Brushes.Transparent;
            _backdrop.Visibility = immersive ? Visibility.Visible : Visibility.Collapsed;
            _backdrop.Opacity = _backdropOpacity;
            _face.Invalidate();
            return;
        }

        var look = Describe(data.Code);

        _face.Temp = $"{data.Temp:0}°";
        _face.Condition = look.Text;
        _face.Place = string.IsNullOrWhiteSpace(data.Place) ? Cfg.Weather.City : data.Place;
        _face.Detail = _showDetails
            ? $"体感 {data.Feels:0}° · 湿度 {data.Humidity:0}% · 风 {data.Wind:0.#} m/s"
            : "";
        _face.Code = data.Code;

        if (immersive)
        {
            _backdrop.Background = Gradient(look.Top, look.Bottom);
            _backdrop.Opacity = _backdropOpacity;
            _backdrop.Visibility = Visibility.Visible;
        }
        else
        {
            _backdrop.Visibility = Visibility.Collapsed;
        }

        _face.Invalidate();
    }

    private static LinearGradientBrush Gradient(Color top, Color bottom)
    {
        var brush = new LinearGradientBrush(top, bottom, new Point(0.1, 0), new Point(0.9, 1));
        brush.Freeze();
        return brush;
    }

    // ─────────────────────────── 数据 ───────────────────────────

    private void Kick(bool force)
    {
        string city = (Cfg.Weather.City ?? "").Trim();
        if (string.IsNullOrWhiteSpace(city)) return;
        if (_busy) return;

        bool fresh = _cache != null
                     && string.Equals(_cacheCity, city, StringComparison.OrdinalIgnoreCase)
                     && (DateTime.UtcNow - _cacheAt) < TimeSpan.FromMinutes(10);

        if (!force && fresh)
        {
            Paint();
            return;
        }

        if (!force && (DateTime.UtcNow - _failedAt) < TimeSpan.FromMinutes(1)) return;

        _busy = true;
        _ = Task.Run(async () =>
        {
            Snapshot snapshot;
            try
            {
                snapshot = await FetchAsync(city).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn("获取天气失败：" + ex.Message);
                snapshot = new Snapshot { Ok = false, Place = city };
                _failedAt = DateTime.UtcNow;
            }

            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _busy = false;
                    if (snapshot.Ok)
                    {
                        _cache = snapshot;
                        _cacheAt = DateTime.UtcNow;
                        _cacheCity = city;
                    }

                    Paint();
                }));
            }
            catch
            {
                _busy = false;
            }
        });
    }

    private static async Task<Snapshot> FetchAsync(string city)
    {
        // 1) 城市名 → 经纬度
        string geoUrl =
            "https://geocoding-api.open-meteo.com/v1/search?count=1&language=zh&format=json&name=" +
            Uri.EscapeDataString(city);

        using var geoDoc = JsonDocument.Parse(await Http.GetStringAsync(geoUrl).ConfigureAwait(false));
        if (!geoDoc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return new Snapshot { Ok = false, Place = city };

        var first = results[0];
        double lat = first.GetProperty("latitude").GetDouble();
        double lon = first.GetProperty("longitude").GetDouble();
        string place = first.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? city : city;

        // 2) 当前天气
        string api =
            $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m" +
            "&timezone=auto";

        using var doc = JsonDocument.Parse(await Http.GetStringAsync(api).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("current", out var current)) return new Snapshot { Ok = false, Place = place };

        return new Snapshot
        {
            Ok = true,
            Place = place,
            Temp = Read(current, "temperature_2m"),
            Feels = Read(current, "apparent_temperature"),
            Humidity = Read(current, "relative_humidity_2m"),
            Wind = Read(current, "wind_speed_10m"),
            Code = (int)Read(current, "weather_code")
        };
    }

    private static double Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;

    // ─────────────────────────── 天气码 → 文案 / 铺满底色 ───────────────────────────

    private static (string Text, Color Top, Color Bottom) Describe(int code) => code switch
    {
        0 => ("晴", Color.FromRgb(0xBE, 0x71, 0x18), Color.FromRgb(0x8A, 0x44, 0x12)),
        1 or 2 => ("多云", Color.FromRgb(0x50, 0x6E, 0x96), Color.FromRgb(0x2E, 0x44, 0x60)),
        3 => ("阴", Color.FromRgb(0x5C, 0x67, 0x75), Color.FromRgb(0x38, 0x41, 0x4C)),
        45 or 48 => ("雾", Color.FromRgb(0x66, 0x6E, 0x78), Color.FromRgb(0x42, 0x48, 0x50)),
        >= 51 and <= 57 => ("毛毛雨", Color.FromRgb(0x47, 0x67, 0x8D), Color.FromRgb(0x29, 0x3E, 0x54)),
        >= 61 and <= 67 => ("雨", Color.FromRgb(0x40, 0x61, 0x88), Color.FromRgb(0x24, 0x38, 0x4F)),
        >= 71 and <= 77 => ("雪", Color.FromRgb(0x63, 0x80, 0x9B), Color.FromRgb(0x3D, 0x55, 0x6C)),
        >= 80 and <= 82 => ("阵雨", Color.FromRgb(0x3F, 0x5F, 0x86), Color.FromRgb(0x22, 0x36, 0x4C)),
        >= 85 and <= 86 => ("阵雪", Color.FromRgb(0x63, 0x80, 0x9B), Color.FromRgb(0x3D, 0x55, 0x6C)),
        >= 95 => ("雷阵雨", Color.FromRgb(0x47, 0x3F, 0x68), Color.FromRgb(0x26, 0x21, 0x3F)),
        _ => ("晴", Color.FromRgb(0xBE, 0x71, 0x18), Color.FromRgb(0x8A, 0x44, 0x12))
    };
}

/// <summary>
/// 天气卡片的全部绘制：圆盘（或铺满时的透明） + 大温度 + 天气图标 + 底部城市/细节。
/// 颜色每次绘制现取，主题切换只重画不重建。
/// </summary>
internal sealed class WeatherFace : FrameworkElement
{
    private static readonly Typeface DisplayType = new(
        new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static readonly Typeface LabelType = new(
        new FontFamily("Microsoft YaHei UI, Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");

    public bool Immersive;
    public string Temp = "--°";
    public string Condition = "";
    public string Place = "";
    public string Detail = "";
    public int Code = -1;

    public void Invalidate() => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize)
    {
        // 自绘元素没有子元素，默认量出来是 0——卡片会跟着塌成一条线
        double w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height;
        return new Size(w, h);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 24 || h < 24) return;

        bool immersive = Immersive;
        bool dark = Palette.IsDark;
        double s = Math.Min(w, h);
        var center = new Point(w / 2, h / 2);
        double r = s / 2 - 1;

        // ── 圆盘（铺满样式不要，直接压在天气底色上）──
        if (!immersive)
        {
            var top = Mix(M3.C.SurfaceHighest, M3.C.Primary, dark ? 0.10 : 0.06);
            var bottom = Mix(M3.C.SurfaceHighest, M3.C.Primary, dark ? 0.24 : 0.16);
            var fill = new LinearGradientBrush(top, bottom, new Point(0.15, 0), new Point(0.85, 1));
            fill.Freeze();
            dc.DrawEllipse(fill, null, center, r, r);
        }

        // ── 配色 ──
        Brush tempBrush = immersive ? Brushes.White : M3.C.OnSurface;
        Brush condBrush = immersive ? M3.Alpha(Brushes.White, 0.86) : M3.Alpha(M3.C.OnSurfaceVariant, 0.95);
        Brush placeBrush = immersive ? M3.Alpha(Brushes.White, 0.94) : M3.Alpha(M3.C.OnSurface, 0.88);
        Brush detailBrush = immersive ? M3.Alpha(Brushes.White, 0.76) : M3.Alpha(M3.C.OnSurfaceVariant, 0.86);

        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // ── 天气文案 ──
        if (!string.IsNullOrWhiteSpace(Condition))
        {
            double condSize = Math.Max(10.5, s * 0.056);
            var cond = Fit(Condition, LabelType, condSize, s * 0.62, condBrush, ppd);
            dc.DrawText(cond, new Point(center.X - cond.Width / 2, center.Y - s * 0.35 - cond.Baseline + condSize * 0.35));
        }

        // ── 大温度 ──
        double tempSize = s * 0.32;
        var temp = Fit(Temp, DisplayType, tempSize, s * 0.74, tempBrush, ppd);
        dc.DrawText(temp, new Point(center.X - temp.Width / 2, center.Y - s * 0.03 - temp.Baseline + tempSize * 0.35));

        // ── 底部：图标 + 城市；细节单独一行，别挤在图标右边 ──
        // 卡片被缩到 2×1 这种小尺寸时，体感那行字会小到看不清，直接不画
        double iconW = s * 0.30, iconH = s * 0.185;
        double gap = s * 0.045;
        var placeText = string.IsNullOrWhiteSpace(Place)
            ? null
            : Fit(Place, LabelType, Math.Max(10, s * 0.052), s * 0.42, placeBrush, ppd);
        bool showDetail = !string.IsNullOrWhiteSpace(Detail) && s >= 150;
        var detailText = showDetail
            ? Fit(Detail, LabelType, Math.Max(9.5, s * 0.046), s * 0.80, detailBrush, ppd)
            : null;

        double groupWidth = iconW + (placeText != null ? gap + placeText.Width : 0);
        double left = center.X - groupWidth / 2;
        double iconCenterY = center.Y + s * (detailText == null ? 0.285 : 0.255);

        DrawIcon(dc, new Rect(left, iconCenterY - iconH / 2, iconW, iconH), immersive);

        if (placeText != null)
        {
            dc.DrawText(placeText, new Point(left + iconW + gap, iconCenterY - placeText.Height / 2 + s * 0.004));
        }

        if (detailText != null)
        {
            double detailSize = Math.Max(9.5, s * 0.046);
            dc.DrawText(detailText, new Point(
                center.X - detailText.Width / 2,
                center.Y + s * 0.395 - detailText.Baseline + detailSize * 0.35));
        }
    }

    // ─────────────────────────── 图标 ───────────────────────────

    private void DrawIcon(DrawingContext dc, Rect box, bool immersive)
    {
        int code = Code;
        bool clear = code is >= 0 and <= 2;
        bool fog = code is 45 or 48;
        bool rain = (code is >= 51 and <= 67) || (code is >= 80 and <= 82);
        bool snow = (code is >= 71 and <= 77) || (code is >= 85 and <= 86);
        bool storm = code >= 95;

        var sunColor = Color.FromRgb(0xF7, 0xC6, 0x43);
        var cloudColor = Colors.White;
        var cloudShade = immersive ? Color.FromArgb(0x38, 0x00, 0x00, 0x00) : Color.FromArgb(0x18, 0x1E, 0x30, 0x52);
        var accent = immersive ? Colors.White : Color.FromRgb(0x53, 0x7C, 0xC9);

        if (clear)
        {
            // 太阳在左上，云压在右下：参考图里那种「云飘过太阳」的层次
            double sunR = box.Height * 0.36;
            var sunCenter = new Point(box.X + box.Width * 0.29, box.Y + box.Height * 0.30);
            dc.DrawEllipse(new SolidColorBrush(sunColor), null, sunCenter, sunR, sunR);

            var cloudBox = new Rect(box.X + box.Width * 0.26, box.Y + box.Height * 0.40, box.Width * 0.74, box.Height * 0.60);
            dc.DrawGeometry(new SolidColorBrush(cloudShade), null, Cloud(cloudBox, -0.06));
            dc.DrawGeometry(new SolidColorBrush(cloudColor), null, Cloud(cloudBox, 0));
            return;
        }

        bool plain = code < 0 || code == 3 || fog;
        var body = new Rect(box.X, box.Y + box.Height * 0.06, box.Width, box.Height * (plain ? 0.62 : 0.58));

        if (fog)
        {
            var pen = new Pen(new SolidColorBrush(accent), Math.Max(1.2, box.Height * 0.075))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            pen.Freeze();

            for (int i = 0; i < 3; i++)
            {
                double y = box.Y + box.Height * (0.34 + i * 0.24);
                double shrink = box.Width * (0.10 + i * 0.06);
                dc.DrawLine(pen, new Point(box.X + shrink, y), new Point(box.X + box.Width - shrink, y));
            }

            return;
        }

        dc.DrawGeometry(new SolidColorBrush(cloudShade), null, Cloud(new Rect(body.X, body.Y + box.Height * 0.05, body.Width, body.Height), -0.06));
        dc.DrawGeometry(new SolidColorBrush(cloudColor), null, Cloud(body, 0));

        if (rain || snow)
        {
            double y0 = box.Y + box.Height * 0.80;
            if (rain)
            {
                var pen = new Pen(new SolidColorBrush(accent), Math.Max(1.2, box.Height * 0.07))
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                pen.Freeze();

                for (int i = 0; i < 3; i++)
                {
                    double x = box.X + box.Width * (0.28 + i * 0.22);
                    dc.DrawLine(pen, new Point(x, y0), new Point(x - box.Width * 0.03, y0 + box.Height * 0.14));
                }
            }
            else
            {
                var dot = new SolidColorBrush(accent);
                double rad = Math.Max(1.1, box.Height * 0.048);
                for (int i = 0; i < 3; i++)
                {
                    dc.DrawEllipse(dot, null, new Point(box.X + box.Width * (0.30 + i * 0.20), y0 + box.Height * 0.07), rad, rad);
                }
            }
        }
        else if (storm)
        {
            var bolt = new StreamGeometry();
            using (var ctx = bolt.Open())
            {
                ctx.BeginFigure(new Point(box.X + box.Width * 0.52, box.Y + box.Height * 0.72), true, true);
                ctx.LineTo(new Point(box.X + box.Width * 0.40, box.Y + box.Height * 0.94), true, false);
                ctx.LineTo(new Point(box.X + box.Width * 0.49, box.Y + box.Height * 0.94), true, false);
                ctx.LineTo(new Point(box.X + box.Width * 0.42, box.Y + box.Height * 1.12), true, false);
                ctx.LineTo(new Point(box.X + box.Width * 0.62, box.Y + box.Height * 0.86), true, false);
                ctx.LineTo(new Point(box.X + box.Width * 0.53, box.Y + box.Height * 0.86), true, false);
                ctx.LineTo(new Point(box.X + box.Width * 0.60, box.Y + box.Height * 0.72), true, false);
            }

            bolt.Freeze();
            dc.DrawGeometry(new SolidColorBrush(sunColor), null, bolt);
        }
    }

    /// <summary>云：底座 + 三个鼓包，整组填充，重叠处不会出现接缝。</summary>
    private static Geometry Cloud(Rect b, double lift)
    {
        double h = b.Height;
        double dy = h * lift;

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        group.Children.Add(new RectangleGeometry(
            new Rect(b.X + b.Width * 0.04, b.Y + b.Height * 0.50 - dy, b.Width * 0.92, b.Height * 0.50),
            h * 0.25, h * 0.25));
        group.Children.Add(new EllipseGeometry(
            new Point(b.X + b.Width * 0.30, b.Y + b.Height * 0.48 - dy), h * 0.30, h * 0.30));
        group.Children.Add(new EllipseGeometry(
            new Point(b.X + b.Width * 0.56, b.Y + b.Height * 0.34 - dy), h * 0.36, h * 0.36));
        group.Children.Add(new EllipseGeometry(
            new Point(b.X + b.Width * 0.80, b.Y + b.Height * 0.54 - dy), h * 0.26, h * 0.26));

        group.Freeze();
        return group;
    }

    // ─────────────────────────── 小工具 ───────────────────────────

    private static Color Mix(Brush background, Brush foreground, double alpha) =>
        M3.Of(M3.Layer(M3.Of(background), M3.Of(foreground), alpha));

    private static FormattedText Fit(string text, Typeface type, double size, double maxWidth, Brush brush, double ppd)
    {
        var formatted = new FormattedText(text, Zh, FlowDirection.LeftToRight, type, size, brush, ppd);
        if (formatted.Width <= maxWidth || formatted.Width <= 0) return formatted;

        return new FormattedText(text, Zh, FlowDirection.LeftToRight, type, size * (maxWidth / formatted.Width), brush, ppd);
    }
}

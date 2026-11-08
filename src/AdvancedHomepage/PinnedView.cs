using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IOPath = System.IO.Path;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「单项快捷入口」卡片。两套版式，按卡片形状自动切换：
/// 又宽又扁（1 行高）走「图标在上」的紧凑版；两行以上走「图标在左 + 竖分隔线」的横向版。
/// </summary>
internal sealed class PinnedView : Grid
{
    /// <summary>紧凑版的设计尺寸（参考图一）。</summary>
    private const double CompactW = 313;
    private const double CompactH = 152;

    private readonly PinOptions _pin;
    private readonly Canvas _canvas = new();
    private readonly Border _iconFrame;
    private readonly Rectangle _divider;
    private readonly Path _catGlyph;
    private readonly TextBlock _catText;
    private readonly TextBlock _title;
    private readonly Path _m1Glyph;
    private readonly TextBlock _m1Text;
    private readonly Path _m2Glyph;
    private readonly TextBlock _m2Text;
    private readonly Path _watermark;
    private readonly Border _play;
    private readonly Path _playTriangle;

    private BitmapSource? _iconBitmap;
    private FrameworkElement? _iconFallback;
    private string _titleText = "";
    private string _catTextValue = "";
    private string _m1 = "";
    private string _m2 = "";
    private string _m1GlyphData = "";
    private string _m2GlyphData = "";
    private string _catGlyphData = "";

    private int _rowHint = 2;
    private double _width = CompactW;
    private double _height = CompactH;
    private DateTime _loadedAt = DateTime.MinValue;
    private DateTime _pingedAt = DateTime.MinValue;

    public PinnedView(PinOptions pin)
    {
        _pin = pin;

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        ClipToBounds = false;

        _canvas.HorizontalAlignment = HorizontalAlignment.Stretch;
        _canvas.VerticalAlignment = VerticalAlignment.Stretch;
        Children.Add(_canvas);

        _iconFrame = new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = Brushes.Transparent,
            ClipToBounds = true
        };
        _canvas.Children.Add(_iconFrame);

        _divider = new Rectangle
        {
            Width = 1,
            VerticalAlignment = VerticalAlignment.Stretch,
            Fill = Palette.Solid(Palette.IsDark ? "#14FFFFFF" : "#10000000")
        };
        _canvas.Children.Add(_divider);

        _catGlyph = Glyph(Palette.TextTertiary);
        _canvas.Children.Add(_catGlyph);

        _catText = new TextBlock { Foreground = Palette.TextTertiary, TextWrapping = TextWrapping.NoWrap };
        _canvas.Children.Add(_catText);

        _title = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = Palette.TextPrimary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        _canvas.Children.Add(_title);

        _m1Glyph = Glyph(Palette.TextTertiary);
        _canvas.Children.Add(_m1Glyph);

        _m1Text = new TextBlock
        {
            Foreground = Palette.TextTertiary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        _canvas.Children.Add(_m1Text);

        _m2Glyph = Glyph(Palette.TextTertiary);
        _canvas.Children.Add(_m2Glyph);

        _m2Text = new TextBlock
        {
            Foreground = Palette.TextTertiary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        _canvas.Children.Add(_m2Text);

        _watermark = new Path
        {
            Data = Geometry.Parse(WatermarkFor(pin.Kind)),
            Stroke = Palette.IsDark ? Palette.Solid("#FFFFFF") : Palette.Solid("#000000"),
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Opacity = 0.045,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        _canvas.Children.Add(_watermark);

        _playTriangle = new Path
        {
            Data = Geometry.Parse("M 0,0 L 10,6 L 0,12 Z"),
            Fill = M3.C.OnSurfaceVariant,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 平时是灰底（不抢标题），鼠标压上去才亮成主题色
        _play = new Border
        {
            CornerRadius = new CornerRadius(13),
            Background = M3.C.SurfaceContainerHighest,
            Cursor = Cursors.Hand,
            Child = _playTriangle
        };
        ToolTipService.SetToolTip(_play, UiStyles.RoundedToolTip(TipText()));
        ToolTipService.SetInitialShowDelay(_play, 350);
        _play.MouseEnter += (_, _) =>
        {
            _play.Background = M3.C.Primary;
            _playTriangle.Fill = M3.C.OnPrimary;
        };
        _play.MouseLeave += (_, _) =>
        {
            _play.Background = M3.C.SurfaceContainerHighest;
            _playTriangle.Fill = M3.C.OnSurfaceVariant;
        };
        _play.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Launch();
        };
        _canvas.Children.Add(_play);

        ToolTipService.SetToolTip(this, UiStyles.RoundedToolTip(""));
        ToolTipService.SetInitialShowDelay(this, 600);

        Build(CompactW, CompactH);
        Loaded += (_, _) => Reload(false);
    }

    public string Kind => _pin.Kind;

    /// <summary>卡片占几行：两行及以上用横向版，一行用紧凑版。由画布在排布时告知。</summary>
    public void SetRowSpan(int rows)
    {
        rows = Math.Max(1, rows);
        if (rows == _rowHint) return;
        _rowHint = rows;
        Build(_width, _height);
    }

    public string PinId => _pin.Id;

    /// <summary>编辑模式下藏起播放钮：右下角要留给缩放手柄，也避免编辑时误启动。</summary>
    public void SetEditing(bool editing)
    {
        _play.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        Opacity = editing ? 0.9 : 1;
    }

    // ─────────────────────────── 数据 ───────────────────────────

    /// <summary>重新解析目标：实例 / 存档 / 服务器都可能被用户改名或删掉。</summary>
    public void Reload(bool force)
    {
        if (!force && (DateTime.Now - _loadedAt).TotalSeconds < 10) return;
        _loadedAt = DateTime.Now;

        _iconBitmap = null;
        _iconFallback = null;
        _titleText = _pin.Name;
        _catTextValue = "实例";
        _catGlyphData = GlyphFor(_pin.Kind);
        _m1 = "";
        _m2 = "";
        _m1GlyphData = _catGlyphData;
        _m2GlyphData = IconClock;

        try
        {
            switch (_pin.Kind)
            {
                case "world":
                {
                    _catTextValue = "世界";
                    var world = WorldStore.ScanInstance(_pin.Instance)
                        .FirstOrDefault(w => string.Equals(IOPath.GetFileName(w.Folder), _pin.Target, StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(w.Folder, _pin.Target, StringComparison.OrdinalIgnoreCase));

                    if (world == null)
                    {
                        _m1 = "存档已不存在";
                        _m1GlyphData = GlyphFor("world");
                        _m2 = "";
                        _iconFallback = ImageUtil.CreateGrassIcon(48);
                        break;
                    }

                    _titleText = world.Name;
                    _m1 = string.IsNullOrWhiteSpace(world.GameModeText) ? "未知模式" : world.GameModeText;
                    _m1GlyphData = IconGamepad;
                    _m2 = string.Join(" · ", new[]
                    {
                        world.Version,
                        world.LastPlayed > DateTime.MinValue ? "游玩于 " + WorldStore.Humanize(world.LastPlayed) : ""
                    }.Where(x => !string.IsNullOrWhiteSpace(x)));

                    _iconBitmap = ImageUtil.Load(world.IconPath);
                    _iconFallback = ImageUtil.CreateGrassIcon(48);
                    break;
                }

                case "server":
                {
                    _catTextValue = "服务器";
                    _m2GlyphData = IconPin;

                    var server = ServerStore.Find(_pin.Instance, _pin.Target);
                    if (server == null)
                    {
                        _m1 = "服务器已不存在";
                        _m1GlyphData = GlyphFor("server");
                        _m2 = _pin.Target;
                        _iconFallback = ImageUtil.CreateServerIcon(48);
                        break;
                    }

                    _titleText = server.Name;
                    _m2 = server.Ip;
                    _m1 = "正在查询…";
                    _m1GlyphData = IconSignal;

                    _iconBitmap = server.Icon;
                    _iconFallback = ImageUtil.CreateServerIcon(48);
                    break;
                }

                case "site":
                {
                    _catTextValue = "网站";
                    var url = Sites.Normalize(_pin.Target);

                    _titleText = string.IsNullOrWhiteSpace(_pin.Name) ? Sites.Host(url) : _pin.Name;
                    _m1 = Sites.Host(url);
                    _m1GlyphData = IconGlobe;
                    _m2 = url;
                    _m2GlyphData = IconLink;
                    _iconBitmap = Sites.LoadIcon(_pin.Icon);
                    if (_iconBitmap == null) _iconFallback = ImageUtil.CreateGlobeIcon(48);
                    break;
                }

                default:
                {
                    var instance = InstanceStore.Scan()
                        .FirstOrDefault(i => string.Equals(i.Name, _pin.Instance, StringComparison.OrdinalIgnoreCase));

                    if (instance == null)
                    {
                        _m1 = "实例已不存在";
                        _m1GlyphData = GlyphFor("instance");
                        _m2 = "";
                        _iconFallback = ImageUtil.CreateGrassIcon(48);
                        break;
                    }

                    _titleText = string.IsNullOrWhiteSpace(instance.Name) ? _pin.Name : instance.Name;
                    _m1 = instance.VersionText;
                    _m2 = instance.LastPlayed > DateTime.MinValue
                        ? "游玩于 " + WorldStore.Humanize(instance.LastPlayed)
                        : "还没启动过";

                    _iconBitmap = ImageUtil.Load(instance.IconPath);

                    var block = ImageUtil.LoadPclBlock(instance.Loader);
                    if (block != null)
                        _iconFallback = new Image { Source = block, Stretch = Stretch.UniformToFill };

                    if (_iconFallback == null && _iconBitmap == null)
                        _iconFallback = ImageUtil.CreateGrassIcon(48);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"刷新快捷入口失败（{_pin.Name}）：{ex.Message}");
        }

        ApplyIcon();
        Build(_width, _height);

        if (_pin.Kind == "server" && !string.IsNullOrWhiteSpace(_pin.Target)) PingServer();
    }

    /// <summary>在线人数：后台查，回来再改文字，绝不卡住主页。</summary>
    private void PingServer()
    {
        if (DateTime.Now - _pingedAt < TimeSpan.FromSeconds(30)) return;
        _pingedAt = DateTime.Now;

        var address = _pin.Target;
        try
        {
            ServerPing.GetAsync(address, status =>
            {
                if (!string.Equals(_pin.Target, address, StringComparison.OrdinalIgnoreCase)) return;
                _m1 = status.Text;
                FlushTexts(_width, _height);
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"查询在线人数失败（{address}）：{ex.Message}");
            _m1 = "离线";
            FlushTexts(_width, _height);
        }
    }

    private void ApplyIcon()
    {
        _iconFrame.Child = null;
        _iconFrame.Background = Brushes.Transparent;

        if (_iconBitmap != null)
        {
            _iconFrame.Background = new ImageBrush(_iconBitmap)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            return;
        }

        var fallback = _iconFallback ?? ImageUtil.CreateGrassIcon(48);

        // 占位图是按 48px 画的，这里用 Viewbox 撑满实际图标框
        _iconFrame.Child = new Viewbox
        {
            Child = fallback,
            Stretch = Stretch.UniformToFill,
            StretchDirection = StretchDirection.Both
        };
    }

    private void Launch()
    {
        switch (_pin.Kind)
        {
            case "world":
                LaunchBridge.Launch(_pin.Instance, IOPath.GetFileName(_pin.Target));
                break;

            case "server":
                LaunchBridge.LaunchServer(_pin.Instance, _pin.Target);
                break;

            case "site":
                Sites.Open(_pin.Target);
                break;

            default:
                LaunchBridge.Launch(_pin.Instance);
                break;
        }
    }

    private string TipText()
    {
        var line = _pin.Kind switch
        {
            "world" => "进入这个存档",
            "server" => "加入这个服务器",
            "site" => "用浏览器打开",
            _ => "启动这个实例"
        };

        var detail = _pin.Kind == "site" ? _pin.Target : _m2;
        return $"{line}\n{(string.IsNullOrWhiteSpace(detail) ? _pin.Name : detail)}".TrimEnd();
    }

    // ─────────────────────────── 版式 ───────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = _width;

        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * CompactH / CompactW;

        if (Math.Abs(w - _width) > 0.4 || Math.Abs(h - _height) > 0.4) Build(w, h);

        var size = new Size(w, h);
        base.MeasureOverride(size);
        return size;
    }

    private bool IsWide()
    {
        // 两行以上一定是横版；1 行高但很宽的卡（3×1）也走横版，
        // 否则图标会被「图标在上」的紧凑版挤成一小块。
        if (_rowHint >= 2) return true;

        return _width > 1 && _height > 1 && _width / _height >= 1.7;
    }

    private void Build(double width, double height)
    {
        if (width <= 0 || height <= 0) return;

        _width = width;
        _height = height;

        _canvas.Width = width;
        _canvas.Height = height;

        // 高宽比大于 0.6（两行及以上）用横向版，否则用图标在上的紧凑版
        if (IsWide()) BuildWide(width, height);
        else BuildCompact(width, height);

        FlushTexts(width, height);
    }

    /// <summary>只更新文字（在线人数回来时用，不重新排版）。</summary>
    private void FlushTexts(double width, double height)
    {
        _title.Text = _titleText;
        _catText.Text = _catTextValue;
        _m1Text.Text = _m1;
        _m2Text.Text = _m2;

        if (IsWide())
        {
            bool hasSecond = !string.IsNullOrWhiteSpace(_m2);
            _m2Glyph.Visibility = hasSecond ? Visibility.Visible : Visibility.Collapsed;
            _m2Text.Visibility = hasSecond ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            // 紧凑版只显示一行副标题
            _m2Glyph.Visibility = Visibility.Collapsed;
            _m2Text.Visibility = Visibility.Collapsed;
        }

        ToolTipService.SetToolTip(_play, UiStyles.RoundedToolTip(TipText()));
        ToolTipService.SetToolTip(this, UiStyles.RoundedToolTip(CardTip()));
    }

    /// <summary>整卡的说明：标题可能被省略号截断，悬停能看全。</summary>
    private string CardTip()
    {
        var lines = new List<string> { _titleText };
        if (!string.IsNullOrWhiteSpace(_m1)) lines.Add(_m1);
        if (!string.IsNullOrWhiteSpace(_m2)) lines.Add(_m2);
        return string.Join("\n", lines);
    }

    private void BuildCompact(double width, double height)
    {
        double s = Math.Clamp(Math.Min(width / CompactW, height / CompactH), 0.7, 2.4);

        double padX = 16 * s;
        double iconSize = Math.Min(60 * s, height * 0.5);

        _iconFrame.Width = iconSize;
        _iconFrame.Height = iconSize;
        _iconFrame.CornerRadius = new CornerRadius(8 * s);
        Canvas.SetLeft(_iconFrame, padX);
        Canvas.SetTop(_iconFrame, Math.Max(6 * s, height * 0.045));

        _divider.Visibility = Visibility.Collapsed;
        _catGlyph.Visibility = Visibility.Collapsed;
        _catText.Visibility = Visibility.Collapsed;

        _title.FontSize = UiStyles.CardTitleSize(width);
        _title.MaxWidth = Math.Max(40, width - padX * 2);
        Canvas.SetLeft(_title, padX);
        Canvas.SetTop(_title, Math.Max(height * 0.445, iconSize + 10 * s));

        // 副标题：小图标 + 一行文字
        double glyph = 12 * s;
        double subY = height * 0.62;

        _m1Glyph.Visibility = Visibility.Visible;
        _m1Glyph.Width = glyph;
        _m1Glyph.Height = glyph;
        _m1Glyph.Data = Geometry.Parse(string.IsNullOrEmpty(_m1GlyphData) ? IconCube : _m1GlyphData);
        _m1Glyph.StrokeThickness = 1.5 * s;
        Canvas.SetLeft(_m1Glyph, padX);
        Canvas.SetTop(_m1Glyph, subY + 1.5 * s);

        _m1Text.FontSize = 11 * s;
        _m1Text.MaxWidth = Math.Max(40, width - padX * 2 - glyph - 6 * s);
        Canvas.SetLeft(_m1Text, padX + glyph + 6 * s);
        Canvas.SetTop(_m1Text, subY - 1.5 * s);

        // 右上角水印
        double mark = 76 * s;
        _watermark.Width = mark;
        _watermark.Height = mark;
        _watermark.StrokeThickness = 1.4 * s;
        _watermark.Opacity = 0.04;
        Canvas.SetLeft(_watermark, width - 14 * s - mark);
        Canvas.SetTop(_watermark, 8 * s);

        PlacePlay(width, height, Math.Clamp(26 * s, 20, 46), 15 * s, 13 * s);
    }

    private void BuildWide(double width, double height)
    {
        // 版式按设计稿 313×152 的坐标系摆，再整体缩放到卡片宽度。
        // 9 列网格下 3 格宽只有 ~250，卡片会比设计稿更高，所以内容整体垂直居中，
        // 不再按 height 的百分比硬摊开（那样越高留白越散，字还不变大）。
        double s = Math.Clamp(Math.Min(width / CompactW, height / CompactH * 1.15), 0.72, 1.45);
        double designH = CompactH * s;
        double top = Math.Max(0, (height - designH) / 2);

        double leftW = 108 * s;
        double iconSize = Math.Min(leftW * 0.84, 96 * s);

        _iconFrame.Width = iconSize;
        _iconFrame.Height = iconSize;
        _iconFrame.CornerRadius = new CornerRadius(Math.Min(14, iconSize * 0.16));
        Canvas.SetLeft(_iconFrame, Math.Max(6 * s, (leftW - iconSize) / 2));
        Canvas.SetTop(_iconFrame, top + (designH - iconSize) / 2);

        _divider.Visibility = Visibility.Visible;
        _divider.Width = 1;
        _divider.Height = designH * 0.72;
        Canvas.SetLeft(_divider, leftW);
        Canvas.SetTop(_divider, top + designH * 0.14);

        double textX = leftW + 14 * s;
        double textW = Math.Max(60, width - textX - 12 * s);

        // 分类（实例 / 世界 / 服务器）
        double catFont = Math.Clamp(10.2 * s, 10, 14);
        double catGlyphSize = catFont * 1.15;

        _catGlyph.Visibility = Visibility.Visible;
        _catGlyph.Width = catGlyphSize;
        _catGlyph.Height = catGlyphSize;
        _catGlyph.Data = Geometry.Parse(string.IsNullOrEmpty(_catGlyphData) ? IconCube : _catGlyphData);
        _catGlyph.StrokeThickness = Math.Max(1, catFont * 0.13);
        Canvas.SetLeft(_catGlyph, textX);
        Canvas.SetTop(_catGlyph, top + designH * 0.15);

        _catText.Visibility = Visibility.Visible;
        _catText.FontSize = catFont;
        Canvas.SetLeft(_catText, textX + catGlyphSize + catFont * 0.4);
        Canvas.SetTop(_catText, top + designH * 0.128);

        // 标题
        _title.FontSize = UiStyles.CardTitleSize(width);
        _title.MaxWidth = textW;
        Canvas.SetLeft(_title, textX);
        Canvas.SetTop(_title, top + designH * 0.245);

        // 两行元信息
        double metaFont = Math.Clamp(11.2 * s, 11, 14);
        double metaGlyph = metaFont * 1.05;
        double m1Y = top + designH * 0.565;
        double m2Y = top + designH * 0.735;

        _m1Glyph.Visibility = Visibility.Visible;
        _m1Glyph.Width = metaGlyph;
        _m1Glyph.Height = metaGlyph;
        _m1Glyph.Data = Geometry.Parse(string.IsNullOrEmpty(_m1GlyphData) ? IconCube : _m1GlyphData);
        _m1Glyph.StrokeThickness = Math.Max(1, metaFont * 0.12);
        Canvas.SetLeft(_m1Glyph, textX);
        Canvas.SetTop(_m1Glyph, m1Y + metaFont * 0.1);

        _m1Text.FontSize = metaFont;
        _m1Text.MaxWidth = textW;
        Canvas.SetLeft(_m1Text, textX + metaGlyph + metaFont * 0.42);
        Canvas.SetTop(_m1Text, m1Y - metaFont * 0.16);

        _m2Glyph.Width = metaGlyph;
        _m2Glyph.Height = metaGlyph;
        _m2Glyph.Data = Geometry.Parse(string.IsNullOrEmpty(_m2GlyphData) ? IconClock : _m2GlyphData);
        _m2Glyph.StrokeThickness = Math.Max(1, metaFont * 0.12);
        Canvas.SetLeft(_m2Glyph, textX);
        Canvas.SetTop(_m2Glyph, m2Y + metaFont * 0.1);

        _m2Text.FontSize = metaFont;
        _m2Text.MaxWidth = textW;
        Canvas.SetLeft(_m2Text, textX + metaGlyph + metaFont * 0.42);
        Canvas.SetTop(_m2Text, m2Y - metaFont * 0.16);

        // 右上角水印（和其他快捷入口一致）
        double mark = 70 * s;
        _watermark.Width = mark;
        _watermark.Height = mark;
        _watermark.StrokeThickness = 1.5 * s;
        _watermark.Opacity = 0.04;
        Canvas.SetLeft(_watermark, width - 10 * s - mark);
        Canvas.SetTop(_watermark, top + 4 * s);

        PlacePlay(width, height, Math.Clamp(24 * s, 20, 34), 12 * s, 10 * s);
    }

    private void PlacePlay(double width, double height, double size, double right, double bottom)
    {
        _play.Width = size;
        _play.Height = size;
        _play.CornerRadius = new CornerRadius(size / 2);
        Canvas.SetLeft(_play, width - right - size);
        Canvas.SetTop(_play, height - bottom - size);

        _playTriangle.Width = size * 0.34;
        _playTriangle.Height = size * 0.4;
        _playTriangle.Margin = new Thickness(size * 0.06, 0, 0, 0);
    }

    // ─────────────────────────── 小图标 ───────────────────────────

    private static Path Glyph(Brush stroke) => new()
    {
        Width = 12,
        Height = 12,
        Stroke = stroke,
        StrokeThickness = 1.5,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Stretch = Stretch.Uniform
    };

    private const string IconCube = "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z M4,7.5 L12,12 L20,7.5 M12,12 L12,21";
    private const string IconSignal = "M6,20 V15 M10,20 V11 M14,20 V7 M18,20 V3";
    private const string IconPin = "M12,21 C12,21 18,15.5 18,10 A6,6 0 1,0 6,10 C6,15.5 12,21 12,21 Z";
    private const string IconClock = "M12,3 A9,9 0 1,1 11.99,3 Z M12,7.5 V12.5 L15.5,14.5";
    private const string IconGamepad = "M7,9 H17 A4,4 0 0 1 17,17 H7 A4,4 0 0 1 7,9 Z M9.5,12 V14.5 M8.2,13.2 H10.8";
    private const string IconGlobe = "M12,3 A9,9 0 1,1 11.99,3 Z M3,12 H21 M12,3 C15.4,6.4 15.4,17.6 12,21 C8.6,17.6 8.6,6.4 12,3";
    private const string IconLink = "M10.5,13.5 L13.5,10.5 M9,12 L7,14 A2.8,2.8 0 0 0 11,18 L13,16 M15,12 L17,10 A2.8,2.8 0 0 0 13,6 L11,8";

    private static string GlyphFor(string kind) => kind switch
    {
        "server" => IconSignal,
        "world" => IconPin,
        "site" => IconGlobe,
        _ => IconCube
    };

    private static string WatermarkFor(string kind) => kind switch
    {
        "server" => "M4,17 A8,8 0 0 1 20,17 M8,17 A4,4 0 0 1 16,17 M12,17 L12,20",
        "world" => "M7,9 H17 A4,4 0 0 1 17,17 H7 A4,4 0 0 1 7,9 Z M9.5,12 V14.5 M8.2,13.2 H10.8",
        "site" => IconGlobe,
        _ => "M12,2 L21,7 L21,17 L12,22 L3,17 L3,7 Z M3,7 L12,12 L21,7 M12,12 L12,22"
    };
}

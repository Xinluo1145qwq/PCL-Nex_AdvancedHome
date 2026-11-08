using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 日历卡片。所有尺寸按参考稿（内容区 315 × 370）等比例换算，
/// 因此卡片放到任意宽度下，留白与字号比例都与设计稿一致。
/// </summary>
internal sealed class CalendarView : Grid
{
    // ── 参考稿基准：卡片内容区 315 宽 × 370 高 ──
    private const double RefWidth = 315.0;
    private const double RefHeight = 370.0;
    private const double HeightRatio = RefHeight / RefWidth;

    private static readonly string[] WeekMon = { "一", "二", "三", "四", "五", "六", "日" };
    private static readonly string[] WeekSun = { "日", "一", "二", "三", "四", "五", "六" };
    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");

    // TextBlock 用 BlockLineHeight 时，墨迹中心比行框中心偏高约 0.21 em
    private const double TextBaselineFudge = 0.21;

    private static readonly DateTime MinMonth = new(1970, 1, 1);
    private static readonly DateTime MaxMonth = new(2099, 12, 1);

    private readonly Canvas _canvas = new();
    private DateTime _month;
    private DateTime _selected;
    private double _width;
    private double _height;

    /// <summary>编辑布局时关闭内部交互，避免与拖动冲突。</summary>
    public bool Interactive { get; set; } = true;

    /// <summary>极简样式：去掉格子底色与热力着色，只保留日期与描边。</summary>
    public bool Minimal { get; set; } = Cfg.IsMinimal;

    public CalendarView()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;

        _canvas.ClipToBounds = false;
        Children.Add(_canvas);


        var today = DateTime.Today;
        _month = new DateTime(today.Year, today.Month, 1);
        _selected = today;
        Build(RefWidth, RefWidth * HeightRatio);
    }

    // ─────────────────────────── 布局 ───────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = _width > 0 ? _width : RefWidth;

        // 卡片高度撑满格子：这样日历底边和右边的卡片对齐在同一条网格线上
        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * HeightRatio;
        h = Math.Max(h, w * HeightRatio);

        if (Math.Abs(w - _width) > 0.4 || Math.Abs(h - _height) > 0.4) Build(w, h);

        var size = new Size(w, h);
        base.MeasureOverride(size);
        return size;
    }

    // ─────────────────────────── 构建 ───────────────────────────

    private void Build(double width, double height)
    {
        _width = width;
        _height = height;
        _canvas.Children.Clear();

        double s = width / RefWidth;
        double designH = width * HeightRatio;

        // 比设计稿高出来的部分：四成让日期格子变高，其余平均分给「格子与详情之间」和「底部留白」，
        // 这样卡片底边对齐网格线，内部间距也不会出现一块空洞。
        double extra = Math.Max(0, height - designH);
        double bandExtra = extra * 0.4;
        double detailShift = extra * 0.7;

        _canvas.Width = width;
        _canvas.Height = height;

        double padText = 29 * s;          // 文字左缩进
        double padGrid = 6 * s;           // 网格左右留白
        double gapX = 3.2 * s;
        double cellW = (width - padGrid * 2 - gapX * 6) / 7;
        double pitch = cellW + gapX;

        double headerCy = 44.5 * s;
        double titleLeft = padText;
        double titleFont = UiStyles.CardTitleSize(width);
        double monthFont = 16 * s;
        double monthCx = width - 105 * s;
        double arrowOffset = 63 * s;

        double weekCy = 78 * s;
        double weekFont = 17 * s;

        double bandTop = 95 * s;
        double bandH = 170 * s + bandExtra;
        double gapY = 5 * s;

        double dayFont = 15.5 * s;
        double cellRadius = 7.5 * s;
        double ringPad = 2 * s;
        double ringThick = Math.Max(1.2, 2.5 * s);
        double ringRadius = 10.5 * s;

        double detail1Cy = 298 * s + detailShift;
        double detail1Font = 17 * s;
        double detail2Cy = 326.5 * s + detailShift;
        double detail2Font = 16 * s;

        // ── 头部：标题 ──
        var title = Text("日历", titleFont, Palette.TextPrimary, FontWeights.SemiBold);
        PutTextLeft(title, titleLeft, headerCy, titleFont);

        // ── 头部：月份切换 ──
        var monthText = Text(MonthTitle(), monthFont, Palette.TextSub, FontWeights.Normal);
        PutTextCenter(monthText, monthCx, headerCy, monthFont);
        monthText.Cursor = Cursors.Hand;
        monthText.MouseLeftButtonDown += (_, e) =>
        {
            if (!Interactive) return;
            e.Handled = true;
            GoToday();
        };
        ToolTipService.SetToolTip(monthText, "回到今天");

        PutArrow("‹", monthCx - arrowOffset, headerCy, s, -1);
        PutArrow("›", monthCx + arrowOffset, headerCy, s, 1);

        // ── 星期行 ──
        int weekStart = Cfg.Calendar.WeekStart == 0 ? 0 : 1;
        var labels = weekStart == 0 ? WeekSun : WeekMon;
        for (int i = 0; i < 7; i++)
        {
            var label = Text(labels[i], weekFont, Palette.TextTertiary, FontWeights.Normal);
            label.TextAlignment = TextAlignment.Center;
            label.Width = cellW;
            Put(label, padGrid + pitch * i, weekCy - weekFont / 2 + weekFont * TextBaselineFudge, cellW, weekFont);
        }

        // ── 日期网格 ──
        int offset = ((int)_month.DayOfWeek - weekStart + 7) % 7;
        int days = DateTime.DaysInMonth(_month.Year, _month.Month);
        int rows = Math.Max(1, (int)Math.Ceiling((offset + days) / 7.0));
        double cellH = (bandH + gapY) / rows - gapY;
        var start = _month.AddDays(-offset);

        var today = DateTime.Today;
        var nextMonth = _month.AddMonths(1);
        double ringX = 0, ringY = 0;
        bool hasRing = false;

        for (int i = 0; i < rows * 7; i++)
        {
            var date = start.AddDays(i);
            int col = i % 7;
            int row = i / 7;
            double x = padGrid + pitch * col;
            double y = bandTop + row * (cellH + gapY);

            var cell = CreateDayCell(date, today, nextMonth, cellH, cellRadius, dayFont);
            Put(cell, x, y, cellW, cellH);

            if (!hasRing && (date.Date == _selected.Date || date.Date == today))
            {
                hasRing = true;
                ringX = x;
                ringY = y;
            }
        }

        // ── 今天 / 选中：蓝色描边（画在格子外圈） ──
        if (hasRing)
        {
            bool isToday = _selected.Date == today;
            var ring = new Border
            {
                BorderBrush = Palette.AccentBlue,
                BorderThickness = new Thickness(isToday ? ringThick : Math.Max(1.1, ringThick * 0.8)),
                CornerRadius = new CornerRadius(ringRadius),
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Put(ring, ringX - ringPad, ringY - ringPad, cellW + ringPad * 2, cellH + ringPad * 2);
        }

        // ── 详情 ──
        int count = Cfg.CountOf(_selected);
        var line1 = Text(_selected.ToString("M月d日", Zh) + "你游玩了：", detail1Font, Palette.TextPrimary, FontWeights.SemiBold);
        PutTextLeft(line1, padText, detail1Cy, detail1Font);

        var line2 = Text(count > 0 ? $"共启动了 {count} 次。" : "这一天没有游玩记录。", detail2Font, Palette.TextTertiary, FontWeights.Normal);
        PutTextLeft(line2, padText, detail2Cy, detail2Font);
    }

    // ── 定位工具 ──

    private void Put(UIElement element, double x, double y, double w, double h)
    {
        element.Measure(new Size(Math.Max(w, 0), Math.Max(h, 0)));
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        if (element is FrameworkElement fe)
        {
            fe.Width = w;
            fe.Height = h;
        }

        _canvas.Children.Add(element);
    }

    private void PutTextLeft(TextBlock text, double left, double centerY, double font)
    {
        text.Measure(new Size(double.PositiveInfinity, font));
        Canvas.SetLeft(text, left);
        Canvas.SetTop(text, centerY - font / 2 + font * TextBaselineFudge);
        _canvas.Children.Add(text);
    }

    private void PutTextCenter(TextBlock text, double centerX, double centerY, double font)
    {
        text.Measure(new Size(double.PositiveInfinity, font));
        Canvas.SetLeft(text, centerX - text.DesiredSize.Width / 2);
        Canvas.SetTop(text, centerY - font / 2 + font * TextBaselineFudge);
        _canvas.Children.Add(text);
    }

    private void PutArrow(string glyph, double centerX, double centerY, double s, int delta)
    {
        double box = 22 * s;
        var text = Text(glyph, 16 * s, Palette.TextSub, FontWeights.Normal);
        text.TextAlignment = TextAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.Width = box;
        text.Opacity = 0.8;

        var host = new Border
        {
            Width = box,
            Height = box,
            Background = Brushes.Transparent,
            Child = text,
            Cursor = Cursors.Hand
        };
        host.MouseEnter += (_, _) => text.Opacity = 1;
        host.MouseLeave += (_, _) => text.Opacity = 0.8;
        host.MouseLeftButtonDown += (_, e) =>
        {
            if (!Interactive) return;
            e.Handled = true;
            ShiftMonth(delta);
        };

        Put(host, centerX - box / 2, centerY - box / 2, box, box);
    }

    private Border CreateDayCell(DateTime date, DateTime today, DateTime nextMonth,
        double cellH, double radius, double font)
    {
        int count = Cfg.CountOf(date);
        bool isNext = date >= nextMonth;
        bool isPrev = date < _month;

        var text = new TextBlock
        {
            Text = date.Day.ToString(),
            FontSize = font,
            LineHeight = font,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            // 让数字墨迹在格子里真正居中（行框中心比墨迹中心高约 0.21 em）
            Margin = new Thickness(0, 0.42 * font, 0, 0)
        };

        var cell = new Border
        {
            CornerRadius = new CornerRadius(radius),
            Background = Minimal ? Brushes.Transparent : Palette.CellFill,
            Child = text,
            Cursor = Cursors.Hand
        };

        if (!isNext && count > 0 && !Minimal)
        {
            var brush = HeatBrush(count);
            cell.Background = brush;
            text.Foreground = ContrastText(brush.Color);
            text.FontWeight = FontWeights.SemiBold;
        }
        else if (isNext)
        {
            text.Foreground = Palette.TextTertiary;
            text.Opacity = 0.62;
        }
        else if (isPrev)
        {
            text.Foreground = Palette.TextTertiary;
            text.Opacity = 0.9;
        }
        else
        {
            text.Foreground = Palette.TextPrimary;
        }

        cell.MouseLeftButtonDown += (_, e) =>
        {
            if (!Interactive) return;
            e.Handled = true;
            _selected = date;
            Rebuild();
        };
        ToolTipService.SetToolTip(cell, BuildTip(date, count, date.Date == today));
        ToolTipService.SetInitialShowDelay(cell, 300);

        _ = cellH;
        return cell;
    }

    private static TextBlock Text(string value, double size, Brush brush, FontWeight weight)
    {
        var text = new TextBlock
        {
            Text = value,
            FontSize = size,
            LineHeight = size,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            FontWeight = weight,
            TextWrapping = TextWrapping.NoWrap
        };
        if (brush != null) text.Foreground = brush;
        return text;
    }

    private string MonthTitle()
    {
        var format = string.IsNullOrWhiteSpace(Cfg.Calendar.HeaderFormat) ? "yyyy年M月" : Cfg.Calendar.HeaderFormat;
        try
        {
            return _month.ToString(format, Zh);
        }
        catch
        {
            return _month.ToString("yyyy年M月", Zh);
        }
    }

    // ─────────────────────────── 状态 ───────────────────────────

    public void ShiftMonth(int delta)
    {
        var target = _month.AddMonths(delta);
        if (target < MinMonth || target > MaxMonth) return;
        _month = target;
        Rebuild();
    }

    public void GoToday()
    {
        var today = DateTime.Today;
        _month = new DateTime(today.Year, today.Month, 1);
        _selected = today;
        Rebuild();
    }

    public void Rebuild()
    {
        double w = _width > 0 ? _width : RefWidth;
        double h = _height > 0 ? _height : w * HeightRatio;
        Build(w, h);
        InvalidateMeasure();
    }

    public void ApplyTheme() => Rebuild();

    private static string BuildTip(DateTime date, int count, bool isToday)
    {
        string head = date.ToString("M月d日 dddd", Zh);
        if (count <= 0) return head + "\n无启动记录";
        return head + (isToday ? "\n今天已启动 " : "\n已启动 ") + count + " 次";
    }

    /// <summary>启动次数 → 色阶档位（1 次最浅，8 次及以上最深），颜色跟着主题色走。</summary>
    private static SolidColorBrush HeatBrush(int count) => M3.Ramp(Math.Clamp(count, 1, 8) - 1, 8);

    private static Brush ContrastText(Color color)
    {
        double luma = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return luma > 0.62 ? Palette.Solid("#10202B") : Brushes.White;
    }
}

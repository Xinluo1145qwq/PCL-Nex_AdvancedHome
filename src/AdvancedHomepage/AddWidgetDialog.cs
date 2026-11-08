using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「添加高级主页小组件」窗口。
/// 左侧是分区导航栏（图标 + 分区名，图标直接落在栏底色上，不套底板），
/// 右侧上方是标题与说明，下方是所有分区的小组件目录，一排三个。
/// 点左侧分区把右侧滚到对应小节；右侧滚动时左侧选中项也跟着走。
/// </summary>
internal sealed class AddWidgetDialog : Window
{
    private sealed record Entry(string Key, string Title, string Desc, string Icon, string Category, bool Soon = false);

    private const string Basic = "basic";
    private const string Favorite = "favorite";
    private const string Pin = "pin";
    private const string Utility = "pcl";
    private const string Multi = "multi";

    private const double RailWidth = 96;
    private const int Columns = 3;

    private static readonly (string Key, string Short, string Name, string Icon)[] Categories =
    {
        (Basic, "基础", "基础", IconGrid),
        (Favorite, "收藏", "收藏内容", IconStar),
        (Multi, "多功能", "多功能小组件", IconSpark),
        (Pin, "快捷入口", "单项快捷入口", IconPin),
        (Utility, "实用功能", "PCL 实用功能", IconTool)
    };

    private readonly string _style;
    private readonly HashSet<string> _added;
    private string? _result;

    private readonly ScrollViewer _scroll = new();
    private readonly StackPanel _body = new();
    private readonly List<(string Key, FrameworkElement View)> _sections = new();
    private readonly Dictionary<string, RailItem> _rails = new();

    private string _active = Basic;
    private bool _syncing;
    private DispatcherTimer? _scrollTimer;
    private Border? _thumb;
    private Border? _fade;
    private DispatcherTimer? _thumbTimer;

    private AddWidgetDialog(string style, IEnumerable<string> added)
    {
        _style = Cfg.NormalizeStyle(style);
        _added = new HashSet<string>(added ?? Array.Empty<string>(), StringComparer.Ordinal);

        Dlg.Setup(this, "添加高级主页小组件", 860, scrim: true);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RailWidth) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(BuildRail());

        var content = BuildContent();
        Grid.SetColumn(content, 1);
        root.Children.Add(content);

        Dlg.Mount(this, Dlg.Shell(root, new Thickness(0)));

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
        };

        Loaded += (_, _) =>
        {
            if (_rails.Count > 0) Select(FirstCategory(), false);
            Dispatcher.BeginInvoke(new Action(UpdateThumb), DispatcherPriority.Loaded);
        };

        Closed += (_, _) =>
        {
            _scrollTimer?.Stop();
            _scrollTimer = null;
            _thumbTimer?.Stop();
            _thumbTimer = null;
        };
    }

    /// <summary>用户选中的条目（组件 id，或 pin:instance 这类），取消时为 null。</summary>
    public string? Result => _result;

    private static bool _open;

    public static string? Show(string style, IEnumerable<string> added)
    {
        if (_open) return null;

        _open = true;
        try
        {
            var dialog = new AddWidgetDialog(style, added);
            return dialog.ShowDialog() == true ? dialog.Result : null;
        }
        catch (Exception ex)
        {
            Log.Error("打开添加小组件窗口失败", ex);
            return null;
        }
        finally
        {
            _open = false;
        }
    }

    // ─────────────────────────── 左侧导航 ───────────────────────────

    private FrameworkElement BuildRail()
    {
        // 栏底色比弹窗底色亮一档（浅色主题下则暗一档），靠这一层把「导航区 / 内容区」分开
        var fill = M3.Layer(M3.Of(M3.C.SurfaceHigh), M3.Of(M3.C.OnSurface), 0.07);

        var stack = new StackPanel { Margin = new Thickness(0, 14, 0, 10) };
        var entries = Entries();

        foreach (var category in Categories)
        {
            if (!entries.Any(e => e.Category == category.Key)) continue;

            var item = new RailItem(category.Icon, category.Short, () => Select(category.Key, true));
            _rails[category.Key] = item;
            stack.Children.Add(item.Root);
        }

        return new Border
        {
            Background = fill,
            CornerRadius = new CornerRadius(M3.DialogRadius, 0, 0, M3.DialogRadius),
            BorderThickness = new Thickness(0, 0, 1, 0),
            BorderBrush = M3.C.OutlineVariant,
            Child = stack
        };
    }

    private string FirstCategory()
    {
        foreach (var category in Categories)
        {
            if (_rails.ContainsKey(category.Key)) return category.Key;
        }

        return Basic;
    }

    /// <summary>选中分区：左侧高亮，右侧滚到对应小节。</summary>
    private void Select(string key, bool scroll)
    {
        if (!_rails.ContainsKey(key)) return;
        _active = key;

        foreach (var (other, item) in _rails) item.SetActive(other == key);

        if (!scroll) return;

        var view = _sections.FirstOrDefault(s => s.Key == key).View;
        if (view == null) return;

        double target = view.TransformToAncestor(_body).Transform(new Point(0, 0)).Y;
        AnimateScroll(target);
    }

    private void AnimateScroll(double target)
    {
        target = Math.Clamp(target, 0, Math.Max(0, _scroll.ScrollableHeight));

        double start = _scroll.VerticalOffset;
        if (Math.Abs(target - start) < 0.5) return;

        _syncing = true;
        var begin = DateTime.UtcNow;
        const double duration = 240;

        _scrollTimer?.Stop();
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        _scrollTimer.Tick += (_, _) =>
        {
            double t = (DateTime.UtcNow - begin).TotalMilliseconds / duration;

            if (t >= 1)
            {
                _scrollTimer?.Stop();
                _scroll.ScrollToVerticalOffset(target);
                _syncing = false;
                return;
            }

            double eased = 1 - Math.Pow(1 - t, 3);
            _scroll.ScrollToVerticalOffset(start + (target - start) * eased);
        };
        _scrollTimer.Start();
    }

    /// <summary>右侧滚动时让左侧跟着高亮（程序滚动期间不抢）。</summary>
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateThumb();

        if (_syncing || _sections.Count == 0) return;

        double mark = _scroll.VerticalOffset + 16;
        string key = _sections[0].Key;

        foreach (var (sectionKey, view) in _sections)
        {
            double y = view.TransformToAncestor(_body).Transform(new Point(0, 0)).Y;
            if (y <= mark) key = sectionKey;
            else break;
        }

        // 滚到底时最后一节可能永远顶不到最上面，直接算它选中
        if (_scroll.VerticalOffset >= _scroll.ScrollableHeight - 2) key = _sections[^1].Key;

        if (key == _active) return;
        _active = key;
        foreach (var (other, item) in _rails) item.SetActive(other == key);
    }

    // ─────────────────────────── 右侧内容 ───────────────────────────

    private FrameworkElement BuildContent()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = Dlg.Header("添加高级主页小组件",
            () => DialogResult = false,
            "将一个组件放到主页上，加完可以拖动、缩放、删掉。");
        header.Margin = new Thickness(0, 0, 0, 14);
        grid.Children.Add(header);

        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.MaxHeight = 430;
        _scroll.Margin = new Thickness(0, 0, -8, 0);
        _scroll.Padding = new Thickness(0, 0, 8, 0);
        _scroll.Focusable = false;
        _scroll.ScrollChanged += OnScrollChanged;
        _scroll.Content = _body;

        // 启动器自带的滚动条是一条亮白粗条，放在这里太抢眼。
        // 换 ScrollBar 模板会踩 WPF FrameworkElementFactory 的命名坑，
        // 所以干脆藏掉原生条，自己画一根 4dp 圆角指示条，跟着 ScrollChanged 走位。
        var host = new Grid();
        host.Children.Add(_scroll);

        _thumb = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Background = M3.C.OutlineVariant,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 1, 0),
            Opacity = 0,
            IsHitTestVisible = false,
            RenderTransform = new TranslateTransform()
        };
        host.Children.Add(_thumb);

        _thumbTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _thumbTimer.Tick += (_, _) =>
        {
            _thumbTimer?.Stop();
            if (_thumb == null) return;
            _thumb.BeginAnimation(OpacityProperty, new DoubleAnimation(0, M3.FastEffects)
            {
                EasingFunction = M3.Effects
            });
        };

        Grid.SetRow(host, 1);
        grid.Children.Add(host);

        BuildSections();

        // 底部渐隐：卡片被窗口裁掉一条时，给个「下面还有内容」的提示。
        // 挂在内容区最外层（而不是滚动区），这样它的位置和「看得见的底边」永远一致；
        // 滚动到底自动收起，也不挡点击。
        var cardColor = M3.Of(M3.DialogCard);
        _fade = new Border
        {
            Height = 26,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, -16),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Background = new LinearGradientBrush(
                Color.FromArgb(0, cardColor.R, cardColor.G, cardColor.B),
                cardColor,
                new Point(0.5, 0),
                new Point(0.5, 1))
        };

        var outer = new Grid();
        outer.Children.Add(grid);
        outer.Children.Add(_fade);

        return new Border
        {
            Padding = new Thickness(24, 20, 18, 16),
            Child = outer
        };
    }

    /// <summary>按滚动比例摆好指示条：长度随可见比例变化，最短 28dp，停手 0.7 秒淡出。</summary>
    private void UpdateThumb()
    {
        if (_thumb == null) return;

        double viewport = _scroll.ViewportHeight;
        double extent = _scroll.ExtentHeight;

        // 底部还有没露出来的内容才显示渐隐
        if (_fade != null)
        {
            _fade.Visibility = extent > viewport + 1 && _scroll.VerticalOffset < extent - viewport - 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (viewport <= 0 || extent <= viewport + 1)
        {
            _thumb.Opacity = 0;
            return;
        }

        double height = Math.Max(28, viewport * viewport / extent);
        double travel = viewport - height;
        double max = Math.Max(1, extent - viewport);
        double y = Math.Clamp(_scroll.VerticalOffset / max, 0, 1) * travel;

        _thumb.Height = height;
        if (_thumb.RenderTransform is TranslateTransform move) move.Y = y;

        _thumb.BeginAnimation(OpacityProperty, new DoubleAnimation(1, M3.FastEffects)
        {
            EasingFunction = M3.Effects
        });

        _thumbTimer?.Stop();
        _thumbTimer?.Start();
    }

    private void BuildSections()
    {
        var entries = Entries();
        bool first = true;

        foreach (var category in Categories)
        {
            var items = entries.Where(e => e.Category == category.Key).ToList();
            if (items.Count == 0) continue;

            var section = new StackPanel
            {
                Margin = new Thickness(0, first ? 0 : 22, 0, 0)
            };
            first = false;

            var title = M3.Text(category.Name, M3.TypeRole.TitleSmall, M3.C.OnSurface, 1, emphasized: true);
            // 标题和它的卡片要贴得比「卡片之间」更近，否则分组归属看不出来
            title.Margin = new Thickness(5, 0, 5, 12);
            section.Children.Add(title);

            var grid = new Grid();
            for (int i = 0; i < Columns; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < items.Count; i++)
            {
                int row = i / Columns;
                while (grid.RowDefinitions.Count <= row)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var card = BuildCard(items[i]);
                Grid.SetColumn(card, i % Columns);
                Grid.SetRow(card, row);
                grid.Children.Add(card);
            }

            section.Children.Add(grid);
            _body.Children.Add(section);
            _sections.Add((category.Key, section));
        }
    }

    private FrameworkElement BuildCard(Entry entry)
    {
        bool added = _added.Contains(entry.Key);

        var body = new StackPanel();

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new Path
        {
            Data = Geometry.Parse(entry.Icon),
            Stroke = added ? M3.C.OnSurfaceVariant : M3.C.Primary,
            // 已添加的只弱化图标，标题保持满色，别整张卡糊成一片灰
            Opacity = added ? 0.45 : 1,
            StrokeThickness = 1.7,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        top.Children.Add(Dlg.Tile(icon, 38, M3.CornerM));

        if (added || entry.Soon)
        {
            var chip = BuildChip(added ? "已添加" : "开发中", entry.Soon && !added, check: added);
            chip.HorizontalAlignment = HorizontalAlignment.Right;
            chip.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(chip, 1);
            top.Children.Add(chip);
        }

        body.Children.Add(top);

        var title = M3.Text(entry.Title, M3.TypeRole.TitleSmall, M3.C.OnSurface, 1, emphasized: true);
        title.TextWrapping = TextWrapping.Wrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Margin = new Thickness(0, 11, 0, 0);
        body.Children.Add(title);

        var desc = M3.Text(entry.Desc, M3.TypeRole.BodySmall, M3.C.OnSurfaceVariant);
        desc.TextWrapping = TextWrapping.Wrap;
        desc.TextTrimming = TextTrimming.CharacterEllipsis;
        desc.LineHeight = 16;
        desc.Height = 32;
        desc.Margin = new Thickness(0, 3, 0, 0);
        desc.Opacity = added ? 0.55 : 1;
        body.Children.Add(desc);

        var card = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            BorderThickness = new Thickness(1),
            BorderBrush = M3.C.OutlineVariant,
            Background = M3.DialogCard,
            Padding = new Thickness(16, 16, 16, 14),
            Margin = new Thickness(4),
            Cursor = added ? Cursors.Arrow : Cursors.Hand,
            Child = body,
            ToolTip = added ? UiStyles.Tooltip("已经在主页上了") : null
        };

        if (added)
        {
            return card;
        }

        card.MouseEnter += (_, _) =>
        {
            card.Background = M3.Layer(M3.Of(M3.DialogCard), M3.Of(M3.C.Primary), M3.HoverAlpha);
            card.BorderBrush = M3.C.Primary;
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = M3.DialogCard;
            card.BorderBrush = M3.C.OutlineVariant;
        };
        card.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            _result = entry.Key;
            DialogResult = true;
        };

        return card;
    }

    private static Border BuildChip(string text, bool accent, bool check = false)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (check)
        {
            content.Children.Add(new Path
            {
                Data = Geometry.Parse("M1,4.5 L3.4,7 L8,1.5"),
                Stroke = M3.C.OnSurfaceVariant,
                StrokeThickness = 1.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Width = 9,
                Height = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.Medium,
            Foreground = accent ? M3.C.OnTertiaryContainer : M3.C.OnSurfaceVariant
        });

        return new Border
        {
            CornerRadius = new CornerRadius(M3.CornerS),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(6, -2, 0, 0),
            Background = accent ? M3.C.TertiaryContainer : M3.C.SurfaceContainerHighest,
            Child = content
        };
    }

    // ─────────────────────────── 导航条目 ───────────────────────────

    /// <summary>
    /// 一个分区条目，按 M3 NavigationRail 的做法：图标背后是 56×32 的 pill 指示器，
    /// 选中时填 secondary container、图标换 on secondary container，文字挂在 pill 下方。
    /// </summary>
    private sealed class RailItem
    {
        public Border Root { get; }
        private readonly Path _icon;
        private readonly TextBlock _label;
        private readonly Border _pill;
        private bool _active;

        public RailItem(string iconData, string label, Action onClick)
        {
            _icon = new Path
            {
                Data = Geometry.Parse(iconData),
                Stroke = M3.C.OnSurfaceVariant,
                StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Width = 22,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            _pill = new Border
            {
                Width = 56,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = Brushes.Transparent,
                Child = _icon,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            };

            _label = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = M3.C.OnSurfaceVariant,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                IsHitTestVisible = false
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(_pill);
            stack.Children.Add(_label);

            var hover = M3.Layer(M3.Of(M3.C.SurfaceHigh), M3.Of(M3.C.OnSurface), 0.06);

            Root = new Border
            {
                Height = 72,
                Margin = new Thickness(8, 2, 8, 2),
                CornerRadius = new CornerRadius(16),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = stack
            };

            Root.MouseEnter += (_, _) => { if (!_active) Root.Background = hover; };
            Root.MouseLeave += (_, _) => { if (!_active) Root.Background = Brushes.Transparent; };
            Root.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
        }

        public void SetActive(bool active)
        {
            _active = active;
            _pill.Background = active ? M3.C.SecondaryContainer : Brushes.Transparent;
            _icon.Stroke = active ? M3.C.OnSecondaryContainer : M3.C.OnSurfaceVariant;
            _label.Foreground = active ? M3.C.OnSurface : M3.C.OnSurfaceVariant;
            _label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            if (active) Root.Background = Brushes.Transparent;
        }
    }

    // ─────────────────────────── 目录 ───────────────────────────

    private List<Entry> Entries()
    {
        var list = new List<Entry>();

        if (_style == "minimal")
        {
            list.Add(new Entry("minimal", "标语卡片", "居中的日期与标语轮播。", IconSlogan, Basic));
        }
        else if (_style == "notes")
        {
            list.Add(new Entry("note", "便签", "随手记一句话，自动保存。", IconNote, Basic));
            list.Add(new Entry("todo", "待办事项", "勾掉一条少一条，带完成进度。", IconTodo, Basic));
        }
        else
        {
            list.Add(new Entry("greeting", "问候", "日期 + 一句话问候，背景透明。", IconGreeting, Basic));
            list.Add(new Entry("worlds", "最近游玩的世界", "最近打开过的存档，一键进入。", IconCompass, Basic));
            list.Add(new Entry("instances", "最近游玩的实例", "最近启动过的实例，一键开玩。", IconCube, Basic));
        }

        list.Add(new Entry("pin:instance", "单个实例入口", "固定一个实例，点击直接启动。", IconPinInstance, Pin));
        list.Add(new Entry("pin:world", "单个世界入口", "固定一个存档，点击直接进入。", IconPinWorld, Pin));
        list.Add(new Entry("pin:server", "单个服务器入口", "固定一个服务器，点击直接联机。", IconPinServer, Pin));

        list.Add(new Entry("skin", "皮肤预览", "3D 看皮肤：跟随鼠标、走路、拖动转身。", IconSkin, Favorite));
        list.Add(new Entry("pin:site", "单个网站", "固定一个网址，点击用浏览器打开。", IconPinSite, Favorite));
        list.Add(new Entry("sites", "多个网站", "收藏的网址列表，点一行就打开。", IconGlobe, Favorite));

        list.Add(new Entry("download", "下载自定义文件", "填个地址，用启动器的多线程引擎下载任意文件。", IconDownload, Utility));

        if (_style == "fancy")
        {
            list.Add(new Entry("calendar", "日历", "按月显示启动次数热力。", IconCalendar, Multi));
            list.Add(new Entry("clock", "时钟", "M3 大字时间，可切电子钟、开关日期与秒。", IconClock, Multi));
            list.Add(new Entry("weather", "天气", "当前天气与温度，可切成铺满样式。", IconWeather, Multi));
        }

        list.Add(new Entry("calculator", "计算器", "四则运算、括号、百分号，带可回填的历史记录。", IconCalculator, Multi));

        return list;
    }

    // ─────────────────────────── 图标 ───────────────────────────

    private const string IconGrid = "M4,4.5 H9.5 V10 H4 Z M14.5,4.5 H20 V10 H14.5 Z M4,14 H9.5 V19.5 H4 Z M14.5,14 H20 V19.5 H14.5 Z";
    private const string IconStar = "M12,3.8 L14.5,9 L20.2,9.8 L16,13.9 L17,19.6 L12,16.9 L7,19.6 L8,13.9 L3.8,9.8 L9.5,9 Z";
    private const string IconSpark = "M12,3 V21 M4.2,7.5 L19.8,16.5 M19.8,7.5 L4.2,16.5";
    private const string IconPin = "M9,3.5 H15 L14.2,9.5 L17.5,12.5 H6.5 L9.8,9.5 Z M12,12.5 V20.5";
    private const string IconTool = "M4,8.5 H20 V19.5 H4 Z M9,8.5 V5.5 H15 V8.5 M4,13.5 H20";

    private const string IconCalendar = "M4,6.5 H20 V20 H4 Z M4,10.5 H20 M8,3 V7 M16,3 V7";
    private const string IconGreeting = "M4,5 H20 V16 H11 L6,20 V16 H4 Z";
    private const string IconCompass = "M12,3 A9,9 0 1,1 11.99,3 Z M15.6,8.4 L13.4,13.4 L8.4,15.6 L10.6,10.6 Z";
    private const string IconCube = "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z M4,7.5 L12,12 L20,7.5 M12,12 L12,21";
    private const string IconSlogan = "M6,7 H18 M6,12 H15 M6,17 H10";
    private const string IconNote = "M5,4 H19 V14.5 L14.5,20 H5 Z M14.5,20 V14.5 H19 M8.5,9 H15.5 M8.5,12.5 H13";
    private const string IconTodo = "M4,6.5 H6.5 V9 H4 Z M9,7.75 H20 M4,14.5 H6.5 V17 H4 Z M9,15.75 H20";
    private const string IconPinInstance = "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z M10,9 L15.5,12 L10,15 Z";
    private const string IconPinWorld = "M4,6 L9,4 L15,6 L20,4 V18 L15,20 L9,18 L4,18 Z M9,4 V18 M15,6 V20";
    private const string IconPinServer = "M6,20 V15 M10,20 V11 M14,20 V7 M18,20 V3";
    private const string IconPinSite = "M12,3 A9,9 0 1,1 11.99,3 Z M3,12 H21 M12,3 C15.4,6.4 15.4,17.6 12,21 C8.6,17.6 8.6,6.4 12,3";
    private const string IconSkin = "M12,3.2 A3.8,3.8 0 1,1 11.99,3.2 Z M4.6,20.4 C4.6,16.6 8,14.6 12,14.6 C16,14.6 19.4,16.6 19.4,20.4";
    private const string IconGlobe = "M12,3 A9,9 0 1,1 11.99,3 Z M3,12 H21 M12,3 C15.4,6.4 15.4,17.6 12,21 C8.6,17.6 8.6,6.4 12,3";
    private const string IconDownload = "M12,3 V15 M7.5,10.5 L12,15 L16.5,10.5 M4,19 H20";

    private const string IconClock = "M12,3 A9,9 0 1,1 11.99,3 Z M12,7 V12 L15.5,14";

    private const string IconWeather =
        "M7,17.5 H17.2 A3.8,3.8 0 0,0 17.2,9.9 A5.4,5.4 0 0,0 6.6,11.1 A3.4,3.4 0 0,0 7,17.5 Z M9,20.5 V22 M13,20.5 V22";

    private const string IconCalculator = "M6,3 H18 V21 H6 Z M9,6.5 H15 M9,11 H10 M13,11 H14 M9,14.5 H10 M13,14.5 H14 M9,18 H10 M13,18 H14";
}

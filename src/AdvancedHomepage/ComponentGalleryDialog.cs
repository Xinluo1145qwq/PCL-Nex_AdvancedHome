using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 组件库：把高级主页里用到的 M3 组件摊开摆一遍——卡片、按钮、弹窗、界面组件。
/// 弹窗由按钮真的弹出来，开关/滑块/分段按钮也能点，用来确认主题换色后这些控件没有跑偏。
/// 纯展示，不读写主页配置。
/// </summary>
internal sealed class ComponentGalleryDialog : Window
{
    private const string IconGrid =
        "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z";
    private const string IconStar =
        "M12,4 L14.3,9.3 L20,9.9 L15.7,13.8 L16.9,19.5 L12,16.6 L7.1,19.5 L8.3,13.8 L4,9.9 L9.7,9.3 Z";
    private const string IconNote = "M6,4.5 H14.5 L18,8 V19.5 H6 Z M14.5,4.5 V8 H18 M9,12 H15 M9,15.5 H15";
    private const string IconSearch = "M10.5,4.5 A6,6 0 1 1 10.4,4.5 Z M15.1,15.1 L20,20";
    private const string IconSettings =
        "M12,9 A3,3 0 1 1 11.9,9 Z M12,3.6 L12,5.4 M12,18.6 L12,20.4 M3.6,12 L5.4,12 M18.6,12 L20.4,12 " +
        "M6.1,6.1 L7.4,7.4 M16.6,16.6 L17.9,17.9 M17.9,6.1 L16.6,7.4 M7.4,16.6 L6.1,17.9";
    private const string IconPlus = "M12,5 V19 M5,12 H19";
    private const string IconChevron = "M9,6 L15,12 L9,18";
    private const string IconDownload = "M12,4.5 V15 M8,11 L12,15 L16,11 M5,16.5 V19.5 H19 V16.5";

    private readonly StackPanel _content = new();
    private Grid _overlay = null!;
    private TextBlock _status = null!;
    private double _sliderValue = 60;

    private ComponentGalleryDialog()
    {
        Dlg.Setup(this, "组件库", 1040, scrim: true);
        SizeToContent = SizeToContent.Manual;
        ResizeMode = ResizeMode.NoResize;

        var (width, height) = FitSize();
        var root = new Grid { Width = width, Height = height };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(BuildHeader());

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Content = _content,
            Margin = new Thickness(32, 20, 18, 26)
        };

        try
        {
            var bar = UiSkin.ThinScrollBar();
            if (bar != null) scroll.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = bar;
        }
        catch (Exception ex)
        {
            Log.Warn("组件库滚动条样式失败：" + ex.Message);
        }

        var host = new Grid();
        host.Children.Add(scroll);
        Grid.SetRow(host, 1);
        root.Children.Add(host);

        // 浮层容器：底部面板 / scrim 都挂在这里，盖住整个窗口
        _overlay = new Grid { Visibility = Visibility.Collapsed };
        Grid.SetRowSpan(_overlay, 2);
        root.Children.Add(_overlay);

        _content.Margin = new Thickness(0, 0, 14, 0);
        _content.Children.Add(Section("卡片", "M3 三种卡片：filled / elevated / outlined，圆角 12dp。",
            BuildCardSamples()));
        _content.Children.Add(Section("按钮",
            "五种变体 + 图标按钮 + FAB。静止是 12dp 圆角矩形，鼠标移上去圆角弹到全圆角（shape morph），按下还会回缩一点。",
            BuildButtonSamples()));
        _content.Children.Add(Section("加载",
            "MD3 的不确定进度不是原地转圈：1333ms 一圈，弧长在一个周期里从 10° 涨到 288° 再收回去。" +
            "波浪是 Expressive 的写法，轨道和进度都带正弦波。",
            BuildLoadingSamples()));
        _content.Children.Add(Section("界面组件",
            "列表、开关、滑块、分段按钮、输入框（填充 / 描边两种），以及筛选和输入 chip。",
            BuildControlSamples()));
        _content.Children.Add(Section("更多组件",
            "单选、复选、徽标、标签页、扩展面板和提示条，都能点。",
            BuildMoreSamples()));
        _content.Children.Add(Section("导航",
            "Top App Bar、Navigation Bar、Navigation Rail：选中项是 32dp 高的药丸指示器，切换有底色过渡。",
            BuildNavigationSamples()));
        _content.Children.Add(Section("浮层",
            "下拉菜单是真的 Popup（会跟着按钮定位），底部面板从窗口下沿滑上来，提示气泡走系统 ToolTip。",
            BuildOverlaySamples()));
        _content.Children.Add(Section("动效",
            "四条官方缓动曲线、container transform、shared axis、交错入场——点「播放」或卡片就能看。",
            BuildMotionSamples()));
        _content.Children.Add(Section("弹窗", "对话框由下面的按钮触发，都走同一个 M3 外壳。",
            BuildDialogSamples()));

        var shell = Dlg.Shell(root, new Thickness(0));
        shell.Background = UiSkin.Canvas;
        Dlg.Mount(this, shell);
        shell.Margin = new Thickness(20);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    public static void Show(Window? owner)
    {
        try
        {
            var dialog = new ComponentGalleryDialog();
            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Error("打开组件库失败", ex);
        }
    }

    private (double Width, double Height) FitSize()
    {
        double width = 1040, height = 760;

        double hostWidth = 0, hostHeight = 0;
        try
        {
            var owner = PCL.ModMain.frmMain;
            if (owner != null)
            {
                hostWidth = owner.ActualWidth;
                hostHeight = owner.ActualHeight;
            }
        }
        catch
        {
            // 拿不到宿主尺寸就用默认值
        }

        if (hostWidth > 420) width = Math.Min(width, hostWidth - 40);
        if (hostHeight > 420) height = Math.Min(height, hostHeight - 40);

        return (Math.Max(820, width), Math.Max(560, height));
    }

    private FrameworkElement BuildHeader()
    {
        var grid = new Grid { Margin = new Thickness(34, 28, 22, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "组件库",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink
        });
        stack.Children.Add(new TextBlock
        {
            Text = "高级主页用到的 Material 3 组件实例。按钮、开关、滑块都能点，弹窗会真的弹出来。",
            FontSize = 12.5,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim
        });
        grid.Children.Add(stack);

        var close = M3IconButton.Create("M6,6 L18,18 M18,6 L6,18", Close, "关闭", scheme: UiSkin.L);
        Grid.SetColumn(close, 1);
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(12, 0, 0, 0);
        grid.Children.Add(close);

        return grid;
    }

    // ─────────────────────────── 分区 ───────────────────────────

    private FrameworkElement Section(string title, string desc, FrameworkElement body)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 30) };

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink,
            Margin = new Thickness(2, 0, 0, 0)
        });

        stack.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 12,
            Foreground = UiSkin.Dim,
            Margin = new Thickness(2, 4, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(body);
        return stack;
    }

    private static Border Panel(FrameworkElement content, Thickness padding)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(M3.CornerL),
            Background = UiSkin.Card,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.Line,
            Padding = padding,
            Child = content
        };
    }

    // ─────────────────────────── 卡片 ───────────────────────────

    private FrameworkElement BuildCardSamples()
    {
        var grid = new UniformGrid { Columns = 3 };
        grid.Children.Add(CardSample("Filled", "surface container highest 底，没有阴影。",
            UiSkin.L.SurfaceHighest, false, false));
        grid.Children.Add(CardSample("Elevated", "白底 + 1 级阴影，比 filled 更「浮」。",
            UiSkin.Card, true, false));
        grid.Children.Add(CardSample("Outlined", "白底 + 1px 描边，最克制的一种。",
            UiSkin.Card, false, true));

        return new Border { Child = grid };
    }

    private FrameworkElement CardSample(string title, string desc, Brush background, bool elevated, bool outlined)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        head.Children.Add(new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = UiSkin.L.PrimaryContainer,
            Child = M3.Icon(IconStar, UiSkin.L.OnPrimaryContainer, 20),
            Margin = new Thickness(0, 0, 10, 0)
        });

        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink
        });
        titles.Children.Add(new TextBlock
        {
            Text = "Card",
            FontSize = 11,
            Foreground = UiSkin.Dim
        });
        head.Children.Add(titles);

        var body = new StackPanel();
        body.Children.Add(head);
        body.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 12,
            LineHeight = 18,
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim
        });

        var action = new TextBlock
        {
            Text = "查看详情",
            FontSize = 12.5,
            FontWeight = FontWeights.Medium,
            Foreground = UiSkin.L.Primary,
            Margin = new Thickness(0, 14, 0, 0)
        };
        body.Children.Add(action);

        return new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = background,
            BorderThickness = new Thickness(outlined ? 1 : 0),
            BorderBrush = outlined ? UiSkin.L.OutlineVariant : Brushes.Transparent,
            Padding = new Thickness(16, 15, 16, 14),
            Margin = new Thickness(0, 0, 10, 0),
            Effect = elevated ? M3.Elevation(1) : null,
            Child = body
        };
    }

    // ─────────────────────────── 按钮 ───────────────────────────

    private FrameworkElement BuildButtonSamples()
    {
        var stack = new StackPanel();

        var row1 = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        row1.Children.Add(Named("Filled",
            M3Button.Create("Filled", () => Report("Filled 按钮"), M3ButtonKind.Filled, scheme: UiSkin.L)));
        row1.Children.Add(Named("Tonal",
            M3Button.Create("Tonal", () => Report("Tonal 按钮"), M3ButtonKind.Tonal, scheme: UiSkin.L)));
        row1.Children.Add(Named("Outlined",
            M3Button.Create("Outlined", () => Report("Outlined 按钮"), M3ButtonKind.Outlined, scheme: UiSkin.L)));
        row1.Children.Add(Named("Text",
            M3Button.Create("Text", () => Report("Text 按钮"), M3ButtonKind.Text, scheme: UiSkin.L)));
        row1.Children.Add(Named("Elevated",
            M3Button.Create("Elevated", () => Report("Elevated 按钮"), M3ButtonKind.Elevated, scheme: UiSkin.L)));
        row1.Children.Add(Named("Disabled",
            M3Button.Create("Disabled", null, M3ButtonKind.Filled, enabled: false, scheme: UiSkin.L)));

        stack.Children.Add(row1);

        var divider = new Border { Height = 1, Background = UiSkin.Line, Margin = new Thickness(0, 14, 0, 14) };
        stack.Children.Add(divider);

        var row2 = new WrapPanel();
        row2.Children.Add(Named("图标按钮", M3IconButton.Create(IconPlus, () => Report("新增"), "新增", scheme: UiSkin.L)));
        row2.Children.Add(Named("Filled 图标",
            M3IconButton.Create(IconDownload, () => Report("下载"), "下载", filled: true, scheme: UiSkin.L)));
        row2.Children.Add(Named("危险图标",
            M3IconButton.Create("M6,6 L18,18 M18,6 L6,18", () => Report("删除"), "删除", danger: true, scheme: UiSkin.L)));
        row2.Children.Add(Named("Extended FAB",
            M3Fab.Create("新建", IconPlus, () => Report("Extended FAB"), scheme: UiSkin.L)));

        stack.Children.Add(row2);

        _status = new TextBlock
        {
            Text = "点上面的按钮，这里会显示最后一个动作。",
            FontSize = 11.5,
            Margin = new Thickness(0, 16, 0, 0),
            Foreground = UiSkin.Dim
        };
        stack.Children.Add(_status);

        return Panel(stack, new Thickness(20, 18, 20, 18));
    }

    private static FrameworkElement Named(string label, FrameworkElement control)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 10.5,
            Foreground = UiSkin.Dim,
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var stack = new StackPanel { Margin = new Thickness(0, 0, 18, 6) };
        control.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(control);
        stack.Children.Add(text);
        return stack;
    }

    private void Report(string what)
    {
        if (_status == null) return;
        _status.Text = "刚刚点了：" + what;
    }

    // ─────────────────────────── 弹窗 ───────────────────────────

    private FrameworkElement BuildDialogSamples()
    {
        var row = new WrapPanel();

        var basic = M3Button.Create("基础弹窗", () =>
        {
            bool ok = OpenSample("基础弹窗",
                "M3 的基础对话框：标题、正文、右下角一个「知道了」。整块走同一个外壳，圆角 16dp。",
                "知道了", false, false);
            if (ok) Report("基础弹窗 → 知道了");
        }, M3ButtonKind.Filled, scheme: UiSkin.L);

        var confirm = M3Button.Create("确认弹窗", () =>
        {
            bool ok = OpenSample("删除这个组件？",
                "删除后可以再从「添加小组件」里加回来，但这张卡片上的设置会一起丢掉。",
                "删除", true, true);
            Report(ok ? "确认弹窗 → 删除" : "确认弹窗 → 取消");
        }, M3ButtonKind.Tonal, scheme: UiSkin.L);

        var list = M3Button.Create("选择弹窗", () =>
        {
            bool ok = OpenSample("选择主页样式",
                "花样：默认的 9 列卡片墙。\n极简：一整块日期加一行标语。\n便签：便签与待办清单。",
                "使用花样", false, true);
            Report(ok ? "选择弹窗 → 确定" : "选择弹窗 → 取消");
        }, M3ButtonKind.Outlined, scheme: UiSkin.L);

        row.Children.Add(Pad(basic, 0, 0, 10, 0));
        row.Children.Add(Pad(confirm, 0, 0, 10, 0));
        row.Children.Add(list);

        return Panel(row, new Thickness(20, 18, 20, 18));
    }

    private static FrameworkElement Pad(FrameworkElement element, double l, double t, double r, double b)
    {
        element.Margin = new Thickness(l, t, r, b);
        return element;
    }

    /// <summary>弹一个小窗，确认返回 true。</summary>
    private bool OpenSample(string title, string body, string primary, bool danger, bool withCancel)
    {
        var window = new Window { ShowInTaskbar = false };
        Dlg.Setup(window, title, 460);
        window.Owner = this;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Width = 392 };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink,
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = body,
            FontSize = 13,
            LineHeight = 20,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 26, 0, 0)
        };

        if (withCancel)
        {
            actions.Children.Add(M3Button.Create("取消", () => window.DialogResult = false,
                M3ButtonKind.Text, scheme: UiSkin.L));
        }

        var confirm = M3Button.Create(primary, () => window.DialogResult = true,
            M3ButtonKind.Filled, danger: danger, scheme: UiSkin.L);
        confirm.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(confirm);
        panel.Children.Add(actions);

        var surface = new Border
        {
            CornerRadius = new CornerRadius(M3.DialogRadius),
            Background = UiSkin.Card,
            Padding = new Thickness(26, 24, 22, 20),
            Child = panel,
            Effect = M3.Elevation(3)
        };

        surface.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.94, 0.94);
        surface.RenderTransform = scale;
        surface.Opacity = 0;
        surface.Loaded += (_, _) =>
        {
            surface.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, M3.FastEffects) { EasingFunction = M3.Effects });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
        };

        Dlg.Mount(window, surface);
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) window.DialogResult = false;
        };

        return window.ShowDialog() == true;
    }

    // ─────────────────────────── 界面组件 ───────────────────────────

    private FrameworkElement BuildControlSamples()
    {
        var body = new StackPanel();

        body.Children.Add(Demo("搜索栏", BuildSearchSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("过滤 Chip", BuildChips()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("开关", BuildSwitchSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("滑块", BuildSliderSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("分段按钮", new M3Segmented(new[] { "日", "周", "月" }, 1, _ => { },
            scheme: UiSkin.L) { HorizontalAlignment = HorizontalAlignment.Left }));
        body.Children.Add(Divider());
        body.Children.Add(Demo("文本输入", BuildTextField()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("列表项", BuildListSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("描边输入", BuildOutlinedField()));

        return Panel(body, new Thickness(20, 8, 20, 8));
    }

    private static Border Divider() => new()
    {
        Height = 1,
        Background = UiSkin.Line,
        Margin = new Thickness(0, 2, 0, 2)
    };

    private static FrameworkElement Demo(string label, FrameworkElement control)
    {
        var grid = new Grid { MinHeight = 64, Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center
        });

        control.VerticalAlignment = VerticalAlignment.Center;
        control.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    private FrameworkElement BuildSearchSample()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = M3.Icon(IconSearch, UiSkin.Dim, 18);
        icon.Margin = new Thickness(14, 0, 0, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(icon);

        var hint = new TextBlock
        {
            Text = "搜索组件",
            FontSize = 13,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(hint, 1);
        grid.Children.Add(hint);

        return new Border
        {
            Width = 320,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = UiSkin.L.SurfaceContainerHigh,
            Child = grid
        };
    }

    private FrameworkElement BuildChips()
    {
        var filter = new StackPanel { Orientation = Orientation.Horizontal };
        filter.Children.Add(Chip("全部", true));
        filter.Children.Add(Chip("已启用", false));
        filter.Children.Add(Chip("隐藏", false));

        var input = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var first = InputChip("模组");
        var second = InputChip("光影");
        var restore = M3Button.Create("恢复", () =>
        {
            first.Visibility = Visibility.Visible;
            second.Visibility = Visibility.Visible;
            first.Opacity = 1;
            second.Opacity = 1;
        }, M3ButtonKind.Text, scale: 0.8, scheme: UiSkin.L);
        restore.Margin = new Thickness(4, 0, 0, 0);
        input.Children.Add(first);
        input.Children.Add(second);
        input.Children.Add(restore);

        var stack = new StackPanel();
        stack.Children.Add(filter);
        stack.Children.Add(input);
        return stack;
    }

    /// <summary>输入 chip：文字 + 尾部 ×，点 × 淡出移除。</summary>
    private FrameworkElement InputChip(string label)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            FontWeight = FontWeights.Medium,
            Foreground = UiSkin.L.OnSurface,
            VerticalAlignment = VerticalAlignment.Center
        };

        var inner = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        inner.Children.Add(text);

        var chip = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Padding = new Thickness(14, 0, 10, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Background = UiSkin.L.SecondaryContainer,
            Cursor = Cursors.Hand,
            Child = inner
        };

        var close = M3.Icon("M7,7 L17,17 M17,7 L7,17", UiSkin.L.OnSecondaryContainer, 14);
        close.Margin = new Thickness(8, 0, 0, 0);
        close.Cursor = Cursors.Hand;
        close.IsHitTestVisible = true;
        close.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            var fade = new DoubleAnimation(0, M3.FastEffects) { EasingFunction = M3BezierEase.Accelerate() };
            fade.Completed += (_, _) => chip.Visibility = Visibility.Collapsed;
            chip.BeginAnimation(OpacityProperty, fade);
        };
        inner.Children.Add(close);

        return chip;
    }

    private FrameworkElement Chip(string label, bool selected)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            FontWeight = FontWeights.Medium,
            Foreground = selected ? UiSkin.L.OnSecondaryContainer : UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center
        };

        bool state = selected;
        var border = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Background = state ? UiSkin.L.SecondaryContainer : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = state ? Brushes.Transparent : UiSkin.L.OutlineVariant,
            Cursor = Cursors.Hand,
            Child = text
        };

        border.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            state = !state;
            text.Foreground = state ? UiSkin.L.OnSecondaryContainer : UiSkin.Dim;
            border.BorderBrush = state ? Brushes.Transparent : UiSkin.L.OutlineVariant;
            M3.AnimateFill(border, state ? UiSkin.L.SecondaryContainer : Brushes.Transparent);
        };

        return border;
    }

    private FrameworkElement BuildSwitchSample()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var toggle = new M3Switch(true, scheme: UiSkin.L);
        var label = new TextBlock
        {
            Text = "开",
            FontSize = 12,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        toggle.Changed += value => label.Text = value ? "开" : "关";

        row.Children.Add(toggle);
        row.Children.Add(label);
        return row;
    }

    private FrameworkElement BuildSliderSample()
    {
        var value = new TextBlock
        {
            Text = $"{_sliderValue:0}%",
            FontSize = 12,
            Width = 42,
            TextAlignment = TextAlignment.Right,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center
        };

        var slider = new M3Slider(_sliderValue, 0, 100, scheme: UiSkin.L) { Width = 220 };
        slider.Changed += raw =>
        {
            _sliderValue = Math.Round(raw);
            value.Text = $"{_sliderValue:0}%";
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(slider);
        row.Children.Add(value);
        return row;
    }

    private FrameworkElement BuildTextField()
    {
        var box = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            FontSize = 13.5,
            Foreground = UiSkin.Ink,
            CaretBrush = UiSkin.L.Primary,
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null
        };

        var placeholder = new TextBlock
        {
            Text = "输入点什么…",
            FontSize = 13.5,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        var grid = new Grid { Margin = new Thickness(14, 0, 14, 0) };
        grid.Children.Add(box);
        grid.Children.Add(placeholder);

        box.TextChanged += (_, _) =>
            placeholder.Visibility = box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;

        var line = new Border { Height = 2, Background = UiSkin.L.Primary, VerticalAlignment = VerticalAlignment.Bottom };
        var root = new Grid();
        root.Children.Add(new Border { Background = UiSkin.L.SurfaceContainerHigh });
        root.Children.Add(grid);
        root.Children.Add(line);

        return new Border
        {
            Width = 320,
            Height = 48,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = root
        };
    }

    private FrameworkElement BuildListSample()
    {
        var stack = new StackPanel();

        stack.Children.Add(ListItem("生存存档", "上次游玩 3 天前 · 1.20.1", IconNote));
        stack.Children.Add(new Border { Height = 1, Background = UiSkin.Line, Margin = new Thickness(0, 0, 0, 0) });
        stack.Children.Add(ListItem("模组实例", "Forge 47.2.0 · 128 个模组", IconGrid));

        return new Border
        {
            Width = 420,
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = UiSkin.L.SurfaceLow,
            Padding = new Thickness(6, 4, 6, 4),
            Child = stack
        };
    }

    private FrameworkElement ListItem(string title, string desc, string icon)
    {
        var grid = new Grid { Margin = new Thickness(10, 9, 6, 9) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(19),
            Background = UiSkin.L.PrimaryContainer,
            Child = M3.Icon(icon, UiSkin.L.OnPrimaryContainer, 20),
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(avatar);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = FontWeights.Medium,
            Foreground = UiSkin.Ink
        });
        text.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 11.5,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = UiSkin.Dim
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var chevron = M3.Icon(IconChevron, UiSkin.Dim, 18);
        chevron.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        return grid;
    }

    // ─────────────────────────── 加载 ───────────────────────────

    private FrameworkElement BuildLoadingSamples()
    {
        var body = new StackPanel();

        body.Children.Add(Demo("线性 · 确定", LinearBar(false)));
        body.Children.Add(Divider());
        body.Children.Add(Demo("线性 · 不确定", LinearBar(true)));
        body.Children.Add(Divider());
        body.Children.Add(Demo("波浪 · 确定", WavyBar()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("波浪 · 不确定", WavyIndeterminate()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("环形 · 确定", Ring(false)));
        body.Children.Add(Divider());
        body.Children.Add(Demo("环形 · 不确定", Ring(true)));
        body.Children.Add(Divider());
        body.Children.Add(Demo("波浪环形", WavyRing()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("加载按钮", LoadingButton()));

        return Panel(body, new Thickness(20, 8, 20, 8));
    }

    private FrameworkElement LinearBar(bool indeterminate)
    {
        var bar = new M3LinearProgress
        {
            Width = 300,
            Value = 0.58,
            IsIndeterminate = indeterminate,
            ActiveBrush = UiSkin.L.Primary,
            TrackBrush = UiSkin.L.SecondaryContainer,
            VerticalAlignment = VerticalAlignment.Center
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(bar);

        row.Children.Add(new TextBlock
        {
            Text = indeterminate ? "不定长" : "58%",
            FontSize = 11.5,
            Width = 42,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(12, 0, 0, 0),
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center
        });

        return row;
    }

    private FrameworkElement WavyBar()
    {
        var value = new TextBlock
        {
            Text = "60%",
            FontSize = 11.5,
            Width = 40,
            TextAlignment = TextAlignment.Right,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center
        };

        var wavy = new M3WavyProgress
        {
            Width = 280,
            Value = 0.6,
            ActiveBrush = UiSkin.L.Primary,
            TrackBrush = UiSkin.L.SecondaryContainer,
            VerticalAlignment = VerticalAlignment.Center
        };

        var minus = M3IconButton.Create("M5,12 H19", () =>
        {
            wavy.Value = Math.Max(0, Math.Round(wavy.Value * 10 - 1) / 10);
            value.Text = $"{wavy.Value * 100:0}%";
        }, "减 10%", scale: 0.78, scheme: UiSkin.L);

        var plus = M3IconButton.Create(IconPlus, () =>
        {
            wavy.Value = Math.Min(1, Math.Round(wavy.Value * 10 + 1) / 10);
            value.Text = $"{wavy.Value * 100:0}%";
        }, "加 10%", scale: 0.78, scheme: UiSkin.L);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(wavy);
        row.Children.Add(value);
        row.Children.Add(Pad(minus, 12, 0, 0, 0));
        row.Children.Add(Pad(plus, 4, 0, 0, 0));
        return row;
    }

    private FrameworkElement WavyIndeterminate()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new M3WavyProgress
        {
            Width = 280,
            IsIndeterminate = true,
            ActiveBrush = UiSkin.L.Primary,
            TrackBrush = UiSkin.L.SecondaryContainer,
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock
        {
            Text = "不定长",
            FontSize = 11.5,
            Margin = new Thickness(12, 0, 0, 0),
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    private FrameworkElement Ring(bool indeterminate)
    {
        var ring = new M3CircularProgress
        {
            Width = 44,
            Height = 44,
            Value = 0.68,
            IsIndeterminate = indeterminate,
            ActiveBrush = UiSkin.L.Primary,
            TrackBrush = UiSkin.L.SecondaryContainer
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(ring);
        row.Children.Add(new TextBlock
        {
            Text = indeterminate ? "不定长" : "68%",
            FontSize = 11.5,
            Margin = new Thickness(12, 0, 0, 0),
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    private FrameworkElement WavyRing()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new M3WavyCircularProgress
        {
            Width = 52,
            Height = 52,
            ActiveBrush = UiSkin.L.Primary,
            TrackBrush = UiSkin.L.SecondaryContainer
        });
        row.Children.Add(new TextBlock
        {
            Text = "波浪弧 + 自转",
            FontSize = 11.5,
            Margin = new Thickness(14, 0, 0, 0),
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    /// <summary>点一下进入加载态：按钮里塞一个 16dp 的转圈，1.8 秒后恢复。</summary>
    private FrameworkElement LoadingButton()
    {
        var spinner = new M3CircularProgress
        {
            Width = 16,
            Height = 16,
            Thickness = 2,
            IsIndeterminate = true,
            TrackBrush = null,
            ActiveBrush = UiSkin.L.OnPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Visibility = Visibility.Collapsed
        };

        bool busy = false;
        Border button = null!;
        button = M3Button.Create("保存布局", () =>
        {
            if (busy) return;
            busy = true;

            // M3Button 的 Child 是 Grid（底色层 + 内容层），不能直接当 StackPanel 用
            StackPanel content = null!;
            TextBlock label = null!;

            if (button.Child is Grid root)
            {
                foreach (var child in root.Children)
                {
                    if (child is not StackPanel panel) continue;
                    content = panel;
                    foreach (var inner in panel.Children)
                    {
                        if (inner is TextBlock text) label = text;
                    }
                }
            }

            if (content == null || label == null) return;

            string original = label.Text;
            content.Children.Insert(0, spinner);
            spinner.Visibility = Visibility.Visible;
            label.Text = "保存中…";

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1800)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                spinner.Visibility = Visibility.Collapsed;
                content.Children.Remove(spinner);
                label.Text = original;
                busy = false;
                Report("加载按钮 → 完成");
            };
            timer.Start();
        }, M3ButtonKind.Filled, scheme: UiSkin.L);

        return button;
    }

    /// <summary>描边输入框：平时 1dp outline，聚焦变 2dp 主色。</summary>
    private FrameworkElement BuildOutlinedField()
    {
        var box = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            FontSize = 13.5,
            Foreground = UiSkin.Ink,
            CaretBrush = UiSkin.L.Primary,
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null
        };

        var placeholder = new TextBlock
        {
            Text = "描边输入框…",
            FontSize = 13.5,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };

        var grid = new Grid { Margin = new Thickness(14, 0, 14, 0) };
        grid.Children.Add(box);
        grid.Children.Add(placeholder);

        box.TextChanged += (_, _) =>
            placeholder.Visibility = box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;

        var border = new Border
        {
            Width = 320,
            Height = 48,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.L.Outline,
            Child = grid
        };

        box.GotFocus += (_, _) =>
        {
            border.BorderBrush = UiSkin.L.Primary;
            border.BorderThickness = new Thickness(2);
        };
        box.LostFocus += (_, _) =>
        {
            border.BorderBrush = UiSkin.L.Outline;
            border.BorderThickness = new Thickness(1);
        };

        return border;
    }

    // ─────────────────────────── 更多组件 ───────────────────────────

    private FrameworkElement BuildMoreSamples()
    {
        var body = new StackPanel();

        body.Children.Add(Demo("单选", BuildRadioSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("复选", BuildCheckboxSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("徽标", BuildBadgeSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("标签页", BuildTabSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("扩展面板", BuildExpansionSample()));
        body.Children.Add(Divider());
        body.Children.Add(Demo("提示条", BuildSnackbarSample()));

        return Panel(body, new Thickness(20, 8, 20, 8));
    }

    private FrameworkElement BuildRadioSample()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var radios = new List<M3RadioButton>();
        string[] names = { "花样", "极简", "便签" };

        for (int i = 0; i < names.Length; i++)
        {
            var radio = new M3RadioButton(i == 0, UiSkin.L);
            var label = new TextBlock
            {
                Text = names[i],
                FontSize = 12.5,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = UiSkin.Ink,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };

            // 互斥统一走这里：无论点圆点还是点文字都选同一组，否则会出现两个同时亮。
            void Pick()
            {
                radio.IsChecked = true;
                foreach (var item in radios)
                {
                    if (!ReferenceEquals(item, radio)) item.IsChecked = false;
                }
            }

            // radio 自己已经 Handled 了鼠标事件，外层容器再挂 MouseLeftButtonUp 收不到。
            radio.Changed += on =>
            {
                if (on) Pick();
            };
            label.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Pick();
            };

            var wrap = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 20, 0)
            };
            wrap.Children.Add(radio);
            wrap.Children.Add(label);

            radios.Add(radio);
            row.Children.Add(wrap);
        }

        return row;
    }

    private FrameworkElement BuildCheckboxSample()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        string[] names = { "时钟", "天气", "皮肤" };

        for (int i = 0; i < names.Length; i++)
        {
            var box = new M3Checkbox(i < 2, UiSkin.L);
            var label = new TextBlock
            {
                Text = names[i],
                FontSize = 12.5,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = UiSkin.Ink,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };

            // 方块自己会切换；标签单独挂一份，点字也能勾
            label.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                box.IsChecked = !box.IsChecked;
            };

            var wrap = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 20, 0)
            };
            wrap.Children.Add(box);
            wrap.Children.Add(label);

            row.Children.Add(wrap);
        }

        return row;
    }

    private FrameworkElement BuildBadgeSample()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        var avatar = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(21),
            Background = UiSkin.L.PrimaryContainer,
            Child = new TextBlock
            {
                Text = "阿",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = UiSkin.L.OnPrimaryContainer,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var badged = new Grid { Width = 46, Height = 46 };
        badged.Children.Add(avatar);
        var dot = M3Badge.Dot();
        dot.Margin = new Thickness(0, 1, 1, 0);
        badged.Children.Add(dot);
        row.Children.Add(badged);

        var count = M3Badge.Count(12, UiSkin.L);
        count.VerticalAlignment = VerticalAlignment.Center;
        count.Margin = new Thickness(16, 0, 0, 0);
        row.Children.Add(count);

        var count2 = M3Badge.Count(128, UiSkin.L);
        count2.VerticalAlignment = VerticalAlignment.Center;
        count2.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(count2);

        return row;
    }

    private FrameworkElement BuildTabSample()
    {
        var hint = new TextBlock
        {
            Text = "当前：概览",
            FontSize = 11.5,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };

        var tabs = new M3Tabs(new[] { "概览", "实例", "设置" }, 0, UiSkin.L);
        tabs.Changed += index =>
        {
            hint.Text = "当前：" + new[] { "概览", "实例", "设置" }[index];
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(tabs);
        row.Children.Add(hint);
        return row;
    }

    private FrameworkElement BuildExpansionSample()
    {
        var body = new TextBlock
        {
            Text = "扩展面板收起时只占一行标题的高度，展开后把内容推下去。" +
                   "主页的「组件设置」里也可以用这个模式收纳高级项。",
            FontSize = 12,
            LineHeight = 19,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim
        };

        return new M3Expansion("高级选项", "点标题展开 / 收起", body, false, UiSkin.L)
        {
            Width = 460,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private FrameworkElement BuildSnackbarSample()
    {
        var host = new Grid { Width = 460, Height = 118 };

        var snackbar = M3Snackbar.Create("已保存布局 · 刚刚", "撤销", () => Report("提示条 → 撤销"), UiSkin.L);
        snackbar.VerticalAlignment = VerticalAlignment.Bottom;
        snackbar.Opacity = 0;
        snackbar.Visibility = Visibility.Collapsed;
        host.Children.Add(snackbar);

        var show = M3Button.Create("显示提示条", () =>
        {
            snackbar.Visibility = Visibility.Visible;
            snackbar.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, M3.DefaultEffects) { EasingFunction = M3.Effects });
        }, M3ButtonKind.Tonal, scheme: UiSkin.L);
        show.VerticalAlignment = VerticalAlignment.Top;
        show.HorizontalAlignment = HorizontalAlignment.Left;
        show.Margin = new Thickness(0, 2, 0, 0);
        host.Children.Add(show);

        var hide = M3Button.Create("收起", () =>
        {
            var fade = new DoubleAnimation(0, M3.FastEffects) { EasingFunction = M3.Effects };
            fade.Completed += (_, _) => snackbar.Visibility = Visibility.Collapsed;
            snackbar.BeginAnimation(OpacityProperty, fade);
        }, M3ButtonKind.Text, scheme: UiSkin.L);
        hide.VerticalAlignment = VerticalAlignment.Top;
        hide.HorizontalAlignment = HorizontalAlignment.Left;
        hide.Margin = new Thickness(156, 2, 0, 0);
        host.Children.Add(hide);

        return host;
    }

    // ─────────────────────────── 导航 ───────────────────────────

    private FrameworkElement BuildNavigationSamples()
    {
        var items = new (string Icon, string Label)[]
        {
            (IconGrid, "主页"),
            ("M4,11.5 L12,4.5 L20,11.5 V20 H4 Z", "实例"),
            (IconSettings, "设置")
        };

        var body = new StackPanel();

        var bar = new M3TopAppBar("高级主页",
            "M4,7 H20 M4,12 H20 M4,17 H20", () => Report("顶部栏 → 菜单"),
            IconSearch, () => Report("顶部栏 → 搜索"), UiSkin.L)
        {
            Width = 460,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        body.Children.Add(Demo("顶部栏", bar));
        body.Children.Add(Divider());

        var navHint = new TextBlock
        {
            Text = "当前：主页",
            FontSize = 11.5,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };

        var navBar = new M3NavigationBar(items, 0, UiSkin.L)
        {
            Width = 400,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        navBar.Changed += index => navHint.Text = "当前：" + items[index].Label;

        var navRow = new StackPanel { Orientation = Orientation.Horizontal };
        navRow.Children.Add(navBar);
        navRow.Children.Add(navHint);
        body.Children.Add(Demo("底部导航", navRow));
        body.Children.Add(Divider());

        var railHint = new TextBlock
        {
            Text = "当前：主页",
            FontSize = 11.5,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(16, 18, 0, 0)
        };

        var rail = new M3NavigationRail(items, 0, UiSkin.L);
        rail.Changed += index => railHint.Text = "当前：" + items[index].Label;

        var railRow = new StackPanel { Orientation = Orientation.Horizontal };
        railRow.Children.Add(rail);
        railRow.Children.Add(railHint);
        body.Children.Add(Demo("侧边导航", railRow));

        return Panel(body, new Thickness(20, 8, 20, 8));
    }

    // ─────────────────────────── 浮层 ───────────────────────────

    private FrameworkElement BuildOverlaySamples()
    {
        string[] labels = { "按最近使用", "按名称", "按来源" };
        var body = new StackPanel();

        var menu = M3Menu.CreateButton("排序方式", new (string Icon, string Text)[]
        {
            (IconStar, labels[0]),
            (IconNote, labels[1]),
            (IconSettings, labels[2])
        }, index => Report("菜单 → " + labels[index]), UiSkin.L);

        body.Children.Add(Demo("下拉菜单", menu));
        body.Children.Add(Divider());

        var tip = M3Button.Create("悬停看提示", null, M3ButtonKind.Outlined, scheme: UiSkin.L);
        tip.ToolTip = UiStyles.Tooltip("提示气泡：用来补充说明，不打断操作。");
        body.Children.Add(Demo("提示气泡", tip));
        body.Children.Add(Divider());

        var sheet = M3Button.Create("打开底部面板", ShowSheet, M3ButtonKind.Tonal, scheme: UiSkin.L);
        body.Children.Add(Demo("底部面板", sheet));

        return Panel(body, new Thickness(20, 8, 20, 8));
    }

    /// <summary>底部面板：scrim + 从下沿滑上来的 28dp 顶圆角面板。</summary>
    private void ShowSheet()
    {
        Log.Info("[组件库] 打开底部面板");
        try
        {
            ShowSheetCore();
        }
        catch (Exception ex)
        {
            Log.Error("打开底部面板失败", ex);
        }
    }

    private void ShowSheetCore()
    {
        var scrim = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            Opacity = 0
        };

        // 起始位置先塞到窗口下面；真实高度要等布局完成才知道，Loaded 里再算
        var move = new TranslateTransform(0, 320);
        var sheet = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(28, 28, 0, 0),
            Background = UiSkin.Card,
            Padding = new Thickness(26, 12, 26, 22),
            RenderTransform = move
        };

        var layer = new Grid();
        layer.Children.Add(scrim);
        layer.Children.Add(sheet);

        void Close()
        {
            var fade = new DoubleAnimation(0, M3.FastEffects) { EasingFunction = M3BezierEase.Accelerate() };
            fade.Completed += (_, _) =>
            {
                _overlay.Children.Remove(layer);
                if (_overlay.Children.Count == 0) _overlay.Visibility = Visibility.Collapsed;
            };
            scrim.BeginAnimation(OpacityProperty, fade);
            move.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(sheet.ActualHeight + 24, M3.DefaultEffects)
                {
                    EasingFunction = M3BezierEase.Accelerate()
                });
        }

        var handle = new Border
        {
            Width = 32,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = UiSkin.Line,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var content = new StackPanel();
        content.Children.Add(handle);
        content.Children.Add(new TextBlock
        {
            Text = "底部面板",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink,
            Margin = new Thickness(0, 16, 0, 0)
        });
        content.Children.Add(new TextBlock
        {
            Text = "M3 的 bottom sheet 从窗口下沿滑入：遮罩淡入 200ms，面板用 emphasized decelerate 上浮。" +
                 "关掉时反过来用 accelerate，所以是「滑上来慢、收回去快」。",
            FontSize = 12.5,
            LineHeight = 20,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var close = M3Button.Create("收起面板", Close, M3ButtonKind.Text, scheme: UiSkin.L);
        close.HorizontalAlignment = HorizontalAlignment.Left;
        close.Margin = new Thickness(-12, 12, 0, 0);
        content.Children.Add(close);

        sheet.Child = content;
        scrim.MouseLeftButtonUp += (_, _) => Close();

        _overlay.Children.Add(layer);
        _overlay.Visibility = Visibility.Visible;

        scrim.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, M3.DefaultEffects) { EasingFunction = M3BezierEase.Standard() });

        // Loaded 时机拿 ActualHeight 可能还是 0，推到布局完成之后再算，滑动距离才准
        sheet.Dispatcher.BeginInvoke(new Action(() =>
        {
            move.BeginAnimation(TranslateTransform.YProperty, null);
            move.Y = sheet.ActualHeight + 24;
            move.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, M3.FastSpatial) { EasingFunction = M3BezierEase.Emphasized() });
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ─────────────────────────── 动效 ───────────────────────────

    private FrameworkElement BuildMotionSamples()
    {
        var body = new StackPanel();

        body.Children.Add(Demo("缓动曲线", new M3EasingDemo(UiSkin.L)));
        body.Children.Add(Divider());
        body.Children.Add(Demo("容器变形", new M3ContainerTransformDemo(UiSkin.L)));
        body.Children.Add(Divider());
        body.Children.Add(Demo("共享轴", new M3SharedAxisDemo(UiSkin.L)));
        body.Children.Add(Divider());
        body.Children.Add(Demo("交错入场", new M3StaggerDemo(UiSkin.L) { Width = 320 }));

        return Panel(body, new Thickness(20, 8, 20, 8));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 主页画布：列数固定为 <see cref="Cols"/>，行数按组件实际堆到哪就在哪收尾，
/// 不再留一大片空网格。编辑模式下可移动、缩放、删除、添加。
/// </summary>
internal sealed class HomeCanvas : Grid
{
    /// <summary>横向网格列数。</summary>
    public const int Cols = Cfg.GridCols;

    /// <summary>当前网格行数（自适应，不写死）。</summary>
    private int _rows = Cfg.MinRows;

    private readonly Grid _cells = new();
    private readonly List<Border> _lines = new();
    private readonly CalendarView _calendar = new();
    private readonly MinimalView _minimal = new();
    private readonly NotesView _notes = new();
    private readonly TodoView _todos = new();
    private readonly RecentWorldsView _worlds = new();
    private readonly InstancesView _instances = new();
    private readonly GreetingView _greeting = new();
    private readonly SitesView _sites = new();
    private readonly DownloadView _download = new();
    private readonly CalculatorView _calculator = new();
    private readonly ClockView _clock = new();
    private readonly WeatherView _weather = new();

    /// <summary>组件注册表：每个组件有独立的卡片宿主与独立的位置记录。</summary>
    private readonly List<HomeWidget> _widgets = new();
    private readonly Dictionary<string, FrameworkElement> _hosts = new(StringComparer.Ordinal);

    /// <summary>每张卡片自己的背景图层（背景图片 + 透明度）。</summary>
    private readonly Dictionary<string, Border> _looks = new(StringComparer.Ordinal);

    /// <summary>透明组件（问候）的可选底板，用来承载「整卡透明度」。</summary>
    private readonly Dictionary<string, Border> _fills = new(StringComparer.Ordinal);

    /// <summary>编辑模式下的透明交互层：盖住整张卡片，保证组件内容（比如 3D 视图）不会把鼠标事件吃掉。</summary>
    private readonly Dictionary<string, Border> _hits = new(StringComparer.Ordinal);

    private readonly Border _ghost;
    private readonly TextBlock _hint;
    private readonly TextBlock _emptyHint;
    private readonly StackPanel _topHost;
    private Border _topSurface = null!;
    private HomeTopBar _topBar = null!;
    private bool _topBarDark;

    // 组件菜单层：选中卡片的「⋯」选项菜单画在画布内，不依赖独立弹窗
    private readonly Grid _menuLayer = new();

    // 选中卡片的「小组件选项」菜单（显示条数 / 更换目标 / 删除）
    private readonly Border _widgetMenu;
    private readonly StackPanel _widgetMenuItems = new();

    // 选中态：主题色 8% 叠加 + 描边 + 右上角操作药丸（设置 / ×）+ 右下角缩放手柄
    private readonly Grid _selection = new();
    private readonly Border _selectionTint;
    private readonly Border _selectionFrame;
    private readonly Border _actionPill;
    private readonly Border _deleteButton;
    private readonly Border _moreButton;
    private readonly Border _settingsButton;
    private readonly Border _pillDivider;
    private readonly Border _pillDividerTop;
    private readonly Border _resizeGrip;

    // 撤销提示（右下角 snackbar）
    private readonly Border _snackbar;
    private readonly TextBlock _snackbarText;
    private readonly Border _snackbarAction;
    private readonly ScaleTransform _snackbarProgress = new();
    private readonly System.Windows.Threading.DispatcherTimer _snackTimer = new();
    private Action? _undoAction;

    private HomeWidget? _selected;
    private string _style = "fancy";
    private double _rowHeight = 96;
    private bool _edit;
    private bool _clearing;
    private bool _removing;
    private DateTime _lastEnter = DateTime.MinValue;

    // 滚动位置：进主页默认落到小组件那一排，切页面回来则回到上次的位置
    private ScrollViewer? _scroller;
    private bool _scrollHooked;
    private bool _scrollSettled;
    private static double _lastOffset = -1;

    // 拖动状态
    private HomeWidget? _dragWidget;
    private FrameworkElement? _dragHost;
    private bool _dragArmed;
    private Point _dragStart;
    private int _dragOriginCol;
    private int _dragOriginRow;
    private TranslateTransform? _dragTransform;

    // 缩放状态
    private HomeWidget? _resizeWidget;
    private FrameworkElement? _resizeHost;
    private Point _resizeStart;
    private int _resizeOriginCol;
    private int _resizeOriginRow;
    private int _resizeOriginColSpan;
    private int _resizeOriginRowSpan;

    public HomeCanvas()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Margin = new Thickness(0, 0, 0, 14);
        ClipToBounds = false;
        SnapsToDevicePixels = true;

        // 主页字体：字体族可继承，卡片里的文字只要没写死字体就会跟着变
        if (!string.IsNullOrWhiteSpace(Cfg.Ui.FontFamily))
        {
            try
            {
                System.Windows.Documents.TextElement.SetFontFamily(this, new FontFamily(Cfg.Ui.FontFamily));
            }
            catch (Exception ex)
            {
                Log.Warn($"主页字体设置无效（{Cfg.Ui.FontFamily}）：{ex.Message}");
            }
        }

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 顶部样式条
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // 网格
        ColumnDefinitions.Add(new ColumnDefinition());

        for (int i = 0; i < Cols; i++) _cells.ColumnDefinitions.Add(new ColumnDefinition());
        EnsureRows(Cfg.MinRows);

        _cells.MouseLeftButtonDown += (_, _) =>
        {
            if (_edit && _selected != null) Select(null);
        };

        // 有的组件内容自己吃掉 MouseEnter（例如 3D 视图），
        // 这里再用 MouseMove 兜一次底：沿着源元素往上找带 Tag 的卡片宿主。
        _cells.AddHandler(UIElement.MouseMoveEvent, new MouseEventHandler(OnCellsMouseMove), true);

        // 编辑提示放在顶部条右侧，避免压到卡片
        _hint = new TextBlock
        {
            Text = "悬停选中 · 拖动移动 · 右下角缩放 · 右上角删除",
            FontSize = 12,
            Foreground = M3.C.OnSurfaceVariant,
            Margin = new Thickness(14, 1, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        _emptyHint = new TextBlock
        {
            Text = "还没有组件：点右上角画笔进入编辑，再点 + 添加",
            FontSize = 13,
            Opacity = 0.78,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Palette.Bind(_emptyHint, TextBlock.ForegroundProperty, Palette.TextTertiary, "ColorBrush3");
        Grid.SetColumn(_emptyHint, 0);
        Grid.SetRow(_emptyHint, 0);
        Grid.SetColumnSpan(_emptyHint, Cols);
        Grid.SetRowSpan(_emptyHint, _rows);
        Panel.SetZIndex(_emptyHint, 950);
        _cells.Children.Add(_emptyHint);

        _ghost = new Border
        {
            CornerRadius = new CornerRadius(M3.CardRadius),
            BorderThickness = new Thickness(1.5),
            BorderBrush = Palette.Accent,
            Background = Palette.Solid(Palette.IsDark ? "#14FFFFFF" : "#0A000000"),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(4)
        };
        Panel.SetZIndex(_ghost, 900);
        _cells.Children.Add(_ghost);

        RegisterWidgets();

        foreach (var widget in _widgets)
        {
            if (_hosts.ContainsKey(widget.Id)) continue;    // 快捷入口在注册时就建好了
            var host = BuildCardHost(widget);
            _hosts[widget.Id] = host;
            _cells.Children.Add(host);
        }

        // ── 选中覆盖层 ──
        // 先铺一层 8% 主题色：只在卡片边上描一圈，扫视时很难一眼看出选的是哪张
        _selectionTint = new Border
        {
            CornerRadius = new CornerRadius(M3.CardRadius),
            Background = M3.Alpha(M3.C.Primary, 0.06),
            IsHitTestVisible = false
        };
        _selection.Children.Add(_selectionTint);

        _selectionFrame = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = M3.C.Primary,
            CornerRadius = new CornerRadius(14),
            IsHitTestVisible = false
        };
        _selection.Children.Add(_selectionFrame);

        // 右上角操作药丸：⋯（选项）｜×（删除），和描边一起淡入
        _deleteButton = BuildPillIcon(CreateCrossIcon(10.5, M3.C.Error), "删除这个组件");
        _deleteButton.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            DeleteSelected();
        };

        _moreButton = BuildPillIcon(ImageUtil.CreateMoreIcon(11.5, M3.C.OnSurfaceVariant), "小组件选项");
        _moreButton.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_selected != null) ShowWidgetMenu(_selected);
        };

        _settingsButton = BuildPillIcon(CreateSlidersIcon(12, M3.C.OnSurfaceVariant), "组件设置");
        _settingsButton.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_selected != null) OpenSettings(_selected);
        };

        _pillDividerTop = new Border
        {
            Width = 1,
            Height = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Background = M3.C.OutlineVariant,
            IsHitTestVisible = false
        };

        _pillDivider = new Border
        {
            Width = 1,
            Height = 12,
            Margin = new Thickness(0, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = M3.C.OutlineVariant,
            IsHitTestVisible = false
        };

        var pillRow = new StackPanel { Orientation = Orientation.Horizontal };
        pillRow.Children.Add(_settingsButton);
        pillRow.Children.Add(_pillDividerTop);
        pillRow.Children.Add(_moreButton);
        pillRow.Children.Add(_pillDivider);
        pillRow.Children.Add(_deleteButton);

        _actionPill = new Border
        {
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = M3.C.SurfaceContainerHigh,
            Child = pillRow,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            // 与卡片圆角同心：内缩 6，离角更远一点，不糊在圆角上
            Margin = new Thickness(0, 6, 6, 0),
            Effect = M3.Elevation(2)
        };
        _selection.Children.Add(_actionPill);

        _resizeGrip = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = M3.C.Primary,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            // 6 的边距正好让手柄圆心落在卡片 11 圆角的弧心上（同心），又比原来的 2 离角更远
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = Cursors.SizeNWSE,
            Child = CreateGripIcon(8.5, M3.C.OnPrimary)
        };
        ToolTipService.SetToolTip(_resizeGrip, "拖动调整大小");
        _resizeGrip.MouseLeftButtonDown += OnResizeStart;
        _resizeGrip.MouseMove += OnResizeMove;
        _resizeGrip.MouseLeftButtonUp += OnResizeEnd;
        _selection.Children.Add(_resizeGrip);

        _selection.Margin = new Thickness(4);
        _selection.Visibility = Visibility.Collapsed;
        Panel.SetZIndex(_selection, 850);
        _cells.Children.Add(_selection);

        _topHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        BuildTopBar();
        _topHost.Children.Add(_hint);

        // 顶栏浮在照片壁纸上，必须有自己的表面：M3 的 top app bar 就是一整条 surface，
        // 裸控件压在壁纸上时对比度完全看壁纸脸色。
        _topSurface = new Border
        {
            CornerRadius = new CornerRadius(28),
            Background = M3.Alpha(M3.C.SurfaceContainer, 0.92),
            // 阴影在深色壁纸上几乎看不见，补一圈 1px 描边把边界勾出来
            BorderThickness = new Thickness(1),
            BorderBrush = M3.Alpha(M3.C.OutlineVariant, 0.55),
            // 左右都是 4：右边原来是 14，药丸会多伸出一截，和编辑按钮对不齐
            Padding = new Thickness(4, 4, 4, 4),
            Margin = new Thickness(4, 0, 4, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _topHost,
            Effect = M3.Elevation(1)
        };
        SetRow(_topSurface, 0);

        SetRow(_cells, 1);
        Children.Add(_topSurface);
        Children.Add(_cells);

        // ── 组件菜单层：覆盖整块画布，点空白处收起 ──
        _menuLayer.Background = Brushes.Transparent;
        _menuLayer.Visibility = Visibility.Collapsed;
        _menuLayer.MouseLeftButtonDown += (_, e) =>
        {
            HideMenu();
            e.Handled = true;
        };
        Panel.SetZIndex(_menuLayer, 2000);
        SetRowSpan(_menuLayer, 2);

        _widgetMenu = new Border
        {
            Width = 200,
            CornerRadius = new CornerRadius(M3.CornerXs),
            Background = M3.C.SurfaceContainer,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _widgetMenuItems,
            Effect = M3.Elevation(2)
        };
        _widgetMenu.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _menuLayer.Children.Add(_widgetMenu);
        Children.Add(_menuLayer);

        // ── 右下角撤销提示 ──
        // M3 snackbar：inverse surface 底 + inverse on surface 文字（不透明的，确保可读）
        _snackbarText = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = M3.C.InverseOnSurface,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _snackbarAction = new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(12, 0, 0, 0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "撤销",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = M3.C.InversePrimary
            }
        };
        _snackbarAction.MouseEnter += (_, _) =>
            _snackbarAction.Background = M3.Layer(M3.Of(M3.C.InverseSurface), M3.Of(M3.C.InverseOnSurface), M3.HoverAlpha);
        _snackbarAction.MouseLeave += (_, _) => _snackbarAction.Background = Brushes.Transparent;
        _snackbarAction.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            var undo = _undoAction;
            HideSnackbar();
            undo?.Invoke();
        };

        var snackRow = new DockPanel { LastChildFill = true, Margin = new Thickness(16, 10, 12, 16) };
        DockPanel.SetDock(_snackbarAction, Dock.Right);
        snackRow.Children.Add(_snackbarAction);
        snackRow.Children.Add(_snackbarText);

        var snackBody = new Grid();
        snackBody.Children.Add(snackRow);
        var progressTrack = new Border
        {
            Height = 2,
            Margin = new Thickness(14, 0, 14, 7),
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = M3.Layer(M3.Of(M3.C.InverseSurface), M3.Of(M3.C.InverseOnSurface), 0.20),
            CornerRadius = new CornerRadius(1),
            IsHitTestVisible = false
        };
        var progressFill = new Border
        {
            Height = 2,
            Margin = new Thickness(14, 0, 14, 7),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = M3.C.InversePrimary,
            CornerRadius = new CornerRadius(1),
            RenderTransformOrigin = new Point(0, 0.5),
            RenderTransform = _snackbarProgress,
            IsHitTestVisible = false
        };
        snackBody.Children.Add(progressTrack);
        snackBody.Children.Add(progressFill);

        _snackbar = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerXs),
            Background = M3.C.InverseSurface,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Child = snackBody,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 10, 10),
            MinWidth = 250,
            MaxWidth = 420,
            Visibility = Visibility.Collapsed,
            Opacity = 0,
            Effect = M3.Elevation(3)
        };
        Grid.SetRow(_snackbar, 1);
        Panel.SetZIndex(_snackbar, 1900);
        Children.Add(_snackbar);

        _snackTimer.Interval = TimeSpan.FromSeconds(6);
        _snackTimer.Tick += (_, _) => HideSnackbar();

        ApplyStyle(Cfg.CurrentStyle);
        HomeRefresh.Register(this);

        SizeChanged += (_, _) => UpdateRowHeight();
        Loaded += (_, _) =>
        {
            // 页面卸载时（切到「下载 / 设置 / 启动游戏」再回来）把所有组件的 SetActive 关掉了，
            // 必须在这里按当前样式重新打开：皮肤动画就是靠这个标志决定要不要摆姿势，
            // 漏了就会出现「切回主页人物站着不动」。
            foreach (var widget in _widgets)
            {
                bool visible = string.Equals(widget.Style, _style, StringComparison.Ordinal)
                               && Cfg.HasCard(_style, widget.Id);
                widget.SetActive?.Invoke(visible);
            }

            if (string.Equals(_style, "minimal", StringComparison.Ordinal)) _minimal.SetActive(true);
            CheckTheme();
            Refresh();
            PlayEnterAnimation(force: true);
            _scrollSettled = false;
            RestoreScroll();

            // 第一次装插件：等入场动画结束弹一次新手教程
            Onboarding.OnHomeLoaded(this);
        };
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                PlayEnterAnimation();
                // 从别的页面切回来：停在原来的位置，别弹回顶部
                _scrollSettled = false;
                RestoreScroll();
            }
        };
        Unloaded += (_, _) =>
        {
            foreach (var widget in _widgets) widget.SetActive?.Invoke(false);
        };
    }

    // ─────────────────────────── 滚动位置 ───────────────────────────

    /// <summary>找到包着主页的 ScrollViewer，并把用户滚到的位置记下来。</summary>
    private void HookScroller()
    {
        if (_scrollHooked) return;
        _scroller = FindAncestor<ScrollViewer>(this);
        if (_scroller == null) return;

        _scrollHooked = true;
        _scroller.ScrollChanged += (_, _) =>
        {
            // 切页面时 PCL 会把位置归零；定位过程中的中间值也不要记，否则会停在半路
            if (IsVisible && _scrollSettled) _lastOffset = _scroller.VerticalOffset;
        };
    }

    /// <summary>
    /// 有记录就回到原位置；第一次进主页没有记录时，直接落到第一排小组件，
    /// 顶栏留在视口上方，往上滑就能看到。
    /// </summary>
    private void RestoreScroll()
    {
        HookScroller();
        if (_scroller == null) return;

        Dispatcher.BeginInvoke(new Action(() => ApplyScroll(0)), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 布局还没算完时 ScrollableHeight 是 0、顶栏高度也是 0，设置会被夹成 0；
    /// 所以每帧重算一次目标位置，直到真的落下去（最多 6 次）。
    /// </summary>
    private void ApplyScroll(int attempt)
    {
        if (_scroller == null)
        {
            _scrollSettled = true;
            return;
        }

        bool restoring = _lastOffset >= 0;
        // 第一次进主页落到最上面：顶栏（模板切换 / 编辑 / 设置 / 添加）要能第一眼看到，
        // 否则它藏在视口上方，用户根本不知道有这些入口。
        double goal = restoring ? _lastOffset : 0;
        // 顶栏还没量出来之前，算出来的目标没有意义，等下一帧
        bool ready = restoring || _topSurface.ActualHeight > 1;

        try
        {
            _scroller.ScrollToVerticalOffset(goal);
            if (attempt == 0) Log.Info($"[滚动] 目标 {goal:0}，当前 {_scroller.VerticalOffset:0}，可滚 {_scroller.ScrollableHeight:0}");
        }
        catch (Exception ex)
        {
            Log.Warn("恢复主页滚动位置失败：" + ex.Message);
            _scrollSettled = true;
            return;
        }

        if (ready && Math.Abs(_scroller.VerticalOffset - goal) <= 1)
        {
            _scrollSettled = true;
            return;
        }

        if (attempt >= 6)
        {
            _scrollSettled = true;
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ApplyScroll(attempt + 1);
        };
        timer.Start();
    }

    /// <summary>
    /// 把网格那一排顶到视口最上面需要的滚动量。用实测坐标而不是「顶栏高度」，
    /// 这样页面上下还有多少留白都不会差几像素（差一点就会露出半截顶栏）。
    /// </summary>
    private double GridTopInScroller()
    {
        if (_scroller == null) return 0;

        try
        {
            var point = _cells.TranslatePoint(new Point(0, 0), _scroller);
            double goal = _scroller.VerticalOffset + point.Y;
            return goal > 0 ? goal : 0;
        }
        catch
        {
            return _topSurface.ActualHeight + 12;
        }
    }

    private static T? FindAncestor<T>(DependencyObject node) where T : DependencyObject
    {
        try
        {
            var current = node;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
        }
        catch
        {
            // 还没进可视树
        }

        return null;
    }

    /// <summary>
    /// M3 的颜色是构造时取的角色值，切深浅色之后必须重建顶栏，
    /// 否则会出现「深色卡片配浅色分段按钮」这种两种主题混在一起的样子。
    /// </summary>
    private void BuildTopBar()
    {
        var old = _topBar;
        _topBar = new HomeTopBar(Cfg.CurrentStyle);
        _topBar.EditClicked += () => SetEdit(!_edit);
        _topBar.StyleChanged += OnStyleChanged;

        // 会弹模态窗口，等这次鼠标事件走完再开，否则窗口会卡在按下状态里
        _topBar.AddClicked += () => Dispatcher.BeginInvoke(new Action(ShowAddDialog));
        _topBar.DeleteLayoutClicked += () => Dispatcher.BeginInvoke(new Action(DeleteLayout));
        _topBar.SettingsClicked += () =>
        {
            Log.Info("[顶栏] 齿轮被点击，打开高级主页设置");
            Dispatcher.BeginInvoke(new Action(UiSettingsDialog.Show));
        };

        _topBarDark = Palette.IsDark;

        if (old == null)
        {
            _topHost.Children.Insert(0, _topBar);
            return;
        }

        // 替换：保住编辑态与选中样式
        int index = _topHost.Children.IndexOf(old);
        _topHost.Children.Remove(old);
        _topHost.Children.Insert(index < 0 ? 0 : index, _topBar);
        _topBar.SetStyle(Cfg.CurrentStyle, false);
        _topBar.SetEdit(_edit);
    }

    private void CheckTheme()
    {
        if (Palette.IsDark == _topBarDark) return;
        BuildTopBar();

        // 卡片外壳同样要跟着换色
        foreach (var widget in _widgets)
        {
            if (_hosts.TryGetValue(widget.Id, out var host) && host is M3Card card)
                card.RefreshTheme();
        }
    }

    /// <summary>
    /// 组件注册表。新增组件在这里追加一条即可：指定 Id / 所属 Style / 内容 View / 默认跨度。
    /// <c>Resizable = false</c> 表示尺寸写死，编辑模式下不给缩放手柄（日历就是这样）。
    /// </summary>
    private void RegisterWidgets()
    {
        _widgets.Add(new HomeWidget
        {
            Id = "calendar",
            Title = "日历",
            Description = "按月显示启动次数热力。",
            IconData = "M4,6.5 H20 V20 H4 Z M4,10.5 H20 M8,3 V7 M16,3 V7",
            Category = "multi",
            Style = "fancy",
            View = _calendar,
            ColSpan = 3,
            RowSpan = 4,
            DefaultCol = 6,
            DefaultRow = 0,
            AutoPlace = false,          // 日历留在「+」里，默认布局只有问候 + 世界 + 实例
            Resizable = true,           // 日历是按宽度等比重绘的矢量版式，放开缩放不会糊
            SetEditing = editing => _calendar.Interactive = !editing,
            OnShown = () =>
            {
                _calendar.Minimal = false;
                _calendar.Rebuild();
            },
            ApplyTheme = () => _calendar.ApplyTheme()
        });

        _widgets.Add(new HomeWidget
        {
            Id = "worlds",
            Title = "最近游玩的世界",
            Description = "最近打开过的存档，一键进入。",
            IconData = "M12,3 A9,9 0 1,1 11.99,3 Z M15.6,8.4 L13.4,13.4 L8.4,15.6 L10.6,10.6 Z",
            Category = "basic",
            Style = "fancy",
            View = _worlds,
            ColSpan = 3,
            RowSpan = 3,
            DefaultCol = 0,
            DefaultRow = 2,
            CountOptions = new[] { 2, 4, 6, 8 },
            GetCount = () => Cfg.GetOption("worlds.count", 4),
            SetCount = count => Cfg.SetOption("worlds.count", count),
            OnShown = () => _worlds.Reload(),
            Reload = () => _worlds.Reload()
        });

        _widgets.Add(new HomeWidget
        {
            Id = "instances",
            Title = "最近游玩的实例",
            Description = "最近启动过的实例，一键开玩。",
            IconData = "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z M4,7.5 L12,12 L20,7.5 M12,12 L12,21",
            Category = "basic",
            Style = "fancy",
            View = _instances,
            ColSpan = 3,
            RowSpan = 3,
            DefaultCol = 3,
            DefaultRow = 2,
            CountOptions = new[] { 2, 4, 6, 8 },
            GetCount = () => Cfg.GetOption("instances.count", 4),
            SetCount = count => Cfg.SetOption("instances.count", count),
            OnShown = () => _instances.Reload(),
            Reload = () => _instances.Reload()
        });

        HomeWidget? greetingWidget = null;
        greetingWidget = new HomeWidget
        {
            Id = "greeting",
            Title = "问候",
            Description = "日期 + 一句话问候，背景可调。",
            IconData = "M4,5 H20 V16 H11 L6,20 V16 H4 Z",
            Category = "basic",
            Style = "fancy",
            View = _greeting,
            ColSpan = 4,
            RowSpan = 2,
            DefaultCol = 0,
            DefaultRow = 0,
            Transparent = true,
            DefaultOpacity = 0,
            SetActive = active => _greeting.SetActive(active),
            SetEditing = editing => _greeting.Opacity = editing ? 0.75 : 1,
            OnShown = () => _greeting.Refresh(),
            ApplyTheme = () => _greeting.Refresh()
        };
        _widgets.Add(greetingWidget);

        _widgets.Add(new HomeWidget
        {
            Id = "minimal",
            Title = "标语卡片",
            Description = "居中的日期与标语轮播。",
            IconData = "M6,7 H18 M6,12 H15 M6,17 H10",
            Category = "basic",
            Style = "minimal",
            View = _minimal,
            ColSpan = 3,
            RowSpan = 2,
            SetActive = active => _minimal.SetActive(active)
        });

        // ── 便签：便签 + 待办，两栏铺满整页 ──
        _widgets.Add(new HomeWidget
        {
            Id = "note",
            Title = "便签",
            Description = "随手记一句话，自动保存。",
            IconData = "M5,4 H19 V14.5 L14.5,20 H5 Z M14.5,20 V14.5 H19 M8.5,9 H15.5 M8.5,12.5 H13",
            Category = "basic",
            Style = "notes",
            View = _notes,
            ColSpan = 5,
            RowSpan = 4,
            DefaultCol = 0,
            DefaultRow = 0,
            SetActive = active => _notes.SetActive(active),
            SetEditing = editing => _notes.SetEditing(editing),
            ApplyTheme = () => _notes.ApplyTheme()
        });

        _widgets.Add(new HomeWidget
        {
            Id = "todo",
            Title = "待办事项",
            Description = "勾掉一条少一条，带完成进度。",
            IconData = "M4,6.5 H6.5 V9 H4 Z M9,7.75 H20 M4,14.5 H6.5 V17 H4 Z M9,15.75 H20",
            Category = "basic",
            Style = "notes",
            View = _todos,
            ColSpan = 4,
            RowSpan = 4,
            DefaultCol = 5,
            DefaultRow = 0,
            SetActive = _ => { },
            SetEditing = editing => _todos.SetEditing(editing),
            OnShown = () => _todos.Reload(),
            ApplyTheme = () => _todos.ApplyTheme()
        });

        // ── 收藏内容：多个网站 ──
        _widgets.Add(new HomeWidget
        {
            Id = "sites",
            Title = "多个网站",
            Description = "收藏的网址列表，点一下用浏览器打开。",
            IconData = "M12,3 A9,9 0 1,1 11.99,3 Z M3,12 H21 M12,3 C15.4,6.4 15.4,17.6 12,21 C8.6,17.6 8.6,6.4 12,3",
            Category = "favorite",
            Style = "fancy",
            View = _sites,
            ColSpan = 3,
            RowSpan = 3,
            DefaultCol = 0,
            DefaultRow = 0,
            AutoPlace = false,
            CountOptions = new[] { 2, 4, 6, 8 },
            CountLabel = "显示 {0} 个网站",
            GetCount = () => Cfg.GetOption("sites.count", 4),
            SetCount = count => Cfg.SetOption("sites.count", count),
            OnShown = () => _sites.Reload(),
            Reload = () => _sites.Reload(),
            ApplyTheme = () => _sites.Reload()
        });

        // ── PCL 实用功能：下载 ──
        _widgets.Add(new HomeWidget
        {
            Id = "download",
            Title = "下载自定义文件",
            Description = "填个地址，用启动器的多线程引擎下载任意文件。",
            IconData = "M12,3 V15 M7.5,10.5 L12,15 L16.5,10.5 M4,19 H20",
            Category = "pcl",
            Style = "fancy",
            View = _download,
            ColSpan = 4,
            RowSpan = 3,
            DefaultCol = 0,
            DefaultRow = 0,
            AutoPlace = false,
            OnShown = () => _download.Reload(),
            ApplyTheme = () => _download.Reload()
        });

        // ── 多功能小组件：计算器 ──
        _widgets.Add(new HomeWidget
        {
            Id = "calculator",
            Title = "计算器",
            Description = "四则运算、括号、百分号，带可回填的历史记录。",
            IconData = "M6,3 H18 V21 H6 Z M9,6.5 H15 M9,11 H10 M13,11 H14 M9,14.5 H10 M13,14.5 H14 M9,18 H10 M13,18 H14",
            Category = "multi",
            Style = "fancy",
            View = _calculator,
            ColSpan = 3,
            RowSpan = 3,
            DefaultCol = 0,
            DefaultRow = 0,
            AutoPlace = false,
            ApplyTheme = () => _calculator.RefreshTheme()
        });

        // ── 多功能小组件：时钟 / 天气 ──
        _widgets.Add(new HomeWidget
        {
            Id = "clock",
            Title = "时钟",
            Description = "刻度表盘 / 饼干表盘 / 电子钟三种样式，可开关日期与秒。",
            IconData = "M12,3 A9,9 0 1,1 11.99,3 Z M12,7 V12 L15.5,14",
            Category = "multi",
            Style = "fancy",
            View = _clock,
            ColSpan = 3,
            RowSpan = 2,
            DefaultCol = 3,
            DefaultRow = 0,
            AutoPlace = false,
            OnShown = () => _clock.Refresh(),
            ApplyTheme = () => _clock.ApplyTheme()
        });

        _widgets.Add(new HomeWidget
        {
            Id = "weather",
            Title = "天气",
            Description = "超大温度 + 太阳云朵图标，可切成铺满样式。",
            IconData = "M7,18 H17.2 A3.8,3.8 0 0,0 17.2,10.4 A5.4,5.4 0 0,0 6.6,11.6 A3.4,3.4 0 0,0 7,18 Z M9,21 V22.5 M13,21 V22.5",
            Category = "multi",
            Style = "fancy",
            View = _weather,
            ColSpan = 3,
            RowSpan = 2,
            DefaultCol = 3,
            DefaultRow = 2,
            AutoPlace = false,
            OnShown = () => _weather.Refresh(),
            ApplyTheme = () => _weather.ApplyTheme()
        });

        LoadPins();
        LoadSkins();
    }

    // ─────────────────────────── 单项快捷入口 ───────────────────────────

    /// <summary>把配置里钉住的卡片建成真正的组件。</summary>
    private void LoadPins()
    {
        foreach (var pin in Cfg.Pins.ToList()) AttachPin(pin);
    }

    /// <summary>为一个快捷入口注册组件 + 内容视图（还没决定摆哪儿）。</summary>
    private void AttachPin(PinOptions pin)
    {
        if (_widgets.Any(w => w.Id == pin.Id)) return;

        var view = new PinnedView(pin);

        var widget = new HomeWidget
        {
            Id = pin.Id,
            Title = pin.Name,
            Description = "单项快捷入口。",
            Category = "pin",
            Style = Cfg.NormalizeStyle(pin.Style),
            View = view,
            ColSpan = 3,
            RowSpan = 2,
            Resizable = true,
            Dynamic = true,
            OnRemoved = () => Cfg.RemovePin(pin.Id),
            SetEditing = editing => view.SetEditing(editing),
            OnShown = () => view.Reload(true),
            ApplyTheme = () => view.Reload(true)
        };

        _widgets.Add(widget);

        // 运行时新加的卡片要当场建封面，否则只有数据没有界面
        if (!_hosts.ContainsKey(widget.Id))
        {
            var host = BuildCardHost(widget);
            host.Visibility = Visibility.Collapsed;
            _hosts[widget.Id] = host;
            _cells.Children.Add(host);
        }
    }

    /// <summary>添加一个快捷入口：分配 id、落配置、找空位、建卡片。</summary>
    private void AddPin(PinOptions proto)
    {
        // 之前删掉的同目标入口还在配置里，直接放回主页，不要再造一条
        var orphan = FindOrphanPin(proto.Kind, proto.Instance, proto.Target);
        if (orphan != null)
        {
            if (!Cfg.AddCard(_style, orphan.Id, 4, 2))
            {
                Log.Warn("添加快捷入口失败：没有空位");
                return;
            }

            ApplyStyle(_style);
            Log.Info($"已恢复快捷入口 {orphan.Kind}：{orphan.Name}（{_style}）");
            return;
        }

        var pin = Cfg.AddPin(proto.Kind, _style, proto.Instance, proto.Name, proto.Target);

        if (!Cfg.AddCard(_style, pin.Id, 4, 2))
        {
            Cfg.RemovePin(pin.Id);
            Log.Warn("添加快捷入口失败：没有空位");
            return;
        }

        AttachPin(pin);
        ApplyStyle(_style);
        Log.Info($"已添加快捷入口 {pin.Kind}：{pin.Name}（{_style}）");
    }

    /// <summary>添加一张网站卡片：url 与图标都跟着卡片自己走。</summary>
    private void AddSitePin(SiteEdit edit)
    {
        var orphan = FindOrphanPin("site", "", edit.Url);
        if (orphan != null)
        {
            if (!Cfg.AddCard(_style, orphan.Id, 4, 2))
            {
                Log.Warn("添加网站卡片失败：没有空位");
                return;
            }

            ApplyStyle(_style);
            Log.Info($"已恢复网站卡片：{orphan.Name}（{orphan.Target}）");
            return;
        }

        var pin = Cfg.AddPin("site", _style, "", edit.Name, edit.Url, edit.Icon);

        if (!Cfg.AddCard(_style, pin.Id, 4, 2))
        {
            Cfg.RemovePin(pin.Id);
            Log.Warn("添加网站卡片失败：没有空位");
            return;
        }

        AttachPin(pin);
        ApplyStyle(_style);
        Log.Info($"已添加网站卡片：{edit.Name}（{edit.Url}）");
    }

    /// <summary>
    /// 找一条「配置里还在、主页上已经没有」的同目标快捷入口。
    /// 删除布局只清卡片、不动配置，所以重新添加时应该把它放回来，而不是再插一条重复的。
    /// </summary>
    private PinOptions? FindOrphanPin(string kind, string instance, string target)
    {
        return Cfg.Pins.FirstOrDefault(p =>
            string.Equals(Cfg.NormalizeStyle(p.Style), _style, StringComparison.Ordinal)
            && string.Equals(p.Kind, kind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Instance ?? "", instance ?? "", StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Target ?? "", target ?? "", StringComparison.OrdinalIgnoreCase)
            && !Cfg.HasCard(_style, p.Id));
    }

    // ─────────────────────────── 皮肤预览 ───────────────────────────

    private void LoadSkins()
    {
        foreach (var skin in Cfg.Skins.ToList()) AttachSkin(skin);
    }

    /// <summary>把一条皮肤配置变成主页上的卡片。</summary>
    private void AttachSkin(SkinOptions skin)
    {
        if (_widgets.Any(w => w.Id == skin.Id)) return;

        var view = new SkinView();
        view.SetSkin(SkinLibrary.Load(skin), SkinLibrary.ResolveModel(skin));
        view.SetCaption(skin.Name);
        view.Configure(skin.HeadFollow, skin.Walking, skin.Bouncing, skin.DragRotate);

        // 正版皮肤没缓存时（换机、清过缓存、以前只按 uuid 存过）先显示 Steve，
        // 同时后台补拉一次，拿到就换上去，不用用户重新去设置里点一遍。
        if (skin.Source == "player" && !SkinLibrary.HasCache(skin.Value) && !string.IsNullOrWhiteSpace(skin.Value))
        {
            string playerName = skin.Value;
            _ = Task.Run(() => SkinLibrary.FetchPlayerAsync(playerName)).ContinueWith(task =>
            {
                if (!task.IsCompletedSuccessfully)
                {
                    Log.Warn($"皮肤补拉失败：{playerName}（{task.Exception?.GetBaseException().Message}）");
                    return;
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var fetch = task.Result;
                        view.SetSkin(fetch.Image,
                            fetch.Model == "slim" ? "slim" : SkinLibrary.ResolveModel(skin));
                        Log.Info($"皮肤补拉成功：{playerName}（{fetch.Model}）");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"皮肤补拉后更新失败：{ex.Message}");
                    }
                }));
            });
        }

        HomeWidget? widget = null;
        widget = new HomeWidget
        {
            Id = skin.Id,
            Title = string.IsNullOrWhiteSpace(skin.Name) ? "皮肤预览" : skin.Name,
            Description = "3D 皮肤预览：跟随鼠标、走路、拖动转身。",
            Category = "favorite",
            Style = Cfg.NormalizeStyle(skin.Style),
            View = view,
            ColSpan = 3,
            RowSpan = 3,
            Resizable = true,
            Dynamic = true,
            OnRemoved = () => Cfg.RemoveSkin(skin.Id),
            SetActive = active => view.SetActive(active),
            SetEditing = editing => view.SetEditing(editing),
            OnShown = () =>
            {
                view.SetActive(true);
                view.SetSkin(SkinLibrary.Load(skin), SkinLibrary.ResolveModel(skin));
            },
            ApplyTheme = () => { }
        };

        _widgets.Add(widget);

        if (!_hosts.ContainsKey(widget.Id))
        {
            var host = BuildCardHost(widget);
            host.Visibility = Visibility.Collapsed;
            _hosts[widget.Id] = host;
            _cells.Children.Add(host);
        }

        ApplyLook(widget);
    }

    /// <summary>添加一张皮肤卡片：先弹窗选皮肤，再落卡片。</summary>
    private void AddSkinCard()
    {
        var choice = SkinDialog.Show("添加皮肤预览", "选一个正版玩家、本地图片，或者直接用 Steve / Alex。");
        if (choice == null) return;

        var skin = Cfg.AddSkin(_style, choice.Name, choice.Source, choice.Value, choice.Model);
        if (!Cfg.AddCard(_style, skin.Id, 3, 3))
        {
            Cfg.RemoveSkin(skin.Id);
            Log.Warn("添加皮肤预览失败：没有空位");
            return;
        }

        AttachSkin(skin);
        ApplyStyle(_style);
        Log.Info($"已添加皮肤预览：{skin.Name}（{skin.Source}/{skin.Model}）");
    }

    private void ChangeSkin(SkinOptions skin, SkinView view)
    {
        var choice = SkinDialog.Show("更换皮肤", "换个玩家、换张图，或者回到 Steve / Alex。", skin);
        if (choice == null) return;

        skin.Name = choice.Name;
        skin.Source = choice.Source;
        skin.Value = choice.Value;
        skin.Model = choice.Model;
        Cfg.UpdateSkin(skin.Id, s =>
        {
            s.Name = choice.Name;
            s.Source = choice.Source;
            s.Value = choice.Value;
            s.Model = choice.Model;
        });

        SkinLibrary.Invalidate("file:" + choice.Value);
        SkinLibrary.Invalidate("player:" + choice.Value.ToLowerInvariant());
        view.SetSkin(SkinLibrary.Load(skin), SkinLibrary.ResolveModel(skin));
        view.SetCaption(choice.Name);
        Log.Info($"皮肤卡改为：{choice.Name}（{choice.Source}/{choice.Model}）");
    }

    // ─────────────────────────── 组件设置面板 ───────────────────────────

    /// <summary>右上角齿轮：打开这个组件自己的设置面板。</summary>
    private void OpenSettings(HomeWidget widget)
    {
        HideMenu();

        var skin = widget.View is SkinView ? Cfg.FindSkin(widget.Id) : null;
        var skinView = widget.View as SkinView;
        var pin = widget.Dynamic ? Cfg.Pins.FirstOrDefault(x => x.Id == widget.Id) : null;
        var clock = widget.View as ClockView;
        var weather = widget.View as WeatherView;
        bool greeting = widget.Id == "greeting";
        bool sites = widget.Id == "sites";

        bool changed = SettingsDialog.Show(
            $"{widget.Title} · 设置",
            "所有改动即时生效",
            dialog =>
            {
                // 问候的选项多，单独走两栏；其余组件是一栏「外观 + 自己的设置」
                if (greeting)
                {
                    AddGreetingSettings(dialog, widget);
                    return;
                }

                dialog.AddBackground(widget.Id, () => ApplyLook(widget), widget.DefaultOpacity);

                if (skin != null && skinView != null)
                {
                    AddSkinSettings(dialog, skin, skinView);
                }
                else if (clock != null)
                {
                    AddClockSettings(dialog, clock);
                }
                else if (weather != null)
                {
                    AddWeatherSettings(dialog, weather);
                }
                else if (sites)
                {
                    dialog.AddSection("网站列表");
                    dialog.AddNode(SitesDialog.BuildEditor(() => _sites.Reload()));
                    dialog.AddNote("这些网站会显示在这张卡片里，点一行就用浏览器打开。");
                }
                else if (pin != null)
                {
                    AddPinSettings(dialog, pin);
                }
                else if (widget.CountOptions.Length > 0 && widget.GetCount != null && widget.SetCount != null)
                {
                    // 显示条数原来藏在「⋯」里，现在并进设置面板
                    var options = widget.CountOptions;
                    int current = widget.GetCount();
                    int pick = 0;
                    for (int i = 1; i < options.Length; i++)
                    {
                        if (Math.Abs(options[i] - current) < Math.Abs(options[pick] - current)) pick = i;
                    }

                    dialog.AddSection("显示条数");
                    dialog.AddChoice("显示条数", options.Select(o => o + " 项").ToArray(), pick, value =>
                    {
                        widget.SetCount(options[Math.Clamp(value, 0, options.Length - 1)]);
                        widget.Reload?.Invoke();
                    }, "只影响这张卡片显示多少条");
                }
            },
            columns: greeting ? 2 : 1);

        if (changed) ApplyLook(widget);
    }

    /// <summary>皮肤组件的那几行。</summary>
    private void AddSkinSettings(SettingsDialog dialog, SkinOptions skin, SkinView skinView)
    {
        dialog.AddSection("皮肤");
        dialog.AddAction("当前皮肤", "更换皮肤…", () => ChangeSkin(skin, skinView));

        dialog.AddChoice("手臂模型", new[] { "经典 4px", "细 3px" },
            SkinLibrary.ResolveModel(skin) == "slim" ? 1 : 0,
            value =>
            {
                string model = value == 1 ? "slim" : "classic";
                skin.Model = model;
                Cfg.UpdateSkin(skin.Id, s => s.Model = model);
                skinView.SetSkin(SkinLibrary.Load(skin), model);
            },
            "Alex 那类皮肤用细手臂，Steve 用经典");

        dialog.AddToggle("头部跟随", "鼠标在卡片里移动，头会朝过去", skin.HeadFollow, value =>
        {
            skin.HeadFollow = value;
            Cfg.UpdateSkin(skin.Id, s => s.HeadFollow = value);
            skinView.Configure(skin.HeadFollow, skin.Walking, skin.Bouncing, skin.DragRotate);
        });

        dialog.AddToggle("走路动作", "手臂和腿前后摆动，像在原地迈步", skin.Walking, value =>
        {
            skin.Walking = value;
            Cfg.UpdateSkin(skin.Id, s => s.Walking = value);
            skinView.Configure(skin.HeadFollow, skin.Walking, skin.Bouncing, skin.DragRotate);
        });

        dialog.AddToggle("弹跳动作", "整个人上下弹跳、左右轻晃，四肢保持不动", skin.Bouncing, value =>
        {
            skin.Bouncing = value;
            Cfg.UpdateSkin(skin.Id, s => s.Bouncing = value);
            skinView.Configure(skin.HeadFollow, skin.Walking, skin.Bouncing, skin.DragRotate);
        });

        dialog.AddToggle("拖动转身", "按住卡片左右拖，能从不同角度看", skin.DragRotate, value =>
        {
            skin.DragRotate = value;
            Cfg.UpdateSkin(skin.Id, s => s.DragRotate = value);
            skinView.Configure(skin.HeadFollow, skin.Walking, skin.Bouncing, skin.DragRotate);
        });

        dialog.AddNote("走路和弹跳可以同时开，也可以只留一个；两个都关就是静止站姿。拖动只在「编辑布局」关闭时生效。");
    }

    /// <summary>时钟：样式 / 日期 / 秒。</summary>
    private void AddClockSettings(SettingsDialog dialog, ClockView view)
    {
        var options = Cfg.Clock;
        string[] keys = { "marks", "cookie", "digital" };
        int index = Array.IndexOf(keys, (options.Style ?? "marks").ToLowerInvariant());
        if (index < 0) index = 0;

        dialog.AddSection("时钟");
        dialog.AddChoice("样式", new[] { "刻度表盘", "饼干表盘", "电子钟" }, index, value =>
        {
            options.Style = keys[Math.Clamp(value, 0, keys.Length - 1)];
            Cfg.Save();
            view.Refresh();
        }, "刻度表盘：刻度数字压在盘边上；饼干表盘：扇贝形圆盘 + 顶部日期；电子钟：反色面板 + 等宽数字");

        dialog.AddToggle("显示日期", "刻度表盘在盘底、饼干表盘在顶部斜排", options.ShowDate, value =>
        {
            options.ShowDate = value;
            Cfg.Save();
            view.Refresh();
        });

        dialog.AddToggle("显示秒", "给刻度表盘加一根细秒针（饼干表盘没有秒针）", options.ShowSeconds, value =>
        {
            options.ShowSeconds = value;
            Cfg.Save();
            view.Refresh();
        });

        dialog.AddNote("背景透明度只作用在卡片底色上，表盘和指针始终是实色。");
    }

    /// <summary>天气：城市 / 样式 / 细节。</summary>
    private void AddWeatherSettings(SettingsDialog dialog, WeatherView view)
    {
        var options = Cfg.Weather;
        string[] keys = { "md3", "immersive" };
        int index = Array.IndexOf(keys, (options.Style ?? "md3").ToLowerInvariant());
        if (index < 0) index = 0;

        dialog.AddSection("天气");
        dialog.AddTextField("城市", "中文或拼音都行，比如「杭州」「Hangzhou」", options.City, text =>
        {
            options.City = (text ?? "").Trim();
            Cfg.Save();
        });

        dialog.AddChoice("样式", new[] { "圆盘", "铺满" }, index, value =>
        {
            options.Style = keys[Math.Clamp(value, 0, keys.Length - 1)];
            Cfg.Save();
            view.Refresh();
        }, "圆盘：浅色圆盘 + 大温度；铺满：整块卡片换成当前天气的底色，信息压在上面");

        dialog.AddToggle("显示体感与风", "温度下面那行「体感 · 湿度 · 风」", options.ShowDetails, value =>
        {
            options.ShowDetails = value;
            Cfg.Save();
            view.Refresh();
        });

        dialog.AddAction("刷新", "立刻重新获取", () => view.Refresh());

        dialog.AddNote("数据来自 Open-Meteo，不需要 API key；同一城市 10 分钟缓存一次。背景透明度只影响底色。");
    }

    /// <summary>单项快捷入口：这里换目标 / 改网站，不再占卡片右上角的菜单。</summary>
    private void AddPinSettings(SettingsDialog dialog, PinOptions pin)
    {
        var view = _widgets.FirstOrDefault(w => w.Id == pin.Id)?.View as PinnedView;

        dialog.AddSection(pin.Kind == "site" ? "网址" : "目标");

        if (pin.Kind == "site")
        {
            string name = pin.Name, url = pin.Target, icon = pin.Icon;

            void Apply()
            {
                Cfg.UpdatePin(pin.Id, pin.Instance, name, url, icon);
                pin.Name = name;
                pin.Target = url;
                pin.Icon = icon;
                view?.Reload(true);
            }

            dialog.AddTextField("名称", "留空就用域名", name, text =>
            {
                name = (text ?? "").Trim();
                Apply();
            });

            dialog.AddTextField("网址", "带不带 https:// 都行", url, text =>
            {
                url = (text ?? "").Trim();
                Apply();
            });

            dialog.AddTextField("图标地址", "留空就用默认的地球图标", icon, text =>
            {
                icon = (text ?? "").Trim();
                Apply();
            });

            dialog.AddNote("这三项改完直接生效，不用再开别的窗口。");
            return;
        }

        dialog.AddValue("当前目标", string.IsNullOrWhiteSpace(pin.Target) ? pin.Name : pin.Name);

        dialog.AddAction("更换目标", pin.Kind == "world" ? "换一个存档…" : pin.Kind == "server" ? "换一个服务器…" : "换一个实例…",
            () =>
            {
                var changed = PinFlow.Repick(pin);
                if (changed == null) return;

                Cfg.UpdatePin(pin.Id, changed.Instance, changed.Name, changed.Target);
                pin.Instance = changed.Instance;
                pin.Name = changed.Name;
                pin.Target = changed.Target;

                view?.Reload(true);
                Log.Info($"快捷入口改为：{changed.Name}");
            });

        dialog.AddNote("更换目标只改这张卡片指向的东西，卡片位置和背景都保留。");
    }

    /// <summary>问候组件的设置：左栏内容，右栏预览 + 外观，两栏并排压到一屏内。</summary>
    private void AddGreetingSettings(SettingsDialog dialog, HomeWidget widget)
    {
        var greeting = Cfg.Greeting;

        dialog.AddSection("问候内容");

        string[] modes = { "仅自动", "文字 + 自动", "仅自定义" };
        string[] keys = { "auto", "mix", "text" };
        int index = Array.IndexOf(keys, Cfg.GreetingMode);
        if (index < 0) index = 0;

        TextBlock? preview = null;
        void RefreshPreview()
        {
            if (preview == null) return;
            var texts = (Cfg.Greeting.Texts ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            preview.Text = Cfg.GreetingMode switch
            {
                "text" => texts.FirstOrDefault() ?? Cfg.DefaultSlogans[0],
                "mix" => Cfg.AutoGreeting(DateTime.Now) + "  ·  " + (texts.FirstOrDefault() ?? Cfg.DefaultSlogans[0]),
                _ => Cfg.AutoGreeting(DateTime.Now)
            };
        }

        dialog.AddChoice("显示方式", modes, index, value =>
        {
            greeting.Mode = keys[Math.Clamp(value, 0, keys.Length - 1)];
            Cfg.Save();
            _greeting.Refresh();
            RefreshPreview();
        });

        dialog.AddSlider("字号", "问候文字相对卡片的大小", 16, Math.Clamp(greeting.FontSize, 16, 32) - 16,
            value =>
            {
                greeting.FontSize = 16 + value;
                Cfg.Save();
                _greeting.Refresh();
            },
            value => (16 + value) + " px");

        string[] colors = { "跟随主题", "白色", "黑色" };
        string[] colorKeys = { "auto", "white", "black" };
        int colorIndex = Array.IndexOf(colorKeys, (greeting.Color ?? "auto").ToLowerInvariant());
        if (colorIndex < 0) colorIndex = 0;

        TextBlock? preview2 = null;
        Border? previewHost = null;
        void ApplyPreviewColor()
        {
            if (preview2 == null) return;

            string mode = (greeting.Color ?? "auto").ToLowerInvariant();
            preview2.Foreground = mode switch
            {
                "white" => Palette.Solid("#FFFFFF"),
                "black" => Palette.Solid("#121212"),
                _ => Palette.TextPrimary
            };

            // 白字得配深底、黑字配浅底，否则预览块里根本看不见
            if (previewHost != null)
            {
                previewHost.Background = mode switch
                {
                    "white" => Palette.Solid("#3A3F45"),
                    "black" => Palette.Solid("#F2F3F5"),
                    _ => M3.C.SurfaceContainerHighest
                };
            }
        }

        dialog.AddChoice("文字颜色", colors, colorIndex, value =>
        {
            greeting.Color = colorKeys[Math.Clamp(value, 0, colorKeys.Length - 1)];
            Cfg.Save();
            _greeting.Refresh();
            ApplyPreviewColor();
        }, "壁纸太亮时选黑色，太暗时选白色");

        dialog.AddMultiline("自定义标语", "一行一条，留空则用内置标语；轮换间隔仍是 8 秒",
            string.Join("\n", greeting.Texts ?? new List<string>()),
            text =>
            {
                greeting.Texts = (text ?? "")
                    .Split('\n')
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .ToList();

                Cfg.Save();
                _greeting.Refresh();
                RefreshPreview();
            });

        // 右栏：预览 + 外观。左栏是内容设置，两栏高度接近，窗口才不会顶出屏幕
        dialog.NextColumn();

        dialog.AddSection("预览");
        previewHost = dialog.AddPreview(() => Cfg.AutoGreeting(DateTime.Now), block =>
        {
            preview = block;
            preview2 = block;
        });
        ApplyPreviewColor();
        RefreshPreview();

        dialog.AddBackground(widget.Id, () => ApplyLook(widget), widget.DefaultOpacity);

        dialog.AddNote("「文字 + 自动」会先显示自动问候，再轮换你写的句子；问候卡默认完全透出壁纸，想加底色就调上面的「背景透明度」。");
    }

    /// <summary>把配置里的背景图片铺到卡片底层，并同步整卡底色的不透明度。</summary>
    private void ApplyLook(HomeWidget widget)
    {
        double cardOpacity = CardOpacityOf(widget);

        // 0 = 完全透出壁纸，1 = 完整色调容器（M3 卡片默认不透明）
        if (_hosts.TryGetValue(widget.Id, out var host) && host is M3Card card)
        {
            card.FillOpacity = cardOpacity;
        }
        else if (_fills.TryGetValue(widget.Id, out var plain))
        {
            // 透明组件（问候）：底色靠这层可选底板撑起来
            plain.Opacity = cardOpacity;
            plain.Visibility = cardOpacity > 0.001 ? Visibility.Visible : Visibility.Collapsed;
        }

        var look = Cfg.Look(widget.Id);
        if (!_looks.TryGetValue(widget.Id, out var layer)) return;

        if (look == null || string.IsNullOrWhiteSpace(look.Bg))
        {
            layer.Visibility = Visibility.Collapsed;
            layer.Background = null;
            return;
        }

        var image = ImageUtil.Load(look.Bg, 480);
        if (image == null)
        {
            layer.Visibility = Visibility.Collapsed;
            return;
        }

        layer.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        layer.Opacity = Math.Clamp(look.BgOpacity, 0, 1);
        layer.Visibility = Visibility.Visible;
    }

    /// <summary>单张卡的底色不透明度 = 自己的设置 × 全局倍率。</summary>
    private static double CardOpacityOf(HomeWidget widget)
    {
        var look = Cfg.Look(widget.Id);
        double own = Math.Clamp(look?.Opacity ?? widget.DefaultOpacity, 0, 1);
        double global = Math.Clamp(Cfg.Ui.CardOpacity, 0, 1);
        return Math.Clamp(own * global, 0, 1);
    }

    /// <summary>拖全局透明度滑块时只改底色，不重建整页。</summary>
    public void RefreshCardOpacity()
    {
        foreach (var widget in _widgets)
        {
            double value = CardOpacityOf(widget);

            if (_hosts.TryGetValue(widget.Id, out var host) && host is M3Card card)
            {
                card.FillOpacity = value;
            }
            else if (_fills.TryGetValue(widget.Id, out var plain))
            {
                plain.Opacity = value;
                plain.Visibility = value > 0.001 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// 把当前主页的布局恢复成默认排列（设置里「重置主页布局」）。
    /// 标准组件按默认位置放；用户自己加的快捷入口 / 皮肤卡保留，排到后面找空位。
    /// </summary>
    public void ResetLayout()
    {
        var defaults = _widgets
            .Where(w => w.Style == _style && !w.Dynamic)
            .Select(w => w.DefaultPlacement())
            .ToList();

        defaults.AddRange(_widgets
            .Where(w => w.Style == _style && w.Dynamic)
            .Select(w => Cfg.GetCard(_style, w.Id)));

        Cfg.ResetLayoutToDefaults(_style, defaults);
        ApplyStyle(_style);
        Log.Info($"[布局] 已重置「{_style}」为默认排列");
    }

    /// <summary>
    /// 配置里的布局被外部改写了（导入文件 / 从备份恢复）之后重新套一遍。
    /// 传入样式时会把主页一起切过去，顶栏的分段按钮也跟着换。
    /// </summary>
    public void ReloadLayout(string? style = null)
    {
        string target = Cfg.VisibleStyle(style ?? Cfg.CurrentStyle);
        ApplyStyle(target);

        try
        {
            _topBar.SetStyle(target, true);
        }
        catch (Exception ex)
        {
            Log.Warn("同步顶栏样式失败：" + ex.Message);
        }

        Log.Info($"[布局] 已重新应用「{target}」");
    }

    private void OnStyleChanged(string style)
    {
        try
        {
            Cfg.SetStyle(style);
            ApplyStyle(style);
            Log.Info($"主页样式切换为：{style}");
        }
        catch (Exception ex)
        {
            Log.Error("切换主页样式失败", ex);
        }
    }

    /// <summary>切换到指定样式：只显示该样式下「已被添加」的组件，并套用它们各自的位置。</summary>
    private void ApplyStyle(string style)
    {
        _style = Cfg.VisibleStyle(style);

        // 该样式第一次出现时才铺默认组件；用户删掉的不会自己回来。
        // 快捷入口是用户主动加的、AutoPlace=false 的组件留给「+」，都不进默认布局。
        Cfg.SeedLayout(_style, _widgets
            .Where(w => w.Style == _style && !w.Dynamic && w.AutoPlace)
            .Select(w => w.DefaultPlacement()));

        Select(null);

        foreach (var widget in _widgets.ToList())
        {
            if (!_hosts.TryGetValue(widget.Id, out var host)) continue;

            bool visible = string.Equals(widget.Style, _style, StringComparison.Ordinal)
                           && Cfg.HasCard(_style, widget.Id);
            bool wasVisible = host.Visibility == Visibility.Visible;

            host.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (visible)
            {
                var card = Cfg.GetCard(_style, widget.Id);

                // 计算器一开始是 3×4，改成 3×3 后把老布局收回一格
                if (widget.Id == "calculator" && card.RowSpan == 4)
                {
                    card.RowSpan = 3;
                    Cfg.Save();
                }
                Grid.SetColumn(host, card.Col);
                Grid.SetRow(host, card.Row);
                Grid.SetColumnSpan(host, card.ColSpan);
                Grid.SetRowSpan(host, card.RowSpan);

                // 快捷入口按「占几行」在两套版式之间切换
                if (widget.View is PinnedView pinView) pinView.SetRowSpan(card.RowSpan);

                if (!wasVisible) widget.OnShown?.Invoke();
                ApplyLook(widget);
            }

            widget.SetActive?.Invoke(visible);
            widget.SetEditing?.Invoke(_edit);
        }

        SyncRowCount();
        UpdateEmptyHint();

        // 换主页样式时整块网格淡入 + 轻微上浮，别硬切
        if (AnimateSwap())
        {
            _cells.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, M3.DefaultEffects) { EasingFunction = M3.Effects });

            _swapSlide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, M3.FastSpatial) { EasingFunction = M3.Spatial });
        }
    }

    private readonly TranslateTransform _swapSlide = new(0, 0);

    /// <summary>把网格淡出并往下沉一点，返回 true 表示动画已启动。</summary>
    private bool AnimateSwap()
    {
        if (_cells.Children.Count == 0) return false;

        if (_cells.RenderTransform is not TranslateTransform)
        {
            _cells.RenderTransform = _swapSlide;
        }

        _cells.Opacity = 0;
        _swapSlide.Y = 12;
        return true;
    }

    private void UpdateEmptyHint()
    {
        bool any = _widgets.Any(w => string.Equals(w.Style, _style, StringComparison.Ordinal)
                                     && Cfg.HasCard(_style, w.Id));
        _emptyHint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
    }

    public void OnDataChanged()
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(OnDataChanged));
                return;
            }

            if (IsWidgetVisible("calendar"))
            {
                _calendar.Rebuild();
                Log.Info($"收到数据变更：今天 {Cfg.TodayCount} 次，已刷新日历");
            }

            if (IsWidgetVisible("greeting")) _greeting.Refresh();
        }
        catch (Exception ex)
        {
            Log.Error("刷新日历时出错", ex);
        }
    }

    private bool IsWidgetVisible(string id) =>
        _hosts.TryGetValue(id, out var host) && host.Visibility == Visibility.Visible;

    private void UpdateRowHeight()
    {
        if (ActualWidth <= 0) return;

        // 一格高 = 1.5 格宽，所以行高按「宽度 ÷ 6」算，和 6 列时的观感一致
        double height = Math.Clamp(ActualWidth / Cfg.RowUnitDivisor, 68, 220);
        if (Math.Abs(height - _rowHeight) < 0.5) return;
        _rowHeight = height;
        foreach (var row in _cells.RowDefinitions) row.Height = new GridLength(_rowHeight);
    }

    /// <summary>行数跟着组件走：把网格收到最下面那张卡片的底边，不留空网格。</summary>
    private void SyncRowCount()
    {
        int needed = Cfg.MinRows;

        foreach (var widget in _widgets)
        {
            if (!_hosts.TryGetValue(widget.Id, out var host)) continue;
            if (host.Visibility != Visibility.Visible) continue;
            needed = Math.Max(needed, Grid.GetRow(host) + Grid.GetRowSpan(host));
        }

        // 编辑时多留一行，否则最底下那张卡没地方往下拖
        EnsureRows(needed + (_edit ? 1 : 0));
    }

    private void EnsureRows(int count)
    {
        count = Math.Clamp(count, Cfg.MinRows, Cfg.MaxSearchRows);
        if (count == _rows && _cells.RowDefinitions.Count == count) return;

        while (_cells.RowDefinitions.Count > count)
            _cells.RowDefinitions.RemoveAt(_cells.RowDefinitions.Count - 1);

        while (_cells.RowDefinitions.Count < count)
            _cells.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_rowHeight) });

        _rows = count;
        if (_emptyHint != null) Grid.SetRowSpan(_emptyHint, _rows);

        RebuildGridLines();
    }

    private void RebuildGridLines()
    {
        foreach (var line in _lines) _cells.Children.Remove(line);
        _lines.Clear();

        var brush = Palette.Solid(Palette.IsDark ? "#1FFFFFFF" : "#18000000");
        for (int row = 0; row < _rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                var line = new Border
                {
                    BorderThickness = new Thickness(1),
                    BorderBrush = brush,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(2),
                    IsHitTestVisible = false,
                    Visibility = _edit ? Visibility.Visible : Visibility.Collapsed
                };
                Grid.SetColumn(line, col);
                Grid.SetRow(line, row);
                Panel.SetZIndex(line, -1);
                _cells.Children.Add(line);
                _lines.Add(line);
            }
        }
    }

    /// <summary>卡片外壳优先复用启动器原生 MyCard，使圆角/透明度/模糊与启动器完全一致。</summary>
    private FrameworkElement BuildCardHost(HomeWidget widget)
    {
        var wrap = new Grid { ClipToBounds = true };

        // 背景层在最下面：用户给这张卡设了背景图就显示，否则保持透明
        var look = new Border
        {
            CornerRadius = new CornerRadius(M3.CardRadius),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(look, -2);
        wrap.Children.Add(look);
        _looks[widget.Id] = look;

        wrap.Children.Add(widget.View);

        // 透明交互层：编辑模式下盖住整张卡，组件内容（3D 视图等）就不会把鼠标事件吃掉
        var hit = new Border { Background = Brushes.Transparent, IsHitTestVisible = _edit };
        _hits[widget.Id] = hit;
        wrap.Children.Add(hit);

        FrameworkElement host;

        if (widget.Transparent)
        {
            // 问候这类组件默认不要底板：壁纸直接透出来，只有把「背景透明度」调上去才有底色
            var grid = new Grid { Background = Brushes.Transparent };

            var fill = new Border
            {
                CornerRadius = new CornerRadius(M3.CardRadius),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Background = M3.C.SurfaceLow
            };
            Panel.SetZIndex(fill, -3);
            grid.Children.Add(fill);
            _fills[widget.Id] = fill;

            grid.Children.Add(wrap);
            host = grid;
        }
        else
        {
            // M3 filled card：surface container low + 12dp 圆角 + 1 级阴影
            var card = new M3Card(M3.CardRadius)
            {
                Margin = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Top
            };
            card.Body.Children.Add(wrap);
            host = card;
        }

        host.Tag = widget.Id;
        host.AddHandler(UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler((_, e) => BeginDrag(widget, host, e)), true);
        host.MouseMove += OnDragMove;
        host.MouseLeftButtonUp += EndDrag;
        host.MouseEnter += (_, _) => HoverSelect(widget);
        WireHitLayer(widget, host);
        return host;
    }

    /// <summary>把透明交互层接到卡片上：悬停选中 + 按下开始拖动。</summary>
    private void WireHitLayer(HomeWidget widget, FrameworkElement host)
    {
        if (!_hits.TryGetValue(widget.Id, out var hit)) return;

        hit.Tag = widget.Id;
        hit.MouseEnter += (_, _) => HoverSelect(widget);
        hit.MouseLeftButtonDown += (_, e) => BeginDrag(widget, host, e);
        hit.MouseMove += OnDragMove;
        hit.MouseLeftButtonUp += EndDrag;
    }

    // ─────────────────────────── 编辑态 ───────────────────────────

    // 新手教程要用的几个口子：气泡指着「+」、指着某张卡片，或者替用户选中卡片
    internal FrameworkElement? TopBarAddButton => _topBar?.AddButtonEl;

    internal FrameworkElement? TopBarEditButton => _topBar?.EditButtonEl;

    internal FrameworkElement? HostOf(string id) =>
        _hosts.TryGetValue(id, out var host) ? host : null;

    internal void InvokeAddDialog() => ShowAddDialog();

    /// <summary>把页面滚到最上面，让顶栏（含「+」）露出来。新手教程要用。</summary>
    internal void ScrollToTop()
    {
        HookScroller();
        if (_scroller == null) return;

        try
        {
            _lastOffset = 0;
            _scroller.ScrollToVerticalOffset(0);
            _scrollSettled = true;
        }
        catch (Exception ex)
        {
            Log.Warn("滚动到顶部失败：" + ex.Message);
        }
    }

    internal void Toast(string text) => ShowSnackbar(text, null);

    internal void SelectCard(string id)
    {
        var widget = _widgets.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.Ordinal));
        if (widget != null) Select(widget);
    }

    public void SetEdit(bool on)
    {
        _edit = on;
        foreach (var widget in _widgets) widget.SetEditing?.Invoke(on);
        if (on) EnsureRows(_rows + 1);   // 进编辑时留一行余量，方便往下拖
        foreach (var line in _lines) line.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        foreach (var layer in _hits.Values) layer.IsHitTestVisible = on;
        _topBar.SetEdit(on);
        _hint.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        _ghost.Visibility = Visibility.Collapsed;
        HideMenu();
        if (!on)
        {
            CancelDrag();
            Select(null);
            SyncRowCount();              // 退出编辑把多留的那一行收回去
        }

        UpdateEmptyHint();
    }

    private void OnCellsMouseMove(object sender, MouseEventArgs e)
    {
        if (!_edit || _clearing || _removing || _dragHost != null || _resizeHost != null) return;

        DependencyObject? node = e.OriginalSource as DependencyObject;
        int guard = 0;
        while (node != null && !ReferenceEquals(node, _cells) && guard++ < 24)
        {
            if (node is FrameworkElement element && element.Tag is string id && _hosts.ContainsKey(id))
            {
                var widget = _widgets.Find(w => string.Equals(w.Id, id, StringComparison.Ordinal));
                if (widget != null) HoverSelect(widget);
                return;
            }

            node = VisualTreeHelper.GetParent(node);
        }
    }

    /// <summary>编辑模式下鼠标扫过卡片就选中，不用先点一下。</summary>
    private void HoverSelect(HomeWidget widget)
    {
        if (!_edit || _clearing || _removing || _dragHost != null || _resizeHost != null) return;
        if (ReferenceEquals(_selected, widget)) return;
        if (!_hosts.TryGetValue(widget.Id, out var host) || host.Visibility != Visibility.Visible) return;
        Select(widget);
    }

    private void Select(HomeWidget? widget)
    {
        if (ReferenceEquals(_selected, widget) && widget != null) return;

        _selected = widget;
        if (widget != null) Log.Info($"选中组件 {widget.Id}（{widget.Style}）");

        if (!_edit || widget == null ||
            !_hosts.TryGetValue(widget.Id, out var host) ||
            host.Visibility != Visibility.Visible)
        {
            _selection.Visibility = Visibility.Collapsed;
            return;
        }

        _selectionFrame.BorderBrush = Palette.Accent;
        _selectionTint.Background = M3.Alpha(Palette.Accent, 0.06);
        bool canRemove = widget.CanRemove;
        // 「⋯」里现在只剩删除，而删除就在旁边，没有额外的可调项就不弹这个多余的分隔菜单
        bool hasOptions = !string.IsNullOrEmpty(widget.ConfigureLabel);

        _deleteButton.Visibility = canRemove ? Visibility.Visible : Visibility.Collapsed;
        _moreButton.Visibility = hasOptions ? Visibility.Visible : Visibility.Collapsed;
        _settingsButton.Visibility = Visibility.Visible;
        _pillDividerTop.Visibility = hasOptions || canRemove ? Visibility.Visible : Visibility.Collapsed;
        _pillDivider.Visibility = canRemove && hasOptions ? Visibility.Visible : Visibility.Collapsed;
        _actionPill.Visibility = Visibility.Visible;
        _resizeGrip.Visibility = widget.Resizable ? Visibility.Visible : Visibility.Collapsed;

        _selection.Visibility = Visibility.Visible;
        SyncSelection(host);
        AnimateSelection();
    }

    /// <summary>选中框淡入，右上角的药丸从角上「弹」出来一点，跟着鼠标扫不会闪。</summary>
    private void AnimateSelection()
    {
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };

        _selection.BeginAnimation(OpacityProperty, null);
        _selection.Opacity = 0;
        _selection.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(90)) { EasingFunction = ease });

        var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
        var scale = new ScaleTransform(0.86, 0.86);
        _actionPill.RenderTransformOrigin = new Point(1, 0);
        _actionPill.RenderTransform = scale;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = pop });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = pop });
    }

    private void SyncSelection(FrameworkElement host)
    {
        Grid.SetColumn(_selection, Grid.GetColumn(host));
        Grid.SetRow(_selection, Grid.GetRow(host));
        Grid.SetColumnSpan(_selection, Grid.GetColumnSpan(host));
        Grid.SetRowSpan(_selection, Grid.GetRowSpan(host));
    }

    private void DeleteSelected()
    {
        var widget = _selected;
        if (widget == null || !widget.CanRemove) return;

        var host = _hosts.TryGetValue(widget.Id, out var found) ? found : null;
        bool animate = host != null && host.Visibility == Visibility.Visible;

        if (!Cfg.RemoveCard(widget.Style, widget.Id))
        {
            Log.Warn($"删除组件失败：{widget.Id}");
            return;
        }

        widget.OnRemoved?.Invoke();
        Log.Info($"已删除组件 {widget.Id}（{widget.Style}）");
        Select(null);

        if (animate) _removing = true;

        void Finish()
        {
            _removing = false;
            if (widget.Dynamic)
            {
                // 快捷入口是一次性的：连视图一起摘掉，别留在内存里
                if (host != null)
                {
                    _cells.Children.Remove(host);
                    _hosts.Remove(widget.Id);
                }

                _widgets.Remove(widget);
            }

            ApplyStyle(_style);
        }

        if (animate && host != null) AnimateOut(host, Finish, 0);
        else Finish();
    }

    // ─────────────────────────── 删除整个布局 ───────────────────────────

    private void DeleteLayout()
    {
        if (!_edit) return;

        var live = _widgets
            .Where(w => string.Equals(w.Style, _style, StringComparison.Ordinal) && Cfg.HasCard(_style, w.Id))
            .ToList();

        if (live.Count == 0)
        {
            ShowSnackbar("当前布局已经是空的了", null);
            return;
        }

        int pick = Modal.Show(
            "删除整个布局？",
            $"将移除当前布局里的 {live.Count} 个小组件，删除后 6 秒内可以撤销。",
            new ModalButton("取消", -2, danger: true),
            new ModalButton("确认", 0, danger: true),
            new ModalButton("取消", -3));

        if (pick != 0) return;

        var snapshot = Cfg.SnapshotLayout(_style);
        _clearing = true;
        Cfg.ClearLayout(_style);
        Select(null);

        var hosts = live
            .Select(w => _hosts.TryGetValue(w.Id, out var h) ? h : null)
            .Where(h => h != null && h.Visibility == Visibility.Visible)
            .Cast<FrameworkElement>()
            .OrderBy(h => Grid.GetRow(h))
            .ThenBy(h => Grid.GetColumn(h))
            .ToList();

        Log.Info($"已删除布局 {_style}：{hosts.Count} 个组件");

        if (hosts.Count == 0)
        {
            AfterClear();
            return;
        }

        int pending = hosts.Count;
        for (int i = 0; i < hosts.Count; i++)
        {
            AnimateOut(hosts[i], () =>
            {
                if (--pending > 0) return;
                AfterClear();
            }, i * 45);
        }

        void AfterClear()
        {
            _clearing = false;
            ApplyStyle(_style);
            ShowSnackbar($"已删除 {live.Count} 个小组件", () =>
            {
                Cfg.RestoreLayout(_style, snapshot);
                ApplyStyle(_style);
                PlayEnterAnimation(force: true);
                Log.Info($"已撤销删除布局 {_style}");
            });
        }
    }

    // ─────────────────────────── 添加组件 ───────────────────────────

    private bool _addDialogOpen;

    /// <summary>「+」：弹「添加高级主页小组件」，选完再走对应的选择流程。</summary>
    private void ShowAddDialog()
    {
        if (_addDialogOpen) return;
        _addDialogOpen = true;

        string? pick;
        try
        {
            // 「已在主页」要看当前布局，而不是内存里注册过哪些组件：
            // 否则删掉的组件会一直显示成「已添加」，再也点不回来。
            var current = _widgets
                .Where(w => string.Equals(w.Style, _style, StringComparison.Ordinal)
                            && Cfg.HasCard(_style, w.Id))
                .Select(w => w.Id)
                .ToList();

            pick = AddWidgetDialog.Show(_style, current);
        }
        finally
        {
            _addDialogOpen = false;
        }

        if (string.IsNullOrEmpty(pick)) return;

        // 皮肤预览：先弹窗选皮肤
        if (pick == "skin")
        {
            Dispatcher.BeginInvoke(new Action(AddSkinCard));
            return;
        }

        // 单个网站要填名称 / 网址 / 图标，先弹编辑窗再落卡片
        if (pick == "pin:site")
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var edit = SiteDialog.Show("添加网站");
                    if (edit != null) AddSitePin(edit);
                }
                catch (Exception ex)
                {
                    Log.Error("添加网站卡片失败", ex);
                }
            }));
            return;
        }

        if (pick.StartsWith("pin:", StringComparison.Ordinal))
        {
            string kind = pick.Substring(4);

            // 紧接着还要弹两个选择窗口，等上一轮事件收尾再开
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var proto = PinFlow.Create(kind);
                    if (proto != null) AddPin(proto);
                }
                catch (Exception ex)
                {
                    Log.Error("添加快捷入口失败", ex);
                }
            }));
            return;
        }

        var widget = _widgets.FirstOrDefault(w => w.Id == pick && w.Style == _style);
        if (widget == null)
        {
            Log.Warn($"添加组件失败：找不到 {pick}");
            return;
        }

        if (Cfg.HasCard(_style, widget.Id))
        {
            Log.Info($"组件 {widget.Id} 已经在主页上");
            return;
        }

        AddWidget(widget);
        Onboarding.OnCardAdded(this, widget.Id);
    }

    private void HideMenu() => _menuLayer.Visibility = Visibility.Collapsed;

    private void ShowWidgetMenu(HomeWidget widget)
    {
        _widgetMenuItems.Children.Clear();

        if (!string.IsNullOrEmpty(widget.ConfigureLabel) && widget.Configure != null)
        {
            _widgetMenuItems.Children.Add(BuildMenuItem(widget.ConfigureLabel, () =>
            {
                HideMenu();
                // 等这次鼠标事件走完再开模态窗口，否则弹窗会卡在按下状态里
                Dispatcher.BeginInvoke(new Action(() => widget.Configure()));
            }));
        }

        if (widget.CanRemove)
        {
            if (_widgetMenuItems.Children.Count > 0) _widgetMenuItems.Children.Add(BuildSeparator());
            _widgetMenuItems.Children.Add(BuildMenuItem("删除小组件", () =>
            {
                HideMenu();
                DeleteSelected();
            }, null, true));
        }

        if (_widgetMenuItems.Children.Count == 0)
        {
            _widgetMenuItems.Children.Add(BuildMenuItem("这个组件没有可调项", null));
        }

        // 贴在选中卡片的右上角
        double left = 4, top = 40;
        try
        {
            if (_selected != null && _hosts.TryGetValue(_selected.Id, out var host))
            {
                var point = host.TranslatePoint(new Point(0, 0), this);
                left = Math.Max(4, Math.Min(point.X + host.ActualWidth - 180, ActualWidth - 184));
                top = point.Y + 28;
            }
        }
        catch
        {
            // 布局未完成时用默认位置
        }

        _widgetMenu.Margin = new Thickness(left, top, 0, 0);
        _widgetMenu.Visibility = Visibility.Visible;
        _menuLayer.Visibility = Visibility.Visible;

        // 菜单从锚点侧弹出来：淡入 + 轻微缩放
        var pop = _widgetMenu.RenderTransform as ScaleTransform;
        if (pop == null)
        {
            _widgetMenu.RenderTransformOrigin = new Point(0, 0);
            pop = new ScaleTransform(0.96, 0.96);
            _widgetMenu.RenderTransform = pop;
        }

        _widgetMenu.Opacity = 0;
        pop.ScaleX = 0.96;
        pop.ScaleY = 0.96;
        _widgetMenu.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1, M3.FastEffects) { EasingFunction = M3.Effects });
        pop.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
        pop.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
    }

    private FrameworkElement BuildSeparator()
    {
        return new Border
        {
            Height = 1,
            Margin = new Thickness(6, 4, 6, 4),
            Background = Palette.Solid(Palette.IsDark ? "#1FFFFFFF" : "#14000000")
        };
    }

    private FrameworkElement BuildMenuItem(string title, Action? onClick, bool? check = null, bool danger = false)
    {
        var text = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = danger ? M3.C.Error
                : onClick == null ? M3.C.OnSurfaceVariant
                : M3.C.OnSurface
        };

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (check == true)
        {
            var tick = new TextBlock
            {
                Text = "\u2713",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = M3.C.Primary
            };
            Grid.SetColumn(tick, 0);
            content.Children.Add(tick);
        }

        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        var item = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerXs),
            Padding = new Thickness(10, 8, 12, 8),
            Background = Brushes.Transparent,
            Child = content,
            Cursor = onClick == null ? Cursors.Arrow : Cursors.Hand
        };

        if (onClick != null)
        {
            var hover = M3.Layer(M3.Of(M3.C.SurfaceContainer), M3.Of(M3.C.OnSurface), M3.HoverAlpha);
            item.MouseEnter += (_, _) => item.Background = hover;
            item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
            item.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
        }

        return item;
    }

    private void AddWidget(HomeWidget widget)
    {
        if (!Cfg.AddCard(_style, widget.Id, widget.ColSpan, widget.RowSpan))
        {
            Log.Warn($"添加组件失败：{widget.Id}");
            return;
        }

        Log.Info($"已添加组件 {widget.Id}（{_style}）");
        ApplyStyle(_style);
    }

    // ─────────────────────────── 移动 ───────────────────────────

    private void BeginDrag(HomeWidget widget, FrameworkElement host, MouseButtonEventArgs e)
    {
        if (!_edit) return;
        if (_dragHost != null) return;
        if (host.Visibility != Visibility.Visible) return;

        _dragWidget = widget;
        _dragHost = host;
        _dragArmed = true;                       // 还没超过拖动阈值，先只记录
        _dragStart = e.GetPosition(_cells);
        _dragOriginCol = Grid.GetColumn(host);
        _dragOriginRow = Grid.GetRow(host);
        _dragTransform = new TranslateTransform();
        host.RenderTransform = _dragTransform;
        host.CaptureMouse();
        e.Handled = true;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (_dragHost == null || _dragTransform == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var point = e.GetPosition(_cells);
        double dx = point.X - _dragStart.X;
        double dy = point.Y - _dragStart.Y;

        // 超过阈值才真正开始拖动，避免单击就把卡片挪走
        if (_dragArmed)
        {
            if (Math.Abs(dx) < 6 && Math.Abs(dy) < 6) return;
            _dragArmed = false;
            _dragHost.Cursor = Cursors.SizeAll;
            Panel.SetZIndex(_dragHost, 1000);
            _ghost.Visibility = Visibility.Visible;
            UpdateGhost(_dragOriginCol, _dragOriginRow, _dragHost);
        }

        if (_dragTransform is TranslateTransform translate)
        {
            translate.X = dx;
            translate.Y = dy;
        }

        double cellW = _cells.ActualWidth / Cols;
        if (cellW <= 0) return;
        int col = (int)Math.Round(_dragOriginCol + dx / cellW);
        int row = (int)Math.Round(_dragOriginRow + dy / _rowHeight);
        int colSpan = Grid.GetColumnSpan(_dragHost);
        int rowSpan = Grid.GetRowSpan(_dragHost);
        col = Math.Clamp(col, 0, Cols - colSpan);
        row = Math.Max(0, row);
        if (row + rowSpan > _rows) EnsureRows(row + rowSpan + 1);   // 拖到底就自动加行
        UpdateGhost(col, row, _dragHost);
        e.Handled = true;
    }

    private void UpdateGhost(int col, int row, FrameworkElement host)
    {
        Grid.SetColumn(_ghost, col);
        Grid.SetRow(_ghost, row);
        Grid.SetColumnSpan(_ghost, Grid.GetColumnSpan(host));
        Grid.SetRowSpan(_ghost, Grid.GetRowSpan(host));
    }

    private void EndDrag(object sender, MouseButtonEventArgs e)
    {
        if (_dragHost == null) return;

        var host = _dragHost;
        var widget = _dragWidget;
        var point = e.GetPosition(_cells);
        double dx = point.X - _dragStart.X;
        double dy = point.Y - _dragStart.Y;

        // 点击（几乎无位移）= 选中，而不是移动
        bool click = _dragArmed || (Math.Abs(dx) < 8 && Math.Abs(dy) < 8);
        ResetDrag();

        if (click)
        {
            Select(widget);
            return;
        }

        double cellW = _cells.ActualWidth / Cols;
        int colSpan = Grid.GetColumnSpan(host);
        int rowSpan = Grid.GetRowSpan(host);
        int col = cellW > 0 ? (int)Math.Round(_dragOriginCol + dx / cellW) : _dragOriginCol;
        int row = (int)Math.Round(_dragOriginRow + dy / _rowHeight);
        col = Math.Clamp(col, 0, Cols - colSpan);
        row = Math.Max(0, row);

        if (col == _dragOriginCol && row == _dragOriginRow) return;

        // 落在另一张同尺寸卡片的正上方 → 两张互换，而不是叠在一起
        var swapped = FindWidgetAt(widget?.Id, col, row, colSpan, rowSpan);
        if (swapped != null && _hosts.TryGetValue(swapped.Id, out var swapHost))
        {
            Grid.SetColumn(swapHost, _dragOriginCol);
            Grid.SetRow(swapHost, _dragOriginRow);
            Grid.SetColumnSpan(swapHost, colSpan);
            Grid.SetRowSpan(swapHost, rowSpan);
            Cfg.SetCard(swapped.Style, swapped.Id, _dragOriginCol, _dragOriginRow, colSpan, rowSpan);
            Log.Info($"组件 {swapped.Id} 与 {widget?.Id} 互换位置（第 {_dragOriginRow + 1} 行 ↔ 第 {row + 1} 行）");
        }

        Grid.SetColumn(host, col);
        Grid.SetRow(host, row);

        if (widget != null)
        {
            Cfg.SetCard(widget.Style, widget.Id, col, row, colSpan, rowSpan);
            Log.Info($"组件 {widget.Id}（{widget.Style}）移动到 第 {row + 1} 行 第 {col + 1} 列（{colSpan}×{rowSpan}）");
            if (ReferenceEquals(widget, _selected)) SyncSelection(host);
        }

        SyncRowCount();
        e.Handled = true;
    }

    /// <summary>找一张正好铺满目标格子的同尺寸卡片（用来做位置互换）。</summary>
    private HomeWidget? FindWidgetAt(string? exceptId, int col, int row, int colSpan, int rowSpan)
    {
        foreach (var other in _widgets)
        {
            if (string.Equals(other.Id, exceptId, StringComparison.Ordinal)) continue;
            if (!string.Equals(other.Style, _style, StringComparison.Ordinal)) continue;
            if (!_hosts.TryGetValue(other.Id, out var host) || host.Visibility != Visibility.Visible) continue;
            if (Grid.GetColumn(host) != col || Grid.GetRow(host) != row) continue;
            if (Grid.GetColumnSpan(host) != colSpan || Grid.GetRowSpan(host) != rowSpan) continue;
            return other;
        }

        return null;
    }

    private void ResetDrag()
    {
        if (_dragHost == null) return;
        _dragHost.ReleaseMouseCapture();
        _dragHost.RenderTransform = null;
        _dragHost.Cursor = Cursors.Arrow;
        Panel.SetZIndex(_dragHost, 0);
        _dragHost = null;
        _dragWidget = null;
        _dragTransform = null;
        _dragArmed = false;
        _ghost.Visibility = Visibility.Collapsed;
    }

    private void CancelDrag() => ResetDrag();

    // ─────────────────────────── 缩放 ───────────────────────────

    private void OnResizeStart(object sender, MouseButtonEventArgs e)
    {
        if (!_edit || _selected == null || !_selected.Resizable) return;
        if (!_hosts.TryGetValue(_selected.Id, out var host)) return;

        _resizeWidget = _selected;
        _resizeHost = host;
        _resizeStart = e.GetPosition(_cells);
        _resizeOriginCol = Grid.GetColumn(host);
        _resizeOriginRow = Grid.GetRow(host);
        _resizeOriginColSpan = Grid.GetColumnSpan(host);
        _resizeOriginRowSpan = Grid.GetRowSpan(host);
        _resizeGrip.CaptureMouse();
        e.Handled = true;
    }

    private void OnResizeMove(object sender, MouseEventArgs e)
    {
        if (_resizeHost == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        double cellW = _cells.ActualWidth / Cols;
        if (cellW <= 0) return;

        var point = e.GetPosition(_cells);
        int colSpan = _resizeOriginColSpan + (int)Math.Round((point.X - _resizeStart.X) / cellW);
        int rowSpan = _resizeOriginRowSpan + (int)Math.Round((point.Y - _resizeStart.Y) / _rowHeight);
        colSpan = Math.Clamp(colSpan, 1, Cols - _resizeOriginCol);
        rowSpan = Math.Max(1, rowSpan);
        if (_resizeOriginRow + rowSpan > _rows) EnsureRows(_resizeOriginRow + rowSpan + 1);

        Grid.SetColumnSpan(_resizeHost, colSpan);
        Grid.SetRowSpan(_resizeHost, rowSpan);
        if (_resizeWidget?.View is PinnedView pinView) pinView.SetRowSpan(rowSpan);
        SyncSelection(_resizeHost);
        e.Handled = true;
    }

    private void OnResizeEnd(object sender, MouseButtonEventArgs e)
    {
        if (_resizeHost == null) return;

        _resizeGrip.ReleaseMouseCapture();
        var host = _resizeHost;
        var widget = _resizeWidget;
        _resizeHost = null;
        _resizeWidget = null;

        int colSpan = Grid.GetColumnSpan(host);
        int rowSpan = Grid.GetRowSpan(host);
        if (colSpan == _resizeOriginColSpan && rowSpan == _resizeOriginRowSpan) return;
        if (widget == null) return;

        Cfg.SetCard(widget.Style, widget.Id, Grid.GetColumn(host), Grid.GetRow(host), colSpan, rowSpan);
        Log.Info($"组件 {widget.Id} 调整为 {colSpan}×{rowSpan} 格");
        SyncRowCount();
        e.Handled = true;
    }

    // ─────────────────────────── 装饰 ───────────────────────────

    private static Border BuildPillIcon(FrameworkElement icon, string tip)
    {
        var border = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = icon
        };
        ToolTipService.SetToolTip(border, tip);
        border.MouseEnter += (_, _) => border.Background = Palette.Solid("#30FFFFFF");
        border.MouseLeave += (_, _) => border.Background = Brushes.Transparent;
        return border;
    }

    // ─────────────────────────── 动画 ───────────────────────────

    /// <summary>卡片删除：缩一下 + 淡出，比直接消失自然。</summary>
    private static void AnimateOut(FrameworkElement host, Action done, int delayMs)
    {
        try
        {
            var ease = new QuinticEase { EasingMode = EasingMode.EaseIn };
            var delay = TimeSpan.FromMilliseconds(delayMs);
            var duration = TimeSpan.FromMilliseconds(190);

            host.RenderTransformOrigin = new Point(0.5, 0.45);
            var scale = new ScaleTransform(1, 1);
            var lift = new TranslateTransform(0, 0);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(lift);
            host.RenderTransform = group;

            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.93, duration) { BeginTime = delay, EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.93, duration) { BeginTime = delay, EasingFunction = ease });
            lift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(-7, duration) { BeginTime = delay, EasingFunction = ease });

            var fade = new DoubleAnimation(0, duration)
            {
                BeginTime = delay,
                EasingFunction = ease
            };

            fade.Completed += (_, _) =>
            {
                host.BeginAnimation(OpacityProperty, null);
                host.RenderTransform = null;
                host.Opacity = 1;
                done();
            };

            host.BeginAnimation(OpacityProperty, fade);
        }
        catch (Exception ex)
        {
            Log.Warn("删除动画失败：" + ex.Message);
            host.RenderTransform = null;
            host.Opacity = 1;
            done();
        }
    }

    /// <summary>从别的页面回到主页时，卡片按行列顺序错开淡入。</summary>
    private void PlayEnterAnimation(bool force = false)
    {
        try
        {
            if (!IsVisible) return;

            var now = DateTime.UtcNow;
            if (!force && (now - _lastEnter).TotalMilliseconds < 1200) return;
            _lastEnter = now;

            var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var hosts = _widgets
                .Select(w => _hosts.TryGetValue(w.Id, out var h) ? h : null)
                .Where(h => h != null && h.Visibility == Visibility.Visible)
                .Cast<FrameworkElement>()
                .OrderBy(h => Grid.GetRow(h))
                .ThenBy(h => Grid.GetColumn(h))
                .ToList();

            for (int i = 0; i < hosts.Count; i++)
            {
                var host = hosts[i];
                var delay = TimeSpan.FromMilliseconds(Math.Min(i, 10) * 36);

                var lift = new TranslateTransform(0, 10);
                host.RenderTransform = lift;
                host.Opacity = 0;

                var fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260))
                {
                    BeginTime = delay,
                    EasingFunction = ease
                };
                fade.Completed += (_, _) =>
                {
                    if (ReferenceEquals(host.RenderTransform, lift)) host.RenderTransform = null;
                };

                host.BeginAnimation(OpacityProperty, fade);
                lift.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(0, TimeSpan.FromMilliseconds(320))
                    {
                        BeginTime = delay,
                        EasingFunction = ease
                    });
            }
        }
        catch (Exception ex)
        {
            Log.Warn("入场动画失败：" + ex.Message);
        }
    }

    // ─────────────────────────── 撤销提示 ───────────────────────────

    private void ShowSnackbar(string text, Action? undo)
    {
        _snackbarText.Text = text;
        _undoAction = undo;
        _snackbarAction.Visibility = undo == null ? Visibility.Collapsed : Visibility.Visible;

        _snackbar.Visibility = Visibility.Visible;
        _snackbar.BeginAnimation(OpacityProperty, null);
        _snackbar.Opacity = 0;

        var lift = new TranslateTransform(0, 16);
        _snackbar.RenderTransform = lift;

        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        _snackbar.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
        lift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });

        // 6 秒进度条，让「还能撤销多久」一眼可见
        _snackbarProgress.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _snackbarProgress.ScaleX = 1;
        _snackbarProgress.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0, TimeSpan.FromSeconds(6)));

        _snackTimer.Stop();
        _snackTimer.Start();
    }

    private void HideSnackbar()
    {
        _snackTimer.Stop();
        _undoAction = null;
        if (_snackbar.Visibility != Visibility.Visible) return;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) =>
        {
            _snackbar.Visibility = Visibility.Collapsed;
            _snackbar.RenderTransform = null;
        };
        _snackbar.BeginAnimation(OpacityProperty, fade);
    }

    private static FrameworkElement CreateCrossIcon(double size, Brush brush)
    {
        var canvas = new Canvas { Width = size, Height = size };
        double k = size / 10.0;
        canvas.Children.Add(new Path
        {
            Data = Geometry.Parse($"M {2.4 * k},{2.4 * k} L {7.6 * k},{7.6 * k}"),
            Stroke = brush,
            StrokeThickness = 1.8 * k,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        canvas.Children.Add(new Path
        {
            Data = Geometry.Parse($"M {7.6 * k},{2.4 * k} L {2.4 * k},{7.6 * k}"),
            Stroke = brush,
            StrokeThickness = 1.8 * k,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        return canvas;
    }

    /// <summary>三条横线 + 三个滑块的「设置」图标。</summary>
    private static FrameworkElement CreateSlidersIcon(double size, Brush brush)
    {
        var canvas = new Canvas { Width = size, Height = size };
        double k = size / 14.0;

        double[] rows = { 3.6, 7.0, 10.4 };
        double[] knobs = { 9.2, 4.6, 10.6 };

        for (int i = 0; i < rows.Length; i++)
        {
            double y = rows[i] * k;
            canvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse($"M {1.2 * k},{y} L {12.8 * k},{y}"),
                Stroke = brush,
                StrokeThickness = 1.3 * k,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });

            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 2.6 * k,
                Height = 2.6 * k,
                Stroke = brush,
                StrokeThickness = 1.2 * k,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(dot, knobs[i] * k - 1.3 * k);
            Canvas.SetTop(dot, y - 1.3 * k);
            canvas.Children.Add(dot);
        }

        return canvas;
    }

    private static FrameworkElement CreateGripIcon(double size, Brush brush)
    {
        var canvas = new Canvas { Width = size, Height = size };
        double k = size / 10.0;
        canvas.Children.Add(new Path
        {
            Data = Geometry.Parse($"M {8.6 * k},{2.2 * k} L {2.2 * k},{8.6 * k}"),
            Stroke = brush,
            StrokeThickness = 1.6 * k,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        canvas.Children.Add(new Path
        {
            Data = Geometry.Parse($"M {8.6 * k},{5.6 * k} L {5.6 * k},{8.6 * k}"),
            Stroke = brush,
            StrokeThickness = 1.6 * k,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        return canvas;
    }

    public void Refresh()
    {
        try
        {
            // 配色方案（种子色）换了以后，顶栏和卡片外壳里存的是旧画笔，必须一起重建
            BuildTopBar();
            foreach (var widget in _widgets)
            {
                widget.ApplyTheme?.Invoke();
                if (_hosts.TryGetValue(widget.Id, out var host) && host is M3Card card)
                    card.RefreshTheme();
            }

            if (IsWidgetVisible("calendar")) _calendar.Rebuild();
            RefreshCardOpacity();
        }
        catch (Exception ex)
        {
            Log.Error("刷新主页失败", ex);
        }
    }
}

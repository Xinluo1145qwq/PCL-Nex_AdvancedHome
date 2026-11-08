using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 高级主页设置窗口（M3 模态）。
///
/// 版式：
///   · 左侧固定导航栏（#D7DAE1），最上面是搜索框——输关键词直接过滤右边的设置项；
///     下面是 通用 / 外观 / 关于，GitHub 反馈钉在最底部；侧栏不再折叠；
///   · 右侧是唯一滚动区（#E5E8EF），三段设置按「通用 → 外观 → 关于」连续排列，
///     滚到底停住，点导航平滑滚到对应段落，反过来滚动时导航高亮跟随。
/// </summary>
internal sealed class UiSettingsDialog : Window
{
    private const string RepoUrl = "https://github.com/Xinluo1145qwq/PCL-Nex_AdvancedHome";
    private const string IssueUrl = "https://github.com/Xinluo1145qwq/PCL-Nex_AdvancedHome/issues";
    private const string AuthorUrl = "https://github.com/Xinluo1145qwq";

    // 24dp / 1.8 描边的图标，和插件里其它图标同一套画法
    private const string IconSearch = "M10.5,4.5 A6,6 0 1 1 10.4,4.5 Z M15.1,15.1 L20,20";
    private const string IconGeneral =
        "M4,7 H8.5 M14.5,7 H20 M11.5,7 m-3,0 a3,3 0 1 0 6,0 a3,3 0 1 0 -6,0 " +
        "M4,17 H6.5 M12.5,17 H20 M9.5,17 m-3,0 a3,3 0 1 0 6,0 a3,3 0 1 0 -6,0";
    private const string IconLook =
        "M12,3.6 C12,3.6 5.6,10.3 5.6,14.2 A6.4,6.4 0 0 0 18.4,14.2 C18.4,10.3 12,3.6 12,3.6 Z";
    private const string IconInfo = "M12,3.2 A8.8,8.8 0 1 1 11.9,3.2 Z M12,10.6 V16.9 M12,7.5 V7.7";
    private const string IconBug =
        "M9,8.2 A3,3 0 0 1 15,8.2 V14.2 A3,3 0 0 1 9,14.2 Z " +
        "M9.4,7.7 L7.8,5.9 M14.6,7.7 L16.2,5.9 " +
        "M9,10.8 H6.2 M15,10.8 H17.8 M9,13.6 H6.2 M15,13.6 H17.8";
    private const string IconEye =
        "M12,5.2 C6.9,5.2 3.1,9.1 1.9,12 C3.1,14.9 6.9,18.8 12,18.8 C17.1,18.8 20.9,14.9 22.1,12 C20.9,9.1 17.1,5.2 12,5.2 Z M12,15.1 A3.1,3.1 0 1 0 12,8.9 A3.1,3.1 0 1 0 12,15.1 Z";
    private const string IconEyeOff =
        "M4.2,4.2 L19.8,19.8 M9.4,5.6 C10.2,5.4 11.1,5.2 12,5.2 C17.1,5.2 20.9,9.1 22.1,12 C21.4,13.6 20.3,15.2 18.8,16.5 M6.1,7.1 C4.2,8.6 2.7,10.6 1.9,12 C3.1,14.9 6.9,18.8 12,18.8 C13.2,18.8 14.4,18.6 15.5,18.2";
    private const string IconCheck = "M5,12.5 L10,17.5 L19,6.5";
    private const string IconReset = "M19,12 A7,7 0 1 1 16.9,6.9 M19,4.4 V8.4 H15";
    private const string IconExport = "M12,15.5 V4.5 M8,8.5 L12,4.5 L16,8.5 M5,14.5 V19 H19 V14.5";
    private const string IconImport = "M12,4.5 V15.5 M8,11.5 L12,15.5 L16,11.5 M5,14.5 V19 H19 V14.5";
    private const string IconRestore = "M9,5.5 L4.5,10 L9,14.5 M4.5,10 H13.5 A5,5 0 0 1 13.5,20 H10";
    private const string IconBackup =
        "M5,7.5 C5,6.1 8.1,5 12,5 C15.9,5 19,6.1 19,7.5 C19,8.9 15.9,10 12,10 C8.1,10 5,8.9 5,7.5 Z " +
        "M5,7.5 V16.5 C5,17.9 8.1,19 12,19 C15.9,19 19,17.9 19,16.5 V7.5";
    private const string IconFlask =
        "M9.6,4.6 H14.4 M10.6,4.6 V10 L6.3,18.1 A1.4,1.4 0 0 0 7.5,20.2 H16.5 A1.4,1.4 0 0 0 17.7,18.1 " +
        "L13.4,10 V4.6 M8.4,14.6 H15.6";
    private const string IconGrid =
        "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z";

    private const double NavWidth = 268;

    private static readonly string[] NavTitles = { "通用", "外观", "关于" };
    private static readonly string[] NavIcons = { IconGeneral, IconLook, IconInfo };
    private static readonly string[] NavHints = { "字体、顶部菜单、布局与测试", "配色方案", "作者与数据" };

    // ── 搜索索引：卡片 → 行 → 文本 ──
    private sealed class SearchRow
    {
        public FrameworkElement Root = null!;
        public Border? Divider;
        public string Text = "";
    }

    private sealed class SearchCard
    {
        public FrameworkElement Root = null!;
        public string Title = "";
        public string Section = "";
        public readonly List<SearchRow> Rows = new();
    }

    private sealed class SearchSection
    {
        public FrameworkElement Root = null!;
        public string Title = "";
        public readonly List<SearchCard> Cards = new();
    }

    private readonly List<NavItem> _nav = new();
    private readonly List<NavItem> _allNav = new();
    private readonly List<SearchSection> _search = new();
    private readonly List<(string Key, Border Ring, FrameworkElement Check)> _swatches = new();
    private SearchSection? _current;

    private Border _shell = null!;
    private Grid _root = null!;
    private Border? _toast;
    private DispatcherTimer? _toastTimer;
    private Border _navPane = null!;
    private ScrollViewer _scroll = null!;
    private StackPanel _content = null!;
    private StackPanel _empty = null!;

    private Border _searchField = null!;
    private TextBox _searchBox = null!;
    private TextBlock _searchPlaceholder = null!;
    private Border _searchClear = null!;
    private TextBlock _searchHint = null!;

    private bool _filtering;
    private bool _ready;
    private int _active;
    private DispatcherTimer? _unlock;
    private DispatcherTimer? _saveTimer;

    // 通用
    private Border _fontButton = null!;
    private TextBlock _fontLabel = null!;
    private TextBlock _sizeValue = null!;
    private Popup? _fontPopup;
    private string[]? _fonts;
    private TextBlock _ioStatus = null!;

    // 外观
    private readonly Stopwatch _watch = new();
    private DispatcherTimer? _ticker;
    private double _from;
    private double _to;
    private int _pending;
    private bool _spy = true;

    private UiSettingsDialog()
    {
        Dlg.Setup(this, "高级主页设置", 1120, scrim: true);
        SizeToContent = SizeToContent.Manual;
        ResizeMode = ResizeMode.NoResize;

        BuildShell();
        MountShell();

        // 打开动画的这几百毫秒里不响应点击：否则「点入口」的 MouseUp 会落到
        // 新窗口正好在同一个屏幕位置上的控件（上一次就是它替用户点了 GitHub）。
        foreach (var item in _allNav) item.Enabled = false;
        _unlock = new DispatcherTimer(TimeSpan.FromMilliseconds(340), DispatcherPriority.Background,
            (_, _) =>
            {
                _unlock?.Stop();
                _ready = true;
                foreach (var item in _allNav) item.Enabled = true;
            }, Dispatcher);
        _unlock.Start();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (_searchBox.Text.Length > 0)
                {
                    ClearSearch();
                    _searchBox.Focus();
                }
                else
                {
                    Finish();
                }

                e.Handled = true;
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                e.Handled = true;
            }
        };
    }

    /// <summary>比普通弹窗更大：设置页要放得下预览和滑块，边距收到 20dp。</summary>
    private void MountShell()
    {
        Dlg.Mount(this, _shell);
        _shell.Margin = new Thickness(20);
    }

    private void Finish() => Close();

    public static void Show()
    {
        try
        {
            var dialog = new UiSettingsDialog();
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Error("打开高级主页设置失败", ex);
        }
    }

    // ─────────────────────────── 骨架 ───────────────────────────

    private void BuildShell()
    {
        var (width, height) = FitSize();

        var root = new Grid { Width = width, Height = height };
        _root = root;
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _navPane = BuildNav();
        root.Children.Add(_navPane);

        var right = BuildContentHost();
        Grid.SetColumn(right, 1);
        root.Children.Add(right);

        _shell = Dlg.Shell(root, new Thickness(0));
        _shell.Background = UiSkin.Canvas;
        SetActive(0);
    }

    /// <summary>弹窗不能高过宿主窗口：底部被裁掉的话，用户会觉得「滑不动」。</summary>
    private (double Width, double Height) FitSize()
    {
        double width = 1120, height = 760;

        double hostWidth = Width > 200 ? Width : 0;
        double hostHeight = Height > 200 ? Height : 0;
        if (hostWidth <= 0 || hostHeight <= 0)
        {
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
        }

        if (hostWidth > 420) width = Math.Min(width, hostWidth - 40);
        if (hostHeight > 420) height = Math.Min(height, hostHeight - 40);

        return (Math.Max(860, width), Math.Max(560, height));
    }

    // ─────────────────────────── 左栏 ───────────────────────────

    private Border BuildNav()
    {
        _nav.Clear();
        _allNav.Clear();

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 左上角：搜索（MD3 search bar）
        var top = new StackPanel { Margin = new Thickness(16, 16, 16, 0) };
        top.Children.Add(BuildSearchField());

        _searchHint = new TextBlock
        {
            FontSize = 11.5,
            Margin = new Thickness(4, 8, 0, 0),
            Foreground = UiSkin.Dim,
            Visibility = Visibility.Collapsed
        };
        top.Children.Add(_searchHint);
        grid.Children.Add(top);

        // 中部：三个导航项
        var navList = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        for (int i = 0; i < NavTitles.Length; i++)
        {
            int index = i;
            var item = new NavItem(NavIcons[i], NavTitles[i], NavHints[i], () =>
            {
                ClearSearch();
                ScrollToSection(index, true);
            })
            {
                ToolTip = UiStyles.Tooltip(NavTitles[i] + " · " + NavHints[i])
            };
            _nav.Add(item);
            _allNav.Add(item);
            navList.Children.Add(item);
        }
        Grid.SetRow(navList, 1);
        grid.Children.Add(navList);

        // 底部：分隔线 + GitHub 反馈 + 版本
        var bottom = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        bottom.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(18, 0, 18, 8),
            Background = UiSkin.Paint("#C3C8D2")
        });

        var github = new NavItem(IconBug, "反馈 Bug", "去 GitHub 提 issue", () => OpenUrl(IssueUrl))
        {
            ToolTip = UiStyles.Tooltip("在 GitHub 上反馈 Bug（github.com/Xinluo1145qwq/PCL-Nex_AdvancedHome/issues）")
        };
        bottom.Children.Add(github);
        _allNav.Add(github);

        bottom.Children.Add(new TextBlock
        {
            Text = VersionText(),
            FontSize = 11,
            Margin = new Thickness(30, 8, 0, 0),
            Foreground = UiSkin.Dim
        });

        Grid.SetRow(bottom, 2);
        grid.Children.Add(bottom);

        return new Border
        {
            Width = NavWidth,
            Background = UiSkin.Nav,
            ClipToBounds = true,
            Child = grid
        };
    }

    private string VersionText()
    {
        try
        {
            return typeof(UiSettingsDialog).Assembly.GetName().Version is { } v
                ? $"高级主页 v{v.Major}.{v.Minor}.{v.Build}"
                : "高级主页";
        }
        catch
        {
            return "高级主页";
        }
    }

    /// <summary>左上角搜索框：48dp、全圆角、放大镜 + 输入 + 清除按钮。</summary>
    private FrameworkElement BuildSearchField()
    {
        var icon = M3.Icon(IconSearch, UiSkin.Dim, 18);
        icon.Margin = new Thickness(14, 0, 0, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;

        _searchBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(9, 0, 4, 0),
            FontSize = 13.5,
            Foreground = UiSkin.Ink,
            CaretBrush = UiSkin.L.Primary,
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null,
            AcceptsReturn = false
        };

        _searchPlaceholder = new TextBlock
        {
            Text = "搜索设置",
            FontSize = 13.5,
            Foreground = UiSkin.Dim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0, 0, 0),
            IsHitTestVisible = false
        };

        _searchClear = M3IconButton.Create("M7,7 L17,17 M17,7 L7,17", ClearSearch, "清除搜索", 0.7,
            scheme: UiSkin.L);
        _searchClear.Margin = new Thickness(0, 0, 6, 0);
        _searchClear.VerticalAlignment = VerticalAlignment.Center;
        _searchClear.Visibility = Visibility.Collapsed;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(icon);
        Grid.SetColumn(_searchBox, 1);
        grid.Children.Add(_searchBox);
        Grid.SetColumn(_searchPlaceholder, 1);
        grid.Children.Add(_searchPlaceholder);
        Grid.SetColumn(_searchClear, 2);
        grid.Children.Add(_searchClear);

        _searchField = new Border
        {
            Height = 48,
            CornerRadius = new CornerRadius(24),
            Background = UiSkin.Field,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.FieldLine,
            Child = grid
        };

        _searchField.MouseLeftButtonUp += (_, _) => _searchBox.Focus();

        _searchBox.TextChanged += (_, _) =>
        {
            bool has = _searchBox.Text.Length > 0;
            _searchPlaceholder.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            _searchClear.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            ApplySearch(_searchBox.Text);
        };

        _searchBox.GotKeyboardFocus += (_, _) => _searchField.BorderBrush = UiSkin.L.Primary;
        _searchBox.LostKeyboardFocus += (_, _) => _searchField.BorderBrush = UiSkin.FieldLine;

        return _searchField;
    }

    private void ClearSearch()
    {
        if (_searchBox == null || _searchBox.Text.Length == 0) return;
        _searchBox.Text = "";
    }

    // ─────────────────────────── 搜索过滤 ───────────────────────────

    /// <summary>
    /// 关键词过滤：行命中就留，整张卡没有命中就藏起来，整段没有命中就连标题一起藏。
    /// 卡片标题和段落名也算命中关键词——搜「配色」时整张配色卡都该出来。
    /// </summary>
    private void ApplySearch(string raw)
    {
        string query = (raw ?? "").Trim();
        bool filtering = query.Length > 0;
        _filtering = filtering;

        int hits = 0;
        bool firstSection = true;

        foreach (var section in _search)
        {
            bool sectionHit = false;

            foreach (var card in section.Cards)
            {
                bool cardHit = false;
                bool previousVisible = false;

                foreach (var row in card.Rows)
                {
                    bool hit = !filtering
                              || row.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                              || card.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                              || card.Section.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

                    row.Root.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
                    if (row.Divider != null)
                    {
                        row.Divider.Visibility = hit && previousVisible
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }

                    if (hit)
                    {
                        cardHit = true;
                        previousVisible = true;
                        hits++;
                    }
                }

                card.Root.Visibility = cardHit ? Visibility.Visible : Visibility.Collapsed;
                if (cardHit) sectionHit = true;
            }

            section.Root.Visibility = sectionHit ? Visibility.Visible : Visibility.Collapsed;
            if (sectionHit)
            {
                section.Root.Margin = new Thickness(0, firstSection ? 0 : 38, 0, 0);
                firstSection = false;
            }
        }

        if (_empty != null)
        {
            _empty.Visibility = filtering && hits == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_searchHint != null)
        {
            _searchHint.Visibility = filtering ? Visibility.Visible : Visibility.Collapsed;
            if (filtering)
            {
                _searchHint.Text = hits == 0 ? "没有匹配的设置" : $"找到 {hits} 项设置";
            }
        }

        SetActive(filtering ? -1 : CurrentSection());

        if (filtering && _scroll != null)
        {
            _spy = false;
            _scroll.ScrollToVerticalOffset(0);
            _spy = true;
        }
    }

    /// <summary>清空搜索时按当前滚动位置重新点亮导航，而不是粗暴地跳回「通用」。</summary>
    private int CurrentSection()
    {
        if (_scroll == null || _search.Count == 0) return 0;

        double y = _scroll.VerticalOffset + 96;
        int index = 0;
        for (int i = 0; i < _search.Count; i++)
        {
            if (Position(_search[i].Root) <= y) index = i;
        }

        if (_scroll.ScrollableHeight > 8 && _scroll.VerticalOffset >= _scroll.ScrollableHeight - 2)
            index = _search.Count - 1;
        return index;
    }

    // ─────────────────────────── 右栏 ───────────────────────────

    private FrameworkElement BuildContentHost()
    {
        var content = new StackPanel
        {
            Margin = new Thickness(40, 32, 36, 48),
            MaxWidth = 820,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _content = content;
        _search.Clear();

        content.Children.Add(SectionBlock(0, "通用", "字体、顶部菜单、布局数据与测试。", BuildGeneralBody));
        content.Children.Add(SectionBlock(1, "外观", "配色方案会立刻作用到主页与这个窗口。", BuildAppearanceBody));
        content.Children.Add(SectionBlock(2, "关于", "版本、作者与数据维护；插件以 MIT 协议开源。", BuildAboutBody));

        _empty = BuildEmptyState();
        content.Children.Add(_empty);

        _scroll?.ScrollChanged -= OnScrollChanged;
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Content = content
        };

        try
        {
            var bar = UiSkin.ThinScrollBar();
            if (bar != null) scroll.Resources[typeof(ScrollBar)] = bar;
        }
        catch (Exception ex)
        {
            Log.Warn("滚动条样式失败：" + ex.Message);
        }

        scroll.ScrollChanged += OnScrollChanged;
        scroll.PreviewMouseWheel += (_, _) => StopScroll();
        _scroll = scroll;

        if (!string.IsNullOrEmpty(_searchBox?.Text)) ApplySearch(_searchBox.Text);

        var host = new Grid();
        host.Children.Add(scroll);

        var close = M3IconButton.Create("M6,6 L18,18 M18,6 L6,18", Finish, "关闭", scheme: UiSkin.L);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(0, 12, 12, 0);
        host.Children.Add(close);

        return host;
    }

    private FrameworkElement SectionBlock(int index, string title, string desc, Func<FrameworkElement> buildBody)
    {
        var stack = new StackPanel { Margin = new Thickness(0, index == 0 ? 0 : 38, 0, 0) };

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink,
            Margin = new Thickness(4, 0, 0, 0)
        });

        stack.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim,
            Margin = new Thickness(4, 6, 0, 0)
        });

        // 先登记段落，再建内容：Card() 要靠 _current 把卡片挂到这一段上
        var record = new SearchSection { Root = stack, Title = title };
        _search.Add(record);

        var previous = _current;
        _current = record;

        var body = buildBody();
        body.Margin = new Thickness(0, 16, 0, 0);
        stack.Children.Add(body);

        _current = previous;
        return stack;
    }

    private StackPanel BuildEmptyState()
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 72, 0, 0),
            Visibility = Visibility.Collapsed
        };

        var icon = M3.Icon(IconSearch, UiSkin.Paint("#A9AFBB"), 40);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(icon);

        stack.Children.Add(new TextBlock
        {
            Text = "没有找到匹配的设置",
            FontSize = 15,
            FontWeight = FontWeights.Medium,
            Foreground = UiSkin.Ink,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "换个关键词试试，比如「字体」「配色」「备份」。",
            FontSize = 12.5,
            Foreground = UiSkin.Dim,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0)
        });

        return stack;
    }

    private void ScrollToSection(int index, bool animate)
    {
        if (_search.Count == 0) return;
        index = Math.Clamp(index, 0, _search.Count - 1);
        _pending = index;

        // 指示器立刻跟手（M3 的导航是「点哪亮哪」），滚动再慢慢追过去
        SetActive(index);

        if (!animate)
        {
            _scroll.ScrollToVerticalOffset(0);
            return;
        }

        _spy = false;
        AnimateScroll(Math.Max(0, Position(_search[index].Root) - 4));
        FadeContent();
    }

    /// <summary>切页时内容轻微收一下再回来，滚动才有「换了一段」的感觉。</summary>
    private void FadeContent()
    {
        if (_content == null) return;

        var fade = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(520)
        };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0.86, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        });

        _content.BeginAnimation(OpacityProperty, fade);
    }

    private double Position(FrameworkElement element)
    {
        try
        {
            return element.TranslatePoint(new Point(0, 0), _content).Y;
        }
        catch
        {
            return 0;
        }
    }

    private void AnimateScroll(double target)
    {
        double max = Math.Max(0, _scroll.ExtentHeight - _scroll.ViewportHeight);
        target = Math.Clamp(target, 0, max);

        _from = _scroll.VerticalOffset;
        _to = target;
        if (Math.Abs(_to - _from) < 0.5)
        {
            _spy = true;
            SetActive(_pending);
            return;
        }

        _watch.Restart();
        _ticker ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render,
            (_, _) => TickScroll(), Dispatcher);
        _ticker.Start();
    }

    private void TickScroll()
    {
        double t = Math.Clamp(_watch.Elapsed.TotalMilliseconds / 480.0, 0, 1);
        // emphasize-decelerate：起步快、尾巴长，停得稳
        double eased = 1 - Math.Pow(1 - t, 3);
        _scroll.ScrollToVerticalOffset(_from + (_to - _from) * eased);

        if (t < 1) return;

        StopScroll();
        _spy = true;
        SetActive(_pending);
    }

    private void StopScroll()
    {
        if (_ticker is { IsEnabled: true }) _ticker.Stop();
        _spy = true;

        if (_content != null)
        {
            _content.BeginAnimation(OpacityProperty, null);
            _content.Opacity = 1;
        }
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_spy || _filtering || _search.Count == 0) return;
        SetActive(CurrentSection());
    }

    private void SetActive(int index)
    {
        if (_nav.Count == 0) return;
        _active = index < 0 ? -1 : Math.Clamp(index, 0, _nav.Count - 1);
        for (int i = 0; i < _nav.Count; i++) _nav[i].SetActive(i == _active);
    }

    // ─────────────────────────── 通用 ───────────────────────────

    private FrameworkElement BuildGeneralBody()
    {
        var stack = new StackPanel();

        _fontLabel = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(Cfg.Ui.FontFamily) ? "跟随启动器" : Cfg.Ui.FontFamily,
            FontSize = 14,
            Foreground = UiSkin.Ink,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 220,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var fontRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        fontRow.Children.Add(_fontLabel);
        var chevron = M3.Icon("M7,10 L12,15 L17,10", UiSkin.Dim, 18);
        chevron.Margin = new Thickness(6, 0, 0, 0);
        fontRow.Children.Add(chevron);

        _fontButton = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerS),
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.FieldLine,
            Padding = new Thickness(12, 7, 8, 7),
            Background = Brushes.Transparent,
            Child = fontRow,
            Cursor = Cursors.Hand
        };
        _fontButton.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            ToggleFontPopup();
        };
        _fontButton.MouseEnter += (_, _) => _fontButton.Background = UiSkin.Hover(UiSkin.Card);
        _fontButton.MouseLeave += (_, _) => _fontButton.Background = Brushes.Transparent;

        // 字号：− 22 +
        _sizeValue = new TextBlock
        {
            Text = ((int)Math.Round(Cfg.Ui.FontSize)).ToString(),
            FontSize = 15,
            FontWeight = FontWeights.Medium,
            Foreground = UiSkin.Ink,
            Width = 34,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var stepper = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        stepper.Children.Add(StepButton("M5,12 H19", () => ChangeFontSize(-1)));
        stepper.Children.Add(_sizeValue);
        stepper.Children.Add(StepButton("M12,5 V19 M5,12 H19", () => ChangeFontSize(1)));

        stack.Children.Add(Card("字体", new[]
        {
            Row("选择字体", "主页上所有文字都用这个字体；选「跟随启动器」则和启动器一致", _fontButton),
            Row("字体大小", "问候文字的字号，16–32", stepper)
        }));

        // 顶部菜单：一排三个开关（和主页顶部的切换按钮同款），眼睛长在名字后面
        var editSwitch = new M3Switch(Cfg.Ui.ShowEditButton, scheme: UiSkin.L);
        editSwitch.Changed += value =>
        {
            Cfg.Ui.ShowEditButton = value;
            Cfg.Save();
            HomeInjector.RequestHomeRefresh();
        };

        stack.Children.Add(Card("主页顶部菜单", new FrameworkElement[]
        {
            Row("显示的主页", "点一下切换显示；关掉的主页不会出现在顶部菜单里", BuildStyleToggles()),
            Row("显示编辑模式按钮", "关掉后顶部只留主页切换；进编辑仍可从设置里打开", editSwitch)
        }, "把「极简」「便签」都关掉，顶部就只剩花样主页，切换按钮会自动隐藏。"));

        stack.Children.Add(BuildBackupCard());
        stack.Children.Add(BuildTutorialCard());
        stack.Children.Add(BuildTestCard());

        return stack;
    }

    /// <summary>主页显示开关：三颗 filter chip 并排，眼睛就是选中态。</summary>
    private FrameworkElement BuildStyleToggles()
    {
        var inner = new StackPanel { Orientation = Orientation.Horizontal };
        inner.Children.Add(StyleToggle("花样", true, null));
        inner.Children.Add(StyleToggle("极简", Cfg.Ui.ShowMinimal, value =>
        {
            Cfg.Ui.ShowMinimal = value;
            Cfg.Save();
            HomeInjector.RequestHomeRefresh();
        }));
        inner.Children.Add(StyleToggle("便签", Cfg.Ui.ShowNotes, value =>
        {
            Cfg.Ui.ShowNotes = value;
            Cfg.Save();
            HomeInjector.RequestHomeRefresh();
        }));

        return new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(2),
            Background = UiSkin.CardAlt,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.Line,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = inner
        };
    }

    private FrameworkElement StyleToggle(string label, bool on, Action<bool>? set)
    {
        bool locked = set == null;
        bool state = on;

        var text = new TextBlock
        {
            Text = label,
            FontSize = 13.5,
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center
        };

        var eye = MakeIcon(state ? IconEye : IconEyeOff, UiSkin.L.OnSecondaryContainer, 17);
        eye.Margin = new Thickness(5, 0, 0, 0);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(text);
        row.Children.Add(eye);

        var segment = new Border
        {
            Height = 38,
            CornerRadius = new CornerRadius(19),
            Padding = new Thickness(13, 0, 13, 0),
            Background = state ? UiSkin.L.SecondaryContainer : Brushes.Transparent,
            Child = row,
            Cursor = locked ? Cursors.Arrow : Cursors.Hand,
            Opacity = state ? 1 : 0.72,
            ToolTip = UiStyles.Tooltip(locked
                ? "花样是兜底主页，不能隐藏"
                : (state ? "隐藏这个主页" : "显示这个主页"))
        };
        text.Foreground = state ? UiSkin.L.OnSecondaryContainer : UiSkin.Dim;

        if (!locked)
        {
            segment.MouseEnter += (_, _) =>
            {
                if (!state) segment.Background = UiSkin.Hover(UiSkin.CardAlt);
            };
            segment.MouseLeave += (_, _) =>
            {
                if (!state) segment.Background = Brushes.Transparent;
            };
            segment.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                state = !state;

                var fg = state ? UiSkin.L.OnSecondaryContainer : UiSkin.Dim;
                M3.AnimateFill(segment, state ? UiSkin.L.SecondaryContainer : Brushes.Transparent);
                text.Foreground = fg;
                eye.Data = Geometry.Parse(state ? IconEye : IconEyeOff);
                eye.Stroke = fg;
                segment.Opacity = state ? 1 : 0.72;
                segment.ToolTip = UiStyles.Tooltip(state ? "隐藏这个主页" : "显示这个主页");

                set!(state);
            };
        }

        return segment;
    }

    private Border StepButton(string icon, Action onClick)
    {
        var button = M3IconButton.Create(icon, onClick, null, 0.85, scheme: UiSkin.L);
        button.Width = 34;
        button.Height = 34;
        button.CornerRadius = new CornerRadius(17);
        return button;
    }

    private void ChangeFontSize(int delta)
    {
        double next = Math.Clamp(Cfg.Ui.FontSize + delta, 16, 32);
        if (Math.Abs(next - Cfg.Ui.FontSize) < 0.01) return;

        Cfg.Ui.FontSize = next;
        Cfg.Greeting.FontSize = (int)Math.Round(next);
        _sizeValue.Text = ((int)Math.Round(next)).ToString();
        Cfg.Save();
        HomeInjector.RequestHomeRefresh();
    }

    // ───────────────────── 重置与备份 ─────────────────────

    private FrameworkElement BuildBackupCard()
    {
        _ioStatus = new TextBlock
        {
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim,
            Text = "备份存在 AdvancedHomepage\\backups；重置、导入、恢复都只改组件位置，不动配色和字体。"
        };

        var import = M3Button.Create("导入…", ImportLayout, M3ButtonKind.Tonal,
            iconData: IconImport, scheme: UiSkin.L);
        var restore = M3Button.Create("恢复…", RestoreFromBackup, M3ButtonKind.Outlined,
            iconData: IconRestore, scheme: UiSkin.L);
        var reset = M3Button.Create("重置", ResetHomeLayout, M3ButtonKind.Text, danger: true,
            iconData: IconReset, scheme: UiSkin.L);
        var export = M3Button.Create("导出…", ExportLayout, M3ButtonKind.Outlined,
            iconData: IconExport, scheme: UiSkin.L);
        var backup = M3Button.Create("备份", BackupLayout, M3ButtonKind.Tonal,
            iconData: IconBackup, scheme: UiSkin.L);

        var rows = new List<FrameworkElement>
        {
            Row("从备份恢复", "挑一份备份文件，原样还原三套主页的摆位", restore),
            Row("导入主页布局", "从导出的 txt 或 config 备份 json 读回布局", import),
            Row("重置主页布局", "把当前主页恢复成默认排列，不能撤销", reset),
            Row("导出主页布局", "三套主页的组件位置写进一个 txt 文件", export),
            Row("备份主页布局", "把配置文件整份复制到备份文件夹", backup),
            SimpleRow(_ioStatus)
        };

        return Card("重置与备份", rows, "被重置了就从「从备份恢复」里挑最近的备份找回来。");
    }

    /// <summary>把布局包写回配置并让主页立刻重排，两个入口（导入 / 恢复）共用。</summary>
    private void ApplyBundle(LayoutTools.LayoutBundle bundle, string verb)
    {
        LayoutTools.Apply(bundle, restoreStyle: true);

        var canvas = HomeInjector.Active;
        if (canvas != null) canvas.ReloadLayout(bundle.Style);
        else HomeInjector.RequestHomeRefresh();

        string extra = bundle.UnknownCount > 0
            ? $"；其中 {bundle.UnknownCount} 个组件当前版本没有，已记下但不会显示"
            : "";
        _ioStatus.Text = $"已{verb}：{bundle.Summary}{extra}";
    }

    private void ImportLayout()
    {
        string? path = null;
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入主页布局",
                Filter = "布局文件 (*.txt;*.json)|*.txt;*.json|文本文件 (*.txt)|*.txt|配置备份 (*.json)|*.json",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true) return;
            path = dialog.FileName;

            var bundle = LayoutTools.LoadBundle(path);
            string name = System.IO.Path.GetFileName(path);

            bool ok = ConfirmDialog.Ask(this, "导入主页布局",
                $"{name}\n{bundle.Summary}\n\n" +
                "导入会覆盖这三套主页现有的组件位置（配色和字体不受影响）。" +
                (bundle.UnknownCount > 0
                    ? $"\n其中 {bundle.UnknownCount} 个组件当前版本没有，会先记下但不会显示。"
                    : ""),
                "导入");
            if (!ok) return;

            ApplyBundle(bundle, "导入");
            Log.Info($"[布局] 已从 {path} 导入：{bundle.Summary}");
        }
        catch (Exception ex)
        {
            Log.Error("导入主页布局失败：" + (path ?? ""), ex);
            _ioStatus.Text = "导入失败：" + ex.Message;
        }
    }

    private void RestoreFromBackup()
    {
        try
        {
            var entries = LayoutTools.ListBackups();
            if (entries.Count == 0)
            {
                _ioStatus.Text = "备份文件夹里还没有文件，先点「备份」存一份。";
                return;
            }

            string? path = RestoreDialog.Ask(this, entries);
            if (path == null) return;

            var bundle = LayoutTools.LoadBundle(path);
            bool ok = ConfirmDialog.Ask(this, "从备份恢复",
                $"{System.IO.Path.GetFileName(path)}\n{bundle.Summary}\n\n" +
                "会把三套主页的组件位置还原成这份备份里的样子。配色和字体不动。",
                "恢复");
            if (!ok) return;

            ApplyBundle(bundle, "恢复");
            Log.Info($"[布局] 已从备份恢复 {path}：{bundle.Summary}");
        }
        catch (Exception ex)
        {
            Log.Error("从备份恢复失败", ex);
            _ioStatus.Text = "恢复失败：" + ex.Message;
        }
    }

    private void ResetHomeLayout()
    {
        string style = Cfg.CurrentStyle;
        string name = LayoutTools.StyleName(style);
        int count = Cfg.SnapshotLayout(style).Count;

        bool ok = ConfirmDialog.Ask(this, "重置主页布局",
            $"把「{name}」主页的 {count} 个组件恢复成第一次安装时的默认排列。\n" +
            "其它主页不受影响，这个操作不能撤销（建议先点「备份」）。",
            "重置", danger: true);
        if (!ok) return;

        try
        {
            var canvas = HomeInjector.Active;
            if (canvas == null)
            {
                _ioStatus.Text = "主页还没加载，先回主页再试一次。";
                return;
            }

            canvas.ResetLayout();
            _ioStatus.Text = $"已把「{name}」恢复成默认排列。";
            Log.Info("[布局] 用户重置了 " + style);
        }
        catch (Exception ex)
        {
            Log.Error("重置主页布局失败", ex);
            _ioStatus.Text = "重置失败，详情见插件日志。";
        }
    }

    private void ExportLayout()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "导出主页布局",
                Filter = "文本文件 (*.txt)|*.txt",
                DefaultExt = ".txt",
                AddExtension = true,
                FileName = $"高级主页布局-{DateTime.Now:yyyyMMdd-HHmm}.txt"
            };

            if (dialog.ShowDialog(this) != true) return;

            LayoutTools.WriteText(dialog.FileName, LayoutTools.ExportText());
            _ioStatus.Text = "已导出：" + dialog.FileName;
            Log.Info("[布局] 已导出到 " + dialog.FileName);
        }
        catch (Exception ex)
        {
            Log.Error("导出主页布局失败", ex);
            _ioStatus.Text = "导出失败：" + ex.Message;
        }
    }

    private void BackupLayout()
    {
        try
        {
            string path = LayoutTools.Backup();
            _ioStatus.Text = "已备份：" + path;
            Log.Info("[布局] 已备份到 " + path);
        }
        catch (Exception ex)
        {
            Log.Error("备份主页布局失败", ex);
            _ioStatus.Text = "备份失败：" + ex.Message;
        }
    }

    // ───────────────────── 测试 / 组件库 ─────────────────────

    private FrameworkElement BuildTestCard()
    {
        var open = M3Button.Create("打开组件库", () => ComponentGalleryDialog.Show(this),
            M3ButtonKind.Filled, iconData: IconGrid, scheme: UiSkin.L);

        var rows = new List<FrameworkElement>
        {
            Row("组件库", "卡片、按钮、弹窗与界面组件的实例；弹窗由按钮触发", open)
        };

        return Card("测试", rows, "组件库只是展示，不会改动你的主页；想试哪个控件就在里面点。");
    }

    // ───────────────────── 新手教程 ─────────────────────

    private TextBlock _tutorialHint = null!;

    private FrameworkElement BuildTutorialCard()
    {
        _tutorialHint = new TextBlock
        {
            Text = TutorialStateText(),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = UiSkin.Dim,
            TextWrapping = TextWrapping.Wrap
        };

        var restart = M3Button.Create("重新开始", RestartTutorial, M3ButtonKind.Tonal,
            scale: 0.9, scheme: UiSkin.L);

        var stop = M3Button.Create("结束教程", EndTutorial, M3ButtonKind.Text,
            scale: 0.9, danger: true, scheme: UiSkin.L);
        stop.Margin = new Thickness(8, 0, 0, 0);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(restart);
        actions.Children.Add(stop);

        return Card("新手教程", new FrameworkElement[]
        {
            Row("教程进度", "第一次装好插件会自动弹一次；随时可以重新走，也可以提前结束", _tutorialHint),
            Row("操作", "重新开始会先回到主页，从欢迎弹窗开始", actions)
        });
    }

    private static string TutorialStateText()
    {
        if (Onboarding.Running) return "正在进行：跟着主页上的气泡做一遍就好。";
        return Cfg.Ui.TutorialSeen
            ? "已看过。想再看一遍点「重新开始」。"
            : "还没看过：下次打开主页会自动弹出。";
    }

    private void RestartTutorial()
    {
        Cfg.Ui.TutorialSeen = false;
        Cfg.Save();

        var canvas = HomeRefresh.Current;
        Close();

        if (canvas == null)
        {
            Log.Warn("[教程] 找不到主页画布，重新开始失败");
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Onboarding.Start(canvas, true);
            }
            catch (Exception ex)
            {
                Log.Error("重新开始新手教程失败", ex);
            }
        }), DispatcherPriority.ApplicationIdle);
    }

    private void EndTutorial()
    {
        if (Onboarding.Running)
        {
            Onboarding.Stop(true);
        }
        else
        {
            Cfg.Ui.TutorialSeen = true;
            Cfg.Save();
        }

        if (_tutorialHint != null) _tutorialHint.Text = TutorialStateText();
        Log.Info("[教程] 已标记为结束");
    }

    private void ToggleFontPopup()
    {
        if (_fontPopup is { IsOpen: true })
        {
            _fontPopup.IsOpen = false;
            return;
        }

        _fontPopup ??= BuildFontPopup();

        if (_fonts == null)
        {
            try
            {
                _fonts = Fonts.SystemFontFamilies
                    .Select(f => f.Source)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.CurrentCulture)
                    .ToArray();
            }
            catch
            {
                _fonts = Array.Empty<string>();
            }

            var list = (StackPanel)((ScrollViewer)((Border)_fontPopup.Child).Child).Content;
            list.Children.Clear();
            list.Children.Add(FontItem("跟随启动器", ""));
            foreach (var name in _fonts) list.Children.Add(FontItem(name, name));
        }

        _fontPopup.PlacementTarget = _fontButton;
        _fontPopup.IsOpen = true;
    }

    private Popup BuildFontPopup()
    {
        var list = new StackPanel();

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            MaxHeight = 300,
            Content = list
        };

        var surface = new Border
        {
            Width = 280,
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = UiSkin.Card,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.Line,
            Effect = M3.Elevation(3),
            Padding = new Thickness(6),
            Child = scroller
        };

        return new Popup
        {
            Child = surface,
            StaysOpen = false,
            AllowsTransparency = true,
            Placement = PlacementMode.Bottom,
            VerticalOffset = 4,
            PopupAnimation = PopupAnimation.Fade
        };
    }

    private FrameworkElement FontItem(string label, string family)
    {
        bool selected = string.Equals(Cfg.Ui.FontFamily ?? "", family, StringComparison.OrdinalIgnoreCase);

        var text = new TextBlock
        {
            Text = label,
            FontSize = 14,
            Foreground = selected ? UiSkin.L.Primary : UiSkin.Ink,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        if (!string.IsNullOrEmpty(family))
        {
            try
            {
                text.FontFamily = new FontFamily(family);
            }
            catch
            {
                // 个别字体族名不能直接构造，保持默认字体显示
            }
        }

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(text);

        if (selected)
        {
            var check = M3.Icon(IconCheck, UiSkin.L.Primary, 18);
            Grid.SetColumn(check, 1);
            row.Children.Add(check);
        }

        var item = new Border
        {
            Height = 38,
            CornerRadius = new CornerRadius(M3.CornerS),
            Padding = new Thickness(10, 0, 10, 0),
            Background = Brushes.Transparent,
            Child = row,
            Cursor = Cursors.Hand
        };

        item.MouseEnter += (_, _) => item.Background = UiSkin.Hover(UiSkin.Card);
        item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
        item.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Cfg.Ui.FontFamily = family;
            Cfg.Save();
            _fontLabel.Text = string.IsNullOrEmpty(family) ? "跟随启动器" : family;
            _fontPopup!.IsOpen = false;
            HomeInjector.RequestHomeRefresh();
        };

        return item;
    }

    // ─────────────────────────── 外观 ───────────────────────────

    private FrameworkElement BuildAppearanceBody()
    {
        var stack = new StackPanel();
        _swatches.Clear();

        var grid = new UniformGrid { Columns = 5, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var preset in AppTheme.All)
        {
            grid.Children.Add(Swatch(preset.Key, preset.Name, preset.Hint));
        }
        grid.Children.Add(Swatch("custom", "自定义", "用取色器调一个种子色"));

        stack.Children.Add(Card("配色方案", new FrameworkElement[]
        {
            grid,
            BuildOpacityRow()
        }, "默认是官方 M3 蓝；「莫奈」从壁纸取样，「自定义」用取色器调。换完立刻生效。"));

        RefreshSwatches();
        stack.Children.Add(Card("预览", new[] { BuildPreview() }));

        stack.Children.Add(new TextBlock
        {
            Text = CurrentSchemeText(),
            FontSize = 12,
            Margin = new Thickness(4, 12, 0, 0),
            Foreground = UiSkin.Dim
        });

        return stack;
    }

    /// <summary>全局透明度滑块：拖动时只改底色，松手 400ms 后才落盘。</summary>
    private FrameworkElement BuildOpacityRow()
    {
        double initial = Math.Clamp(Cfg.Ui.CardOpacity, 0, 1);

        var value = new TextBlock
        {
            Text = $"{initial * 100:0}%",
            FontSize = 12,
            Width = 40,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = UiSkin.Dim,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var slider = new M3Slider(initial * 100, 0, 100, scheme: UiSkin.L)
        {
            Width = 200,
            VerticalAlignment = VerticalAlignment.Center
        };

        slider.Changed += raw =>
        {
            double next = Math.Round(raw / 5.0) * 5 / 100.0;
            Cfg.Ui.CardOpacity = next;
            value.Text = $"{next * 100:0}%";
            HomeInjector.Active?.RefreshCardOpacity();
            QueueSave();
        };

        var host = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        host.Children.Add(slider);
        host.Children.Add(value);

        return Row("全部组件透明度", "统一压淡所有卡片的底色，文字始终清晰", host);
    }

    private void QueueSave()
    {
        _saveTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background,
            (_, _) =>
            {
                _saveTimer?.Stop();
                Cfg.SaveSilently();
            }, Dispatcher);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private static string CurrentSchemeText() => AppTheme.CurrentKey switch
    {
        "orange" => "当前：橙",
        "green" => "当前：绿",
        "monet" => "当前：莫奈取色" + (string.IsNullOrWhiteSpace(Cfg.Ui.MonetSeed) ? "（还没取到）" : " " + Cfg.Ui.MonetSeed),
        "custom" => "当前：自定义 " + AppTheme.Hex(AppTheme.CustomSeed),
        _ => "当前：蓝"
    };

    private FrameworkElement Swatch(string key, string name, string hint)
    {
        var seed = key switch
        {
            "monet" => AppTheme.Parse(Cfg.Ui.MonetSeed) ?? Color.FromRgb(0x9C, 0x7A, 0xD8),
            "custom" => AppTheme.CustomSeed,
            _ => AppTheme.Parse(AppTheme.All.First(p => p.Key == key).Hex) ?? Colors.Gray
        };

        var glyph = key switch
        {
            "monet" => M3.Icon("M12,4 C8,4 5,7 5,11 C5,15 8,18 12,20 C16,18 19,15 19,11 C19,7 16,4 12,4 Z", Brushes.White, 20),
            "custom" => M3.Icon("M12,5 V19 M5,12 H19", Brushes.White, 18),
            _ => null
        };

        var circle = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = new SolidColorBrush(seed),
            Child = glyph
        };

        var label = new TextBlock
        {
            Text = name,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = UiSkin.Ink
        };

        var sub = new TextBlock
        {
            Text = hint,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = UiSkin.Dim
        };

        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(new Border { Child = circle, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(label);
        content.Children.Add(sub);

        var ring = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(14, 14, 14, 12),
            Margin = new Thickness(4),
            Cursor = Cursors.Hand,
            Child = content
        };

        var check = MakeIcon(IconCheck, UiSkin.L.Primary, 16);
        check.HorizontalAlignment = HorizontalAlignment.Right;
        check.VerticalAlignment = VerticalAlignment.Top;
        check.Margin = new Thickness(0, 2, 4, 0);
        check.Visibility = Visibility.Collapsed;

        var tile = new Grid();
        tile.Children.Add(ring);
        tile.Children.Add(check);

        ring.MouseEnter += (_, _) =>
        {
            if (key != AppTheme.CurrentKey) ring.Background = UiSkin.Hover(UiSkin.Card);
        };
        ring.MouseLeave += (_, _) =>
        {
            if (key != AppTheme.CurrentKey) ring.Background = Brushes.Transparent;
        };

        ring.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;

            if (key == "custom")
            {
                OpenColorPicker();
                return;
            }

            // 已经是莫奈了：再点一次 = 按当前壁纸重新取色（换了壁纸不用切走再切回来）
            if (key == "monet" && AppTheme.CurrentKey == "monet")
            {
                if (AppTheme.RepickMonet())
                {
                    Toast("已按当前壁纸重新取色 " + Cfg.Ui.MonetSeed);
                    Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
                }
                else
                {
                    Toast("没找到壁纸文件，先给启动器设一张背景图");
                }

                return;
            }

            if (key == AppTheme.CurrentKey) return;

            if (!AppTheme.Apply(key))
            {
                Toast("没找到壁纸文件，先给启动器设一张背景图");
                return;
            }

            Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
        };

        _swatches.Add((key, ring, check));
        return tile;
    }

    /// <summary>
    /// 配色预览：左边是「页面」，右边是一张真的卡片。
    /// 卡片底色、按钮、强调色、文字四层同时出现，换方案时一眼能看出层次对不对。
    /// </summary>
    private FrameworkElement BuildPreview()
    {
        var panel = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerL),
            Background = UiSkin.CardAlt,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.Line,
            Padding = new Thickness(16),
            Margin = new Thickness(4, 6, 4, 0)
        };

        var body = new StackPanel();

        // ── 迷你卡片（真实卡片色） ──
        var title = new TextBlock
        {
            Text = "高级主页",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.L.OnSurface
        };

        var sub = new TextBlock
        {
            Text = "卡片表面 · 主色按钮 · 强调色",
            FontSize = 11.5,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = UiSkin.L.OnSurfaceVariant
        };

        var button = new Border
        {
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = UiSkin.L.Primary,
            Padding = new Thickness(16, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "按钮",
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                Foreground = UiSkin.L.OnPrimary,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var chip = new Border
        {
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = UiSkin.L.TertiaryContainer,
            Padding = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = "强调",
                FontSize = 11.5,
                Foreground = UiSkin.L.OnTertiaryContainer,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(title);
        left.Children.Add(sub);

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(button);
        right.Children.Add(chip);

        var cardGrid = new Grid();
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cardGrid.Children.Add(left);
        Grid.SetColumn(right, 1);
        cardGrid.Children.Add(right);

        body.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = UiSkin.L.SurfaceLow,
            Padding = new Thickness(16, 14, 16, 14),
            Child = cardGrid
        });

        // ── 色板图例 ──
        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(2, 14, 0, 0)
        };
        legend.Children.Add(LegendDot("主色", UiSkin.L.Primary));
        legend.Children.Add(LegendDot("主色容器", UiSkin.L.PrimaryContainer));
        legend.Children.Add(LegendDot("卡片", UiSkin.L.SurfaceLow));
        legend.Children.Add(LegendDot("强调", UiSkin.L.Tertiary));

        body.Children.Add(legend);
        panel.Child = body;
        return panel;
    }

    private static FrameworkElement LegendDot(string label, Brush color)
    {
        var dot = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = color,
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.Line
        };

        var text = new TextBlock
        {
            Text = label,
            FontSize = 11.5,
            Margin = new Thickness(6, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = UiSkin.Dim
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(dot);
        row.Children.Add(text);
        return row;
    }

    private void OpenColorPicker()
    {
        var picked = ColorPickerDialog.Pick(AppTheme.CustomSeed, this);
        if (picked is not Color color) return;

        AppTheme.ApplyCustom(color);
        Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
    }

    private void RefreshSwatches()
    {
        string current = AppTheme.CurrentKey;
        foreach (var (key, ring, check) in _swatches)
        {
            bool on = key == current;
            ring.BorderBrush = on ? UiSkin.L.Primary : Brushes.Transparent;
            ring.Background = on ? M3.Layer(M3.Of(UiSkin.Card), M3.Of(UiSkin.L.Primary), 0.05) : Brushes.Transparent;
            check.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>换配色后整窗重建：M3 的画笔是在构造时取值的，不重建会停在旧色。</summary>
    /// <summary>窗口底部的一颗轻提示：只在换配色失败 / 取色成功这类瞬间用一下。</summary>
    private void Toast(string text)
    {
        if (_root == null) return;

        _toastTimer?.Stop();
        if (_toast != null)
        {
            _root.Children.Remove(_toast);
            _toast = null;
        }

        var card = new Border
        {
            Background = UiSkin.L.InverseSurface,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 10, 16, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 28),
            IsHitTestVisible = false,
            Opacity = 0,
            Effect = M3.Elevation(2),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Foreground = UiSkin.L.InverseOnSurface
            }
        };

        Grid.SetColumnSpan(card, 2);
        Panel.SetZIndex(card, 100);
        _root.Children.Add(card);
        _toast = card;

        card.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        _toastTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(2400), DispatcherPriority.Background,
            (_, _) =>
            {
                _toastTimer?.Stop();
                if (_toast != card) return;

                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
                fade.Completed += (_, _) =>
                {
                    if (_toast == card) _toast = null;
                    _root.Children.Remove(card);
                };
                card.BeginAnimation(OpacityProperty, fade);
            }, Dispatcher);
        _toastTimer.Start();
    }

    private void Rebuild()
    {
        double offset = _scroll?.VerticalOffset ?? 0;
        string query = _searchBox?.Text ?? "";

        if (_fontPopup is { IsOpen: true }) _fontPopup.IsOpen = false;

        BuildShell();
        MountShell();

        if (query.Length > 0)
        {
            _searchBox.Text = query;
            _searchBox.CaretIndex = query.Length;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _scroll.ScrollToVerticalOffset(offset);
            if (query.Length == 0) SetActive(_active);
        }), DispatcherPriority.Loaded);
    }

    // ─────────────────────────── 关于 ───────────────────────────

    private FrameworkElement BuildAboutBody()
    {
        var stack = new StackPanel();

        string version = typeof(UiSettingsDialog).Assembly.GetName().Version is { } v
            ? $"v{v.Major}.{v.Minor}.{v.Build}"
            : "v?";

        var versionText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        versionText.Children.Add(new TextBlock
        {
            Text = "高级主页 " + version,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = UiSkin.Ink
        });
        versionText.Children.Add(new TextBlock
        {
            Text = "PCL Nex 插件 · MIT 开源",
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = UiSkin.Dim
        });

        var linkText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        linkText.Children.Add(new TextBlock
        {
            Text = "github.com/Xinluo1145qwq/PCL-Nex_AdvancedHome",
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = UiSkin.L.Primary
        });
        linkText.Children.Add(new TextBlock
        {
            Text = "源码、更新日志和问题反馈都在这个仓库",
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = UiSkin.Dim
        });

        var linkButton = new Border
        {
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(M3.CornerS),
            Cursor = Cursors.Hand,
            Child = linkText,
            Background = Brushes.Transparent
        };
        linkButton.MouseEnter += (_, _) => linkButton.Background = UiSkin.Hover(UiSkin.Card);
        linkButton.MouseLeave += (_, _) => linkButton.Background = Brushes.Transparent;
        linkButton.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenUrl(RepoUrl);
        };

        stack.Children.Add(Card("关于", new[]
        {
            SimpleRow(versionText),
            SimpleRow(linkButton)
        }));

        stack.Children.Add(BuildAuthorCard());

        var open = M3Button.Create("打开数据文件夹", OpenDataFolder, M3ButtonKind.Outlined, scheme: UiSkin.L);
        var reset = M3Button.Create("重置启动记录", ResetStats, M3ButtonKind.Text, danger: true, scheme: UiSkin.L);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(open);
        reset.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(reset);

        stack.Children.Add(Card("数据与维护", new[]
        {
            Row("数据文件", Paths.ConfigFile, actions)
        }, "配置和背景图都放在这个目录里。重置启动记录只清空打卡统计，不动布局。"));

        return stack;
    }

    /// <summary>
    /// 关于页底部的作者卡片：背景图 + 压在交界处的头像 + 名字 / 身份 + 四个平台入口。
    /// 图片全部内嵌在 DLL 里，换机器、换主题都不会丢图。
    /// </summary>
    private FrameworkElement BuildAuthorCard()
    {
        var wrap = new StackPanel { Margin = new Thickness(0, 28, 0, 0) };

        wrap.Children.Add(new TextBlock
        {
            Text = "作者",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.L.Primary,
            Margin = new Thickness(18, 0, 0, 8)
        });

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(168) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 背景图：铺满上半部分，底边压一层渐变把头像和文字托起来
        var banner = new Grid();
        if (ImageUtil.Embedded("PCLN.AdvancedHomepage.Assets.Author.banner.jpg", 1200) is { } bannerSource)
        {
            var image = new Image
            {
                Source = bannerSource,
                Stretch = Stretch.UniformToFill,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            banner.Children.Add(image);
        }
        else
        {
            banner.Background = UiSkin.L.PrimaryContainer;
        }

        banner.Children.Add(new Border
        {
            Background = new LinearGradientBrush(
                Color.FromArgb(0, 0, 0, 0), Color.FromArgb(64, 0, 0, 0), 90)
        });
        root.Children.Add(banner);

        // 头像：骑在背景图与文字区的交线上
        var avatar = new Border
        {
            Width = 84,
            Height = 84,
            CornerRadius = new CornerRadius(26),
            BorderThickness = new Thickness(4),
            BorderBrush = UiSkin.Card,
            Background = UiSkin.CardAlt,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(24, -42, 0, 0),
            Child = ImageUtil.Embedded("PCLN.AdvancedHomepage.Assets.Author.avatar.jpg", 256) is { } face
                ? new Image { Source = face, Stretch = Stretch.UniformToFill }
                : null
        };
        avatar.SizeChanged += (_, _) =>
        {
            if (avatar.ActualWidth <= 0) return;
            avatar.Clip = new RectangleGeometry(
                new Rect(0, 0, avatar.ActualWidth, avatar.ActualHeight), 22, 22);
        };
        Grid.SetRow(avatar, 1);
        root.Children.Add(avatar);

        // 名字 + 身份
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        name.Children.Add(new TextBlock
        {
            Text = "星络",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink
        });
        name.Children.Add(new TextBlock
        {
            Text = "高级主页作者",
            FontSize = 12.5,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = UiSkin.Dim
        });

        var socials = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };

        socials.Children.Add(SocialButton("github.png", "Github", AuthorUrl));
        socials.Children.Add(SocialButton("douyin.png", "抖音", "https://v.douyin.com/RWM3vXwiAAE/"));
        socials.Children.Add(SocialButton("bilibili.png", "BiliBili", "https://space.bilibili.com/3546612299204684"));
        socials.Children.Add(SocialButton("xiaohongshu.png", "小红书",
            "https://www.xiaohongshu.com/user/profile/66f4e4b0000000001d021f15?xsec_token=YBjqFLK-lgsXr-YJtBN8ovn9lafF5vjgVOBGd3SNS_SKs=&xsec_source=app_share&xhsshare=CopyLink&shareRedId=ODxINEg5Rjk2NzUyOTgwNjdHOTk1Szc-&apptime=1794111178&share_id=a51c0c85e0cb4e5b9c94f562d5e2779b"));

        var footer = new Grid { Margin = new Thickness(24, 52, 24, 22) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(name);
        Grid.SetColumn(socials, 1);
        footer.Children.Add(socials);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);

        var card = new Border
        {
            CornerRadius = new CornerRadius(20),
            Background = UiSkin.Card,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.Line,
            Child = root
        };
        card.SizeChanged += (_, _) =>
        {
            if (card.ActualWidth <= 0 || card.ActualHeight <= 0) return;
            card.Clip = new RectangleGeometry(
                new Rect(0, 0, card.ActualWidth, card.ActualHeight), 20, 20);
        };

        wrap.Children.Add(card);
        return wrap;
    }

    /// <summary>一颗圆形平台入口：图标直接铺满圆形，悬停换成 primary container 底。</summary>
    private FrameworkElement SocialButton(string iconFile, string label, string url)
    {
        const double size = 40;

        var image = new Image
        {
            Stretch = Stretch.UniformToFill,
            SnapsToDevicePixels = true
        };

        if (ImageUtil.Embedded("PCLN.AdvancedHomepage.Assets.Author." + iconFile, 128) is { } source)
        {
            image.Source = source;
            image.Clip = new EllipseGeometry(new Point(size / 2, size / 2), size / 2, size / 2);
        }

        var circle = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = UiSkin.CardAlt,
            Child = image,
            Cursor = Cursors.Hand,
            ToolTip = UiStyles.Tooltip(label)
        };

        circle.MouseEnter += (_, _) => circle.Background = UiSkin.L.PrimaryContainer;
        circle.MouseLeave += (_, _) => circle.Background = UiSkin.CardAlt;
        circle.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenUrl(url);
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(0, 0, 14, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.Children.Add(circle);
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10.5,
            Foreground = UiSkin.Dim,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        });

        return stack;
    }

    private void OpenUrl(string url)
    {
        if (!_ready)
        {
            Log.Warn("[链接] 窗口刚打开，忽略这次跳转：" + url);
            return;
        }

        try
        {
            Log.Info("[链接] 打开 " + url);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn("打开链接失败：" + ex.Message);
        }
    }

    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Paths.DataDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn("打开数据文件夹失败：" + ex.Message);
        }
    }

    private void ResetStats()
    {
        try
        {
            Cfg.ResetHistory();
            Log.Info("已重置启动记录");
        }
        catch (Exception ex)
        {
            Log.Error("重置启动记录失败", ex);
        }
    }

    // ─────────────────────────── 通用积木 ───────────────────────────

    private static Path MakeIcon(string data, Brush color, double size = 24, bool filled = false)
    {
        try
        {
            return M3.Icon(data, color, size, filled);
        }
        catch (Exception ex)
        {
            Log.Warn("图标路径解析失败：" + ex.Message);
            return M3.Icon("M5,12 H19", color, size);
        }
    }

    /// <summary>
    /// 一张设置卡片：小标题 + 若干行（行间 1px 分隔）+ 可选说明。
    /// 顺手把每行的文字记进搜索索引——搜索框靠它决定这一行留不留。
    /// </summary>
    private Border Card(string title, IReadOnlyList<FrameworkElement> rows, string? note = null)
    {
        var body = new StackPanel();

        body.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.L.Primary,
            Margin = new Thickness(18, 14, 18, 4)
        });

        var record = new SearchCard { Title = title, Section = _current?.Title ?? "" };
        _current?.Cards.Add(record);

        for (int i = 0; i < rows.Count; i++)
        {
            Border? divider = null;
            if (i > 0)
            {
                divider = new Border
                {
                    Height = 1,
                    Background = UiSkin.Line,
                    Margin = new Thickness(18, 0, 18, 0)
                };
                body.Children.Add(divider);
            }

            body.Children.Add(rows[i]);
            record.Rows.Add(new SearchRow
            {
                Root = rows[i],
                Divider = divider,
                Text = title + " " + CollectText(rows[i])
            });
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            body.Children.Add(new TextBlock
            {
                Text = note,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(18, 8, 18, 14),
                Foreground = UiSkin.Dim
            });
        }
        else
        {
            body.Children.Add(new Border { Height = 12 });
        }

        var border = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerL),
            Background = UiSkin.Card,
            BorderThickness = new Thickness(1),
            BorderBrush = UiSkin.Line,
            Margin = new Thickness(0, 0, 0, 14),
            Child = body
        };

        record.Root = border;
        return border;
    }

    /// <summary>把一行里所有 TextBlock 的文字拼起来（按钮文字、说明都算），供搜索匹配。</summary>
    private static string CollectText(DependencyObject node)
    {
        var sb = new StringBuilder();
        Walk(node);
        return sb.ToString();

        void Walk(DependencyObject current)
        {
            switch (current)
            {
                case TextBlock text:
                    sb.Append(text.Text).Append(' ');
                    break;
                case Panel panel:
                    foreach (UIElement child in panel.Children) Walk(child);
                    break;
                case Border border when border.Child != null:
                    Walk(border.Child);
                    break;
                case ContentControl content when content.Content is DependencyObject inner:
                    Walk(inner);
                    break;
                case Decorator decorator when decorator.Child != null:
                    Walk(decorator.Child);
                    break;
            }
        }
    }

    private static FrameworkElement Row(string label, string desc, FrameworkElement control,
        bool showText = true, FrameworkElement? left = null)
    {
        var grid = new Grid { MinHeight = 56, Margin = new Thickness(18, 4, 14, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        FrameworkElement content = left ?? BuildText(label, desc);
        grid.Children.Add(content);

        control.VerticalAlignment = VerticalAlignment.Center;
        control.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private static FrameworkElement SimpleRow(FrameworkElement content)
    {
        content.Margin = new Thickness(18, 10, 18, 10);
        content.HorizontalAlignment = HorizontalAlignment.Left;
        return content;
    }

    private static FrameworkElement BuildText(string label, string desc)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = UiSkin.Ink
        });

        if (!string.IsNullOrWhiteSpace(desc))
        {
            text.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = UiSkin.Dim
            });
        }

        return text;
    }

    // ─────────────────────────── 导航项 ───────────────────────────

    /// <summary>一个 MD3 导航抽屉条目：52dp 高、全圆角药丸底、左图标右文字。</summary>
    /// <summary>
    /// 侧栏条目，版式照抄组件库里的「列表项」：38dp 圆形图标容器 + 标题 + 说明。
    /// 选中时整行铺 primary container，图标容器翻成实心 primary——比原来的药丸描边更像一枚可点的设置项。
    /// </summary>
    private sealed class NavItem : Grid
    {
        private readonly Border _row;
        private readonly Border _badge;
        private readonly Path _icon;
        private readonly TextBlock _label;
        private readonly TextBlock _hint;
        private readonly bool _filled;
        private bool _active;
        private bool _hovering;

        /// <summary>窗口刚弹出来的几百毫秒里关掉点击，避免吃掉上一个窗口的 MouseUp。</summary>
        public bool Enabled = true;

        public NavItem(string iconData, string label, string hint, Action onClick, bool filled = false)
        {
            Height = 60;
            Margin = new Thickness(10, 0, 10, 4);
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;

            _filled = filled;

            _row = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = Brushes.Transparent
            };
            Children.Add(_row);

            _icon = MakeIcon(iconData, UiSkin.Dim, 20, filled);
            _badge = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(19),
                Background = UiSkin.CardAlt,
                Child = _icon,
                VerticalAlignment = VerticalAlignment.Center
            };

            _label = new TextBlock
            {
                Text = label,
                FontSize = 13.5,
                FontWeight = FontWeights.Medium,
                Foreground = UiSkin.Ink
            };

            _hint = new TextBlock
            {
                Text = hint,
                FontSize = 11.5,
                Foreground = UiSkin.Dim,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var text = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 10, 0)
            };
            text.Children.Add(_label);
            text.Children.Add(_hint);

            var row = new Grid { Margin = new Thickness(10, 0, 8, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(_badge);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            Children.Add(row);

            MouseEnter += (_, _) =>
            {
                _hovering = true;
                Paint(false);
            };
            MouseLeave += (_, _) =>
            {
                _hovering = false;
                Paint(false);
            };
            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (!Enabled) return;
                onClick();
            };
        }

        public void SetActive(bool value)
        {
            if (_active == value) return;
            _active = value;
            Paint(true);
        }

        private void Paint(bool animate)
        {
            var scheme = UiSkin.L;

            Brush fill = _active
                ? scheme.PrimaryContainer
                : (_hovering ? UiSkin.CardAlt : Brushes.Transparent);

            if (animate) M3.AnimateFill(_row, fill);
            else _row.Background = fill;

            _badge.Background = _active ? scheme.Primary : UiSkin.CardAlt;

            var iconBrush = _active ? scheme.OnPrimary : UiSkin.Dim;
            if (_filled) _icon.Fill = iconBrush;
            else _icon.Stroke = iconBrush;

            _label.Foreground = _active ? scheme.OnPrimaryContainer : UiSkin.Ink;
            _hint.Foreground = _active ? scheme.OnPrimaryContainer : UiSkin.Dim;
            _hint.Opacity = _active ? 0.78 : 1;
        }
    }
}

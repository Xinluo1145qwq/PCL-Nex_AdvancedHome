using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 主页顶部工具栏（M3 Expressive）：
/// 左「花样 / 极简 / 便签」分段按钮，右编辑图标按钮；编辑态再浮出「添加」与「删除布局」。
/// </summary>
internal sealed class HomeTopBar : StackPanel
{
    public event Action<string>? StyleChanged;   // fancy / minimal / notes
    public event Action? EditClicked;
    public event Action? AddClicked;
    public event Action? DeleteLayoutClicked;
    public event Action? SettingsClicked;

    private const string IconPencil = "M4,16.5 L4,20 L7.5,20 L18.2,9.3 L14.7,5.8 Z M16.1,4.4 L19.6,7.9";
    private const string IconCheck = "M5,12.5 L10,17.5 L19,6.5";
    private const string IconPlus = "M12,5 V19 M5,12 H19";
    private const string IconTrash = "M5,7 H19 M9.5,7 V4.5 H14.5 V7 M7,7 L8,19.5 H16 L17,7 M10.5,10.5 V16.5 M13.5,10.5 V16.5";

    private const string IconGear =
        "M12,6.8 A5.2,5.2 0 1 1 11.99,6.8 Z M12,10.5 A1.5,1.5 0 1 1 11.99,10.5 Z " +
        "M12,2.4 V4.4 M12,19.6 V21.6 M2.4,12 H4.4 M19.6,12 H21.6 " +
        "M5.3,5.3 L6.7,6.7 M17.3,17.3 L18.7,18.7 M18.7,5.3 L17.3,6.7 M6.7,17.3 L5.3,18.7";

    private readonly M3Segmented? _segments;
    private readonly Border _editButton;
    private readonly FrameworkElement _pencilIcon;
    private readonly FrameworkElement _checkIcon;
    private readonly Border _addButton;
    private readonly Border _deleteButton;
    private readonly StackPanel _host;

    private string _style = "fancy";
    private bool _edit;

    private static readonly string[] AllKeys = { "fancy", "minimal", "notes" };
    private static readonly string[] AllLabels = { "花样", "极简", "便签" };

    // 设置里可能把某几个主页藏起来了，顶部菜单只留启用的那几个
    private readonly string[] _visibleKeys;
    private readonly string[] _visibleLabels;

    public HomeTopBar(string style)
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Top;
        HorizontalAlignment = HorizontalAlignment.Left;

        _style = Cfg.NormalizeStyle(style);

        var keys = new List<string>();
        var labels = new List<string>();
        for (int i = 0; i < AllKeys.Length; i++)
        {
            if (!IsVisibleStyle(AllKeys[i])) continue;
            keys.Add(AllKeys[i]);
            labels.Add(AllLabels[i]);
        }

        // 花儿是兜底：全藏了也要有一个能用的主页
        if (keys.Count == 0)
        {
            keys.Add("fancy");
            labels.Add("花样");
        }

        _visibleKeys = keys.ToArray();
        _visibleLabels = labels.ToArray();
        if (!_visibleKeys.Contains(_style)) _style = _visibleKeys[0];

        _host = new StackPanel { Orientation = Orientation.Horizontal };
        Children.Add(_host);

        int selected = Math.Max(0, Array.IndexOf(_visibleKeys, _style));

        // 只剩一个主页时，切换按钮没有意义，整块收起来
        if (_visibleKeys.Length > 1)
        {
            _segments = new M3Segmented(_visibleLabels, selected, index =>
            {
                string next = _visibleKeys[Math.Clamp(index, 0, _visibleKeys.Length - 1)];
                if (next == _style) return;
                _style = next;
                StyleChanged?.Invoke(next);
            });
            _segments.Margin = new Thickness(0, 0, 8, 0);
            _segments.VerticalAlignment = VerticalAlignment.Center;
            _segments.HorizontalAlignment = HorizontalAlignment.Left;
            _host.Children.Add(_segments);
        }

        // 设置：编辑按钮左边的齿轮入口，进「高级主页设置」
        var gear = M3.Icon(IconGear, M3.C.OnSecondaryContainer, 20);
        var settingsButton = CircleButton(gear, M3.C.SecondaryContainer, M3.C.OnSecondaryContainer,
            "高级主页设置", () => SettingsClicked?.Invoke());
        settingsButton.Margin = new Thickness(0, 0, 8, 0);
        _host.Children.Add(settingsButton);

        // 编辑：filled 图标按钮，编辑态换成对勾
        _pencilIcon = M3.Icon(IconPencil, M3.C.OnPrimary, 20);
        _checkIcon = M3.Icon(IconCheck, M3.C.OnPrimary, 20);
        _checkIcon.Visibility = Visibility.Collapsed;

        var iconHost = new Grid();
        iconHost.Children.Add(_pencilIcon);
        iconHost.Children.Add(_checkIcon);

        _editButton = CircleButton(iconHost, M3.C.Primary, M3.C.OnPrimary, "编辑布局", () => EditClicked?.Invoke());
        _editButton.Visibility = Cfg.Ui.ShowEditButton ? Visibility.Visible : Visibility.Collapsed;
        _host.Children.Add(_editButton);

        // 添加：tonal 图标按钮（编辑态才出现）
        var plus = M3.Icon(IconPlus, M3.C.OnSecondaryContainer, 20);
        _addButton = CircleButton(plus, M3.C.SecondaryContainer, M3.C.OnSecondaryContainer, "添加小组件",
            () => AddClicked?.Invoke());
        _addButton.Margin = new Thickness(8, 0, 0, 0);
        _addButton.Visibility = Visibility.Collapsed;
        _host.Children.Add(_addButton);

        // 删除布局：error 文字按钮
        var trash = new Viewbox
        {
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 8, 0),
            Child = M3.Icon(IconTrash, M3.C.Error, 24),
            VerticalAlignment = VerticalAlignment.Center
        };
        var label = M3.Text("删除布局", M3.TypeRole.LabelLarge, M3.C.Error, 1, true);
        label.VerticalAlignment = VerticalAlignment.Center;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(trash);
        row.Children.Add(label);

        _deleteButton = new Border
        {
            Height = M3.ButtonHeight,
            CornerRadius = new CornerRadius(M3.ButtonHeight / 2),
            Padding = new Thickness(16, 0, 18, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = M3.C.OutlineVariant,
            Child = row,
            Cursor = Cursors.Hand,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = UiStyles.Tooltip("清空当前布局里的所有小组件")
        };

        var hover = M3.Layer(M3.Of(M3.C.Surface), M3.Of(M3.C.Error), M3.HoverAlpha);
        _deleteButton.MouseEnter += (_, _) => _deleteButton.Background = hover;
        _deleteButton.MouseLeave += (_, _) => _deleteButton.Background = Brushes.Transparent;
        _deleteButton.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            DeleteLayoutClicked?.Invoke();
        };

        _host.Children.Add(_deleteButton);
    }

    public string Style => _style;

    /// <summary>设置里这个主页有没有被藏掉（花样永远可用）。</summary>
    private static bool IsVisibleStyle(string key) => key switch
    {
        "minimal" => Cfg.Ui.ShowMinimal,
        "notes" => Cfg.Ui.ShowNotes,
        _ => true
    };

    public void SetStyle(string style, bool animate)
    {
        _style = Cfg.NormalizeStyle(style);

        int index = Array.IndexOf(_visibleKeys, _style);
        if (index < 0) index = 0;
        _segments?.SetSelected(index, false);
    }

    public void SetEdit(bool on)
    {
        if (_edit == on) return;
        _edit = on;

        _pencilIcon.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        _checkIcon.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        _editButton.ToolTip = UiStyles.Tooltip(on ? "完成" : "编辑布局");

        // 设置里关掉了编辑按钮：平时不显示，但编辑态必须留一个出口，否则退不出来
        _editButton.Visibility = on || Cfg.Ui.ShowEditButton ? Visibility.Visible : Visibility.Collapsed;

        _addButton.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        _deleteButton.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

        if (!on) return;

        // 新出现的按钮：缩放 + 淡入，走 Expressive 空间弹簧
        foreach (var element in new FrameworkElement[] { _addButton, _deleteButton })
        {
            element.Opacity = 0;
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = new ScaleTransform(0.85, 0.85);
            element.RenderTransform = scale;

            element.BeginAnimation(OpacityProperty, new DoubleAnimation(1, M3.FastEffects)
            {
                BeginTime = TimeSpan.FromMilliseconds(30),
                EasingFunction = M3.Effects
            });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, M3.FastSpatial)
            {
                BeginTime = TimeSpan.FromMilliseconds(30),
                EasingFunction = M3.Spatial
            });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, M3.FastSpatial)
            {
                BeginTime = TimeSpan.FromMilliseconds(30),
                EasingFunction = M3.Spatial
            });
        }
    }

    /// <summary>40dp 圆形按钮（M3 图标按钮 / FAB 小号共同的形状）。</summary>
    private static Border CircleButton(FrameworkElement content, Brush background, Brush foreground,
        string tooltip, Action onClick)
    {
        var button = new Border
        {
            Width = M3.IconButtonSize,
            Height = M3.IconButtonSize,
            CornerRadius = new CornerRadius(M3.IconButtonSize / 2),
            Background = background,
            Child = content,
            Cursor = Cursors.Hand,
            ToolTip = UiStyles.Tooltip(tooltip)
        };

        var hover = M3.Layer(M3.Of(background), M3.Of(foreground), M3.HoverAlpha);
        var press = M3.Layer(M3.Of(background), M3.Of(foreground), M3.PressAlpha);

        button.MouseEnter += (_, _) => button.Background = hover;
        button.MouseLeave += (_, _) => button.Background = background;
        button.PreviewMouseLeftButtonDown += (_, _) => button.Background = press;
        button.MouseLeftButtonUp += (_, e) =>
        {
            button.Background = background;
            e.Handled = true;
            onClick?.Invoke();
        };

        return button;
    }
}

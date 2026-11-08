using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>M3 Top App Bar（small）：64dp 高，左导航图标、中间标题、右侧动作。</summary>
internal sealed class M3TopAppBar : Border
{
    public M3TopAppBar(string title, string leadingIcon, Action onLeading,
        string trailingIcon, Action onTrailing, M3.Scheme scheme = null, double scale = 1)
    {
        scheme ??= M3.C;

        Height = 64 * scale;
        Background = scheme.Surface;
        CornerRadius = new CornerRadius(M3.CornerM);
        BorderThickness = new Thickness(1);
        BorderBrush = scheme.OutlineVariant;
        Padding = new Thickness(4 * scale, 0, 6 * scale, 0);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (!string.IsNullOrEmpty(leadingIcon))
        {
            var lead = M3IconButton.Create(leadingIcon, onLeading, "菜单", scale: 0.9, scheme: scheme);
            lead.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(lead);
        }

        var text = new TextBlock
        {
            Text = title,
            FontSize = 18 * scale,
            FontWeight = FontWeights.Medium,
            Foreground = scheme.OnSurface,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6 * scale, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (!string.IsNullOrEmpty(trailingIcon))
        {
            var trail = M3IconButton.Create(trailingIcon, onTrailing, "更多", scale: 0.9, scheme: scheme);
            trail.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(trail, 2);
            grid.Children.Add(trail);
        }

        Child = grid;
    }
}

/// <summary>
/// M3 Navigation Bar：80dp 高，每项是「64×32 药丸指示器 + 图标 + 12dp 标签」。
/// 选中项铺 secondary container，未选中是 onSurfaceVariant。
/// </summary>
internal sealed class M3NavigationBar : Border
{
    private sealed class NavItem
    {
        public Border Indicator = null!;
        public Path Icon = null!;
        public TextBlock Label = null!;
    }

    private readonly List<NavItem> _items = new();
    private readonly M3.Scheme _scheme;
    private int _selected;

    public event Action<int> Changed;

    public M3NavigationBar((string Icon, string Label)[] items, int selected, M3.Scheme scheme = null,
        double scale = 1)
    {
        _scheme = scheme ?? M3.C;
        _selected = Math.Clamp(selected, 0, Math.Max(0, items.Length - 1));

        Height = 80 * scale;
        Background = _scheme.SurfaceHigh;
        CornerRadius = new CornerRadius(M3.CornerL);
        Padding = new Thickness(4 * scale, 0, 4 * scale, 0);

        var grid = new UniformGrid { Columns = Math.Max(1, items.Length) };

        for (int i = 0; i < items.Length; i++)
        {
            int index = i;

            var icon = M3.Icon(items[i].Icon, _scheme.OnSurfaceVariant, 22);
            var indicator = new Border
            {
                Width = 64 * scale,
                Height = 32 * scale,
                CornerRadius = new CornerRadius(16 * scale),
                Background = Brushes.Transparent,
                Child = icon
            };

            var label = new TextBlock
            {
                Text = items[i].Label,
                FontSize = 12 * scale,
                FontWeight = FontWeights.Medium,
                Foreground = _scheme.OnSurfaceVariant,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4 * scale, 0, 0)
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(indicator);
            stack.Children.Add(label);

            var host = new Border
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = stack
            };
            host.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Select(index);
                Changed?.Invoke(index);
            };

            _items.Add(new NavItem { Indicator = indicator, Icon = icon, Label = label });
            grid.Children.Add(host);
        }

        Child = grid;
        Apply(false);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _items.Count || index == _selected) return;
        _selected = index;
        Apply(true);
    }

    private void Apply(bool animate)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            bool on = i == _selected;
            var item = _items[i];

            var bg = on ? _scheme.SecondaryContainer : Brushes.Transparent;
            if (animate) M3.AnimateFill(item.Indicator, bg);
            else item.Indicator.Background = bg;

            var ink = on ? _scheme.OnSecondaryContainer : _scheme.OnSurfaceVariant;
            item.Icon.Stroke = ink;
            item.Label.Foreground = on ? _scheme.OnSurface : _scheme.OnSurfaceVariant;
        }
    }
}

/// <summary>M3 Navigation Rail：80dp 宽的竖排导航，指示器 56×32。</summary>
internal sealed class M3NavigationRail : Border
{
    private sealed class NavItem
    {
        public Border Indicator = null!;
        public Path Icon = null!;
        public TextBlock Label = null!;
    }

    private readonly List<NavItem> _items = new();
    private readonly M3.Scheme _scheme;
    private int _selected;

    public event Action<int> Changed;

    public M3NavigationRail((string Icon, string Label)[] items, int selected, M3.Scheme scheme = null,
        double scale = 1)
    {
        _scheme = scheme ?? M3.C;
        _selected = Math.Clamp(selected, 0, Math.Max(0, items.Length - 1));

        Width = 80 * scale;
        Background = _scheme.SurfaceHigh;
        CornerRadius = new CornerRadius(M3.CornerL);
        Padding = new Thickness(0, 10 * scale, 0, 10 * scale);

        var stack = new StackPanel();

        for (int i = 0; i < items.Length; i++)
        {
            int index = i;

            var icon = M3.Icon(items[i].Icon, _scheme.OnSurfaceVariant, 22);
            var indicator = new Border
            {
                Width = 56 * scale,
                Height = 32 * scale,
                CornerRadius = new CornerRadius(16 * scale),
                Background = Brushes.Transparent,
                Child = icon
            };

            var label = new TextBlock
            {
                Text = items[i].Label,
                FontSize = 11.5 * scale,
                FontWeight = FontWeights.Medium,
                Foreground = _scheme.OnSurfaceVariant,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3 * scale, 0, 0)
            };

            var inner = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            inner.Children.Add(indicator);
            inner.Children.Add(label);

            var host = new Border
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 4 * scale, 0, 4 * scale),
                Child = inner
            };
            host.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Select(index);
                Changed?.Invoke(index);
            };

            _items.Add(new NavItem { Indicator = indicator, Icon = icon, Label = label });
            stack.Children.Add(host);
        }

        Child = stack;
        Apply(false);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _items.Count || index == _selected) return;
        _selected = index;
        Apply(true);
    }

    private void Apply(bool animate)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            bool on = i == _selected;
            var item = _items[i];

            var bg = on ? _scheme.SecondaryContainer : Brushes.Transparent;
            if (animate) M3.AnimateFill(item.Indicator, bg);
            else item.Indicator.Background = bg;

            var ink = on ? _scheme.OnSecondaryContainer : _scheme.OnSurfaceVariant;
            item.Icon.Stroke = ink;
            item.Label.Foreground = on ? _scheme.OnSurface : _scheme.OnSurfaceVariant;
        }
    }
}

/// <summary>
/// M3 下拉菜单：按钮 + Popup，菜单面是 8dp 圆角、elevation 2，单项 48dp。
/// Popup 是独立可视树，字体靠 Popup 自身的属性继承链带下去。
/// </summary>
internal static class M3Menu
{
    public static Border CreateButton(string label, (string Icon, string Text)[] items, Action<int> onPick,
        M3.Scheme scheme = null, double scale = 1)
    {
        scheme ??= M3.C;

        var list = new StackPanel();
        var popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            // 按钮在 MouseUp 才触发，配 StaysOpen=false 会被同一次抬起立刻关掉；
            // 这里改成常开，点菜单项 / 再点按钮 / 窗口失活时自己收。
            StaysOpen = true,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            VerticalOffset = 6 * scale
        };

        for (int i = 0; i < items.Length; i++)
        {
            int index = i;

            var row = new Grid { Margin = new Thickness(14 * scale, 0, 18 * scale, 0), Height = 48 * scale };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = M3.Icon(items[i].Icon, scheme.OnSurfaceVariant, 18);
            icon.Margin = new Thickness(0, 0, 12 * scale, 0);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(icon);

            var text = new TextBlock
            {
                Text = items[i].Text,
                FontSize = 13 * scale,
                Foreground = scheme.OnSurface,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var host = new Border
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                CornerRadius = new CornerRadius(6),
                Child = row
            };
            host.MouseEnter += (_, _) => host.Background = M3.Layer(M3.Of(scheme.SurfaceLow),
                M3.Of(scheme.OnSurface), M3.HoverAlpha);
            host.MouseLeave += (_, _) => host.Background = Brushes.Transparent;
            host.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                popup.IsOpen = false;
                onPick?.Invoke(index);
            };

            list.Children.Add(host);
        }

        popup.Child = new Border
        {
            MinWidth = 208 * scale,
            Background = scheme.SurfaceLow,
            CornerRadius = new CornerRadius(M3.CornerS),
            Padding = new Thickness(0, 8 * scale, 0, 8 * scale),
            Child = list,
            Effect = M3.Elevation(2)
        };

        // 注意：M3Button 自己的 MouseLeftButtonUp 会设 Handled，事后追加的处理器收不到，
        // 所以开关逻辑必须走 onClick 参数。
        Border button = null!;
        button = M3Button.Create(label, () =>
        {
            // StaysOpen=true，所以可以在 MouseUp 里直接开：
            // 用 StaysOpen=false 时，同一次抬起会被当成「点到外面」，菜单刚开就关。
            popup.PlacementTarget = button;
            popup.IsOpen = !popup.IsOpen;
        }, M3ButtonKind.Tonal, iconData: "M5,9.5 L12,16 L19,9.5", scheme: scheme, scale: scale);

        button.Loaded += (_, _) =>
        {
            var window = Window.GetWindow(button);
            if (window != null) window.Deactivated += (_, _) => popup.IsOpen = false;
        };

        return button;
    }
}

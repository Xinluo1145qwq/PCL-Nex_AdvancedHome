using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>选择器里的一行。</summary>
internal sealed class PickerItem
{
    public string Title = "";
    public string Subtitle = "";
    public BitmapSource? Image;
    public string Glyph = "";
    public object? Payload;
}

/// <summary>
/// 通用选择窗口：一列可筛选的条目，点一行选中，双击或「下一步」确认。
/// 「选择实例 / 选择存档 / 选择服务器」都复用它。
/// </summary>
internal sealed class PickerDialog : Window
{
    private readonly StackPanel _rows = new();
    private readonly TextBox _search;
    private readonly TextBlock _placeholder;
    private readonly TextBlock _empty;
    private readonly Border _confirm;
    private readonly List<PickerItem> _items;
    private readonly List<(PickerItem Item, Border Row)> _built = new();

    private PickerItem? _selected;
    private PickerItem? _result;

    private PickerDialog(string title, string hint, IReadOnlyList<PickerItem> items, string emptyHint, string confirmText)
    {
        _items = items.ToList();

        Dlg.Setup(this, title, 520);

        var panel = new StackPanel();
        panel.Children.Add(Dlg.Header(title, () => DialogResult = false, hint));

        // ── 筛选框 ──
        var searchHost = new Grid { Margin = new Thickness(0, 0, 0, 10) };

        _search = new TextBox
        {
            Height = 34,
            FontSize = 12.5,
            Padding = new Thickness(11, 0, 11, 0),
            Background = Dlg.SubtleFill,
            Foreground = Palette.TextPrimary,
            CaretBrush = Palette.TextPrimary,
            BorderThickness = new Thickness(1),
            BorderBrush = Dlg.SoftBorder,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _search.TextChanged += (_, _) => Refilter();
        searchHost.Children.Add(_search);

        _placeholder = new TextBlock
        {
            Text = "搜索…",
            FontSize = 12.5,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Palette.TextTertiary,
            IsHitTestVisible = false
        };
        searchHost.Children.Add(_placeholder);
        panel.Children.Add(searchHost);

        // ── 列表 ──
        var listGrid = new Grid();

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 342,
            Focusable = false
        };
        scroll.Content = _rows;
        listGrid.Children.Add(scroll);

        _empty = new TextBlock
        {
            Text = emptyHint,
            FontSize = 12,
            Foreground = Palette.TextTertiary,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 40, 0, 40),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        listGrid.Children.Add(_empty);

        panel.Children.Add(listGrid);

        // ── 按钮 ──
        _confirm = (Border)Dlg.Button(confirmText, true, Confirm);
        _confirm.Opacity = 0.4;      // 没选之前看着是灰的，Confirm 内部会忽略点击

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        footer.Children.Add(Dlg.Button("取消", false, () => DialogResult = false));
        footer.Children.Add(_confirm);
        panel.Children.Add(footer);

        Content = Dlg.Shell(panel);

        BuildRows();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
            else if (e.Key == Key.Enter) Confirm();
        };
    }

    private static bool _open;

    /// <summary>弹出选择窗口；返回用户选中的条目，取消时为 null。</summary>
    public static PickerItem? Pick(string title, string hint, IReadOnlyList<PickerItem> items,
        string emptyHint = "这里还没有可选项。", string confirmText = "下一步")
    {
        if (_open) return null;

        _open = true;
        try
        {
            var dialog = new PickerDialog(title, hint, items, emptyHint, confirmText);
            return dialog.ShowDialog() == true ? dialog._result : null;
        }
        catch (Exception ex)
        {
            Log.Error($"打开选择窗口失败（{title}）", ex);
            return null;
        }
        finally
        {
            _open = false;
        }
    }

    // ─────────────────────────── 列表 ───────────────────────────

    private void BuildRows()
    {
        _rows.Children.Clear();
        _built.Clear();

        foreach (var item in _items)
        {
            var row = BuildRow(item);
            _rows.Children.Add(row);
            _built.Add((item, row));
        }

        UpdateEmptyHint();
    }

    private Border BuildRow(PickerItem item)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var tile = Dlg.Tile(new TextBlock(), 34, 8);

        if (item.Image != null)
        {
            tile.Background = new ImageBrush(item.Image)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
        }
        else
        {
            tile.Child = new Path
            {
                Data = Geometry.Parse(string.IsNullOrEmpty(item.Glyph) ? PinFlow.CubeGlyph : item.Glyph),
                Stroke = Palette.TextTertiary,
                StrokeThickness = 1.5,
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                StrokeLineJoin = PenLineJoin.Round
            };
        }

        Grid.SetColumn(tile, 0);
        grid.Children.Add(tile);

        var texts = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(11, 0, 8, 0)
        };
        texts.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = 12.8,
            FontWeight = FontWeights.SemiBold,
            Foreground = Palette.TextPrimary,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrWhiteSpace(item.Subtitle))
        {
            texts.Children.Add(new TextBlock
            {
                Text = item.Subtitle,
                FontSize = 10.8,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = Palette.TextTertiary,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        var row = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerS),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Padding = new Thickness(8, 7, 8, 7),
            Margin = new Thickness(0, 1, 0, 1),
            Cursor = Cursors.Hand,
            Child = grid
        };

        row.MouseEnter += (_, _) =>
        {
            if (!ReferenceEquals(_selected, item)) row.Background = Dlg.Hover;
        };
        row.MouseLeave += (_, _) =>
        {
            if (!ReferenceEquals(_selected, item)) row.Background = Brushes.Transparent;
        };
        row.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Select(item);
        };
        return row;
    }

    private void Select(PickerItem item)
    {
        _selected = item;

        foreach (var (candidate, row) in _built)
        {
            bool on = ReferenceEquals(candidate, item);
            row.BorderBrush = on ? Palette.AccentBlue : Brushes.Transparent;
            row.Background = on
                ? Palette.WithOpacity(((SolidColorBrush)Palette.AccentBlue).Color, 0.12)
                : Brushes.Transparent;
        }

        _confirm.Opacity = 1;
    }

    private void Confirm()
    {
        if (_selected == null) return;
        _result = _selected;
        DialogResult = true;
    }

    private void Refilter()
    {
        string query = (_search.Text ?? "").Trim();
        _placeholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        _rows.Children.Clear();
        _built.Clear();

        foreach (var item in _items)
        {
            bool hit = query.Length == 0
                       || item.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                       || item.Subtitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hit) continue;

            var row = BuildRow(item);
            if (ReferenceEquals(item, _selected))
            {
                row.BorderBrush = Palette.AccentBlue;
                row.Background = Palette.WithOpacity(((SolidColorBrush)Palette.AccentBlue).Color, 0.12);
            }

            _rows.Children.Add(row);
            _built.Add((item, row));
        }

        UpdateEmptyHint();
    }

    private void UpdateEmptyHint()
    {
        bool none = _built.Count == 0;
        _empty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>「单项快捷入口」的两步选择流程：先选实例，再选存档 / 服务器。</summary>
internal static class PinFlow
{
    public const string CubeGlyph = "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z";
    private const string SignalGlyph = "M6,20 V15 M10,20 V11 M14,20 V7 M18,20 V3";

    /// <summary>按类型走完选择流程；用户中途取消返回 null。</summary>
    public static PinOptions? Create(string kind)
    {
        kind = kind is "world" or "server" ? kind : "instance";

        if (kind == "instance")
        {
            var entry = PickInstance("选择实例", "挑一个实例，之后卡片就是这个实例的启动按钮。");
            if (entry == null) return null;

            return new PinOptions
            {
                Kind = "instance",
                Instance = entry.Name,
                Name = entry.Name,
                Target = ""
            };
        }

        var owner = PickInstance(
            kind == "world" ? "选择存档" : "选择服务器",
            "第 1 步：先选这个目标所在的实例。");
        if (owner == null) return null;

        return kind == "world" ? PickWorld(owner) : PickServer(owner);
    }

    /// <summary>实例选择器。</summary>
    public static InstanceEntry? PickInstance(string title, string hint)
    {
        var instances = InstanceStore.Scan()
            .OrderByDescending(i => i.Starred)
            .ThenByDescending(i => i.LastPlayed)
            .ToList();

        var items = instances.Select(instance => new PickerItem
        {
            Title = instance.Name,
            Subtitle = instance.VersionText
                       + (instance.LastPlayed > DateTime.MinValue
                           ? " · 游玩于" + WorldStore.Humanize(instance.LastPlayed)
                           : ""),
            Image = ImageUtil.Load(instance.IconPath) ?? ImageUtil.LoadPclBlock(instance.Loader),
            Glyph = CubeGlyph,
            Payload = instance
        }).ToList();

        var picked = PickerDialog.Pick(title, hint, items, "还没有可用的实例，先去启动器里新建一个。");
        return picked?.Payload as InstanceEntry;
    }

    private static PinOptions? PickWorld(InstanceEntry owner)
    {
        var worlds = WorldStore.ScanInstance(owner.Name);

        var items = worlds.Select(world => new PickerItem
        {
            Title = world.Name,
            Subtitle = string.Join(" · ", new[] { world.GameModeText, world.Version }
                .Where(x => !string.IsNullOrWhiteSpace(x))),
            Image = ImageUtil.Load(world.IconPath),
            Glyph = "M12,21 C12,21 18,15.5 18,10 A6,6 0 1,0 6,10 C6,15.5 12,21 12,21 Z",
            Payload = world
        }).ToList();

        var picked = PickerDialog.Pick("选择存档",
            $"第 2 步：从「{owner.Name}」里挑一个存档。",
            items,
            "这个实例里还没有存档，进游戏创建一个世界再回来。");

        // 启动器要的是存档目录名，不是整条路径
        return picked?.Payload is WorldEntry world
            ? new PinOptions
            {
                Kind = "world",
                Instance = owner.Name,
                Name = world.Name,
                Target = System.IO.Path.GetFileName(world.Folder)
            }
            : null;
    }

    private static PinOptions? PickServer(InstanceEntry owner)
    {
        var servers = ServerStore.ForInstance(owner.Name);

        var items = servers.Select(server => new PickerItem
        {
            Title = server.Name,
            Subtitle = server.Ip,
            Image = server.Icon,
            Glyph = SignalGlyph,
            Payload = server
        }).ToList();

        var picked = PickerDialog.Pick("选择服务器",
            $"第 2 步：从「{owner.Name}」里挑一个服务器。",
            items,
            "这个实例里还没加过服务器，进游戏「多人游戏 → 添加服务器」之后再来。");

        return picked?.Payload is ServerEntry server
            ? new PinOptions { Kind = "server", Instance = owner.Name, Name = server.Name, Target = server.Ip }
            : null;
    }

    /// <summary>「更换目标…」：按已有卡片的类型重新走一遍流程。</summary>
    public static PinOptions? Repick(PinOptions pin)
    {
        if (pin.Kind == "instance") return Create("instance");

        var owner = InstanceStore.Scan()
            .FirstOrDefault(i => string.Equals(i.Name, pin.Instance, StringComparison.OrdinalIgnoreCase));

        // 原实例还在就直接进第二步，省一次点击
        if (owner != null) return pin.Kind == "world" ? PickWorld(owner) : PickServer(owner);

        return Create(pin.Kind);
    }
}

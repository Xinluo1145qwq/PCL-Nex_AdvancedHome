using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCLN.AdvancedHomepage;

/// <summary>皮肤选择结果：来源 + 值（玩家名 / 文件路径 / steve|alex）+ 手臂模型。</summary>
internal sealed class SkinChoice
{
    public SkinChoice(string name, string source, string value, string model = "classic")
    {
        Name = name;
        Source = source;
        Value = value;
        Model = model;
    }

    public string Name { get; }
    public string Source { get; }
    public string Value { get; }

    /// <summary>classic（经典 4px）/ slim（细 3px）。</summary>
    public string Model { get; }
}

/// <summary>
/// 「添加皮肤预览」弹窗：正版玩家名 / 本地皮肤图片 / 内置 Steve、Alex 三选一，
/// 右侧带动起来就是最终效果的 3D 预览。
/// </summary>
internal sealed class SkinDialog : Window
{
    private readonly SkinView _preview = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _titleBox = new();
    private readonly TextBlock _fileLabel = new();
    private readonly Border[] _armTiles = new Border[2];

    private string _source = "builtin";
    private string _value = "steve";
    private string _display = "";
    private string _model = "classic";

    private readonly Border[] _segments = new Border[3];
    private readonly Grid _panels = new();

    private SkinDialog(string title, string subtitle, SkinOptions? current)
    {
        Dlg.Setup(this, title, 620);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(196) });

        if (current != null)
        {
            _source = current.Source;
            _value = current.Value;
            _display = current.Name;
            _model = SkinLibrary.ResolveModel(current);
        }

        var left = new StackPanel();

        // ── 来源切换 ──
        var segmentRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        string[] names = { "正版玩家", "本地图片", "默认皮肤" };
        string[] keys = { "player", "file", "builtin" };

        for (int i = 0; i < names.Length; i++)
        {
            string key = keys[i];
            int index = i;

            var text = new TextBlock
            {
                Text = names[i],
                FontSize = 12.2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var segment = new Border
            {
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 8, 0),
                Child = text,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Dlg.SoftBorder
            };

            segment.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                Switch(keys[index]);
            };

            _segments[i] = segment;
            segmentRow.Children.Add(segment);
        }

        left.Children.Add(segmentRow);

        // ── 三个面板叠在一起，只显示一个 ──
        left.Children.Add(_panels);

        // 1. 正版玩家
        _nameBox.FontSize = 13;
        _nameBox.Padding = new Thickness(10, 8, 10, 8);
        _nameBox.Background = Dlg.SubtleFill;
        _nameBox.Foreground = Palette.TextPrimary;
        _nameBox.BorderThickness = new Thickness(1);
        _nameBox.BorderBrush = Dlg.SoftBorder;
        _nameBox.CaretBrush = Palette.TextPrimary;
        _nameBox.Margin = new Thickness(0, 8, 0, 0);
        _nameBox.MaxLength = 24;
        _nameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) _ = FetchAsync();
        };

        var fetchButton = Dlg.SmallButton("获取皮肤", () => _ = FetchAsync());
        fetchButton.Margin = new Thickness(0, 10, 0, 0);

        _status.FontSize = 11.4;
        _status.Margin = new Thickness(0, 8, 0, 0);
        _status.Foreground = Palette.TextTertiary;
        _status.TextWrapping = TextWrapping.Wrap;

        var playerPanel = new StackPanel
        {
            Children =
            {
                Dlg.Label("输入正版玩家名（会自动从 Mojang 拉取皮肤）"),
                _nameBox,
                fetchButton,
                _status
            }
        };

        // 2. 本地图片
        _fileLabel.FontSize = 12;
        _fileLabel.Foreground = Palette.TextSecondary;
        _fileLabel.TextWrapping = TextWrapping.Wrap;
        _fileLabel.Margin = new Thickness(0, 10, 0, 0);
        _fileLabel.Text = "还没选择图片";

        var filePanel = new StackPanel
        {
            Children =
            {
                Dlg.Label("选一张 64×64（或 64×32）的皮肤 PNG，正方形头像图也能凑合用"),
                Dlg.SmallButton("选择图片…", PickFile),
                _fileLabel
            }
        };

        // 3. 内置皮肤
        var builtinRow = new StackPanel { Orientation = Orientation.Horizontal };
        builtinRow.Children.Add(BuildBuiltin("Steve", "steve"));
        builtinRow.Children.Add(new Border { Width = 12 });
        builtinRow.Children.Add(BuildBuiltin("Alex", "alex"));

        var builtinPanel = new StackPanel
        {
            Children =
            {
                Dlg.Label("不想查名字的话，直接用官方默认皮肤"),
                builtinRow
            }
        };

        _panels.Children.Add(playerPanel);
        _panels.Children.Add(filePanel);
        _panels.Children.Add(builtinPanel);
        foreach (var child in _panels.Children) ((UIElement)child).Visibility = Visibility.Collapsed;

        // ── 卡片标题 + 手臂模型 ──
        _titleBox.FontSize = 13;
        _titleBox.Padding = new Thickness(10, 7, 10, 7);
        _titleBox.Background = Dlg.SubtleFill;
        _titleBox.Foreground = Palette.TextPrimary;
        _titleBox.BorderThickness = new Thickness(1);
        _titleBox.BorderBrush = Dlg.SoftBorder;
        _titleBox.CaretBrush = Palette.TextPrimary;
        _titleBox.MaxLength = 20;
        _titleBox.Text = _display;

        var titleRow = new Grid { Margin = new Thickness(0, 20, 0, 0) };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.Children.Add(new TextBlock
        {
            Text = "卡片标题",
            FontSize = 12.2,
            Foreground = Palette.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(_titleBox, 1);
        titleRow.Children.Add(_titleBox);
        left.Children.Add(titleRow);

        var armRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        armRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        armRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var armLabel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        armLabel.Children.Add(new TextBlock
        {
            Text = "手臂",
            FontSize = 12.2,
            Foreground = Palette.TextSecondary
        });
        armLabel.Children.Add(new TextBlock
        {
            Text = "Alex 是细的 3px",
            FontSize = 10.4,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = Palette.TextTertiary
        });
        armRow.Children.Add(armLabel);

        var armChips = new StackPanel { Orientation = Orientation.Horizontal };
        armChips.Children.Add(BuildArmChip(0, "经典 4px", "classic"));
        armChips.Children.Add(BuildArmChip(1, "细 3px", "slim"));
        Grid.SetColumn(armChips, 1);
        armRow.Children.Add(armChips);
        left.Children.Add(armRow);

        _titleBox.TextChanged += (_, _) => { _display = _titleBox.Text.Trim(); };
        ApplyArmChips();

        // ── 右侧预览 ──
        var previewHost = new Border
        {
            Width = 176,
            Height = 244,
            CornerRadius = new CornerRadius(12),
            Background = Dlg.SubtleFill,
            BorderThickness = new Thickness(1),
            BorderBrush = Dlg.SoftBorder,
            Margin = new Thickness(20, 0, 0, 0),
            Child = _preview,
            ClipToBounds = true
        };
        Grid.SetColumn(previewHost, 1);
        root.Children.Add(left);
        root.Children.Add(previewHost);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        footer.Children.Add(Dlg.Button("取消", false, () => DialogResult = false));
        footer.Children.Add(Dlg.Button("确定", true, Confirm));

        var body = new StackPanel();
        body.Children.Add(Dlg.Header(title, () => DialogResult = false, subtitle));
        body.Children.Add(root);
        body.Children.Add(footer);

        Content = Dlg.Shell(body);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
        };

        // 回填已有配置
        if (current != null && current.Source == "player") _nameBox.Text = current.Value;
        if (current != null && current.Source == "file")
            _fileLabel.Text = System.IO.Path.GetFileName(current.Value);

        Switch(_source);

        // 标题留空时给个合理的默认：内置用皮肤名，正版用玩家名，本地图片留白
        if (string.IsNullOrWhiteSpace(_display))
        {
            _display = _source switch
            {
                "builtin" => string.Equals(_value, "alex", StringComparison.OrdinalIgnoreCase) ? "Alex" : "Steve",
                "player" => _value,
                _ => ""
            };
            _titleBox.Text = _display;
        }

        ApplyArmChips();
        RefreshPreview();
    }

    public SkinChoice? Result { get; private set; }

    public static SkinChoice? Show(string title, string subtitle, SkinOptions? current = null)
    {
        try
        {
            var dialog = new SkinDialog(title, subtitle, current);
            return dialog.ShowDialog() == true ? dialog.Result : null;
        }
        catch (Exception ex)
        {
            Log.Error("打开皮肤选择窗口失败", ex);
            return null;
        }
    }

    // ─────────────────────────── 交互 ───────────────────────────

    /// <summary>手臂模型的两个小胶囊。</summary>
    private FrameworkElement BuildArmChip(int index, string text, string model)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        var chip = new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent,
            BorderBrush = Dlg.SoftBorder,
            Cursor = Cursors.Hand,
            Child = label
        };

        chip.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _model = model;
            ApplyArmChips();
            RefreshPreview();
        };

        _armTiles[index] = chip;
        return chip;
    }

    private void ApplyArmChips()
    {
        for (int i = 0; i < _armTiles.Length; i++)
        {
            var chip = _armTiles[i];
            if (chip == null) continue;

            bool on = (i == 0 && _model != "slim") || (i == 1 && _model == "slim");
            chip.Background = on ? Palette.AccentBrush : Brushes.Transparent;
            chip.BorderBrush = on ? Palette.AccentBrush : Dlg.SoftBorder;
            if (chip.Child is TextBlock text)
                text.Foreground = on ? Palette.Solid("#FFFFFF") : Palette.TextPrimary;
        }
    }

    private void Switch(string source)
    {
        _source = source;

        for (int i = 0; i < _segments.Length; i++)
        {
            bool active = (i == 0 && source == "player") || (i == 1 && source == "file") || (i == 2 && source == "builtin");
            _segments[i].Background = active ? Palette.AccentBrush : Brushes.Transparent;
            _segments[i].BorderBrush = active ? Palette.AccentBrush : Dlg.SoftBorder;
            if (_segments[i].Child is TextBlock text)
                text.Foreground = active ? Palette.Solid("#FFFFFF") : Palette.TextPrimary;
        }

        int index = source switch { "player" => 0, "file" => 1, _ => 2 };
        for (int i = 0; i < _panels.Children.Count; i++)
            ((UIElement)_panels.Children[i]).Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement BuildBuiltin(string name, string key)
    {
        bool active = key == _value && _source == "builtin";

        var head = new Image
        {
            Source = HeadOf(key == "alex" ? SkinLibrary.PaintAlex() : SkinLibrary.PaintSteve()),
            Width = 40,
            Height = 40,
            Stretch = Stretch.Uniform
        };

        var text = new TextBlock
        {
            Text = name,
            FontSize = 12.4,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(head);
        stack.Children.Add(text);

        var tile = new Border
        {
            Width = 104,
            Height = 116,
            CornerRadius = new CornerRadius(10),
            Background = Dlg.SubtleFill,
            BorderThickness = new Thickness(1),
            BorderBrush = active ? Palette.AccentBrush : Dlg.SoftBorder,
            Child = stack,
            Cursor = Cursors.Hand
        };

        tile.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _source = "builtin";
            _value = key;
            _model = key == "alex" ? "slim" : "classic";
            _titleBox.Text = name;
            _display = name;

            foreach (var sibling in ((Panel)tile.Parent).Children)
            {
                if (sibling is Border border && !ReferenceEquals(border, tile))
                    border.BorderBrush = Dlg.SoftBorder;
            }

            tile.BorderBrush = Palette.AccentBrush;
            ApplyArmChips();
            RefreshPreview();
        };

        return tile;
    }

    private void PickFile()
    {
        var path = SettingsDialog.PickImage();
        if (path == null) return;

        // 标题栏不再直接用文件名，留白让用户自己起名
        _fileLabel.Text = System.IO.Path.GetFileName(path);
        _source = "file";
        _value = path;
        _display = "";
        _titleBox.Text = "";
        RefreshPreview();
    }

    private async Task FetchAsync()
    {
        string name = _nameBox.Text.Trim();
        if (name.Length == 0)
        {
            _status.Text = "先输入一个玩家名";
            return;
        }

        _status.Foreground = Palette.TextTertiary;
        _status.Text = "正在获取…";

        try
        {
            var fetch = await SkinLibrary.FetchPlayerAsync(name);

            _source = "player";
            _value = fetch.Name.Length > 0 ? fetch.Name : name;
            _model = fetch.Model;
            _display = _value;
            _titleBox.Text = _value;

            ApplyArmChips();
            _preview.SetSkin(fetch.Image, _model);
            _status.Foreground = Palette.AccentBlue;
            _status.Text = $"已获取 {_value} 的皮肤（{(fetch.Model == "slim" ? "细手臂" : "经典手臂")}）";
        }
        catch (Exception ex)
        {
            _status.Foreground = Palette.Solid("#F0524D");
            _status.Text = ex.Message;
            Log.Warn($"获取皮肤失败：{name}（{ex.Message}）");
        }
    }

    private void RefreshPreview()
    {
        var skin = _source switch
        {
            "player" => SkinLibrary.Load(new SkinOptions { Source = "player", Value = _value }),
            "file" => SkinLibrary.Load(new SkinOptions { Source = "file", Value = _value }),
            _ => string.Equals(_value, "alex", StringComparison.OrdinalIgnoreCase)
                ? SkinLibrary.PaintAlex()
                : SkinLibrary.PaintSteve()
        };

        _preview.SetSkin(skin, _model);
    }

    private void Confirm()
    {
        if (_source == "player" && _value.Length == 0) return;
        if (_source == "file" && _value.Length == 0) return;

        Result = new SkinChoice(_display.Trim(), _source, _value, _model);
        DialogResult = true;
    }

    /// <summary>从整张贴图里裁出正脸，做成 40×40 的小头像。</summary>
    private static BitmapSource HeadOf(BitmapSource skin)
    {
        try
        {
            var face = new CroppedBitmap(skin, new Int32Rect(8, 8, 8, 8));
            var scaled = new TransformedBitmap(face, new ScaleTransform(8, 8));
            scaled.Freeze();
            return scaled;
        }
        catch
        {
            return skin;
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCLN.AdvancedHomepage;

/// <summary>一个网站编辑好之后的结果。</summary>
internal sealed record SiteEdit(string Name, string Url, string Icon);

/// <summary>
/// 「自定义网站」对话框：名称 / 网址 / 图标（本地图片或图片地址），带实时预览。
/// 单个网站组件和多网站组件的管理窗口都用它。
/// </summary>
internal sealed class SiteDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBox _urlBox;
    private readonly TextBox _iconBox;
    private readonly Border _preview;

    public SiteEdit? Result { get; private set; }

    private SiteDialog(string title, SiteEdit? initial)
    {
        Dlg.Setup(this, title, 520);

        var panel = new StackPanel();
        panel.Children.Add(Dlg.Header(title,
            () => DialogResult = false,
            "两处都可以留空：名称会取域名，图标会用一个默认的地球。"));

        // ── 名称 ──
        panel.Children.Add(Dlg.Label("名称"));
        _nameBox = Input(initial?.Name ?? "");
        _nameBox.Margin = new Thickness(0, 8, 0, 16);
        panel.Children.Add(_nameBox);

        // ── 网址 ──
        panel.Children.Add(Dlg.Label("网址"));
        _urlBox = Input(initial?.Url ?? "");
        _urlBox.Margin = new Thickness(0, 8, 0, 16);
        panel.Children.Add(_urlBox);

        // ── 图标 ──
        panel.Children.Add(Dlg.Label("图标（本地图片路径或图片地址，可留空）"));

        var iconRow = new Grid { Margin = new Thickness(0, 8, 0, 6) };
        iconRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        iconRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        iconRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _preview = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(9),
            Background = Dlg.SubtleFill,
            BorderThickness = new Thickness(1),
            BorderBrush = Dlg.SoftBorder,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_preview, 0);
        iconRow.Children.Add(_preview);

        _iconBox = Input(initial?.Icon ?? "");
        _iconBox.Margin = new Thickness(10, 0, 10, 0);
        _iconBox.LostFocus += (_, _) => RefreshPreview();
        Grid.SetColumn(_iconBox, 1);
        iconRow.Children.Add(_iconBox);

        var pick = Dlg.Button("选择图片…", false, PickFile);
        Grid.SetColumn(pick, 2);
        iconRow.Children.Add(pick);

        panel.Children.Add(iconRow);

        var hint = Dlg.Label("支持 png / jpg / ico；网址图标会自动下载一次并缓存到插件目录。");
        hint.Margin = new Thickness(0, 2, 0, 20);
        panel.Children.Add(hint);

        // ── 按钮 ──
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(Dlg.Button("取消", false, () => DialogResult = false));
        buttons.Children.Add(Dlg.Button(initial == null ? "添加" : "保存更改", true, Save));
        panel.Children.Add(buttons);

        Content = Dlg.Shell(panel);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
            if (e.Key == Key.Enter && Keyboard.FocusedElement is TextBox) Save();
        };

        Loaded += (_, _) =>
        {
            RefreshPreview();
            _nameBox.Focus();
            _nameBox.CaretIndex = _nameBox.Text.Length;
        };
    }

    private static bool _open;

    /// <summary>弹出编辑窗；取消返回 null。</summary>
    public static SiteEdit? Show(string title, SiteEdit? initial = null)
    {
        if (_open) return null;

        _open = true;
        try
        {
            var dialog = new SiteDialog(title, initial);
            return dialog.ShowDialog() == true ? dialog.Result : null;
        }
        catch (Exception ex)
        {
            Log.Error("打开网站编辑窗失败", ex);
            return null;
        }
        finally
        {
            _open = false;
        }
    }

    private static TextBox Input(string text) => new()
    {
        Height = 38,
        FontSize = 12.5,
        Padding = new Thickness(10, 0, 10, 0),
        VerticalContentAlignment = VerticalAlignment.Center,
        Background = Palette.Solid(Palette.IsDark ? "#1AFFFFFF" : "#0A000000"),
        Foreground = Palette.TextPrimary,
        BorderThickness = new Thickness(1),
        BorderBrush = Palette.Solid(Palette.IsDark ? "#26FFFFFF" : "#14000000"),
        CaretBrush = Palette.TextPrimary,
        Text = text
    };

    private void PickFile()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择网站图标",
                Filter = "图片|*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif|所有文件|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true) _iconBox.Text = dialog.FileName;
        }
        catch (Exception ex)
        {
            Log.Error("选择图标文件失败", ex);
        }
    }

    private void RefreshPreview()
    {
        _preview.Child = null;
        _preview.Background = Dlg.SubtleFill;

        var bitmap = Sites.LoadIcon(_iconBox.Text, 96);
        if (bitmap != null)
        {
            _preview.Background = new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            return;
        }

        _preview.Child = GlobeIcon(20, Palette.TextTertiary);
    }

    private void Save()
    {
        var url = Sites.Normalize(_urlBox.Text);
        if (string.IsNullOrWhiteSpace(url))
        {
            _urlBox.Focus();
            return;
        }

        var name = _nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = Sites.Host(url);

        Result = new SiteEdit(name, url, _iconBox.Text.Trim());
        DialogResult = true;
    }

    /// <summary>地球图标：没有自定义图标时用它。</summary>
    public static FrameworkElement GlobeIcon(double size, Brush brush) => new Path
    {
        Data = Geometry.Parse("M12,3 A9,9 0 1,1 11.99,3 Z M3,12 H21 M12,3 C15.4,6.4 15.4,17.6 12,21 C8.6,17.6 8.6,6.4 12,3"),
        Stroke = brush,
        StrokeThickness = size * 0.075,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Stretch = Stretch.Uniform,
        Width = size,
        Height = size,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「便签」主页的便签卡片（M3 Expressive）：
/// 标题 + 自动保存状态，下面是一整块可编辑的文本域（焦点时描边变主题色）。
/// 打字按 600ms 防抖落盘，避免每敲一个字都写 JSON。
/// </summary>
internal sealed class NotesView : Grid
{
    private const double RefWidth = 340;

    private readonly TextBlock _title = new();
    private readonly TextBlock _hint = new();
    private readonly Border _field;
    private readonly TextBox _editor;
    private readonly TextBlock _placeholder;
    private readonly DispatcherTimer _saveTimer;

    private double _width = RefWidth;
    private double _scale = 1;
    private bool _loading;
    private bool _editable = true;

    public NotesView()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        ClipToBounds = false;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ── 标题行：便签 + 保存状态 ──
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _title.Text = "便签";
        _title.FontWeight = FontWeights.SemiBold;
        _title.Foreground = M3.C.OnSurface;
        _title.VerticalAlignment = VerticalAlignment.Bottom;
        header.Children.Add(_title);

        _hint.FontSize = 11;
        _hint.Foreground = M3.C.OnSurfaceVariant;
        _hint.VerticalAlignment = VerticalAlignment.Bottom;
        _hint.Margin = new Thickness(0, 0, 1, 3);
        Grid.SetColumn(_hint, 1);
        header.Children.Add(_hint);

        Children.Add(header);

        // ── 文本域 ──
        _editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = M3.C.OnSurface,
            CaretBrush = M3.C.Primary,
            SelectionBrush = M3.Layer(M3.Of(M3.C.Primary), M3.Of(M3.C.Surface), 0.35),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Top,
            FocusVisualStyle = null
        };
        _editor.Text = Cfg.NoteText;
        _editor.TextChanged += OnTextChanged;
        _editor.GotKeyboardFocus += (_, _) => UpdateFieldStroke();
        _editor.LostKeyboardFocus += (_, _) => UpdateFieldStroke();
        _editor.Loaded += (_, _) => UpdatePlaceholder();

        _placeholder = new TextBlock
        {
            Text = "写点什么……自动保存",
            Foreground = M3.C.OnSurfaceVariant,
            Opacity = 0.55,
            IsHitTestVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        var fieldBody = new Grid();
        fieldBody.Children.Add(_editor);
        fieldBody.Children.Add(_placeholder);

        _field = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            // M3 filled text field：容器比卡片高一档 tone，否则和卡片底几乎同色、看不出是可输入区
            Background = M3.C.SurfaceContainerHighest,
            BorderThickness = new Thickness(1),
            BorderBrush = M3.C.OutlineVariant,
            Child = fieldBody
        };
        Grid.SetRow(_field, 1);
        Children.Add(_field);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Cfg.NoteText = _editor.Text;
            Cfg.SaveNotes();
            _hint.Text = Summary() + " · 已保存";
            _hint.Opacity = 1;
        };

        Build(RefWidth);
        RefreshHint();

        // 切页 / 关启动器时可能还有 600ms 内的输入没落盘，这里补一次
        Unloaded += (_, _) => Flush();
    }

    private void Flush()
    {
        if (!_saveTimer.IsEnabled) return;
        _saveTimer.Stop();
        Cfg.NoteText = _editor.Text;
        Cfg.SaveNotes();
    }

    /// <summary>组件显示/隐藏：离开便签页时把没落盘的内容补写一次。</summary>
    public void SetActive(bool active)
    {
        if (!active) Flush();
    }

    /// <summary>编辑模式下让出鼠标：整卡要能拖动，文字也不该在这里被改。</summary>
    public void SetEditing(bool editing)
    {
        _editable = !editing;
        _editor.IsHitTestVisible = !editing;
        Opacity = editing ? 0.92 : 1;
        if (editing) _editor.ClearValue(TextBox.BorderBrushProperty);
    }

    public void ApplyTheme()
    {
        _title.Foreground = M3.C.OnSurface;
        _hint.Foreground = M3.C.OnSurfaceVariant;
        _field.Background = M3.C.SurfaceContainerHighest;
        _editor.Foreground = M3.C.OnSurface;
        _editor.CaretBrush = M3.C.Primary;
        _editor.SelectionBrush = M3.Layer(M3.Of(M3.C.Primary), M3.Of(M3.C.Surface), 0.35);
        _placeholder.Foreground = M3.C.OnSurfaceVariant;
        UpdateFieldStroke();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;

        UpdatePlaceholder();
        Cfg.NoteText = _editor.Text;
        _hint.Text = "编辑中…";
        _hint.Opacity = 0.7;

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private string Summary()
    {
        int count = _editor.Text?.Length ?? 0;
        return count == 0 ? "空便签" : $"{count} 字";
    }

    private void RefreshHint()
    {
        _hint.Text = Summary();
        _hint.Opacity = 0.75;
    }

    private void UpdatePlaceholder()
    {
        _placeholder.Visibility = string.IsNullOrEmpty(_editor.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateFieldStroke()
    {
        bool focused = _editable && _editor.IsKeyboardFocusWithin;
        _field.BorderBrush = focused ? M3.C.Primary : M3.C.OutlineVariant;
        _placeholder.Visibility = string.IsNullOrEmpty(_editor.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = _width;

        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * 0.62;

        if (Math.Abs(w - _width) > 0.4) Build(w);

        var size = new Size(w, double.IsInfinity(availableSize.Height) ? h : Math.Min(h, availableSize.Height));
        base.MeasureOverride(size);
        return size;
    }

    private void Build(double width)
    {
        if (width <= 0) return;

        _width = width;
        _scale = Math.Clamp(width / RefWidth, 0.72, 2.2);
        double s = _scale;

        Margin = new Thickness(16 * s, 14 * s, 16 * s, 14 * s);
        _title.FontSize = UiStyles.CardTitleSize(width);
        _hint.FontSize = 11 * s;

        _field.Margin = new Thickness(0, 9 * s, 0, 0);
        _field.Padding = new Thickness(12 * s, 10 * s, 12 * s, 10 * s);
        _editor.FontSize = 14 * s;
        _placeholder.FontSize = 14 * s;
        _placeholder.LineHeight = 22 * s;
    }
}

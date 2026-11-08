using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 单个组件的设置面板。控件全部用启动器自带的 MySlider / MyCheckBox / MyButton，
/// 分节用和设置页一致的卡片块，标题栏与「完成」按钮也照 PCL 弹窗的样式来。
/// 外观部分（背景图片 / 背景透明度）对所有组件通用，其余行由调用方按组件追加。
/// </summary>
internal sealed class SettingsDialog : Window
{
    private readonly StackPanel _body = new();
    private readonly Grid _sections = new();
    private readonly StackPanel[] _columns;
    private int _column;
    private StackPanel? _current;
    private int _rowsInSection;
    private bool _dirty;
    private readonly Action? _live;
    private readonly ScrollViewer _scroll;

    private SettingsDialog(string title, string subtitle, Action? live, int columns)
    {
        _live = live;
        columns = Math.Clamp(columns, 1, 2);

        Dlg.Setup(this, title, columns == 2 ? 840 : 472, scrim: true);
        SizeToContent = SizeToContent.Manual;

        _columns = new StackPanel[columns];
        for (int i = 0; i < columns; i++)
        {
            _columns[i] = new StackPanel { Margin = new Thickness(i == 0 ? 0 : 12, 0, i == columns - 1 ? 0 : 12, 0) };
            _sections.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _sections.Children.Add(_columns[i]);
            Grid.SetColumn(_columns[i], i);
        }

        var headerText = new StackPanel();
        headerText.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Palette.TextPrimary
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            headerText.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 0),
                Foreground = Palette.TextTertiary
            });
        }

        // 和「添加组件」窗口一致：右上角也要有关闭按钮，不能只靠底部「完成」
        var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(headerText);

        var close = Dlg.CloseButton(Finish);
        Grid.SetColumn(close, 1);
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(8, -6, -6, 0);
        header.Children.Add(close);

        _body.Children.Add(header);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Margin = new Thickness(0, 0, -8, 0),
            Padding = new Thickness(0, 0, 8, 0),
            Content = _sections
        };
        _body.Children.Add(scroll);
        _scroll = scroll;

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };

        var done = NativeButton("完成", true, Finish);
        done.Margin = new Thickness(0);
        footer.Children.Add(done);
        _body.Children.Add(footer);

        Dlg.Mount(this, Dlg.Shell(_body));

        // 内容再长也不许超出屏幕：留出标题栏和底部的余量，超出就滚动
        Loaded += (_, _) =>
        {
            try
            {
                var owner = Owner;
                double available = (owner?.ActualHeight ?? SystemParameters.WorkArea.Height) - 96;
                _scroll.MaxHeight = Math.Max(240, available - 150);
            }
            catch
            {
                _scroll.MaxHeight = 520;
            }
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Finish();
        };
    }

    /// <summary>打开设置面板；返回是否有改动。</summary>
    public static bool Show(string title, string subtitle, Action<SettingsDialog> build, Action? live = null, int columns = 1)
    {
        try
        {
            var dialog = new SettingsDialog(title, subtitle, live, columns);
            build(dialog);
            dialog.ShowDialog();
            return dialog._dirty;
        }
        catch (Exception ex)
        {
            Log.Error("打开组件设置面板失败", ex);
            return false;
        }
    }

    private void Finish()
    {
        try
        {
            DialogResult = _dirty;
        }
        catch
        {
            // 不是模态打开时 DialogResult 会抛异常，直接关窗
            Close();
        }
    }

    // ─────────────────────────── 分节 ───────────────────────────

    public void AddSection(string title)
    {
        _rowsInSection = 0;

        var stack = new StackPanel();

        var caption = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = M3.C.OnSurfaceVariant,
            Margin = new Thickness(2, 0, 0, 10)
        };
        stack.Children.Add(caption);

        _current = stack;
        _columns[_column].Children.Add(new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = M3.C.SurfaceContainer,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(16, 16, 16, 10),
            Margin = new Thickness(0, 0, 0, 12),
            Child = stack
        });
    }

    /// <summary>换到下一列（两栏布局用）。</summary>
    public void NextColumn()
    {
        if (_columns.Length < 2) return;
        _column = Math.Min(_columns.Length - 1, _column + 1);
        _current = null;
        _rowsInSection = 0;
    }

    /// <summary>一行：左侧标签（可带说明），右侧控件。控件右边缘全部对齐。</summary>
    private void AddRow(string label, FrameworkElement control, string desc = "", bool stacked = false)
    {
        _current ??= NewPlainSection();

        if (_rowsInSection > 0)
        {
            _current.Children.Add(new Border
            {
                Height = 1,
                Background = M3.C.OutlineVariant,
                Margin = new Thickness(0, 0, 0, 0)
            });
        }

        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 12, 2)
        };

        text.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = M3.C.OnSurface,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrWhiteSpace(desc))
        {
            text.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 12,
                LineHeight = 16,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = M3.C.OnSurfaceVariant
            });
        }

        // 宽控件（三段式分段按钮、三个按钮并排）在左右布局里会把标签挤成「显示…」，
        // 这类行改成上下两行：标签在上，控件在下，M3 里也是这么处理宽选择器的。
        if (stacked)
        {
            text.Margin = new Thickness(0, 2, 0, 8);
            control.VerticalAlignment = VerticalAlignment.Center;
            control.HorizontalAlignment = HorizontalAlignment.Left;

            var stack = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            stack.Children.Add(text);
            stack.Children.Add(control);

            _current.Children.Add(stack);
            _rowsInSection++;
            return;
        }

        control.VerticalAlignment = VerticalAlignment.Center;
        control.HorizontalAlignment = HorizontalAlignment.Right;

        var row = new Grid { MinHeight = 48 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 0);
        Grid.SetColumn(control, 1);
        row.Children.Add(text);
        row.Children.Add(control);

        _current.Children.Add(row);
        _rowsInSection++;
    }

    private StackPanel NewPlainSection()
    {
        var stack = new StackPanel();
        _columns[_column].Children.Add(stack);
        return stack;
    }

    // ─────────────────────────── 具体控件 ───────────────────────────

    /// <summary>背景图片 + 背景透明度（所有组件通用）。</summary>
    public void AddBackground(string cardId, Action? live = null, double defaultOpacity = 1)
    {
        AddSection("外观");

        var look = Cfg.Look(cardId);
        string bg = look?.Bg ?? "";
        double opacity = look?.Opacity ?? defaultOpacity;

        // 只有「设了背景图」时才用图片透明度兜底；
        // 问候这类默认透明的组件必须老老实实从 0 开始，否则滑块显示 100%、卡片却是透的。
        if (opacity <= 0.001 && look != null && !string.IsNullOrWhiteSpace(look.Bg))
            opacity = look.BgOpacity;

        Action refresh = () => live?.Invoke();

        // 缩略图：按当前透明度实时预览
        var thumb = new Border
        {
            Width = 86,
            Height = 48,
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            BorderBrush = Dlg.SoftBorder,
            Background = Dlg.SubtleFill,
            ClipToBounds = true
        };

        var empty = new TextBlock
        {
            Text = "未设置",
            FontSize = 11,
            Foreground = Palette.TextTertiary,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        void PaintThumb()
        {
            var image = string.IsNullOrWhiteSpace(bg) ? null : ImageUtil.Load(bg, 320);
            if (image == null)
            {
                thumb.Child = empty;
                return;
            }

            thumb.Child = new Image
            {
                Source = image,
                Stretch = Stretch.UniformToFill,
                Opacity = Math.Clamp(opacity, 0.08, 1)
            };
        }

        PaintThumb();

        var pick = NativeButton("选择图片…", false, () =>
        {
            var path = PickImage();
            if (path == null) return;

            bg = path;
            _dirty = true;
            Cfg.SetLook(cardId, bg, opacity, opacity);
            PaintThumb();
            refresh();
        });

        var clear = NativeButton("清除", false, () =>
        {
            bg = "";
            opacity = defaultOpacity;
            _dirty = true;
            Cfg.SetLook(cardId, "", defaultOpacity, defaultOpacity);
            PaintThumb();
            refresh();
        });

        pick.Margin = new Thickness(0);
        clear.Margin = new Thickness(8, 0, 0, 0);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        buttons.Children.Add(pick);
        buttons.Children.Add(clear);

        var pickGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        pickGroup.Children.Add(thumb);
        pickGroup.Children.Add(new Border { Width = 10 });
        pickGroup.Children.Add(buttons);

        AddRow("背景图片", pickGroup);

        // M3 滑块
        var slider = new M3Slider((int)Math.Round(opacity * 100), 0, 100)
        {
            Width = 168,
            VerticalAlignment = VerticalAlignment.Center
        };

        var percent = new TextBlock
        {
            Text = $"{opacity * 100:0}%",
            FontSize = 11,
            Width = 38,
            TextAlignment = TextAlignment.Right,
            Foreground = Palette.TextTertiary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        slider.Changed += value =>
        {
            opacity = Math.Round(value / 5.0) * 5 / 100.0;
            percent.Text = $"{opacity * 100:0}%";
            _dirty = true;
            Cfg.SetLook(cardId, bg, opacity, opacity);
            PaintThumb();
            refresh();
        };

        var sliderRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        sliderRow.Children.Add(slider);
        sliderRow.Children.Add(percent);

        AddRow("背景透明度", sliderRow, "拖到 0 就完全透出壁纸");
    }

    /// <summary>一组互斥选项：M3 分段按钮。</summary>
    public void AddChoice(string label, string[] options, int selected, Action<int> set, string desc = "")
    {
        var segments = new M3Segmented(options, selected, index =>
        {
            _dirty = true;
            set(index);
            _live?.Invoke();
        });

        AddRow(label, segments, desc, stacked: options.Length >= 3);
    }

    /// <summary>一条原生滑块，值域 [0, max]，外面自己贴一个数字。</summary>
    public void AddSlider(string label, string desc, int max, int value, Action<int> set, Func<int, string>? format = null)
    {
        int sliderMax = Math.Max(1, max);
        int sliderValue = Math.Clamp(value, 0, sliderMax);

        var slider = new M3Slider(sliderValue, 0, sliderMax)
        {
            Width = 168,
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new TextBlock
        {
            Text = format?.Invoke(sliderValue) ?? sliderValue.ToString(),
            FontSize = 11,
            Width = 48,
            TextAlignment = TextAlignment.Right,
            Foreground = Palette.TextTertiary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        slider.Changed += current =>
        {
            int rounded = (int)Math.Round(current);
            _dirty = true;
            text.Text = format?.Invoke(rounded) ?? rounded.ToString();
            set(rounded);
            _live?.Invoke();
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(slider);
        row.Children.Add(text);

        AddRow(label, row, desc);
    }

    /// <summary>占满整行宽度的多行输入框（自定义标语这类）。</summary>
    public void AddMultiline(string label, string desc, string value, Action<string> set, int height = 86)
    {
        _current ??= NewPlainSection();

        if (_rowsInSection > 0)
        {
            _current.Children.Add(new Border
            {
                Height = 1,
                Background = Dlg.SoftBorder
            });
        }

        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 4) };

        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = Palette.TextPrimary
        });

        if (!string.IsNullOrWhiteSpace(desc))
        {
            stack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = Palette.TextTertiary,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var box = new TextBox
        {
            Text = value ?? "",
            Height = height,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 14,
            Padding = new Thickness(4, 2, 4, 2),
            Background = Brushes.Transparent,
            Foreground = M3.C.OnSurface,
            BorderThickness = new Thickness(0),
            CaretBrush = M3.C.Primary
        };

        // M3 filled text field：surface container highest + 12dp 上圆角
        var field = new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = M3.C.SurfaceContainerHighest,
            BorderThickness = new Thickness(1),
            BorderBrush = M3.C.OutlineVariant,
            Child = box
        };

        box.TextChanged += (_, _) =>
        {
            _dirty = true;
            set(box.Text ?? "");
            _live?.Invoke();
        };

        stack.Children.Add(field);
        _current.Children.Add(stack);
        _rowsInSection++;
    }

    /// <summary>把一段现成的界面塞进当前分节（比如网站列表编辑器）。</summary>
    public void AddNode(FrameworkElement node)
    {
        _current ??= NewPlainSection();
        node.Margin = new Thickness(0, 6, 0, 6);
        _current.Children.Add(node);
        _rowsInSection++;
    }

    /// <summary>只读的一行：左边标签，右边一段值。</summary>
    public void AddValue(string label, string value)
    {
        AddRow(label, new TextBlock
        {
            Text = value ?? "",
            FontSize = 14,
            Foreground = M3.C.OnSurfaceVariant,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 260
        });
    }

    /// <summary>单行输入框（城市名、网址这类）。</summary>
    public void AddTextField(string label, string desc, string value, Action<string> set)
    {
        _current ??= NewPlainSection();

        if (_rowsInSection > 0)
        {
            _current.Children.Add(new Border
            {
                Height = 1,
                Background = M3.C.OutlineVariant
            });
        }

        var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 6) };

        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = M3.C.OnSurface
        });

        if (!string.IsNullOrWhiteSpace(desc))
        {
            stack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = M3.C.OnSurfaceVariant,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var box = new TextBox
        {
            Text = value ?? "",
            FontSize = 14,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = M3.C.OnSurface,
            BorderThickness = new Thickness(0),
            CaretBrush = M3.C.Primary,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var field = new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = M3.C.SurfaceContainerHighest,
            BorderThickness = new Thickness(1),
            BorderBrush = M3.C.OutlineVariant,
            Child = box
        };

        box.TextChanged += (_, _) =>
        {
            _dirty = true;
            set(box.Text ?? "");
            _live?.Invoke();
        };

        stack.Children.Add(field);
        _current.Children.Add(stack);
        _rowsInSection++;
    }

    /// <summary>一块随输入实时变化的预览；返回外框，调用方可以改底色（比如白字配深底）。</summary>
    public Border AddPreview(Func<string> text, Action<TextBlock>? bind = null)
    {
        _current ??= NewPlainSection();

        var block = new TextBlock
        {
            Text = text() ?? "",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Palette.TextPrimary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 10, 2, 8)
        };

        bind?.Invoke(block);

        var host = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            Background = M3.C.SurfaceContainerHighest,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 6, 0, 6),
            Child = block
        };

        _current.Children.Add(host);
        _rowsInSection++;
        return host;
    }

    /// <summary>开关行：标签 + 说明 + 右侧原生复选框。</summary>
    public void AddToggle(string label, string desc, bool value, Action<bool> set)
    {
        var toggle = new M3Switch(value)
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        toggle.Changed += next =>
        {
            _dirty = true;
            set(next);
            _live?.Invoke();
        };

        AddRow(label, toggle, desc);
    }

    /// <summary>一行标签 + 一颗原生按钮（例如「更换皮肤…」）。</summary>
    public void AddAction(string label, string text, Action click)
    {
        var button = NativeButton(text, false, () =>
        {
            click();
            _dirty = true;
            _live?.Invoke();
        });

        AddRow(label, button);
    }

    /// <summary>纯提示文字（占满整节宽度）。</summary>
    public void AddNote(string text)
    {
        _current ??= NewPlainSection();

        _current.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            LineHeight = 17,
            TextWrapping = TextWrapping.Wrap,
            Foreground = M3.C.OnSurfaceVariant,
            Margin = new Thickness(2, 8, 2, 4)
        });
    }

    // ─────────────────────────── 原生控件 ───────────────────────────

    /// <summary>M3 按钮：主要动作用 filled，其余用 tonal。</summary>
    private static FrameworkElement NativeButton(string text, bool primary, Action click)
    {
        var button = M3Button.Create(text, click,
            primary ? M3ButtonKind.Filled : M3ButtonKind.Tonal, scale: 0.85);
        button.Margin = new Thickness(6, 0, 0, 0);
        button.VerticalAlignment = VerticalAlignment.Center;
        return button;
    }

    // ─────────────────────────── 小工具 ───────────────────────────

    public static string? PickImage()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择图片",
                Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
        catch (Exception ex)
        {
            Log.Warn("选择图片失败：" + ex.Message);
            return null;
        }
    }
}

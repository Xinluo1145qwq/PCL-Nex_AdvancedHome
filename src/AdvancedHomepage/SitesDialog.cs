using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「管理网站」：增删改收藏的网址。
/// 列表既能开独立窗口用，也能直接嵌进组件设置里（<see cref="BuildEditor"/>）。
/// </summary>
internal sealed class SitesDialog : Window
{
    private SitesDialog()
    {
        Dlg.Setup(this, "管理网站", 560);

        var panel = new StackPanel();
        panel.Children.Add(Dlg.Header("管理网站",
            () => DialogResult = true,
            "这些网站会显示在「多个网站」组件里，点一行就能用浏览器打开。"));

        panel.Children.Add(BuildEditor());

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        footer.Children.Add(Dlg.Button("完成", true, () => DialogResult = true));
        panel.Children.Add(footer);

        Content = Dlg.Shell(panel);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = true;
        };
    }

    private static bool _open;

    public static void Show()
    {
        if (_open) return;

        _open = true;
        try
        {
            new SitesDialog().ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Error("打开网站管理窗失败", ex);
        }
        finally
        {
            _open = false;
        }
    }

    /// <summary>网站列表编辑器：直接塞进组件设置用，改动会通过 onChanged 通知外面刷新卡片。</summary>
    public static FrameworkElement BuildEditor(Action? onChanged = null)
    {
        var list = new StackPanel { Orientation = Orientation.Vertical };

        void Rebuild()
        {
            list.Children.Clear();

            var sites = Cfg.Sites;
            if (sites.Count == 0)
            {
                list.Children.Add(new Border
                {
                    Height = 68,
                    CornerRadius = new CornerRadius(M3.CornerM),
                    BorderThickness = new Thickness(1),
                    BorderBrush = M3.C.OutlineVariant,
                    Background = M3.C.SurfaceContainerHighest,
                    Child = new TextBlock
                    {
                        Text = "还没有网站，点下面的「添加网站」加一个。",
                        FontSize = 12,
                        Foreground = M3.C.OnSurfaceVariant,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });
                return;
            }

            foreach (var site in sites) list.Children.Add(BuildRow(site, Rebuild, onChanged));
        }

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 260,
            Margin = new Thickness(0, 0, -8, 0),
            Padding = new Thickness(0, 0, 8, 0),
            Focusable = false,
            Content = list
        };

        var add = Dlg.Button("添加网站", false, () =>
        {
            var edit = SiteDialog.Show("添加网站");
            if (edit == null) return;

            Cfg.AddSite(edit.Name, edit.Url, edit.Icon);
            Rebuild();
            onChanged?.Invoke();
        });
        add.Margin = new Thickness(0, 10, 0, 0);
        add.HorizontalAlignment = HorizontalAlignment.Left;

        var host = new StackPanel();
        host.Children.Add(scroll);
        host.Children.Add(add);

        Rebuild();
        return host;
    }

    private static FrameworkElement BuildRow(SiteOptions site, Action rebuild, Action? onChanged)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var frame = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(M3.CornerS),
            Background = M3.C.SurfaceContainerHighest,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center
        };

        var bitmap = Sites.LoadIcon(site.Icon, 96);
        if (bitmap != null)
        {
            frame.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        }
        else
        {
            frame.Child = SiteDialog.GlobeIcon(18, M3.C.OnSurfaceVariant);
        }

        Grid.SetColumn(frame, 0);
        grid.Children.Add(frame);

        var texts = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(11, 0, 8, 0)
        };
        texts.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(site.Name) ? Sites.Host(site.Url) : site.Name,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = M3.C.OnSurface,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        texts.Children.Add(new TextBlock
        {
            Text = site.Url,
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = M3.C.OnSurfaceVariant,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(TextButton("编辑", false, () =>
        {
            var edit = SiteDialog.Show("编辑网站", new SiteEdit(site.Name, site.Url, site.Icon));
            if (edit == null) return;

            Cfg.UpdateSite(site.Id, edit.Name, edit.Url, edit.Icon);
            rebuild();
            onChanged?.Invoke();
        }));
        actions.Children.Add(TextButton("删除", true, () =>
        {
            Cfg.RemoveSite(site.Id);
            rebuild();
            onChanged?.Invoke();
        }));
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        return new Border
        {
            CornerRadius = new CornerRadius(M3.CornerM),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Background = M3.C.SurfaceContainerHighest,
            Child = grid
        };
    }

    private static FrameworkElement TextButton(string text, bool danger, Action onClick)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Foreground = danger ? M3.C.Error : M3.C.OnSurfaceVariant,
            VerticalAlignment = VerticalAlignment.Center
        };

        var button = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerS),
            Padding = new Thickness(9, 5, 9, 5),
            Margin = new Thickness(6, 0, 0, 0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = label
        };

        button.MouseEnter += (_, _) => button.Background = M3.C.SurfaceHigh;
        button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;
        button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return button;
    }
}

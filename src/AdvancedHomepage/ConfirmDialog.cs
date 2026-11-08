using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 一颗 M3 基础对话框：标题 + 正文 + 右下角两个动作。
/// 用来做「重置布局」「删除组件」这类不可撤销操作的确认。
/// </summary>
internal sealed class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message, string confirmText, string cancelText, bool danger)
    {
        Dlg.Setup(this, title, 460);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Width = 392 };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiSkin.Ink,
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            LineHeight = 20,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiSkin.Dim
        });

        var cancel = M3Button.Create(cancelText, () => DialogResult = false, M3ButtonKind.Text, scheme: UiSkin.L);
        var confirm = M3Button.Create(confirmText, () => DialogResult = true,
            M3ButtonKind.Filled, danger: danger, scheme: UiSkin.L);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 26, 0, 0)
        };
        actions.Children.Add(cancel);
        confirm.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(confirm);
        panel.Children.Add(actions);

        var surface = new Border
        {
            CornerRadius = new CornerRadius(M3.DialogRadius),
            Background = UiSkin.Card,
            Padding = new Thickness(26, 24, 22, 20),
            Child = panel,
            Effect = M3.Elevation(3)
        };

        surface.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.94, 0.94);
        surface.RenderTransform = scale;
        surface.Opacity = 0;

        surface.Loaded += (_, _) =>
        {
            surface.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, M3.FastEffects) { EasingFunction = M3.Effects });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
        };

        Dlg.Mount(this, surface);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
            else if (e.Key == Key.Enter) DialogResult = true;
        };

        Loaded += (_, _) =>
        {
            try
            {
                Activate();
            }
            catch
            {
                // 激活失败不影响使用
            }
        };
    }

    /// <summary>问一句，确认返回 true。默认按钮是右侧那个「确认」。</summary>
    public static bool Ask(Window? owner, string title, string message, string confirmText = "确定",
        bool danger = false, string cancelText = "取消")
    {
        try
        {
            var dialog = new ConfirmDialog(title, message, confirmText, cancelText, danger);
            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            return dialog.ShowDialog() == true;
        }
        catch (Exception ex)
        {
            Log.Error("打开确认对话框失败", ex);
            return false;
        }
    }
}

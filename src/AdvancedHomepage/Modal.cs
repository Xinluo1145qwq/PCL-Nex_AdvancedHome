using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace PCLN.AdvancedHomepage;

/// <summary>确认弹窗上的一颗按钮。</summary>
internal sealed class ModalButton
{
    public ModalButton(string text, int value, bool danger = false)
    {
        Text = text;
        Value = value;
        Danger = danger;
    }

    public string Text { get; }
    public int Value { get; }
    public bool Danger { get; }
}

/// <summary>
/// 仿启动器原生样式的模态确认框：整窗压暗 + 居中卡片 + 原生 MyButton。
/// 用独立窗口实现，Owner 设为主窗体，ShowDialog 阻塞到用户点选为止。
/// </summary>
internal static class Modal
{
    /// <summary>返回值：点中的那颗按钮的 <see cref="ModalButton.Value"/>；直接关掉返回 -1。</summary>
    public static int Show(string title, string message, params ModalButton[] buttons)
    {
        Window? host = null;
        int result = -1;

        try
        {
            const int width = 400;

            var titleBlock = M3.Text(title, M3.TypeRole.HeadlineSmall, M3.C.OnSurface, 1, emphasized: true);
            titleBlock.TextWrapping = TextWrapping.Wrap;

            var bodyBlock = M3.Text(message, M3.TypeRole.BodyMedium, M3.C.OnSurfaceVariant);
            bodyBlock.TextWrapping = TextWrapping.Wrap;
            bodyBlock.Margin = new Thickness(0, 16, 0, 24);

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var content = new StackPanel();
            content.Children.Add(titleBlock);
            content.Children.Add(bodyBlock);
            content.Children.Add(buttonRow);

            var card = new Border
            {
                Width = width,
                CornerRadius = new CornerRadius(M3.DialogRadius),
                Background = M3.C.SurfaceHigh,
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = M3.Elevation(3),
                Child = content
            };

            host = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                // M3：模态必须压暗底下的内容（scrim 32%）
                Background = new SolidColorBrush(Color.FromArgb(0x52, 0, 0, 0)),
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = card
            };

            host.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Escape) return;
                result = -1;
                host.Close();
            };

            foreach (var spec in buttons)
            {
                var button = BuildButton(spec, host, v => result = v);
                buttonRow.Children.Add(button);
            }

            if (buttonRow.Children.Count == 0)
            {
                var ok = BuildButton(new ModalButton("好的", 0), host, v => result = v);
                buttonRow.Children.Add(ok);
            }

            FitToOwner(host);

            card.Opacity = 0;
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            var pop = new ScaleTransform(0.94, 0.94);
            card.RenderTransform = pop;
            card.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, M3.FastEffects) { EasingFunction = M3.Effects });
            pop.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
            pop.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });

            host.Loaded += (_, _) =>
            {
                // 透明无边框窗口拿不到前台焦点时，第一次点击会被系统吃掉（只用来激活窗口）。
                // 这里先抢一次焦点，再让第一次点击直接落到按钮上。
                try
                {
                    host.Topmost = true;
                    host.Activate();
                    host.Focus();
                    host.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            host.Topmost = false;
                            host.Activate();
                        }
                        catch
                        {
                            // 已经关掉了
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                catch
                {
                    // 激活失败不影响使用
                }
            };

            Log.Info($"弹出确认框：{title}");
            host.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Error("显示确认框失败", ex);
        }
        finally
        {
            try { host?.Close(); } catch { /* 已经关掉了 */ }
        }

        return result;
    }

    /// <summary>M3 对话框动作：一律文本按钮，危险操作（删除）用 error 色。</summary>
    private static FrameworkElement BuildButton(ModalButton spec, Window host, Action<int> pick)
    {
        var button = M3Button.Create(spec.Text, () =>
        {
            pick(spec.Value);
            host.Close();
        }, M3ButtonKind.Text, danger: spec.Danger);

        button.Margin = new Thickness(8, 0, 0, 0);
        return button;
    }

    /// <summary>把遮罩铺满启动器主窗体。</summary>
    private static void FitToOwner(Window host)
    {
        try
        {
            var owner = PCL.ModMain.frmMain;
            if (owner == null) return;

            host.Owner = owner;

            var scale = 1.0;
            var source = PresentationSource.FromVisual(owner);
            if (source?.CompositionTarget != null) scale = source.CompositionTarget.TransformToDevice.M11;

            var origin = owner.PointToScreen(new Point(0, 0));
            host.Left = origin.X / scale;
            host.Top = origin.Y / scale;
            host.Width = owner.ActualWidth;
            host.Height = owner.ActualHeight;
        }
        catch (Exception ex)
        {
            Log.Warn("确认框定位失败：" + ex.Message);
            host.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            host.Width = 420;
            host.Height = 300;
        }
    }
}

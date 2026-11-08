using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 新手教程：装好插件后的第一次见面礼。
///
/// 只做四步，全部围绕「用户真的动手做一遍」：
///   1. 欢迎弹窗（顺带把反馈 bug 的仓库地址给出去）；
///   2. 提示气泡指着「+」，让用户加一张日历；
///   3. 加完之后指着卡片右下角，说明拖手柄可以改大小；
///   4. 再指着卡片右上角，说明「⚙」里能换背景、调透明度、改字号，结束。
///
/// 气泡用 <see cref="Popup"/> 挂在目标控件上，弹窗尺寸变了也不用自己算坐标。
/// 随时可以从 设置 → 通用 → 新手教程 重新走一遍，或者提前结束。
/// </summary>
internal static class Onboarding
{
    internal const string IssueUrl = "https://github.com/Xinluo1145qwq/PCL-Nex_AdvancedHome/issues";

    private static HomeCanvas? _canvas;
    private static Coach? _coach;
    private static DispatcherTimer? _wait;
    private static DispatcherTimer? _notice;

    /// <summary>教程是否正在进行（设置里据此显示「结束教程」）。</summary>
    public static bool Running { get; private set; }

    /// <summary>主页第一次加载完成：没看过教程就自动开一遍。</summary>
    public static void OnHomeLoaded(HomeCanvas canvas)
    {
        if (Running) return;
        if (Cfg.Ui.TutorialSeen) return;
        if (canvas == null || !canvas.IsVisible) return;

        // 等入场动画（卡片错峰淡入约 600ms）走完再弹，不然气泡会指着还没落位的按钮
        Delay(TimeSpan.FromMilliseconds(900), () => Start(canvas, true));
    }

    /// <summary>开始（或重新开始）教程。<paramref name="withWelcome"/> = 先弹欢迎窗。</summary>
    public static void Start(HomeCanvas canvas, bool withWelcome)
    {
        if (canvas == null) return;

        if (Running) Stop(false);
        _canvas = canvas;

        if (withWelcome && !WelcomeDialog.Ask())
        {
            // 「稍后」只是这次不看：不写 TutorialSeen，下次进主页还会再问一遍
            _canvas = null;
            return;
        }

        Running = true;

        // 教程要加的是日历，只有「花样」主页里有；当前在别的模板就先切过去
        try
        {
            if (!string.Equals(Cfg.CurrentStyle, "fancy", StringComparison.Ordinal))
                canvas.ReloadLayout("fancy");

            canvas.ScrollToTop();
            canvas.SetEdit(true);
        }
        catch (Exception ex)
        {
            Log.Warn("教程进入编辑模式失败：" + ex.Message);
        }

        Delay(TimeSpan.FromMilliseconds(520), () =>
        {
            // 日历已经在了（老用户重看教程）：跳过「加一张」这一步，直接讲怎么调
            if (Cfg.HasCard("fancy", "calendar")) StepResize();
            else StepAddCard();
        });
    }

    /// <summary>设置里点「结束新手教程」。</summary>
    public static void Stop(bool completed = false)
    {
        _coach?.Close();
        _coach = null;
        _wait?.Stop();
        _wait = null;
        Running = false;
        _canvas = null;

        Cfg.Ui.TutorialSeen = true;
        Cfg.Save();

        if (completed) Log.Info("[教程] 已结束");
    }

    /// <summary>主页加了一张卡片（由 HomeCanvas 调用），教程正好在等日历就往下走。</summary>
    public static void OnCardAdded(HomeCanvas canvas, string id)
    {
        if (!Running || canvas != _canvas) return;
        if (!string.Equals(id, "calendar", StringComparison.Ordinal)) return;

        // 卡片刚塞进网格，等一次布局再定位
        Delay(TimeSpan.FromMilliseconds(320), StepResize);
    }

    // ─────────────────────────── 四个步骤 ───────────────────────────

    private static void StepAddCard()
    {
        _canvas?.ScrollToTop();
        var target = _canvas?.TopBarAddButton;
        if (target == null)
        {
            Finish(false);
            return;
        }

        Show(target, "先加一个日历组件试试：点这个「+」，选「日历」。",
            "帮我打开", () =>
            {
                CloseCoach();
                try
                {
                    _canvas?.InvokeAddDialog();
                }
                catch (Exception ex)
                {
                    Log.Warn("教程打开添加面板失败：" + ex.Message);
                }
            }, PlacementMode.Bottom, 10);
    }

    private static void StepResize()
    {
        var host = _canvas?.HostOf("calendar");
        if (host == null || host.ActualWidth <= 0)
        {
            Finish(true);
            return;
        }

        try
        {
            _canvas?.SelectCard("calendar");
        }
        catch (Exception ex)
        {
            Log.Warn("教程选中卡片失败：" + ex.Message);
        }

        Show(host, "按住卡片右下角这个圆点往外拖，就能把日历改大改小。",
            "下一步", StepSettings, PlacementMode.Bottom, 12, -8, alignRight: true);
    }

    private static void StepSettings()
    {
        var host = _canvas?.HostOf("calendar");
        if (host == null)
        {
            Finish(true);
            return;
        }

        Show(host, "选中卡片后，点右上角的「⚙」可以换背景图、调透明度和字号。教程就到这里。",
            "完成", () => Finish(true), PlacementMode.Top, -12, -8, alignRight: true);
    }

    private static void Finish(bool completed)
    {
        var canvas = _canvas;
        CloseCoach();

        if (canvas != null)
        {
            try
            {
                canvas.SetEdit(false);
            }
            catch (Exception ex)
            {
                Log.Warn("教程退出编辑模式失败：" + ex.Message);
            }

            if (completed && canvas.TopBarEditButton is { } edit)
            {
                ShowNotice(edit, "教程结束 · 设置 → 通用 里可以重新看");
            }
        }

        Stop(completed);
    }

    /// <summary>收尾用的一条短提示：没有按钮，3 秒后自己消失。</summary>
    private static void ShowNotice(FrameworkElement target, string text)
    {
        _coach = new Coach(target, text, "", null!, PlacementMode.Top, -10, 0, () => CloseCoach(),
            alignRight: false);

        _notice?.Stop();
        _notice = new DispatcherTimer(TimeSpan.FromMilliseconds(3200), DispatcherPriority.Background,
            (_, _) =>
            {
                _notice?.Stop();
                _notice = null;
                CloseCoach();
            }, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _notice.Start();
    }

    // ─────────────────────────── 气泡 ───────────────────────────

    private static void Show(FrameworkElement target, string text, string action, Action onAction,
        PlacementMode placement, double gapY, double shiftX = 0, bool alignRight = false)
    {
        CloseCoach();
        _coach = new Coach(target, text, action, onAction, placement, gapY, shiftX,
            () => Finish(false), alignRight);
    }

    private static void CloseCoach()
    {
        _coach?.Close();
        _coach = null;
    }

    private static void Delay(TimeSpan wait, Action action)
    {
        _wait?.Stop();
        _wait = new DispatcherTimer(wait, DispatcherPriority.Background, (_, _) =>
        {
            _wait?.Stop();
            _wait = null;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error("新手教程步骤失败", ex);
            }
        }, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);

        _wait.Start();
    }

    /// <summary>一颗挂在控件上的 M3 提示气泡：反色底 + 一句说明 + 一个动作 + 「跳过」。</summary>
    private sealed class Coach
    {
        private readonly Popup _popup;
        private readonly Border _card;
        private readonly FrameworkElement _target;
        private readonly double _gap;
        private readonly double _shift;
        private readonly bool _alignRight;

        public Coach(FrameworkElement target, string text, string action, Action onAction,
            PlacementMode placement, double gapY, double shiftX, Action onSkip, bool alignRight = false)
        {
            _target = target;
            _gap = gapY;
            _shift = shiftX;
            _alignRight = alignRight;

            var scheme = M3.C;

            var body = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                LineHeight = 19,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 268,
                Foreground = scheme.InverseOnSurface
            };

            var primary = new Border
            {
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = action,
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = scheme.InversePrimary
                }
            };
            primary.MouseEnter += (_, _) => primary.Background = M3.Layer(
                M3.Of(scheme.InverseSurface), M3.Of(scheme.InverseOnSurface), M3.HoverAlpha);
            primary.MouseLeave += (_, _) => primary.Background = Brushes.Transparent;
            primary.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onAction();
            };

            var skip = new Border
            {
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "跳过",
                    FontSize = 12.5,
                    Foreground = M3.Alpha(scheme.InverseOnSurface, 0.7)
                }
            };
            skip.MouseEnter += (_, _) => skip.Background = M3.Layer(
                M3.Of(scheme.InverseSurface), M3.Of(scheme.InverseOnSurface), 0.08);
            skip.MouseLeave += (_, _) => skip.Background = Brushes.Transparent;
            skip.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onSkip();
            };

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, -4, -4)
            };
            actions.Children.Add(skip);
            if (!string.IsNullOrEmpty(action)) actions.Children.Add(primary);

            var stack = new StackPanel();
            stack.Children.Add(body);
            stack.Children.Add(actions);

            _card = new Border
            {
                Background = scheme.InverseSurface,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 12, 12, 10),
                Child = stack,
                Effect = M3.Elevation(2),
                Opacity = 0
            };

            _popup = new Popup
            {
                Child = _card,
                PlacementTarget = target,
                Placement = placement,
                StaysOpen = true,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                VerticalOffset = gapY,
                HorizontalOffset = shiftX
            };

            _card.SizeChanged += (_, _) => Center();
            _popup.Opened += (_, _) =>
            {
                Center();
                _card.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1, TimeSpan.FromMilliseconds(160))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });
            };

            _popup.IsOpen = true;
        }

        /// <summary>气泡横向对准目标中心；越界时往屏幕里收一点。</summary>
        private void Center()
        {
            double width = _card.ActualWidth;
            if (width <= 0) return;

            double offset = _alignRight
                ? _target.ActualWidth - width + _shift
                : (_target.ActualWidth - width) / 2 + _shift;

            try
            {
                var origin = _target.PointToScreen(new Point(0, 0));
                double screenWidth = SystemParameters.WorkArea.Width;
                // Popup 的偏移是相对目标的，这里换算出「绝对左边」再夹回屏幕
                double left = origin.X + offset;
                if (left < 8) offset += 8 - left;
                else if (left + width > screenWidth - 8) offset -= left + width - (screenWidth - 8);
            }
            catch
            {
                // 拿不到屏幕坐标就按居中放
            }

            _popup.HorizontalOffset = offset;
        }

        public void Close()
        {
            try
            {
                _popup.IsOpen = false;
                _popup.Child = null;
            }
            catch
            {
                // 关不掉也不影响后续
            }
        }
    }

    /// <summary>第一次装插件时弹的那颗 M3 对话框。</summary>
    private sealed class WelcomeDialog : Window
    {
        private WelcomeDialog()
        {
            Dlg.Setup(this, "欢迎使用高级主页", 520, scrim: true);
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Width = 440 };

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(M3.Icon(
                "M12,3.4 L14.2,9 L20.2,9.4 L15.6,13.2 L17,19 L12,15.7 L7,19 L8.4,13.2 L3.8,9.4 L9.8,9 Z",
                M3.C.Primary, 22));
            titleRow.Children.Add(new TextBlock
            {
                Text = "欢迎使用高级主页",
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = M3.C.OnSurface,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(titleRow);

            panel.Children.Add(new TextBlock
            {
                Text = "这是一个把原版主页换成可拖拽卡片画布的开源插件：卡片能拖动、能缩放，配色和组件都随你改。",
                FontSize = 13,
                LineHeight = 21,
                TextWrapping = TextWrapping.Wrap,
                Foreground = M3.C.OnSurfaceVariant,
                Margin = new Thickness(0, 12, 0, 0)
            });

            var issue = new TextBlock
            {
                Text = IssueUrl,
                FontSize = 13,
                LineHeight = 21,
                TextWrapping = TextWrapping.Wrap,
                Foreground = M3.C.Primary,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 0)
            };
            issue.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenIssue();
            };

            var tip = new TextBlock
            {
                Text = "遇到 bug 可以到这里反馈：",
                FontSize = 13,
                LineHeight = 21,
                Foreground = M3.C.OnSurfaceVariant,
                Margin = new Thickness(0, 12, 0, 0)
            };

            panel.Children.Add(tip);
            panel.Children.Add(issue);

            var later = M3Button.Create("稍后", () => DialogResult = false, M3ButtonKind.Text);
            var start = M3Button.Create("开始教程", () => DialogResult = true, M3ButtonKind.Filled);
            start.Margin = new Thickness(8, 0, 0, 0);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 24, 0, 0)
            };
            actions.Children.Add(later);
            actions.Children.Add(start);
            panel.Children.Add(actions);

            Dlg.Mount(this, Dlg.Shell(panel, new Thickness(26, 24, 22, 18)));

            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) DialogResult = false;
                else if (e.Key == Key.Enter) DialogResult = true;
            };
        }

        private void OpenIssue()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(IssueUrl)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Warn("打开 issues 失败：" + ex.Message);
            }
        }

        public static bool Ask()
        {
            try
            {
                return new WelcomeDialog().ShowDialog() == true;
            }
            catch (Exception ex)
            {
                Log.Error("打开欢迎弹窗失败", ex);
                return false;
            }
        }
    }
}

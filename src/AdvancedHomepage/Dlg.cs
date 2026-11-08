using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// M3 对话框外壳：28dp 圆角、surface container high 底、24dp 内边距、底部右对齐的动作区。
/// 所有设置/选择弹窗都走这里，保证「一个产品只有一种弹窗」。
/// </summary>
internal static class Dlg
{
    // 兼容老调用点的语义化画刷，全部落到 M3 角色
    public static Brush Border => M3.C.OutlineVariant;
    public static Brush SoftBorder => M3.C.OutlineVariant;
    public static Brush SubtleFill => M3.C.SurfaceContainer;
    public static Brush Hover => M3.C.SurfaceHighest;
    public static Brush Surface => M3.C.SurfaceHigh;

    /// <summary>打在窗口 Tag 上的标记：这个窗口是「铺满启动器 + scrim」的模态层。</summary>
    private const string ScrimTag = "advhome.scrim";

    /// <summary>
    /// 无边框 + 跟随启动器字体。
    /// <paramref name="scrim"/> = true 时窗口铺满启动器并压一层 32% 黑，内容居中——
    /// 这是 M3 的 modal：底下的界面必须被压暗，读者才知道「现在在操作这个弹窗」。
    /// </summary>
    public static void Setup(Window window, string title, double width, bool scrim = false)
    {
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = true;
        window.Background = scrim
            ? new SolidColorBrush(Color.FromArgb(0x59, 0, 0, 0))
            : Brushes.Transparent;
        window.ResizeMode = ResizeMode.NoResize;
        window.ShowInTaskbar = false;
        window.SizeToContent = scrim ? SizeToContent.Manual : SizeToContent.Height;
        window.Width = width;
        window.Title = title;

        try
        {
            window.Owner = PCL.ModMain.frmMain ?? Application.Current?.MainWindow;
            if (window.Owner != null)
            {
                window.WindowStartupLocation = scrim ? WindowStartupLocation.Manual : WindowStartupLocation.CenterOwner;
                window.FontFamily = window.Owner.FontFamily;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("设置对话框 Owner 失败：" + ex.Message);
        }

        if (scrim)
        {
            window.Tag = ScrimTag;
            FitToOwner(window);

            // 黑幕淡入，别一开窗就「啪」地压暗
            window.Loaded += (_, _) =>
            {
                if (window.Background is not SolidColorBrush brush || brush.IsFrozen) return;
                brush.BeginAnimation(SolidColorBrush.ColorProperty,
                    new ColorAnimation(Color.FromArgb(0x59, 0, 0, 0), M3.DefaultEffects)
                    {
                        EasingFunction = M3.Effects
                    });
            };
        }

        window.Loaded += (_, _) =>
        {
            try
            {
                window.Activate();
            }
            catch
            {
                // 激活失败不影响使用
            }
        };
    }

    /// <summary>M3 basic dialog 容器：28dp 圆角 + 24dp padding + 3 级阴影。</summary>
    public static Border Shell(FrameworkElement content, Thickness? padding = null)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(M3.DialogRadius),
            Background = M3.C.SurfaceHigh,
            BorderThickness = new Thickness(0),
            Padding = padding ?? new Thickness(24),
            Margin = new Thickness(24),
            Child = content,
            Effect = M3.Elevation(3)
        };

        // 内容要贴边（比如左侧导航栏铺满整条边）时，圆角得靠裁剪切出来
        border.SizeChanged += (_, _) =>
        {
            if (border.ActualWidth <= 0 || border.ActualHeight <= 0) return;
            border.Clip = new RectangleGeometry(
                new Rect(0, 0, border.ActualWidth, border.ActualHeight), M3.DialogRadius, M3.DialogRadius);
        };

        // 入场：从 92% 缩放 + 8dp 下沉，用 Expressive 空间弹簧
        border.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.94, 0.94);
        border.RenderTransform = scale;
        border.Opacity = 0;

        border.Loaded += (_, _) =>
        {
            border.BeginAnimation(UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, M3.FastEffects)
                {
                    EasingFunction = M3.Effects
                });
            border.BeginAnimation(UIElement.RenderTransformProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, M3.FastSpatial) { EasingFunction = M3.Spatial });
        };

        return border;
    }

    /// <summary>
    /// 把内容挂到窗口上。scrim 模态层要把卡片居中浮在黑幕上，
    /// 普通浮层窗口则让内容自己决定高度。
    /// </summary>
    public static void Mount(Window window, FrameworkElement card)
    {
        if (window.Tag as string != ScrimTag)
        {
            window.Content = card;
            return;
        }

        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        card.Margin = new Thickness(24);

        var grid = new Grid { Background = Brushes.Transparent };
        grid.Children.Add(card);
        window.Content = grid;
    }

    /// <summary>把窗口铺满启动器主窗体（scrim 层要盖住整个界面）。</summary>
    private static void FitToOwner(Window window)
    {
        try
        {
            var owner = window.Owner;
            if (owner == null) return;

            double scale = 1.0;
            var source = PresentationSource.FromVisual(owner);
            if (source?.CompositionTarget != null) scale = source.CompositionTarget.TransformToDevice.M11;

            var origin = owner.PointToScreen(new Point(0, 0));
            window.Left = origin.X / scale;
            window.Top = origin.Y / scale;
            window.Width = owner.ActualWidth;
            window.Height = owner.ActualHeight;
        }
        catch (Exception ex)
        {
            Log.Warn("模态层定位失败：" + ex.Message);
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Width = 900;
            window.Height = 640;
        }
    }

    /// <summary>标题区：headline small（emphasized）+ 右上角关闭图标按钮。</summary>
    public static FrameworkElement Header(string title, Action onClose, string subtitle = "")
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();

        var headline = M3.Text(title, M3.TypeRole.HeadlineSmall, M3.C.OnSurface, 1, emphasized: true);
        headline.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(headline);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var sub = M3.Text(subtitle, M3.TypeRole.BodyMedium, M3.C.OnSurfaceVariant);
            sub.TextWrapping = TextWrapping.Wrap;
            sub.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(sub);
        }

        grid.Children.Add(stack);

        var close = CloseButton(onClose);
        Grid.SetColumn(close, 1);
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(8, -4, -8, 0);
        grid.Children.Add(close);

        return grid;
    }

    public static FrameworkElement CloseButton(Action onClose)
    {
        return M3IconButton.Create("M6,6 L18,18 M18,6 L6,18", onClose, "关闭");
    }

    public static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.Medium,
        Foreground = M3.C.OnSurfaceVariant
    };

    /// <summary>主按钮 = filled，次按钮 = text；第三个及以后请用 text。</summary>
    public static FrameworkElement Button(string text, bool primary, Action onClick, bool enabled = true)
    {
        var button = M3Button.Create(text, onClick,
            primary ? M3ButtonKind.Filled : M3ButtonKind.Text, enabled);
        button.Margin = new Thickness(8, 0, 0, 0);
        return button;
    }

    /// <summary>设置面板里的小按钮：32dp 高、tonal。</summary>
    public static FrameworkElement SmallButton(string text, Action onClick, bool enabled = true)
    {
        double height = 32;
        var button = new Border
        {
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = M3.C.SurfaceContainerHighest,
            Padding = new Thickness(12, 0, 12, 0),
            Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
            VerticalAlignment = VerticalAlignment.Center,
            Child = M3.Text(text, M3.TypeRole.LabelMedium,
                enabled ? M3.C.OnSurface : M3.Layer(M3.Of(M3.C.OnSurface), M3.Of(M3.C.Surface), 0.38), 1, true)
        };

        if (enabled)
        {
            var hover = M3.Layer(M3.Of(M3.C.SurfaceContainerHighest), M3.Of(M3.C.OnSurface), M3.HoverAlpha);
            button.MouseEnter += (_, _) => button.Background = hover;
            button.MouseLeave += (_, _) => button.Background = M3.C.SurfaceContainerHighest;
            button.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
        }

        return button;
    }

    /// <summary>小块底座：surface container + 16dp 圆角（M3 里 tile 的常规形状）。</summary>
    public static Border Tile(FrameworkElement child, double size, double radius)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(radius <= 0 ? M3.CornerM : radius),
            Background = M3.C.SurfaceContainer,
            BorderThickness = new Thickness(0),
            Child = child,
            ClipToBounds = true
        };
    }
}

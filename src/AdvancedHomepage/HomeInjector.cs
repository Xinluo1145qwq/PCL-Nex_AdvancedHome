using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PCLN.AdvancedHomepage;

/// <summary>把 HomeCanvas 挂到启动器主页的 PanCustom 上。</summary>
internal static class HomeInjector
{
    private static readonly Dictionary<object, Visibility> HintStates = new();
    private static bool _loggedTree;

    /// <summary>当前挂在主页上的画布，设置窗口要拿它做即时预览（透明度滑块）。</summary>
    public static HomeCanvas? Active { get; private set; }

    public static bool TryAttach(object page)
    {
        if (page == null) return false;
        if (!Cfg.Enabled) return false;

        var pan = Reflect.Field(page, "PanCustom") as Panel;
        if (pan == null)
        {
            Log.Warn("主页结构变化：未找到 PanCustom 字段，跳过接管");
            return false;
        }

        LogTreeOnce(pan);

        var existing = pan.Children.OfType<HomeCanvas>().FirstOrDefault();
        if (existing != null)
        {
            Active = existing;
            existing.Refresh();
            HideHint(page);
            return true;
        }

        pan.Children.Clear();
        var canvas = new HomeCanvas();
        Active = canvas;
        pan.Children.Add(canvas);
        HideHint(page);

        Log.Info($"已接管主页：画布宽 {canvas.ActualWidth:0.#}，容器 {pan.GetType().Name}");
        return true;
    }

    /// <summary>关闭插件后把原主页的提示卡片恢复。</summary>
    public static void RestoreHint(object page)
    {
        if (page == null) return;
        if (!HintStates.TryGetValue(page, out var state)) return;
        if (Reflect.Field(page, "PanHint") is UIElement hint) hint.Visibility = state;
        HintStates.Remove(page);
    }

    private static void HideHint(object page)
    {
        if (HintStates.ContainsKey(page)) return;
        if (Reflect.Field(page, "PanHint") is not UIElement hint) return;
        HintStates[page] = hint.Visibility;
        hint.Visibility = Visibility.Collapsed;
    }

    /// <summary>启用状态切换后让主页立即重建。</summary>
    public static void RequestHomeRefresh()
    {
        try
        {
            var page = PCL.ModMain.frmLaunchRight;
            if (page == null)
            {
                Log.Warn("主页实例尚未创建，跳过即时刷新");
                return;
            }

            page.ForceRefresh();
            Log.Info("已请求主页刷新");
        }
        catch (Exception ex)
        {
            Log.Error("请求主页刷新失败", ex);
        }
    }

    private static void LogTreeOnce(Panel pan)
    {
        if (_loggedTree) return;
        _loggedTree = true;
        try
        {
            var chain = new List<string>();
            DependencyObject node = pan;
            for (int i = 0; i < 6 && node != null; i++)
            {
                chain.Add(node.GetType().Name);
                node = System.Windows.Media.VisualTreeHelper.GetParent(node);
            }

            Log.Info("PanCustom 视觉链：" + string.Join(" <- ", chain)
                     + $"；对齐={pan.HorizontalAlignment}；宽={pan.ActualWidth:0.#}");
        }
        catch (Exception ex)
        {
            Log.Error("输出主页视觉链失败", ex);
        }
    }
}

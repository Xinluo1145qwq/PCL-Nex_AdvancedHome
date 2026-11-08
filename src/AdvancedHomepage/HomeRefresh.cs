using System;
using System.Collections.Generic;
using System.Windows;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 数据变更 → 主页刷新 的统一通道。
///
/// 只向 <see cref="Cfg.Changed"/> 订阅一次，画布以弱引用登记。
/// 这样页面在「启动游戏」「切换设置」等过程中被卸载再加载时，不会因为
/// 元素级的 Unloaded 退订而永久收不到通知（之前的 bug 就是这样丢的）。
/// </summary>
internal static class HomeRefresh
{
    private static readonly object Gate = new();
    private static readonly List<WeakReference<HomeCanvas>> Live = new();
    private static bool _hooked;

    public static void Register(HomeCanvas canvas)
    {
        if (canvas == null) return;

        lock (Gate)
        {
            if (!_hooked)
            {
                _hooked = true;
                Cfg.Changed += Notify;
            }

            Prune();
            Live.Add(new WeakReference<HomeCanvas>(canvas));
        }
    }

    /// <summary>当前活着的那个主页画布（设置里「重新开始教程」要用它）。</summary>
    public static HomeCanvas? Current
    {
        get
        {
            lock (Gate)
            {
                Prune();
                foreach (var weak in Live)
                {
                    if (weak.TryGetTarget(out var canvas)) return canvas;
                }
            }

            return null;
        }
    }

    /// <summary>主动广播一次（例如插件刚接管主页时）。</summary>
    public static void Notify()
    {
        List<HomeCanvas> targets = new();
        lock (Gate)
        {
            Prune();
            foreach (var weak in Live)
            {
                if (weak.TryGetTarget(out var canvas)) targets.Add(canvas);
            }
        }

        foreach (var canvas in targets)
        {
            try
            {
                canvas.OnDataChanged();
            }
            catch (Exception ex)
            {
                Log.Error("刷新主页失败", ex);
            }
        }
    }

    /// <summary>必须在持锁时调用。</summary>
    private static void Prune() => Live.RemoveAll(w => !w.TryGetTarget(out _));
}

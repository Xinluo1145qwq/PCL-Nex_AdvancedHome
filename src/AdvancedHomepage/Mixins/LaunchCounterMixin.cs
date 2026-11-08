using System;
using PCL.Mixin;

namespace PCLN.AdvancedHomepage.Mixins;

/// <summary>每启动一次游戏，给当天计数 +1（挂在真正创建进程的位置）。</summary>
[Mixin("PCL.ModLaunch")]
internal static class LaunchCounterMixin
{
    [Inject("McLaunchRun", At = MixinAt.Head, Priority = 1200)]
    private static void BeforeLaunch()
    {
        try
        {
            if (!Cfg.Enabled) return;
            Cfg.AddLaunch(DateTime.Today);
            Log.Info("记录一次游戏启动，今天累计 " + Cfg.TodayCount + " 次");
        }
        catch (Exception ex)
        {
            Log.Error("记录启动次数失败", ex);
        }
    }
}

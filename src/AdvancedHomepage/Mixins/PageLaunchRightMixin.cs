using System;
using PCL.Mixin;

namespace PCLN.AdvancedHomepage.Mixins;

/// <summary>接管主页内容：启用时跳过原版 LoadContent，改用自定义网格。</summary>
[Mixin("PCL.PageLaunchRight")]
internal static class PageLaunchRightMixin
{
    [Inject("LoadContent", At = MixinAt.Head, Cancellable = true, Priority = 1500)]
    private static void BeforeLoadContent([This] object self, CallbackInfo callback)
    {
        try
        {
            if (Cfg.Enabled && HomeInjector.TryAttach(self))
            {
                callback.Cancel();
                return;
            }

            HomeInjector.RestoreHint(self);
        }
        catch (Exception ex)
        {
            Log.Error("接管主页时出错，回退原版主页", ex);
        }
    }
}

using System;
using PCL.Mixin;

namespace PCLN.AdvancedHomepage.Mixins;

/// <summary>在设置页「启动器选项」插入高级主页配置卡片。</summary>
[Mixin("PCL.PageSetupUI")]
internal static class PageSetupUIMixin
{
    [Inject("Reload", At = MixinAt.Return, Priority = 1200)]
    private static void AfterReload([This] object self) => SafeInject(self);

    [Inject("PageSetupUI_Loaded", At = MixinAt.Return, Priority = 1200)]
    private static void AfterLoaded([This] object self) => SafeInject(self);

    private static void SafeInject(object self)
    {
        try
        {
            SettingsPanel.EnsureInjected(self);
        }
        catch (Exception ex)
        {
            Log.Error("插入设置卡片失败", ex);
        }
    }
}

using System;
using System.Linq;
using PCL.Mixin;

namespace PCLN.AdvancedHomepage.Mixins;

/// <summary>启动器创建完成后做一次初始化与自检。</summary>
[Mixin("PCL.FormMain")]
internal static class FormMainMixin
{
    [Inject(".ctor", At = MixinAt.Return, Priority = int.MaxValue)]
    private static void AfterCreated([This] object self)
    {
        try
        {
            Log.Info($"========== 高级主页 v{typeof(FormMainMixin).Assembly.GetName().Version?.ToString(3) ?? "?"} 已装载 ==========");
            Log.Info("配置目录：" + Paths.DataDir);

            var layouts = string.Join("、", Cfg.Data.Layouts.Select(kv => $"{kv.Key} {kv.Value.Count} 个"));
            Log.Info($"启用={Cfg.Enabled}；样式={Cfg.CurrentStyle}；布局=[{layouts}]；历史天数={Cfg.Data.History.Count}；累计={Cfg.TotalCount}");

            Palette.DumpKeysOnce();

        }
        catch (Exception ex)
        {
            Log.Error("插件初始化失败", ex);
        }
    }
}

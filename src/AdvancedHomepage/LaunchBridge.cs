using System;
using System.Collections.Generic;
using System.Linq;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 从主页卡片启动游戏。走的是启动器自己的入口 <c>ModLaunch.McLaunchStart</c>，
/// 因此 Java 检查、登录、内存设置等流程和「开始游戏」按钮完全一致。
/// 这一切必须在 UI 线程调用。
/// </summary>
internal static class LaunchBridge
{
    /// <summary>启动某个实例；<paramref name="world"/> 非空时直接进入该存档。</summary>
    public static bool Launch(string instanceName, string world = null)
    {
        try
        {
            var instance = ResolveInstance(instanceName);
            if (instance == null && string.IsNullOrWhiteSpace(world))
            {
                Log.Warn($"启动失败：找不到实例 {instanceName}");
                return false;
            }

            var options = new PCL.ModLaunch.McLaunchOptions
            {
                instance = instance,
                WorldName = string.IsNullOrWhiteSpace(world) ? null : world
            };

            Log.Info(string.IsNullOrWhiteSpace(world)
                ? $"主页请求启动实例：{instanceName}"
                : $"主页请求启动存档：{world}（实例 {instanceName}）");

            // 启动器内部会把错误用 HintService 提示出来，这里只负责发起
            PCL.ModLaunch.McLaunchStart(options);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"启动 {instanceName} 时出错", ex);
            return false;
        }
    }

    /// <summary>加入某个服务器：走启动器自己的启动流程，只是带上服务器地址。</summary>
    public static bool LaunchServer(string instanceName, string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            Log.Warn("启动失败：服务器地址为空");
            return false;
        }

        try
        {
            var instance = ResolveInstance(instanceName);

            var options = new PCL.ModLaunch.McLaunchOptions
            {
                instance = instance,
                ServerIp = ip
            };

            Log.Info($"主页请求加入服务器：{ip}（实例 {instanceName}）");
            PCL.ModLaunch.McLaunchStart(options);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"加入服务器 {ip} 时出错", ex);
            return false;
        }
    }

    /// <summary>在启动器的实例缓存里按名字找实例对象；找不到就新建一个交给启动器去加载。</summary>
    private static PCL.McInstance? ResolveInstance(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        try
        {
            // mcInstanceList 是「卡片分类 -> 该分类下的实例列表」
            var groups = PCL.ModInstanceList.mcInstanceList;
            if (groups != null && groups.Count > 0)
            {
                foreach (var group in groups.Values)
                {
                    if (group == null) continue;
                    foreach (var item in group)
                    {
                        if (item == null) continue;
                        if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) return item;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("读取实例缓存失败：" + ex.Message);
        }

        try
        {
            // 当前选中的实例名字一致时直接复用，省一次 Load
            var selected = PCL.ModInstanceList.McMcInstanceSelected;
            if (selected != null && string.Equals(selected.Name, name, StringComparison.OrdinalIgnoreCase))
                return selected;
        }
        catch
        {
            // 忽略：继续走新建
        }

        try
        {
            return new PCL.McInstance(name);
        }
        catch (Exception ex)
        {
            Log.Warn("构造实例对象失败：" + ex.Message);
            return null;
        }
    }
}

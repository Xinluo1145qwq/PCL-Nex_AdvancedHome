using System;
using System.IO;

namespace PCLN.AdvancedHomepage;

internal static class Paths
{
    private static string _dataDir;
    private static string _root;

    /// <summary>启动器数据目录（PCL 文件夹本体）。</summary>
    public static string Root
    {
        get
        {
            if (_root != null) return _root;
            string root = null;
            try
            {
                root = PCL.ModBase.pathPCLData;
            }
            catch
            {
                // 启动早期 ModBase 可能尚未初始化
            }

            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(AppContext.BaseDirectory, "PCL");

            _root = root;
            return _root;
        }
    }

    /// <summary>插件数据目录：&lt;PCL 数据目录&gt;/AdvancedHomepage</summary>
    public static string DataDir
    {
        get
        {
            if (_dataDir != null) return _dataDir;
            var dir = Path.Combine(Root, "AdvancedHomepage");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                dir = AppContext.BaseDirectory;
            }

            _dataDir = dir;
            return _dataDir;
        }
    }

    public static string ConfigFile => Path.Combine(DataDir, "config.json");
}

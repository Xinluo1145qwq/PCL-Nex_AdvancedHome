using System;
using System.Globalization;
using System.IO;

namespace PCLN.AdvancedHomepage;

/// <summary>自定义下载用的小工具：默认目录、地址整理、文件名猜测、体积格式化。</summary>
internal static class Downloads
{
    /// <summary>默认保存到 PCL 数据目录下的 MyDownload，和启动器自己的惯例一致。</summary>
    public static string DefaultFolder
    {
        get
        {
            try
            {
                var root = Path.GetDirectoryName(Paths.DataDir);
                if (!string.IsNullOrWhiteSpace(root)) return Path.Combine(root, "MyDownload");
            }
            catch
            {
                // 取不到就退回插件目录
            }

            return Path.Combine(Paths.DataDir, "MyDownload");
        }
    }

    public static string NormalizeUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var url = text.Trim();
        return url.Contains("://", StringComparison.Ordinal) ? url : "https://" + url;
    }

    /// <summary>从地址里猜一个文件名，猜不出来就叫 download.bin。</summary>
    public static string FileNameFrom(string url)
    {
        try
        {
            var uri = new Uri(NormalizeUrl(url));
            var name = Path.GetFileName(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(name)) return Uri.UnescapeDataString(name);
        }
        catch
        {
            // 地址不合法时走兜底
        }

        return "download.bin";
    }

    public static string HumanSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Format(CultureInfo.InvariantCulture, "{0:0} {1}", value, units[unit])
            : string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", value, units[unit]);
    }
}

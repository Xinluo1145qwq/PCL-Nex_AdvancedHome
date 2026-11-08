using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace PCLN.AdvancedHomepage;

/// <summary>网站的地址整理、图标加载与打开。</summary>
internal static class Sites
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        try
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PCL-AdvancedHomepage/1.0");
        }
        catch
        {
            // UA 加不上不影响下载
        }

        return client;
    }

    /// <summary>用户常常只写 example.com，这里补上协议。</summary>
    public static string Normalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        var text = url.Trim();
        if (text.Contains("://", StringComparison.Ordinal)) return text;
        return "https://" + text;
    }

    /// <summary>把地址压成一行可读的「域名」。</summary>
    public static string Host(string url)
    {
        var text = Normalize(url);
        if (string.IsNullOrEmpty(text)) return "";

        try
        {
            var uri = new Uri(text);
            var host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) host = host.Substring(4);
            return uri.IsDefaultPort ? host : $"{host}:{uri.Port}";
        }
        catch
        {
            return text;
        }
    }

    /// <summary>用系统默认浏览器打开。</summary>
    public static void Open(string url)
    {
        var text = Normalize(url);
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            Process.Start(new ProcessStartInfo(text) { UseShellExecute = true });
            Log.Info($"打开网站：{text}");
        }
        catch (Exception ex)
        {
            Log.Error($"打开网站失败（{text}）", ex);
        }
    }

    /// <summary>
    /// 图标既可以是本地图片，也可以是 http(s) 图片地址。
    /// 远程图片下载一次后缓存到插件目录，避免每次开主页都重新拉。
    /// </summary>
    public static BitmapSource? LoadIcon(string source, int decodeWidth = 112)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        var text = source.Trim();
        if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return ImageUtil.Load(text, decodeWidth);
        }

        try
        {
            var cache = CacheFile(text);
            if (!File.Exists(cache) || new FileInfo(cache).Length == 0)
            {
                using var response = Client.GetAsync(text).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Log.Warn($"下载网站图标失败（{text}）：HTTP {(int)response.StatusCode}");
                    return null;
                }

                var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (bytes.Length == 0) return null;

                Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
                File.WriteAllBytes(cache, bytes);
            }

            return ImageUtil.Load(cache, decodeWidth);
        }
        catch (Exception ex)
        {
            Log.Warn($"读取网站图标失败（{text}）：{ex.Message}");
            return null;
        }
    }

    private static string CacheFile(string url)
    {
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
        var name = Convert.ToHexString(hash).ToLower(CultureInfo.InvariantCulture).Substring(0, 16);
        return Path.Combine(Paths.DataDir, "site-icons", name + ".img");
    }
}

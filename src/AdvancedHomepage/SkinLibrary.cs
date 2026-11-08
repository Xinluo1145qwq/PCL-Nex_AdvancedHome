using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 皮肤贴图来源：内置 Steve / Alex（用代码画出来，不依赖外部资源）、本地图片、正版玩家名（走 Mojang API）。
/// 一律返回 64×64 的贴图，老版 64×32 会在使用时做镜像。
/// </summary>
internal static class SkinLibrary
{
    private static readonly Dictionary<string, BitmapSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>正版皮肤的手臂模型（classic / slim），键和 <see cref="Cache"/> 一致。</summary>
    private static readonly Dictionary<string, string> Models = new(StringComparer.OrdinalIgnoreCase);

    private static readonly object Gate = new();

    public static string CacheDir
    {
        get
        {
            var dir = Path.Combine(Paths.DataDir, "skins");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                // 建不了目录就用数据目录本身
                dir = Paths.DataDir;
            }

            return dir;
        }
    }

    /// <summary>按配置取贴图；取不到时给 Steve。</summary>
    public static BitmapSource Load(SkinOptions skin)
    {
        string key;
        switch (skin.Source)
        {
            case "file":
                key = "file:" + skin.Value;
                break;
            case "player":
                key = "player:" + skin.Value.ToLowerInvariant();
                break;
            default:
                key = "builtin:" + (string.Equals(skin.Value, "alex", StringComparison.OrdinalIgnoreCase) ? "alex" : "steve");
                break;
        }

        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
        }

        BitmapSource? image = key switch
        {
            _ when key.StartsWith("builtin:", StringComparison.Ordinal) => key.EndsWith("alex", StringComparison.Ordinal)
                ? PaintAlex()
                : PaintSteve(),
            _ when key.StartsWith("file:", StringComparison.Ordinal) => LoadFile(key.Substring(5)),
            _ => LoadCachedPlayer(key.Substring(7))
        };

        // 正版皮肤没缓存时，这个 Steve 只是临时兜底，不能写进 Cache：
        // 否则后台「有没有缓存」的判断会以为已经拿到了，补拉永远不会触发。
        if (image == null) return PaintSteve();

        lock (Gate) Cache[key] = image;
        return image;
    }

    /// <summary>这个玩家名有没有可用的缓存贴图（内存或磁盘）。</summary>
    public static bool HasCache(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        lock (Gate)
        {
            if (Cache.ContainsKey("player:" + name.ToLowerInvariant())) return true;
        }

        try
        {
            return File.Exists(Path.Combine(CacheDir, name.ToLowerInvariant() + ".png"));
        }
        catch
        {
            return false;
        }
    }

    public static void Invalidate(string key)
    {
        lock (Gate)
        {
            if (key != null) Cache.Remove(key);
        }
    }

    /// <summary>拉取结果：贴图 + 手臂模型（classic / slim）。</summary>
    public sealed class SkinFetch
    {
        public BitmapSource Image = null!;
        public string Model = "classic";
        public string Name = "";
    }

    /// <summary>
    /// 从正版账号拉皮肤。国内网络下 Mojang 经常连不上，所以分层回退：
    /// 官方 profile（重试一次）→ playerdb（名字换 uuid + 贴图元数据）→ sessionserver
    /// → 贴图直链 → Crafatar → Minotar → mc-heads。任何一层拿到就返回。
    /// </summary>
    public static async Task<SkinFetch> FetchPlayerAsync(string input)
    {
        input = (input ?? "").Trim();
        if (input.Length == 0) throw new ArgumentException("玩家名不能为空");

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        http.DefaultRequestHeaders.UserAgent.ParseAdd("PCLN-AdvancedHomepage/1.6 (+https://github.com)");

        string uuid = "";
        string name = input;
        string textureValue = "";
        string model = "";
        bool sawProfile = false;

        if (IsUuid(input))
        {
            uuid = input.Replace("-", "");
            sawProfile = true;
        }
        else
        {
            // 官方接口偶尔抽风，失败就再来一次
            for (int attempt = 0; attempt < 2 && uuid.Length == 0; attempt++)
            {
                var profile = await GetJsonAsync(http,
                    "https://api.mojang.com/users/profiles/minecraft/" + Uri.EscapeDataString(input));

                if (profile == null) continue;

                uuid = ReadUuid(profile) ?? "";
                name = ReadName(profile) ?? input;
                sawProfile = uuid.Length > 0;
            }

            // 官方查不到（被墙 / 限流）就用 playerdb，它把 uuid 和贴图元数据一起给了
            if (uuid.Length == 0)
            {
                var mirror = await GetJsonAsync(http,
                    "https://playerdb.co/api/player/minecraft/" + Uri.EscapeDataString(input));

                if (mirror != null)
                {
                    var (dbUuid, dbName, dbValue, dbModel) = ReadPlayerDb(mirror);
                    uuid = dbUuid;
                    if (dbName.Length > 0) name = dbName;
                    textureValue = dbValue;
                    model = dbModel;
                    sawProfile = uuid.Length > 0;
                }
            }
        }

        if (uuid.Length > 0 && textureValue.Length == 0)
        {
            var session = await GetJsonAsync(http,
                "https://sessionserver.mojang.com/session/minecraft/profile/" + uuid);

            if (session != null)
            {
                var (value, sessionModel) = ReadTextures(session);
                textureValue = value ?? "";
                model = sessionModel;
                sawProfile = true;
            }
        }

        byte[]? bytes = null;

        // ① 官方贴图直链
        if (textureValue.Length > 0)
        {
            string? url = ReadSkinUrl(textureValue);
            if (!string.IsNullOrEmpty(url)) bytes = await GetBytesAsync(http, url);
        }

        // ② Crafatar：有 uuid 就能出
        if (bytes == null && uuid.Length > 0)
            bytes = await GetBytesAsync(http, "https://crafatar.com/skins/" + uuid);

        // ③ Minotar：只要名字
        if (bytes == null)
            bytes = await GetBytesAsync(http, "https://minotar.net/skin/" + Uri.EscapeDataString(name));

        // ④ mc-heads：最后的备胎
        if (bytes == null)
            bytes = await GetBytesAsync(http, "https://mc-heads.net/skin/" + Uri.EscapeDataString(name));

        if (bytes == null || bytes.Length < 64)
        {
            throw new InvalidOperationException(sawProfile
                ? "查到了玩家，但皮肤下载失败（贴图服务器连不上），换网再试"
                : "找不到这个正版玩家：名字拼写对吗？也可能是这台机器连不上 Mojang");
        }

        var bitmap = Decode(bytes);

        var result = new SkinFetch
        {
            Image = bitmap,
            Model = model == "slim" ? "slim" : "classic",
            Name = name
        };

        // 缓存：uuid 和玩家名各存一份。
        // 之前只存 uuid，启动时却按玩家名找，重启后必然退回史蒂夫。
        try
        {
            var keys = new List<string>();
            if (uuid.Length > 0) keys.Add(uuid);
            if (!string.IsNullOrWhiteSpace(name)) keys.Add(name.ToLowerInvariant());

            foreach (string key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string path = Path.Combine(CacheDir, key + ".png");
                await File.WriteAllBytesAsync(path, bytes);
                await File.WriteAllTextAsync(Path.ChangeExtension(path, ".model"), result.Model);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("皮肤缓存写入失败：" + ex.Message);
        }

        lock (Gate)
        {
            Cache["player:" + name.ToLowerInvariant()] = bitmap;
            Models["player:" + name.ToLowerInvariant()] = result.Model;
            if (uuid.Length > 0) Models[uuid] = result.Model;
        }

        return result;
    }

    /// <summary>解析 playerdb.co 的返回：uuid、名字、贴图元数据。</summary>
    private static (string Uuid, string Name, string Value, string Model) ReadPlayerDb(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("player", out var player))
                return ("", "", "", "");

            string uuid = player.TryGetProperty("id", out var id)
                ? (id.GetString() ?? "").Replace("-", "")
                : "";

            string name = player.TryGetProperty("username", out var username)
                ? username.GetString() ?? ""
                : "";

            var (value, model) = ReadTextures(player);
            return (uuid, name, value ?? "", model);
        }
        catch
        {
            return ("", "", "", "");
        }
    }

    /// <summary>正版皮肤的手臂模型（内存里有就用内存，没有读侧车文件，默认经典）。</summary>
    public static string ModelOf(string key)
    {
        if (string.IsNullOrEmpty(key)) return "classic";

        lock (Gate)
        {
            if (Models.TryGetValue(key, out var cached)) return cached;
        }

        try
        {
            var path = Path.Combine(CacheDir, key + ".model");
            if (File.Exists(path))
            {
                string model = File.ReadAllText(path).Trim();
                model = model == "slim" ? "slim" : "classic";
                lock (Gate) Models[key] = model;
                return model;
            }
        }
        catch
        {
            // 读不到就当经典
        }

        return "classic";
    }

    /// <summary>决定一张卡片该用哪种手臂：配置优先，其次来源，最后默认经典。</summary>
    public static string ResolveModel(SkinOptions skin)
    {
        if (skin == null) return "classic";
        if (skin.Model == "slim" || skin.Model == "classic") return skin.Model;

        switch (skin.Source)
        {
            case "builtin":
                return string.Equals(skin.Value, "alex", StringComparison.OrdinalIgnoreCase) ? "slim" : "classic";
            case "player":
                return ModelOf(skin.Value);
            default:
                return "classic";
        }
    }

    // ─────────────────────────── 网络小工具 ───────────────────────────

    private static bool IsUuid(string text)
    {
        string raw = text.Replace("-", "");
        return raw.Length == 32 && raw.All(char.IsLetterOrDigit);
    }

    private static async Task<string?> GetJsonAsync(HttpClient http, string url)
    {
        try
        {
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            string text = await response.Content.ReadAsStringAsync();
            return text.TrimStart().StartsWith("{", StringComparison.Ordinal) ? text : null;
        }
        catch (Exception ex)
        {
            Log.Warn($"请求失败（{Short(url)}）：{ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    private static async Task<byte[]?> GetBytesAsync(HttpClient http, string url)
    {
        try
        {
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Log.Warn($"下载失败（{Short(url)}）：{ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    private static string Short(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host + uri.AbsolutePath;
        }
        catch
        {
            return url;
        }
    }

    private static string? ReadUuid(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()?.Replace("-", "")
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("name", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>从 profile 的 properties 里取出 textures 的 base64 和 model。</summary>
    private static (string? Value, string Model) ReadTextures(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ReadTextures(doc.RootElement);
        }
        catch
        {
            return (null, "");
        }
    }

    private static (string? Value, string Model) ReadTextures(JsonElement root)
    {
        if (!root.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Array)
            return (null, "");

        foreach (var prop in props.EnumerateArray())
        {
            if (!prop.TryGetProperty("name", out var nameNode)
                || !string.Equals(nameNode.GetString(), "textures", StringComparison.Ordinal))
                continue;

            if (!prop.TryGetProperty("value", out var valueNode)) continue;
            string value = valueNode.GetString() ?? "";
            if (value.Length == 0) continue;

            try
            {
                var raw = Convert.FromBase64String(value);
                using var doc = JsonDocument.Parse(raw);

                string model = "";
                if (doc.RootElement.TryGetProperty("textures", out var textures)
                    && textures.TryGetProperty("SKIN", out var skin)
                    && skin.TryGetProperty("metadata", out var metadata)
                    && metadata.TryGetProperty("model", out var modelNode))
                {
                    model = modelNode.GetString() ?? "";
                }

                return (value, model);
            }
            catch (Exception ex)
            {
                Log.Warn("解析皮肤元数据失败：" + ex.Message);
            }
        }

        return (null, "");
    }

    private static string? ReadSkinUrl(string base64Value)
    {
        try
        {
            var raw = Convert.FromBase64String(base64Value);
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("textures", out var textures)
                && textures.TryGetProperty("SKIN", out var skin)
                && skin.TryGetProperty("url", out var url))
            {
                return url.GetString();
            }
        }
        catch (Exception ex)
        {
            Log.Warn("解析皮肤地址失败：" + ex.Message);
        }

        return null;
    }

    private static BitmapSource? LoadCachedPlayer(string uuidOrName)
    {
        try
        {
            var path = Path.Combine(CacheDir, uuidOrName + ".png");
            return File.Exists(path) ? LoadFile(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? LoadFile(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            return Decode(File.ReadAllBytes(path));
        }
        catch (Exception ex)
        {
            Log.Warn($"读取皮肤图片失败：{path}（{ex.Message}）");
            return null;
        }
    }

    private static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static BitmapSource Decode(Stream stream)
    {
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    // ─────────────────────────── 内置皮肤 ───────────────────────────

    private static readonly Dictionary<string, BitmapSource> BuiltinCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>经典 Steve：直接读打包进 DLL 的官方原版贴图。</summary>
    public static BitmapSource PaintSteve() => Builtin("Steve");

    /// <summary>Alex：官方原版贴图，手臂是细 3px。</summary>
    public static BitmapSource PaintAlex() => Builtin("Alex");

    private static BitmapSource Builtin(string name)
    {
        lock (Gate)
        {
            if (BuiltinCache.TryGetValue(name, out var cached)) return cached;
        }

        BitmapSource? image = null;

        try
        {
            var assembly = typeof(SkinLibrary).Assembly;
            using var stream = assembly.GetManifestResourceStream("PCLN.AdvancedHomepage.Assets." + name + ".png");
            if (stream != null) image = Decode(stream);
        }
        catch (Exception ex)
        {
            Log.Warn("内置皮肤读取失败：" + ex.Message);
        }

        // 资源没打进包时（不该发生）退回代码画的版本，至少还有人可看
        image ??= PaintFallback(string.Equals(name, "Alex", StringComparison.OrdinalIgnoreCase));
        image.Freeze();

        lock (Gate) BuiltinCache[name] = image;
        return image;
    }

    private static BitmapSource PaintFallback(bool slim) => slim
        ? Paint(new SkinPalette(
            skin: "#F2C6A0", hair: "#C4753B", shirt: "#7FB069", sleeve: "#7FB069",
            pants: "#41436D", shoes: "#5A5A5A", eye: "#FFFFFF", iris: "#4C7A3A"), slim: true)
        : Paint(new SkinPalette(
            skin: "#B4855A", hair: "#3B2A1A", shirt: "#00A8A8", sleeve: "#00A8A8",
            pants: "#3B3BB5", shoes: "#4B4B4B", eye: "#FFFFFF", iris: "#3A5DA8"));

    private readonly record struct SkinPalette(
        string skin, string hair, string shirt, string sleeve,
        string pants, string shoes, string eye, string iris);

    private static BitmapSource Paint(SkinPalette p, bool slim = false)
    {
        const int size = 64;
        var pixels = new byte[size * size * 4];   // BGRA，默认全透明

        void Fill(int x, int y, int w, int h, string hex)
        {
            var color = Palette.Parse(hex, Colors.Gray);
            for (int iy = y; iy < y + h; iy++)
            {
                for (int ix = x; ix < x + w; ix++)
                {
                    if (ix < 0 || iy < 0 || ix >= size || iy >= size) continue;
                    int offset = (iy * size + ix) * 4;
                    pixels[offset + 0] = color.B;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 2] = color.R;
                    pixels[offset + 3] = 255;
                }
            }
        }

        // ── 头（8×8×8）：顶/底 + 四个侧面 ──
        Fill(8, 0, 8, 8, p.hair);          // 顶
        Fill(16, 0, 8, 8, p.skin);         // 底
        Fill(0, 8, 8, 8, p.hair);          // 右
        Fill(16, 8, 8, 8, p.hair);         // 左
        Fill(24, 8, 8, 8, p.hair);         // 后
        Fill(8, 8, 8, 8, p.skin);          // 前

        // 前脸：刘海、眼睛、嘴
        Fill(8, 8, 8, 2, p.hair);
        Fill(9, 12, 1, 1, p.eye);
        Fill(12, 12, 1, 1, p.eye);
        Fill(9, 13, 1, 1, p.iris);
        Fill(12, 13, 1, 1, p.iris);
        Fill(10, 15, 2, 1, "#8A5E3B");

        // ── 身体（8×12×4）──
        Fill(20, 16, 8, 4, p.shirt);       // 顶
        Fill(28, 16, 8, 4, p.shirt);       // 底
        Fill(16, 20, 4, 12, p.shirt);      // 右
        Fill(28, 20, 4, 12, p.shirt);      // 左
        Fill(32, 20, 8, 12, p.shirt);      // 后
        Fill(20, 20, 8, 12, p.shirt);      // 前

        // ── 手臂（经典 4px / 细 3px，后面按 slim 决定宽度）──
        void FillArm(double ox, double oy, bool slimArm)
        {
            double aw = slimArm ? 3 : 4;

            Fill((int)(ox + 4), (int)oy, (int)aw, 4, p.sleeve);                  // 顶
            Fill((int)(ox + 4 + aw), (int)oy, (int)aw, 4, p.sleeve);             // 底
            Fill((int)ox, (int)(oy + 4), 4, 12, p.sleeve);                       // 右
            Fill((int)(ox + 4 + aw), (int)(oy + 4), 4, 12, p.sleeve);            // 左
            Fill((int)(ox + 4 + aw + 4), (int)(oy + 4), (int)aw, 12, p.sleeve);  // 后
            Fill((int)(ox + 4), (int)(oy + 4), (int)aw, 10, p.sleeve);           // 前
            Fill((int)(ox + 4), (int)(oy + 14), (int)aw, 2, p.skin);             // 手掌露出来
        }

        FillArm(40, 16, slim);   // 右臂

        // ── 右腿（4×12×4）──
        Fill(4, 16, 4, 4, p.pants);
        Fill(8, 16, 4, 4, p.pants);
        Fill(0, 20, 4, 12, p.pants);
        Fill(8, 20, 4, 12, p.pants);
        Fill(12, 20, 4, 12, p.pants);
        Fill(4, 20, 4, 12, p.pants);
        Fill(0, 30, 4, 2, p.shoes);
        Fill(4, 30, 4, 2, p.shoes);
        Fill(8, 30, 4, 2, p.shoes);
        Fill(12, 30, 4, 2, p.shoes);

        // ── 左臂 / 左腿（64×64 才有；64×32 的老皮肤会镜像右臂右腿）──
        FillArm(32, 48, slim);   // 左臂

        Fill(20, 48, 4, 4, p.pants);
        Fill(24, 48, 4, 4, p.pants);
        Fill(16, 52, 4, 12, p.pants);
        Fill(24, 52, 4, 12, p.pants);
        Fill(28, 52, 4, 12, p.pants);
        Fill(20, 52, 4, 12, p.pants);
        Fill(16, 62, 4, 2, p.shoes);
        Fill(20, 62, 4, 2, p.shoes);
        Fill(24, 62, 4, 2, p.shoes);
        Fill(28, 62, 4, 2, p.shoes);

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bitmap.Freeze();
        return bitmap;
    }
}

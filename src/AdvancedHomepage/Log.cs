using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace PCLN.AdvancedHomepage;

/// <summary>极简文件日志，写到 PCL/AdvancedHomepage/log.txt。</summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static string _path;

    public static string FilePath
    {
        get
        {
            if (_path != null) return _path;
            try
            {
                _path = Path.Combine(Paths.DataDir, "log.txt");
            }
            catch
            {
                _path = Path.Combine(AppContext.BaseDirectory, "advanced-homepage.log");
            }
            return _path;
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(" [").Append(level).Append("] ").Append(message);
            if (ex != null) sb.AppendLine().Append(ex);
            lock (Gate)
            {
                var path = FilePath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                    File.Delete(path);
                File.AppendAllText(path, sb.ToString().TrimEnd() + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败不得影响启动器
        }
    }
}

/// <summary>反射读取启动器内部成员（字段可能为 internal/private）。</summary>
internal static class Reflect
{
    public static object Field(object instance, string name)
    {
        if (instance == null) return null;
        var type = instance.GetType();
        while (type != null)
        {
            var field = type.GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(instance);
            type = type.BaseType;
        }
        return null;
    }

    public static object StaticField(Type type, string name)
    {
        var field = type?.GetField(name,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.GetValue(null);
    }

    public static object Call(object instance, string name, params object[] args)
    {
        if (instance == null) return null;
        var method = instance.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return method?.Invoke(instance, args);
    }
}

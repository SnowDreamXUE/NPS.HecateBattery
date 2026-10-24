// PluginSettings.cs
// 组件样式等轻量设置的持久化（宿主 v2.0.2 没有插件存储 API）。
// 保存到 %APPDATA%\NotchPeninsula\plugins\com.snow.hecatebattery\settings.json；
// 读写全部失败安全：任何异常都回落到默认值，绝不影响插件运行。
using System;
using System.IO;
using System.Text.Json;

namespace NPS.HecateBattery;

/// <summary>折叠态组件样式（详情页可切换）。</summary>
internal enum WidgetStyle
{
    /// <summary>方案 A · 经典描边：描边电池 + 电量进度填充 + 常规数字。</summary>
    Classic,

    /// <summary>方案 B · 彩色胶囊：强调色胶囊电池 + 加粗同色数字，无进度填充。</summary>
    Capsule,

    /// <summary>方案 C · 极简细框：线条耳机 + 细边框电池，无填充，最轻量。</summary>
    Minimal,
}

internal sealed class PluginSettings
{
    public string WidgetStyle { get; set; } = "classic";
}

internal static class SettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NotchPeninsula", "plugins", "com.snow.hecatebattery", "settings.json");

    private static readonly object Gate = new();

    public static WidgetStyle Load()
    {
        try
        {
            lock (Gate)
            {
                if (!System.IO.File.Exists(SettingsPath)) return WidgetStyle.Classic;
                PluginSettings? s = JsonSerializer.Deserialize<PluginSettings>(
                    System.IO.File.ReadAllText(SettingsPath));
                return s?.WidgetStyle switch
                {
                    "capsule" => WidgetStyle.Capsule,
                    "minimal" => WidgetStyle.Minimal,
                    _ => WidgetStyle.Classic,
                };
            }
        }
        catch
        {
            return WidgetStyle.Classic;
        }
    }

    public static void Save(WidgetStyle style)
    {
        try
        {
            lock (Gate)
            {
                string? dir = Path.GetDirectoryName(SettingsPath);
                if (dir != null) Directory.CreateDirectory(dir);
                string name = style switch
                {
                    WidgetStyle.Capsule => "capsule",
                    WidgetStyle.Minimal => "minimal",
                    _ => "classic",
                };
                System.IO.File.WriteAllText(
                    SettingsPath,
                    JsonSerializer.Serialize(new PluginSettings { WidgetStyle = name }));
            }
        }
        catch
        {
            // 设置保存失败不影响运行，下次切换会再次尝试。
        }
    }
}

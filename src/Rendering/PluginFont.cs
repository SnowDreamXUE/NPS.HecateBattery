// PluginFont.cs
// 从宿主 FontConfig 取得字体。宿主字体属于共享资源：
// 每次绘制按需现取、不缓存、绝不 Dispose；用 try/catch 包住允许永久降级为 Skia 默认字体。
// MeasureWidth 与 Draw 必须用同一档字体面，否则量出的宽度和画出的不一致。
using SkiaSharp;

namespace NPS.HecateBattery;

internal static class PluginFont
{
    public static SKTypeface? Normal()
    {
        try { return NotchPeninsula.FontConfig.Normal; } catch { }
        try { return NotchPeninsula.FontConfig.Fallback; } catch { }
        return null;
    }

    public static SKTypeface? SemiBold()
    {
        try { return NotchPeninsula.FontConfig.SemiBold; } catch { }
        try { return NotchPeninsula.FontConfig.Normal; } catch { }
        return null;
    }
}

// PluginFont.cs
// 从宿主 FontConfig 取得字体。宿主字体属于共享资源：
// 每次绘制按需现取、不缓存、绝不 Dispose；用 try/catch 包住允许永久降级为 Skia 默认字体。
// 2.1.0 起 AutoWidth 协议的测量字体由宿主 WidgetKit 统一（本组件布局为定宽盒子、不依赖字体），
// 这里只服务 Draw 绘制，取的档位与 WidgetKit.Typeface 一致（SemiBold ↔ FontConfig.SemiBold）。
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

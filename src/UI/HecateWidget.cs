// HecateWidget.cs
// 折叠态组件：左侧耳机剪影 + 右侧电池，电量数字嵌在电池内部。
// 三种样式（右键详情页可切换，见 HecateDetailPage）：
//   Classic 方案 A · 经典描边 —— 描边电池 + 电量进度填充 + 常规数字；
//   Capsule 方案 B · 彩色胶囊 —— 强调色胶囊电池 + 加粗同色数字，无进度填充；
//   Minimal 方案 C · 极简细框 —— 线条耳机 + 细边框电池，无填充，最轻量。
// 电量 ≤ 20% 或未连接时按既有规则变色/置灰；关机(Offline)整组件隐藏。
// 只读不可变快照并绘制；宽度变化后由插件调用 InvalidateWidgetLayout 触发宿主重测。
// 适配宿主 2.1.0 AutoWidth 协议：实现 IAutoWidthWidget，宽度由宿主按 Layout 盒子测量
//（不再实现老协议 MeasureWidth），槽位两侧 1px 留白由宿主统一加，绘制用 ContentRect 取内容区。
using System;
using NotchPeninsula.Plugins;
using SkiaSharp;

namespace NPS.HecateBattery;

internal sealed class HecateWidget : IAutoWidthWidget
{
    private const float HeadsetW = 15f;   // 耳机剪影宽
    private const float Gap = 5f;         // 耳机与电池间距
    private const float BodyH = 12.5f;    // 电池主体高（方案 A/B；C 为 11.5）
    private const float CapBlock = 2.8f;  // 电池小帽区（三种样式统一：帽宽 + 间隙）
    private const float Inset = 1.9f;     // 电池内部留白（数字与填充的内边界）

    private readonly HecateBatteryPlugin _plugin;
    private readonly HecateDetailPage _detail;

    public HecateWidget(HecateBatteryPlugin plugin)
    {
        _plugin = plugin;
        _detail = new HecateDetailPage(plugin);
    }

    public string Id => "com.snow.hecatebattery.widget";
    public string DisplayName => "HECATE 电量";
    public IDetailPage? DetailPage => _detail;

    // 宽度只有「恒定 / 0（隐藏）」两态，不随内容抖动：保持默认 false，
    // 宿主对固定宽组件零额外开销，显示/隐藏切换即时生效（没有 600ms 收窄迟滞）。
    public Box? Layout(float availableHeight)
    {
        // 耳机已关机/超出范围：返回 null（0 宽），宿主本帧不分配空间、不绘制、不注册命中区（自动隐藏）。
        // 开机重连后轮询刷新快照并触发 InvalidateWidgetLayout，组件自动恢复显示。
        if (_plugin.Snapshot.State == LinkState.Offline) return null;

        // "100%" 靠数字自适应缩放放进电池，因此各样式宽度恒定，不随电量抖动。
        // 电池块 = 主体宽 + 右侧电极帽；两侧 1px 留白由宿主统一加（见 WidgetKit.AutoPadX）。
        float bodyW = _plugin.Style == WidgetStyle.Minimal ? 22.5f : 24f;
        return WidgetKit.Row(
            WidgetKit.Icon(HeadsetW),
            WidgetKit.Gap(Gap),
            WidgetKit.Icon(bodyW + CapBlock));
    }

    public void Draw(SKCanvas canvas, SKRect rect, WidgetFrame frame)
    {
        if (frame.Alpha == 0) return;

        BatterySnapshot s = _plugin.Snapshot;
        if (s.State == LinkState.Offline) return; // 宿主宽度缓存刷新前的过渡帧，不画

        bool ok = s.State == LinkState.Ok;
        bool low = ok && s.Level <= 20;
        string text = ok ? s.Level + "%" : "--";

        SKColor glyphColor = ColorFor(ok, low, frame).WithAlpha(frame.Alpha);
        float cy = rect.MidY + frame.TextOffsetY;
        float left = WidgetKit.ContentRect(rect).Left;

        DrawHeadset(canvas, left, cy, glyphColor, outline: _plugin.Style == WidgetStyle.Minimal);

        float bx = left + HeadsetW + Gap;
        switch (_plugin.Style)
        {
            case WidgetStyle.Capsule:
                DrawCapsuleBattery(canvas, bx, cy, ok, s.Level, text, frame);
                break;
            case WidgetStyle.Minimal:
                DrawMinimalBattery(canvas, bx, cy, ok, s.Level, text, frame);
                break;
            default:
                DrawClassicBattery(canvas, bx, cy, ok, s.Level, text, frame);
                break;
        }
    }

    private static SKColor ColorFor(bool ok, bool low, WidgetFrame frame) => ok
        ? (low ? HecateBatteryPlugin.WarningColor : frame.Theme.TextColor)
        : frame.Theme.SubTextColor;

    /// <summary>耳机剪影：头梁半圆弧 + 左右耳罩，局部宽 15、高约 11.4，相对 cy 垂直居中。</summary>
    private static void DrawHeadset(SKCanvas canvas, float left, float cy, SKColor color, bool outline)
    {
        float top = cy - 7.2f;

        using (var band = new SKPath())
        {
            // 头梁：上半圆弧，圆心 (left+7.5, top+6.8)，半径 5.3
            band.ArcTo(
                new SKRect(left + 2.2f, top + 1.5f, left + 12.8f, top + 12.1f),
                180f, 180f, false);

            using var stroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = outline ? 1.4f : 1.7f,
                StrokeCap = SKStrokeCap.Round,
                Color = color,
            };
            canvas.DrawPath(band, stroke);
        }

        if (outline)
        {
            // 线条耳罩：细描边、不填充
            using var cupStroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.3f,
                Color = color,
            };
            canvas.DrawRoundRect(left + 0.9f, top + 6.8f, 3.2f, 6f, 1.4f, 1.4f, cupStroke);
            canvas.DrawRoundRect(left + 10.9f, top + 6.8f, 3.2f, 6f, 1.4f, 1.4f, cupStroke);
        }
        else
        {
            using var cup = new SKPaint { IsAntialias = true, Color = color };
            canvas.DrawRoundRect(left + 0.4f, top + 6.3f, 3.7f, 6.6f, 1.7f, 1.7f, cup);
            canvas.DrawRoundRect(left + 10.9f, top + 6.3f, 3.7f, 6.6f, 1.7f, 1.7f, cup);
        }
    }

    /// <summary>方案 A · 经典描边：描边电池 + 电量进度填充，数字嵌在电池内部。</summary>
    private static void DrawClassicBattery(
        SKCanvas canvas, float bx, float cy, bool ok, int level, string text, WidgetFrame frame)
    {
        const float bodyW = 24f;
        SKColor main = ColorFor(ok, ok && level <= 20, frame).WithAlpha(frame.Alpha);
        SKColor fillTint = main.WithAlpha((byte)(frame.Alpha * 0.26f));
        var body = new SKRect(bx, cy - BodyH / 2f, bx + bodyW, cy + BodyH / 2f);

        using (var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = main })
        using (var tint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = fillTint })
        using (var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.2f,
            Color = main,
        })
        {
            if (ok) // 电量进度填充
            {
                float w = (bodyW - Inset * 2f) * Math.Clamp(level / 100f, 0f, 1f);
                if (w > 0.4f)
                {
                    canvas.DrawRoundRect(
                        new SKRect(body.Left + Inset, body.Top + Inset, body.Left + Inset + w, body.Bottom - Inset),
                        2f, 2f, tint);
                }
            }

            canvas.DrawRoundRect(body, 3.2f, 3.2f, stroke);
            canvas.DrawRoundRect(body.Right + 0.7f, cy - 2.35f, 2.1f, 4.7f, 1f, 1f, fill);
        }

        DrawBatteryText(canvas, bx + bodyW / 2f, cy, bodyW, text, 8.6f, main, fakeBold: false);
    }

    /// <summary>方案 B · 彩色胶囊：强调色胶囊电池，无进度填充，数字加粗同色。</summary>
    private static void DrawCapsuleBattery(
        SKCanvas canvas, float bx, float cy, bool ok, int level, string text, WidgetFrame frame)
    {
        const float bodyW = 24f;
        SKColor accent = (ok
                ? (level <= 20 ? HecateBatteryPlugin.WarningColor : HecateBatteryPlugin.AccentColor)
                : frame.Theme.SubTextColor)
            .WithAlpha(frame.Alpha);
        SKColor innerTint = accent.WithAlpha((byte)(frame.Alpha * 0.20f));
        var body = new SKRect(bx, cy - BodyH / 2f, bx + bodyW, cy + BodyH / 2f);
        var inner = new SKRect(bx + 2.2f, body.Top + 2.2f, body.Right - 2.2f, body.Bottom - 2.2f);

        using (var tint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = innerTint })
        {
            canvas.DrawRoundRect(inner, 4.05f, 4.05f, tint);
        }

        using (var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.6f,
            Color = accent,
        })
        {
            canvas.DrawRoundRect(body, 5.45f, 5.45f, stroke);
        }

        using (var fill = new SKPaint { IsAntialias = true, Color = accent })
        {
            canvas.DrawRoundRect(body.Right + 0.7f, cy - 2.45f, 2.1f, 4.9f, 1.1f, 1.1f, fill);
        }

        DrawBatteryText(canvas, bx + bodyW / 2f, cy, bodyW, text, 9f, accent, fakeBold: true);
    }

    /// <summary>方案 C · 极简细框：细边框电池，无填充，数字浅色低调（低电量转红）。</summary>
    private static void DrawMinimalBattery(
        SKCanvas canvas, float bx, float cy, bool ok, int level, string text, WidgetFrame frame)
    {
        const float bodyW = 22.5f;
        const float bodyH = 11.5f;
        bool low = ok && level <= 20;
        SKColor main = ColorFor(ok, low, frame);

        // 常态 55% 线框 + 92% 数字保持低调；低电量线框/数字全透明度保住警示力
        SKColor edge = main.WithAlpha((byte)(frame.Alpha * (low ? 1f : 0.55f)));
        SKColor number = main.WithAlpha((byte)(frame.Alpha * (low ? 1f : 0.92f)));
        var body = new SKRect(bx, cy - bodyH / 2f, bx + bodyW, cy + bodyH / 2f);

        using (var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.9f,
            Color = edge,
        })
        {
            canvas.DrawRoundRect(body, 2.6f, 2.6f, stroke);
        }

        using (var fill = new SKPaint { IsAntialias = true, Color = edge })
        {
            canvas.DrawRoundRect(body.Right + 0.9f, cy - 2.15f, 1.9f, 4.3f, 0.9f, 0.9f, fill);
        }

        DrawBatteryText(canvas, bx + bodyW / 2f, cy, bodyW, text, 8.6f, number, fakeBold: false);
    }

    /// <summary>电池内嵌数字：先按基准字号测量，超出电池内宽就等比缩小（保证 "100%" 也放得下）。</summary>
    private static void DrawBatteryText(
        SKCanvas canvas, float centerX, float cy, float bodyW, string text, float size, SKColor color, bool fakeBold)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            TextSize = size,
            Color = color,
            FakeBoldText = fakeBold,
        };
        SKTypeface? face = PluginFont.SemiBold();
        if (face != null) paint.Typeface = face;

        float inner = bodyW - Inset * 2f;
        float w = paint.MeasureText(text);
        if (w > inner)
        {
            paint.TextSize = size * inner / w;
            w = paint.MeasureText(text);
        }

        SKFontMetrics m = paint.FontMetrics;
        float baseline = cy - (m.Ascent + m.Descent) / 2f;
        canvas.DrawText(text, centerX - w / 2f, baseline, paint);
    }

    public WidgetHit HitTest(float x, float y, SKRect rect)
    {
        return x >= 0 && x <= rect.Width && y >= 0 && y <= rect.Height
            ? new WidgetHit("refresh")
            : WidgetHit.None;
    }

    public void OnLeftClick(string? action, float x, float y)
    {
        if (action == "refresh") _plugin.RequestRefresh();
    }

    public void OnRightClick() { }

    public void OnActivate(IPluginHost host) { }

    public void OnDeactivate() { }
}

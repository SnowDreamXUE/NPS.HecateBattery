// HecateWidget.cs
// 折叠态组件：电池图标（外框 + 小帽 + 按电量填充）+ 百分比文字。
// 只读不可变快照并绘制；宽度变化后由插件调用 InvalidateWidgetLayout 触发宿主重测。
using System;
using NotchPeninsula.Plugins;
using SkiaSharp;

namespace NPS.HecateBattery;

internal sealed class HecateWidget : IWidget
{
    private const float IconBlock = 18f;
    private const float Gap = 5f;
    private const float TextSize = 12.5f;
    private const float LeftPad = 1f;
    private const float RightPad = 1f;

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

    private string CurrentText()
    {
        BatterySnapshot s = _plugin.Snapshot;
        return s.State == LinkState.Ok ? s.Level + "%" : "--";
    }

    public float MeasureWidth(float availableHeight)
    {
        using var paint = new SKPaint { IsAntialias = true, TextSize = TextSize };
        SKTypeface? face = PluginFont.SemiBold();
        if (face != null) paint.Typeface = face;

        return LeftPad + IconBlock + Gap + paint.MeasureText(CurrentText()) + RightPad;
    }

    public void Draw(SKCanvas canvas, SKRect rect, WidgetFrame frame)
    {
        if (frame.Alpha == 0) return;

        BatterySnapshot s = _plugin.Snapshot;
        string text = s.State == LinkState.Ok ? s.Level + "%" : "--";

        SKColor color = s.State == LinkState.Ok
            ? (s.Level <= 20 ? HecateBatteryPlugin.WarningColor : frame.Theme.TextColor)
            : frame.Theme.SubTextColor;
        color = color.WithAlpha(frame.Alpha);

        float cy = rect.MidY + frame.TextOffsetY;
        float left = rect.Left + LeftPad;
        const float bodyW = 15f;
        const float bodyH = 9f;
        float top = cy - bodyH / 2f;
        var body = new SKRect(left, top, left + bodyW, top + bodyH);

        using (var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.2f,
            Color = color,
        })
        using (var fill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = color,
        })
        {
            canvas.DrawRoundRect(body, 2.2f, 2.2f, stroke);

            var cap = new SKRect(body.Right + 1.2f, cy - 2f, body.Right + 2.6f, cy + 2f);
            canvas.DrawRoundRect(cap, 0.8f, 0.8f, fill);

            if (s.State == LinkState.Ok && s.Level > 0)
            {
                const float pad = 2f;
                float innerW = (bodyW - pad * 2f) * Math.Clamp(s.Level / 100f, 0f, 1f);
                if (innerW > 0.4f)
                {
                    var inner = new SKRect(
                        body.Left + pad,
                        body.Top + pad,
                        body.Left + pad + innerW,
                        body.Bottom - pad);
                    canvas.DrawRoundRect(inner, 1.2f, 1.2f, fill);
                }
            }
        }

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = TextSize,
            Color = color,
        };
        SKTypeface? face = PluginFont.SemiBold();
        if (face != null) textPaint.Typeface = face;

        canvas.DrawText(text, left + IconBlock + Gap, cy + 4.5f, textPaint);
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

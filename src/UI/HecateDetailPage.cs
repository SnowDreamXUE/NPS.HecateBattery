// HecateDetailPage.cs
// 右键展开的详情页（固定设计坐标 340×132，绘制时按 rect 等比缩放，
// 宿主展开动画期间 rect 是插值尺寸，不能拿它当排版基准）。
// 内容：标题/副标题、大号电量、状态文案、进度条、上次更新时间与刷新按钮。
using System;
using NotchPeninsula.Plugins;
using SkiaSharp;

namespace NPS.HecateBattery;

internal sealed class HecateDetailPage : IDetailPage
{
    private const float DesignW = 340f;
    private const float DesignH = 132f;
    private static readonly SKRect RefreshButton = new(258f, 16f, 326f, 42f);

    private readonly HecateBatteryPlugin _plugin;

    public HecateDetailPage(HecateBatteryPlugin plugin)
    {
        _plugin = plugin;
    }

    public float MeasureWidth() => DesignW;
    public float MeasureHeight() => DesignH;

    public void Draw(SKCanvas canvas, SKRect rect, WidgetFrame frame)
    {
        if (frame.Alpha == 0 || rect.Width <= 0 || rect.Height <= 0) return;

        float scale = Math.Min(rect.Height / DesignH, 1f);
        if (scale <= 0) return;

        canvas.Save();
        canvas.Translate(rect.MidX, rect.MidY);
        canvas.Scale(scale);
        canvas.Translate(-DesignW / 2f, -DesignH / 2f);

        BatterySnapshot s = _plugin.Snapshot;
        RenderTheme theme = frame.Theme;
        byte alpha = frame.Alpha;

        SKColor mainColor = theme.TextColor.WithAlpha(alpha);
        SKColor subColor = theme.SubTextColor.WithAlpha(alpha);

        DrawText(canvas, "HECATE G3 MAX", 18f, 26f, 15f, true, mainColor);
        DrawText(canvas, "2.4G 无线接收器 · VID 35BB / PID A226", 18f, 43f, 10.5f, false, subColor);

        string big = s.State == LinkState.Ok ? s.Level + "%" : "--";
        SKColor bigColor = s.State == LinkState.Ok
            ? (s.Level <= 20 ? HecateBatteryPlugin.WarningColor : theme.TextColor).WithAlpha(alpha)
            : subColor;
        DrawText(canvas, big, 18f, 81f, 32f, true, bigColor);

        string status = s.State switch
        {
            LinkState.Reading => "正在读取…",
            LinkState.Ok => "已连接 · 刷新周期 " + HecateBatteryPlugin.PollSeconds + " 秒",
            LinkState.Disconnected => "未连接：请确认接收器已插入、耳机已开机",
            _ => "设备无响应：可能被其他程序短暂占用",
        };
        DrawText(canvas, status, 118f, 78f, 11f, false, subColor);

        float progress = s.State == LinkState.Ok
            ? Math.Clamp(s.Level / 100f, 0f, 1f)
            : 0f;

        using (var track = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = theme.SubTextColor.WithAlpha((byte)(alpha * 0.25f)),
        })
        using (var bar = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = bigColor,
        })
        {
            canvas.DrawRoundRect(new SKRect(18f, 92f, 322f, 100f), 4f, 4f, track);
            if (progress > 0.01f)
            {
                float w = 304f * progress;
                canvas.DrawRoundRect(new SKRect(18f, 92f, 18f + w, 100f), 4f, 4f, bar);
            }
        }

        string when = "—";
        if (s.UpdatedUtcTicks > 0)
        {
            when = new DateTime(s.UpdatedUtcTicks, DateTimeKind.Utc)
                .ToLocalTime()
                .ToString("HH:mm:ss");
        }
        DrawText(canvas,
            "上次更新 " + when + " · 左键点击岛上的组件可立即刷新",
            18f, 122f, 10.5f, false, subColor);

        using (var border = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = subColor,
        })
        {
            canvas.DrawRoundRect(RefreshButton, 7f, 7f, border);
        }

        using (var measure = new SKPaint { IsAntialias = true, TextSize = 11f })
        {
            SKTypeface? face = PluginFont.SemiBold();
            if (face != null) measure.Typeface = face;
            float w = measure.MeasureText("刷新");
            DrawText(canvas, "刷新",
                RefreshButton.MidX - w / 2f,
                RefreshButton.MidY + 4f,
                11f, true, mainColor);
        }

        canvas.Restore();
    }

    public WidgetHit HitTest(float x, float y, SKRect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return WidgetHit.None;

        float sx = x * (DesignW / rect.Width);
        float sy = y * (DesignH / rect.Height);
        return RefreshButton.Contains(sx, sy)
            ? new WidgetHit("refresh")
            : WidgetHit.None;
    }

    public void OnAction(string? action, float x, float y)
    {
        if (action == "refresh") _plugin.RequestRefresh();
    }

    private static void DrawText(
        SKCanvas canvas,
        string text,
        float x,
        float baseline,
        float size,
        bool semiBold,
        SKColor color)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            TextSize = size,
            Color = color,
        };

        SKTypeface? face = semiBold ? PluginFont.SemiBold() : PluginFont.Normal();
        if (face != null) paint.Typeface = face;

        canvas.DrawText(text, x, baseline, paint);
    }
}

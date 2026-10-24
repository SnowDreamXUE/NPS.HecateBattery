// HecateBatteryPlugin.cs
// NotchPeninsula 插件入口：注册组件、挂 30 秒轮询定时器、低电量提醒与生命周期管理。
// HID 协议细节见 Hardware/HecateHid.cs 与 docs/G3MAX_BATTERY_PROTOCOL.md。
using System;
using System.Threading;
using NotchPeninsula;
using NotchPeninsula.Plugins;
using SkiaSharp;

namespace NPS.HecateBattery;

public sealed class HecateBatteryPlugin : INotchPlugin, IDisposable
{
    internal const int PollSeconds = 30;
    internal static readonly SKColor WarningColor = new(0xF0, 0x5A, 0x5A);

    private readonly HecateHid _hid = new();
    private IPluginHost? _host;
    private IDisposable? _refresh;
    private HecateWidget? _widget;
    private BatterySnapshot _snapshot = new(-1, LinkState.Reading, 0);
    private int _polling;
    private int _disposed;
    private int _failures;
    private int _lastNotified = 101;
    private int _lastLoggedState = -1;
    private bool _fontHooked;

    public string Id => "com.snow.hecatebattery";
    public string DisplayName => "HECATE 耳机电量";
    public string Version => "1.1.0";
    public string Author => "SnowDream";

    internal BatterySnapshot Snapshot => Volatile.Read(ref _snapshot);

    public void Initialize(IPluginHost host)
    {
        _host = host;

        _widget = new HecateWidget(this);
        host.RegisterWidget(_widget);

        _refresh = host.ScheduleRefresh(TimeSpan.FromSeconds(PollSeconds), Refresh);

        try { TryHookFontChanged(); } catch { }

        // 不要在 Initialize 里做 HID I/O（最坏 1.5 s）；投递到线程池。
        ThreadPool.QueueUserWorkItem(_ => Refresh());
    }

    private void TryHookFontChanged()
    {
        FontConfig.Changed += OnFontChanged;
        _fontHooked = true;
    }

    private void OnFontChanged()
    {
        TryInvalidateLayout();
    }

    private void Refresh()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0) return;

        try { DoPoll(); }
        catch (Exception ex) { HandleFailure(ex); }
        finally { Volatile.Write(ref _polling, 0); }
    }

    private void DoPoll()
    {
        int level = _hid.Poll(out bool present);
        if (Volatile.Read(ref _disposed) != 0) return;

        if (level >= 0)
        {
            _failures = 0;
            BatterySnapshot previous = Snapshot;
            Volatile.Write(ref _snapshot, new BatterySnapshot(level, LinkState.Ok, DateTime.UtcNow.Ticks));
            if (previous.State != LinkState.Ok || previous.Level != level)
                TryInvalidateLayout();

            LogState(0, "已连接，电量 " + level + "%");
            MaybeNotifyLow(level);
        }
        else if (level == -3)
        {
            // 接收器明确报告耳机离线（合法状态帧 byte[5]==0x00，不是 0% 电量）：
            // 立即切换到 Offline（组件自动隐藏），不走「连续 2 次失败」防抖。
            _failures = 0;
            bool changed = Snapshot.State != LinkState.Offline;
            Volatile.Write(ref _snapshot, new BatterySnapshot(-1, LinkState.Offline, DateTime.UtcNow.Ticks));
            if (changed) TryInvalidateLayout();

            LogState(4, "耳机已关机或超出 2.4G 范围，岛上组件已自动隐藏");
        }
        else
        {
            _failures++;
            if (_failures >= 2)
            {
                var next = new BatterySnapshot(
                    -1,
                    present ? LinkState.Error : LinkState.Disconnected,
                    DateTime.UtcNow.Ticks);

                bool changed = Snapshot.State != next.State;
                Volatile.Write(ref _snapshot, next);
                if (changed) TryInvalidateLayout();

                LogState(present ? 2 : 1, present ? "设备存在但无响应" : "未检测到接收器");
            }
        }
    }

    private void HandleFailure(Exception ex)
    {
        _failures++;
        if (_failures >= 2)
        {
            bool changed = Snapshot.State != LinkState.Error;
            Volatile.Write(ref _snapshot, new BatterySnapshot(-1, LinkState.Error, DateTime.UtcNow.Ticks));
            if (changed) TryInvalidateLayout();
        }

        if (_lastLoggedState != 3)
        {
            _lastLoggedState = 3;
            try { Logger.Error("[HecateBattery] 读取异常", ex); } catch { }
        }
    }

    private void MaybeNotifyLow(int level)
    {
        if (level <= 20 && level < _lastNotified - 4)
        {
            _lastNotified = level;
            try
            {
                _host?.PostReminder(new ReminderData
                {
                    Title = "HECATE G3 MAX 电量低",
                    Body = "剩余 " + level + "%，建议尽快充电。",
                    Duration = TimeSpan.FromSeconds(5),
                });
            }
            catch { }
        }

        if (level > 25) _lastNotified = 101;
    }

    internal void RequestRefresh()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        ThreadPool.QueueUserWorkItem(_ => Refresh());
    }

    private void TryInvalidateLayout()
    {
        try { _host?.InvalidateWidgetLayout(); } catch { }
    }

    private void LogState(int state, string message)
    {
        if (_lastLoggedState == state) return;
        _lastLoggedState = state;
        try { Logger.Info("[HecateBattery] " + message); } catch { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (_fontHooked)
        {
            try { FontConfig.Changed -= OnFontChanged; } catch { }
            _fontHooked = false;
        }

        _refresh?.Dispose();
        _refresh = null;
    }
}

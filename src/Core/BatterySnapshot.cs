// BatterySnapshot.cs
// 不可变电量快照与链路状态。后台轮询线程写入、渲染线程读取，
// 通过 Volatile.Read/Write 交接，绘制层不做加锁。
namespace NPS.HecateBattery;

internal enum LinkState
{
    Reading,
    Ok,
    Disconnected,
    Error,
}

internal sealed record BatterySnapshot(int Level, LinkState State, long UpdatedUtcTicks);

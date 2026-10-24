// BatterySnapshot.cs
// 不可变电量快照与链路状态。后台轮询线程写入、渲染线程读取，
// 通过 Volatile.Read/Write 交接，绘制层不做加锁。
namespace NPS.HecateBattery;

internal enum LinkState
{
    Reading,
    Ok,

    /// <summary>收到合法状态帧且 byte[5]==0x00：耳机已关机/超出 2.4G 范围。组件自动隐藏。</summary>
    Offline,

    /// <summary>接收器不存在或连续轮询失败。组件显示 --。</summary>
    Disconnected,
    Error,
}

internal sealed record BatterySnapshot(int Level, LinkState State, long UpdatedUtcTicks);

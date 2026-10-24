# HECATE G3 MAX 2.4G 接收器电量获取协议说明（通用版）

> 适用对象：任何想读取「HECATE G3 MAX 通过 2.4G 接收器连接时的电量」的开发者。
> 不绑定任何宿主软件 —— 托盘程序、桌面小部件、脚本、插件均可按本文自行实现。
> 协议已在实机（Windows 10.0.26300 x64 + 官方 HECATE 客户端同时运行）逆向并验证。
> 参考实现见本仓库 `src/Hardware/HecateHid.cs`（C# / Win32 P/Invoke，零第三方依赖）。

---

## 0. TL;DR（30 秒版）

```text
设备      : USB VID 0x35BB / PID 0xA226（G3 MAX 的 2.4G 无线接收器）
目标集合   : 设备接口路径中包含 "col03" 的 HID 厂商集合（Report ID = 0xD0）
查询      : 写一个正好 64 字节的输出报文  D0 35 BB 00 00 ... 00
回包      : 约 10 ms 内读到 64 字节输入报文 D0 35 BB 0A 00 FF LL 00 ... 00
链路      : FF = report[5] 链路标志：01 = 耳机在线；00 = 耳机关机/超出范围
电量      : LL = report[6]，0..100，仅当 report[5]==01 时有效（0x60 = 96%，与官方客户端一致）
离线      : 耳机关机时接收器仍回包 D0 35 BB 0A 00 00 00 ...，byte[6]=0 不是 0% 电量，必须忽略
校验      : report[0]=0xD0 且 report[1]=0x35 且 report[2]=0xBB
            且 report[3]=0x0A 且 report[5]∈{0,1} 且 report[6]<=100，否则丢弃继续读
节奏      : 读窗 300 ms；失败重试 4 次间隔 100 ms；轮询间隔不要小于 5 s
```

---

## 1. 设备身份与集合选择

| 字段 | 值 | 说明 |
|---|---|---|
| USB VID | `0x35BB` | Edifier / HECATE 厂商 ID |
| USB PID | `0xA226` | G3 MAX 的 2.4G 无线接收器（注意：不是耳机本体的 PID） |
| HID 状态集合 | 接口路径含 **`col03`** | 厂商集合（Usage Page 自定义），电量走这里 |
| 该集合 Report ID | `0xD0` | 写报文首字节 |
| 报文形态 | 64 字节带编号 HID 输入/输出报文 | byte[0] = Report ID |
| 官方佐证 | HECATE 安装目录 `data/devices/015_g3max.json` | `support_battery: true`，电量走 `mi_03&col03` |

### 同一接收器的其它集合（不要用错）

| 集合 | Report ID | 用途 | 实测行为 |
|---|---|---|---|
| `col01` | 键盘集合 | 标准键盘 | 与电量无关 |
| `col02` | `0x02` | 厂商集合 | 写查询后**从不回包** |
| `col03` | `0xD0` | 状态/电量集合 | ✅ 回电量帧 |
| `col04` | `0xED` | LED 集合 | 回 LED 状态帧（`ED 35 BB 09 ...`），不是电量 |

---

## 2. 查询与回包报文

### 2.1 查询帧（Host → Dongle，64 字节）

```text
字节:  0    1    2    3    4 ... 63
值  :  D0   35   BB   00   00 ... 00
       │    └─┬─┘  └───────────────┘
       │      │    其余全 0（写任意第 3 个 payload 字节均可回包，
       │      │    本实现固定用 00）
       │      └── 固定魔法 0x35 0xBB（必须相邻、顺序固定）
       └───────── Report ID = 0xD0（col03）
```

**硬性要求：写入长度必须正好 64 字节，且 byte[0] = 0xD0。**

- 写 65 字节，或 byte[0] 写 0 → `WriteFile` 失败，`GetLastError = 87`（参数错误）。
- 魔法写错（`34 BB` / `35 BC` / `BB 35` / `35 35`）→ 无任何回包。

### 2.2 回包帧（Dongle → Host，64 字节）

```text
字节:  0    1    2    3    4    5    6    7 ... 63
值  :  D0   35   BB   0A   00   FF   LL   00 ... 00
       │    └─┬─┘  │    │    │   └─ 电量 0..100（仅在线时有效）
       │      │    │    │    └──── 链路标志：01 = 耳机在线；00 = 关机/超出范围
       │      │    │    └───────── 0x00
       │      │    └────────────── op = 0x0A（状态/电量帧）
       │      └─────────────────── 0xBB
       └────────────────────────── Report ID 0xD0（回显）
```

**电量 = `report[6]`，但只有 `report[5] == 0x01`（耳机在线）时才有效。** 实测 `0x60 = 96`，与官方 HECATE 客户端界面显示的 96% 完全一致。

耳机关机或离开 2.4G 范围后，接收器**仍然回包**，只是把链路标志（`report[5]`）置 0：

```text
离线帧: D0 35 BB 0A 00 00 00 00 ... 00
                       │    │  └── byte[6] = 0x00，此时不是 0% 电量，必须忽略
                       └─────── byte[5] = 0x00 → 链路断开
```

2026-10-24 实测（关机 → 等 5 秒 → 开机）：关机期间连续 6 帧全部为 `0A 00 00 00`，重新开机后第一帧立即恢复 `0A 00 01 5E`（94%）；同一时间段接收器的 4 个集合路径（col01–col04）始终存在，写入也不报错。**因此连接判断只能看 `report[5]`：绝不能用「有没有回包」判断，更不能用 `report[6] == 0` 判断。**

### 2.3 帧校验（必须严格执行）

只接受**同时**满足以下全部条件的帧，否则丢弃并继续读（接收器还会主动/被动发出其它 op 的帧）：

1. `report[0] == 0xD0`（或你当前集合的 Report ID）
2. `report[1] == 0x35`
3. `report[2] == 0xBB`
4. `report[3] == 0x0A`
5. `report[5] ∈ { 0x00, 0x01 }`（链路标志；注意 `report[5] == 0x00` 是**合法的离线帧**，不是脏帧）
6. `report[6] <= 100`

`report[5] == 0x00` 时 `report[6]` 必须忽略——那是链路清零后的占位值，不是电量。

### 2.4 其它已观测帧（不要误解析成电量）

| 帧 | 含义 | 处理 |
|---|---|---|
| `D0 35 BB 07 02 6C 16 29 65 78 12 ...` | op `0x07`，设备信息（含 6 字节 MAC） | 忽略（除非你要展示 MAC） |
| `D0 35 BB 01 xx 00 00 ...` | op `0x01`，查询确认/ACK | 忽略 |
| `ED 35 BB 09 ...`（col04） | LED 状态帧，payload 内有 `FF 00 FF 00` 等颜色值 | 忽略（不读 col04） |

---

## 3. 尚未确认的字段（禁止猜测实现）

- **是否在充电**：byte[7] 及之后未被可靠区分。如需显示充电状态，必须对照官方客户端「充电中」图标，在充电/未充电两种状态下抓包对比，确认具体 bit/byte 后再实现。
- **其它 HECATE 型号**：不同 PID 或不同固件的报文可能不同。本协议只对 `VID 0x35BB / PID 0xA226` 的 G3 MAX 2.4G 接收器负责。

---

## 4. 时序、频率与并发

| 项 | 推荐值 | 说明 |
|---|---|---|
| 回包延迟 | ~10 ms | 实测 |
| 读取窗口 | 300 ms | 足够覆盖抖动 |
| 失败重试 | 4 次，间隔 100 ms | 最坏 ~1.5 s，应放在后台线程 |
| 正常轮询 | ≥ 5 s（本项目用 30 s） | 厂商私有协议，无公开限流文档，不要暴力轮询 |
| 并发 | 同一时刻只允许一次查询 | 用门闩/锁防重入；手动刷新在轮询中可丢弃 |
| 与官方客户端共存 | 可共存 | 双方各自打开句柄即可；偶发被抢占靠重试兜底 |
| 句柄生命周期 | 每次轮询开、用完即关 | 不要长期占用设备 |

### 为什么不能用 Windows 标准电量 API

Windows 设置里能看到该设备，但不提供标准电池百分比（设备不上报标准 HID Battery usage）。`Windows.Devices.Power`、UWP HID 都拿不到这个值，必须走厂商私有报文。

---

## 5. Windows HID 访问实现要点（已验证的坑）

以下以 Win32 为准；其它语言/框架（hidapi、Python hid 等）同样适用 2.x 节的报文规则，本节 5.2/5.3/5.4 的句柄细节为 Win32 专属。

### 5.1 枚举设备

1. `SetupDiGetClassDevsW(HID GUID, NULL, NULL, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE)`，flags = `0x12`；HID GUID = `4d1e55b2-f16f-11cf-88cb-001111000030`。
2. `SetupDiEnumDeviceInterfaces` 遍历。
3. `SetupDiGetDeviceInterfaceDetailW` 取路径：
   - 第一次传空缓冲拿 `required`；
   - `SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize` 在 x64 上必须写 **8**（x86 是 6）；
   - **稳健做法**：在返回缓冲区里扫描 UTF-16 的 `\?\`（字节 `5C 00 5C 00 3F 00 5C 00`）作为路径字符串起点，不要硬编码偏移。
4. 路径过滤（不区分大小写）：必须包含 `vid_35bb&pid_a226` **且** 包含 `col03`。
5. 用 `CreateFileW(path, access=0, share=3, OPEN_EXISTING)` 打开后调 `HidD_GetAttributes` 复核 `VendorID/ProductID`。

### 5.2 打开句柄

| 用途 | access | share | flags |
|---|---|---|---|
| 读（overlapped） | `GENERIC_READ \| GENERIC_WRITE` | 3 | `FILE_FLAG_OVERLAPPED`（0x40000000） |
| 写（同步） | `GENERIC_READ \| GENERIC_WRITE` | 3 | `FILE_ATTRIBUTE_NORMAL`（0x80） |

读、写**各开一个句柄**；写入长度必须正好 64 字节。

### 5.3 OVERLAPPED 结构体布局（经典大坑）

```c
typedef struct _OVERLAPPED {
    ULONG_PTR Internal;      // 必须是指针宽度（x64 下 8 字节）
    ULONG_PTR InternalHigh;  // 同上
    DWORD     Offset;
    DWORD     OffsetHigh;
    HANDLE    hEvent;
} OVERLAPPED;
```

C# 里如果把 `Internal` / `InternalHigh` 声明成 `uint`，x64 下 `hEvent` 偏移错误，所有 `ReadFile` 会失败并返回 `ERROR_INVALID_HANDLE (6)`。

### 5.4 Overlapped 读取 + 超时（伪码）

```text
ResetEvent(ev)
ok = ReadFile(h, buf, 64, out got, ref ov)
if ok: return got
err = GetLastError()
if err != ERROR_IO_PENDING (997): return -1
wait = WaitForSingleObject(ev, timeoutMs)
if wait != WAIT_OBJECT_0:
    CancelIo(h)                                  // 取消本次读
    GetOverlappedResult(h, ref ov, out _, true)  // 阻塞收尾，避免句柄悬空
    return 0
if !GetOverlappedResult(h, ref ov, out got, false): return -1
return got
```

注意：`CancelIo` 只取消「调用线程发起的 I/O」，本模式要求读请求与超时收尾在同一线程；跨线程请改用 `CancelIoEx`。

### 5.5 采集状态机建议

```text
Poll():
  1. 若缓存路径存在，直接试缓存路径（离线路径同样缓存：接收器工作正常，开机后无需重新枚举）
  2. 失败则清缓存并重新枚举
  3. 枚举结果为空                  → 返回「设备不存在」
  4. 设备在但 4 次无有效回包        → 返回「设备在、无响应」
  5. 合法帧且 report[5]==0x00      → 返回「耳机离线」（此时 report[6] 不是电量）
  6. 合法帧且 report[5]==0x01      → 返回 0..100，并缓存路径 + Report ID
```

UI 层建议：

- 「耳机离线」是**明确状态**：收到离线帧立即切换显示（本项目为隐藏组件），不要走「连续失败」防抖，也绝不显示 0%；
- 「设备不存在 / 无响应」才用「连续 2 次失败」防抖，避免一次偶发超时闪屏。

---

## 6. 其它平台提示（未验证，谨慎参考）

- **Linux / macOS**：可尝试 hidapi（`hid_init` → `hid_open_path` → `hid_write` 64 字节 → `hid_read_timeout`）。macOS 对厂商集合的访问有 entitlement 限制；Linux 需要 udev 规则授权。以上均未实测，报文规则（第 2 节）应当一致。
- **抓包/验证工具**：可用 Wireshark + USBPcap 观察 `col03` 接口上的输出/输入报文，与本文报文表比对。

---

## 7. 已知限制

1. 只对 **G3 MAX 2.4G 接收器**（VID 35BB / PID A226）验证过；有线模式、其它型号不保证。
2. 不显示充电状态（字段未确认）。
3. 与官方 HECATE 客户端共享设备：极端情况下连续多次无回包属正常现象，靠重试与轮询兜底，不要因此缩短轮询间隔。
4. 个别 HID 防护软件可能拦截对厂商集合的写入，表现为「无响应」，不是协议实现问题。

---

## 8. 协议速查表

```text
VID/PID          : 35BB / A226
状态集合          : interface path contains "col03"
Report ID        : D0
报文长度          : 64 bytes（含 Report ID）
查询             : 64B: D0 35 BB 00 00 ... 00
回包（在线）      : 64B: D0 35 BB 0A 00 01 LL ... （LL = 0..100 = report[6]）
回包（离线）      : 64B: D0 35 BB 0A 00 00 00 ... （report[6] 不是电量）
链路标志          : report[5]（01 = 耳机在线，00 = 关机/超出范围）
电量             : report[6]，仅当 report[5]==01 时有效
回包延迟          : ~10 ms
读取窗口          : 300 ms
重试             : 4 次，间隔 100 ms
轮询             : ≥ 5 s；同一时刻一次查询；可与官方客户端共存
打开标志          : read=OVERLAPPED，write=sync；share=3
关键结构体        : OVERLAPPED.Internal/InternalHigh 必须为指针宽度
```

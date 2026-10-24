# NPS.HecateBattery

NotchPeninsula 的 HECATE G3 MAX 耳机电量插件，把「G3 MAX 通过 2.4G 接收器连接时的电量」显示在灵动岛上。项目以 C# 编写，当前版本为 **1.2.0**，插件 ID 为 `com.snow.hecatebattery`。

## 功能

- 岛上组件为「左侧耳机剪影 + 右侧电池」造型，电量数字显示在电池内部；电量 ≤ 20% 变警示红。
- 组件提供三种可选样式：**经典描边**、**彩色胶囊**、**极简细框**，在详情页「组件样式」区一键切换，选择持久化到 `%APPDATA%`，重启后保留。
- 耳机关机或超出 2.4G 范围时组件**自动隐藏**（按协议链路标志判定，不会误显示 0%）；开机重连后 ≤ 30 秒自动恢复显示。接收器未插入或无响应时显示 `--`。
- 每 30 秒自动轮询一次；左键点击组件立即刷新。
- 右键展开详情页：大号电量、状态说明（已连接 / 耳机已关机或超出范围 / 未连接 / 设备无响应 / 正在读取）、电量进度条、组件样式切换、上次更新时间，右上角「刷新」按钮。
- 电量 ≤ 20% 时弹一条 5 秒低电量提醒；回到 25% 以上后重新武装，不会反复打扰。
- 与官方 HECATE 客户端可同时运行（各自打开设备句柄，偶发被抢占靠重试兜底）。
- 唯一的用户设置是组件样式，持久化到 `%APPDATA%\NotchPeninsula\plugins\com.snow.hecatebattery\settings.json`；读写失败安全，不影响电量显示。

## 电量获取原理

该设备不上报标准 HID Battery usage，Windows 设置里看不到电量，因此插件直接与接收器的厂商 HID 集合通信：向 `col03` 集合（Report ID `0xD0`）写一条 64 字节查询报文 `D0 35 BB 00 ...`，接收器约 10 ms 内回 `D0 35 BB 0A 00 FF LL ...`，其中 `FF`（第 6 字节）是链路标志（`01` = 耳机在线，`00` = 耳机关机/超出范围），`LL`（第 7 字节）仅在线时才是电量 0–100。注意**耳机关机时接收器仍然回包**，只是链路标志置 0、`LL` 恒为 0——插件据此把组件自动隐藏，而不是错误地显示 0%。

完整的报文格式、校验规则、时序参数、已验证的反例与 Win32 实现要点整理在 **[docs/G3MAX_BATTERY_PROTOCOL.md](docs/G3MAX_BATTERY_PROTOCOL.md)** —— 这是一份**通用协议文档**，不绑定本项目，任何语言、任何软件（托盘程序、脚本、其它插件）都可以按它自行实现 G3 MAX 电量读取。

## 安装（无需本机编译）

安装需要一份与目标 NotchPeninsula 版本兼容的预编译 `NPS.HecateBattery.dll`，可直接从本仓库 `releases/` 目录获取；仓库同时提供全部源代码。

1. 从本仓库 `releases/` 目录取得 `NPS.HecateBattery.dll`，或使用提供构建能力的远程开发平台自行生成。
2. 将 DLL 放入 NotchPeninsula 主程序 exe 同级的 `plugins` 文件夹（例如 `C:\NotchPeninsula\plugins\`），或在宿主的 **设置 → 我的插件** 中导入 DLL。
3. 按宿主提示重启或重新加载插件。
4. 在 **设置 → 显示设置 → 显示内容** 中启用「HECATE 电量」并调整顺序。

## 硬件要求

- HECATE G3 MAX 耳机 + 其 2.4G 无线接收器（USB VID `35BB` / PID `A226`），仅支持 2.4G 模式；有线模式与其它型号不保证。
- Windows 10/11 x64。

## 项目结构

| 文件 | 职责 |
| --- | --- |
| `src/HecateBatteryPlugin.cs` | 插件入口：组件注册、30 秒轮询调度、低电量提醒、样式切换、生命周期 |
| `src/Core/BatterySnapshot.cs` | 链路状态与不可变电量快照（跨线程交接） |
| `src/Core/PluginSettings.cs` | 组件样式枚举与 `%APPDATA%` 设置持久化（失败安全） |
| `src/Hardware/NativeMethods.cs` | Win32 P/Invoke 声明（kernel32 / setupapi / hid） |
| `src/Hardware/HecateHid.cs` | HID 设备枚举、查询报文、回包校验与解析 |
| `src/Rendering/PluginFont.cs` | 从宿主取得当前字体（不缓存、不释放） |
| `src/UI/HecateWidget.cs` | 灵动岛折叠态组件（耳机剪影 + 电池内嵌百分比，三种样式） |
| `src/UI/HecateDetailPage.cs` | 右键详情页（大号电量、进度条、状态、样式切换与刷新按钮） |
| `docs/G3MAX_BATTERY_PROTOCOL.md` | 通用 G3MAX 电量获取协议说明（面向所有开发者） |
| `NPS.HecateBattery.csproj` | 目标框架、宿主项目引用和编译依赖 |
| `releases/NPS.HecateBattery.dll` | 与当前版本对应的预编译产物 |
| `AI_DEVELOPMENT.md` | 供 AI 开发工具接手时阅读的项目约定和远程构建说明 |

## 开发环境说明

项目当前面向 `net10.0-windows10.0.19041.0`。项目文件位于仓库根目录，通过相对路径引用同级的 `NotchPeninsula` 宿主源码工程，因此单独检出本仓库并不能解析完整的编译引用。当前仓库没有包含 CI 构建工作流，预编译产物位于 `releases/` 目录；远程构建所需的目录布局、依赖和注意事项见 [AI_DEVELOPMENT.md](AI_DEVELOPMENT.md)。

本插件当前适配的宿主为 **NotchPeninsula 正式版 2.0.2**（GitHub release [NPS-v2.0.2](https://github.com/GEORGEWWWU/NotchPeninsula/releases/tag/NPS-v2.0.2)，对应 tag `NPS-v2.0.2`）。更新宿主兼容性前，请以该 tag 的宿主源码和插件 API 文档为准，不要默认 `main` 分支与已发布版本一致：

- [NotchPeninsula 宿主仓库](https://github.com/GEORGEWWWU/NotchPeninsula)
- [NPS-v2.0.2 的插件开发 API 文档](https://github.com/GEORGEWWWU/NotchPeninsula/blob/NPS-v2.0.2/NPS_PluginsAPI.md)

## 版本记录

- **1.2.0**：组件视觉重构并新增三种可选样式。岛上组件改为「左侧耳机剪影 + 右侧电池」，电量数字显示在电池内部，宽度恒定不随数字抖动；详情页新增「组件样式」区，可在**经典描边**（白描边电池）/ **彩色胶囊**（强调色描边 + 内衬染色）/ **极简细框**（常态半透明线框，低调不抢眼）之间切换，选择持久化到 `%APPDATA%\NotchPeninsula\plugins\com.snow.hecatebattery\settings.json`，重启保留。
- **1.1.0**：修复耳机关机后组件显示 0% 的问题。现在按状态帧 `report[5]` 链路标志判定连接：收到离线帧立即把岛上组件自动隐藏（开机重连后 ≤ 30 秒恢复），离线帧的电量字节不再被误读；全程不会显示 0%。
- **1.0.0**：首个版本。实现 G3 MAX 2.4G 接收器电量读取（HID 厂商集合 `col03`）、岛上电量组件、右键详情页、30 秒轮询 + 左键手动刷新、低电量提醒（≤20%，滞后 4% 防抖）、连续失败 2 次才切换离线显示；适配 NotchPeninsula 2.0.2（tag `NPS-v2.0.2`）。

## 许可

本项目基于 [MIT License](LICENSE) 开源。

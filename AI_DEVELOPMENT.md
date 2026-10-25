# AI 开发交接说明

本文档供接手 `NPS.HecateBattery` 的 AI 开发工具阅读。它记录当前代码结构、宿主耦合点、HID 协议边界，以及云环境/用户本机的构建方式。

## Git 工作流与提交规范

所有新功能、Bug 修复和文档变更都遵循下面的流程，不要直接在 `main` 分支上修改。

### 开始任务前

1. 先检查工作区和当前分支：`git status --short --branch`。不要覆盖、丢弃或重置用户已有的未提交改动；如果这些改动会影响本任务，应先说明并等待处理方式。
2. 更新基础分支：切换到 `main` 后执行 `git pull --ff-only origin main`。禁止用强制重置覆盖远端或本地改动；如果同步发生冲突，先停止并处理冲突。
3. 从最新的 `main` 创建任务分支，再开始编辑：
   - 新功能：`<prefix>/feat-简短名称`
   - Bug 修复：`<prefix>/fix-简短名称`
   - 文档或维护：`<prefix>/docs-简短名称` 或 `<prefix>/chore-简短名称`
   - `<prefix>` 由团队或所使用的开发工具约定，例如团队缩写、用户名或工具名；不要把某一种工具名称写死为项目唯一前缀。
4. 分支名使用简短、稳定、可读的描述。后续操作都在任务分支上完成，不要直接提交到 `main`。

### 修改过程中

- 先阅读相关源码、`docs/G3MAX_BATTERY_PROTOCOL.md` 和宿主 API 文档，再确定方案；保持改动范围与任务一致，不顺手重构无关代码。
- AI 设计方案、分析记录、临时补丁、截图和调试输出统一放在仓库根目录的 `.ai-work/` 下。这些文件是本地缓存，不属于交付内容，不要 `git add` 或提交。
- 其他工具产生的无关产物也不得进入仓库，包括 `.trae-html-share-packages/`、`.superpowers/`、`.uploads/`、HTML 预览包、截图、日志、PID/状态文件、个人配置、IDE 文件、构建输出和缓存；发现这类内容时应删除或移出工作区，不要提交。
- 只有明确属于项目交付物的源码、文档、资源和经过确认的发布产物才加入暂存区。提交前逐项检查 `git status` 和暂存区，避免使用无法确认内容的批量暂存命令。
- 不要擅自修改或删除用户已有改动；不确定文件是否应纳入任务时，先保留并询问。

### 完成任务前

在提交前完成以下检查：

1. 查看 `git diff`、`git diff --stat` 和 `git status --short`，确认差异只包含本任务内容。
2. 执行 `git diff --check`，修复空白错误；按项目约束进行源码审阅和可用的远程构建/测试。不要在用户本机执行 `dotnet build`、`dotnet test` 或其他本地编译流程。
3. 再次确认 `.ai-work/`、截图、日志、构建目录、个人配置和敏感信息没有进入暂存区。不要使用无法逐项确认内容的 `git add -A`。
4. 功能或修复完成且检查通过后再提交。一个提交应表达一个完整目的，避免把未完成方案或无关格式化混入提交。

### 提交信息与后续操作

- 提交信息使用清晰的约定式格式：`<type>(<scope>): <简短摘要>`，例如 `fix(hid): 修复重枚举后缓存路径失效`、`docs(protocol): 补充充电字段抓包结论`。
- `type` 优先使用 `feat`、`fix`、`docs`、`refactor`、`test`、`chore`；摘要说明实际结果，避免使用「更新代码」「一些修改」等无信息描述。必要时在提交正文说明原因、行为变化和验证方式。
- 提交后记录分支名、提交 ID、验证结果和未提交文件状态。除非用户明确要求，不要自动推送、合并、删除分支或改写已有提交。

## 开发约束

- 运行环境为用户本机时，**不要在用户本机执行 `dotnet build`、`dotnet test` 或其他本地编译流程**；应以源码审阅为主，并说明无法在本机验证构建。
- 当前运行环境若是云电脑、CI runner 或用户明确选定的远程构建平台，则应在该环境中自行检查并下载所需的 .NET SDK、宿主源码、NuGet 依赖和其他构建依赖，完成可行的构建与验证；不得把「禁止用户本机编译」误解为「禁止云环境构建」。
- 远程构建需要生成或更新 DLL 时，说明构建是否成功以及产物位置；仅将经过确认的发布产物放入 `releases/`，不要提交 `bin/`、`obj/` 或临时构建缓存。
- 仓库当前没有 CI 构建工作流或测试工程；`releases/` 目录中的预编译 DLL 对应最近一次远程构建，不代表每次源码改动都已通过构建或测试。
- 改动宿主接口调用前，先查阅宿主对应版本的官方 API 文档和源码。不要仅凭其他 NotchPeninsula 版本的接口猜测。
- 尽量保持插件仍以单个 `NPS.HecateBattery.dll` 分发；不要把宿主运行时已提供的依赖（`SkiaSharp.dll` 等）复制进插件目录。
- **插件 ID `com.snow.hecatebattery` 发布后不可更改**；入口程序集名 `NPS.HecateBattery` 与之绑定，不要顺手重命名。

## 项目与宿主

- 插件 ID：`com.snow.hecatebattery`
- 当前插件版本：`1.3.0`
- 目标框架：`net10.0-windows10.0.19041.0`（必须与宿主一致）
- 插件入口：`NPS.HecateBattery.HecateBatteryPlugin`（public，实现 `INotchPlugin`）
- 宿主 API 命名空间：`NotchPeninsula.Plugins`；字体与日志在 `NotchPeninsula`（`FontConfig` / `Logger`，非冻结契约，全部 try/catch 包裹）
- 目标宿主版本：**NotchPeninsula 正式版 2.1.0**（GitHub tag `NPS-v2.1.0`，tag 发布于 2026-10-24）。

⚠️ **必须按 tag 拉取宿主源码，不要用 `main`**：`main` 是未发布的开发版，API 可能继续演进；用非目标 tag 的源码编出的 DLL 放进正式版可能加载失败。2.1.0 的框架更新是把 AutoWidth 协议转正：`IAutoWidthWidget`、`WidgetKit`（`Text`/`Icon`/`Gap`/`Row`/`Stack`/`Min`、`Typeface`、`ContentRect`）与 `IPluginHost.GetSlotBudget` 已是正式 API；老协议 `MeasureWidth` 仍被兼容，但新代码应走 AutoWidth。本插件 1.3.0 起已迁移到 `IAutoWidthWidget`。

官方参考：

- [NotchPeninsula 仓库](https://github.com/GEORGEWWWU/NotchPeninsula)
- [NPS-v2.1.0 tag](https://github.com/GEORGEWWWU/NotchPeninsula/releases/tag/NPS-v2.1.0)
- 该 tag 内的 `Plugin/PluginApi.cs`、`Plugin/WidgetKit.cs` 与根目录 `NPS_PluginsAPI.md` 是插件 API 的唯一权威来源。
- 若目标版本有对应 tag，应优先使用该 tag 的 API 文档和宿主源码，不要默认 `main` 与已发布版本完全一致。宿主发布新版本后，先 diff 新 tag 的 `PluginApi.cs` 与 `WidgetKit.cs`，确认本仓库用到的成员没有改名/删除，再决定是否重编。

## 当前实现结构

仓库布局如下。`.csproj` 保留在仓库根目录以维持 `..\NotchPeninsula` 相对引用；源码按职责放在 `src/` 子目录，但所有文件统一使用 `NPS.HecateBattery` 命名空间（不随文件夹拆分），跨文件引用无需 using：

```text
NPS.HecateBattery/
├── NPS.HecateBattery.csproj
├── src/
│   ├── HecateBatteryPlugin.cs        # 插件入口：注册、轮询调度、提醒、样式切换、生命周期
│   ├── Core/BatterySnapshot.cs       # LinkState + 不可变电量快照
│   ├── Core/PluginSettings.cs        # WidgetStyle 枚举 + %APPDATA% 设置持久化
│   ├── Hardware/NativeMethods.cs     # Win32 P/Invoke（kernel32/setupapi/hid）
│   ├── Hardware/HecateHid.cs         # HID 枚举、查询、回包解析（协议实现）
│   ├── Rendering/PluginFont.cs       # 从宿主 FontConfig 取字体（不缓存、不 Dispose）
│   └── UI/
│       ├── HecateWidget.cs           # 灵动岛折叠态组件（耳机剪影 + 电池，三种样式）
│       └── HecateDetailPage.cs       # 右键详情页（340×148 设计坐标，含样式切换）
├── docs/
│   └── G3MAX_BATTERY_PROTOCOL.md     # 通用 G3MAX 电量获取协议说明（不绑定本项目）
├── releases/
│   └── NPS.HecateBattery.dll          # 预编译分发产物
├── AI_DEVELOPMENT.md
├── README.md
├── LICENSE
└── .gitignore
```

### `src/HecateBatteryPlugin.cs`

实现 `INotchPlugin` 和 `IDisposable`。`Initialize` 只做四件事：读回持久化样式、注册组件、挂 `ScheduleRefresh(30s)` 定时器、投递一次初始轮询到线程池（**不在 Initialize 里做 HID I/O**，最坏 1.5 s）。轮询用 `Interlocked` 门闩防重入；轮询结果写入不可变 `BatterySnapshot` 快照（`Volatile.Read/Write` 交接），状态或电量变化时调 `InvalidateWidgetLayout` 让宿主重测宽度。`SetWidgetStyle` 处理详情页的样式切换：写持久化 + `InvalidateWidgetLayout`，样式不变时直接返回。

失败语义：`-3`（收到合法状态帧且 `byte[5]==0x00`，即耳机已关机/超出 2.4G 范围）是**明确状态**，立即切换到 `Offline`——岛上组件自动隐藏（`Layout` 返回 `null`，宿主按 0 宽处理：不分配空间、不绘制、不注册命中区），不走防抖，也绝不显示 0%；开机重连后下一轮轮询（≤ 30 s）自动恢复显示。其余失败连续 2 次才把界面切到 `--`（避免偶发超时闪屏）；`-1`（设备不存在）显示「未连接」，`-2`（设备在、无响应）显示「设备无响应」。日志用 `_lastLoggedState` 去重，避免每 30 秒刷屏。

低电量提醒：`level <= 20` 且比上次提醒值低 4 以上时 `PostReminder` 一条 5 秒提醒；回到 `> 25` 后重置。

### `src/Core/BatterySnapshot.cs`

`LinkState`（Reading/Ok/Offline/Disconnected/Error；`Offline` = 收到合法离线帧、耳机关机/超出范围，组件自动隐藏）与 `BatterySnapshot(Level, State, UpdatedUtcTicks)` record。跨线程只传不可变快照，绘制层不加锁。

### `src/Core/PluginSettings.cs`

`WidgetStyle` 枚举（Classic / Capsule / Minimal）与 `SettingsStore`。样式持久化落到 `%APPDATA%\NotchPeninsula\plugins\com.snow.hecatebattery\settings.json`（System.Text.Json 单字段，读写全部 try/catch，任何异常回落默认 Classic，绝不影响电量显示），不依赖宿主的 `GetSetting`/`SetSetting`，读写失败也不影响电量显示。`Load` 只在 `Initialize` 调一次，`Save` 只在样式实际变化时调。

### `src/Hardware/NativeMethods.cs` 与 `src/Hardware/HecateHid.cs`

Win32 P/Invoke 与协议实现。⚠️ `OVERLAPPED.Internal/InternalHigh` 必须声明为 `IntPtr`——写成 `uint` 会在 x64 下让 `hEvent` 偏移错位，所有 `ReadFile` 报 `ERROR_INVALID_HANDLE (6)`。

`HecateHid.Poll` 返回值约定：`0..100` 成功（耳机在线）；`-1` 设备不存在；`-2` 设备在但无有效回包；`-3` 收到合法状态帧但 `byte[5]==0x00`（耳机已关机/超出 2.4G 范围，此时 `byte[6]` 不是电量）。查询/回包报文、校验规则（含 `report[5]` 链路标志）、时序参数（读窗 300 ms、重试 4×100 ms、轮询 ≥ 5 s）**一律以 `docs/G3MAX_BATTERY_PROTOCOL.md` 为准**，不要凭记忆改动协议常量。

状态帧解析统一走 `HecateHid.TryParseStatusReport`（纯函数，不碰 Win32），它对离线帧有回归测试覆盖（`.ai-work/parse-test/` 本地工程链接源码运行，不入库）：核心断言是「离线帧绝不能被解析成在线 + 0% 电量」，改动解析逻辑前先跑一遍。

`CancelIo` 仅在「读请求与超时收尾在同一线程」的前提下安全；若改成跨线程读，换 `CancelIoEx`。

### `src/Rendering/PluginFont.cs`

宿主字体是共享资源：每次绘制按需现取、不缓存、绝不 `Dispose`；`FontConfig`/`Logger` 是非冻结契约，全部 try/catch，允许永久降级为 Skia 默认字体/无日志。2.1.0 起 AutoWidth 的测量字体由宿主 `WidgetKit.Typeface` 统一；本组件布局全是定宽盒子、不依赖字体，`PluginFont` 只服务 `Draw`，取的档位与 `WidgetKit.Typeface` 一致（SemiBold ↔ `FontConfig.SemiBold`）。

### `src/UI/HecateWidget.cs` 与 `src/UI/HecateDetailPage.cs`

折叠态组件（含宿主 1px 两侧留白后槽位约 47–49 px 宽）造型为「左侧耳机剪影 + 右侧电池、数字内嵌电池内部」（宿主无 emoji 字体，耳机用 SkiaSharp 路径绘制剪影），组件宽度恒定、数字超宽（如 `100%`）时等比缩小，不随数字抖动。**宿主 2.1.0 AutoWidth 协议**：组件实现 `IAutoWidthWidget`，不再写 `MeasureWidth`——`Layout` 返回 `WidgetKit.Row(Icon(HeadsetW), Gap(Gap), Icon(bodyW + CapBlock))` 定宽盒子，宽度由宿主统一测量（内容向上取整 + 两侧 `AutoPadX` 留白）；`Draw` 用 `WidgetKit.ContentRect(rect)` 取内容区定位。宽度只有「恒定 / 0（隐藏）」两态，`WidthVaries` 保持默认 `false`（显示/隐藏即时切换，无收窄迟滞）。组件有三种样式（`WidgetStyle`）：**Classic**（白描边电池 + 进度填充）、**Capsule**（强调色 `#56C8FF` 胶囊描边 + 内衬染色）、**Minimal**（常态 55% 线框 + 92% 数字的低调细框，低电量恢复全亮度；耳机剪影同步改为线条描边）。切换链路：详情页 `OnAction("style.*")` → `SetWidgetStyle`（持久化 + `InvalidateWidgetLayout`）。

电量 ≤ 20% 用警示红 `#F05A5A`；耳机关机/超出范围（`Offline`）时 `Layout` 返回 `null`（0 宽）、`Draw` 直接返回，宿主据此自动隐藏组件（不占宽、不绘制、不可命中），开机重连后自动恢复；接收器异常显示灰色 `--`；左键触发一次手动刷新（线程池，不阻塞渲染），右键由宿主展开详情页。

详情页按固定设计坐标 340×148 排版，绘制时按 `rect` 等比缩放——宿主展开动画期间 `rect` 是插值尺寸，不能拿它当排版基准。详情页右上角「刷新」按钮、底部「组件样式」三按钮（命中值 `style.classic` / `style.capsule` / `style.minimal`，选中态高亮描边）。组件槽位宽度被宿主缓存，任何影响宽度的状态变化（`Offline` ↔ 在线、样式切换改变电池主体宽）都必须触发 `InvalidateWidgetLayout`；数字内容（`96%` → `100%`、Ok → `--`）不影响宽度，无需通知。

## 线程与生命周期约定

| 阶段 | 线程 | 约束 |
|---|---|---|
| `Initialize` | 宿主加载线程 | 不做 HID I/O、不阻塞 |
| `ScheduleRefresh` 回调 | 宿主 Timer 线程 | 同步 HID 轮询（最坏 1.5 s）后写快照 |
| 手动刷新 | 线程池 | 门闩防重入，进行中直接丢弃 |
| `Layout` / `Draw` | 渲染线程 | `Layout` 只读快照、返回纯盒子（无 I/O、无锁、不耗时）；`Draw` 只读快照 + 绘制 |
| `Dispose` | 卸载线程 | 置 `_disposed`、停定时器；在途轮询自行结束并关句柄 |

- `SKPaint` 等绘制资源**每帧临时创建、用完即释放**，绝不缓存成字段跨线程复用（否则卸载/热重载时会 `0xC0000005`）。
- 组件被隐藏时插件仍在运行；不能把「岛上看不到」当成卸载。

## HID 协议边界

协议只对 **VID 0x35BB / PID 0xA226 的 G3 MAX 2.4G 接收器**负责。充电状态字段**未确认，禁止猜测实现**；要扩展前必须对照官方客户端在充电/未充电两种状态下抓包对比。完整报文表、反例集合、频率限制见 `docs/G3MAX_BATTERY_PROTOCOL.md`——该文档面向所有开发者，与宿主无关，改动协议行为时同步更新它。

## 项目引用与依赖

`NPS.HecateBattery.csproj` 通过相对路径引用 `..\NotchPeninsula\NotchPeninsula.csproj`，并设置 `Private="false"`。SkiaSharp 固定 2.88.8（与宿主 v2.1.0 一致）且 `ExcludeAssets="runtime"`，仅作编译引用，运行时由宿主提供；插件不随带任何宿主已有程序集。

如果远程环境需要构建，两个仓库必须按相对路径并列放置，不能把插件源码放进宿主仓库目录（宿主项目的默认源文件通配可能把插件代码一并编译进宿主）：

```text
工作目录/
├── NotchPeninsula/        # 按 tag NPS-v2.1.0 克隆的宿主源码
└── NPS.HecateBattery/      # 本插件仓库
```

```bash
git clone --branch NPS-v2.1.0 --depth 1 https://github.com/GEORGEWWWU/NotchPeninsula.git
```

在云电脑、托管 Windows runner 或其他所选远程开发平台中，先确认或安装 .NET 10 SDK，再进入插件目录构建。`InstallToHostPlugins` 目标会把 DLL 复制到宿主输出目录的 `plugins` 文件夹。若当前环境是用户本机，不要运行下面的构建命令；若当前环境是云电脑或其他授权的远程环境，则应运行并验证构建结果：

```powershell
dotnet build NPS.HecateBattery.csproj -c Release
```

构建产物预期位于 `bin\Release\net10.0-windows10.0.19041.0\NPS.HecateBattery.dll`。生成产物后，先核对它与目标宿主 API/运行时版本匹配，再通过远程平台提供的方式交付，并更新 `releases/` 中的分发 DLL。

Linux/macOS 环境构建说明：csproj 里的 `EnableWindowsTargeting=true` 只作用于本工程，引用的宿主工程不会自动继承，因此非 Windows 上构建必须显式传该属性；宿主工程含 `PublishSingleFile=true`，还会因 RID 解析报 `NETSDK1082`，需一并关闭：

```bash
dotnet build NPS.HecateBattery.csproj -c Release -p:EnableWindowsTargeting=true -p:PublishSingleFile=false -p:SelfContained=false
```

（也可改用 `-r win-x64` 解决 RID 问题，产物位于 `bin/Release/net10.0-windows10.0.19041.0/win-x64/`。）

## 自检清单（交付前）

- `dotnet build -c Release` 0 error；
- DLL 的 `AssemblyName` 与文件名一致（`NPS.HecateBattery`）；
- 源码不含硬编码电量值（如 `96`）、MAC 地址、机器专属路径；
- `HecateWidget` 实现 `IAutoWidthWidget`：`Layout` 只返回 `WidgetKit` 工厂构造的盒子（不自己派生 `Box`），不做 I/O、加锁或耗时计算；隐藏语义用返回 `null` 表达，不用 0 宽盒子；
- `Id` 仍为 `com.snow.hecatebattery`；
- `releases/` 中的 DLL 与当前源码构建产物一致。

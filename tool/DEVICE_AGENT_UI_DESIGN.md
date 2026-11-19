# DeviceAgent WinUI3 桌面端设计（设备数据源与 JS 解析脚本）

> 状态：设计稿，未实施（2026-11-16）。宿主形态已确定：现有控制台 + WinForms 托盘改为 **WinUI3 一体化托盘应用**，界面、托盘与采集运行时同进程；关闭主窗口默认最小化到托盘，可设置开机启动。
>
> 关联：[`DEVICE_AGENT_DESIGN.md`](DEVICE_AGENT_DESIGN.md)（采集链路与协议基线）、[`README.md`](README.md)（当前实现与运行方式）。
>
> 本轮只交付设计与关键页面 XAML 骨架，不新建工程、不修改运行代码。

## 1. 目标与决策

### 1.1 已确认决策

| 决策项 | 结论 |
| --- | --- |
| 宿主形态 | WinUI3 一体化宿主：UI、托盘、采集运行时在同一个 `Sim.DeviceAgent.App.exe` 进程内 |
| 窗口行为 | 关闭主窗口默认最小化到托盘；只有托盘“退出”或设置页显式退出才结束进程 |
| 开机启动 | 设置页开关，写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`；带 `--autostart` 启动时只驻留托盘 |
| 配置事实 | 继续以 `agent.json` 为唯一运行配置；UI 结构化编辑后原子写回并触发现有热加载 |
| 解析脚本 | 新增 `script` Codec：Jint 进程内沙箱执行 JS，用于帧/文件解析；自定义切帧仍留 P2 |
| 管理面 | UI 直接调用进程内运行时服务；本机 HTTP/WebSocket 接口继续服务浏览器页面，不要求 UI 走回环 HTTP |

### 1.2 页面范围

- 设备数据源：串口、TCP 客户端、文件目录的创建、编辑、删除、绑定与监听测试。
- 设备通道：设备信息、源绑定、解析器选择、出口选择与运行诊断。
- 解析脚本：脚本库、Monaco 编辑器、API 文档、样例测试台、脚本与设备绑定。
- 支撑页面：概览、实时数据与命令台、日志、设置（含开机启动）。

### 1.3 非目标

- 不做平台设备台账、账号权限与中心管理页；这些属于平台侧（P3）。
- 不做远程管理入口；本机监听仍只绑定回环地址，不因 UI 改变安全边界。
- 不在 UI 内做业务判定（稳定判定、超差判定、工单绑定）。
- P1 的 `script` Codec 只做“切帧后解析 / 整文件解析”，不支持脚本自定义帧边界；流式大文件解析也留 P2。
- 不改变“一源一设备”模型：一个串口、TCP 连接或目录仍只对应一台设备。

## 2. 总体架构

### 2.1 进程结构

```text
┌─ Sim.DeviceAgent.App.exe（WinUI3，非打包，单实例） ─────────────────────────┐
│                                                                              │
│  UI 线程（WinUI3）                         后台线程池（Generic Host + Kestrel）│
│  ┌─────────────────────────────┐           ┌────────────────────────────────┐ │
│  │ AppShell / NavigationView   │           │ DeviceAgentRuntime             │ │
│  │ Dashboard / Devices         │  facades  │ Sources：serial/tcpClient/file │ │
│  │ Sources / Scripts / Live    │◄─────────►│ Codecs：delimiter/csv/script   │ │
│  │ Logs / Settings             │           │ ScriptHost（Jint 沙箱）        │ │
│  │ WebView2 + Monaco（脚本）   │           │ Sinks：wsServer/wsClient/spool │ │
│  └─────────────────────────────┘           │ ConfigWatcher / Reloader       │ │
│                                            │ 本机 /ws、/api/status          │ │
│  H.NotifyIcon 托盘                          └────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────────┘
          │                                                     │
          ▼                                                     ▼
   工位浏览器页面（ws://127.0.0.1:18080/ws）              中心网关（wsClient + spool）
```

- UI 线程不执行采集、解析、文件扫描或磁盘 spool。
- 采集运行时仍是现有 Generic Host + Kestrel；WinUI3 只替换“进程入口 + 宿主外壳 + 托盘”。
- 解析脚本在设备通道的管道线程上执行，超时或异常只影响该通道，不阻塞 UI。

### 2.2 工程拆分

| 现状 | 目标 |
| --- | --- |
| `src/Sim.DeviceAgent`（Web SDK 可执行 + WinForms 托盘） | 拆为 `Sim.DeviceAgent.Core`（类库）与 `Sim.DeviceAgent.App`（WinUI3 可执行） |
| `src/Sim.DeviceAgent.Contracts` | 保持不变 |
| `tests/Sim.DeviceAgent.Tests` | 改为引用 Core |

```text
tool/src/
├── Sim.DeviceAgent.Contracts/     # 消息信封、插件契约，不引用 UI
├── Sim.DeviceAgent.Core/          # 现 Sim.DeviceAgent 的 Configuration/Sources/Codecs/Runtime/Spool/WebSockets
│                                  # 新增 Scripting/、Diagnostics/、Hosting/（Kestrel + Generic Host 组装）
└── Sim.DeviceAgent.App/           # WinUI3 宿主
    ├── App.xaml(.cs)              # DI、单实例、运行时启动、全局异常
    ├── AppShell.xaml(.cs)         # NavigationView、标题栏、状态栏、托盘
    ├── Views/                     # 各 Page
    ├── ViewModels/
    ├── Controls/                  # 状态胶囊、源摘要、JSON 树
    ├── Services/                  # RuntimeFacade、ConfigWorkspace、ScriptWorkspace、StartupRegistration 等
    └── Assets/monaco/             # 离线 Monaco 资源
```

- 命名空间继续使用 `Sim.DeviceAgent.*`，便于与现有代码和文档对齐。
- `Sim.DeviceAgent.slnx` 由两个工程变为三个工程；`Directory.Packages.props` 增加 WinUI、Jint、WebView2、托盘包。

### 2.3 启动、单实例与退出

启动顺序：

1. 解析命令行：`--config <path>`；未指定时读 `SIM_DEVICE_AGENT_CONFIG`；再回退到应用数据目录的 `agent.json`。
2. 读取 `ui-settings.json`（窗口尺寸、主题、关闭行为、上次页面）。
3. 注册单实例键（Windows App SDK `AppInstance`）；已有实例则激活其主窗口并退出当前进程。若目标 Windows App SDK 版本对非打包单实例支持不完整，使用命名 Mutex + 命名管道兜底。
4. 在后台启动运行时宿主（Kestrel + 通道）；同时创建主窗口。
5. 配置校验失败时**不退出进程**：主窗口直接打开“配置错误”状态（InfoBar + 错误列表 + 打开配置文件/重新加载按钮），运行时保持停止。
6. 带 `--autostart` 启动时不显示主窗口，只创建托盘；托盘菜单可打开主界面。
7. 托盘初始化失败只记录警告，不阻止采集（沿用现有策略）。

退出：

- 托盘“退出”：停止接收新数据 → 停止通道与出口 → flush spool → 关闭托盘与窗口，最长等待 5 秒。
- 窗口关闭：按设置执行“最小化到托盘”（默认）或“退出应用”；首次最小化时托盘气泡提示“仍在后台采集”。
- 系统注销/关机：尽力优雅停止；无法完成时依赖 spool 重启补传，不承诺该窗口内零丢失。
- 进程内不做 Windows 服务托管；Windows 服务形态仍属后续阶段。

### 2.4 UI 与运行时的接口

| 接口 | 职责 |
| --- | --- |
| `IRuntimeFacade` | 获取通道/出口状态快照、最近事件、命令事务发送、请求配置重载 |
| `IConfigWorkspace` | 读取当前 `agent.json` 快照、构造编辑草稿、校验、原子保存、检测外部修改 |
| `ILiveTap` | 每设备有界帧环形缓冲（默认 200 帧），供实时数据页与脚本样例捕获，不干扰采集 |
| `IScriptWorkspace` | 枚举/保存/编译/测试脚本，查询脚本引用关系与生效版本 |
| `ISourceProbe` | 对未绑定源做限时监听测试（串口/TCP/文件），输出原始帧预览 |
| `IStartupRegistration` | 读写开机启动注册表项，校验 exe 路径有效性 |
| `IUiSettingsStore` | `ui-settings.json` 读写 |
| `ITrayController` | 托盘图标、菜单、气泡、窗口显隐 |

- 运行时向 UI 推送事件时统一经 `DispatcherQueue.TryEnqueue`；高频帧只进 `ILiveTap` 环形缓冲，UI 以约 10 Hz 批量拉取，暂停时只停止 UI 拉取。
- 状态页以 1 秒轮询快照为主，避免高频事件导致 UI 抖动；配置重载、脚本编译等低频结果用事件即时提示。
- 浏览器页面继续使用现有 `/ws`、`/health`、`/api/status`；UI 不依赖这些接口，因此在关闭本机监听配置前也能完整管理。

## 3. 导航与整体布局

```text
┌ Device Agent · A 线测试上位机 ──────────────────────── [运行中]  ─ □ × ┐
├──────────────┬─────────────────────────────────────────────────────────┤
│  概览         │                                                         │
│  设备         │                    页面内容                              │
│  数据源       │                                                         │
│  解析脚本     │                                                         │
│  实时数据     │                                                         │
│  日志         │                                                         │
│              │                                                         │
│  设置         │                                                         │
│ ● 运行中 3/3 │ ws://127.0.0.1:18080 · 中心已连接 · 待补传 0            │
└──────────────┴─────────────────────────────────────────────────────────┘
```

| 页面 | 导航键 | 核心职责 |
| --- | --- | --- |
| 概览 | Ctrl+1 | Agent 身份、运行状态、设备卡片、告警、最近事件 |
| 设备 | Ctrl+2 | 通道列表、设备信息、源绑定、解析器、出口、诊断与命令测试 |
| 数据源 | Ctrl+3 | 串口/TCP/文件源的创建、编辑、删除、绑定关系与监听测试 |
| 解析脚本 | Ctrl+4 | 脚本库、Monaco 编辑、测试台、API 文档、引用设备 |
| 实时数据 | Ctrl+5 | 按设备查看原始帧/解析结果、暂停/过滤、命令事务控制台 |
| 日志 | Ctrl+6 | 结构化日志查看、级别/设备过滤、跟随与导出 |
| 设置 | Ctrl+, | Agent 身份、本机 WS、中心出口、数据目录、桌面体验、开机启动、关于 |

- 导航底部固定显示 Agent 状态胶囊、在线设备数、本机 WS 地址与中心出口状态。
- 所有页面共用“配置错误”InfoBar 与“正在应用配置”进度条。
- 深色/浅色/跟随系统主题；Mica 背景；状态颜色同时配图标与文字，不单靠颜色区分。

## 4. 页面设计

### 4.1 概览

```text
Agent · agent-line-a-01                         [重新加载配置] [打开配置目录]
┌ Agent 状态 ─┐ ┌ 在线设备 ──┐ ┌ 中心出口 ──┐ ┌ 本机 WebSocket ──────────┐
│ 运行中 02:14│ │ 3 / 3      │ │ 已连接     │ │ ws://127.0.0.1:18080  [复制]│
└─────────────┘ └────────────┘ │ 待补传 12  │ └───────────────────────────┘
┌ 告警 ───────────────────────────────────────────────────────────────────┐
│ 警告：DS-001 连续解析失败 23 次，通道已降级。          [查看设备]        │
└─────────────────────────────────────────────────────────────────────────┘
设备                                          [全部] [在线] [异常] [搜索…]
┌───────────────────┐ ┌───────────────────┐ ┌───────────────────┐
│ 焊线拉力测试机 01  │ │ Die 焊接推力 01   │ │ 拉力测试机 02      │
│ PT-001 · pullTest │ │ DS-001 · dieShear │ │ PT-002 · pullTest │
│ ● 已连接           │ │ ● 已连接           │ │ ● 已连接           │
│ 2 秒前 · 12 帧/分  │ │ 5 秒前 · 8 帧/分   │ │ 1 分钟前 · 文件    │
│ 丢弃 0 · 解析错 0  │ │ 丢弃 0 · 解析错 23 │ │ 丢弃 0 · 解析错 0  │
└───────────────────┘ └───────────────────┘ └───────────────────┘
最近事件
14:02:11  配置已重新加载（3 个通道）
14:01:58  DS-001 解析失败：unexpected frame
13:59:03  PT-002 处理文件 PT02_20261116.csv，产出 12 条
```

- 设备卡片点击进入“设备”页对应通道；异常卡片直接展开诊断 Tab。
- 告警来源：配置 `lastError`、`degraded`、`silent`、spool 配额、源断线、脚本编译失败。
- “在线”定义沿用运行时状态：`connected` 视为在线，`waiting/degraded/faulted` 归入异常，`stopped` 单独展示。

### 4.2 设备与通道

左侧主列表 + 右侧详情，沿用“一源一设备”模型。

```text
设备                                             [+ 新建设备] [搜索…]
┌──────────────────┬──────────────────────────────────────────────────────┐
│ PT-001           │ 焊线拉力测试机 01        ● 已连接      [启用] [实时] │
│ 焊线拉力机 01     │ PT-001 · pullTest · pt-01                           │
│ COM3 · 已连接     ├──────────────────────────────────────────────────────┤
│                  │ [基本信息] [数据源] [解析器] [出口] [诊断]            │
│ DS-001           │                                                      │
│ Die 推力机 01     │  设备编号  PT-001        设备类型  pullTest ▼        │
│ TCP · 已连接      │  设备名称  焊线拉力测试机 01                          │
│                  │  通道 ID   pt-01         队列容量  1024              │
│ PT-002           │  启用      ●────────                                  │
│ 文件拉力机 02     │                                                      │
│ 文件 · 已连接     │  [保存]  [放弃修改]                                   │
└──────────────────┴──────────────────────────────────────────────────────┘
```

基本信息：

| 字段 | 控件 | 说明 |
| --- | --- | --- |
| `channel.id` | 文本框 | 必填，全局唯一，仅 `[A-Za-z0-9._-]` |
| `deviceId` | 文本框 | 必填，同一 Agent 下唯一，建议取平台台账设备编号 |
| `deviceType` | 可编辑下拉 | 预置 `scale` / `pullTest` / `dieShear`，允许填写新类型 |
| `deviceName` | 文本框 | 展示名 |
| `enabled` | 开关 | 关闭后该通道不参与启动校验与运行 |
| `queueCapacity` | 数字框 | 16–1000000 |
| `sinks` | 多选 | 至少选择一个出口 |

数据源 Tab：

- 显示当前绑定的源摘要（类型、连接参数、归档目录等）与状态。
- “更改数据源”只列出未绑定其他启用通道的源；已绑定源显示占用方设备。
- “新建数据源”打开数据源编辑对话框，保存后自动回填并绑定。
- 同一源绑定第二台启用设备时，保存前直接报错，提示“一源一设备”。

解析器 Tab：

| 类型 | 适用源 | 主要字段 |
| --- | --- | --- |
| `delimiter` 分隔符 + 正则 | 全部 | 分隔符、编码、正则命名组、单帧上限；正则旁提供样例测试 |
| `passthrough` 直通 | 全部 | 编码 |
| `csv` | 仅文件 | 列分隔符、编码、表头映射（JSON 键 → 表头名） |
| `script` JS 脚本 | 全部 | 脚本选择、入口函数、超时、切帧分隔符（串口/TCP）、文件模式（文件）、单帧上限 |

- 选择 `script` 时显示“编辑脚本”链接，跳转解析脚本页并定位到该文件。
- 未选择脚本、脚本不存在、脚本编译失败时不允许“保存并应用”，错误显示在字段下方。
- 解析器切换后保留原配置字段，但只序列化当前类型使用的字段，避免污染 `agent.json`（见 5.4）。

诊断 Tab：

- 状态、最后数据时间、`seq`、接收/丢弃/解析失败计数、最近错误原文。
- 命令测试：文本载荷（支持 `\r\n` 转义）、超时、匹配正则、发送；右侧显示 `requestId`、响应原文与解析结果、耗时；历史保留最近 20 条。
- “查看实时数据”跳转到实时数据页并按该设备过滤。
- 命令测试直接复用 `DeviceChannel.SendCommandAsync`，与 WebSocket 命令事务共享单通道串行约束。

新建设备向导（3 步）：

1. 基本信息：通道 ID、设备编号、类型、名称。
2. 数据源：选择未绑定源或新建源。
3. 解析器与出口：选择解析器（含脚本）、出口、队列容量。

保存时执行整份配置校验与脚本编译校验，通过后原子写入并触发热加载。

### 4.3 数据源（重点）

#### 4.3.1 列表

```text
数据源                                      [刷新端口] [测试] [+ 新建数据源]
┌─────────────────────────────────────────────────────────────────────────────┐
│ 类型   ID             连接摘要                        绑定设备   状态   操作  │
│ 串口   com3-pulltest  COM3 · 9600 · 8-N-1            PT-001    已连接  编辑  │
│ TCP    tcp-dieshear   192.168.1.50:5000              DS-001    已连接  编辑  │
│ 文件   dir-pulltest   D:\device\in · PT02_*.csv      PT-002    已连接  编辑  │
│ 串口   com5-spare     COM5 · 115200 · 8-N-1          未绑定    —      绑定  │
└─────────────────────────────────────────────────────────────────────────────┘
```

- 类型图标 + 摘要：串口显示端口/波特率/数据位-校验-停止位；TCP 显示 `host:port`；文件显示目录 + 匹配模式。
- 绑定设备显示 `deviceId`（点击跳转设备页）；未绑定源允许删除，已绑定源删除前要求先解绑。
- 状态列只在绑定后显示通道实时状态；未绑定显示“—”。
- 顶部“刷新端口”只刷新串口列表，不重启通道。
- 多选不支持批量删除；删除前二次确认并说明对通道的影响。

#### 4.3.2 新建/编辑对话框

对话框为单页表单，按类型切换字段，ID 创建后不可修改（避免影响 `sourceRef`）。

串口：

| 字段 | 控件 | 默认 | 校验 |
| --- | --- | --- | --- |
| `id` | 文本框 | — | 必填，唯一，`[A-Za-z0-9._-]` |
| `type` | 分段选择 | serial | 固定 |
| `port` | 可编辑下拉 + 刷新 | — | 必填；显示 `COM3 (USB-SERIAL CH340)` 形式友好名 |
| `baudRate` | 可编辑下拉 | 9600 | 300–921600；预置 9600/19200/38400/57600/115200 |
| `dataBits` | 下拉 | 8 | 5–8 |
| `parity` | 下拉 | None | None/Odd/Even/Mark/Space |
| `stopBits` | 下拉 | One | One/OnePointFive/Two |
| `dtrEnable` | 开关 | 关 | — |
| `rtsEnable` | 开关 | 关 | — |
| `readTimeoutMs` | 数字框 | 200 | 50–10000 |

- 串口为独占资源：若被其他程序占用，监听测试会给出“访问被拒绝/端口占用”的明确提示。
- 端口列表通过 `SerialPort.GetPortNames` 获取，友好名从系统设备信息补充；对话框打开时每 2 秒刷新一次。

TCP 客户端：

| 字段 | 控件 | 默认 | 校验 |
| --- | --- | --- | --- |
| `host` | 文本框 | — | 必填，主机名或 IP |
| `port` | 数字框 | — | 1–65535 |
| `reconnectMinSeconds` | 数字框 | 1 | ≥1 |
| `reconnectMaxSeconds` | 数字框 | 30 | ≥ 最小值 |

文件：

| 字段 | 控件 | 默认 | 校验 |
| --- | --- | --- | --- |
| `directory` | 目录选择 | — | 必填；不存在时警告但允许保存 |
| `pattern` | 文本框 | `*.*` | 必填 |
| `stableSeconds` | 数字框 | 5 | ≥0.1 |
| `pollIntervalSeconds` | 数字框 | 5 | ≥0.1 |
| `archiveDirectory` | 目录选择 | — | 必填；与错误目录不同 |
| `errorDirectory` | 目录选择 | — | 必填；与归档目录不同 |

- 归档/错误目录限制在源目录外的明确路径，UI 只做“不相同”校验，路径穿越由运行时兜底。
- 网络共享路径允许输入 UNC；保存后由通道自行重连与补偿扫描。

#### 4.3.3 监听测试

- 仅对**未绑定**源开放“监听测试”，避免抢占串口/打断正在采集的 TCP 连接。
- 测试时长固定 10 秒（可提前停止）；显示原始文本与 HEX 双视图、帧数、接收字节数；串口/TCP 显示连接状态变化。
- 文件源测试执行一次补偿扫描，列出匹配文件、稳定判定结果与最近 20 条记录预览，不移动、不归档文件。
- 测试使用独立源实例与解析器，不进入正式通道、不写 spool、不发送出口。
- 测试结果可“送入脚本测试台”作为样例（见 4.4）。
- 已绑定源不提供断开测试，统一引导到“实时数据”页查看真实采集，避免生产中断。

### 4.4 解析脚本（重点）

#### 4.4.1 页面布局

```text
解析脚本                                 [新建] [导入] [格式化] [保存] [运行测试]
┌────────────────┬──────────────────────────────────┬─────────────────────────┐
│ 脚本库          │  pt-01.js              已生效 ●  │ [测试] [API 文档] [引用] │
│                │  // @apiVersion 1                │                         │
│ ● pt-01.js     │  // @name 焊线拉力机             │ 输入  帧 ▼             │
│ ● dieshear.js  │                                  │ ┌─────────────────────┐ │
│ ● pt02-file.js │  function parse(input) {         │ │ RESULT,FORCE=12.34N │ │
│                │    const m = /FORCE=(...)/...    │ └─────────────────────┘ │
│ 未绑定 1        │    ...                           │ [从实时帧插入] [运行]   │
│                │  }                               │ 状态：成功 · 1 条 · 3ms │
│                │                                  │ 记录：{force:12.34}     │
│                │                                  │ 日志：…                 │
└────────────────┴──────────────────────────────────┴─────────────────────────┘
```

- 左栏：`<configDir>/scripts/*.js` 文件列表，显示生效状态、引用设备数、未保存标记。
- 中栏：Monaco 编辑器（WebView2），语法高亮、括号匹配、查找替换、格式化、错误行标记。
- 右栏：测试台 / API 文档 / 引用关系三个标签页。
- 顶部工具栏：新建、导入、保存（Ctrl+S）、格式化、运行测试（Ctrl+Enter）、文件定位。

#### 4.4.2 脚本库

- 新建脚本：输入文件名（自动加 `.js`），生成带 `@apiVersion 1` 头的模板。
- 导入：从本地选择 `.js` 文件，复制到 `scripts/`，同名时提示覆盖或改名。
- 删除：被设备引用时禁止删除，列出引用设备并提供“先解绑”跳转。
- 重命名：被引用时同步更新通道 `codec.script` 并触发配置校验；实现阶段可先限制为“未引用才可重命名”。
- 外部编辑器修改：页面检测文件哈希变化；编辑器有未保存修改时提示“重新加载/保留本地修改”。

#### 4.4.3 编辑器与保存语义

- 保存写入 UTF-8（无 BOM）、CRLF（遵循仓库行尾约定）。
- 保存策略：
  1. 语法错误**允许保存**，避免丢失现场工作；状态标记“已保存，未生效”。
  2. 编译成功且被引用时热更新到绑定通道，状态标记“已生效”，显示应用结果。
  3. 编译失败时绑定通道继续使用上一次成功版本；首次启动即失败时该通道进入 `faulted`，其他通道不受影响。
  4. 配置保存（设备页）会校验脚本存在且可编译；引用无效脚本的配置不能应用。
- 未保存修改、外部修改、编译失败都通过编辑器顶部状态条与 InfoBar 明确展示。
- 保存后记录脚本 SHA-256 与编译结果到结构化日志，便于追溯“某次采集使用的脚本版本”。

#### 4.4.4 测试台

- 输入类型：帧 / 文件。
- 输入来源：
  - 手工粘贴文本；支持“文本 / HEX”切换，HEX 用于二进制设备。
  - 从实时帧插入：选择设备与最近帧（来自 `ILiveTap`，不暂停采集）。
  - 文件：选择本地文件，按脚本配置的文件模式读取；超过输入上限时提示。
- 展示：成功/过滤/错误状态、产出记录数、耗时、脚本日志、归一化后的记录 JSON、错误行号与消息。
- 测试使用独立引擎实例与相同资源限制，不修改生产通道的运行时状态。
- “测试”按钮同时校验入口函数存在、输出可 JSON 序列化。

#### 4.4.5 脚本与设备绑定

- 引用标签页列出使用该脚本的通道，提供“绑定到设备”入口：选择设备 → 将 `codec` 切换为 `script` 并带入默认参数 → 保存配置。
- 一个脚本可被多个设备引用；修改脚本会同时影响所有引用设备，保存前显示影响范围确认。
- 绑定关系保存在 `agent.json` 的通道 `codec.script` 字段，不额外维护映射文件。

### 4.5 实时数据与命令台

```text
实时数据                       [设备：全部 ▼] [暂停] [清空] [只看解析错误]
┌────────────────────────────────────┬────────────────────────────────────┐
│ 时间      设备     序号   摘要      │ 详情                                │
│ 14:22:01 PT-001  1024   FORCE=12.34│ 原文  RESULT,FORCE=12.34N          │
│ 14:22:01 DS-001   512   shear=8.76 │ HEX   52 45 53 55 4C 54 ...         │
│ 14:22:02 PT-001  1025   FORCE=12.35│ 解析  { "force": 12.34 }            │
│ 14:22:03 PT-002     8   file #3    │ 来源  PT02_...csv · 12.3 KB · idx 2 │
└────────────────────────────────────┴────────────────────────────────────┘
命令测试  [设备 pt-01 ▼] [载荷 READ\r\n] [超时 2000ms] [匹配 ^RESULT] [发送]
          请求 r-20261116-0007 · 成功 · 42ms · RESPONSE,FORCE=12.34N
```

- 数据源为 `ILiveTap` 环形缓冲（默认每设备 200 帧）；暂停只停止 UI 消费，不影响采集与出口。
- 支持按设备/通道过滤，保留最近事件与解析错误计数。
- 命令台与 WebSocket 命令事务共用同一实现；同一通道同时只允许一个在途命令，排队约束由运行时保证。
- 高频场景下 UI 每秒批量刷新，不逐帧渲染；离开页面后停止拉取并释放缓冲。

### 4.6 日志

- 级别过滤（Trace/Debug/Information/Warning/Error）、文本搜索、设备过滤、跟随尾部、暂停、导出当前视图。
- 日志来源：滚动文件 + 进程内有界缓冲（默认 2000 条），UI 重启后至少能看到文件中的历史。
- 默认不记录原始载荷；脚本日志、配置变更、通道状态、事务结果、spool 水位、脚本编译结果必须可见。
- 行内可展开异常堆栈与结构化字段；UI 不提供删除日志操作。

### 4.7 设置

| 分区 | 项目 | 存储 | 生效方式 |
| --- | --- | --- | --- |
| Agent 身份 | `agentId`（默认主机名，固化）、`displayName` | `agent.json` | 修改 `id` 需重启；`displayName` 重载生效 |
| 本机 WebSocket | 启用、绑定地址（仅回环）、端口、Origin 白名单、令牌 | `agent.json` | 绑定/端口/启用需重启；令牌与白名单对新连接生效 |
| 中心出口 | `wsClient` 列表：URL、令牌（密码框）、重连区间、spool 大小与目录 | `agent.json` | 保存后重载生效 |
| 数据与状态 | 状态目录、磁盘占用、打开目录 | `agent.json` | 状态目录需重启 |
| 桌面体验 | 关闭窗口行为、主题、托盘启用、实时缓冲大小 | `ui-settings.json`；托盘启用存 `agent.json` | 即时 |
| 开机启动 | 随 Windows 启动、启动时最小化 | 注册表 `HKCU\...\Run`（镜像到 `ui-settings.json`） | 即时 |
| 配置 | 配置路径、打开配置文件、重新加载、打开脚本目录 | — | 即时 |
| 关于 | 版本、构建时间、Windows App SDK 版本、本机 WebSocket 地址 | — | — |

开机启动细节：

- 注册表值名 `Sim.DeviceAgent`，命令为 `"<exe 完整路径>" --autostart`。
- 开关关闭时删除该值；开启时写入并立即校验当前 exe 路径。
- 应用启动时读取注册表显示真实状态；如果 exe 被移动，提示“开机启动路径失效，是否更新”。
- 带 `--autostart` 启动固定不显示主窗口，只驻留托盘；设置页“启动时最小化”控制的是手动启动时的表现。
- 当前采用当前用户注册表项，不需要管理员权限；面向全用户安装或提权运行的方案留 P2（HKLM Run 或任务计划）。
- 不在应用内自动提升权限；串口与文件目录访问权限按运行账户处理。

开关“托盘启用”从“需重启”调整为即时：WinUI3 宿主可动态创建/销毁托盘图标；`agent.tray.enabled` 仍是配置事实，保存后立即应用。

### 4.8 通用交互与视觉

- 表单校验：字段级即时校验 + 保存时整份配置校验；错误定位到字段并在顶部汇总。
- 脏状态保护：切换页面、关闭窗口、外部修改配置时提示保存/放弃。
- “需要重启”对话框：列出待重启字段，提供“稍后”和“立即重启应用”；重启沿用同一配置路径。
- 配置重载期间显示不确定进度条，完成后 InfoBar 汇总“已应用/未生效/需重启”。
- 空状态：无设备、无数据源、无脚本时提供主操作按钮与一句说明。
- 状态胶囊：图标 + 文本 + 颜色；连接/等待/故障/停止与运行时状态一一对应。
- 可访问性：所有控件有 AutomationProperties.Name；键盘可达；对比度使用主题资源；不依赖悬停展示关键信息。
- 字体与布局使用 WinUI 主题资源，适配 125%–200% 缩放；列表行高不写死。

## 5. 数据模型与字段映射

### 5.1 `agent.json` 映射

数据源（`sources[]`）：

| UI 字段 | JSON 字段 | 说明 |
| --- | --- | --- |
| 源 ID | `id` | 唯一，创建后锁定 |
| 类型 | `type` | `serial` / `tcpClient` / `file` |
| 串口端口 | `port` | 字符串；兼容数字写法 |
| 波特率/数据位/校验/停止位 | `baudRate` / `dataBits` / `parity` / `stopBits` | 枚举大小写不敏感 |
| DTR/RTS | `dtrEnable` / `rtsEnable` | 布尔 |
| 读超时 | `readTimeoutMs` | 50–10000 |
| TCP 主机/端口 | `host` / `port` | `port` 复用同一字段 |
| 重连区间 | `reconnectMinSeconds` / `reconnectMaxSeconds` | 仅 TCP |
| 监听目录/模式 | `directory` / `pattern` | 仅文件 |
| 稳定判定/扫描周期 | `stableSeconds` / `pollIntervalSeconds` | 仅文件 |
| 归档/错误目录 | `archiveDirectory` / `errorDirectory` | 仅文件 |

设备通道（`channels[]`）：

| UI 字段 | JSON 字段 | 说明 |
| --- | --- | --- |
| 通道 ID | `id` | 唯一 |
| 启用 | `enabled` | 默认 true |
| 设备编号/类型/名称 | `deviceId` / `deviceType` / `deviceName` | 设备身份不随 COM 口变化 |
| 源引用 | `sourceRef` | 指向 `sources[].id`，一源一设备 |
| 解析器 | `codec` | 见下表 |
| 出口 | `sinks[]` | 至少一个 |
| 队列容量 | `queueCapacity` | 16–1000000 |

解析器（`channels[].codec`）：

| 类型 | 字段 |
| --- | --- |
| `delimiter` | `delimiter`、`encoding`、`parse.pattern`、`maxFrameBytes` |
| `passthrough` | `encoding` |
| `csv` | `csvDelimiter`、`encoding`、`hasHeader`（MVP 必须 true）、`columns`、`maxFrameBytes` |
| `script` | `script`、`entry`、`delimiter`（串口/TCP 切帧）、`encoding`、`timeoutMs`、`fileMode`（文件）、`maxFrameBytes` |

出口（`sinks`）：

| UI | JSON | 说明 |
| --- | --- | --- |
| 本机出口 | `{ "type": "wsServer" }` | 每个 Agent 一个，名称约定 `local` |
| 中心出口 | `{ "type": "wsClient", "url", "token", ... }` | 多个出口可并存，按设备引用 |

Agent（`agent`）：

| UI | JSON | 说明 |
| --- | --- | --- |
| Agent ID | `id` | 默认首次启动主机名，修改需重启 |
| 展示名 | `displayName` | 可热更新 |
| 状态目录 | `stateDirectory` | 序号、spool 根目录，修改需重启 |
| 托盘 | `tray.enabled` | UI 宿主下即时生效 |
| 本机 WebSocket | `localWs` | 回环绑定；端口/地址变更需重启 |
| 白名单/令牌 | `localWs.allowedOrigins` / `localWs.token` | 令牌支持环境变量覆盖 |

### 5.2 `ui-settings.json`

位置：`%LOCALAPPDATA%\Sim\DeviceAgent\ui-settings.json`，不进入 `agent.json`，不参与采集校验。

```json
{
  "theme": "system",
  "closeAction": "minimizeToTray",
  "startWithWindows": true,
  "startMinimized": true,
  "window": { "width": 1280, "height": 800, "maximized": false },
  "lastPage": "devices",
  "liveMonitor": { "bufferPerDevice": 200 },
  "scriptEditor": { "fontSize": 13 }
}
```

- `startWithWindows` 只是镜像值，事实以注册表为准。
- 文件损坏时使用默认值并记录警告，不阻塞启动。

### 5.3 校验规则

UI 校验与 `AgentConfigValidator` 共用同一份规则，避免两套逻辑漂移：

- 将校验器错误从 `string` 扩展为 `{ path, message }`，UI 按 `path` 定位字段；现有调用方可继续取 `message`。
- `script` 校验新增：`script` 必须是相对路径且位于配置目录的 `scripts/` 下；文件存在；`entry` 非空；`timeoutMs` 50–2000；文件模式合法。
- 配置保存前额外执行一次脚本编译（不运行），编译失败阻止应用并提示行号。
- 源存在性检查、目录存在性检查作为 UI 警告，不写入配置校验器，避免离线/共享目录暂时不可用导致无法保存。

### 5.4 保存与热加载时序

1. UI 以原始 JSON DOM 为底稿，只替换本次编辑的节点，保留未知字段与顺序无关的扩展字段。
2. 生成草稿并执行完整校验（含脚本编译）。
3. 通过后写入临时文件，`File.Replace` 原子覆盖 `agent.json`，同时保留 `agent.json.bak`。
4. 调用现有 `AgentConfigReloader.ReloadAsync`；文件监听触发的重复重载按内容哈希抑制。
5. 成功：刷新快照并提示应用结果；需要重启的字段进入“需重启”对话框。
6. 失败：恢复 `.bak` 并重新加载旧配置，错误定位到字段/日志；避免通道长时间处于半停止状态。
7. 外部修改：监听器先于 UI 感知时，UI 弹出差异提示，不静默丢弃未保存表单。

## 6. JS 解析脚本设计

### 6.1 引擎选型

| 方案 | 结论 |
| --- | --- |
| Jint（.NET 嵌入式 JS） | **采用**。无额外进程与运行时依赖，可限制内存/语句数/超时，便于单文件/目录化部署 |
| WebView2 内执行 | 拒绝。解析在 UI 引擎内运行会影响界面，且生命周期与通道解耦困难 |
| 外部 Node 进程 | 拒绝。引入 Node 运行时与进程管理，违背当前轻量部署目标 |

编辑器使用 WebView2 + 离线 Monaco；执行引擎与编辑器完全分离，Monaco 只负责编辑体验。

### 6.2 解析模型

P1 的 `script` 只接管“解析”：

- 串口/TCP：先按 `delimiter` 切帧，再将每帧交给 JS。
- 文件：`fileMode = wholeFile`，整文件读入文本（受上限保护）后交给 JS；返回多条记录。
- 自定义切帧（长度前缀、STX/ETX、脚本增量解码）仍属 P2；届时不改变脚本解析函数契约，只增加新的切帧模式。

### 6.3 配置

```json
{
  "type": "script",
  "script": "scripts/pt-01.js",
  "entry": "parse",
  "delimiter": "\r\n",
  "encoding": "utf-8",
  "timeoutMs": 200,
  "fileMode": "wholeFile",
  "maxFrameBytes": 65536
}
```

- `script`：相对配置目录的路径，必须位于 `scripts/` 下；绝对路径与 `..` 逃逸直接拒绝。
- `entry`：入口函数名，默认 `parse`。
- `timeoutMs`：单次调用超时，默认 200，范围 50–2000。
- `fileMode`：仅文件源的 `wholeFile`；预留 `line` 到 P2。
- `delimiter`：仅串口/TCP 切帧使用，文件源忽略。
- `encoding`：串口/TCP 与文件源都使用；文件源按该编码解码整文件。

### 6.4 脚本契约 v1

入口函数：

```js
parse(input) → undefined | null | object | object[]
```

输入对象：

```ts
interface ScriptInput {
  kind: "frame" | "file";
  channel: { channelId: string; deviceId: string; deviceType: string };
  text?: string;                 // 可解码时的文本
  bytes: Uint8Array;             // 原始字节（二进制设备使用 DataView）
  sourceRef?: { name: string; size: number; sha256: string }; // 文件来源
  log(message: string): void;    // 最多 50 条，每条截断到 1 KiB
}
```

输出规则：

| 脚本返回 | 结果 |
| --- | --- |
| `object` | 一条记录，作为消息 `parsed` |
| `object[]` | 多条记录；帧按数组下标，文件按 `sourceRef.index` 升序产出 |
| `null` / `undefined` | 主动过滤：不产出记录，不计解析错误（用于心跳/噪声帧） |
| 抛出异常 | 仍产出原始帧，`parsed=null`，`parseError=script_error: <message>` |
| 超时 | 产出原始帧，`parseError=script_timeout`，销毁并重建引擎 |
| 输出无法 JSON 序列化 | 产出原始帧，`parseError=script_output_invalid` |

- 输出必须是纯 JSON 值：函数、循环引用、`BigInt`、宿主对象一律拒绝；宿主通过脚本内 `JSON.stringify` 归一化后再反序列化。
- 脚本不能修改 `deviceId`、`channelId`、`seq`、`ts` 等路由字段；路由身份始终来自通道配置。
- 保留现有容错语义：解析失败不改变通道状态，连续失败达到阈值后通道 `degraded`。

资源上限：

| 项 | 限制 | 超限行为 |
| --- | --- | --- |
| 脚本文件大小 | 256 KiB | 编译失败 |
| 单次执行超时 | 默认 200 ms，最大 2000 ms | `script_timeout`，引擎重建 |
| 内存 | 每个引擎 32 MiB | 执行失败，通道计数 |
| 语句数 | 2,000,000 | 执行失败 |
| 帧输入 | `maxFrameBytes`（默认 64 KiB） | 现有超限丢弃策略 |
| 文件输入 | 4 MiB（P1 整文件） | 进入错误目录并告警 |
| 单帧输出记录 | 100 条 | `script_output_invalid` |
| 单文件输出记录 | 10000 条 | `script_output_invalid` |
| 输出 JSON | 1 MiB | `script_output_invalid` |

### 6.5 示例

帧解析（替代分隔符 + 正则）：

```js
// @apiVersion 1
// @name 焊线拉力机
// @description RESULT,FORCE=12.34N → { force: 12.34, unit: "N" }

function parse(input) {
  if (input.kind !== "frame") return null;
  const text = input.text || "";

  if (/^(RDY|PONG)/.test(text)) return null; // 心跳帧过滤

  const match = /^RESULT,FORCE=(\d+(?:\.\d+)?)N/.exec(text);
  if (!match) throw new Error("unexpected frame: " + text);

  return { force: Number(match[1]), unit: "N" };
}
```

文件解析：

```js
// @apiVersion 1
// @name 文件型拉力机（自定义 TXT）
// @description 分号分隔，每行一条记录

function parse(input) {
  if (input.kind !== "file") return null;

  const rows = [];
  for (const line of (input.text || "").split(/\r?\n/)) {
    if (!line || line.startsWith("#")) continue;
    const [sampleId, force, time] = line.split(";");
    if (!sampleId || force === undefined) {
      throw new Error("bad line: " + line);
    }
    rows.push({ sampleId, force: Number(force), time });
  }
  return rows;
}
```

- 标准 CSV 优先使用内置 `csv` Codec；脚本用于私有格式或需要额外字段换算的场景。
- 二进制帧可用 `input.bytes` 与 `DataView` 读取；P1 切帧仍由分隔符完成，纯长度前缀协议待 P2 切帧扩展。

### 6.6 热更新与故障隔离

- `ScriptRegistry` 监听 `scripts/` 目录（300 ms 防抖），按“路径 + SHA-256”缓存编译结果。
- 编译成功：原子替换该脚本的运行时版本；绑定通道下一次解析即使用新版本，并产生 `script.applied` 事件。
- 编译失败：保留上一次成功版本；产生 `script.compileFailed` 事件，UI 标记“未生效”。
- 首次启动即无可用版本：引用它的通道进入 `faulted`，记录明确错误；其他通道正常启动。
- 配置重载会重新构建通道，脚本编译失败会使整份配置无法应用（保留旧配置），避免静默降级。
- 每次脚本变化记录：脚本路径、SHA-256、编译结果、影响通道；默认不记录原始帧。

### 6.7 沙箱与安全

- Jint 引擎只注入纯数据对象与 `log` 函数；不开放文件、网络、环境变量、进程、反射与任意 CLR 对象。
- 脚本目录建议 ACL 只允许运维账号与 Agent 运行账号写入；脚本属于受信工程资产，不作为多租户隔离边界。
- UI 阻止删除/重命名被引用脚本；保存时展示影响设备清单。
- 脚本路径拒绝绝对路径、`..` 与符号链接逃逸；运行时再次校验真实路径。
- 脚本执行不持有 UI 或配置写锁；超时后强制取消并重建引擎，避免污染下一次调用。
- 审计：脚本版本变化与编译失败进入日志；原始载荷仍按现有调试开关策略。

### 6.8 测试台行为

- 使用独立引擎实例，与生产通道隔离；相同超时、内存与输出上限。
- 输出展示归一化后的记录数组，而不是脚本原始返回值，确保所见即生产结果。
- 编译错误展示行/列；运行时错误展示堆栈摘要与输入摘要（截断）。
- “从实时帧插入”只读取 `ILiveTap`，不改变设备状态，不触发出口。

## 7. 运行时改造清单

| 组件 | 类型 | 说明 |
| --- | --- | --- |
| `Sim.DeviceAgent.Core` | 工程拆分 | 现工程内容迁入；新增 `Hosting` 组装入口，供 WinUI3 调用 |
| `Sim.DeviceAgent.App` | 新增 | WinUI3 宿主、页面、托盘、单实例、开机启动 |
| `ScriptCodec` | 新增 | 实现 `IDeviceCodec`；帧/文件两种输入，调用 `ScriptHost` |
| `ScriptHost` / `ScriptRegistry` | 新增 | Jint 编译、资源限制、热更新、最后成功版本、事件 |
| `AgentEventBus` / `LiveTap` | 新增 | 帧环形缓冲与低频事件，供 UI 消费 |
| `ConfigWorkspace` | 新增 | JSON DOM 合并、原子保存、备份与重载编排 |
| `ConfigValidationIssue` | 修改 | 校验错误带字段路径；新增 `script` 规则 |
| `AgentConfigLoader` | 修改 | 默认配置路径改为应用数据目录；UI 宿主不依赖当前工作目录 |
| `TrayService` | 替换 | WinForms 托盘改为 WinUI3 托盘控制器，菜单与窗口联动 |
| 日志 | 修改 | 移除控制台输出，增加滚动文件与 UI 缓冲；保留结构化字段 |
| `SerialPortDiscovery` | 新增 | 端口枚举与友好名，供数据源对话框 |
| `SourceProbe` | 新增 | 未绑定源限时监听测试 |
| `StartupRegistration` | 新增 | HKCU Run 读写与路径校验 |
| `UiSettingsStore` | 新增 | `ui-settings.json` 读写 |
| 单实例 | 新增 | `AppInstance` 重定向激活 |
| 测试 | 修改 | 项目引用 Core；新增脚本沙箱、配置路径、ViewModel 测试 |

保留不变：`Contracts` 消息信封、本机 WebSocket 协议、中心 `wsClient` 与 spool、命令事务、序号持久化、配置热加载语义。

## 8. WinUI3 实现规范

### 8.1 工程与依赖

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <Platforms>x64</Platforms>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```

| 依赖 | 用途 |
| --- | --- |
| Windows App SDK（稳定版，支持当前 .NET） | WinUI3、Mica、AppWindow、AppInstance |
| CommunityToolkit.Mvvm | ObservableObject、RelayCommand |
| H.NotifyIcon.WinUI | 托盘图标与菜单 |
| Microsoft.Web.WebView2 | Monaco 编辑器宿主 |
| Jint | JS 解析脚本引擎 |
| Microsoft.Extensions.Hosting / ASP.NET Core FrameworkReference | 进程内 Generic Host 与 Kestrel |
| System.IO.Ports | 串口（迁入 Core） |

- 非打包部署（`WindowsPackageType=None`），避免 MSIX 对文件路径、串口与开机启动的限制。
- WebView2 使用 Evergreen Runtime；工业内网需随安装包提供离线安装器或预装说明。
- 托盘库固定一个，避免同时引入 WinUIEx 与 H.NotifyIcon 造成生命周期冲突。

### 8.2 MVVM 与线程

- Page 只做布局与轻量交互；状态在 ViewModel，服务通过 DI 注入。
- 运行时事件不得直接触碰 `ObservableCollection`；统一经 `DispatcherQueue`。
- 状态轮询 1 秒；实时帧批量拉取约 10 Hz；页面不可见时停止拉取。
- 配置保存、重载、脚本编译均为异步操作，UI 显示进度与结果；禁止同步等待网络/串口/磁盘。
- 所有页面共享 `ConfigWorkspace` 的当前草稿版本；切换页面保留未保存状态并提示。

### 8.3 部署与开机启动

- 默认按用户安装：程序位于 `%LOCALAPPDATA%\Programs\Sim\DeviceAgent`，数据目录 `%LOCALAPPDATA%\Sim\DeviceAgent`。
- 数据目录包含 `agent.json`、`scripts/`、`data/`、`logs/`；便携模式可通过 `--config` 指定他处配置。
- 现有“配置在 exe 旁”的部署迁移：首次启动检测到旧路径时提示复制到数据目录，或继续便携模式。
- 升级不覆盖 `agent.json`、`scripts`、`data`；升级前自动备份配置与 spool 元数据。
- 开机启动为当前用户 HKCU Run，不需要管理员；统一安装/提权运行留 P2。
- 发布物为目录 zip（含自包含 Windows App SDK），不承诺 WinUI3 单文件发布。

### 8.4 WebView2 与 Monaco

- Monaco 资源随应用分发，放在 `Assets/monaco/`，通过 `SetVirtualHostNameToFolderMapping` 映射为本地站点，不依赖公网 CDN。
- 提供 `device-agent.d.ts` 给编辑器做补全：`ScriptInput`、返回类型、`@apiVersion` 说明。
- 编辑器设置：UTF-8、CRLF、2 空格缩进、保存时格式化可选。
- WebView2 初始化失败时降级：显示只读文本预览 + “用系统默认编辑器打开”，测试台仍可用。

### 8.5 视觉与可访问性

- Mica 背景、系统强调色、圆角卡片；状态色使用系统语义色资源。
- 关键操作（保存、应用、删除、退出）需明确按钮文案，不使用仅图标操作。
- 命令台、脚本测试台输出区支持键盘滚动与复制。
- 高 DPI 与 200% 缩放下不截断关键字段；长路径中间省略并提供完整 Tooltip。

## 9. 关键页面 XAML 骨架

以下为结构示意，绑定属性与命令由对应 ViewModel 提供；样式资源来自 WinUI 主题。

### 9.1 AppShell

```xml
<Window
    x:Class="Sim.DeviceAgent.App.AppShell"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Device Agent">

    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>

    <Grid>
        <NavigationView
            x:Name="Nav"
            IsBackButtonVisible="Collapsed"
            IsSettingsVisible="False"
            PaneDisplayMode="Left"
            SelectionChanged="Nav_SelectionChanged">

            <NavigationView.MenuItems>
                <NavigationViewItem Content="概览" Tag="dashboard">
                    <NavigationViewItem.Icon>
                        <FontIcon Glyph="&#xE80F;" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
                <NavigationViewItem Content="设备" Tag="devices">
                    <NavigationViewItem.Icon>
                        <FontIcon Glyph="&#xE772;" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
                <NavigationViewItem Content="数据源" Tag="sources">
                    <NavigationViewItem.Icon>
                        <FontIcon Glyph="&#xE86F;" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
                <NavigationViewItem Content="解析脚本" Tag="scripts">
                    <NavigationViewItem.Icon>
                        <FontIcon Glyph="&#xE943;" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
                <NavigationViewItem Content="实时数据" Tag="live">
                    <NavigationViewItem.Icon>
                        <FontIcon Glyph="&#xE93E;" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
                <NavigationViewItem Content="日志" Tag="logs">
                    <NavigationViewItem.Icon>
                        <FontIcon Glyph="&#xE8A5;" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
            </NavigationView.MenuItems>

            <NavigationView.FooterMenuItems>
                <NavigationViewItem Content="设置" Tag="settings">
                    <NavigationViewItem.Icon>
                        <FontIcon Glyph="&#xE713;" />
                    </NavigationViewItem.Icon>
                </NavigationViewItem>
            </NavigationView.FooterMenuItems>

            <NavigationView.PaneFooter>
                <StackPanel Padding="12,8" Spacing="4">
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <Ellipse Width="8" Height="8"
                                 Fill="{ThemeResource SystemFillColorSuccessBrush}" />
                        <TextBlock Text="{x:Bind ViewModel.AgentStateText, Mode=OneWay}"
                                   Style="{ThemeResource CaptionTextBlockStyle}" />
                    </StackPanel>
                    <TextBlock Text="{x:Bind ViewModel.StatusLine, Mode=OneWay}"
                               Style="{ThemeResource CaptionTextBlockStyle}"
                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                </StackPanel>
            </NavigationView.PaneFooter>

            <Frame x:Name="ContentFrame" />
        </NavigationView>

        <InfoBar
            x:Name="GlobalConfigError"
            IsOpen="{x:Bind ViewModel.HasConfigError, Mode=OneWay}"
            Severity="Error"
            Title="配置未生效"
            Message="{x:Bind ViewModel.ConfigErrorSummary, Mode=OneWay}"
            IsClosable="False"
            VerticalAlignment="Top"
            Margin="48,48,48,0" />
    </Grid>
</Window>
```

### 9.2 SourcesPage

```xml
<Page
    x:Class="Sim.DeviceAgent.App.Views.SourcesPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:Sim.DeviceAgent.App.ViewModels">

    <Grid Padding="24" RowSpacing="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <Grid ColumnSpacing="8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <StackPanel Spacing="4">
                <TextBlock Text="数据源" Style="{ThemeResource TitleTextBlockStyle}" />
                <TextBlock Text="一个串口、TCP 连接或目录只绑定一台设备。"
                           Style="{ThemeResource CaptionTextBlockStyle}"
                           Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
            </StackPanel>
            <Button Grid.Column="1" Content="刷新端口"
                    Command="{x:Bind ViewModel.RefreshPortsCommand}" />
            <Button Grid.Column="2" Content="新建数据源" Style="{ThemeResource AccentButtonStyle}"
                    Command="{x:Bind ViewModel.CreateSourceCommand}" />
        </Grid>

        <InfoBar Grid.Row="1"
                 IsOpen="{x:Bind ViewModel.HasProbeError, Mode=OneWay}"
                 Severity="Warning"
                 Title="监听测试失败"
                 Message="{x:Bind ViewModel.ProbeError, Mode=OneWay}" />

        <ListView Grid.Row="2"
                  ItemsSource="{x:Bind ViewModel.Sources, Mode=OneWay}"
                  SelectionMode="Single"
                  SelectedItem="{x:Bind ViewModel.SelectedSource, Mode=TwoWay}">
            <ListView.Header>
                <Grid Padding="12,8" ColumnSpacing="12">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="80" />
                        <ColumnDefinition Width="180" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="120" />
                        <ColumnDefinition Width="100" />
                        <ColumnDefinition Width="160" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="类型" Style="{ThemeResource CaptionTextBlockStyle}" />
                    <TextBlock Grid.Column="1" Text="ID" Style="{ThemeResource CaptionTextBlockStyle}" />
                    <TextBlock Grid.Column="2" Text="连接摘要" Style="{ThemeResource CaptionTextBlockStyle}" />
                    <TextBlock Grid.Column="3" Text="绑定设备" Style="{ThemeResource CaptionTextBlockStyle}" />
                    <TextBlock Grid.Column="4" Text="状态" Style="{ThemeResource CaptionTextBlockStyle}" />
                    <TextBlock Grid.Column="5" Text="操作" Style="{ThemeResource CaptionTextBlockStyle}" />
                </Grid>
            </ListView.Header>
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:SourceItemViewModel">
                    <Grid Padding="12,10" ColumnSpacing="12">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="80" />
                            <ColumnDefinition Width="180" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="120" />
                            <ColumnDefinition Width="100" />
                            <ColumnDefinition Width="160" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Text="{x:Bind TypeLabel}" />
                        <TextBlock Grid.Column="1" Text="{x:Bind Id}" FontWeight="SemiBold" />
                        <TextBlock Grid.Column="2" Text="{x:Bind Summary}"
                                   TextTrimming="CharacterEllipsis" />
                        <Button Grid.Column="3" Content="{x:Bind DeviceId}"
                                Visibility="{x:Bind DeviceIdVisibility}" Click="OpenDevice_Click" />
                        <TextBlock Grid.Column="4" Text="{x:Bind StateText}" />
                        <StackPanel Grid.Column="5" Orientation="Horizontal" Spacing="4">
                            <Button Content="编辑" Click="EditSource_Click" />
                            <Button Content="测试" Click="ProbeSource_Click"
                                    IsEnabled="{x:Bind CanProbe}" />
                        </StackPanel>
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </Grid>
</Page>
```

### 9.3 ScriptsPage

```xml
<Page
    x:Class="Sim.DeviceAgent.App.Views.ScriptsPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:Sim.DeviceAgent.App.ViewModels">

    <Grid Padding="24" RowSpacing="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <Grid ColumnSpacing="8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <StackPanel Spacing="2">
                <TextBlock Text="解析脚本" Style="{ThemeResource TitleTextBlockStyle}" />
                <TextBlock Text="脚本先保存，再应用到绑定设备；编译失败时保留上一成功版本。"
                           Style="{ThemeResource CaptionTextBlockStyle}"
                           Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
            </StackPanel>
            <Button Grid.Column="1" Content="新建脚本"
                    Command="{x:Bind ViewModel.CreateScriptCommand}" />
            <Button Grid.Column="2" Content="保存"
                    Command="{x:Bind ViewModel.SaveScriptCommand}" />
            <Button Grid.Column="3" Content="运行测试"
                    Style="{ThemeResource AccentButtonStyle}"
                    Command="{x:Bind ViewModel.RunTestCommand}" />
        </Grid>

        <Grid Grid.Row="1" ColumnSpacing="12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="240" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="360" />
            </Grid.ColumnDefinitions>

            <ListView ItemsSource="{x:Bind ViewModel.Scripts, Mode=OneWay}"
                      SelectedItem="{x:Bind ViewModel.SelectedScript, Mode=TwoWay}">
                <ListView.ItemTemplate>
                    <DataTemplate x:DataType="vm:ScriptItemViewModel">
                        <StackPanel Padding="4,6" Spacing="2">
                            <TextBlock Text="{x:Bind FileName}" FontWeight="SemiBold" />
                            <TextBlock Text="{x:Bind StatusText}"
                                       Style="{ThemeResource CaptionTextBlockStyle}"
                                       Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                        </StackPanel>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>

            <Border Grid.Column="1"
                    Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                    BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                    BorderThickness="1" CornerRadius="8">
                <Grid RowSpacing="0">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="*" />
                    </Grid.RowDefinitions>
                    <Grid Padding="12,8" ColumnSpacing="8">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Text="{x:Bind ViewModel.SelectedScript.FileName, Mode=OneWay}"
                                   FontWeight="SemiBold" />
                        <TextBlock Grid.Column="1"
                                   Text="{x:Bind ViewModel.SelectedScript.StatusText, Mode=OneWay}"
                                   Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                    </Grid>
                    <WebView2 x:Name="EditorWebView" Grid.Row="1" />
                </Grid>
            </Border>

            <TabView Grid.Column="2" IsAddTabButtonVisible="False">
                <TabViewItem Header="测试" IsClosable="False">
                    <ScrollViewer>
                        <StackPanel Padding="12" Spacing="8">
                            <ComboBox ItemsSource="{x:Bind ViewModel.InputKinds}"
                                      SelectedItem="{x:Bind ViewModel.SelectedInputKind, Mode=TwoWay}" />
                            <TextBox Text="{x:Bind ViewModel.TestInput, Mode=TwoWay}"
                                     AcceptsReturn="True" TextWrapping="Wrap"
                                     MinHeight="120" />
                            <StackPanel Orientation="Horizontal" Spacing="8">
                                <Button Content="从实时帧插入"
                                        Command="{x:Bind ViewModel.CaptureFrameCommand}" />
                                <Button Content="运行"
                                        Command="{x:Bind ViewModel.RunTestCommand}" />
                            </StackPanel>
                            <InfoBar IsOpen="{x:Bind ViewModel.TestFailed, Mode=OneWay}"
                                     Severity="Error"
                                     Title="测试未通过"
                                     Message="{x:Bind ViewModel.TestError, Mode=OneWay}" />
                            <TextBlock Text="{x:Bind ViewModel.TestSummary, Mode=OneWay}"
                                       Style="{ThemeResource CaptionTextBlockStyle}" />
                            <TextBlock Text="日志" FontWeight="SemiBold" />
                            <TextBlock Text="{x:Bind ViewModel.TestLog, Mode=OneWay}"
                                       FontFamily="Consolas" TextWrapping="Wrap" />
                            <TextBlock Text="记录" FontWeight="SemiBold" />
                            <TextBox Text="{x:Bind ViewModel.TestJson, Mode=OneWay}"
                                     IsReadOnly="True" AcceptsReturn="True"
                                     FontFamily="Consolas" MinHeight="200" />
                        </StackPanel>
                    </ScrollViewer>
                </TabViewItem>
                <TabViewItem Header="API 文档" IsClosable="False">
                    <TextBlock Padding="12" TextWrapping="Wrap"
                               Text="{x:Bind ViewModel.ApiDocText, Mode=OneWay}" />
                </TabViewItem>
                <TabViewItem Header="引用" IsClosable="False">
                    <ListView ItemsSource="{x:Bind ViewModel.References, Mode=OneWay}">
                        <ListView.ItemTemplate>
                            <DataTemplate x:DataType="vm:ScriptReferenceViewModel">
                                <StackPanel Padding="4,6">
                                    <TextBlock Text="{x:Bind DeviceName}" />
                                    <TextBlock Text="{x:Bind DeviceId}"
                                               Style="{ThemeResource CaptionTextBlockStyle}" />
                                </StackPanel>
                            </DataTemplate>
                        </ListView.ItemTemplate>
                    </ListView>
                </TabViewItem>
            </TabView>
        </Grid>
    </Grid>
</Page>
```

### 9.4 SettingsPage（启动项片段）

```xml
<Page
    x:Class="Sim.DeviceAgent.App.Views.SettingsPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:CommunityToolkit.WinUI.Controls">

    <ScrollViewer>
        <StackPanel Padding="24" Spacing="8" MaxWidth="900">
            <TextBlock Text="桌面体验" Style="{ThemeResource SubtitleTextBlockStyle}" />

            <controls:SettingsCard Header="开机启动"
                                   Description="登录 Windows 后自动运行 Device Agent，只驻留托盘。">
                <ToggleSwitch IsOn="{x:Bind ViewModel.StartWithWindows, Mode=TwoWay}" />
            </controls:SettingsCard>

            <controls:SettingsCard Header="启动时最小化"
                                   Description="手动启动应用时直接驻留托盘，不显示主窗口。">
                <ToggleSwitch IsOn="{x:Bind ViewModel.StartMinimized, Mode=TwoWay}" />
            </controls:SettingsCard>

            <controls:SettingsCard Header="关闭主窗口时"
                                   Description="最小化到托盘后采集继续运行。">
                <ComboBox SelectedIndex="{x:Bind ViewModel.CloseActionIndex, Mode=TwoWay}">
                    <ComboBoxItem Content="最小化到托盘" />
                    <ComboBoxItem Content="退出应用" />
                </ComboBox>
            </controls:SettingsCard>

            <TextBlock Text="Agent 身份" Style="{ThemeResource SubtitleTextBlockStyle}"
                       Margin="0,16,0,0" />

            <controls:SettingsCard Header="Agent ID"
                                   Description="默认取首次安装主机名，修改后需要重启并在平台重新注册。">
                <TextBox Text="{x:Bind ViewModel.AgentId, Mode=TwoWay}" Width="260" />
            </controls:SettingsCard>

            <controls:SettingsCard Header="展示名称">
                <TextBox Text="{x:Bind ViewModel.DisplayName, Mode=TwoWay}" Width="260" />
            </controls:SettingsCard>

            <Button Content="保存设置"
                    Style="{ThemeResource AccentButtonStyle}"
                    Command="{x:Bind ViewModel.SaveCommand}" />
        </StackPanel>
    </ScrollViewer>
</Page>
```

## 10. 边界与异常状态

| 场景 | UI 行为 |
| --- | --- |
| 启动时配置无效 | 主窗口显示错误页/InfoBar；运行时停止；提供打开配置、重新加载、恢复备份 |
| 保存时校验失败 | 禁止写入；字段级错误；保留编辑内容 |
| 脚本语法错误 | 允许保存；标记“未生效”；绑定通道继续使用上一成功版本或故障隔离 |
| 重载失败（端口占用等） | 恢复上一份配置并重载；提示失败原因与设备影响 |
| 外部修改 `agent.json` | 检测哈希变化；表单脏时提示选择重新加载或保留本地修改 |
| 串口被占用 | 数据源测试与通道状态显示明确错误；其他设备不受影响 |
| 文件目录离线/共享断开 | 通道 `waiting`；UI 不弹模态错误，概览告警 |
| spool 配额满 | 概览与出口卡片红色告警；仅该设备受影响 |
| 脚本超时/输出非法 | 实时数据标记解析错误；连续失败进入 `degraded`；脚本页可见事件 |
| 单实例重复启动 | 激活已运行实例窗口，不启动第二套采集 |
| 托盘不可用 | 警告日志；主窗口仍可用，关闭行为降级为退出确认 |
| WebView2 不可用 | 脚本编辑降级为只读预览 + 外部编辑器；测试台可用 |
| 升级后配置路径变化 | 首次启动提示迁移；拒绝静默使用错误配置 |

## 11. 实施计划与验收

| 阶段 | 范围 |
| --- | --- |
| U1 宿主迁移 | Core 拆分、WinUI3 Shell、托盘、单实例、开机启动、ui-settings、日志文件、设置页骨架 |
| U2 配置管理 | 概览、设备页、数据源页、配置工作区、原子保存与重载、校验错误定位 |
| U3 JS 脚本 | ScriptCodec、ScriptHost/Registry、脚本页、Monaco、测试台、热更新与故障隔离 |
| U4 运行可视化 | 实时数据页、命令台、日志页、异常状态与可访问性打磨 |

验收清单：

- 开启开机启动后重启 Windows，应用只驻留托盘，采集自行恢复；关闭主窗口后采集不中断。
- 新建串口/TCP/文件源并绑定设备，保存后约 1 秒内生效，无需重启进程。
- 数据源监听测试能预览原始帧；已绑定源不会被测试打断。
- JS 脚本能完成帧解析与文件多记录解析；测试台输出与生产 `parsed` 一致。
- 脚本语法错误可保存但不生效；绑定通道继续使用上一版本；其他通道不受影响。
- 脚本死循环在超时内被终止，通道产生解析错误并保持可恢复。
- 无效配置（重复 deviceId、源被两台设备引用、脚本不存在）被阻止应用，旧配置继续运行。
- 需重启字段修改后出现重启对话框；重启后配置与序号/spool 延续。
- 外部修改配置文件与 UI 未保存表单冲突时有明确提示，不丢数据。
- 100 帧/秒 × 2 台设备时 UI 可交互，设备页与实时页无明显卡顿。
- 单实例、托盘菜单、退出优雅停止、日志落盘、开机启动路径失效提示均验证通过。
- 本轮仅设计文档：未新增工程、未修改运行代码、未构建。

## 12. 开放问题

| 编号 | 问题 | 建议 |
| --- | --- | --- |
| Q-UI-01 | 是否保留无界面启动参数（如 `--headless`）用于诊断或无人值守？ | 建议保留 `--autostart`（托盘）即可；无 UI 调试可由日志与 API 完成 |
| Q-UI-02 | 工业内网是否统一预装 WebView2 Evergreen Runtime？ | 建议随安装包提供离线安装器，并保留外部编辑器降级 |
| Q-UI-03 | 安装按当前用户还是全用户？是否需要提权运行？ | P1 当前用户免提权；全用户/服务化留 P2 |
| Q-UI-04 | 脚本编辑权限与审计要求（谁能改线边脚本）？ | 建议由 Windows 文件 ACL 控制，日志记录 SHA-256 与变更 |
| Q-UI-05 | 托盘库最终选型？ | 建议 H.NotifyIcon.WinUI，若与 Windows App SDK 版本冲突再评估 WinUIEx |
| Q-UI-06 | 自定义切帧与流式大文件何时进入脚本能力？ | P2；P1 保持分隔符切帧 + 整文件解析 |
| Q-UI-07 | 平台中心管理页上线后，本机 UI 的定位？ | 本机 UI 保留为现场配置与诊断入口，中心页做车队总览 |
| Q-UI-08 | UI 是否需要中英双语？ | 首期中文；资源以 resw 组织，后续可扩展 |

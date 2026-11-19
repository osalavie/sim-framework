# Sim.DeviceAgent

一台上位机并行接入多台设备的采集转发 Agent。P0 MVP 已实现，设计基线见 [`DEVICE_AGENT_DESIGN.md`](DEVICE_AGENT_DESIGN.md)。

桌面端（WinUI3 一体化托盘宿主、数据源配置管理、JS 解析脚本与开机启动）设计见 [`DEVICE_AGENT_UI_DESIGN.md`](DEVICE_AGENT_UI_DESIGN.md)，尚未实施。

> 状态：P0 MVP、中心上报、配置热加载和 Windows 托盘已实现（2026-11-16）。当前宿主目标为 `net10.0-windows`，面向单台上位机本机使用：本机 WebSocket 仅回环监听，令牌可留空，不需要 TLS。解析插件、Windows 服务/开机自启和自动升级待后续阶段。平台侧中心网关尚未实现。
>
> 拓扑关系：一个 Agent 进程对应一台上位机，可同时承载多台设备；每台设备使用独立的串口、TCP 连接或文件目录，不共用链路。因此源与设备固定 1:1，不需要 demux。

## 已实现

- 多设备通道：每台设备独立 `deviceId`、`deviceType`、源、解析器、有界队列和序号。
- 源适配器：`serial`、`tcpClient`、`file`；串口/TCP 自动重连，文件源支持写入稳定判定、SHA-256 去重和归档。
- 内置编解码：`delimiter` + 正则命名组、`passthrough`、`csv`（表头映射、引号处理、数值/布尔转换）。
- 本机 WebSocket：仅回环监听、可选令牌、Origin 白名单、按通道/设备/类型订阅、状态快照、命令/响应事务。
- 中心 `wsClient` 出口：可选 Bearer 令牌、`hello`、30 秒心跳、指数退避重连、每设备有序补传。
- 按设备 spool：追加式 JSONL 分段、发送前落盘、中心 ack 后推进水位并删除分段、断线/重启后重放未确认数据。
- 跨重启设备序号：按设备预留 100000 个序号块，重启跳到下一个块，避免复用序号（允许出现序号空洞）。
- 配置热加载：保存配置文件后自动校验并重建通道/出口；需重启的字段会被明确拒绝。托盘和 `POST /api/config/reload` 也可手动触发。
- Windows 托盘：打开状态页、打开配置文件、重新加载配置、退出；托盘不可用时 Agent 继续运行。
- 管理接口：`/health` 匿名存活检查，`/api/status` 匿名返回通道、出口和最近一次配置重载状态。

## 尚未实现

- 平台侧中心网关：设备注册、消息去重入库、按设备/类型转发、在线状态与原始帧留存策略。Agent 侧只定义了连接与 ack 协议。
- 解析器插件与厂商 SDK 插件。
- 中心设备凭据换短期令牌；当前中心出口使用可选静态 Bearer 令牌。
- spool 按天滚动、保留期清理与全局配额；当前只按设备大小滚动和限流。
- Windows 服务、开机自启、托盘隐藏控制台、自动升级。
- 跨平台宿主与 Linux 服务；当前宿主为 `net10.0-windows`。
- `tcpServer`、`udp`、HTTP/MQTT 出口、MQTT 采样聚合。
- 文件处理状态持久化：当前使用归档移动 + 进程内去重；重启后若文件未归档可能在极端窗口内重复处理。

## 构建与测试

```bash
cd tool
dotnet build Sim.DeviceAgent.slnx
dotnet test Sim.DeviceAgent.slnx
```

需要 Windows 10/11 与 .NET 10 SDK（包含 Windows Desktop 组件）。宿主和测试目标均为 `net10.0-windows`。

## 运行

1. 复制示例配置并按现场修改：

```bash
cp agent.example.json agent.json
```

2. 启动：

```bash
dotnet run --project src/Sim.DeviceAgent -- --config agent.json
```

也可以使用 `SIM_DEVICE_AGENT_CONFIG` 指定配置路径。配置中的相对目录按配置文件所在目录解析。

启动后 Windows 托盘出现 `Device Agent` 图标；本机可直接访问：

- `GET http://127.0.0.1:18080/health`
- `GET http://127.0.0.1:18080/api/status`
- `ws://127.0.0.1:18080/ws`（WebSocket）

本机默认不需要令牌。只有显式设置 `agent.localWs.token` 时才要求 `hello.token`；也可用 `SIM_DEVICE_AGENT_TOKEN` 覆盖配置值。中心出口的 `SIM_DEVICE_AGENT_CENTER_TOKEN` 同样只在配置了 `wsClient` 且需要鉴权时使用。

## 配置要点

`agent.example.json` 展示同一上位机的三类设备：

- `pt-01`：COM3 上的焊线拉力测试机，分隔符 + 正则解析。
- `ds-01`：TCP 连接的 Die 焊接推力测试机，独立解析器。
- `pt-02-file`：设备专属导出目录中的 CSV 拉力测试机。

`agent.center.example.json` 展示单台文件设备同时发本机 WebSocket 与中心 `wsClient` 的最小配置，可直接用于中心链路联调。

配置结构：

| 节点 | 说明 |
| --- | --- |
| `agent` | Agent 标识、状态目录、Windows 托盘、本机 WebSocket 监听、Origin 白名单、可选令牌 |
| `sources` | 物理/网络/文件源定义，按 `id` 被通道引用 |
| `sinks` | 出口注册表；支持 `wsServer`（本机页面）与 `wsClient`（中心网关） |
| `channels` | 每台设备一条通道，绑定 `deviceId`、`deviceType`、`sourceRef`、`codec` 和 `sinks` |

源字段：

- `serial`：`port`、`baudRate`、`dataBits`、`parity`、`stopBits`、`dtrEnable`、`rtsEnable`、`readTimeoutMs`。
- `tcpClient`：`host`、`port`、`reconnectMinSeconds`、`reconnectMaxSeconds`。
- `file`：`directory`、`pattern`、`stableSeconds`、`pollIntervalSeconds`、`archiveDirectory`、`errorDirectory`。

编解码字段：

- `delimiter`：`delimiter`、`encoding`、`parse.pattern`（正则命名组会转为 JSON 字段）、`maxFrameBytes`。
- `passthrough`：`encoding`。
- `csv`：`csvDelimiter`、`hasHeader`（MVP 必须为 `true`）、`columns`（JSON 键 → CSV 表头名）、`encoding`。

同一 `sourceRef` 只能被一个通道引用，即一个串口、一个 TCP 连接或一个目录只对应一台设备。若现场确有一条物理链路承载多台设备（RS-485 多从站、聚合器多工位），先由设备侧网关或前置采集器拆分后再接入 Agent。

本机与托盘字段：

- `agent.tray.enabled`：默认 `true`；Windows 托盘不可用时只记录警告，不阻止 Agent 运行。
- `agent.localWs.token`：可留空；留空时本机 `/ws` 与 `/api/status` 不要求令牌。
- `agent.localWs.allowedOrigins`：浏览器页面 Origin 白名单；为空时浏览器连接会被拒绝，非浏览器客户端可不带 Origin。配置热加载会更新新连接的 Origin/令牌校验。

## 配置热加载

Agent 监听启动时加载的配置文件，保存后约 0.4 秒自动重载；也可以点击托盘“重新加载配置”或调用：

```bash
curl -X POST http://127.0.0.1:18080/api/config/reload
```

重载流程：重新读取并校验配置 → 停止现有通道和出口 → 按新配置重建 → 向已连接页面补发状态快照。设备序号、`sequences.json` 和 spool 数据保留。

即时生效：

- `sources`、`channels`、`codecs`、`sinks` 及通道的出口引用。
- `agent.displayName`、`agent.localWs.token`、`agent.localWs.allowedOrigins`（对之后的新连接生效）。

需要重启 Agent：

- `agent.id`、`agent.stateDirectory`。
- `agent.localWs.enabled`、`agent.localWs.bind`、`agent.localWs.port`。
- `agent.tray.enabled`（托盘只在进程启动时决定是否创建）。

配置文件语法错误、校验失败或修改了需重启字段时，当前运行配置保持不变，`/api/status` 的 `config.lastError` 给出原因。重载期间通道会短暂停止再启动；文件源依赖归档移动防止重复处理，进程内 SHA-256 去重集合会重建。

## Windows 托盘

托盘菜单：

- **打开状态**：浏览器打开 `http://127.0.0.1:<port>/api/status`。
- **打开配置文件**：用系统默认程序打开当前 `agent.json`。
- **重新加载配置**：与文件监听使用同一重载入口；成功/失败通过托盘气泡提示。
- **退出**：优雅停止 Agent。

当前托盘仍在控制台进程中运行，控制台窗口不会隐藏；Windows 服务、开机自启和隐藏控制台属于后续阶段。无桌面会话或托盘初始化失败时，Agent 继续提供 WebSocket 与状态接口。

## WebSocket 协议

消息使用 JSON，字段为 camelCase。完整信封与示例见设计文档第 6 节。

握手（`agent.localWs.token` 为空时不需要 `token`；非空时必须提供）：

```json
{ "v": 1, "type": "hello",
  "subscribe": { "devices": ["PT-001"], "types": ["pullTest"] } }
```

订阅为空数组表示全部；握手成功返回 `helloAck`，随后收到订阅范围内的 `status` 快照。浏览器页面必须把自身 Origin 配置到 `agent.localWs.allowedOrigins`。

数据：

```json
{ "v": 1, "type": "data", "agentId": "agent-line-a-01",
  "deviceId": "PT-001", "deviceType": "pullTest", "channelId": "pt-01",
  "seq": 1, "ts": "2026-11-16T08:15:30.123+08:00",
  "raw": "...", "text": "RESULT,FORCE=12.34N", "parsed": { "force": 12.34 } }
```

命令事务：

```json
{ "v": 1, "type": "command", "channelId": "pt-01", "requestId": "r-1",
  "payload": { "text": "READ\r\n", "timeoutMs": 2000,
               "match": { "pattern": "^RESULT" } } }
```

Agent 在超时内收到匹配帧后返回 `response`（`ok=true`）；超时返回 `ok=false` 和 `command_timeout`。同一通道同时只允许一个在途命令。

## 中心出口与 spool

在 `sinks` 中增加 `wsClient`，并在通道的 `sinks` 数组里同时引用本机与中心出口：

```json
{
  "agent": {
    "stateDirectory": "./data/state",
    "tray": { "enabled": true }
  },
  "sinks": {
    "local": { "type": "wsServer" },
    "center": {
      "type": "wsClient",
      "url": "wss://qms.example.com/device-gateway",
      "token": "",
      "reconnectMinSeconds": 1,
      "reconnectMaxSeconds": 30,
      "spoolSegmentMegabytes": 64,
      "spoolMaxMegabytes": 1024,
      "spoolDirectory": "./data/spool/center"
    }
  },
  "channels": [
    {
      "id": "pt-01",
      "sinks": ["local", "center"]
    }
  ]
}
```

中心出口字段：

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `type` | — | 固定为 `wsClient` |
| `url` | — | `ws://` 或 `wss://` 绝对地址；本机/局域网可先用 `ws://`，跨不可信网络时使用 `wss://` |
| `token` | 空 | 可选；非空时写入 `Authorization: Bearer <token>`，可用 `SIM_DEVICE_AGENT_CENTER_TOKEN` 覆盖 |
| `reconnectMinSeconds` / `reconnectMaxSeconds` | 1 / 30 | 指数退避区间 |
| `spoolSegmentMegabytes` | 64 | 单设备分段大小 |
| `spoolMaxMegabytes` | 1024 | 单设备 spool 配额 |
| `spoolDirectory` | `<stateDirectory>/spool/<agentId>/<sinkId>` | 可选，覆盖 spool 根目录 |

`agent.stateDirectory` 默认是配置文件目录下的 `data`，用于 `sequences.json` 和默认 spool 根目录。

连接行为：

1. 配置了令牌时 WebSocket 握手携带 `Authorization: Bearer <token>`；随后发送 `hello`：
   ```json
   { "v": 1, "type": "hello", "agentId": "agent-line-a-01", "displayName": "...", "ts": "..." }
   ```
2. 每条 `data` 先追加到该设备 spool，再按设备顺序发送；连接断开时继续写盘。
3. 中心持久化后回送确认：
   ```json
   { "v": 1, "type": "ack", "agentId": "agent-line-a-01", "deviceId": "PT-001", "seq": 1 }
   ```
   `seq` 按设备累计确认；Agent 收到后推进 `lastDeliveredSeq`，删除已确认的非活动分段。
4. 重连或进程重启后，从 `lastDeliveredSeq + 1` 继续补传；未确认数据会重发，中心必须按 `agentId + deviceId + seq` 去重。
5. Agent 每 30 秒发送 `heartbeat`，包含 `agentId`、待补传数量和发送/确认计数。

可靠性约束：

- spool 布局为 `<spoolDirectory>/<deviceId>/segment-00000001.jsonl`；每行一条消息信封，`state.json` 保存 `lastDeliveredSeq`。
- 设备序号跨重启从预留块继续，允许出现空洞；中心按序处理时不能要求序号连续。
- 单设备 spool 达到配额后，该设备的新数据写入失败并在 `/api/status` 的出口状态中给出错误；本机 `wsServer` 出口继续工作，其他设备不受影响。
- 中心不回 ack 时 spool 不会释放；接入前必须确认中心网关实现 `ack`。
- 中心网关本身（鉴权、去重入库、业务转发、在线状态）尚未在平台侧实现，当前 `wsClient` 只完成了 Agent 侧协议与缓冲。

## 安全与部署

本机模式按“仅上位机内使用”设计：

- 本机监听强制为回环地址，不监听 `0.0.0.0`；令牌默认留空，任何本机进程都可以访问 `/api/status` 和 `/ws`。
- 浏览器页面仍需命中 `allowedOrigins`；这是本机模式下防止其他网页随意连接的主要边界。
- 页面与 Agent 之间使用 `http://` / `ws://`，不启用 TLS。若页面本身通过 HTTPS 提供，浏览器可能阻止 `ws://127.0.0.1` 的混合内容；此时用本机 HTTP 页面、桌面壳或后续增加本机 TLS 支持。
- 中心出口是可选能力。本机使用可不配置 `wsClient`；若启用且跨机器传输，建议配置令牌并使用可信证书的 `wss`。Agent 不提供跳过证书校验的开关，令牌只放在请求头。
- 本机令牌可用 `SIM_DEVICE_AGENT_TOKEN`，中心令牌可用 `SIM_DEVICE_AGENT_CENTER_TOKEN` 覆盖配置值；不要把生产令牌提交到仓库。
- spool 包含设备原始帧，应限制 `stateDirectory` 和 `spoolDirectory` 的 ACL，只允许运行 Agent 的账号与本机管理员读取。
- 中心 ack 是释放 spool 的唯一条件；中心未实现 ack 时不得把 `wsClient` 作为唯一数据留存链路。
- Agent 进程仍在运行时，断线数据进入 spool；进程停止期间数据不保证补传。
- Windows 服务、开机自启、静默升级和隐藏控制台尚未实现；当前以带托盘的 Windows 控制台进程运行。

## 目录

```text
tool/
├── README.md
├── DEVICE_AGENT_DESIGN.md
├── agent.example.json
├── agent.center.example.json
├── Sim.DeviceAgent.slnx
├── src/
│   ├── Sim.DeviceAgent.Contracts/  # 消息信封、SourceRef、JSON 选项
│   └── Sim.DeviceAgent/
│       ├── Configuration/          # 配置模型、校验、热加载与文件监听
│       ├── Sources/                # serial / tcpClient / file
│       ├── Codecs/                 # delimiter / passthrough / csv
│       ├── Runtime/                # 多设备通道、出口、跨重启序号
│       ├── Spool/                  # 按设备 JSONL 分段与确认水位
│       ├── Tray/                   # Windows 托盘
│       └── WebSockets/             # 本机 WebSocket 服务
└── tests/
    └── Sim.DeviceAgent.Tests/      # 35 个单元/集成测试
```

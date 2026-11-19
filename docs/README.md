# 项目文档

通用系统平台基座的项目级文档入口。子项目的启动与实现细节放在各自 `README.md`；本目录按「现役核心 / 设计与专题 / 运维 / 图表」分层，避免设计提案与现役规范混读。

## 目录约定

| 目录 | 内容 | 维护方式 |
| --- | --- | --- |
| [`core/`](core/) | 架构、开发流程、文档规则、AI 协作规范、企业级基线 | 代码或流程变更时同步更新 |
| [`design/`](design/) | 单模块或单专题的方案设计与实施说明 | 实施进度变化时更新文首状态 |
| [`ops/`](ops/) | 本地基础设施、外部登录等运行与集成说明 | 配置、拓扑或协议变化时更新 |
| [`diagrams/`](diagrams/) | Mermaid / Draw.io 图源 | 系统边界或组件关系变化时更新 |

## 现役核心文档

| 文档 | 用途 | 状态 |
| --- | --- | --- |
| [`ARCHITECTURE.md`](core/ARCHITECTURE.md) | 整体架构、模块划分与前后端关系 | 当前基线 |
| [`DEVELOPMENT.md`](core/DEVELOPMENT.md) | 本地开发、运行、验证和常见改动入口 | 当前基线 |
| [`DOCUMENTATION.md`](core/DOCUMENTATION.md) | 代码变更时如何同步维护文档 | 当前规则 |
| [`AI_CODING_GUIDELINES.md`](core/AI_CODING_GUIDELINES.md) | AI 编码助手的修改规范与验证要求 | 当前规则 |
| [`FRAMEWORK_SYNC.md`](core/FRAMEWORK_SYNC.md) | 基座仓与业务仓的目录分区、业务仓初始化、upstream 合并、冲突处理与升级验收 | 当前规则 |
| [`ENTERPRISE_SYSTEM_GUIDE.md`](core/ENTERPRISE_SYSTEM_GUIDE.md) | 企业级建设清单、完成定义与变更触发矩阵 | 当前基线 |
| [`enterprise/ASSESSMENT.md`](core/enterprise/ASSESSMENT.md) | 尚未授权实施的企业级优化建议 | 待确认，非实施授权 |

## 设计与专题

| 文档 | 用途 | 状态 |
| --- | --- | --- |
| [`approval/APPROVAL_WORKFLOW_DESIGN.md`](design/approval/APPROVAL_WORKFLOW_DESIGN.md) | 审批流总体设计：自研轻量引擎、独立 approval 模块、审批人解析、审批中心四视图 | 核心能力已实施；数据范围过滤、顶栏角标未实施 |
| [`approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md`](design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md) | 审批流第二阶段：有向图引擎、网关与 fork-join、字段/附件元数据、画布式设计器 | P0–P2 已实施；P3–P5 待实施 |
| [`notification/NOTIFICATION_PLATFORM_DESIGN.md`](design/notification/NOTIFICATION_PLATFORM_DESIGN.md) | 系统通知、消息队列与缓存总体设计 | 首版基线已实施；生产联调与增强项见文内第 20 节 |
| [`organization/ORGANIZATION_CHART_DESIGN.md`](design/organization/ORGANIZATION_CHART_DESIGN.md) | 组织架构、成员归属、角色数据范围、权限解释与审计 | 第一阶段已实施；数据范围、权限解释与审计仍为设计提案 |
| [`scheduling/SCHEDULE_TASK_DESIGN.md`](design/scheduling/SCHEDULE_TASK_DESIGN.md) | `Sim.Scheduling` 通用调度基础库设计基线 | 设计完成，尚未实施 |
| [`scheduling/SCHEDULE_TASK_SIMFRAMEWORK_INTEGRATION.md`](design/scheduling/SCHEDULE_TASK_SIMFRAMEWORK_INTEGRATION.md) | `Sim.Scheduling` 与平台的集成层设计 | 设计完成，尚未实施 |
| [`todo/SITE_TODO_DESIGN.md`](design/todo/SITE_TODO_DESIGN.md) | 全站待办：顶栏面板、类型默认期限、可选任务名链接与审批待办实时聚合 | 设计完成，尚未实施 |
| [`ui/IMPLEMENTATION.md`](design/ui/IMPLEMENTATION.md) | 设计系统、令牌与页面落地方式，含多主题与商务皮肤规范 | 当前视觉规范 |
| [`ui/MULTI_THEME_DESIGN.md`](design/ui/MULTI_THEME_DESIGN.md) | 4 套风格 × 明暗的注册表、持久化、商务皮肤与验收结果 | 已实施 |
| [`ui/prototypes/`](design/ui/prototypes/) | 设计系统与页面原型（HTML / CSS / JS） | 设计源，只读 |

## 运维与集成

| 文档 | 用途 | 状态 |
| --- | --- | --- |
| [`LOCAL_INFRASTRUCTURE.md`](ops/LOCAL_INFRASTRUCTURE.md) | 本地 Compose 服务、端口、凭据、健康检查与数据清理 | 当前基线 |
| [`EXTERNAL_LOGIN.md`](ops/EXTERNAL_LOGIN.md) | 微信、QQ、Steam、Microsoft、Google 外部登录协议、时序与绑定设计 | 已实施 |
| [`OAUTH_OPENID_VISUAL_GUIDE.html`](ops/OAUTH_OPENID_VISUAL_GUIDE.html) | OAuth 2.0 / OpenID Connect 原理、Canvas 时序图与业务系统接入图解（自包含 HTML） | 图解基线 |

## 子项目与部署文档

| 文档 | 用途 |
| --- | --- |
| [`../README.md`](../README.md) | 项目组成、快速入口与本地运行概览 |
| [`../api/README.md`](../api/README.md) | 后端启动、配置、模块与接口总览 |
| [`../api/docs/ARCHITECTURE.md`](../api/docs/ARCHITECTURE.md) | 后端分层、模块边界、认证、数据库与 OpenAPI |
| [`../api/docs/DEVELOPMENT.md`](../api/docs/DEVELOPMENT.md) | 后端本地开发、配置、数据库与模块扩展 |
| [`../web/README.md`](../web/README.md) | Web 前端启动、模块、路由、请求与构建 |
| [`../client/Sim.Admin.Wpf.Client/README.md`](../client/Sim.Admin.Wpf.Client/README.md) | Windows WPF 客户端启动、API 会话、管理页、Naive UI 复刻组件库与主题 |
| [`../web/docs/ARCHITECTURE.md`](../web/docs/ARCHITECTURE.md) | Web 模块体系、路由、认证与主题 |
| [`../web/docs/DEVELOPMENT.md`](../web/docs/DEVELOPMENT.md) | Web 环境变量、模块扩展、测试与构建 |
| [`../deploy/README.md`](../deploy/README.md) | 生产部署、CI/CD、密钥、TLS 与回滚 |
| [`../tool/README.md`](../tool/README.md) | 多设备接入 Agent 的构建、运行、配置热加载、Windows 托盘、本机/中心 WebSocket 协议、spool 与验证说明 |
| [`../tool/DEVICE_AGENT_DESIGN.md`](../tool/DEVICE_AGENT_DESIGN.md) | 多设备接入 Agent（串口/TCP/文件 → WebSocket）架构、按设备解析插件、消息协议、可靠性与实施计划（P0 MVP、中心 wsClient/spool、配置热加载和 Windows 托盘已实现；Windows 服务、平台中心网关、解析插件与 P2 能力待实施） |
| [`../tool/DEVICE_AGENT_UI_DESIGN.md`](../tool/DEVICE_AGENT_UI_DESIGN.md) | DeviceAgent WinUI3 一体化托盘宿主、数据源配置管理、JS 解析脚本（Jint 沙箱）、页面结构与关键 XAML 骨架设计（未实施） |
| [`../AGENTS.md`](../AGENTS.md) | AI 编码协作规则与文档同步要求 |
| [`../CLAUDE.md`](../CLAUDE.md) | Claude Code 仓库入口与快速命令 |

## 阅读顺序

新成员建议按以下顺序阅读：

1. 根目录 [`README.md`](../README.md)，了解项目组成和启动入口。
2. [`core/ARCHITECTURE.md`](core/ARCHITECTURE.md)，了解前后端模块关系。
3. 对应子项目 README 与 `docs/`：[`api`](../api/README.md)、[`web`](../web/README.md)、[`WPF Client`](../client/Sim.Admin.Wpf.Client/README.md)。
4. [`core/DEVELOPMENT.md`](core/DEVELOPMENT.md)，按任务类型找到具体修改入口。
5. [`core/AI_CODING_GUIDELINES.md`](core/AI_CODING_GUIDELINES.md)，了解 AI 协作时的代码和验证要求。
6. [`core/ENTERPRISE_SYSTEM_GUIDE.md`](core/ENTERPRISE_SYSTEM_GUIDE.md)，按任务风险检查企业级完成标准和当前治理优先级。
7. 与任务相关的 `design/` 专题文档；先读文首状态，区分已实施与提案。
8. [`core/enterprise/ASSESSMENT.md`](core/enterprise/ASSESSMENT.md)，查看尚需确认的优化建议，不把建议自动视为实施授权。

## 维护规则

- 文档必须描述当前代码真实状态，不保留已失效的命令、路径或模块名。
- 先改离代码最近的 README，再更新项目级总览。
- 新增、移动或删除文档时，同步更新本索引与所有入站链接。
- 设计文档在实施进度变化时更新文首状态；已完成或被取代的方案直接删除或合并回主文档，不再保留归档目录。
- 详细同步要求见 [`core/DOCUMENTATION.md`](core/DOCUMENTATION.md)。

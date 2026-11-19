# 文档维护规则

本地 Compose 的服务、镜像、端口、凭据、健康检查或数据卷发生变化时，同步维护 `api/env/compose.yaml`、`api/env/.env.example` 和 [本地开发基础设施](../ops/LOCAL_INFRASTRUCTURE.md)，并检查根 README、开发指南和后端 README 的启动入口。

本文档用于约定：以后代码更新时，如何同步更新项目文档。文档地图与阅读顺序见 [`../README.md`](../README.md)。

## 必须同步文档的变更

以下代码变更完成后，必须检查并更新文档：

- 启动方式、依赖版本、端口、构建命令、测试命令变化。
- 新增、删除、重命名子项目或顶层目录。
- WPF 客户端的启动、认证接口、管理页能力、组件库控件或主题映射变化；同时检查 `client/Sim.Admin.Wpf.Client/README.md` 与 Web 配色来源。
- 后端新增模块、应用服务、Swagger 分组、认证流程、数据种子、数据库配置。
- 新增文件存储 provider、模板/导出格式、外部转换工具、图片处理能力、限流特性或默认策略。
- 新增或修改外部登录 Provider 时，说明协议、配置项、启用/禁用行为、state/nonce、绑定策略、2FA 继承、OpenAPI 契约和真实提供方联调要求。
- 修改数据库化存储配置、连接测试、凭据加密/回显策略、定时任务类型、创建/删除、Cron 或后台启停能力。
- 修改系统配置或数据字典模型、值类型校验、默认种子参数、权限、管理页或业务读取方式。
- 前端新增模块、页面、路由、布局类型、环境变量、接口封装、主题系统。
- 新增或修改用户可见文案、业务错误码、支持语言、日期/数字格式或语言切换入口；业务错误码必须在对应 `Domain.Shared` 的 `<Module>ErrorCodes.cs` 单点声明，类名和 `Sim:<Module>:` 前缀必须与项目模块一致。
- 部署方式、CI/CD、Docker、Nginx、证书或生产配置变化。
- 业务区、组合根文件、基座同步流程或业务模块接入方式变化。
- 功能增强或缺陷修复改变安全、可靠性、数据、可观测性、测试、运维或企业级完成标准。

## 文档归属

| 变更类型 | 优先更新 |
| --- | --- |
| 项目组成、整体启动方式 | 根目录 `README.md` |
| 架构和模块边界 | `docs/core/ARCHITECTURE.md` |
| 基座仓与业务仓的目录分区、upstream 合并、业务模块接入与升级验收 | `docs/core/FRAMEWORK_SYNC.md` |
| 外部登录协议、时序与绑定设计 | `docs/ops/EXTERNAL_LOGIN.md` |
| 本地开发流程、验证命令 | `docs/core/DEVELOPMENT.md` |
| 后端模块、配置、Swagger、认证 | `api/README.md` |
| Web 前端模块、路由、脚本、环境变量 | `web/README.md` |
| Windows 客户端启动、API 会话、管理页、组件库、主题 | `client/Sim.Admin.Wpf.Client/README.md` |
| 文档维护规则本身 | `docs/core/DOCUMENTATION.md` |
| AI 编码协作规则 | `docs/core/AI_CODING_GUIDELINES.md`、`AGENTS.md`、`CLAUDE.md` |
| 企业级能力、质量基线、治理优先级 | `docs/core/ENTERPRISE_SYSTEM_GUIDE.md` |
| 尚未授权实施的企业优化建议 | `docs/core/enterprise/ASSESSMENT.md` |
| 通知、消息队列与缓存的总体设计与实施基线 | `docs/design/notification/NOTIFICATION_PLATFORM_DESIGN.md` |
| 组织架构、成员归属、数据范围和权限解释设计 | `docs/design/organization/ORGANIZATION_CHART_DESIGN.md` |
| 审批流引擎边界、审批定义与版本、运行时状态机和业务接入 | `docs/design/approval/APPROVAL_WORKFLOW_DESIGN.md` |
| 审批流第二阶段：有向图与网关分支、并发原子性、字段/附件元数据、画布式设计器、前端信息架构与引擎使用说明 | `docs/design/approval/APPROVAL_WORKFLOW_PHASE2_DESIGN.md` |
| 全站视觉规范、Paper Ocean 设计令牌与页面落地方式 | `docs/design/ui/IMPLEMENTATION.md`（设计原型与令牌源码在 `docs/design/ui/prototypes/`） |
| 后端/Web 项目维护手册 | `api/docs/ARCHITECTURE.md`、`api/docs/DEVELOPMENT.md`、`web/docs/ARCHITECTURE.md`、`web/docs/DEVELOPMENT.md` |
| 多设备接入 Agent 的源、解析插件、消息协议、可靠性与部署安全设计，MVP 运行与配置说明，以及 WinUI3 桌面端与 JS 解析脚本设计 | `tool/DEVICE_AGENT_DESIGN.md`、`tool/DEVICE_AGENT_UI_DESIGN.md`、`tool/README.md` |
| 系统上下文、容器和组件工程图 | `docs/diagrams/simframework-software-engineering.drawio` |

## 更新原则

- 新增、移动或删除文档时，同步更新 [`docs/README.md`](../README.md) 的索引。
- 已完成或被取代的设计方案直接删除或合并回主文档，不再保留归档目录。
- 文档应描述当前代码真实状态，不保留已经失效的命令或目录。
- 先改离代码最近的 README，再更新项目级总览。
- 新增模块时，至少写清楚用途、入口文件、注册位置和验证方式。
- 修改接口或认证时，写清楚前端调用路径、令牌处理和失败处理。
- 修改前端请求取消、重试或 CI 静态检查时，写清楚重复请求判定、重试上限以及只读门禁命令。
- 修改环境变量时，写清楚变量名、默认值来源和影响范围。
- 修改密码、JWT、Refresh Token、权限或限流策略时，写清楚兼容行为、会话失效范围和部署前置条件。
- 修改组织层级或数据范围时，写清楚业务数据归属、主组织/兼职关系、范围合并、写入校验、缓存失效、审计、迁移和越权测试；权限预览不能替代后端实时判定。
- 修改文件能力时，写清楚 provider 名称、凭据来源、大小/类型限制、临时文件、外部工具、失败清理、权限和移动端组件行为。
- 修改业务资源公开/私有/分享能力时，写清楚资源类型、所有者判定、关联文件同步、令牌版本与保存方式、有效期、撤销/过期语义、旧版本失效或迁移策略及防枚举响应；业务正文不得固化 provider 对象键或短期预签名 URL。
- 修改头像时，写清楚前端裁剪/压缩参数、后端魔数/大小校验、公开读取键范围和实际存储 provider；不得重新引入 URL 输入。
- 修改定时任务时，写清楚 Worker 定义提供者、HTTP SSRF 边界、Command 白名单/Shell 隔离、Service Method 显式注册、执行参数加密、Quartz Trigger 同步、幂等/并发要求和生产 JobStore 限制。
- Draw.io 图描述的是当前架构；变更系统边界、容器、关键组件或依赖方向时，同步更新图源并运行结构校验。缺少本地 Draw.io CLI 时至少提交已验证的可编辑源，不伪造预览产物。
- 修改数据库驱动时，写清楚原生库/系统包要求，不要把存在公告的通用驱动包继续作为隐式传递依赖。
- 修改日志管道时，写清楚控制台/文件开关、统一格式、默认值、采集方式和敏感信息限制。
- 修改本地化时，写清楚支持文化、资源所有权（前端静态文案或后端错误/菜单/站点配置）、key 命名约定（统一为「冒号分隔 + 大驼峰」，动态键经由 `messageKey(...parts)` 生成）、请求语言头、错误降级顺序、资源完整性测试和新增文案约定。
- 不要把密钥、生产连接串、真实账号密码写入文档。

## 提交前检查清单

提交代码前请确认：

- 根目录 README 的项目结构仍然准确。
- 子项目 README 的启动命令可以执行。
- 新增或删除的模块已在架构文档中体现。
- 新增环境变量已记录。
- 生产密钥没有写入配置或文档，部署所需密钥变量和最低要求已记录。
- 新增测试、构建或迁移步骤已记录。
- Windows/Linux 依赖锁文件、许可证策略、Shared Framework 边界、OpenAPI 契约/生成类型和性能预算变化已记录。
- 文档中的路径、命令、端口、接口前缀与代码一致。
- AI 编码规范仍覆盖当前项目结构、验证命令和协作要求。
- 已检查 `docs/core/ENTERPRISE_SYSTEM_GUIDE.md`；如当前能力、风险优先级或完成标准变化，已同步更新对应章节。
- 企业审查产生的未实施建议已进入 `docs/core/enterprise/ASSESSMENT.md`，并明确标注待确认，没有把建议自动当作授权。
- 新增用户可见文本在后端与 Web 的中英文资源中都有对应项，且切换语言后错误消息与 UI 文案一致。

## 给后续编码助手的要求

在本项目中修改代码时，请把文档同步作为完成标准的一部分。若变更影响用户如何启动、配置、扩展、测试或部署项目，应在同一次修改中更新对应文档，并在最终说明中列出更新过的文档文件。

任何功能增强和缺陷修复都需要检查 [`ENTERPRISE_SYSTEM_GUIDE.md`](ENTERPRISE_SYSTEM_GUIDE.md)。即使无需修改，也应在交付说明中写明已检查及无需更新的原因。

AI 编码助手还应遵守 [`AI_CODING_GUIDELINES.md`](AI_CODING_GUIDELINES.md)。

通知能力变更还必须同步检查以下事实是否一致：

- `docs/design/notification/NOTIFICATION_PLATFORM_DESIGN.md` 的实施状态、Provider 能力和未完成项。
- `Framework:Messaging`、`Framework:Caching` 的部署配置与 Secret 注入说明。
- “系统管理 → 业务编号”的业务场景记录、权限、前端页面、`system_business_number_definition`/`framework_number_sequence` 迁移说明；格式语义、日期分段和多实例唯一性必须与 `api/README.md` 保持一致。
- “系统管理 → 系统配置”和“系统管理 → 数据字典”的表结构、唯一键、权限、值类型、默认种子参数、页面（含独立字典数据 Tab 与响应式布局）和中英文文案；敏感凭据不得进入配置值或字典值。
- System 通知 DTO、权限、动态路由、OpenAPI 当前契约和 Web 生成类型。
- Web “通知配置”与 Provider 供应商选项的术语边界，以及 `providerSchemas.ts` 的 Provider 字段、Secret 标记、默认值、Tooltip 帮助和中英文资源；普通后台配置不得只提供原始 JSON。
- 账号通知目标的 System Contracts 端口、System 内部实现、显式地址优先级、脱敏搜索和测试通知两种目标模式。
- 站内实时 Hub 路径、鉴权、Vite/生产代理 WebSocket 配置、Access Token 日志脱敏、先落库后推送、已读同步、自动重连，以及仅在实时变更或展开面板后读取 API 的策略；多实例 backplane 未完成时必须明确限制。
- 新增表、迁移/回滚要求、Data Protection Key 持久化和外部 Provider 联调清单。
- “官方/成熟 SDK”与“标准 HTTP 合同网关”的措辞必须准确，写明 RocketMQ 5.x gRPC 等版本约束，不能把仅完成代码接入但尚未完成的生产联调描述为已上线。


## 系统 E2EE 文档同步

System 的 E2EE 身份、设备缓存或通行密钥语义变化时，同批更新架构、开发验证、企业级验收与子项目 README；明确包裹格式、旧记录迁移、失败回退和客户端发布要求，不能把仅有哈希清单描述为可抵御恶意发布端。

设备信任相关变更须说明本机缓存格式、旧记录迁移、浏览器能力失败行为、退出/清除竞态、用户验证与硬件绑定边界，不得把不可导出 CryptoKey 描述为可抵御 XSS 的安全硬件。

通行密钥功能必须记录 PRF 与普通 WebAuthn 签名的区别、RP ID/Origin 配置、用户验证、单次挑战、服务端密文托管、密码/RK 回退、域名变更、解绑与身份重置边界。测试替身、虚拟验证器、真实硬件验证结果分别说明。

# 通用系统平台基座

本仓库是通用系统平台基座：以 SimFramework 为基础，保留认证授权、系统配置、审批流、后台任务、多租户与文件/通知等通用能力，供业务模块按约定的阶段与端口边界接入。

## 仓库组成

| 目录        | 用途                                              | 文档                                  |
| ----------- | ------------------------------------------------- | ------------------------------------- |
| `api`       | 后端服务、认证、数据访问与系统模块           | [`api/README.md`](api/README.md)      |
| `client`    | Windows WPF 客户端                           | [`客户端说明`](client/Sim.Admin.Wpf.Client/README.md) |
| `web`       | Web 管理端、静态/动态路由、工作台与审批画布       | [`web/README.md`](web/README.md)      |
| `docs`      | 项目级架构、开发流程、升级基线与文档维护规则      | [`docs/README.md`](docs/README.md)    |
| `deploy`    | 生产部署、CI/CD、密钥、TLS 与回滚                 | [`deploy/README.md`](deploy/README.md) |
| `tool`      | 多设备接入 Agent（串口/TCP/文件 → 本机/中心 WebSocket，含 spool、热加载与 Windows 托盘） | [`实现说明`](tool/README.md) · [`设计方案`](tool/DEVICE_AGENT_DESIGN.md) · [`UI 设计`](tool/DEVICE_AGENT_UI_DESIGN.md) |
| `AGENTS.md` | AI 编码助手协作规则                               | [`AGENTS.md`](AGENTS.md)              |

## 保留能力

- **认证与授权**：本地账号登录、JWT + Refresh Cookie、TOTP 双重验证、密码找回、外部登录扩展点；用户-角色-权限三级 RBAC，权限变更会吊销受影响用户的 Refresh Token 强制重新登录。域账号（LDAP/AD）接入在后续阶段实施。
- **多租户**：保留 `Sim.Tenant` 模块与租户管理页，按租户隔离数据与会话；默认租户与宿主租户为内置数据。
- **审批流**：审批定义 + 运行时实例，支持条件节点、会签/或签/顺序会签、排他网关与并行 fork/join、转交、加签、退回、撤回与实例监控；画布式设计器基于 Vue Flow。
- **系统管理**：系统配置、数据字典、业务编号、文件存储（本地/数据库/S3/COS/OSS）、定时任务、通知服务（邮件、短信、Webhook、站内信、机器人等）、组织架构、租户管理与设备会话管理。
- **组织架构**：组织单位树、成员归属与审批人来源解析，通过稳定端口对审批模块开放。
- **开放平台**：第三方应用管理、AppId/AppSecret 令牌签发、权限范围与 IP 白名单、签发及调用审计；后台提供应用管理和访问日志。
- **文件与内容**：provider 无关的内容文件 ID、上传/下载、模板渲染、CSV/JSON/HTML 导出、Office-to-PDF 与图片处理。
- **Web 管理端**：Paper Ocean 设计系统与多套商务皮肤、明暗主题、动态路由、按钮级权限门禁、国际化（简体中文/英文）与平台工作台。

## 本地运行概览

### 后端

```bash
cd api
dotnet restore
export JwtSettings__IssuerSigningKey="<从本机安全存储读取的至少 32 字节随机值>"
dotnet run --project src/Sim.Admin.HttpApi.Host
```

本机开发可在仓库根目录运行 `docker compose -f api/env/compose.yaml up -d --wait` 启动 PostgreSQL、Redis 和 RabbitMQ；API 与 Web 在宿主机运行。端口、凭据、后端连接示例及数据清理见 [本地开发基础设施](docs/ops/LOCAL_INFRASTRUCTURE.md)。

环境配置文件不入库：仓库只提交共享基线 `appsettings.json` 和 `src/Sim.Admin.HttpApi.Host/appsettings.Example.json` 模板（基于 development，数据库、Redis 和 RabbitMQ 连接默认对齐 `api/env/compose.yaml`；仅包含公开开发凭据，管理员密码留空），Development/Staging/Production 的 `appsettings.{Environment}.json` 一律由 `.gitignore` 忽略。启动时如果当前环境对应的文件不存在，会基于模板自动生成本地文件；环境变量与密钥注入的优先级更高。所有环境启动前都必须提供至少 32 字节的 `JwtSettings__IssuerSigningKey`。

开发环境默认不自动执行数据种子。服务启动后访问 Web 的 `/setup` 页面设置管理员账号与密码、站点名称、默认语言与主题、是否开放注册、登录失败上限和锁定时长；提交后前端展示初始化进度，后端先写入管理员账号，再创建不可登录的系统账号 `system`，并由系统账号执行种子（角色、模块、路由、权限、系统配置、组织与审批基础数据）。初始化完成后仍可用 `dotnet run --project src/Sim.Admin.HttpApi.Host -- --seed` 幂等补齐升级新增的基础数据。服务启动后可访问 `/health/live`、`/health/ready` 和兼容入口 `/health-status`；非生产环境 Swagger 匿名开放，接口文档聚合入口为 `/swagger/all/swagger.json`。

消息与缓存的部署级选择位于 `Framework:Messaging`、`Framework:Caching`。开发默认 Local；RabbitMQ 使用 `amqp(s)` 连接串，Kafka 使用 Bootstrap Servers，RocketMQ 使用 5.x gRPC Endpoint，Redis/Hybrid 使用仅由 Secret 注入的 Redis 连接串；SignalR backplane 由 `Framework:SignalR:UseRedisBackplane` 控制。通知渠道账号在后台维护，但 Data Protection Keys 必须持久化，否则重启后无法解密已保存凭据。

### Windows 客户端

```powershell
dotnet run --project client/Sim.Admin.Wpf.Client/Sim.Admin.Wpf.Client.csproj
```

WPF 客户端位于 `client/Sim.Admin.Wpf.Client`，直连同一 Web API，提供账号登录与双重验证、账号安全、工作台与资料、通知、审批中心（列表、详情、流程草稿新建/筛选/画布新增和拖动节点/四边端点拖线/三种线型切换/节点与连线编辑/校验与同意/驳回/转交/加签/退回/撤回/催办/终止）、授权管理（含模块路由）与系统管理（含组织架构、租户）、定时任务、文件存储配置和通知管理，以及四套风格 × 明暗切换。桌面 UI 基于自建的 Naive UI 复刻组件库（表单、展示、容器、浮层四族，不使用任何投影），可用 `dotnet run --project client/Sim.Admin.Wpf.Client/Sim.Admin.Wpf.Client.csproj -- --gallery` 打开组件库预览。详见 [客户端说明](client/Sim.Admin.Wpf.Client/README.md)。

### Web 前端

```bash
cd web
pnpm install
pnpm dev
```

Web 开发服务器默认使用 HTTP，无需本机证书；`web/.cert/dev-cert.pem` 与 `web/.cert/dev-key.pem` 同时存在时自动使用 HTTPS。证书只存在一个时会明确报错。实际地址以 Vite 输出为准。

默认开发地址由 Vite 输出，一般是 `http://localhost:5173`。接口地址由 `VITE_API_URL` 控制，见 `web/.env.*`；本地代理目标可用仅供开发进程读取的 `VITE_API_PROXY_TARGET` 临时覆盖。Access Token 仅保存在页面内存，Refresh Token 由后端写入 HttpOnly Cookie；登录页“记住我”只在本地保存用户名用于下次预填。访问自定义根路径时默认跳转 `/admin/home`。开放注册开启后，登录页以同一布局切换到注册卡片（也可直接访问 `/register`）。部署时前后端必须正确配置 HTTPS、CORS 凭据和 Cookie 域策略。

后端四组 OpenAPI（`admin`、`system`、`approval`、`openplatform`）在 CI 中重生成并对既有兼容基线进行比较，Web 类型由这些契约生成。前后端流水线还包含锁定依赖、漏洞与许可证门禁；Web 将经过仓库评审的 `pnpm-lock.yaml` 作为可信重复安装基线，新依赖解析和升级仍执行 pnpm 11 默认的发布等待期。后端发布生成 SPDX SBOM，并以非 root、只读根文件系统启动容器验证 live/ready；前端执行类型检查、零警告 ESLint、全部单元测试、生产构建和包体预算。Jenkins 多分支作业可由 GitHub Webhook 触发，镜像推送到 `repo.dulm.net`；`develop` 通过 CI 后自动部署测试，`v*` 标签经人工审批后部署生产，具体作业、凭据和目标机配置见 [`deploy/README.md`](deploy/README.md)。

## 本地化

后端、Web 统一支持简体中文和英文。Web 保存用户的语言偏好，并在请求中发送 `Accept-Language: zh-Hans` 或 `Accept-Language: en`。资源按所有权拆分：组件静态文案归 Web；业务错误、动态菜单、站点展示配置和登录页品牌标语归后端。菜单 key 与前端自有 i18n key 统一为 ABP 风格「冒号分隔 + 大驼峰」（`Menu:ApprovalMonitor`、`Common:Search`），静态文案直接写 `Domain:Segment[:Segment]`，动态拼接键由 `messageKey(...parts)` 生成，禁止 `domain.camelCase` 点号键。Web 启动时通过 `/api/admin/client-localization` 加载后端资源；接口返回 `Vary: Accept-Language` 与 `Cache-Control: no-store`，错误响应直接使用后端当前文化的 `error.message`。新增用户可见文案时必须同时补充中英文资源。

本地化入口：

- 后端资源：`api/src/Sim.Admin.Domain.Shared/Localization/Layered`
- Web 运行时：`web/src/i18n/index.ts`；静态资源：`web/src/i18n/messages`

## 文本与换行约定

仓库通过根目录 `.editorconfig` 与 `.gitattributes` 统一换行：普通文本使用 CRLF，`*.sh`、`Dockerfile` 和 `Jenkinsfile` 使用 LF。这样 Windows/Visual Studio 与 Linux 编辑器打开普通文本都不会出现混合行尾，Linux 脚本也保持可直接执行。

## 文档维护约定

代码更新后请同步检查文档。尤其是以下变更：

- 新增、删除或重命名子项目、模块、页面、路由。
- 修改启动命令、环境变量、端口、数据库配置、认证流程。
- 修改后端模块边界、接口路径、Swagger 分组、数据种子或部署方式。
- 修改前端布局、模块注册方式、动态路由、主题系统或 API 封装。
- 引入新的必需依赖、工具链、测试命令或构建流程。

完整文档地图与阅读顺序见 [`docs/README.md`](docs/README.md)，维护规则见 [`docs/core/DOCUMENTATION.md`](docs/core/DOCUMENTATION.md)。已完成或不再适用的方案直接删除或合并回主文档。

业务系统从本仓创建独立仓库并同步基座更新时，目录分区、`upstream` 合并流程与升级验收见 [`docs/core/FRAMEWORK_SYNC.md`](docs/core/FRAMEWORK_SYNC.md)。

涉及新系统设计、功能增强、缺陷修复、架构治理或生产交付时，还应检查 [`docs/core/ENTERPRISE_SYSTEM_GUIDE.md`](docs/core/ENTERPRISE_SYSTEM_GUIDE.md) 中的建设清单、当前基线和完成定义。

本次企业级优化审查记录在 [`docs/core/enterprise/ASSESSMENT.md`](docs/core/enterprise/ASSESSMENT.md)。其中建议项都需要在实施前再次询问并取得确认。可编辑的软件工程图位于 [`docs/diagrams/simframework-software-engineering.drawio`](docs/diagrams/simframework-software-engineering.drawio)。

AI 编码助手还应遵守 [`docs/core/AI_CODING_GUIDELINES.md`](docs/core/AI_CODING_GUIDELINES.md)。

# SimFramework API

本仓库是通用系统平台基座的后端，由 SimFramework 裁剪而来，保留审批、系统权限、组织与租户、文件存储、通知、后台任务监控等后端能力。

本地依赖通过 `api/env/compose.yaml` 启动：在仓库根目录执行 `docker compose -f api/env/compose.yaml up -d --wait`。后端仍使用本机 .NET SDK；PostgreSQL、Redis、RabbitMQ 的连接配置与初始化步骤见 [本地开发基础设施](../docs/ops/LOCAL_INFRASTRUCTURE.md)。

SimFramework API 是项目的后端服务，基于 .NET 10、Sim 框架、SqlSugar 和模块化架构，提供审批流、系统权限、组织与租户、文件存储与通知等接口。

## 技术栈

- .NET 10
- ASP.NET Core Web API
- Sim 框架（ABP 仅保留模块体系与本地化）
- SqlSugarCoreNoDrive（按数据库显式接入驱动）
- ABP Mapperly（编译期对象映射）
- JWT Bearer 认证
- Swagger / OpenAPI
- Serilog
- Scriban、LibreOffice/ImageMagick 可选外部工具、AWS S3 SDK

## 项目结构

```text
api/
├── Directory.Packages.props        # NuGet 中央包版本
├── src/
│   ├── Sim.Admin.Domain.Shared/
│   ├── Sim.Admin.Domain/
│   ├── Sim.Admin.Application.Contracts/
│   ├── Sim.Admin.Application/
│   ├── Sim.Admin.SqlSugar/
│   └── Sim.Admin.HttpApi.Host/      # 服务入口
├── framework/
│   ├── Sim.Framework.Core/
│   ├── Sim.Framework.Application/
│   ├── Sim.Framework.Application.Contracts/
│   ├── Sim.Framework.Domain/
│   ├── Sim.Framework.Domain.Shared/
│   └── Sim.SqlSugar.Core/
├── modules/
│   ├── approval/                    # 审批定义、实例、任务与实例监控
│   ├── openplatform/                # 第三方应用、令牌与审计
│   └── system/                      # 用户、角色、权限、租户、配置、调度、文件、通知与后台任务
└── tests/
    └── Sim.Security.Tests/          # 安全、架构和依赖治理测试
```

Windows WPF 客户端位于仓库根目录的 `client/Sim.Admin.Wpf.Client`，与 `api/` 同级，使用同一 Web API；账号安全、审批中心、授权与系统管理、定时任务、文件存储、通知、认证与四套主题说明见 [客户端 README](../client/Sim.Admin.Wpf.Client/README.md)。该 Windows UI 不加入面向跨平台后端构建的 `Sim.slnx`。

## 快速开始

开放平台的接口、Scope 边界、凭据轮换和审计说明见 [OpenPlatform 模块](modules/openplatform/README.md)。存量站点需先执行受控数据库迁移，再运行种子补齐模块、权限与菜单。

```powershell
$env:JwtSettings__IssuerSigningKey = "<至少 32 字节的随机密钥>"
dotnet restore
dotnet run --project src/Sim.Admin.HttpApi.Host
```

默认开发地址：

- API / Swagger：`https://localhost:5001/swagger/index.html`
- 直连后端时 Swagger 文档匿名开放，无登录页与会话；导入 Apifox 请填写聚合文档 `https://localhost:5001/swagger/all/swagger.json`，无需逐模块导入。
- Swagger 可用时根路径 `/` 会重定向到 Swagger UI 页面 `/swagger/index.html`。
- Production 默认不开放 Swagger；显式设置 `Swagger:AllowInProduction=true` 后才会注册。Docker Compose 为同域 nginx 部署开启该开关，并通过 nginx Basic Auth 保护 `/swagger/` 与 `/api/swagger-config`。
- Swagger 可用时由 `UseStaticFiles()` 提供 `wwwroot/` 下的静态资源：`/favicon.ico`（与前端 `web/public/favicon.ico` 同源的浏览器标签页图标）及 Swagger 页面引用的 `/swagger/favicon-32x32.png`。Production 默认不注册静态文件中间件，这些路径返回 404。

环境配置模板位于 `src/Sim.Admin.HttpApi.Host/appsettings.Example.json`，其初始内容基于 development 环境，数据库、Redis 和 RabbitMQ 默认连接 `api/env/compose.yaml`，包含公开开发凭据；管理员密码留空。仓库只提交共享基线 `appsettings.json` 与该模板，所有 `appsettings.{Environment}.json`（Development/Staging/Production，可能包含连接串与密码）都不入库，由仓库根 `.gitignore` 忽略；启动时如果当前环境对应的文件不存在，会自动从模板复制生成，请按本机/部署环境切换数据库并补齐消息、缓存等连接信息。该文件按 ASP.NET Core 默认顺序加载，环境变量与密钥注入会覆盖模板默认值与留空项。

系统完成首次初始化后，如需补齐或刷新种子数据：

```bash
dotnet run --project src/Sim.Admin.HttpApi.Host -- --seed
```

系统未完成初始化时，启动自动种子与该命令都会跳过，避免在管理员账号确定前写入种子。初始化后的种子会确保系统账号存在，并以系统账号作为创建人执行。种子支持幂等执行。HTTP Host 组合的种子会创建系统管理、授权管理与审批模块，以及对应的系统管理、授权管理、审批中心菜单路由；重复执行会补齐缺失菜单、把系统与授权菜单的历史文字标题迁移为稳定 `Menu:*` key，并把挂载到后台布局的模块根路由保存为相对路径。授权种子会补齐用户、角色、权限、模块和路由五个子菜单；组织架构与租户管理由系统管理种子下发，并从历史授权菜单幂等迁移。user 模块的根路由与个人资料路由保持可访问但 `menu=false`。Admin Domain/Application 不直接引用业务模块实现。

服务启动后，Web 访问 `/setup` 进入首次初始化页，设置管理员账号与密码、站点名称、默认语言与主题、是否开放注册、登录失败上限和锁定时长。初始化接口为匿名 `GET /api/admin/setup/status`、`GET /api/admin/setup/progress` 与 `POST /api/admin/setup/initialize`，并用 `system.setup.state` 配置项标记状态；已完成或检测到既有用户时拒绝重复初始化。提交后后端先创建或更新管理员账号，再创建不可登录的系统账号 `system`，由系统账号作为创建人执行基础种子（创建时间为当前时间），随后分配 Admin 角色并写入初始化标记；前端根据进度接口展示初始化阶段。初始化写入的站点与安全策略可在“系统管理 → 系统配置”继续调整，登录锁定与自助注册开关立即生效。自助注册成功后由系统配置 `security.registration-role-codes`（默认 `User`，仅非内置角色）分配角色，客户端提交的角色参数被忽略；内置 `User` 角色由种子幂等补齐 `approval` 模块访问。升级站点发布后执行一次 `--seed` 才会写入该配置项并补齐 `User` 角色基础权限。

租户创建不传管理员密码时生成随机初始密码并在创建结果中返回，不再回退固定默认密码；不再预置默认 `admin` 占位账号，首次管理员由 `/setup` 创建。源码没有内置密码。显式执行受控数据库结构迁移：

```bash
dotnet run --project src/Sim.Admin.HttpApi.Host -- --migrate
```

Production/Staging 应在迁移前备份数据库，迁移成功后再启动服务，回滚依赖该备份。

## 主要配置

配置入口在 `src/Sim.Admin.HttpApi.Host/appsettings.json`（共享基线）、`appsettings.Example.json`（模板），以及本机/部署环境自带、不入库的 `appsettings.{Environment}.json`。

常用配置：

- `App:SelfUrl`：服务自身地址。
- `Security`：密码策略、锁定策略、会话、按 PC/Pad/Phone 分类的 `DeviceSessionLimits`、Refresh Token、验证码有效期、SMTP/SMS 网关和 TOTP 发行方。
- `JwtSettings`：JWT issuer、audience、签名密钥、过期时间和时钟偏移（默认 1 分钟）。
- `FileManagement`：只保留文件处理临时目录、LibreOffice/ImageMagick 这类部署级工具开关，以及对象存储 Webhook 签名密钥 `ObjectStorageWebhookSecret`。未配置该密钥时 Webhook 失败关闭；默认存储、上传上限、本地/数据库/对象存储参数和凭据由系统管理接口持久化到数据库。
- 模块发现：由入口程序集的 `Sim.*` 引用链自动收集，不需要配置项；业务模块通过 Host 的 `Business/` 组合根接入。
- `Swagger:AllowInProduction`：是否在 Production 注册 Swagger，默认 `false`；Docker Compose 显式设置为 `true`，并依赖 nginx Basic Auth 保护文档面。
- `Swagger:Documents`：Swagger 分组配置。
- `RateLimiting`：`[RateLimit]` 的默认次数、窗口和排队上限。
- `DataBase`：数据库、Code First、数据种子配置，主要在环境配置文件中。
- `DataProtection`：认证辅助 Cookie 等 Data Protection 密钥的应用名和持久化目录。
- `Logging:Console:Enabled`：控制台日志开关，默认 `true`。
- `Logging:File:Enabled` / `Logging:File:Path`：文件日志开关和路径。Development 默认写入 `Logs/log.txt`，其他环境默认关闭；控制台与文件可以同时启用。
- `Logging:MinimumLevel:Default`：全局最低日志级别，默认 `Information`（`INF`），不再按 Debug/Release 构建条件切换；`Logging:MinimumLevel:Override:<类别>` 按 `SourceContext` 前缀覆盖单个类别（如 `Volo.Abp`、`Quartz`、`Microsoft`）。这些值在对应环境配置文件中维护，无需改代码。
- `OpenTelemetry`：`ServiceName`/`ServiceVersion`/`Environment` 为资源标签；`Tracing:Enabled`/`Metrics:Enabled` 分别控制链路与指标；`Otlp:Endpoint` 留空时不向收集器导出，只保留 `/metrics` 抓取端点。

所有来源统一经过 Serilog，包括 Host、ASP.NET Core、ABP、SqlSugar、数据种子、Quartz Job 和业务服务。控制台与文件共享单行格式：`[模块][yyyy-MM-dd HH:mm:ss.fff zzz Level] 日志内容`；原始 `SourceContext` 会归并为 `ScheduleTask`、`System`、`RBAC`、`Framework` 或 `Admin`，消息、JSON 或异常内部换行转义为 `\n`，便于容器采集和按行检索。可通过环境变量覆盖：

```text
Logging__Console__Enabled=true
Logging__File__Enabled=false
Logging__File__Path=Logs/log.txt
Logging__MinimumLevel__Default=Information
Logging__MinimumLevel__Override__Quartz=Warning
```

`appsettings*.json` 不保存可用的 JWT 密钥。Development、Staging 和 Production 都必须由本机 Secret Manager、密钥管理系统或环境变量提供：

```text
JwtSettings__IssuerSigningKey=<至少 32 字节的随机密钥>
```

本机 Development 环境也可使用 .NET User Secrets：`dotnet user-secrets set "JwtSettings:IssuerSigningKey" "<至少 32 字节的随机密钥>" --project src/Sim.Admin.HttpApi.Host`。项目文件中的 `UserSecretsId` 仅用于定位用户目录下的 `secrets.json`，不包含密钥；User Secrets 不加密，不能用于生产部署。

使用密钥文件时，将 `SIM_SECRETS_DIRECTORY` 设置为 API 进程可读取的绝对目录路径，并在该目录创建文件 `JwtSettings__IssuerSigningKey`，文件内容为至少 32 字节的密钥值。该目录会通过 Key-Per-File 加载，文件值优先于同名环境变量；修改后重启 API。Windows 下若进程级未设置该变量，会依次回退读取用户级、系统级环境变量（非 Windows 平台仅读进程级），设置后同样需要重启 API。不要把密钥目录放在仓库内。

缺少或长度不足时应用会在启动阶段拒绝接受请求；如果登录时报 `No JWT signing key is configured`，检查实际启动 API 进程的环境变量或 Secret 注入，而不是只检查 Web 进程的环境。修改密钥后重启 API；更换现有单密钥会使旧 Access Token 失效，轮换时应使用 `kid` 多密钥配置。`StringEncryption__DefaultPassPhrase`、Swagger 客户端密钥和证书口令如启用，也应由外部密钥源提供。Production/Staging 默认设置 `EnabledCodeFirst=false`、`EnabledDbSeed=false`。

开发环境同样默认设置 `EnabledDbSeed=false`，避免启动服务时意外创建固定基础账号。首次初始化直接通过 Web `/setup` 创建管理员并确定站点默认值，提交后由后端创建系统账号并以系统账号为创建人执行基础种子；初始化完成前启动自动种子与 `--seed` 都会跳过。

模板默认的 PostgreSQL、Redis 和 RabbitMQ 连接仅供本机 Compose 开发。Production/Staging 部署时至少需要通过外部配置覆盖数据库类型与连接串：

```text
DataBase__ConnectionConfigs__0__DbType=<PostgreSQL|MySql|SqlServer>
DataBase__ConnectionConfigs__0__ConnectionString=<由密钥管理系统提供的连接串>
DataProtection__KeysPath=<持久化且权限受控的密钥环目录>
```

切换数据库时须在数据访问层显式安装对应数据库的官方 ADO.NET 驱动。连接配置缺失或连接串为空时，应用会拒绝启动。

生产容器或多实例部署必须设置 `DataProtection__KeysPath`，把 ASP.NET Core Data Protection 密钥环持久化到权限受控的共享存储，并使用平台密钥管理能力保护静态密钥；非开发环境缺少该配置时应用会拒绝启动。

健康检查端点：

- `/health/live`：进程存活检查。
- `/health/ready`：包含真实数据库连接检查。
- `/health-status`：兼容入口，执行全部检查。

可观测性：链路追踪（ASP.NET Core / HttpClient / Redis）与指标统一由 OpenTelemetry 采集，配置 `OpenTelemetry:Otlp:Endpoint`（如 `http://collector:4317`）后按 OTLP 推送；`Metrics:Enabled=true` 时同时暴露 Prometheus 抓取端点 `/metrics`。除框架 instrumentation 外还采集业务指标：`sim.db.command.count`、`sim.db.command.duration`（标签 `db.command`、`status`）和 `sim.schedule_task.count`、`sim.schedule_task.duration`（标签 `task.group`、`task.key`、`trigger.type`、`status`），Meter 名为 `SimFramework`。`/metrics` 不带鉴权，生产环境应只在内网开放或经反向代理限制来源。

生产镜像使用仓库根目录的 `api/Dockerfile`，基于 .NET 10 多阶段、非 root 构建，运行用户为数字 UID/GID `1000:1000`；构建时只在基础镜像尚未提供 UID/GID `1000` 时创建对应条目。宿主机 bind mount 的 `secrets`、`api-logs`、`api-data` 目录必须与该 UID/GID 对齐；`secrets` 至少需要可读，`api-logs` 和 `api-data` 需要可写：

```bash
cd api
docker build -f Dockerfile -t repo.dulm.net/sim-api:<tag> .
```

`Dockerfile.local` 只用于本机快速构建，不进入发布链路。`Jenkinsfile` 会执行锁定还原、依赖漏洞审计、Release 编译、OpenAPI 重生成与漂移检查、测试、发布、SBOM 生成/校验、生产镜像构建/冒烟和镜像推送。镜像阶段会检查镜像用户、进程 UID `1000`、只读根文件系统、权限收缩以及 live/ready 探针。Jenkins Agent 需要提供 .NET 10 SDK、Docker、curl、ssh/scp 及常用 POSIX 工具；本地工具由 `.config/dotnet-tools.json` 固定版本并通过 `dotnet tool restore` 还原。`develop` 推送在 CI 通过后自动部署测试，`v*` 标签触发 `sim-release` 审批发布生产，详见 [`../deploy/README.md`](../deploy/README.md)。

## 模块说明

### system

System 模块合并了原 RBAC、租户与后台监控能力，包含身份、权限、模块/路由、文件存储、定时任务、通知、租户管理与系统配置：

- 用户注册、登录、刷新令牌、撤销令牌：`UserService`
- 删除用户：`DELETE /api/system/user/{id}` 先清理角色关联、偏好、E2EE 身份与会话，再通过 `IUserDeletedHandler` 端口执行跨模块清理，最后删除用户行；内置账号仍返回 `Sim:System:BuiltInUserProtected`。System 侧处理器删除外部登录绑定、刷新令牌、组织成员、组织负责人引用、存储配额与用户拥有的分享链接；可删除的业务归属数据通过 `IUserDeletedHandler` 由各模块自行清理，需要留痕的审批实例与记录保留业务事实并解除或匿名化个人关联。文件元数据、下载记录与访问审计作为审计/公共资源保留。已登录用户可在账户安全页输入当前密码后调用 `POST /api/system/user/delete-my-account` 主动注销，后端校验当前用户与密码（限流 `account-deletion`，3 次/60 秒）后复用同一清理流程，并清除刷新令牌 Cookie；内置账号返回 `Sim:System:BuiltInUserProtected`。
- 审计字段：可编辑业务实体统一继承 `AuditedAggregateRoot` / `AuditedEntity`，SqlSugar AOP 自动回填 `CreationTime`、`CreatorId`、`LastModificationTime`、`LastModifierId`；`CreationTime` 自动带 `CURRENT_TIMESTAMP` 默认值，存量表补列不会因 NOT NULL 失败。列表 DTO 必须返回四项审计字段，管理端统一展示。`GET /api/system/user/display-names?userIds=...`（登录即可，单次最多 200 个 ID）批量返回 `{ displayName, userName }`：`displayName` 为昵称（昵称为空时回退账号名），供创建人/修改人列展示；`userName` 为账号名，供悬停气泡展示。昵称在创建、更新和自助资料中均为必填；`GET /api/system/user/nick-name-availability?nickName=...&excludeUserId=...`（登录态，限流 60 次/分钟）返回布尔值，供表单输入期间防抖查重，最终仍由创建/更新接口的唯一性校验兜底。
- 用户默认密码：`POST /api/system/user` 不再使用调用方密码，`CreateUserDto.password` 仅用于自助注册；管理端创建与 `POST /api/system/user/{id}/reset-user-password` 统一读取系统配置 `security.user-default-password`（安全策略组，首次种子生成随机值，缺失时回退 `App:AdminDefaultPassword`，值不符合密码策略时返回 `Sim:System:DefaultUserPasswordInvalid`）。重置要求 `system:user:reset-password`，会清空锁定与安全挑战并吊销目标用户全部 Refresh Token；内置账号返回 `Sim:System:BuiltInUserProtected`。
- 自助注册角色：`POST /api/system/user/register` 与 `POST /api/system/external-login/register-with-external-login` 在创建用户后读取系统配置 `security.registration-role-codes`（`system.security` 分组，英文逗号分隔，默认 `User`），只分配非内置角色，内置角色被忽略；配置缺失或为空时回退 `User`。自助注册不接收 `CreateUserDto.roleIds`。内置 `User` 角色由种子幂等补齐 `approval` 模块访问，管理员可在角色管理中调整。
- 角色管理：`RoleService`
- 权限管理：`PermissionService`
- 租户管理：`TenantAppService`（`/api/system/tenant`），提供租户创建、更新、启停、查询与模板/分配查询
- 后台任务监控：`MonitorService`（`/api/system/monitor`），只读返回 Quartz 任务运行信息
- 菜单管理：`MenuService`
- 系统配置：`SystemConfigService`

- `ModuleConfigAppService`
- `RouteConfigAppService`
- `FileStorageConfigurationService`：维护默认 provider、上传上限、本地/数据库/S3/COS/OSS 参数；对象存储密钥使用 Data Protection 加密入库，读取接口只返回是否已配置。`POST /api/system/file-storage-configuration/test` 使用当前表单值测试连接（本地目录写入、数据库查询、对象存储列桶），未提交新密钥时回退到已保存的加密凭据，返回 `success` 与失败原因。
- `ContentFileService`：认证上传、匿名/认证受策略读取的业务内容文件入口；返回 `/api/system/content-file/<文件ID>` 稳定 URL，并按 `system_file_metadata.Provider` 回到文件上传时的 provider。读取响应带 `Cache-Control: private, max-age=86400` 与基于文件 SHA-256 的强 ETag，`If-None-Match` 命中直接返回 304 且不打开存储；文件按 ID 内容不可变，因此 ETag 长期稳定。
- `ResourceAccessService`：与业务模块解耦的所有者、公开/私有、显式分享和关联文件授权策略。策略、引用、分享令牌摘要和去重设备摘要分别保存到 `system_resource_access_policy`、`system_resource_file_reference`、`system_resource_share_link`、`system_resource_share_link_viewer`。
- `ScheduleTaskService`：显示并管理四类任务：HTTP/HTTPS 请求、带参数的命令/程序、显式注册的 Service 方法，以及通过 `IScheduleTaskDefinitionProvider` 登记的系统内置 Job。前三类可从后台新增和删除，内置 Job 只能配置 Cron 与启停。动态执行参数使用 Data Protection 整体加密；HTTP 目标拒绝私网/保留地址且不跟随重定向，命令只填写命令名称、不使用 Shell，并受 `ScheduleTaskExecution:AllowedCommands` 白名单约束，Service 方法必须实现并注册 `IScheduleTaskServiceMethodHandler`。旧版 `executablePath` 请求字段仍兼容，但只按命令名称解释，包含路径分隔符的值会被拒绝。任务配置保存到 `system_schedule_task_config`，Quartz 默认使用 PostgreSQL 持久化 JobStore，复用 `Default` 数据库连接。

对应前端入口为“系统管理 → 文件存储/定时任务”。系统种子会创建两个动态子路由及 `system:file-storage:manage`、`system:schedule-task:manage` 权限。

`ResourceAccessService` 为业务模块提供所有者、公开/私有、显式分享和关联文件授权能力；业务模块通过资源类型声明自身策略，System 不感知具体业务实体。分享令牌只返回一次且服务端只保存摘要，可设置有效期与设备数上限；撤销时轮换令牌版本，旧链接失效，已下载副本不在撤销范围内。

### framework

framework 保留通用应用、领域、安全与 SqlSugar 基础抽象；文件端口及适配器现由 System Contracts/Application 承载并在 Host 组合，业务模块只依赖 `IFileStorage`、`IResourceAccessService` 等稳定契约，不直接引用云厂商 SDK 或数据库文件实体。`IBusinessNumberGenerator` 位于 `Sim.Framework.Domain.Shared/Numbering`，由 `Sim.SqlSugar.Core` 的数据库序列实现提供多业务键、租户隔离和多实例安全的流水号分配；System 通过 `IBusinessNumberDefinitionService` 管理数据库规则，Framework 只依赖其稳定查询端口。

`/api/system/file-management` 提供管理型上传、GET 下载、删除、Scriban 模板渲染、CSV/JSON/HTML 导出、Office-to-PDF 和图片处理，需要系统文件管理权限。`/api/system/public-file/avatar` 仅匿名读取受控的 `avatars/users` 键。用户头像上传接口校验 2 MB 上限及 JPEG/PNG/WebP 魔数，并通过当前默认 provider 保存。LibreOffice 和 ImageMagick 默认关闭，部署时显式配置可执行文件后才启用。完整配置、provider 名称和扩展方式见 [`docs/core/ARCHITECTURE.md`](../docs/core/ARCHITECTURE.md)。

`[RateLimit]` 无参数时使用 `RateLimiting` 默认配额；构造参数可覆盖次数与窗口，`PartitionName` 可让相关端点共享业务分区。审批用户/组织目录已配置显式限流。分页请求 `MaxResultCount` 限制为 1–500、`SkipCount` 不得为负；常规控制器统一执行 ModelState 校验，非法分页或 JSON 返回 400 `Sim:Framework:InvalidInput`。分区键为端点分区名加客户端身份（已认证请求取 JWT 自定义 `Id` 声明，匿名请求取客户端 IP）。登录/注册共用 `authentication` 分区（10 次/60 秒），刷新与吊销使用独立 `token-refresh`/`token-revoke` 分区（各 30 次/60 秒）。当前实现为单实例内存计数，多实例共享限流列入待确认企业优化清单。

## API 与认证

### 本地化与错误响应

后端支持 `zh-Hans` 和 `en` 两种面向客户端的文化。`UseAbpRequestLocalization` 根据 `Accept-Language` 选择文化；业务异常使用 `Sim:<Module>:<Error>` 稳定错误码，并从 `src/Sim.Admin.Domain.Shared/Localization/Layered/{culture}.json` 生成 `error.message`。错误码的模块段必须与实现所属业务模块一致，例如 System 模块的文件缺失错误为 `Sim:System:FileNotFound`。客户端可以依赖 `error.code` 做降级展示，但不应解析消息文本。

后端同时拥有动态菜单、站点配置和登录页品牌标语的客户端文本。匿名接口 `GET /api/admin/client-localization` 根据 `Accept-Language` 返回受控的 `Menu:*` 字典、`Auth:Brand*` 品牌标语及 `Branding:Name`；白名单在 `src/Sim.Admin.Domain.Shared/Localization/ClientLocalizationKeys.cs` 与 `MenuLocalizationKeys.ClientKeys` 中维护。匿名接口 `GET /api/admin/site-status` 返回热加载的维护状态；Host 中间件读取热加载的 `App_Data/maintenance.json`，启用后返回业务 API/Hub 503，仅放行健康检查、该状态接口与匿名 `/hubs/site-status` 推送通道。Host 在 maintenance.json 热加载后通过该 Hub 向所有已连接 Web 客户端广播最新状态；客户端页面刷新后只读取一次状态接口作为兜底。它不会把全部内部错误资源暴露给客户端。系统、授权和审批路由种子只保存稳定 key，重复执行会把已有中文/英文标题迁移为 key。其余前端组件静态文案不放入该资源。

新增业务异常时，只能在对应 `Domain.Shared` 项目的 `<Module>ErrorCodes.cs` 中声明一次稳定代码，文件名、类名和 `Sim:<Module>:` 前缀必须与项目模块一致；例如 System 使用 `SystemErrorCodes` 与 `Sim:System:*`，Approval 使用 `ApprovalErrorCodes` 与 `Sim:Approval:*`。在 `zh-Hans.json` 与 `en.json` 同时添加相同 key，随后由调用方引用常量并抛出带 code 和必要 `WithData` 参数的 `BusinessException`，不要在业务代码中直接写 `"Sim:*"` 字面量或单一语言消息。`ArchitectureDependencyTests` 会阻止硬编码、重复声明和模块归属错位；联调还需验证两个 `Accept-Language` 值下的响应。

项目通过 Sim conventional controllers 暴露应用服务。Swagger 按模块分组，文档清单在
`src/Sim.Admin.HttpApi.Host/appsettings.json` 的 `Swagger:Documents` 中维护：

- `/swagger/all/swagger.json`（`Sim API（全部接口）`，`IncludeAll: true`）聚合全部业务模块，是 UI 的默认文档，也是导入 Apifox 等接口工具时唯一需要填写的地址。
- Admin API
- System API
- Approval API
- OpenPlatform API

Approval 分组除流程定义、实例与监控接口外，还提供 `GET /api/approval/business-type/users` 最小用户目录。该接口只返回当前租户启用账号的 ID、用户名与显示名，供审批人、转交和加签远程选人；目标写入前仍由审批引擎再次校验。

聚合文档会把 Host 基础设施端点（`/api/swagger-config`）排除在外，因此分组文档与聚合文档都只包含业务契约。新增模块分组时在 `Swagger:Documents` 中追加一项即可，聚合文档按路由前缀自动包含，无需逐个维护排除清单。

#### Swagger 访问控制

Swagger 文档面本身不实现登录页、会话 Cookie 或角色校验。直连后端时，非生产环境的 `/swagger` 前缀匿名开放；Production 默认不注册 Swagger，只有显式设置 `Swagger:AllowInProduction=true` 才会开启。Docker/nginx 部署中，`/swagger/` 与 `GET /api/swagger-config` 由 nginx Basic Auth 保护，`.htpasswd` 通过部署脚本生成且不入库。

这条边界只适用于文档面。业务接口 `/api/**` 仍然只接受 JWT Bearer，各接口自身的授权策略不受影响；Swagger UI 的 `Authorize` 按钮也只是把令牌透传给被调用的接口，而不是进入文档页的门槛。

JWT 认证在 `AdminHttpApiHostModule` 中配置。前端登录后携带：

```text
Authorization: Bearer <access_token>
```

不再接受省略 `Bearer` 前缀的 Authorization 值，也不再接受 URL 查询参数中的 token，避免令牌进入访问日志、浏览器历史和 Referer。

Access Token 过期后，前端会调用：

```text
POST /api/system/user/refresh-token
```

刷新和注销请求先调用 `GET /api/system/user/antiforgery-token`，再把返回值放入 `X-XSRF-TOKEN` 请求头。登录只接受 JSON，并由 CORS、`SameSite=Strict`、IP 限流和统一错误信息防护，因此 Swagger UI 可以直接调用登录接口；它不依赖尚未建立的防伪 Cookie。登录与双重验证完成请求可携带 `rememberMe`：`true` 签发带过期时间的持久 Refresh Cookie，`false` 签发随浏览器关闭失效的会话 Cookie，令牌值以 `p:`/`s:` 前缀记录该作用域并在轮换时继承；密码本身始终由浏览器密码库保存，服务端不接收已保存的密码。启用双重验证后，密码登录只返回短时 challenge 和可用方法，客户端通过 `send-two-factor-code`（邮箱/SMS）及 `complete-two-factor-login` 完成第二步。公开登录响应 DTO 不定义 Refresh Token 字段；Refresh Token 只通过路径限定为 `/api/system/user` 的 `HttpOnly`、`SameSite=Strict` Cookie 传输，不出现在 JSON 响应或前端持久化存储。Access Token 只保存在页面内存。登出调用 `POST /api/system/user/revoke-refresh-token`。

登录请求可携带非凭据性质的 `deviceId`、`deviceType` 和 `deviceName`；服务端只保存设备标识摘要，并将 Refresh Token 家族作为登录会话。Web 端的 `deviceName` 优先取 UA-CH 型号，其次取浏览器平台，再拼接平台架构与浏览器指纹前六位（如 `PMA110 · armv81 · 3f9a1c`），仅用于会话展示。默认 `Security:DeviceSessionLimits` 对 PC、Pad、Phone 各允许 1 个有效会话，值为 `0` 表示禁止该类型登录，范围为 0～20；同一设备重新登录会替换旧会话，新设备超额时撤销该类型最久未活动的会话。`GET /api/system/user/my-login-sessions` 返回当前用户有效会话，`POST /api/system/user/revoke-my-login-session/{sessionId}` 可撤销指定设备。JWT 包含会话 ID，每次鉴权都会确认其 Refresh Token 家族仍有效，并以最多每分钟一次的频率更新最近活动时间。

### 外部登录（Provider 抽象）

System 提供后端代理式外部登录，支持微信、QQ、Steam、Microsoft 与 Google。公共扩展点是 `IExternalLoginProvider` 和 Provider Registry；各 Provider 直接通过 `IHttpClientFactory` 接入，并使用各自平台的强类型响应对象解析结果。微信/QQ 使用 OAuth 2.0，Steam 使用 OpenID 2.0，Microsoft/Google 使用标准 OIDC，不把非 OIDC 协议伪装成统一 OIDC。OIDC 依赖 Microsoft.IdentityModel 8.22.0。接口为：

```text
GET /api/system/external-login/providers
GET /api/system/external-login/redirect/{providerId}
GET /api/system/external-login/callback/{providerId}
POST /api/system/external-login/register
GET /api/system/external-login/my-bindings
GET /api/system/external-login/my-binding-redirect/{providerId}
DELETE /api/system/external-login/my-binding/{providerId}
```

后端生成一次性 `state`；OIDC Provider 同时生成 `nonce`。state 会话、回调地址、过期时间和绑定意图保存在 Data Protection 加密的 HttpOnly Cookie 中，Cookie 作用域限定为 `/api/system/external-login`，回调时一次性消费并校验提供方协议返回值与外部身份。登录与绑定共用 `GET /api/system/external-login/callback/{providerId}`：登录意图下已绑定身份直接签发本系统 JWT，未绑定身份返回一次性注册令牌，由 `POST /api/system/external-login/register` 完成账号创建和绑定；绑定意图要求当前登录用户与 state 中用户一致，重复绑定同一外部身份时幂等成功，已被其他用户绑定则拒绝。提供方 access token 不落库，`system_external_login` 只保存 `Provider + ProviderKey` 的 SHA-256 摘要且唯一；系统不依据 email 自动关联已有账号。已绑定用户若开启本地 2FA，仍进入原有 challenge 流程。已登录用户可通过 my-bindings 端点查询绑定、发起统一回调绑定并解绑。

配置位于 `ExternalLogin`，每个 Provider 独立设置 `Enabled`，默认全部禁用。微信/QQ 需要 `ClientId` 与 `ClientSecret`；Steam 可选 `Realm`；Microsoft/Google 需要 HTTPS `Authority`、`ClientId`、`ClientSecret`，`Scopes` 必须包含 `openid`。Microsoft Authority 必须与应用注册的账号受众匹配：`common` 要求应用注册选择 All；仅个人账号使用 `consumers`，仅组织账号使用 `organizations`，需要限制组织时可改用具体租户 Authority；Google 使用其标准 OIDC discovery。本地开发将 `CallbackBaseUrl` 设为 `http://localhost:5173/auth/external/callback` 并临时设置 `StateCookieSecure=false`；生产必须使用 `https://<domain>/auth/external/callback` 并设置 `StateCookieSecure=true`。Provider 控制台的重定向 URI 必须与 `CallbackBaseUrl` 完全一致，不能带 query 或 fragment。未绑定的提供方身份统一先选择关联已有账号（`POST /api/system/external-login/link-with-external-login`，校验本系统账号密码后写绑定）或注册新账号（`POST /api/system/external-login/register-with-external-login`，受 `security.registration-enabled` 约束），不再使用 `CreateUnlinkedAccounts` 自动建号。外部登录 HTTP 客户端默认关闭 Information 日志，出站 HTTP Trace 不导出 query，避免 Provider secret/access token 进入日志或追踪。完整协议矩阵、Microsoft 登录/绑定流程与安全设计见 [`docs/ops/EXTERNAL_LOGIN.md`](../docs/ops/EXTERNAL_LOGIN.md)。

直连后端时，Swagger UI 直接打开 `/swagger/index.html` 即可，没有独立的登录页、会话 Cookie 或前端令牌接力；Docker/nginx 部署先通过 nginx Basic Auth，再打开文档页。UI 只从 `GET /api/swagger-config` 读取分组列表并渲染 Swagger UI 自带界面；需要调用受保护的接口时，在 `Authorize` 对话框里填入自行取得的 Access Token，UI 只把它透传给被调用的接口。

开发环境的 Vite HTTP 代理会把 `/api` 转发到本机 HTTPS API，因此防伪 Cookie 在 Development 明确不设置 `Secure`，以浏览器看到的外层 HTTP 请求为准；Staging/Production 始终强制 `Secure`。不要把这一开发配置复制到生产环境。生产部署在 OpenResty/nginx 等反向代理之后时，必须把代理加入 `ForwardedHeaders__KnownNetworks__0`（网段）或 `ForwardedHeaders__KnownProxies__0`（单 IP）并传递 `X-Forwarded-Proto: https`，否则 API 看到的是 HTTP，`Secure=Always` 会在生成防伪 Cookie 时抛异常。

安全行为：

- 密码使用 ASP.NET Core PasswordHasher，迭代次数默认 210000；旧 SM3 密码成功登录后自动升级。
- 内置国密算法：Framework 层 `Sim.Framework.Domain.Security` 的 `IGmCryptoService` 提供 SM2 签名/验签、SM2 加解密（C1C3C2）、SM4 ECB/CBC（PKCS7）加解密和 SM3 哈希；JWT 密钥描述符将 `Algorithm` 设为 `SM2WITHSM3` 即可切换为 SM2 国密非对称签名（签名格式为原始 r||s，与 `kid` 多密钥轮换机制兼容）。
- 连续登录失败达到 5 次后默认锁定 30 分钟。
- Refresh Token 数据库只保存 SHA-256 哈希，按 `Security:RefreshTokenRotationMinutes`（默认 5 分钟，0 表示每次都轮换）间隔轮换；间隔内的刷新只签发新 Access Token 且不更换 Cookie。检测到重复使用时撤销整个令牌家族。
- 设备随机标识不属于认证凭据；Web 可持久化该标识，但服务端只保存 SHA-256 摘要。设备类型上限的总量由三类配置之和约束。
- 部署该存储格式后，旧版明文 Refresh Token 不再可用，现有用户需要重新登录。
- 登录、刷新和注销服务禁用参数审计，避免密码和令牌写入审计日志；HTTP 请求摘要仍保留。
- `change-password` 会校验当前密码并撤销全部会话；`request-password-reset` 对未知账号返回相同结构，`reset-password` 只接受未过期、未超尝试次数的一次性挑战。
- 邮箱/SMS 验证码只保存加盐 PBKDF2 哈希，TOTP 密钥使用持久化 Data Protection 密钥环加密；TOTP 采用 30 秒 RFC 6238 SHA-1/6 位兼容参数，并提供 10 个一次性恢复码。
- 用户昵称必填且全局唯一：创建、更新、自助资料和外部注册都会去除首尾空白，空白值返回 400（`Sim:System:NickNameRequired`）；重复返回 400（`Sim:System:NickNameAlreadyExists`），数据库以唯一索引兜底，存量重复昵称在迁移中追加 `-2`、`-3` 等后缀。
- `Security:EmailVerification` 是通用 SMTP 配置；`Security:SmsVerification` 是通用 HTTPS Webhook 适配器。验证码、目标地址、TOTP 密钥和恢复码不得写入日志。
- 登录、注册、刷新接口显式限制为每分钟 10 次。分区优先使用登录用户 ID，匿名请求使用客户端 IP，并在拒绝响应中返回 `Retry-After`。
- SQL 调试日志只记录参数名和类型，不记录参数值。

权限声明 `PermissionAttribute` 位于 `Sim.Framework.Application.Contracts`，通过 ASP.NET Core Authorization requirement 执行；权限计算 handler 位于 `Sim.System.Application`。因此审批等模块只依赖稳定的公共契约，不再为了使用权限特性反向引用 System Domain。Admin 角色或 `*:*:*` 权限拥有全部访问权；其他角色必须分配接口要求的权限码。用户、角色、权限、模块和审批定义写接口均已启用权限检查。全部可分配权限登记在 `Sim.Admin.Domain/Data/PermissionCatalog.cs`，权限码统一为 `module:resource:action`（如 `system:user:query`、`approval:definition:manage`），服务上的 `[Permission]` 只能引用清单中的权限码；`PermissionCatalogTests` 会扫描程序集校验两者一致。用户创建/更新 DTO 的 `roleIds` 会覆盖式同步 `system_user_role`（`null` 表示保持现有角色），角色编辑通过 `POST /api/system/role/assign-role-permissions/{roleId}` 覆盖式同步权限；两者都额外要求 `system:role:manage`，用户接口内的运行时校验失败返回 403（`Sim:System:RoleManageRequired`），避免仅持有 `system:user:update` 的操作者提权。权限目录的读取（列表、详情、按角色查询）允许 `system:permission:manage`、`system:role:manage` 或路由管理权限（`system:route:create/update/delete`）任一，保证角色管理员和路由管理员能读取清单；权限写接口仍只接受 `system:permission:manage`。权限与模块没有数据关联，权限码前缀只用于展示分组。租户管理接口统一要求 `system:tenant:manage`，匿名请求返回 401、无该权限返回 403。路由路径支持相对或绝对路径，并允许 `:param`、`:param?` 等动态段；路由访问权限保存在 `RouteMeta.PermissionCode`（逗号分隔；`RouteMeta.PermissionRequireAll` 决定任一或全部满足，默认任一，空值表示不限制）：`GET /api/system/route-config/module-routes/{moduleId}` 与 `/api/system/route-config/user-accessible-routes` 会按当前 JWT 权限裁剪路由树，未授权子路由不会下发；路由管理查询接口仍返回全量供管理员维护。种子按 `RoutePermissionCatalog` 的路由名映射为新建路由写入页面读取所需的默认权限码（根路由取全部子页面权限并集，含个人页的根路由保持不限制），并只为空权限码回填，不覆盖管理员在路由管理页维护的结果；路由编辑页从权限清单按模块分组下拉多选（选项展示权限名称与权限码，选中标签只显示名称，支持按名称或权限码搜索），并可为每个路由切换“任一满足 / 全部满足”，保存路由时权限码必须存在于权限清单，否则返回 400（`Sim:System:RoutePermissionCodeNotFound`）。模块与路由为多对多关联（`system_module_route`），关联表是唯一事实来源：模块 DTO 的 `routeIds` 表示该模块下发的路由集合，创建/更新模块时会覆盖式同步并自动补齐所选路由的祖先；`module-routes` 与 `user-accessible-routes` 按关联表读取并去重共享路由。路由是全局目录，不再持有归属模块，路由名全局唯一；路由管理查询接口返回全量路由（含未分配路由）并支持按关联模块筛选，模块编辑页的路由树同样来自全量目录。新建/删除路由不再隐式改动模块关联；删除模块只解除关联，路由保留。租户初始化会一并复制所选模块与路由的关联。角色权限或用户角色发生变化时，服务端会吊销受影响用户的 Refresh Token，残留 Access Token 在会话校验缓存（30 秒）后失效。内置身份以 `RoleEntity.IsBuiltIn` / `UserEntity.IsBuiltIn` 标记识别，不再依赖角色编码或用户名。内置 `Admin` 角色允许修改名称与描述，但编码不可变，分配权限、分配模块与删除均返回 400（`Sim:System:BuiltInRoleProtected`）；内置系统账号 `system` 与初始化创建的管理员账号均为内置账号：不允许修改账号名、变更角色、重置密码或删除（`Sim:System:BuiltInUserProtected`），昵称、邮箱等资料仍可编辑；且不能移除最后一个启用管理员的 Admin 角色；删除角色会级联清理角色权限、角色模块、用户角色与数据范围关联。管理员账号用户名只能在初始化时自定义，JWT 使用 `IsBuiltInRole` claim 承载内置角色旁路。

## 常用命令

```bash
dotnet tool restore
dotnet restore Sim.slnx --locked-mode
dotnet build Sim.slnx
dotnet test Sim.slnx --no-build --no-restore
dotnet run --project src/Sim.Admin.HttpApi.Host
dotnet publish src/Sim.Admin.HttpApi.Host -c Release -o ./publish
```

`tests/Sim.Security.Tests` 除权限、密码、JWT、Refresh Token、DTO、架构、许可证和 OpenAPI 基线外，还会通过 TestHost 验证 SQLite readiness、Cookie/CSRF、迁移、存储、调度、种子、动态路由、中英文业务错误、客户端菜单/站点本地化接口、消息 Outbox/Inbox、通知配置/投递/站内信、鉴权 SignalR Hub 及官方 Provider Secret 隔离。

NuGet 包版本（含显式固定的传递依赖）统一维护在根目录 `Directory.Packages.props`。项目文件中的 `PackageReference` 只声明需要的包，不得设置 `Version` 或 `VersionOverride`；应用 Host 与测试入口分别提交 Windows 的 `packages.lock.json` 和 Linux 的 `packages.linux.lock.json`，避免条件数据库驱动在容器还原时漂移。已启用 `CentralPackageTransitivePinningEnabled`，凡是列入中央版本的传递依赖也会锁定到同一中央版本。新增或升级包时只修改中央文件、更新两组锁文件和 `eng/dependency-license-policy.json`，并运行 Release 编译、测试和直接/传递依赖漏洞扫描。

`common.props` 不再为所有项目隐式引入 `Microsoft.AspNetCore.App`。只有确实使用 ASP.NET Core API 的 8 个项目显式引用 Shared Framework，System Domain 的密码哈希只引用官方 `Microsoft.Extensions.Identity.Core`；架构测试会阻止共享框架范围再次无意扩大。

Swagger 分组的契约以 `definition/` 为单一来源，由 CI 用 `swagger tofile` 重生成并检查未提交漂移；新增分组应在发布前通过同一 CLI 流程导出契约并纳入 Web 类型生成。该模式只跳过数据库迁移、数据种子、静态资源映射以及 System 启动初始化/Quartz 调度，不改变接口注册。破坏性契约变更（删除路径/方法/参数/响应、收紧必填、修改类型或枚举值）必须先完成版本与迁移评审，再显式提交新契约，不能绕过门禁。

本次资源访问表以及 Refresh Token 会话监控列均属于数据库结构变更。Production/Staging 发布前必须备份业务数据库，使用同一发布产物执行 `--migrate`，再启动新版本；回滚代码前恢复兼容备份。部署后，不含会话 ID 的旧 Access Token 会被拒绝，客户端可使用现有 Refresh Cookie 刷新一次或重新登录；旧 Refresh Token 在轮换后会补齐设备会话元数据。

对象映射通过 `Volo.Abp.Mapperly` 完成。新增 DTO/实体映射时，在对应 Application 项目的 `Mappings` 目录添加 Mapperly mapper，不要新增 AutoMapper Profile。

## 文本与换行约定

仓库通过根目录与 `api` 的 `.editorconfig`、`.gitattributes` 统一换行：C#、项目文件、JSON 和 Markdown 等普通文本使用 CRLF，Windows/Visual Studio 与 Linux 编辑器都能稳定打开；`*.sh`、`Dockerfile` 和 `Jenkinsfile` 固定使用 LF，避免 Linux 执行异常。编辑器或 AI 助手不得只转换文件中的部分行；修改后可用 `git ls-files --eol` 确认没有 `w/mixed`。

## 新增后端模块

1. 在 `modules/` 下创建模块目录。
2. 按现有模块拆分 `Domain.Shared`、`Domain`、`Application.Contracts`、`Application`、`SqlSugar`。
3. 将业务工程追加到 `Sim.slnx` 末尾的 `/business/` 文件夹；跨业务模块只引用 `Application.Contracts`、`Domain.Shared` 等稳定契约，不引用其他模块的 Application/Domain 实现，framework 不得引用业务模块。
4. 业务工程引用写在 `src/Sim.Admin.HttpApi.Host/Business/BusinessProjects.props`，并在 `Business/BusinessModuleRegistration.cs` 的 partial `AdminHttpApiHostModule` 上声明 Application/SqlSugar 模块的 `[DependsOn]`，确保编译器保留 `AssemblyRef`、约定式控制器能自动发现模块；不需要修改 Host csproj 和 `AdminHttpApiHostModule` 主体。
5. 如需生成 Swagger 分组，在 `appsettings.json` 的 `Swagger:Documents` 中补充配置；应用服务路由由约定自动暴露，不需要再配置 `Modules:Modules`。
6. 业务新增 NuGet 包只在 `Directory.Packages.business.props` 声明版本，不修改 `Directory.Packages.props`。
7. 运行 `tests/Sim.Security.Tests` 中的架构依赖测试，并更新本文档和 `docs/core/ARCHITECTURE.md`。

业务系统从本仓创建独立仓库后的目录分区与同步流程见 [`docs/core/FRAMEWORK_SYNC.md`](../docs/core/FRAMEWORK_SYNC.md)。

## 通知、消息与缓存

- `Sim.Framework.Domain.Shared/Messaging`、`Caching` 保存稳定抽象；`Sim.Framework.Application` 保存 Outbox 调度、消息 Transport 与缓存实现；`Sim.SqlSugar.Core/Messaging` 保存 Outbox/Inbox 表和 Store。
- `Framework:Messaging:Provider` 支持 `Local`、`RabbitMQ`、`Kafka`、`RocketMQ`，分别由 `RabbitMQ.Client`、`Confluent.Kafka` 和 Apache `RocketMQ.Client` 实现。Kafka 使用 Bootstrap Servers、消费组、手动提交和 DLQ；RocketMQ 使用 5.x gRPC Endpoint、SimpleConsumer 和显式 ACK。RabbitMQ 的 `ConnectionString` 必须是合法 `amqp(s)` URI，用户名或密码中的保留字符需百分号编码：`@`、`:`、`/` 分别写作 `%40`、`%3A`、`%2F`（例如密码 `Dev@123` 应写成 `Dev%40123`），未编码时连接的 URI 校验会失败并停止 Host。
- `Framework:Caching:Provider` 支持 `Local`、`Redis`、`Hybrid`；Redis 由共享的 `StackExchange.Redis` ConnectionMultiplexer 实现，Hybrid 是短时 L1 Local + L2 Redis。
- “系统管理 → 业务编号”通过 `/api/system/business-number-definition` 维护 `system_business_number_definition` 规则：业务键创建后不可改，格式、名称、说明、初始流水和启停可更新。格式必须恰好包含一次 `{sequence}`，可使用 `{sequence:D6}` 或 `{sequence:000000}` 补零；`{date:yyyyMMdd}` 使用任意 .NET 日期格式，日期渲染结果自动作为流水分段（`yyyyMMdd` 按日、`yyyyMM` 按月、`yyyy` 按年；没有日期时连续编号），`{business}` 会渲染规范化业务键。业务模块注入 `IBusinessNumberGenerator` 并调用 `GenerateAsync(new NumberGenerationRequest("purchase-order"))`；不存在或停用规则时不会分配编号。内置场景为 `approval.instance`（审批实例，种子维护）。规则与状态表 `framework_number_sequence` 由 Code First/`--migrate` 创建，Production/Staging 升级前须备份数据库。
- “系统管理 → 系统配置”通过 `/api/system/configuration-item` 维护 `system_configuration_item` 参数：分组键/配置键创建后不可改，同一租户内唯一；分组名、配置名、值、说明、排序和启停可更新。`SystemConfigurationDataSeeder` 幂等提供 host 租户默认参数且不覆盖已有值。
- “系统管理 → 数据字典”通过 `/api/system/data-dictionary` 与 `/api/system/data-dictionary-item` 维护 `data_dictionary` / `data_dictionary_item`：字典键和字典项键在同一租户/字典内唯一；字典统一选择 `String`、`Number` 或 `Date` 值类型且创建后不可变，后端据此校验并规范化值，数字使用 Invariant `G29` 格式去除无意义尾随零；字符串/数字可配置输入框展示用前缀/后缀（例如 ¥、% 或单位），这些符号不参与校验且不会写入存储值；管理端先展示字典大类列表，再通过独立字典数据 Tab 维护字典项。生产升级前仍须备份并执行受控迁移。
- `modules/system` 的 Notifications 目录拥有通知配置、模板、事件规则、多 Site Webhook、投递状态机、站内/浏览器通知与实际渠道 Provider。通知配置记录包含渠道、Provider（供应商实现）、优先级、启停与凭据。数据库 Secret 与重试所需的完整投递目标由 Data Protection 加密；API 对 Secret 只返回 `HasSecret`，对投递目标只返回掩码。更新时空 Secret 不覆盖已有凭据，非空字段按键合并；显式 `ClearSecrets` 才清空全部凭据。机器人 Webhook Endpoint 也按 Secret 保存，但仍执行 HTTPS 与目标地址校验。
- `INotificationAccountDirectory` 是 System Contracts 中的账号解析端口，System Application 提供实现，System 不引用 RBAC。普通事件的 `NotificationRecipient.UserId` 可解析为邮件、短信、语音、站内或浏览器目标；显式 `Address` 仍优先。管理接口 `/accounts` 只返回启用账号及脱敏联系方式，测试接口同时接受 `userId` 或直接 `destination`。
- 当前用户实时入口为鉴权 Hub `/hubs/user-notifications`。站内/浏览器 Provider 先落库，再通过 `IUserNotificationRealtimePublisher` 的 Host SignalR 适配器发送无正文的 `notificationChanged` 失效事件；单条/全部已读也发布同一事件以同步其他页签。实时发布失败只记录脱敏日志，不回滚通知或已读状态。SignalR Redis backplane 由 `Framework:SignalR:UseRedisBackplane` 控制，Development 默认关闭，其他环境默认开启。
- SMTP 使用 `MailKit`；腾讯云/阿里云短信和语音分别使用厂商官方 SDK。标准 HTTP 短信/语音及机器人 Provider 继续作为合同网关适配器，必须配置 HTTPS Endpoint。
- `INotificationEventPublisher` 用于普通异步事件；`ISecurityNotificationSender` 用于验证码即时发送。System 的旧发送器仅作为迁移桥接，不再读取 SMTP/SMS 配置。
- 通知投递后台服务由 `System:Notifications:DeliveryEnabled` 控制，默认启用；仅在不需要异步投递的专用测试宿主中关闭，生产环境不得以此替代正常的渠道与重试配置。
- 新表和 `system_resource_share_link.TokenVersion` 由 Code First/显式 `--migrate` 创建。Production/Staging 升级前必须备份数据库；旧分享记录的版本默认为 0 并失效，私有资源分享链接需重新签发。升级时还应确保 Data Protection Keys、MQ/Redis 连接和通知渠道出网策略已经配置。

## 文档维护

修改后端模块、接口、认证、数据库、Swagger、数据种子、启动方式或部署方式时，请同步更新：

- 本 README
- 项目文档总索引 [`docs/README.md`](../docs/README.md)
- `docs/core/ARCHITECTURE.md`
- `docs/core/DEVELOPMENT.md`
- `docs/core/DOCUMENTATION.md`
- 项目级 [`docs/core/ENTERPRISE_SYSTEM_GUIDE.md`](../docs/core/ENTERPRISE_SYSTEM_GUIDE.md)，检查企业级能力、当前风险和完成定义是否变化

E2EE 通行密钥服务为 `/api/system/e2ee-passkey`。登记名称追加四位展示短编号（取新记录 ID，当前用户/租户已有同名时重新生成）；`PUT /api/system/e2ee-passkey/{id}/name` 支持重命名完整标签，body 为 `{ "name": "办公电脑 · A7F2" }`，要求 1–100 字符且非全空白。更新仅限当前用户/租户/UMK 版本的凭证，仅写名称与修改审计，不修改公钥、计数器、盐或 UMK 密文。无需数据库迁移，旧名称保持不变。登记/解锁流程：登记/解锁使用 Fido2 4.3.1 验证 WebAuthn；需登录，按用户/租户/UMK 版本隔离，挑战 5 分钟有效且原子单次消费。新增 system_user_e2ee_passkey 与 system_user_e2ee_passkey_challenge 表，由 CodeFirst 建表。生产默认关闭，设置 E2eePasskeys:Enabled/RpId/Origins 后启用；不得从请求 Host 推导可信来源。服务端不接收 PRF 输出、派生密钥或 UMK，具体接口、配置与恢复边界见 [`docs/core/ARCHITECTURE.md`](../docs/core/ARCHITECTURE.md) 的“系统 E2EE 约束”。新增依赖及两组锁文件/许可证策略已同步。

许可证策略的 packages 仅列出中央版本声明；Fido2 的传递依赖另经 nuspec 核验：Fido2.Models、NSec.Cryptography、System.Formats.Cbor 为 MIT，libsodium 为 ISC，不将其重复列入直接依赖基线。

Linux 生产镜像包含 libsqlite3-0：数据库模块初始化 SQLitePCL 时需要系统 SQLite 库，即使主连接使用其他数据库也必须存在；curl 和 Kerberos 运行依赖保持。

文本附件：白名单内的 `.md`、`.txt`、`.csv`、`.json`、`.xml`、`.css`、`.ts`、`.py`、`.java`、`.sql`、`.cs` 使用严格 UTF-8 全文校验（支持 UTF-8 BOM），不要求固定魔数，MIME 以扩展名为准；兼容 text/*、application/octet-stream、空 MIME 及 JSON/XML 扩展类型和 CSV 的 Excel MIME。拒绝已知二进制签名、非法 UTF-8 和非换行/制表符控制字符，扫描全文并保持流位置。

上传校验失败统一返回本地化业务错误 HTTP 400，不作为系统 500：缺少扩展名、类型不在白名单或危险类型、格式无法识别、内容与扩展名/声明 MIME 不一致、超过上传大小上限分别有独立错误码；大小超限由 `FileSizeLimitExceededException` 在写入存储前转换为 `FileSizeExceeded` 业务错误并携带最大 MB 数。

# SimApp API 架构说明

本文档说明 SimApp API 的后端架构、模块边界和关键运行机制。

## 总览

SimApp API 是通用系统平台基座的后端服务，基于 .NET 10、Sim 框架和 SqlSugar 构建。项目采用分层模块化结构：

- `src/`：主应用和 Host 入口。
- `framework/`：跨模块基础能力，如通用 CRUD、特性限流、文件存储/处理端口、Swagger 配置、SqlSugar 仓储和工作单元。
- `modules/`：业务模块，当前包含 `approval`、`system`、`openplatform`。

服务入口是 `src/Sim.Admin.HttpApi.Host`。Host 负责读取配置、注册认证、注册 Swagger、加载模块、启用中间件并启动 Web API。

主应用不保留空的 `Sim.Admin.HttpApi` 和 `Sim.Admin.HttpApi.Client` 项目。Host 从 Application 模块的程序集引用链发现应用服务，并通过约定式控制器生成 HTTP 路由；自定义控制器直接位于 Host。Web 和 WPF 客户端通过 HTTP 调用 API。

Windows WPF 项目位于仓库根目录的 `client/Sim.Admin.Wpf.Client`，与 `api/` 同级，独立运行与构建；不参与 `Sim.slnx` 的跨平台后端构建。其连接、认证、账号安全、审批与系统管理页面及主题说明见 [客户端 README](../../client/Sim.Admin.Wpf.Client/README.md)。

## 分层约定

每个业务模块通常按以下项目拆分：

- `*.Domain.Shared`：常量、枚举、错误码、共享资源。
- `*.Domain`：实体、仓储接口、领域服务。
- `*.Application.Contracts`：应用服务接口、DTO、请求模型。
- `*.Application`：应用服务实现、Mapperly 编译期映射、后台任务。
- `*.SqlSugar`：SqlSugar 仓储实现和数据访问注册。

跨模块依赖只应指向稳定契约：Application 可引用其他模块的 `Application.Contracts` 或 `Domain.Shared`，不引用其他模块的 Application/Domain 实现；Domain 不引用其他业务模块；framework 不反向引用业务模块；模块基础设施也不引用 Host。`tests/Sim.Security.Tests/ArchitectureDependencyTests.cs` 会持续检查这些边界。

跨模块权限声明位于 `Sim.Framework.Application.Contracts/Authorization/PermissionAttribute.cs`，System 的权限计算 handler 位于 `Sim.System.Application/Authorization`。其他业务模块只引用公共权限契约，不需要依赖 System Domain。System Application 直接引用 Quartz 官方依赖注入包。

NuGet 使用官方 Central Package Management：52 个 NuGet 包版本（含 2 个传递依赖固定）集中定义在根目录 `Directory.Packages.props`，各项目只声明包用途。架构测试禁止项目内 `Version`/`VersionOverride`、遗漏中央版本或重复中央定义；Host 与测试入口按 Windows/Linux 分别提交锁文件并执行锁定还原。`eng/dependency-license-policy.json` 是直接依赖许可证审批基线，CI 同时执行传递漏洞审计并为发布目录生成、校验 SPDX SBOM。

ASP.NET Core Shared Framework 不再由 `common.props` 全局传播，只有使用 HTTP、Authorization 或相关 API 的项目显式引用。System 的 `CurrentUser` 位于 Application 的 HTTP 安全适配层，Contracts 保持纯契约；Domain 的密码哈希能力使用更小的官方 Identity Core 包。架构测试维护显式引用白名单。

## 核心模块

### admin

主应用模块，负责应用聚合、基础数据种子、主 Host 依赖串联和默认配置。

关键位置：

- `src/Sim.Admin.HttpApi.Host`
- `src/Sim.Admin.Domain/Data`
- `src/Sim.Admin.Domain/DataSeederContributor.cs`

### approval

独立审批模块，提供审批定义与版本、运行时实例与任务、审批人解析、抄送、实例监控和附件代理。

### system

System 模块合并了原 RBAC、租户与后台监控能力，提供用户、角色、权限、菜单、模块配置、路由配置、Refresh Token、组织架构、文件存储、定时任务、通知、租户管理与后台任务监控。

关键服务：

- `UserService`
- `RoleService`
- `PermissionService`
- `ModuleConfigAppService`
- `RouteConfigAppService`
- `RoleModuleService`
- `SystemConfigService`
- `TenantAppService`
- `MonitorService`

### framework 文件与限流

`Sim.Framework.FileManagement` 定义 `IFileStorage`、运行时配置 provider、模板、导出、Office-to-PDF、头像和图片处理端口，并提供本地实现；`.SqlSugar` 与 `.ObjectStorage` 分别适配数据库、Amazon S3、腾讯 COS 和阿里 OSS。Host 组合具体实现，业务模块不引用云 SDK 或数据库文件实体。System 模块把默认 provider、上传上限和各提供器配置保存到数据库，使用 Data Protection 加密对象存储 Secret，管理读取接口不回显。远程文件工作台受 `framework:file:manage` 保护，公开头像服务只允许 `avatars/users` 键。

System 模块还统一管理 Quartz 定时任务，并通过 `IScheduleTaskDefinitionProvider` 声明不可删除的内置 Job 和默认 Cron；System 后台可创建 HTTP/HTTPS、带参数命令/程序、显式白名单 Service 方法三类动态 Job，并通过同一运行时同步 Quartz Job 与 Trigger。`system_schedule_task_config` 保存类型、Cron、启停和经 Data Protection 整体加密的执行参数。HTTP 执行器拒绝私网/保留地址且不跟随重定向，命令执行器不使用 Shell 并受部署目录白名单约束，Service 执行器只解析注册的 `IScheduleTaskServiceMethodHandler`。当前 Quartz JobStore 仍为内存实现。

`[RateLimit]` 作为 endpoint metadata 读取 `RateLimiting` 默认值，特性参数可覆盖次数、窗口和分区名。分区键为端点分区名加客户端身份：已认证请求取 JWT 自定义 `Id` 声明，匿名请求取客户端 IP。登录/注册共用 `authentication` 分区（10 次/60 秒），刷新与吊销使用独立 `token-refresh`/`token-revoke` 分区（各 30 次/60 秒），高频刷新不会挤占登录和注销配额。当前 ASP.NET Core limiter 是单实例内存状态，多实例共享方案尚待确认。

对象存储下载事件 Webhook `POST /api/system/object-storage-event/receive-s3-event` 必须携带 `X-Sim-Signature: sha256=<hex>`（对原始 JSON 文本计算 HMAC-SHA256），签名密钥由 `FileManagement:ObjectStorageWebhookSecret` 提供；未配置密钥时端点失败关闭，匿名无签名写入返回 401。路由与模块管理查询需要路由/模块写权限或角色模块管理权限；普通登录用户通过 `GET /api/system/route-config/user-accessible-routes` 读取自己的可访问模块与全局路由树，后端先按角色可访问模块取路由，再沿全局 `ParentId` 补齐祖先并按 JWT 权限裁剪，跨模块改挂的父路由不会丢失层级。

## 请求链路

典型接口调用链路：

1. 前端请求 `/api/{module}/{service}/{action}`。
2. Sim conventional controller（`AppServiceConvention`）将请求映射到应用服务。
3. 应用服务处理 DTO、权限、分页、事务和业务编排。
4. 领域服务或仓储完成实体规则和数据访问。
5. SqlSugar 仓储访问数据库并返回结果。

业务多写操作通过 `SqlSugarUnitOfWorkAttribute` 和异步 Action Filter 统一提交或回滚；数据种子使用幂等写入，不依赖额外的工作单元包装。工作单元在提交/回滚失败及请求结束时兜底关闭 SqlSugar 连接；`ISqlSugarClient` 使用按请求作用域的普通 `SqlSugarClient`，避免 `SqlSugarScope` 在 AsyncLocal/ThreadLocal 间切换导致事务连接不随请求释放。

## 可观测性

`AdminHttpApiHostModule.ConfigureOpenTelemetry` 注册链路与指标：ASP.NET Core 入站请求、HttpClient 出站调用（配置 Redis 连接串时追加 Redis instrumentation），以及 ASP.NET Core/HttpClient/Runtime 指标；配置 `OpenTelemetry:Otlp:Endpoint` 后按 OTLP 导出，`Metrics:Enabled=true` 时同时开放 `/metrics` Prometheus 抓取端点。

业务指标集中定义在 `framework/Sim.Framework.Domain.Shared/Telemetry/Metrics.cs`（Meter 名 `SimFramework`），由 Host 通过 `AddMeter` 订阅：

- 数据库：`Sim.SqlSugar.Core/Diagnostics/DbCommandProbe.cs` 桥接 SqlSugar 的 `Aop.OnLogExecuting`/`OnLogExecuted`/`OnGetDataReadered`/`OnError`，产出 `sim.db.command.count` 与 `sim.db.command.duration`，标签为归一化的 `db.command`（select/insert/update/delete/ddl/other）和 `status`；采集与 `DataBase:EnableSqlLog` 解耦。
- 定时任务：`ScheduleTaskExecutionHistoryListener` 在 `JobWasExecuted` 中产出 `sim.schedule_task.count` 与 `sim.schedule_task.duration`，标签为 `task.group`/`task.key`/`trigger.type`/`status`；该记录与执行历史写库解耦，历史缺失时仍上报。

标签只取有界值（命令类型、任务键、状态），不写入 SQL 文本、任务参数等自由文本，避免指标基数失控。

## 认证与令牌

认证配置在 `src/Sim.Admin.HttpApi.Host/AdminHttpApiHostModule.cs`。

主要机制：

- 使用 JWT Bearer 认证。
- JWT 配置读取 `JwtSettings`。
- 密码策略、锁定策略、会话、Refresh Token、验证码、SMTP/SMS 发送和 TOTP 策略读取 `Security`。
- `IUserService`/`UserService` 是浏览器会话边界，并由 Sim conventional controller 生成接口，无需手写 Controller。刷新和注销验证 `X-XSRF-TOKEN`；登录接受 JSON，并由 CORS、SameSite Cookie 和限流防护，以兼容 Swagger UI 的首次登录。
- 登录后前端仅在页面内存保存 Access Token，并携带 `Authorization: Bearer <access_token>`。
- Refresh Token 仅存放在后端签发的 `HttpOnly`、`SameSite=Strict` Cookie，JSON 响应不返回令牌值。登录与双重验证完成请求通过 `RememberMe` 选择 Cookie 作用域：`true` 签发带过期时间的持久 Cookie，`false` 签发随浏览器关闭失效的会话 Cookie；令牌值以 `p:`/`s:` 前缀记录作用域，轮换时继承，历史无前缀令牌按持久会话处理。
- Access Token 过期后调用 `/api/system/user/refresh-token` 换取新 Access Token，并按 `Security:RefreshTokenRotationMinutes`（默认 5 分钟，0 表示每次都轮换）间隔轮换 Refresh Cookie；间隔内不更换 Cookie，避免页面导航中止请求后旧 Cookie 触发复用检测而误吊销会话族。登出调用撤销接口并删除 Cookie。
- 登录、刷新、注销、改密、找回和双重验证关闭敏感参数审计，防止密码、令牌、验证码或 TOTP 密钥进入审计日志。
- 内置角色标记只对宿主身份生效：租户管理员角色不使用 `IsBuiltIn`，JWT 的 `IsBuiltInRole` 声明仅由宿主用户携带；权限处理器和数据范围解析同时要求令牌无 `tenant_id` 声明，避免租户管理员获得全局绕过。
- 租户上下文在认证完成后从 JWT `tenant_id` 同步；仓储的按主键读取、更新、删除和批量操作都会附加租户条件。登录、刷新和每次令牌校验都会检查租户存在且启用，租户停用或删除后既有会话立即失效。
- 会话校验成功结果缓存 30 秒；注销、会话撤销、角色/权限变更和租户删除会主动清除本实例缓存。多实例部署仍需共享缓存或吊销广播才能消除跨实例残留窗口。
- `JwtSettings:ClockSkew` 默认 1 分钟。client_credentials 令牌没有用户会话，只能访问带显式 `[Permission]` 的端点，不能访问仅要求登录的用户接口。

生产环境必须从外部密钥源提供或替换敏感配置：

- `JwtSettings:IssuerSigningKey`
- `StringEncryption:DefaultPassPhrase`
- `DataProtection:KeysPath`

多实例必须共享同一个 Data Protection 应用名和密钥环，并用平台 KMS、加密卷或等效机制保护静态密钥。

## 数据库与数据种子

数据库配置位于环境配置文件，例如：

- `src/Sim.Admin.HttpApi.Host/appsettings.Example.json`（模板，基于 development，默认连接本机 Compose 的 PostgreSQL，其余敏感字段留空）
- `src/Sim.Admin.HttpApi.Host/appsettings.Development.json`、`appsettings.Staging.json`、`appsettings.Production.json`（本地/部署环境文件，均不入库；启动时缺失会基于模板自动生成）

开发环境数据库配置以 `appsettings.Example.json` 为准；模板默认连接本机 `api/env/compose.yaml` 提供的 PostgreSQL，也可在本地环境文件中覆盖 `DbType` 与连接串切换 MySQL/SQL Server。

数据种子集中在 `src/Sim.Admin.Domain/Data`，包括用户、角色、权限、模块、路由等基础数据。系统未完成初始化时启动自动种子与 `--seed` 都会跳过；`/setup` 提交后先创建管理员，再创建不可登录的系统账号 `system`，并由系统账号作为创建人执行种子。初始化完成后可显式补齐种子：

```bash
dotnet run --project src/Sim.Admin.HttpApi.Host -- --seed
```

初始化后的 `--seed` 同样确保系统账号存在，并以系统账号作为创建人。

`SystemDataSeeder` 写入相对 Web `src/modules` 的规范组件路径，种子重复执行不覆盖管理端维护结果；前端为早期 `/modules/user/views/*.vue` 路径保留显式兼容别名，因此应用升级与数据库种子可分步执行。

管理员账号由 Web `/setup` 在首次初始化时创建；`App:AdminDefaultPassword` 只作为租户管理员初始密码的兜底配置，不再预置默认 `admin`。Development 不再保留固定密码或 JWT 密钥，并默认关闭自动数据种子；系统未完成初始化时启动自动种子与 `--seed` 都会跳过，初始化提交后创建不可登录的系统账号并以它作为种子创建人。治理状态见项目级 [`docs/core/ENTERPRISE_SYSTEM_GUIDE.md`](../../docs/core/ENTERPRISE_SYSTEM_GUIDE.md)。

`IDatabaseSchemaMigrator` 统一发现业务实体并执行结构同步。Production/Staging 默认关闭 Code First 和数据种子；模板中的默认连接仅供本地，部署必须通过密钥管理系统或环境变量同时覆盖 `DbType` 与连接串，备份数据库后运行 `dotnet run --project src/Sim.Admin.HttpApi.Host -- --migrate`，成功后再启动服务。

内置标记列 `is_built_in` 带数据库默认值，旧库升级时 CodeFirst 可安全添加非空列；`HostBuiltInIdentityBackfillMigration` 会补齐宿主 Admin 角色与 `system` 账号的内置标记，`HostAdministratorBuiltInMigration` 再把宿主侧持有内置 Admin 角色的管理员账号（含初始化创建的管理员）标记为内置，二者均可重复执行。删除租户时 `TenantProvisioningService.DeleteTenantDataAsync` 级联清理租户用户、角色、授权关系、模块/路由/权限副本与刷新令牌，调用方在同一工作单元内删除租户实体；业务数据（审批等）仍按各自模块的数据保留策略处理。

Host 暴露三个健康端点：`/health/live` 只检查进程存活，`/health/ready` 执行真实数据库连接检查，`/health-status` 保留为执行全部检查的兼容入口。检查失败只返回稳定描述，不向调用方泄露数据库异常。

Host 将创建与初始化逻辑收敛到 `Program.CreateApplicationAsync`，生产入口和官方 TestHost 集成测试复用同一组合根。测试使用隔离的临时 SQLite 和 Data Protection 目录，覆盖 readiness、CSRF Cookie/Header 以及实际迁移建表。

日志由统一 Serilog 管道处理：Host、Microsoft/ASP.NET Core、ABP、SqlSugar、数据种子、Quartz Job 和业务 `ILogger<T>` 的细粒度 `SourceContext` 统一归并为 `ScheduleTask`、`System`、`RBAC`、`Framework` 或 `Admin`。控制台和滚动文件共享 `[模块][级别][时间] 内容` 单行格式，事件内部换行转义为 `\n`；`Logging:Console:Enabled` 和 `Logging:File:Enabled` 可独立控制。最低日志级别与类别覆盖由配置提供：`Logging:MinimumLevel:Default` 为全局级别（默认 `Information`，即 `INF`，不再按 Debug/Release 构建条件切换），`Logging:MinimumLevel:Override:<类别>` 按 `SourceContext` 前缀覆盖单个类别（Development/模板为 `Information`，Staging/Production 将 `Volo.Abp`、`Quartz` 覆盖为 `Warning`）。模块加载阶段不再注册独立 Console Logger。

生产默认启用控制台、关闭文件以适配不可变容器；Development 默认同时写入控制台与 `Logs/log.txt`。容器以数字 UID/GID `1000:1000` 非 root 运行，构建时只在基础镜像尚未提供 UID/GID `1000` 时创建对应条目；宿主机挂载的 `secrets/logs/data` 必须与该 UID/GID 对齐；流水线以只读根文件系统、临时 `/tmp`、移除 Linux capabilities 和 `no-new-privileges` 启动服务，并验证 live/ready。

## Swagger 与模块加载

Swagger 分组由 `appsettings.json` 配置；模块通过入口程序集的引用链自动发现。

- 模块发现：`ConfigureConventionalControllers` 从入口程序集递归收集 `Sim.*` 程序集；业务模块通过 `Business/BusinessProjects.props` 的工程引用与 `Business/BusinessModuleRegistration.cs` 的 `[DependsOn]` 接入。
- `Swagger:Documents`：定义 Swagger 分组和展示端点；`IncludeAll: true` 的条目聚合全部模块路由（Host 的 `all` 分组），排在首位即为 UI 默认文档。

`/swagger` 前缀下的 UI 页面、静态资源与 `swagger.json` 默认只在非生产环境注册并匿名开放；Production 只有显式设置 `Swagger:AllowInProduction=true` 才注册。Docker/nginx 部署开启该开关后，通过 nginx Basic Auth 保护 `/swagger/` 与 `GET /api/swagger-config`，`.htpasswd` 由部署脚本生成且不入库。业务接口 `/api/**` 仍只接受 JWT Bearer，Swagger UI 的 `Authorize` 只是把令牌透传给被调用的接口，Basic Auth 不是业务授权边界。

契约以 `definition/` 为单一来源，保存可重生成的当前契约（admin/approval/system/openplatform）。CI 在 `App:OpenApiGeneration=true` 构建下用 `swagger tofile` 重生成并检查未提交漂移，替代原先 baseline/current 对比的兼容测试；破坏性契约变更必须在版本与迁移评审通过后显式提交新契约。前端 `src/api/{group}`（admin/approval/system/openplatform）的 SDK 函数由 @hey-api/openapi-ts 按 method+path 从契约生成（去掉 `/api` 前缀），调用路径必须与契约一致，后端改名后前端编译期即报错。构建期设置 `App:OpenApiGeneration=true` 时跳过数据库迁移、种子和静态资源映射，避免契约导出修改业务数据库；应用服务与 conventional controller 仍按正常组合根加载。

Schema ID 由 `SwaggerConfigurationExtensions.GetSchemaId` 生成：业务 DTO 使用短类名（如 `RouteConfigDto`），泛型类型格式化为 `PagedResultDtoOfRouteConfigDto`，避免全限定名携带程序集版本号导致前端生成类型名随发布漂移；短类名冲突时自动回退全限定名（`SchemaIdRegistry` 冲突防御表）。

新增模块时，需要同时检查：

- `Sim.slnx` 中的业务工程（追加到末尾 `/business/`）。
- `Business/BusinessProjects.props` 的工程引用。
- `Business/BusinessModuleRegistration.cs` 的 `[DependsOn]`。
- `Swagger:Documents` 的业务分组。
- 前端 `businessModules.ts`、动态路由和语言包是否需要同步。

业务系统从本仓创建独立仓库后的目录分区与同步流程见 [`docs/core/FRAMEWORK_SYNC.md`](../../docs/core/FRAMEWORK_SYNC.md)。

## 前后端协作点

Web 前端依赖以下后端能力：

- `/api/system/user/login`
- `/api/system/user/antiforgery-token`
- `/api/system/user/refresh-token`
- `/api/system/user/revoke-refresh-token`
- System 用户、角色、权限、模块、路由接口。
- 审批定义、实例、任务与附件接口。
- 动态路由接口返回的组件路径、布局、菜单元数据。

变更这些契约时，应同步更新前端 API 类型、路由加载逻辑和项目文档。

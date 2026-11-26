# SimApp API 开发指南

本文档说明后端本地开发、常用命令、配置和功能扩展流程。

## 环境要求

- .NET SDK 10.x
- 可选：PostgreSQL 客户端
- 可选：支持 .NET 的 IDE，如 Visual Studio、Rider 或 VS Code

## 本地启动

在 `api` 目录先提供 JWT 密钥再启动：

```powershell
$env:JwtSettings__IssuerSigningKey = "<至少 32 字节的随机密钥>"
dotnet restore
dotnet run --project src/Sim.Admin.HttpApi.Host
```

默认地址：

- API / Swagger：`https://localhost:5001/swagger/index.html`
- 直连后端时 Swagger 文档匿名开放，没有登录页与会话；导入 Apifox 等工具使用聚合文档 `https://localhost:5001/swagger/all/swagger.json`，一次拿到全部模块接口。
- 调用受保护接口时，在 Swagger UI 的 `Authorize` 里填入 Access Token；Basic Auth 只保护文档面，不替代业务接口认证。
- Production 默认不开放 Swagger；显式设置 `Swagger:AllowInProduction=true` 后才会注册。Docker Compose 为同域 nginx 部署开启该开关，并依赖 nginx Basic Auth 保护 `/swagger/` 与 `/api/swagger-config`。
- 存活检查：`https://localhost:5001/health/live`
- 就绪检查：`https://localhost:5001/health/ready`
- 兼容健康检查：`https://localhost:5001/health-status`

首次初始化通过 Web `/setup` 完成：提交后页面展示进度，后端先创建管理员账号，再创建不可登录的系统账号 `system`，并由系统账号作为创建人执行种子。系统未完成初始化时，启动自动种子与 `--seed` 都会跳过。

初始化完成后如需补齐或刷新种子数据：

```bash
dotnet run --project src/Sim.Admin.HttpApi.Host -- --seed
```

初始化后的种子同样确保系统账号存在，并以系统账号作为创建人。种子只写入基础数据与系统管理、授权管理、审批路由，不创建示例业务数据。

重复执行系统种子还会把历史动态路由组件路径迁移为当前 Web 模块路径。新增路由组件字段应相对 Web `src/modules` 填写，不带 `/modules/` 前缀和 `.vue` 后缀。用户菜单由 `GET /api/system/route-config/user-accessible-routes` 返回 `UserMenuDto`（可访问模块 + 跨模块统一组装的全局路由树）；修改该契约或 `RouteConfigDto` 后需重生成 `web/src/api/definition/system.json` 并执行 `pnpm generate:api-types`。

启动前必须从本机 Secret Manager 或部署平台密钥管理系统提供 JWT 签名密钥：

```powershell
$env:JwtSettings__IssuerSigningKey = "<至少 32 字节的随机密钥>"
```

## 常用命令

```bash
dotnet tool restore
dotnet restore Sim.slnx --locked-mode
dotnet build Sim.slnx
dotnet test Sim.slnx --no-build --no-restore
dotnet run --project src/Sim.Admin.HttpApi.Host
dotnet publish src/Sim.Admin.HttpApi.Host -c Release -o ./publish
```

NuGet 包版本集中维护在 `Directory.Packages.props`。新增依赖时，在项目文件添加无版本的 `PackageReference`，并在中央文件添加唯一 `PackageVersion`；升级版本只修改中央文件。随后更新 Host/测试入口的 Windows `packages.lock.json`、Linux `packages.linux.lock.json` 和 `eng/dependency-license-policy.json`。Windows 使用 `dotnet restore Sim.slnx --locked-mode` 验证，维护 Linux 条件依赖时还要在 Linux Agent 执行同一命令；本机可用 `-p:OS=Unix` 预检 Linux 锁文件。禁止在单个项目使用 `Version` 或 `VersionOverride` 绕过统一版本。

## 配置说明

通用配置：

- `src/Sim.Admin.HttpApi.Host/appsettings.json`

环境配置（`appsettings.{Environment}.json` 一律不入库，仓库只提交共享基线与模板；启动时若缺失会基于模板自动生成）：

- `src/Sim.Admin.HttpApi.Host/appsettings.Example.json`（模板，基于 development，默认连接本机 Compose 的 PostgreSQL，其余敏感字段留空）
- `src/Sim.Admin.HttpApi.Host/appsettings.Development.json`（本地文件）
- `src/Sim.Admin.HttpApi.Host/appsettings.Staging.json`（本地/部署环境文件）
- `src/Sim.Admin.HttpApi.Host/appsettings.Production.json`（本地/部署环境文件；容器内由 `deploy/config/appsettings.Production.json` 只读挂载）

环境差异项（连接串、日志级别 `Logging:MinimumLevel`、`App:SelfUrl` 等）都写在对应环境文件里，不要依赖代码内的环境分支。

常用配置项：

- `App:SelfUrl`：服务自身地址。
- `App:HealthCheckUrl`：健康检查路径。
- `Security`：密码、登录锁定、会话、Refresh Token、验证码、SMTP/SMS 和 TOTP 策略。
- `JwtSettings`：JWT 签发、校验、签名和过期配置。
- `DataBase`：数据库类型、连接字符串、Code First、数据种子、SQL 日志开关。`ConnectionConfigs` 是所有数据库连接的唯一配置来源——SqlSugar 和 Quartz 均通过 `DatabaseConnectionStringResolver` 从此处读取连接串，不再需要顶层 `ConnectionStrings` 节。`EnableSqlLog` 为统一 SQL 日志开关，同时控制 SqlSugar AOP 日志和 Quartz ADO.NET SQL 日志（Quartz 3.x 通过其自带 LibLog 桥直接把 SQL 日志写入 Serilog，绕过 Microsoft.Extensions.Logging 过滤，因此 Host 在 `Program.cs` 的 `UseSerilog` 中按该开关对 `Quartz.SQL` 与 `Quartz.Impl.AdoJobStore` 两个类别做 `MinimumLevel.Override`：关闭时为 Warning，开启时为 Debug）。
- `DataProtection`：应用名和持久化密钥环目录；非开发环境必须提供目录。
- `Logging:Console:Enabled`：是否输出到控制台，默认开启。
- `Logging:File:Enabled` / `Logging:File:Path`：是否输出滚动文件及文件路径；Development 默认开启，生产默认关闭。两个输出可以同时开启，格式统一为 `[模块][级别][时间] 内容`，来源只归并到稳定模块标签。
- `Logging:MinimumLevel:Default` / `Logging:MinimumLevel:Override:<类别>`：全局最低日志级别（默认 `Information`）与按 `SourceContext` 前缀的类别覆盖；非法取值回退到上一层级别。按环境在对应配置文件中维护。
- `OpenTelemetry`：`ServiceName`/`ServiceVersion`/`Environment` 资源标签、`Tracing:Enabled`/`Metrics:Enabled` 开关和 `Otlp:Endpoint` 收集器地址（留空则不导出）。业务指标定义在 `framework/Sim.Framework.Domain.Shared/Telemetry/Metrics.cs`，Host 用 `AddMeter(Metrics.MeterName)` 订阅，Prometheus 端点为 `/metrics`。
- `RateLimiting`：`[RateLimit]` 的默认次数、窗口和排队上限。
- `FileManagement`：配置文件只保留临时目录和外部转换工具开关；默认 provider、本地/数据库/对象存储配置与上传上限由 System 管理接口写入数据库。
- `ScheduleTaskExecution:AllowedCommands`：命令/程序动态 Job 可使用的命令名称白名单，例如 `dotnet`、`node` 或部署脚本名；默认空数组即禁止创建命令 Job。命令输入不得包含目录分隔符或绝对路径，工作目录是可选的独立配置。
- 模块发现：由入口程序集的 `Sim.*` 引用链自动收集，业务模块通过 Host 的 `Business/` 组合根接入，不需要配置项。
- `Swagger:AllowInProduction`：Production 是否注册 Swagger，默认 `false`；Docker Compose 通过环境变量 `Swagger__AllowInProduction=true` 显式开启。
- `Swagger:Documents`：Swagger 文档分组；`IncludeAll: true` 的条目聚合全部模块路由（Host 的 `all` 分组），置于首位即成为 UI 默认文档。

## 数据库开发

开发环境默认连接本机 Compose 的 PostgreSQL。配置中启用了：

- `EnabledCodeFirst`
- `UseSnakeCaseNaming`

表名生成规则（由 `SqlSugarCoreModule` 的 `EntityNameService` 统一处理）：

1. 移除类名 `Entity` 后缀（如 `UserEntity` → `User`）
2. 若启用 `UseSnakeCaseNaming`，转换为 snake_case（如 `ArticleTag` → `article_tag`）
3. 自动添加全局前缀 `sim_`（定义在 `Consts.TablePrefix`）

最终表名格式：`sim_<模块前缀>_<实体名>`，例如 `sim_system_user`、`sim_approval_instance`、`sim_framework_number_sequence`。
模块前缀通过 `[SugarTable]` 特性指定（如 `system_`、`approval_`、`framework_`），`sim_` 由框架自动添加，无需在各模块中重复声明。

表前缀属于全局命名基线；从旧前缀切换时，已有数据库不会自动重命名业务表，需要按新前缀重建或执行等价的重命名脚本。

Quartz ADO.NET JobStore 表前缀通过 `appsettings.json -> Quartz:JobStore:TablePrefix` 配置。未配置时按 Provider 自动推导默认值：PostgreSQL/MySQL → `sim_qrtz_`，SQL Server/SQLite → `sim_QRTZ_`。`SystemApplicationModule` 通过 `QuartzTablePrefixResolver.Resolve()` 读取配置值并设置给 Quartz 运行时；`QuartzDatabaseInitializer` 使用同一配置值生成建表 SQL（SQLite/PostgreSQL 内嵌脚本通过参数化前缀动态生成）。独立 PostgreSQL 脚本位于 `ScheduleTask/tables_postgres.sql`，已有数据库升级时执行 `ScheduleTask/migrate_quartz_table_prefix.sql` 重命名旧表。

`EnabledDbSeed` 默认关闭。系统未完成初始化时启动自动种子与 `--seed` 都会跳过；首次初始化在 Web `/setup` 提交后先创建管理员，再创建不可登录的系统账号 `system`，并由系统账号作为创建人执行基础种子。初始化完成后可通过 `--seed` 补齐升级数据；新建用户（管理端、自助注册、外部登录与初始化管理员）由 `IUserCreatedHandler` 通知业务模块按需初始化关联数据。

新增实体时建议流程：

1. 在对应模块的 `Domain/Entities` 中新增实体（system 模块按功能放入 `Domain/<Feature>/`）。
2. 在 `Domain/IRepositories` 中新增仓储接口（system 模块与实体同功能目录）。
3. 在 `SqlSugar/Repositories` 中实现仓储（system 模块放入 `Repositories/<Feature>/`）。
4. 如需初始化数据，在 `src/Sim.Admin.Domain/Data` 或模块内合适位置补充 Seeder。
5. 启动服务并确认 Code First 和数据种子执行正常。

Production/Staging 不应开启自动 Code First。数据库变更流程是：先备份，再以目标环境配置运行 `dotnet run --project src/Sim.Admin.HttpApi.Host -- --migrate`，确认成功后启动新版本；失败回滚使用迁移前备份。

## 新增 API

推荐流程：

1. 在 `Application.Contracts` 中定义 DTO 和应用服务接口。
2. 在 `Application` 中实现应用服务。
3. 在 `Domain` 中沉淀实体规则或领域服务。
4. 在 `SqlSugar` 中实现数据访问。
5. 运行服务，在对应 Swagger 分组确认接口。
6. 同步更新前端 API 类型和文档。

接口完成后还要重生成 `definition/`（CI 检查未提交漂移），并让前端基于该契约执行 `pnpm generate:api-types`。破坏性契约变更必须先完成版本与迁移评审再提交。

项目通过 Sim conventional controllers 暴露应用服务。包括浏览器认证会话在内的新接口应先定义在 `Application.Contracts`，再由应用服务实现；只有 conventional controller 无法表达的 HTTP 协议边界才允许手写 Controller，并需在架构文档说明理由。

## 新增模块

1. 在 `modules/` 下创建模块目录。
2. 按既有结构拆分 `Domain.Shared`、`Domain`、`Application.Contracts`、`Application`、`SqlSugar`。
3. 将业务工程追加到 `Sim.slnx` 末尾的 `/business/` 文件夹。
4. 业务工程引用与模块注册写在 Host 的 `Business/BusinessProjects.props` 与 `Business/BusinessModuleRegistration.cs`，不修改 Host csproj 和 `AdminHttpApiHostModule` 主体。
   - framework 不得引用 `modules/` 或 `src/` 项目。
   - 业务 Application 跨模块只引用 `Application.Contracts` 或 `Domain.Shared`。
   - 业务 Domain 不引用其他业务模块，模块 SqlSugar 不引用 Host 项目。
5. 在 `Swagger:Documents` 中新增文档分组，`Name` 与业务根路径一致。
6. 业务新增 NuGet 包在 `Directory.Packages.business.props` 中声明版本。
7. 如涉及前端菜单或页面，在 Web 前端中同步模块、路由和 API。

业务系统从本仓创建独立仓库后的目录分区与同步流程见 [`docs/core/FRAMEWORK_SYNC.md`](../../docs/core/FRAMEWORK_SYNC.md)。

## 认证开发注意事项

- 不要在业务代码中硬编码 JWT 密钥或测试账号密码。
- 需要当前用户信息时优先使用 ABP 当前用户上下文或已有 `CurrentUser` 相关契约。
- Refresh Token 相关逻辑需要同时考虑签发、刷新、撤销和过期清理。
- 浏览器会话接口必须保留 CSRF 验证；Refresh Token 只允许放在 HttpOnly Cookie，Access Token 不写入浏览器持久化存储。
- 本地 Vite 使用 HTTP 开发服务器并代理到 HTTPS API，Development 防伪 Cookie 因此不带 `Secure`；生产环境必须保持 HTTPS 和 `Secure` Cookie。
- 登录、刷新、注销等敏感入口不得审计 DTO 参数或记录 Cookie/Authorization 值。
- 变更令牌响应结构时必须同步 Web 前端的 `src/utils/request.ts`、`src/stores/user.ts` 和授权模块 API。

## 调试建议

- Swagger 可以用于验证接口分组、DTO 结构和被调用接口的认证行为。
- 文档页 `/swagger/index.html` 直连后端时匿名打开；Docker/nginx 部署需先通过 nginx Basic Auth。页面读取 `GET /api/swagger-config` 得到分组列表。要试调受保护接口，在 `Authorize` 对话框填入 Access Token。
- 数据种子异常优先查看 `src/Sim.Admin.Domain/Data`。
- 动态模块加载异常优先检查 `Business/BusinessProjects.props` 的工程引用与 `Business/BusinessModuleRegistration.cs` 的 `[DependsOn]`。
- Swagger 分组异常优先查看 `Swagger:Documents` 和 XML 注释生成。

## 发布前检查

- `dotnet build Sim.slnx` 通过。
- `dotnet test Sim.slnx --no-build --no-restore` 通过；当前安全测试位于 `tests/Sim.Security.Tests`。
- 完整 Host 集成测试通过；真实数据库 readiness、Cookie/CSRF 和迁移建表契约未退化。
- 日志格式与输出开关测试通过；应用代码没有通过 `AddConsole`、`Console.Write` 或 `Debug.Write` 绕过统一 Serilog 管道。
- 架构依赖测试通过；新增项目引用没有形成 framework 反向依赖业务模块或业务模块间实现层耦合。
- NuGet 中央版本测试通过；新增包已在 `Directory.Packages.props` 定义，项目文件没有局部版本覆盖。
- `dotnet list Sim.slnx package --vulnerable --include-transitive --no-restore` 未报告已知漏洞。
- `dotnet restore Sim.slnx --locked-mode` 和依赖许可证策略测试通过。
- `definition/` 契约已由当前 Release 程序集重生成且没有未提交漂移。
- 发布目录的 SPDX SBOM 已生成并通过完整性校验。
- 生产配置已替换默认密钥和连接字符串。
- Data Protection 密钥环已持久化到权限受控的存储，多实例使用同一应用名和密钥环。
- 生产/测试环境的环境配置文件（如容器内挂载的 `deploy/config/appsettings.Production.json`、`appsettings.Staging.json`）已按各自环境提供，不包含本地开发数据库。
- Swagger 默认仅在 Development/Staging 开放；Production 只有显式设置 `Swagger:AllowInProduction=true` 才注册 Swagger 中间件、静态 UI 和根路径跳转，默认直接访问 `/swagger/index.html` 应返回 404。
- 直连后端时 `/swagger/index.html`、`/swagger/*/swagger.json` 匿名访问返回 200，`/swagger/login.html`、`/swagger-login.html` 已移除并返回 404；Docker/nginx 部署中未带 Basic Auth 访问 `/swagger/index.html` 与 `/api/swagger-config` 应返回 401，带凭据应返回 200；聚合文档 `all` 包含全部业务模块路径且不含 `/api/swagger-config`。
- 数据种子行为符合目标环境预期。
- 前端调用的接口路径和响应结构保持兼容。
- `/health/live`、`/health/ready` 和 `/health-status` 返回预期状态，部署探针使用 live/ready 分离端点。
- `docker build -f Dockerfile -t repo.dulm.net/sim-api:<tag> .` 在具有 Docker 的 CI Agent 上通过，并以只读根文件系统验证 UID `1000` 与 live/ready。
- `Jenkinsfile` 中的锁定还原、漏洞审计、编译、契约、测试、发布、SBOM、生产镜像构建/冒烟和镜像推送阶段全部通过，不允许默认跳过质量门禁；`develop` 自动部署测试、`v*` 标签经 `sim-release` 审批发布生产。

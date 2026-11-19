# 开发指南

本仓库是通用系统平台基座，包含 API、Web 与 Windows WPF Client 三个可运行子项目。业务模块按约定的阶段与端口边界接入。

## 本地基础设施

在仓库根目录执行 `docker compose -f api/env/compose.yaml up -d --wait` 启动 PostgreSQL、Redis 和 RabbitMQ。API/Web 仍在本机运行；后端模板已对齐 Compose 默认连接；已有环境文件不会被自动覆盖。完整连接变量、首次初始化、端口覆盖及数据卷维护见 [本地开发基础设施](../ops/LOCAL_INFRASTRUCTURE.md)。

## 环境要求

- .NET 10 SDK
- Windows（运行 WPF Client）
- Node.js `^20.19.0 || >=22.12.0`
- pnpm
- 可选：Visual Studio 2022、JetBrains Rider
- 可选：LibreOffice（Word/Excel 转 PDF）、ImageMagick（服务端图片处理）；未安装时保持对应配置关闭

## 启动后端

```bash
cd api
dotnet restore
dotnet run --project src/Sim.Admin.HttpApi.Host
```

开发环境默认配置：

- 服务地址：`https://localhost:7777`
- Swagger（仅 Development/Staging）：`https://localhost:7777/swagger/index.html`
- 直连后端时 Swagger 文档匿名开放，直接打开 `https://localhost:7777/swagger/index.html` 即可，没有登录页与会话；文档页默认加载聚合文档 `Sim API（全部接口）`。
- 导入 Apifox 等工具直接使用聚合文档 `https://localhost:7777/swagger/all/swagger.json`；本地直连无需 Basic Auth，Docker/nginx 部署需要输入 nginx Basic Auth 凭据。需要试调受保护接口时，在 `Authorize` 里填入 `POST /api/system/user/login` 返回的 Access Token。
- Production 默认不开放 Swagger；Docker Compose 通过 `Swagger__AllowInProduction=true` 显式开启，并依赖 nginx Basic Auth 保护文档面。
- 数据库：以 `api/src/Sim.Admin.HttpApi.Host/appsettings.Example.json` 为模板，默认连接本机 Compose 的 PostgreSQL `sim_dev`；Redis 和 RabbitMQ 也已配置，启动后端前先启动 Compose
- 环境配置生成：Development/Staging/Production 的 `appsettings.{Environment}.json` 都不入库（仓库只提交共享 `appsettings.json` 与模板），启动时若当前环境对应的文件不存在，会基于模板自动生成本地文件
- 数据种子：系统未完成初始化时不执行（启动自动种子与 `--seed` 均跳过）；`/setup` 提交后先写入管理员账号，再创建不可登录的系统账号 `system`，并由系统账号作为创建人执行种子；种子只写基础数据，不创建示例业务数据
- 首次初始化：打开 `http://localhost:5173/setup`（或部署站点 `/setup`），设置管理员账号密码、站点名称、默认语言与主题、开放注册、登录失败上限和锁定时长；提交后页面展示初始化进度，并自动完成种子、角色分配与初始化标记，未完成初始化时所有路由都会重定向到该页

所有环境启动前必须从本机 Secret Manager、密钥管理系统或环境变量提供至少 32 字节的 JWT 签名密钥，不能提交到配置文件：

```powershell
$env:JwtSettings__IssuerSigningKey = "replace-with-a-random-secret-at-least-32-bytes"
```

该变量必须传给实际运行的 API 进程；修改后重启后端。缺少有效密钥时 Host 在启动阶段报错，不再等到登录请求才返回 500。多密钥部署也可提供一个标记 `IsActive=true` 的有效 `JwtSettings:SigningKeys:Keys` 条目。

本机 Development 环境可改用 `dotnet user-secrets set "JwtSettings:IssuerSigningKey" "<随机密钥>" --project src/Sim.Admin.HttpApi.Host`。`UserSecretsId` 只标识用户目录中的 `secrets.json`；该文件不加密，不能作为生产密钥存储。若同时配置环境变量或 `SIM_SECRETS_DIRECTORY`，后加载的配置会覆盖 User Secrets。

不要把生产密钥写入 `appsettings*.json`。如使用反向代理，应只在可信代理范围内转发 `X-Forwarded-For` 和 `X-Forwarded-Proto`；生产/测试环境还必须把反向代理加入可信列表，否则 API 会忽略转发头，把外部 HTTPS 请求识别为 HTTP，导致防伪 Cookie 等安全功能报错。只填写实际代理 IP 或网段，例如 `ForwardedHeaders__KnownNetworks__0=172.18.0.0/16`，不要信任全部私网地址。

本地开发默认使用 Compose PostgreSQL，项目已引用 Npgsql 驱动。模板连接串包含 `Maximum Pool Size=60` 与 `Timeout=15`，对应 Compose 的 `max_connections=200`；多实例部署时按“实例数 × 单实例池上限 + 运维余量”调整服务端上限。改用其他数据库时需同步调整 `DbType`、连接串及 Quartz Provider；SQLite 应将 `Quartz:JobStore:Provider` 设为空字符串以使用内存调度。SQLite 在 Windows 使用系统 `winsqlite3`，Linux/macOS 需提供 `libsqlite3`。 SqlSugar 实体在 PostgreSQL 下不要用 `Length = 8000` 触发默认 `varchar(max)` 生成；需要超长字符串或 JSON 时显式使用 `[SugarColumn(ColumnDataType = "text")]`（可空字段叠加 `IsNullable = true`）。 SqlSugar 表达式查询（`GetAsync`/`Where`/`Any` 等）内不要使用 `Nullable<T>.HasValue`/`Value` 短路条件（如 `!id.HasValue || e.Id != id.Value`），应在 C# 层分支后构造只含普通比较的表达式，否则会生成 `NOT( IS NOT NULL )` 等非法 SQL 并触发数据库语法错误。

系统完成首次初始化后，如需补齐或刷新种子数据可显式执行：

```bash
dotnet run --project src/Sim.Admin.HttpApi.Host -- --seed
```

未完成初始化时该命令只会记录跳过日志；初始化后的种子创建人统一记为系统账号 `system`。种子支持重复执行。模块种子幂等创建 `system-management`、`authorization-management` 与 `approval`；路由种子幂等补齐系统管理（存储、通知、调度、业务编号、配置、数据字典、组织架构、租户管理）、授权管理（用户、角色、模块、路由、权限）与审批中心（待办/已办/我提交的/抄送我的、流程定义、实例监控）菜单。对已存在的路由也会幂等补齐模块-路由关联，升级存量库无需手动重配菜单；组织架构与租户管理会从历史授权菜单迁移到系统管理，并同步根菜单权限并集（仅在仍为历史并集时替换）。路由种子通过 `RoutePermissionCatalog` 按路由名匹配页面读取权限码（根路由取全部子页面权限并集，含个人页的根路由保持不限制），新建路由直接写入、已有路由只补空值，不覆盖管理端维护结果。管理端动态模块根路由使用相对路径，重复执行种子会把历史 `/user`、`/system` 路径及旧默认跳转迁移到 `/admin` 布局，并隐藏 user 模块的根菜单和两个子菜单但保留路由。普通用户角色由 `UserRoleAssignmentDataSeeder` 幂等补齐 `approval` 模块，不包含系统管理与授权管理。新增业务模块时同步补模块、路由、权限种子与 Host 组合根。

种子只准备基础数据；管理员账号与站点默认值在 Web `/setup` 完成首次初始化时写入。初始化接口为 `GET /api/admin/setup/status`、`GET /api/admin/setup/progress` 与 `POST /api/admin/setup/initialize`，状态保存在 `system.setup.state` 配置项；初始化先创建或更新用户填写的管理员账号并重置密码，再创建不可登录的系统账号 `system`，以系统账号为审计归属人幂等执行基础种子（创建时间为当前时间），随后分配 Admin 角色并写入站点名称、默认语言、默认主题、开放注册、登录失败上限与锁定时长。前端在提交后轮询进度接口展示五个阶段的进度。已完成初始化或检测到既有用户的站点不会再次开放该入口。

健康探针：

- `/health/live`：仅判断进程是否存活。
- `/health/ready`：检查数据库连接，可用于停止向未就绪实例分配流量。
- `/health-status`：兼容入口，执行全部检查。

日志输出配置：

- `Logging__Console__Enabled=true`：开启控制台输出，默认开启。
- `Logging__File__Enabled=true`：开启滚动文件；Development 默认开启，其他环境默认关闭。
- `Logging__File__Path=Logs/log.txt`：设置文件路径。
- `Logging__MinimumLevel__Default=Information`：设置全局最低日志级别；`Logging__MinimumLevel__Override__<类别>=Warning` 按 `SourceContext` 前缀覆盖单个类别。

- `DataBase__EnableSqlLog=true`：统一 SQL 日志开关，同时控制 SqlSugar AOP 日志与 Quartz ADO.NET SQL 日志；默认 false。关闭时 Quartz 的 `Prepared SQL`、锁获取等 Debug 日志会被 Serilog 在 `Quartz.SQL`/`Quartz.Impl.AdoJobStore` 类别上按 Warning 过滤。

控制台和文件可以同时启用，并统一输出 `[模块][级别][时间] 内容`；原始来源会归并为 `ScheduleTask`、`System`、`RBAC`、`Framework` 或 `Admin`，事件内部换行会转义为 `\n`。

认证验证码配置位于 `Security`：`VerificationCodeExpirationMinutes` 和 `VerificationCodeMaxAttempts` 控制挑战有效期与最大尝试次数，`AuthenticatorIssuer` 是 TOTP App 中显示的发行方。Authenticator 绑定接口返回标准 `otpauth://` URI，Web 使用二维码作为首选绑定方式并将共享密钥保留为可展开的无障碍备用方式。邮件通过 `Security__EmailVerification__*` 配置 SMTP，短信通过 `Security__SmsVerification__WebhookUrl` 调用部署方的 HTTPS 网关；Webhook 接收 `phoneNumber`、`code`、`purpose`、`expiresInMinutes` JSON。生产凭据只允许通过 Secret Manager 或环境变量提供，不得提交到配置文件。邮件/SMS 未启用时 TOTP 仍可用，但对应验证码发送功能会拒绝启用。

并发登录设备上限位于 `Security:DeviceSessionLimits`，分别使用 `Pc`、`Pad`、`Phone`，允许值为 0～20，默认均为 1。修改后应验证同设备重新登录、同类型新设备淘汰最旧会话、跨类型互不影响、撤销后旧 Access Token 返回 401，以及 `my-login-sessions` 的当前设备和最近活动状态。客户端设备 ID 只能作为会话归并提示，不得提升为认证或风控凭据。
安全响应头中的 CSP 由 `Security:Headers:ContentSecurityPolicy` 配置，`appsettings.json` 提供默认值，环境文件可按需覆盖；新增外部接口域名时同步调整该配置与边缘 nginx 的 `connect-src`，不需要修改后端代码。留空时不发送 CSP，由边缘代理负责。

文件与限流配置：

- 默认存储、上传上限、本地/数据库/S3/COS/OSS 参数从后台“系统管理 → 文件存储”维护并写入数据库，不再通过 `appsettings*.json` 配置；对象存储 Secret 以 Data Protection 加密，读取接口不回显。
- `FileManagement__LibreOffice__Enabled` 和 `FileManagement__ImageMagick__Enabled` 默认 `false`；启用前确认工具路径、容器依赖、临时目录容量和许可证。
- `RateLimiting__DefaultPermitLimit` 与 `RateLimiting__DefaultWindowSeconds` 是 `[RateLimit]` 无参数时的默认值；特性构造参数会覆盖默认值。

`system_file_storage_settings`、`system_file_storage_provider`、`system_schedule_task_config`、`system_resource_access_policy`、`system_resource_file_reference`、`system_resource_share_link`、`system_resource_share_link_viewer`、`system_role.IsBuiltIn`、`system_user.IsBuiltIn` 以及 Refresh Token 设备会话列是当前需要受控迁移的结构。Production/Staging 必须先备份并执行受控 `--migrate`。资源访问表分别保存通用资源策略、托管文件引用、分享令牌版本/摘要和已用指纹摘要；令牌只返回一次且不得进入日志或业务正文。`system_resource_share_link.TokenVersion` 新增后，历史记录默认版本 0 并按 fail-closed 失效，需要重新签发。部署会话列后，旧 Access Token 因缺少 `SessionId` 需要刷新或重新登录。Worker 内置任务由业务模块通过 `IScheduleTaskDefinitionProvider` 登记，只允许改 Cron/启停；后台可创建、删除 HTTP、Command 和 Service Method 动态 Job。Service Method 必须实现 `IScheduleTaskServiceMethodHandler` 显式登记；Command 只接受命令名称，且必须在 `ScheduleTaskExecution:AllowedCommands` 白名单中，不经 Shell 执行；工作目录为可选配置；HTTP 不得目标本机、内网和保留地址。所有 Job 必须幂等或声明禁止并发。

本地/数据库 provider 可直接用于开发。外部对象存储应使用独立测试 bucket 和最小权限凭据；Office/PDF 与服务端图片处理不要在未隔离的共享生产 Host 上贸然启用，后续 Worker 隔离方案需先确认。

非开发环境的数据库结构变更应先备份数据库，再用相同发布产物和生产配置执行：

```bash
dotnet run --project src/Sim.Admin.HttpApi.Host -- --migrate
```

迁移成功后再启动服务；回滚依赖部署前数据库备份，不应在生产环境开启自动 Code First。审批模块升级会为 `approval_flow_definition` 增加非空 `tenant_scope` 并建立 `(tenant_scope, code, version)` 唯一索引；执行生产迁移前应先检查并清理同一租户范围内的重复流程版本。

审计字段补齐会把仅创建审计或无审计的可变实体升级为创建人/创建时间/修改人/修改时间四字段。SqlSugar 会为新增的非空 `creation_time` 自动补 `CURRENT_TIMESTAMP` 默认值，已有数据的表也能平滑加列；只追加日志、关联表与不可变快照不强制修改人字段。列表 DTO 需同步输出四项审计字段，管理端用 `GET /api/system/user/display-names`（登录态，单次最多 200 个 ID）批量解析昵称（空时回退账号名）与账号名，人员列显示昵称、悬停气泡显示账号名。

## 启动 WPF 客户端

先启动并初始化 API，在 Windows 上运行 `dotnet run --project client/Sim.Admin.Wpf.Client/Sim.Admin.Wpf.Client.csproj`。后端地址由 `client/Sim.Admin.Wpf.Client/appsettings.json` 的 `Api:BaseUrl` 配置（默认 `https://localhost:7777`，构建时复制到输出目录）；本机 HTTPS 证书须受信任。登录页为左右分屏（左侧主行动色品牌面板与欢迎标语，右侧 430px 登录卡，右上角主题/玄夜工具栏），只保留账号密码，登录失败与会话失效以窗口顶部居中的消息提示显示。使用账号或双重验证登录后进入工作台、个人资料、账号安全、通知、授权管理（含用户、角色、权限、模块与路由）、系统管理（含组织架构、租户创建/分配、字典项、定时任务、文件存储与通知）和审批中心。账号安全支持改密、双重验证启停、登录会话与账号注销；审批中心支持列表筛选、详情、同意/驳回/转交/加签/退回/撤回/重新提交/催办/终止、附件和流程定义草稿新建/筛选/画布新增和拖动节点/悬停显示四边连接点/拖动时连接点跟随/直线、正交线、贝塞尔曲线切换/节点和连线编辑/保存/校验/发布/停用/升版；系统管理支持字典项、定时任务、文件存储与通知渠道/模板/规则/Webhook。主题菜单提供四套 Web 风格的月白/玄夜配色，并读取/保存同一用户偏好。Access Token 过期时使用防伪令牌和进程内 Refresh Cookie 单次刷新；关闭程序后会话不保留。构建验证使用 `dotnet build client/Sim.Admin.Wpf.Client/Sim.Admin.Wpf.Client.csproj`，联调步骤与尚未复刻项见 [客户端 README](../../client/Sim.Admin.Wpf.Client/README.md)。该 Windows UI 独立构建，不加入跨平台后端解决方案 `api/Sim.slnx`。

桌面 UI 使用自建的 Naive UI 复刻控件库（`Controls/Inputs`、`Controls/Display`、`Controls/Container`、`Controls/Overlay`，样式在 `Theme/Controls.*.xaml`，由 `Theme/CustomControls.xaml` 合并进 `App.xaml`）。全端不使用投影，层级只由 2px 描边、圆角与语义色表达；颜色必须用 `DynamicResource` 语义键，尺寸用 `CustomControlSize` 三档。调整组件或视觉后用 `dotnet run --project client/Sim.Admin.Wpf.Client/Sim.Admin.Wpf.Client.csproj -- --gallery`（加 `--dark` 为玄夜配色）在预览窗口核对，再同步 `client/Sim.Admin.Wpf.Client/README.md` 与 `docs/design/ui/IMPLEMENTATION.md`。

## 启动 Web 前端

Web 开发服务器在 `web/.cert/dev-cert.pem` 与 `web/.cert/dev-key.pem` 均不存在时使用 `http://localhost:5173`；两者齐全时使用 `https://localhost:5173`，只存在一个时启动报错。证书目录已被 Git 忽略；使用 HTTPS 时请准备包含 `localhost` 的受信任证书，并按实际协议访问 `/setup`。本地 `/api` 与 `/hubs` 仍代理到 `https://localhost:7777`。

开放平台功能在首次初始化后由模块种子创建；存量站点执行 `dotnet run --project src/Sim.Admin.HttpApi.Host -- --seed` 补齐 `openplatform` 模块、权限和菜单。管理员在 `/admin/openplatform/applications` 创建应用，AppSecret 仅展示一次；签发日志位于 `/admin/openplatform/access-logs`。后端 OpenAPI 分组为 `/swagger/openplatform/swagger.json`，模块接口与安全边界见 [`api/modules/openplatform/README.md`](../../api/modules/openplatform/README.md)。

```bash
cd web
pnpm install
pnpm dev
```

Web 使用 pnpm 11。`web/pnpm-workspace.yaml` 设置 `trustLockfile: true`，表示仓库已评审并提交的 `pnpm-lock.yaml` 是可信重复安装基线，避免 pnpm 对刚提交的锁文件再次执行 24 小时发布等待期而阻断开发和 CI；该设置不关闭新依赖解析或版本升级时的成熟度检查。依赖构建脚本必须在 `allowBuilds` 中逐项评审，当前只允许 `vue-demi` 运行本包内的 Vue 版本兼容入口生成脚本。新增、升级依赖时仍必须评审并提交锁文件，运行漏洞与许可证门禁，不得通过删除锁文件或长期包名排除绕过策略。

常用命令：

```bash
pnpm type-check
pnpm lint
pnpm lint:check
pnpm test:unit
pnpm test:e2e
pnpm generate:api-types
pnpm build:production
pnpm check:bundle-budget
```

Web 端 UI 统一遵循 Paper Ocean 设计系统：颜色、字体、圆角与硬投影只从 `web/src/styles/theme-variables.css` 的 `--po-*` 令牌取值，通用版式与组件类（`.po-card`、`.po-stat`、`.po-btn`、`.po-pill` 等）在 `web/src/styles/paper-ocean.css`，纸上海洋的 Naive UI 组件级覆盖在 `web/src/config/theme.ts`；页面不写死色值、不用模糊阴影与渐变光斑。Web 另有玄青·鎏金、竹月·清雅、朱缃·典章 3 套商务风格，4 套风格 × 明暗的色值只在 `web/src/config/themes.ts` 注册表维护，商务结构差异只写进 `web/src/styles/business-skin.css`（`[data-skin='business']`），商务 Naive 覆盖在 `web/src/config/businessTheme.ts`；风格与明暗偏好由 `web/src/stores/theme.ts` 持久化到 `naive-theme`（`themeMode` 支持 `light/dark/system`，`system` 跟随 `prefers-color-scheme`），入口为侧栏菜单下方的「系统设置」（`SystemPreferences`：宽屏右侧抽屉、窄屏居中弹窗），内部由 `ThemeStylePicker` 提供风格与明暗模式选择；新增风格只需注册表 + 语言包。登录用户的语言、明暗与主题色偏好由 `web/src/stores/userPreferences.ts` 经 `/api/system/user-preference/my-preferences` 同步：路由守卫在会话恢复后等待首次加载，切换后自动防抖写回，未保存过偏好的用户保持本机选择。新增页面或调整视觉时不要写死 `2px solid var(--po-ink)`、`font-weight: 700/800/900` 或硬投影字面量，改用 `--po-border*`、`--po-weight-*`、`--po-shadow-*`；Naive 颜色字段必须是 hex / rgba 字面量。内容密度由顶栏密度开关（小 / 中 / 大，`web/src/stores/density.ts`）控制，经根节点 `data-density` 与 `--po-density-*` 令牌只调节垂直方向：页面上下内边距、纵向间距、堆叠/网格行距、筛选区上下内边距与控件高度（搜索框 / 输入 / 下拉 46 / 40 / 34px，紧凑搜索框 40 / 35 / 30px）和表格数据行上下留白；按钮高度固定为 40 / 34 / 28px，不随本轮收紧继续下调。筛选区覆盖 `.po-toolbar`、访问日志 `.log-filters` 与组织架构 `.authz-tree-search`。水平内边距、控件宽度、列宽与横向间距在所有档位保持一致；「大」档为三档中最宽松的一档。页面与表格不得绕开令牌自行写死行高或页面间距。新增页面或调整视觉前先读 `docs/design/ui/IMPLEMENTATION.md` 与 `docs/design/ui/MULTI_THEME_DESIGN.md`，用 `pnpm dev` 在 1440/1024/390 三档宽度与 4 套风格 × 明暗下自检。表单字段的简短说明统一放在标题右侧的 info 气泡（`web/src/components/FormItem/` 的 `PoFormItem` / `PoFieldLabel` / `PoHintIcon`），不要新增字段下方的常驻 hint 文案。

API 与 Web 均提供 `Jenkinsfile`，并假定 Jenkins workspace 是仓库根目录（Multibranch Pipeline 的默认行为）。后端流水线执行锁定还原、漏洞审计、编译、OpenAPI 契约、测试、发布、SPDX SBOM、生产镜像构建/冒烟和镜像推送；Web 流水线执行锁定安装、契约类型、生产依赖漏洞/许可证、类型检查、零警告 ESLint、单元测试、生产构建、性能预算、Chromium 登录 E2E，并把 `dist` 打成 `sim-web` nginx 镜像。质量步骤不得通过默认参数跳过；`pnpm lint` 仅用于本地修复，CI 使用不改写文件的 `pnpm lint:check`。

镜像推送到 `repo.dulm.net`。`develop` 推送在 CI 通过后自动部署测试环境；`v*` 标签构建会为 API 和 Web 推送同一版本标签，并由 `deploy/Jenkinsfile` 对应的 `sim-release` 作业在人工审批后部署生产。部署脚本、Jenkins 作业、Webhook 和目标机准备见 [部署指南](../../deploy/README.md)。

环境变量在 `.env.*` 中维护，常用项：

- `VITE_APP_TITLE`：浏览器标题前缀。
- `VITE_API_URL`：后端接口基地址。
- `VITE_CUSTOM_ROOT_PATH` / `VITE_CUSTOM_REDIRECT`：自定义布局根路径与跳转地址，默认 `/` -> `/admin/home`。
- `VITE_ADMIN_ROOT_PATH` / `VITE_ADMIN_DEFAULT_REDIRECT`：后台布局根路径和默认跳转，默认 `/admin` -> `/admin/home`。

站点维护模式支持热加载：修改 `api/src/Sim.Admin.HttpApi.Host/App_Data/maintenance.json`（生产为 `/app/App_Data/maintenance.json`）中的 `Site:MaintenanceMode`/`Site:MaintenanceMessage` 后无需重启 API。维护模式没有 appsettings 或环境变量开关；该文件是唯一运行时入口。Web 在页面刷新后由路由守卫读取一次 `/api/admin/site-status` 作为兜底，随后通过匿名 SignalR `/hubs/site-status` 接收维护开关推送并全站共享，导航不再重复请求；收到业务 503 时也会进入维护页。维护期间该 Hub 及其协商子路径、健康检查和状态接口不会被阻断，关闭维护后维护页会自动返回站点。本地认证请求应保持 `VITE_API_URL` 为空并使用 Vite `/api` 代理，浏览器统一访问 `http://localhost:5173`；后端开发 CORS 已允许该 Vite 源，临时后端地址可通过启动进程的 `VITE_API_PROXY_TARGET` 覆盖。不要在同一次会话中混用 `localhost` 和 `127.0.0.1`，否则 Cookie 主机不一致会导致 CSRF 校验失败。

## 常见开发入口

### 维护全站字体（Maple Mono CN）

全站（标题、正文、代码、数据、斜体）统一使用 Maple Mono CN（SIL OFL 1.1，圆角等宽，汉字步进 1.0 em、拉丁 0.6 em、空格 0.3 em），并打包 400 / 700 / 900（release 的 ExtraBold 作为 900）/ 400 斜体四套字形。字体栈定义在 `web/src/styles/theme-variables.css` 的 `--po-font-stack`，`--po-font-display`、`--po-font-body`、`--po-font-mono` 都引用它；`web/src/config/theme.ts` 的 `fontDisplay` / `fontBody` / `fontMono` 必须与之一致，因为 Naive UI 只接受字面量字符串。页面样式只能使用这三个令牌，不要另写 `Consolas`、`Georgia`、`Inter` 等硬编码栈。

`web/src/styles/maple-mono.css` 与 `web/src/assets/fonts/maple-mono/*.woff2` 由 `web/scripts/fonts/build-maple-mono.py` 生成，不要手改。脚本打包 latin 与 GB 2312 一级汉字（3755 字）；400 / 700 另补 GB 2312 二级（3008 字，共 6763 字），900 与斜体仍只有一级，汉字按使用频次切成 8 片并写入 `unicode-range`，每套字形的覆盖范围按该字体实际的 cmap 求交；页面只会下载实际渲染到的中文字片，GB 2312 一级之外的生僻字回退系统中文字体。因为正文字重也是自托管字体，中文内容较多的页面首次访问可能拉取多个字重的多数切片（工作台约 2.6 MB、长列表页约 3 MB，之后走命中缓存的哈希文件名），新增字重或扩大字符覆盖时先评估这个成本。升级字体版本时：

```bash
cd web
curl -L -o /tmp/MapleMono-CN.zip https://github.com/subframe7536/maple-font/releases/download/v7.9/MapleMono-CN.zip
python scripts/fonts/build-maple-mono.py --zip /tmp/MapleMono-CN.zip
```

脚本依赖 `fonttools`、`brotli`、`wordfreq`（`wordfreq` 缺失时按 GB 2312 顺序切片，效果明显变差）。验证至少运行 `pnpm run type-check`、`pnpm exec vitest run`、`pnpm run build`，并在浏览器中确认标题 900、正文 400、加粗 700、斜体四套字形都命中 `Maple Mono CN`，代码块的连字（`=>`、`!==`、`->`）与中英文行内混排节奏正常，代码块缩进与纯 ASCII 表格因 `word-spacing: 0.3em` 补偿仍按一格对齐（含汉字的代码表格不再 2:1 对齐）；ProseMirror 会给容器关闭连字，`paper-ocean.css` 对 `.ProseMirror code`、`.markdown-content pre code`、`pre code.hljs` 恢复 `font-variant-ligatures: normal`，代码块因此显示 `=>`、`->`、`!==` 等编程连字（正文不受影响）；`.hljs-keyword` 使用字体自带的 400 斜体，关键字与标识符靠字形区分；切片文件名带内容哈希，升级后 `dist` 资源会整体换名。

### 表单校验与提交

表单校验统一遵循“失焦 + 提交兜底”：必填、长度和格式使用 Naive UI 规则的 `trigger: 'blur'`，并在保存函数中始终调用 `formRef.validate()`；跨字段一致性在依赖字段变化时复验；唯一性在输入时防抖查重，账号昵称使用 `src/utils/nicknameAvailability.ts` 的 `createDebouncedNicknameChecker`（400ms，仅最后一次请求生效），并通过 `GET /api/system/user/nick-name-availability` 检查，后端唯一约束仍是最终判定。文件上传、复杂条件配置、审批画布等无法可靠字段化的表单在提交时校验。

必填标记必须对应真实规则，不能只写 `required` 星号；条件字段显示/隐藏后调用 `restoreValidation()` 清理旧状态。昵称在创建、更新、自助资料和外部注册中均为必填，后端返回 `Sim:System:NickNameRequired` / `Sim:System:NickNameAlreadyExists`。

### 新增或修改用户可见文案

- 后端业务异常使用稳定的 `Sim:<Module>:<Error>` 错误码，`<Module>` 必须与异常实现所属业务模块一致。错误码只能在对应 `Domain.Shared` 的 `<Module>ErrorCodes.cs` 声明一次，文件名、类名和错误码前缀必须同属一个模块；调用方必须引用常量，禁止直接写 `"Sim:*"` 字面量。同时在 `api/src/Sim.Admin.Domain.Shared/Localization/Layered/zh-Hans.json` 与 `en.json` 添加翻译，不要把中文或英文消息直接传给 `BusinessException`。
- Web 静态文案按领域添加到 `web/src/i18n/messages/<locale>.<group>.ts`，组件中使用 `t()`；两种语言 key 必须一致，key 统一为与后端一致的「冒号分隔 + 大驼峰」（`Domain:Segment[:Segment]`，如 `Common:Search`），动态拼接键必须经由 `messageKey(...parts)` 生成。业务错误、动态菜单、站点配置和登录页品牌标语添加到后端 `Localization/Layered`，菜单种子只保存 `Menu:*` key（同 `Sim:<Module>:<Error>` 冒号风格），并同步 `MenuLocalizationKeys.ClientKeys` 或 `ClientLocalizationKeys` 白名单。修改该接口后重生成 Admin OpenAPI 和 Web 类型；不得在前端复制 `Sim:*`、`Menu:*` 或 `Auth:Brand*` 翻译。
- 只被自身页面消费的模块文案必须按需注册，避免整块文案进入入口包：在 `web/src/i18n/lazyPacks.ts` 的 `packLoaders` 注册语言包，并在 `COMPONENT_PACK_RULES` 声明其组件路径前缀（与后端种子 `RouteDataSeeder` 的 `component` 字段一致）。`dynamicRouteLoader` 会在导入组件模块前注册对应语言包，因此规则失配不会报错，只会让文案回退成 key；新增或调整语言包时必须同步更新 `src/i18n/lazyPacks.test.ts` 的路径映射断言。全局导航、布局和公共组件依赖的文案仍随入口包注册。
- 语言包按需加载失败时 `setLocale` 必须保持当前语言，首屏语言包不可用时 `main.ts` 回退到内置中文；不要恢复“加载失败也切换 locale”的写法，否则后端菜单已切到外语、前端正文仍回退中文，会形成中英混排。`/api/admin/client-localization` 响应声明 `Vary: Accept-Language` 且 `No-Store`，前端请求同时携带 `culture`/`ui-culture` 查询参数，避免浏览器或反向代理复用另一种语言的资源。
- 组件路径前缀匹配只覆盖后端动态路由；前端静态模块（如 `account` 的个人信息与账户安全页复用 `authorization` 模块组件）不经过 `dynamicRouteLoader`，必须在自己的路由文件里用 `ensureMessagePacks(...)` 先加载语言包再返回组件，见 `web/src/modules/account/routes.ts`。否则首次进入会直接显示 `Authz:*` 等原始 key。
- 新语言需要同时补充 Naive UI 语言映射、前端语言选项、后端 `LanguageInfo`/JSON 资源及端到端验证。
- 联调时切换客户端语言，并确认请求头分别为 `Accept-Language: zh-Hans` 与 `Accept-Language: en`。

### 新增后端业务接口

1. 在对应 `modules/<module>` 下确认是否已有业务模块。
2. 在 `Domain` 添加实体、领域逻辑和仓储接口。实体继承 Sim 基类（`Entity`、`AuditedAggregateRoot` 或多租户变体），领域服务继承 `DomainService`。
3. 在 `Application.Contracts` 添加 DTO 和服务接口。DTO 继承 `EntityDto`、`PagedResultDto` 等基类，服务接口继承 `IApplicationService`。
4. 在 `Application` 实现应用服务，继承 `ApplicationService` 或 `CrudAppService`；业务异常抛 `BusinessException` / `ValidationException` / `AuthorizationException`。
5. 在 `SqlSugar` 实现仓储。
6. 如需暴露到 Swagger，确认 Host 的模块加载和 Swagger 配置。
7. 更新 `api/README.md` 和相关项目文档。

ABP 依赖已收缩为 `Volo.Abp.Core`（模块体系）与 `Volo.Abp.Localization` 两个包，编写代码时不要再引用已移除的 ABP 类型：

- 用 `BusinessException` 代替 `BusinessException`，用 `IApplicationService` 代替 `IApplicationService`，当前用户/租户用 `ICurrentUser` / `ICurrentTenant`，对象映射用 `IObjectMapper`。
- 依赖注入使用标准 ASP.NET Core 注册（`services.AddTransient` 等），不要使用 `ITransientDependency` 等 ABP 自动注册接口。
- 生成主键直接用 `Guid.NewGuid()`，不要使用 `IGuidGenerator`；获取当前时间用 `DateTime.UtcNow`，不要使用 `IClock`。

### 新增 Web 静态模块

1. 在 `web/src/modules` 下创建模块目录。
2. 提供 `index.ts` 导出 `ModuleConfig`。
3. 提供 `routes.ts` 定义模块路由。
4. 在业务模块清单 `src/config/businessModules.ts` 中追加模块（平台模块不需要注册）。
5. 根据布局选择 `layout: 'admin'`、`'custom'` 或 `'none'`。
6. 模块页面由后端种子菜单驱动加载（`enabled: false` 时静态路由仅作为与种子同构的声明），新增页面必须同步后端模块种子贡献者的 `component` 路径。外链型菜单项（点击直接新标签打开、不进入内容区）在 `web/src/config/externalMenuLinks.ts` 按路由名注册目标解析函数。
7. 模块自带文案按需注册语言包，见「新增或修改用户可见文案」。
8. 业务仓独立构建时，在入口调用 `registerModuleComponents(import.meta.glob('./modules/**/*.vue'), 'modules/')` 注册页面，并用 `registerMessagePacks` 注册业务语言包，再调用 `createApp`。
9. 更新 `web/README.md` 中的模块说明。

### 路由兜底与 404 展示

- 后台未知地址由 `AdminLayout` 下的兜底路由 `AdminNotFound`（`web/src/router/routes.ts` 的 `adminFallbackRoutes`）承接，404 只在内容区渲染，保留侧栏、顶栏与标签栏；该路由 `meta.tab: false`，不写入标签页。
- 其余未知地址仍由顶层 `NotFound` 在布局外渲染；`web/src/views/core/404.vue` 通过 `embedded` 属性区分两种形态（内嵌形态不自带画布和主题 / 语言切换）。
- 新增布局类型时，如需同样保留框架，在该布局路由下增加同级兜底路由并向 404 传入 `embedded: true`。
- 菜单图标名必须在 `web/src/utils/iconLoader.ts` 显式注册（PascalCase 或历史 `mdi:*` 别名）；未注册的名称会渲染警告占位图标并在控制台告警。新增种子图标时同步补别名与 `iconLoader.test.ts` 用例，例如评论菜单的 `mdi:comment-text-multiple-outline` 映射到 `ChatbubbleOutline`。

### 新增 Web API 调用

1. 优先放在对应模块的 `api` 目录。
2. 复用 `src/utils/request.ts`，不要直接新建独立 Axios 实例。
3. 契约单一来源为 `web/src/api/definition`（后端导出提交），重生成后执行 `pnpm generate:api-types`；DTO 优先引用生成的 Schema，仅前端表单/视图类型放在模块内或 `src/types`。
4. API 调用优先使用 `src/api/{group}` 的 SDK 函数（@hey-api/openapi-ts 按 method+path 生成、去掉 `/api` 前缀），路径必须来自契约，禁止 hardcode 契约之外的路径；后端接口改名后前端编译期即报错。脚手架 `_template` 的占位路径除外，以裸 `request` 调用显式豁免。
5. 分页接口的 `MaxResultCount` 上限为 500（`LimitedResultRequestDto`）。Web 前端单页不超过 100，不得传 200/500/1000；需要全量选项或树节点时使用 `src/utils/pagination.ts` 的 `collectAllPages` 逐页拉取。桌面端分页选择器支持 20/50/100/200/500，手输值钳制在 20–500。
6. 如果接口路径、认证或错误格式变化，同步更新文档。

### 新增文件存储 Provider

1. 只在独立适配项目实现 `IFileStorageProvider`，稳定端口留在 `Sim.Framework.FileManagement`。
2. provider 必须规范化对象键，阻止绝对路径和 `..` 穿越，并明确元数据、覆盖和删除语义。
3. 在 Host 组合根注册实现和配置，不让业务模块直接引用厂商 SDK。
4. 增加往返、缺失、覆盖、删除和故障测试；云服务测试使用隔离账号/bucket。
5. 更新权限、OpenAPI、配置示例和运维清理策略。
6. 扩展 `FileStorageConnectionTester` 的连接测试分支，使用只读探测（本地目录写入并清理临时文件、数据库查询、对象存储列桶），不得写入或删除真实业务对象。

### 为接口增加限流

- 使用 `[RateLimit]` 继承配置默认值；只有业务明确要求时才在特性中传入次数/窗口。
- 通过 `PartitionName` 让同一业务操作共享桶，避免用显示名称形成不稳定分区。
- 匿名接口按可信客户端 IP 分区，已登录接口按用户 ID 分区；代理部署必须先配置可信转发头。
- 补充成功额度、超限 `429` 和 `Retry-After` 测试。多实例部署前需评审共享限流或网关方案。

### 修改认证流程

需要同时检查：

- 后端 `modules/system/Sim.System.Application/User/UserService.cs`
- 后端 JWT 配置和安全配置
- 前端 `src/utils/request.ts`
- 前端 `src/stores/user.ts`
- 登录页和路由守卫
- `docs/core/ARCHITECTURE.md`、`api/README.md`、`web/README.md`

### 修改用户删除流程

删除用户是跨模块事务，必须保持“账号数据硬删除、业务引用按端口清理”的边界：

- `modules/system/Sim.System.Application/User/UserService.cs` 的 `DeleteAsync` 先删除角色关联、偏好与 E2EE 身份，再吊销会话，随后调用 `IUserDeletedHandler` 处理器，最后删除用户行；处理器抛异常时整体回滚。
- System 处理器 `SystemUserAccountDataCleanupHandler` 清理外部登录绑定、刷新令牌、组织成员、组织负责人引用、存储配额与用户拥有的分享链接；文件元数据、下载记录和访问审计按业务/审计需要保留。
- 新增业务模块若持有用户数据，实现 `IUserDeletedHandler` 并注册为瞬时依赖，不要在 `UserService` 中直接引用该模块；需要匿名化而非删除的业务数据由模块自行定义并在文档中写明。
- 自助注销入口为 `POST /api/system/user/delete-my-account`：校验当前用户与当前密码，限流分区 `account-deletion`（3 次/60 秒），复用 `DeleteUserCoreAsync` 并清除刷新令牌 Cookie；Web 账户安全页分两步收集当前密码与「已了解注销事项」确认短语，成功后调用 `userStore.logout()` 回到 `/login`，内置账号隐藏入口且后端返回 `Sim:System:BuiltInUserProtected`。
- 回归覆盖 `UserDeletionCleanupTests`（处理器调用、账号数据清理、业务清理与自助注销的密码错误/成功/内置账号保护）；改动实体字段时同步更新跨模块清理范围。

### 外部登录（Provider 抽象）

System 支持微信、QQ、Steam、Microsoft、Google 外部登录。公共抽象是 `IExternalLoginProvider`，Provider Registry 按 `Enabled` 过滤；各 Provider 保留原生协议：微信/QQ 使用 OAuth 2.0，Steam 使用 OpenID 2.0，Microsoft/Google 使用标准 OIDC。登录与绑定统一采用后端代理式流程：前端动态获取已启用 Provider 并跳转提供方；`state` 由后端生成并写入 Data Protection 加密的 HttpOnly Cookie，OIDC 同时校验 `nonce`，加密 state 会话保存“登录”或“绑定到指定用户”的意图。提供方统一回调到 `/auth/external/callback`：该地址是 Provider 控制台登记的固定回调落点，前端只在这里接力，不再渲染中间页；前端从 state 前缀恢复 provider 并调用统一后端回调，回调后已绑定身份直接签发本系统会话，未绑定身份返回一次性注册令牌并进入“关联已有账号 / 注册新账号”选择页，绑定意图则要求当前登录用户与 state 中用户一致并写入绑定关系；失败时按发起来源回到登录页或账户安全页并弹出错误消息。注册令牌、2FA challenge 和失败提示不放入 URL，由前端用短期 sessionStorage 接力到目标页。提供方 access token 不落库，绑定表 `system_external_login` 只保存 `Provider + ProviderKey` 的 SHA-256 摘要且唯一；系统不依据 email 自动关联已有账号。已绑定用户开启 2FA 时仍必须完成原有 challenge。

配置位于 `ExternalLogin`，每个 Provider 独立设置 `Enabled`，默认全部禁用。微信/QQ 需补充 `ClientId`、`ClientSecret`；Steam 可选 `Realm`；Microsoft/Google 需补充 HTTPS `Authority`、`ClientId`、`ClientSecret`，`Scopes` 必须包含 `openid`，默认包含 `openid profile email`。Microsoft Authority 必须与应用注册的账号受众匹配：`common` 支持组织与个人账号，要求应用注册选择 All；仅个人账号使用 `consumers`；仅组织账号使用 `organizations`，需要限制组织时可改为具体租户 Authority。还需设置固定外部回调基地址 `CallbackBaseUrl`，本地开发使用 `http://localhost:5173/auth/external/callback` 并临时设置 `StateCookieSecure=false`；生产必须使用 `https://<domain>/auth/external/callback` 并恢复 `StateCookieSecure=true`。Provider 控制台的重定向 URI 必须与 `CallbackBaseUrl` 完全一致，不能带 query 或 fragment。未绑定的提供方身份统一先在“关联已有账号”与“注册新账号”之间选择，不再使用 `CreateUnlinkedAccounts` 自动建号；关联走本系统账号密码校验后写绑定，注册补齐受 `security.registration-enabled` 约束（关闭时回调仍返回令牌但标记不可注册，只保留关联入口）。匿名端点：`GET /api/system/external-login/providers`、`GET /api/system/external-login/redirect/{providerId}`、`GET /api/system/external-login/callback/{providerId}`、`POST /api/system/external-login/register-with-external-login`、`POST /api/system/external-login/link-with-external-login`；已登录用户绑定端点：`GET /api/system/external-login/my-bindings`、`GET /api/system/external-login/my-binding-redirect/{providerId}`、`DELETE /api/system/external-login/my-binding/{providerId}`。外部登录 HTTP 客户端日志和出站 Trace 均不携带 query，避免 Provider 凭据泄漏。

完整登录、关联/注册分支、绑定与解绑流程及 Microsoft 时序图见 [`EXTERNAL_LOGIN.md`](../ops/EXTERNAL_LOGIN.md)。

修改这些接口后，需要重新生成 OpenAPI 契约并执行 `pnpm generate:api-types`，确保前端请求模型与后端契约一致。

## 文本与换行约定

普通文本统一使用 CRLF；`*.sh`、`Dockerfile` 和 `Jenkinsfile` 使用 LF。提交前可运行 `git ls-files --eol` 检查，不允许出现 `w/mixed`。

## 验证建议

后端改动至少运行：

```bash
cd api
dotnet build Sim.slnx
dotnet test Sim.slnx --no-build --no-restore
```

`tests/Sim.Security.Tests` 在认证、权限、数据库、种子、路由迁移、日志、本地化和 OpenAPI 基线上，还验证 Worker 内置任务登记、动态 Job/Trigger 创建删除、HTTP 私网阻断、Command 白名单、系统存储配置、六类日志、特性限流、文件存储与头像边界、系统配置种子与 CRUD、数据字典值类型校验和 CRUD。架构测试同时要求生产代码不得硬编码 `Sim:*` 错误码，同一错误码只能声明一次，并校验错误码文件、类名、前缀与实际项目模块一致。使用完整 TestHost 的 `HostIntegrationTests` 与 `ApprovalEngineFlowTests` 归入不可并行的 `HostCollection`，避免 SqlSugar/Serilog 进程级状态被多个宿主互相覆盖。

新增后端项目引用时，必须保持 framework 不引用业务模块、业务 Application 跨模块只引用 Contracts/Domain.Shared、业务 Domain 不引用其他业务模块、模块 SqlSugar 不引用 Host，并运行架构依赖测试确认边界没有退化。

新增或升级后端 NuGet 包时，在目标项目添加不带版本的 `PackageReference`，版本只写入 `api/Directory.Packages.props`，并更新 Windows `packages.lock.json`、Linux `packages.linux.lock.json` 和许可证策略。完成后运行锁定还原、Release 编译、测试以及 `dotnet list Sim.slnx package --vulnerable --include-transitive --no-restore`，禁止使用项目级 `VersionOverride`。

涉及认证或请求层时应覆盖刷新并发、单请求一次重试和刷新失败清理。Web 缓存列表页不得同时用 `onMounted` 与 `onActivated` 调用同一加载函数，应复用 `useKeepAliveRefresh` 并测试首次显示只加载一次。上线前还需在真实后端验证登录、刷新与注销链路。

部署包含本次认证存储变更时，旧 `localStorage` 会话和已有 Refresh Token 会失效，用户需要重新登录；旧密码无需批量重置，会在首次成功登录时升级。Production/Staging 还必须通过外部配置提供 `DataBase__ConnectionConfigs__0__ConnectionString` 和 `DataProtection__KeysPath`，空连接串或未配置持久化密钥环时会拒绝启动。多实例必须使用相同的 Data Protection 应用名和权限受控的共享密钥环，并由平台 KMS、加密卷或等效机制保护静态密钥。

Web 改动至少运行：

```bash
cd web
pnpm type-check
pnpm lint:check
```

当前 `web/src` 下没有单元测试文件，以下测试文件引用代表需要恢复的回归覆盖；恢复前不得将前端单元测试视为已验证。Web 单元测试随功能同步扩充，提交前以 `pnpm test:unit -- --run` 结果为准。头像下拉不得恢复 `/user/settings` 失效入口；系统个人信息与账户安全（含活跃会话）分为两个页面，均从头像下拉进入；个人信息的头像必须位于字段之前并通过弹窗编辑。个人资料页为单卡片行内编辑：每个字段行尾的铅笔图标把值切换为输入框，输入框后缀箭头按钮（描述为文本框内右下角按钮，`Ctrl/Cmd+Enter` 提交）只保存该字段，`Esc` 取消，不得恢复右侧表单卡片与整表单保存按钮；保存失败时保留编辑态与草稿。存储配置保持单一 Form 和单提供器编辑区；调度页面必须区分可删除的动态 Job 与不可删除的 Worker Job。其他改动仍需保持登录会话、图片裁剪、动态路由、Tab 批量关闭、卡片自适应、审批列表与用户目录、通知实时重连和登录设备识别的已有测试。

涉及页面、路由、登录或主题时，建议补充运行：

```bash
pnpm test:unit
pnpm test:e2e
```

## 企业级变更复查

功能增强或缺陷修复完成前，使用 [`ENTERPRISE_SYSTEM_GUIDE.md`](ENTERPRISE_SYSTEM_GUIDE.md) 的“变更触发矩阵”和“完成定义”复查安全、数据、可靠性、可观测性、测试、部署与回滚。若改变当前能力或治理优先级，在同一次变更中更新该文档；若无影响，在交付说明中注明已检查。

企业框架审查建议记录在 [`enterprise/ASSESSMENT.md`](enterprise/ASSESSMENT.md)。建议项不等于实施授权；开始任何表中优化前必须向用户说明方案并取得确认。

## 通知平台开发

新增普通事件时，调用 `INotificationEventPublisher` 并使用稳定、带版本的事件名；不要从业务模块选择 SMTP、腾讯云或某个 URL。事件只携带模板白名单数据、用户 ID/显式地址和关联 ID，不携带实体快照、验证码或 Secret。

面向系统账号发送时只设置 `NotificationRecipient.UserId`；System 会通过 `INotificationAccountDirectory` 将邮件解析为账号邮箱、短信/语音解析为手机号、站内/浏览器解析为用户 ID。需要覆盖账号资料或发送到系统外目标时才设置 `Address`，且显式地址优先。账号目录实现归属账号模块的 Application，System 只能引用 Contracts 中的端口，禁止新增 System → RBAC 项目引用。

新增渠道 Provider 时：

1. 在 `Sim.System.Domain.Shared/Notifications` 保持最小 SPI。
2. 在 `Sim.System.Application/Notifications/Providers` 实现协议并注册到 `SystemApplicationModule`。
3. 在初始化定义、Web `providerSchemas.ts` 的分组字段/Secret 标记、后台 Provider 选项、中英文资源和测试中同步登记。
4. 明确 `Accepted`、`Delivered`、可安全重试、永久失败和状态未知的映射；日志不得包含目标、正文和 Secret。

本地默认使用 Local MQ/Cache。RabbitMQ 设置 `Framework__Messaging__Provider=RabbitMQ`，并将 `Framework__Messaging__ConnectionString` 设为 `amqp(s)` URI；URI 中用户名或密码的保留字符必须百分号编码（`@` → `%40`、`:` → `%3A`、`/` → `%2F`），例如密码 `Dev@123` 写成 `amqp://sim:Dev%40123@localhost:5672/`，否则启动时 URI 校验失败并停止 Host；Kafka 将该连接值设为 Bootstrap Servers，可另外配置 `ClientId`、`SecurityProtocol`、`SaslMechanism`、`AccessKey`、`SecretKey` 与 `DeadLetterTopic`；RocketMQ 将连接值设为 5.x gRPC Endpoint，可配置静态 AccessKey/SecretKey。Redis/Hybrid 设置 `Framework__Caching__RedisConnectionString`。Production/Staging 先备份再执行 `--migrate`，并验证 Data Protection Keys 持久化。

通用业务编号通过 `IBusinessNumberGenerator` 使用。先在“系统管理 → 业务编号”创建业务场景数据库记录，填写不可变业务键、名称、格式、初始值与启停状态；不从 `appsettings` 读取，也不允许业务调用时覆盖格式。模板必须包含且仅包含一个 `{sequence}`；`{date:yyyyMMdd}` 接受任意 .NET 日期格式，`{sequence:D6}` 和 `{sequence:000000}` 均可生成补零流水号，`{business}` 会写入业务键。日期占位符渲染值决定重置周期，故日/月/年格式无需另设重置任务。首次部署会新增 `system_business_number_definition` 和 `framework_number_sequence`，Production/Staging 先备份并执行受控 `--migrate`；不要自行缓存或预分配流水值，否则会破坏多实例唯一性。
系统配置通过“系统管理 → 系统配置”维护。分组键/配置键创建后不可修改，且必须在当前租户内唯一；配置值是字符串，业务模块不得把敏感凭据放入配置值。`SystemConfigurationDataSeeder` 在 `--seed` 或数据库种子时幂等创建站点、安全、文件与界面默认参数，已有记录不会被覆盖。自助注册成功后分配的角色由 `system.security` 的 `security.registration-role-codes` 控制：值为英文逗号分隔的角色代码（默认 `User`），只匹配非内置角色，内置 Admin 等角色不会通过注册下发；配置缺失或为空时回退 `User`。内置 `User` 角色的基础权限与模块由种子补齐，管理员可在“授权管理 → 角色”继续调整；升级站点发布后需执行一次 `--seed` 才会新增该配置项并补齐 `User` 角色基础权限（未执行时注册仍回退 `User`，但该角色可能还没有权限）。

用户默认密码保存在 `system.security` 的 `security.user-default-password` 配置项：首次种子生成随机值（不得在源码写死可用默认密码），管理员可在系统配置中覆盖；密码需满足当前密码策略，否则创建/重置返回 `Sim:System:DefaultUserPasswordInvalid`，配置缺失且回退 `App:AdminDefaultPassword` 为空时返回 `Sim:System:DefaultUserPasswordNotConfigured`。管理端 `POST /api/system/user` 忽略请求密码并使用该值，`CreateUserDto.password` 仅供自助注册；管理端创建与编辑用户的邮箱为可选项，空白邮箱按未填写处理并以 `null` 保存，非空仍校验邮箱格式；`POST /api/system/user/{id}/reset-user-password`（`system:user:reset-password`）重置为默认密码、清空锁定并吊销目标用户全部 Refresh Token，内置账号不可重置。Web 用户编辑不再提供密码字段。

数据字典通过“系统管理 → 数据字典”维护，用于业务枚举、业务参数等可配置值。字典键创建后不可变，值类型仅当字典下没有任何数据项时可修改；字典项选择所属字典后继承其值类型，后端支持字符串、数字和日期校验，数字统一保存为 Invariant `G29` 文本（例如 `12.30` 规范为 `12.3`）。管理页先展示字典大类列表（桌面表格、窄屏卡片），“查看数据”会打开独立字典数据 Tab 维护字典项。字符串和数字可在字典上统一配置输入框插槽展示的前缀/后缀（例如 ¥、% 或单位），这些符号不参与校验、不进入存储值，也不用于列表展示。新增种子参数需同步 `SystemConfigurationDataSeeder`、中英文文档与必要测试；数据字典不强制内置业务种子。生产升级先备份并执行受控 `--migrate`。

后台以“通知配置”为记录概念，每条记录选择渠道与 Provider（供应商实现）；配置编辑器使用 Provider 专属结构化表单。字段和分组说明放在可聚焦的信息图标 Tooltip 中，不直接占用表单纵向空间。高级 JSON 只用于兼容尚未进入 Schema 的扩展字段。SMTP 密码、腾讯云 `SecretId`/`SecretKey`、阿里云 `AccessKeySecret` 以及机器人 Webhook URL 等必须进入 Secrets；Endpoint、Region、AppId、签名和模板编号进入 Settings。空 Secret 输入保留已有值，非空值按键合并，只有显式“清空全部凭据”才删除已有 Secret。初始化器会为旧的空配置补齐字段示例，但不会覆盖已有配置。真实账号仍须在目标环境完成模板、资质、回执、限流与费用联调。

站内通知实时链路为 `/hubs/user-notifications`。开发环境的 Vite `/hubs` 代理必须启用 WebSocket；生产反向代理需允许 WebSocket，并兼容 SignalR 协商使用的 SSE/Long Polling 降级。WebSocket 建连按 SignalR/JWT 约定使用查询参数 `access_token`，网关、反向代理和访问日志必须对该参数脱敏或完全排除。System 先提交站内通知/已读状态，再通过 Contracts 端口发布无正文的用户级失效事件；实时通道不可用时不得让业务操作失败，Web 会自动重连，但不以首次加载、定时、页面可见性、焦点、建连/重连或已读操作后的查询作为兜底。通知 API 仅在收到实时变更事件或展开通知面板时读取。新消息提示必须根据 `visibilityState` 与 `document.hasFocus()` 二选一：活动页面使用站内通知组件，非活动页面仅在浏览器权限已授予时使用 Notification API；首次加载不提示历史消息，同一条消息不能同时弹两类提示。SignalR Redis backplane 由 `Framework:SignalR:UseRedisBackplane` 控制：Development 默认关闭，其他环境默认开启；显式启用或部署多实例前，必须验证 Redis 连接与跨实例连接路由。未完成 backplane 或等效跨实例广播验证时，不能宣称实时通知具备完整多实例一致性。

通知 Delivery 后台轮询由 `System:Notifications:DeliveryEnabled` 控制且默认启用。只有不验证异步投递、并使用单文件 SQLite 的专用集成测试宿主才应关闭它；同时关闭 `Framework:Messaging:Enabled`，避免后台 Outbox 与测试请求争用同一测试连接。生产和常规开发环境保持默认值。

后端契约变化后重新生成 `web/src/api/definition/system.json` 并运行 `pnpm generate:api-types`。至少运行后端测试、Web `type-check`/`lint:check`/单测和生产构建；真实 MQ、Redis、云短信、语音及机器人仍需要目标环境联调。DTO 的 DataAnnotations 校验由 `ModelStateValidationFilter` 统一转 400，`error.message` 必须带字段级原因（`error.details` 保留明细数组）；路由路径允许相对或绝对路径，并支持 `:param`、`:param?`、`:param(regex)` 与 `*` 动态段。前端业务 `catch` 使用 `handleError`，避免与请求拦截器已展示的具体错误重复提示。

RBAC 的用户角色或角色权限发生实际变化时，服务端会吊销受影响用户的 Refresh Token；由于 Access Token 绑定登录会话，联调时需预期这些用户在下一次会话校验（缓存 30 秒）后返回 401 并重新登录。角色分配额外要求 `system:role:manage`，即使调用方持有 `system:user:create/update`，缺少该权限也会返回 403。新增或调整授权相关 DTO 时，同步更新 `web/src/api/definition/system.json` 的 `UserDto`/`CreateUserDto`/`UpdateUserDto`/`RoleDto` 与 `reset-user-password` 路径并重新生成 SDK，不要手改 `src/api/system`。内置角色/账号以 `RoleEntity.IsBuiltIn` / `UserEntity.IsBuiltIn` 识别，权限旁路使用 JWT `IsBuiltInRole` claim；新增内置身份时由种子或迁移写入标记，不要在授权逻辑中重新引入角色编码或用户名判断。路由访问权限存放在 `RouteMeta.PermissionCode`（逗号分隔，`RouteMeta.PermissionRequireAll` 决定任一或全部满足，默认任一），管理端编辑路由时从权限目录下拉选择（选项展示名称与权限码、选中标签只显示名称），并可逐路由切换匹配方式；权限目录读取额外允许路由管理权限；修改 `RouteMetaDto`/`CreateRouteMetaDto`/`UpdateRouteMetaDto` 后同样需要重生成契约；用户菜单接口 `GET /api/system/route-config/user-accessible-routes` 返回 `UserMenuDto`（`modules` + 全局 `routes`）：后端从全局路由目录统一建树，可访问模块关联的路由会沿 `ParentId` 补齐全部祖先，再按 JWT 权限裁剪，因此路由改挂到其他模块的父级后不会退化为根路由；前端 `DynamicRouteLoader` 只请求这一轮，不再逐模块拉取和合并。路由管理查询接口仍返回全量。模块关联由多对多关联表维护，`ModuleConfigDto`/`CreateModuleConfigDto`/`UpdateModuleConfigDto` 的 `routeIds` 变更后同样需要重生成契约；关联同步会自动补齐祖先；路由是全局目录，种子仅在新建路由时建立所属模块关联，已有路由的关联由模块编辑页维护。


## 系统 E2EE 与通行密钥

System 只提供身份层 E2EE 能力（`system_user_e2ee_identity`、`system_user_e2ee_passkey`），不包含任何业务实体的服务端解密路径；业务模块启用端到端加密时复用该身份与解锁面板，并在自身契约中扩展密文字段。

设备缓存变更后运行设备密钥保护单测、keyVault 锁定竞态测试和 `pnpm check:e2ee-device-cache`。新数据库版本为 3，旧用户记录按读取迁移；不可导出 CryptoKey 需要 Web Crypto 与 IndexedDB 支持，失败不得保存原始设备密钥兜底。

通行密钥解锁要求精确配置 E2eePasskeys:RpId 与 Origins，生产默认关闭，开发 localhost 已启用。新登记凭证名称由 `web/src/utils/deviceEnvironment.ts` 异步获取可选型号（500ms 超时/拒绝/空值回退系统名）与浏览器名，服务端追加四位展示短编号，如 `Windows · Edge · A7F2`；不读取真实设备名/MAC，不使用 credential ID 指纹，登录会话名称优先取 UA-CH 型号，无型号时用可读系统名（Windows/macOS/Linux/Android），第二段始终是浏览器平台，再拼接浏览器指纹前六位，如 `PMA110 · armv81 · 3f9a1c`、`Windows · Win32 · 3f9a1c`（型号为占位值 `K` 时视为缺失）。设置页通过 `PUT /api/system/e2ee-passkey/{id}/name` 重命名完整标签（1–100 字符，去除首尾空白），既有名称不自动迁移。接口契约修改后重导出 system OpenAPI 并生成 SDK。业务模块执行加密写入前需要重新验证身份时复用同一解锁面板，允许通行密钥、独立口令或离线恢复密钥；`requestE2eeUnlock(reason, 'encrypt')` 每次都打开面板且不读取设备缓存，面板按用途显示“验证加密身份/验证并继续”。命名回归运行 `deviceEnvironment.test.ts`（型号/系统回退、拒绝/超时）与 `E2eePasskeyTests`（重命名所有权/租户隔离、审计及改名后解锁）。验证 WebAuthn 时运行 E2eePasskeyTests（真实 ES256/CBOR）及 `pnpm check:e2ee-passkeys`（浏览器虚拟 PRF）；后者模拟网络，不替代后端签名验证。新增 NuGet 包 Fido2 同步 Windows/Linux 锁文件、许可证和漏洞检查；真实硬件/同步提供方需部署环境联调。

PRF 登记回归还需覆盖创建时有 PRF、后续 get() 缺失及两阶段输出不一致；两者都不得提交服务端登记。浏览器验证脚本另覆盖解锁缺失 PRF 不提交 assertion。真实提供方问题需区分 Windows 本机凭证与 Microsoft 密码管理器/第三方提供方，PIN 验证成功不代表已返回 PRF。

通行密钥排查优先复制失败区域的诊断 JSON（刷新前），按随机操作编号观察 registration-options → credential-create → registration-get → PRF 一致性 → registration-submit / unlock-get → unlock-submit → local-unwrap。NotAllowedError 本身无法区分取消、超时和系统拒绝；PRF firstLength=0 表示该次未返回有效输出，不直接推断整台浏览器不支持。日志仅记录本地白名单元数据，禁止临时增加原始 credential、扩展对象、异常对象或 API body 的 console 输出。运行 passkeyDiagnostics.test.ts 与浏览器检查验证脱敏、100 条上限和失败阶段；实际提供方仍需原生环境日志复现。

上传校验回归使用 MarkdownUploadValidationTests 与 FileManagementTests，不启动额外 SQLite 或容器环境：覆盖 UTF-8 有/无 BOM、中文、短文本、常见 MIME、大小写扩展名、Markdown 内链接/HTML/JSON 字面内容；验证 `.md`/`.txt`/`.csv`/`.json`/`.xml`/`.css`/`.sql` 无魔数文本、二进制伪装、非法 UTF-8/控制字符（含文件后部）、不匹配 MIME 拒绝，流位置/字节保留。所有失败必须是带错误码的 `BusinessException`（缺少扩展名、类型不允许、格式无法识别、内容不一致），异常过滤器映射 HTTP 400；超限返回 `FileSizeExceeded`，不得抛出 `InvalidOperationException` 变成 500。

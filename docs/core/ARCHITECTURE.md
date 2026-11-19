# 架构说明

本仓库是通用系统平台基座，由 SimFramework 裁剪而来，只保留与具体业务无关的通用能力：JWT 账号体系与 RBAC、多租户、审批引擎、组织架构、系统管理（模块、路由、权限、数据字典、系统配置、业务编号、定时任务、通知与文件存储）、首次初始化与审计。业务模块按约定的阶段与端口边界逐步接入；业务系统如何从本仓创建独立仓库并同步基座更新见 [`FRAMEWORK_SYNC.md`](FRAMEWORK_SYNC.md)。

## 本地开发基础设施

`api/env/compose.yaml` 提供独立的 `sim-dev` Compose 项目，仅包含 PostgreSQL、Redis、RabbitMQ。三项服务使用命名卷和健康检查，端口仅映射到宿主机回环地址；业务进程在宿主机运行。该入口独立于 `deploy/` 的 API/nginx 部署，连接与生命周期说明见 [本地开发基础设施](../ops/LOCAL_INFRASTRUCTURE.md)。

## 项目结构

```text
qms-upgrade/
├── api    # .NET 10 API 服务
├── web    # Vue 3 Web 应用
├── client # .NET 10 WPF Windows 客户端
├── tool   # 多设备接入 Agent（串口/TCP/文件 → 本机/中心 WebSocket，Windows 托盘）
├── deploy # 部署与运维
└── docs   # 项目级文档
```

`client/Sim.Admin.Wpf.Client` 是独立构建的 .NET 10 WPF Windows 客户端，通过 HTTP 直接连接现有 Web API，复用账号、双重验证、资料、通知、审批、授权、系统管理与用户偏好契约。Access Token 在进程内存中，过期后使用 Refresh Cookie 与防伪令牌单次刷新；桌面工作区提供账号安全、审批中心（列表、详情与同意/驳回/转交/加签/退回/撤回/重新提交/催办/终止）、授权管理、系统配置、字典项、定时任务、文件存储配置、通知渠道/模板/规则/Webhook 管理。同意/驳回等审批动作按待办权限判断并经用户确认。其四套风格及明暗配色对齐 Web 的 `web/src/config/themes.ts`。桌面 UI 基于自建的 Naive UI 复刻控件库：`Controls/Inputs`、`Display`、`Container`、`Overlay` 提供表单、展示、容器与浮层组件，样式集中在 `Theme/Controls.*.xaml` 并由 `Theme/CustomControls.xaml` 合并进 `App.xaml`；全站不使用任何投影，层级只由 2px 描边、圆角与语义色表达。组件外观通过 `--gallery`（可加 `--dark`）启动的预览窗口核对。流程定义草稿新建、筛选、原生画布节点新增/拖动、悬停显示且随节点移动的四边连接点、三种线型及节点/连线编辑、保存与服务端校验已对接同一 API；E2EE/通行密钥和外部登录绑定仍待原生 WPF 补齐。WPF 项目不加入面向跨平台后端构建的 `api/Sim.slnx`。

## 后端：api

后端基于 .NET 10、Sim 框架和 SqlSugar。解决方案入口是 `Sim.slnx`，运行入口是 `src/Sim.Admin.HttpApi.Host`。

主应用的 HTTP 入口由 Host 直接组合 Application 模块及约定式控制器；原本只有空模块声明的 `Sim.Admin.HttpApi` 和 `Sim.Admin.HttpApi.Client` 已移除。Web/WPF 客户端直接通过 HTTP 调用 Host。

ABP 依赖已收缩为 2 个 NuGet 包：`Volo.Abp.Core`（模块体系：`AbpModule`、模块生命周期）与 `Volo.Abp.Localization`（请求文化中间件、JSON 本地化资源）。原 ABP 提供的 DDD 基类、依赖注入约定、中间件和基础设施全部由 Sim 框架等价实现替代：

- 实体基类：`Entity`、`AuditedAggregateRoot` 及多租户变体（`Sim.Framework.Domain` / `Domain.Shared`）。可编辑业务实体统一使用审计基类，由 SqlSugar AOP 回填创建人、创建时间、最后修改人与最后修改时间；仅追加日志、关联记录和不可变快照保留创建审计或显式豁免。
- 审计展示：列表 DTO 返回 `CreationTime`、`CreatorId`、`LastModificationTime`、`LastModifierId`，管理端数据表统一渲染四项；人员列通过登录态批量接口 `GET /api/system/user/display-names` 解析昵称（空时回退账号名）与账号名（悬停气泡展示），避免逐行请求用户详情。
- 应用/领域服务：`ApplicationService`、`DomainService`、`CrudAppService`，服务接口继承 `IApplicationService`。
- 横切抽象：`ICurrentUser`、`ICurrentTenant`、`IObjectMapper`、`IRemoteStreamContent`。
- 业务异常：`BusinessException`、`ValidationException`、`AuthorizationException`。
- DTO 基类：`EntityDto`、`PagedResultDto` 等（`Sim.Framework.Application.Contracts`）。
- 常规控制器：自定义 `AppServiceConvention` 与 `AppServiceControllerFeatureProvider`（替代 ABP conventional controllers）。
- 依赖注入：使用标准 ASP.NET Core 注册，不再使用 `ITransientDependency` 等 ABP 自动注册约定。

主要层次：

- `src/Sim.Admin.*`：宿主应用和 Admin 主模块。
- `framework/Sim.Framework.*`：通用应用服务、配置、过滤器等基础能力。
- `framework/Sim.SqlSugar.Core`：SqlSugar 仓储和工作单元封装；`ISqlSugarClient` 使用按请求作用域的普通 `SqlSugarClient`，工作单元在提交/回滚及请求结束时兜底关闭事务连接。
- `modules/system`：用户、角色、权限、组织架构、租户管理、后台任务监控、菜单、登录、刷新令牌、模块配置、路由配置、动态路由、数据字典、系统配置、业务编号、文件存储、调度任务和通知能力（原 RBAC、tenant、worker 模块已合并到 System）。登录用户的前端偏好（语言、明暗模式、主题风格）保存于 `system_user_preference`（`UserId` 唯一索引），由 `GET/PUT /api/system/user-preference/my-preferences` 读取和覆盖式保存；`themeMode` 允许 `light/dark/system`，语言只接受 `zh-CN/en-US`，主题风格仅校验长度与字符集以便前端扩展。Web 端在会话恢复后加载一次，切换语言 / 明暗 / 主题色后防抖写回。
- `modules/openplatform`：第三方应用接入。管理员配置应用、授权范围和来源 IP；AppSecret 由 Data Protection 加密保存且仅创建或重置时返回。client_credentials 令牌仅携带授予的权限码，必须经过显式权限端点守卫；签发和携令牌调用分别审计。模块通过 `IModuleSeedContributor` 提供权限与菜单，Web 管理页在 `web/src/modules/openplatform`。
- `modules/approval`：审批定义与版本、审批运行时实例/任务/流转记录状态机、审批人解析、审批中心查询与实例监控；审批定义是有向图（节点 + 连线，支持排他网关与并行 fork/join），运行时以实例节点状态表（`Pending`/`Active`/`Completed`/`Skipped`）推进并处理分支剪枝与合流等待，节点条件不成立时按免审继续沿出边推进，网关未选分支才执行剪枝；业务类型可声明字段清单，发起时固化为带标签的字段快照（`ContextJson`）供详情页只读渲染，旧扁平上下文按回退规则照常读取；实例与任务附件只存文件存储标识与元数据（`ApprovalAttachment`），下载走鉴权代理端点而非签名直链。业务类型注册、启用用户最小目录与提交/查询均走 Contracts 端口；业务提交端口自身建立可嵌套 SqlSugar 工作单元，定义版本与运行中实例分别由非空 `TenantScope` 唯一索引保证并发唯一性。
当前 RBAC 已实现用户、角色和功能权限，并按 [`ORGANIZATION_CHART_DESIGN.md`](../design/organization/ORGANIZATION_CHART_DESIGN.md) 的第一阶段落地组织单位树、单位编码与成员归属，具有单一的稳定读取端口 `IOrganizationDirectory`。管理端已打通用户-角色与角色-权限分配：用户创建/更新 DTO 的 `roleIds` 覆盖式同步 `system_user_role`（`null` 表示保持现状），角色编辑通过 `POST /api/system/role/assign-role-permissions/{roleId}` 覆盖式同步权限；角色分配本身要求 `system:role:manage`（用户接口内以运行时授权校验，失败返回 403），仅持有 `system:user:update` 的操作者不能借此提权；权限目录读取允许 `system:permission:manage`、`system:role:manage` 或路由管理权限（`system:route:create/update/delete`）任一，权限写接口仍只接受 `system:permission:manage`；权限与模块没有数据关联，权限码前缀只用于展示分组。租户管理接口要求 `system:tenant:manage`。角色权限或用户角色发生变化时，服务端吊销受影响用户的 Refresh Token，JWT 绑定的登录会话校验会随后拒绝残留 Access Token（缓存 30 秒），因此变更加上重新登录后生效。内置身份以 `RoleEntity.IsBuiltIn` / `UserEntity.IsBuiltIn` 标记识别：内置 Admin 角色允许修改名称与描述、编码不可变，禁止分配模块/权限和删除；管理端创建用户不接收密码，创建与重置统一读取系统配置 `security.user-default-password`（缺失时回退 `App:AdminDefaultPassword`），首次种子生成随机初始值；`POST /api/system/user/{id}/reset-user-password` 要求 `system:user:reset-password`，重置后吊销目标用户全部 Refresh Token。内置账号（初始化时创建的管理员与系统账号 `system`）不允许修改账号名、变更角色、重置密码或删除，昵称、邮箱等资料仍可编辑；且不能移除最后一个启用管理员的 Admin 角色；授权旁路通过 JWT `IsBuiltInRole` claim 传递，不依赖角色编码；删除角色会级联清理角色权限、角色模块、用户角色与角色数据范围关联。模块与路由为多对多关联（`sim_system_module_route`），关联表是唯一事实来源：路由是全局目录，不持有归属模块，路由名全局唯一；模块编辑页用级联路由树维护关联，取消子路由时父级自动半选，父级半选也计入提交并由后端补齐祖先。路由管理列表返回全量路由（含未分配路由），模块列展示关联模块名，模块筛选同样走关联表；用户菜单由 `GET /api/system/route-config/user-accessible-routes` 从全局路由目录统一建树：先取角色可访问模块关联的路由，再沿全局 `ParentId` 补齐祖先，最后按 JWT 权限裁剪，因此路由改挂到其他模块的父级后仍会归入该父菜单，不会退化为根路由；模块编辑页和模块详情继续使用模块维度的路由接口。删除模块只解除关联，不删除路由；种子对已存在的路由也幂等补齐模块关联，升级存量库无需手动重建菜单。种子路由名称统一使用 PascalCase（如 `SystemManagement`、`AuthorizationManagement`、`UserCenterProfile`），并避开个人中心静态路由 `UserProfile`，重命名由幂等迁移在新库和已有库上保持一致。路由访问权限保存在 `RouteMeta.PermissionCode`（逗号分隔，`RouteMeta.PermissionRequireAll` 决定任一或全部满足，默认任一，空值表示不限制），种子通过 `RoutePermissionCatalog` 按路由名匹配页面读取权限并只回填空值（根路由取全部子页面权限并集，含个人页的根路由保持不限制），管理端从权限目录按模块分组下拉多选并支持搜索（选项展示名称与权限码，选中标签只显示名称），可逐路由切换匹配方式，后端在保存时校验权限码存在；后端在 `GetModuleRoutesAsync` / `GetUserAccessibleRoutesAsync` 下发前按 JWT 权限裁剪路由树，未授权路由不会到达浏览器；前端导航守卫、菜单过滤与按钮级 `v-permission` 读取同一字段作为防御层，后端 `[Permission]` 与运行时校验仍是唯一授权依据。数据范围过滤、资源/字段策略与权限解释仍待实施。后续扩展应遵循该设计：功能权限、数据范围、资源策略和字段策略分别建模，在后端统一汇合判定；组织负责人身份不隐式授予数据权限，前端权限预览也不作为授权凭证。审批流已按 [`APPROVAL_WORKFLOW_DESIGN.md`](../design/approval/APPROVAL_WORKFLOW_DESIGN.md) 落地为独立 approval 模块（自研轻量引擎）：审批只通过 `IOrganizationDirectory` 端口读取组织数据，不直接访问组织表，也不反向修改组织。

用户生命周期通过 System Contracts 的 `IUserCreatedHandler` / `IUserDeletedHandler` 端口扩展：创建处理器在用户行写入后执行，删除处理器在用户行删除前执行；System 自行清理外部登录绑定、刷新令牌、组织成员与负责人引用、存储配额和分享链接，任一处理器异常都会回滚整个删除。用户在账户安全页通过 `POST /api/system/user/delete-my-account` 输入当前密码即可自助注销，后端校验当前用户与密码后复用同一清理流程，成功后清除刷新令牌 Cookie 并由前端退出登录；内置账号仍受保护。业务模块不得直接删除 System 用户行，需要联动清理时必须实现该端口。

API 使用 Sim 自定义 conventional controllers（`AppServiceConvention`）自动暴露应用服务。Swagger 文档按 `admin`、`system`、`approval`、`openplatform` 四组拆分，并额外提供 `all` 聚合文档（`IncludeAll: true`）覆盖全部业务模块，供一次性导入 Apifox 等接口工具；配置位于 `src/Sim.Admin.HttpApi.Host/appsettings.json` 的 `Swagger:Documents`，`all` 排在首位作为 UI 默认文档。聚合文档排除 `/api/swagger-config` 等 Host 基础设施端点，避免工具里出现无法对外调用的宿主接口。自动发现依赖 Host 程序集的元数据引用链：新增业务模块时，在 `src/Sim.Admin.HttpApi.Host/Business/BusinessProjects.props` 添加 `ProjectReference`，并在 `Business/BusinessModuleRegistration.cs` 的 partial `AdminHttpApiHostModule` 上声明 `[DependsOn]`，否则 C# 编译器会省略未直接使用的 `AssemblyRef`，`CollectFrameworkAssemblies` 无法发现该模块并导致其 API 全部 404。`Business/` 是业务仓独占的组合根，基座升级不修改该目录；业务模块通过在该目录补齐自己的 Application/SqlSugar 模块后暴露 `/api/{module}/...`。

认证使用 JWT Bearer。登录、刷新令牌、撤销令牌以及 Cookie/CSRF 会话边界统一由 `IUserService` 和 `modules/system/Sim.System.Application/User/UserService.cs` 实现，并通过 Sim conventional controller 暴露，不额外维护手写 Controller。Access Token 仅由前端保存在页面内存，Refresh Token 使用 `HttpOnly`、`SameSite=Strict` Cookie；刷新和注销必须携带 `X-XSRF-TOKEN`。登录页“记住我”只持久化用户名用于预填，密码交给浏览器密码库自动填充，应用不保存密码或令牌；勾选状态决定 Refresh Token Cookie 是持久 Cookie 还是关闭浏览器即失效的会话 Cookie，令牌值以 `p:`/`s:` 前缀标明作用域并在轮换时继承。登录只接受 JSON，并依靠 CORS、SameSite Cookie 和登录限流防护，同时保持 Swagger UI 可直接调用。权限特性位于公共 `Sim.Framework.Application.Contracts`，System Application 提供 ASP.NET Core Authorization handler；管理员角色和 `*:*:*` 权限可通过全部权限检查，管理接口按用户、角色、权限、模块、路由与租户分别授权。JWT 默认使用 HMAC-SHA256，也可按密钥描述符配置 SM2 国密非对称签名（`JwtSettings:SigningKeys[*]:Algorithm=SM2WITHSM3`），与 `kid` 多密钥轮换共用同一验证链路；国密能力由 Framework 层 `Sim.Framework.Domain.Security` 的 `IGmCryptoService` 统一提供（SM2 签名/加解密、SM4 ECB/CBC、SM3）。

首次启动初始化由 Admin.Application 的 `SetupService` 承担，暴露 `GET /api/admin/setup/status` 与 `POST /api/admin/setup/initialize` 两个匿名端点（初始化接口带独立限流）。状态由 `system.setup.state` 配置项表达：`pending` 表示等待首次设置，`completed` 表示已完成；没有标记但已存在用户的历史站点按已完成处理，避免升级后被重新初始化。系统未完成初始化时，启动自动种子与 `--seed` 都会跳过；提交初始化后先创建或更新用户指定的管理员账号并重置密码，再创建不可登录的系统账号 `system`，由该账号作为审计归属人幂等执行基础种子（角色、模块、路由、权限、系统配置、组织与审批定义），种子数据的 `CreatorId` 记为系统账号、创建时间取当前时间；随后分配 Admin 角色并写入 `site.name`、`site.default-language`、`security.registration-enabled`、`security.login-max-failed-attempts`、`security.lockout-duration-minutes` 与 `ui.default-theme`。初始化期间前端通过匿名 `GET /api/admin/setup/progress` 读取完整步骤列表、当前阶段与百分比并逐项展示进度：步骤列表由准备、管理员、系统账号、种子执行器上报的 12 个步骤（租户、系统模块、角色、用户、模块、路由、权限、系统配置、审批编号、组织、管理员授权、普通用户基础权限）与收尾组成，种子执行器每个步骤开始前上报阶段，前端据此标记已完成/进行中。初始化完成后 `--seed` 仍可用于补齐升级新增的基础数据，执行时同样确保系统账号存在并以它作为审计归属人。登录锁定阈值与时长在登录时从这些配置项读取（缺失时回退 `SecurityOptions`），自助注册开关同时约束普通注册与第三方注册；自助注册成功后由 `security.registration-role-codes` 决定分配的角色（英文逗号分隔的角色代码，默认 `User`，只允许非内置角色，配置缺失或为空时回退 `User`），客户端提交的 `roleIds` 一律忽略；内置 `User` 角色由种子补齐 `approval` 模块。站点名称、默认语言与默认主题由客户端本地化接口下发给 Web，供首访与登录页使用；登录页进入时会重新读取该接口，使后台调整的开放注册开关无需整页刷新即可生效。未完成初始化的站点，Web 路由守卫会把所有页面重定向到 `/setup`。

System 还提供后端代理式外部登录，支持微信、QQ、Steam、Microsoft 与 Google，并以 `IExternalLoginProvider` 作为协议无关扩展点：微信/QQ 使用 OAuth 2.0，Steam 使用 OpenID 2.0，Microsoft/Google 使用标准 OIDC。Provider Registry 只暴露 `Enabled=true` 的实现；登录从 `GET /api/system/external-login/redirect/{providerId}` 发起，绑定从 `GET /api/system/external-login/my-binding-redirect/{providerId}` 发起。后端生成一次性 `state`（OIDC 另有 `nonce`），写入 Data Protection 加密、路径限定为 `/api/system/external-login` 的 HttpOnly Cookie；state 会话同时保存回调地址、过期时间和绑定意图。提供方统一回调到固定 `ExternalLogin:CallbackBaseUrl`（Web 端为 `/auth/external/callback`），前端从 state 前缀恢复 provider，再调用 `GET /api/system/external-login/callback/{providerId}`；该回调落点不渲染中间页，成功后直接进入后台默认页或账户安全页，失败时按发起页回到登录页或账户安全页并弹出错误消息。后端一次性消费 state/nonce，校验协议返回值和外部身份后按意图分支：登录意图走登录、未绑定身份的关联/注册选择或 2FA，绑定意图必须校验当前登录用户与 state 中用户一致并写入绑定。系统不保存提供方 access token；外部登录出站 HTTP 日志和 Trace 不导出 query，避免微信/QQ 协议要求放在 URL 中的凭据泄漏。绑定关系保存于 `system_external_login`，数据库只保存 `Provider + ProviderKey` 的 SHA-256 摘要且唯一，不依据 email 自动关联已有账号；未绑定身份必须先选择关联已有账号（校验本系统口令）或注册新账号（受注册开关约束），已绑定用户若开启本地 2FA，仍进入既有 challenge 流程，不能被外部登录绕过。该能力属于 System 用户身份体系；域账号（LDAP/AD）将作为同一 Provider 扩展点的直连实现纳入后续阶段，不与本地账号体系并列维护第二套用户表。生产 HTTPS 环境必须设置 `ExternalLogin:StateCookieSecure=true`，本地 HTTP 开发才允许临时为 `false`。完整流程与 Microsoft 登录/绑定时序见 [`EXTERNAL_LOGIN.md`](../ops/EXTERNAL_LOGIN.md)。

Swagger 文档面默认只在非生产环境注册并保持匿名：`/swagger` 前缀下的 UI 页面、静态资源与 `swagger.json` 可直接读取，页面的分组列表来自 `GET /api/swagger-config`。Production 只有显式设置 `Swagger:AllowInProduction=true` 才注册 Swagger；Docker Compose 为同域 nginx 部署开启该开关，nginx 对 `/swagger/` 与 `/api/swagger-config` 应用 Basic Auth，`.htpasswd` 由 `setup-secrets` 菜单生成且不入库。文档面不承担业务授权职责，业务接口 `/api/**` 仍只接受 JWT Bearer；Swagger UI 的 `Authorize` 按钮只是把令牌透传给被调用的接口，而不是进入文档页的门槛。

Refresh Token 的 `FamilyId` 同时是登录会话 ID。会话保存设备标识摘要、PC/Pad/Phone 类型、名称、登录时间和最近活动时间；名称由客户端提供（型号优先，其次平台，再拼接平台架构与浏览器指纹前六位），仅用于展示。登录时按 `Security:DeviceSessionLimits` 撤销同设备旧会话并淘汰该类型最久未活动的超额会话。JWT 写入 `SessionId` Claim，Host 的 JWT 校验阶段查询当前令牌家族是否仍有效，因此用户主动撤销、改密或设备数控制都能立即阻止后续受保护请求，而不必等待 Access Token 自然过期。最近活动写入按一分钟节流，在线状态窗口由 `Security:SessionTimeoutMinutes` 控制。

后端依赖方向由架构测试约束：framework 不反向引用业务模块，业务 Application 跨模块只引用 Contracts/Domain.Shared，业务 Domain 不引用其他业务模块，模块 SqlSugar 不引用 Host。Host 组合根只注册实际存在的模块，架构测试阻止已裁剪模块被重新引入。System Application 显式引用 Quartz 官方依赖注入包。文件 framework 也遵守端口/适配器边界：基础项目不引用云厂商或数据库实现，ObjectStorage 和 SqlSugar 项目分别在 Host 组合。

NuGet 使用官方 Central Package Management，包版本统一在 `api/Directory.Packages.props` 维护（其中 ABP 仅 `Volo.Abp.Core` 与 `Volo.Abp.Localization` 两个）。Host/测试入口按 Windows/Linux 分别提交锁文件，依赖变更同时执行锁定还原、直接/传递漏洞扫描和许可证策略测试；发布目录由 CI 生成并校验 SPDX SBOM。

密码使用 ASP.NET Core `PasswordHasher`（PBKDF2）保存，旧版 SM3 密码会在首次成功登录时自动升级。登录失败达到阈值后账号会临时锁定，阈值与时长优先读取初始化写入的系统配置，缺失时回退 `SecurityOptions`。Refresh Token 只保存 SHA-256 哈希，采用间隔轮换（`Security:RefreshTokenRotationMinutes`，默认 5 分钟，0 表示每次刷新都轮换）和令牌家族重放检测；间隔内的刷新只签发新 Access Token 且不更换 Cookie，避免页面导航中止在途请求（499）后，旧 Cookie 触发复用检测而误吊销整个会话族。用户昵称必填且全局唯一（去除首尾空白后比较，空白值无效），由服务端校验和数据库唯一索引双重保证；创建/更新/自助资料/外部注册统一校验，管理端输入时防抖查重，升级迁移会先归一去重，再为存量重复昵称追加数字后缀。个人改密、找回密码和双重验证仍由 `IUserService` 暴露：邮箱/SMS 使用短时、限次、一次性验证码挑战，数据库只保存 PBKDF2 哈希；身份验证器使用 RFC 6238 TOTP，密钥由 ASP.NET Core Data Protection 加密，兼容 Microsoft/Google Authenticator，并提供一次性恢复码。匿名找回请求对存在与不存在的账号返回相同结构，改密或找回成功撤销全部 Refresh Token。`[RateLimit]` 让端点读取配置默认配额或通过特性覆盖次数、窗口和业务分区，分区键为端点分区名加客户端身份（已认证请求取 JWT 自定义 `Id` 声明，匿名请求取客户端 IP）；登录/注册共用 `authentication` 分区（10 次/60 秒），首次初始化使用独立的 `system-setup` 分区（5 次/60 秒），刷新与吊销使用独立的 `token-refresh`/`token-revoke` 分区（各 30 次/60 秒），高频刷新不会挤占登录和注销配额。当前计数只在单实例内存中，多实例共享限流仍需评审。

文件能力以 `IFileStorage`、`IFileStorageProvider`、运行时配置 provider、模板/导出/转换/图片处理端口为核心。本地 provider 使用键规范化、根目录约束和原子移动；数据库 provider 将内容与元数据保存到 `framework_stored_file`；S3、腾讯 COS 和阿里 OSS 通过 ObjectStorage 适配项目接入。默认 provider、上传上限和各存储参数分别保存于 `system_file_storage_settings`、`system_file_storage_provider`，对象存储密钥使用 Data Protection 加密且不通过读取 DTO 回显。Scriban 负责严格变量模板渲染，表格可导出 CSV/JSON/HTML；LibreOffice 与 ImageMagick 是默认关闭、带参数隔离和超时的可选外部进程。生产恶意文件扫描、异步隔离 Worker、多实例共享限流等提升项记录在待确认评估中。

业务内容文件通过 `IContentFileService` 暴露 provider 无关的稳定文件 ID，`framework_file_metadata` 固化实际 provider、对象键、哈希、类型和创建人，默认存储切换不会改变既有链接。通用 `IResourceAccessService` 使用 `system_resource_access_policy`、`system_resource_file_reference`、`system_resource_share_link` 和 `system_resource_share_link_viewer` 表达资源所有者、公开/私有状态、关联文件、显式分享链接和已用设备摘要；该模型不依赖具体业务实体，业务模块以资源类型接入后即可复用所有者授权与分享链接语义。System 同时提供 `system_user_e2ee_identity` 身份端点：仅限当前用户调用，服务端不接收加密口令、UMK 或明文恢复密钥，只保存 KDF 参数与包裹结果；Web 端在浏览器内完成国密算法派生与包裹，设备缓存只保存不可导出的 AES-GCM CryptoKey。业务模块如启用端到端加密，复用该身份能力并自行扩展密文字段，服务端不得新增解密路径。

System Application 汇总 `IScheduleTaskDefinitionProvider` 登记的内置 Job，并统一管理 HTTP/HTTPS、Command/Program、Service Method 三类数据库动态 Job。Cron、启停、类型和加密执行参数保存到 `system_schedule_task_config`，Quartz Job/Trigger 随新建、更新、删除实时同步。HTTP 执行器拒绝本机/内网/保留地址与重定向，Command 只接收命令名称、不经 Shell 且受 `ScheduleTaskExecution:AllowedCommands` 白名单约束，Service Method 仅允许 `IScheduleTaskServiceMethodHandler` 显式登记项。内置 Job 可改 Cron/启停但不可删除；Quartz 持久化 JobStore 表名遵循全局 `sim_` 前缀约定（`sim_qrtz_*` / `sim_QRTZ_*`），由 `SystemApplicationModule` 在 `UsePersistentStore` 后将 `Consts.TablePrefix` 前置到 Quartz 默认表前缀实现。

环境配置以 `src/Sim.Admin.HttpApi.Host/appsettings.Example.json` 为模板（基于 development，数据库、Redis、RabbitMQ 默认连接本机 Compose，仅含公开开发凭据）提交到仓库；所有 `appsettings.{Environment}.json`（Development/Staging/Production）都不入库，启动时若缺失会基于模板自动生成本地文件，需按本机/部署环境覆盖数据库类型、连接串与日志级别等环境差异项。所有环境的 JWT 密钥都由外部提供；租户创建未提供管理员密码时生成随机初始密码并在创建结果中返回，不再回退固定默认密码；不再用于预置默认 `admin` 占位账号，首次管理员由 `/setup` 创建。初始化完成后执行种子，种子只补齐模块、路由、权限与基础配置，不创建示例业务数据。模板开启 Code First、关闭自动数据种子，仅供本地开发；Production/Staging 部署必须覆盖所有中间件连接和凭据，并关闭自动 Code First，使用受控迁移。

数据层使用 `SqlSugarCoreNoDrive`，具体数据库驱动显式集成。基线集成 PostgreSQL 的 `Npgsql` 与 Microsoft 官方 `Microsoft.Data.Sqlite.Core`，并使用操作系统 SQLite，避免把存在安全公告的 `e_sqlite3` 原生库传播到所有模块。切换其他数据库时，应在数据访问层显式添加对应厂商的官方 ADO.NET 驱动。`ISqlSugarClient` 不以 `SqlSugarScope` 形式按请求注册：Scope 依据调用栈在 AsyncLocal/ThreadLocal 间切换内部客户端，ASP.NET Core 请求内的事务与请求结束时的释放可能落到不同上下文，导致连接不归还连接池；改用普通 `SqlSugarClient` 后由 DI 在请求结束时释放，同时 `UnitOfWorkFilter` 在 finally 中兜底关闭连接。Host 将 `BackgroundServiceExceptionBehavior` 配置为 `Ignore`，Outbox、通知投递和文件转换等轮询服务在循环内捕获瞬时依赖异常，数据库抖动只影响当轮任务，不再终止整个 API 进程。PostgreSQL 连接串通过 `Maximum Pool Size=60` 限制单进程连接数，服务端 `max_connections` 按实例数预留余量（本机 Compose 为 200）。

对象映射使用框架抽象 `IObjectMapper`，各模块以编译期生成的 Mapperly 映射器实现分派（直接使用 `Riok.Mapperly`，不再经过 ABP 集成），也不依赖 AutoMapper。Swagger 配置代码只位于 HTTP Host，不向通用核心和领域模块传播 OpenAPI 依赖。

运行探针分为 `/health/live` 和 `/health/ready`：前者只检查进程，后者执行真实数据库连接检查；`/health-status` 保留为兼容入口，但生产边缘（1Panel）只把 `/health/live` 与 `/health/ready` 暴露到公网，聚合 `/health` 在边缘直接返回 404。官方 TestHost 集成测试以临时 SQLite 启动完整组合根，验证探针、Cookie/CSRF 和迁移建表。后端通过固定 .NET 10 SDK、数字 UID/GID `1000:1000` 的非 root 多阶段 Dockerfile（构建时只在基础镜像尚未提供 UID/GID `1000` 时创建对应条目）和 Jenkins 编译/测试/发布/容器阶段形成交付门禁；容器阶段额外使用只读根文件系统、临时写目录和权限收缩验证运行时约束。

后端日志统一进入 Serilog：`ILogger<T>` 的细粒度 `SourceContext` 先归并为 `ScheduleTask`、`System`、`RBAC`、`Framework` 或 `Admin`，控制台与文件采用 `[模块][级别][时间] 内容` 单行格式，消息和异常内部换行会转义。最低日志级别与类别覆盖由配置提供：`Logging:MinimumLevel:Default` 为全局级别（默认 `Information`，即 `INF`，不再按 Debug/Release 构建条件切换），`Logging:MinimumLevel:Override:<类别>` 按 `SourceContext` 前缀覆盖单个类别（Development/模板为 `Information`，Staging/Production 将 `Volo.Abp`、`Quartz` 覆盖为 `Warning`）。`Logging:Console:Enabled` 与 `Logging:File:Enabled` 独立控制输出，数据库中间件、数据种子、Quartz Job、ABP/Microsoft 框架及业务日志不再维护各自的控制台格式。`DataBase:EnableSqlLog` 统一控制 SqlSugar AOP 与 Quartz ADO.NET SQL 日志；Quartz 3.x 经其自带 LibLog 桥直接写 Serilog（绕过 M.E.Logging 过滤），Host 在 `UseSerilog` 中按该开关对 `Quartz.SQL`、`Quartz.Impl.AdoJobStore` 类别做 `MinimumLevel.Override`。

通用业务编号以 `IBusinessNumberGenerator` 作为 Domain Shared 端口，并在 `Sim.SqlSugar.Core` 使用 `framework_number_sequence` 保存序列；System 通过 `IBusinessNumberDefinitionStore` 向 Framework 提供 `system_business_number_definition` 中的业务场景规则，并由 `/api/system/business-number-definition` 与“系统管理 → 业务编号”治理。Framework 不反向依赖 System。业务键、当前租户和由日期占位符渲染出的范围共同形成唯一序列；创建竞争由唯一索引处理，递增使用 `LastValue` 条件更新，保证多进程/多实例只分配一次。规则格式只从数据库读取：`{business}` 输出业务键，`{date:<.NET 日期格式>}` 输出年月日等任意日期文本，`{sequence:<.NET 数字格式>}` 输出流水号，且必须恰好出现一次。日期渲染值会成为流水分段，因此 `yyyyMMdd`、`yyyyMM`、`yyyy` 分别产生按日、月、年重置的序列；无日期则连续递增。内置场景为 `approval.instance`（审批实例，种子维护），业务模块接入时在同一种子机制中登记自己的业务键。

System 的系统配置以 `SystemConfigurationItemEntity`（`system_configuration_item`）保存按租户隔离的分组参数；`/api/system/configuration-item` 由 `system:configuration:manage` 权限保护，支持分组过滤、新建、更新和删除。分组键与配置键在租户范围内唯一，创建后保持不可变，分组名、配置名、值、排序和启停可调整。`SystemConfigurationDataSeeder` 只幂等补充 host 租户默认参数，不覆盖管理员已有配置。

通用业务数据字典由 `data_dictionary` 与 `data_dictionary_item` 两张表承载，接口分别为 `/api/system/data-dictionary` 和 `/api/system/data-dictionary-item`，统一由 `system:data-dictionary:manage` 保护。字典大类保存唯一 `DictionaryKey`、显示名、`String/Number/Date` 值类型、值前缀、值后缀和启停状态；字典键创建后不可变，值类型仅当字典下没有任何数据项时可修改（列表返回 `ItemCount`，存在数据时后端拒绝变更），字典项在 `TenantScope + DictionaryId + ItemKey` 内唯一。后端按值类型校验并规范化值：字符串去除首尾空白，数字使用 Invariant `G29` 十进制文本并去除无意义尾随零，日期只接受 `yyyy-MM-dd`。Web 字典大类列表在桌面端使用表格、窄屏使用卡片；“查看数据”打开独立字典数据 Tab。值前缀/后缀只作为该 Tab 中字典项输入框的展示符号（例如货币、百分比或单位），不参与校验、不参与存储值，也不用于列表值展示。

通用资源分享记录以 `TokenVersion` 标识令牌格式。服务只接受当前版本并继续校验资源、摘要、过期时间、撤销状态和设备名额；迁移新增列后历史记录默认为版本 0，按 fail-closed 失效并由所有者重新签发，避免旧摘要记录绕过当前分享契约。

后端本地化由保留的 `Volo.Abp.Localization` 请求文化中间件和 `AdminResource` 统一承载，Domain Shared 显式依赖 `AbpLocalizationModule` 并生成嵌入文件清单。业务模块抛出稳定的 `Sim:*` 错误码；动态菜单保存 `Menu:*` key，站点名称来自 `Branding:Name`，维护开关和说明只由热加载的 `App_Data/maintenance.json` 提供。Host 维护中间件阻断业务 API/Hub，`ClientLocalizationService` 只向 Web 暴露菜单白名单和站点配置，`SiteStatusService` 提供匿名维护状态，错误仍随具体响应返回，不导出整份内部资源；客户端本地化响应声明 `Vary: Accept-Language` 与 `Cache-Control: no-store`，Web 请求同时携带文化查询参数，前端语言包按需加载失败时不切换语言并丢弃失败缓存。异常本身不写死中文；`ExceptionFilter` 按 `AbpExceptionLocalizationOptions` 的命名空间映射和异常 `Data` 占位符生成本地化消息，`zh-Hans.json` 与 `en.json` 根据 `Accept-Language` 生成面向用户的消息。

## Web 前端：web

Web 端基于 Vue 3、TypeScript、Vite、Pinia、Vue Router、Naive UI，当前模块包括管理端工作台、系统管理（含组织架构与租户管理）、通知、用户与账户安全、授权管理（用户/角色/权限/模块/路由）与审批流程，以及模块开发模板。审批流程设计器使用 Vue Flow；全站遵循 Paper Ocean 设计系统。

主要目录：

- `src/main.ts`：业务应用入口，只声明模块清单、菜单配置并调用 `createApp`。
- `src/framework`：可复用 Web 应用壳，导出 `createApp`、平台模块清单以及业务模块/语言包注册扩展点。
- `src/router`：公开路由、核心路由、模块路由挂载、登录守卫、动态路由加载。
- `src/config/modules.ts`：合并平台与业务模块；业务模块在 `src/config/businessModules.ts` 注册。
- `src/modules`：模块页面。框架平台模块包含 `home`、`account`、`systemManagement`、`notifications`、`authorization`、`approval`、`siteStatus` 和模板模块。
- `src/components`：布局、菜单、标签页、卡片、表格（`Table/DataTable` 统一列表表格与分页栏）、文件工作台和图片编辑等公共组件。
- `src/hooks`：标签页、菜单、主题、响应式等组合式逻辑。
- `src/utils/request.ts`：Axios 实例、认证头、401 刷新令牌和错误提示。
- `src/api/{group}`：由 OpenAPI 契约生成的 TypeScript 类型与 SDK 客户端（@hey-api/openapi-ts；group 为 admin/system/approval/openplatform）。
- `src/styles`：全局样式与主题变量。

框架库通过 `pnpm build:lib` 生成 `dist`（模块保留、类型声明与 `style.css`），业务应用以 npm 包方式引用。框架构建期只能扫描自身源码，业务仓的页面与语言包必须在 `createApp` 之前通过 `registerModuleComponents(import.meta.glob(...), basePath)` 和 `registerMessagePacks(...)` 注册；`DynamicRouteLoader` 仍按后端 `component` 路径从注册表取组件，并在导入前按前缀加载对应语言包。

管理端 `AdminLayout` 由顶栏、可折叠侧栏和标签页内容画布组成（Paper Ocean 设计无页脚）；≤768px 时侧栏切换为覆盖式抽屉（遮罩、内置关闭按钮、Esc 与路由切换自动收起），不再挤压内容区。登录页与工作台首页不改变认证状态机，只负责展示和交互。根主题 Store 同时驱动 Naive UI 覆盖、`--po-*` 设计令牌及 `--theme-*`/`--admin-*` 兼容语义令牌，使登录、导航和业务页面在 4 套风格 × 深浅主题与窄屏下保持一致。全站视觉基线采用 Paper Ocean 设计系统：浅色「纸上海洋」以奶油纸底 `#fff9ee` + 点阵纹理、海军蓝 `#14284b` 2px 描边、无模糊硬投影、电光蓝 `#2f6bff` 强调交互与珊瑚色 `#ff7a6b` 主行动；深色「夜航」把纸底换成深海蓝、文字换成米白，保持同样的描边与硬投影语言。Web 另提供玄青·鎏金、竹月·清雅、朱缃·典章 3 套商务风格，4 套风格 × 明暗的色值只定义在 `src/config/themes.ts` 注册表，商务结构差异由 `src/styles/business-skin.css` 在 `[data-skin='business']` 下承担，Naive 覆盖分别位于 `src/config/theme.ts`（纸上海洋）与 `src/config/businessTheme.ts`（商务）；令牌唯一定义在 `src/styles/theme-variables.css`，基础排版、`.po-*` 组件类与纸上海洋全站换肤在 `src/styles/paper-ocean.css`；页面不得写死颜色、圆角与阴影。设计原型、令牌与组件类定义见 [`design/ui/prototypes/design-system.html`](../design/ui/prototypes/design-system.html) 与 [`design/ui/prototypes/assets/ui.css`](../design/ui/prototypes/assets/ui.css)，页面落地规范见 [`design/ui/IMPLEMENTATION.md`](../design/ui/IMPLEMENTATION.md)，多主题方案见 [`design/ui/MULTI_THEME_DESIGN.md`](../design/ui/MULTI_THEME_DESIGN.md)。工作台首页由 `home` 模块承载，作为平台基座的管理端入口，展示问候、当前账号摘要和指向审批待办、流程定义、用户、角色、组织与个人资料的快捷入口；业务模块接入后再补充真实经营指标，底座本身不依赖任何业务接口。

路由分为三类布局：

- `admin`：挂载到 `AdminLayout`，适合后台管理页面。
- `custom`：挂载到自定义 `Layout`。
- `none`：顶级路由，不套布局，适合全屏页面。

静态模块由 `src/config/modules.ts` 合并平台模块与 `src/config/businessModules.ts` 中的业务模块；登录后还会通过 `DynamicRouteLoader` 从后端加载动态路由，用户菜单接口一次返回可访问模块与跨模块统一组装的全局路由树，顶层路由按子树中排序最前的可访问模块解析布局，不再逐模块拉取并合并路由。框架平台模块由 `src/framework/platformModules.ts` 汇总，业务应用只在其上追加自己的模块。菜单保持单一 `meta.order` 排序规则，静态 `admin` 模块注册时将顶层顺序规范为 `0`，子路由仍使用模块内声明的顺序。`admin` 布局模块的后端根路由必须使用相对路径，由 Router 统一生成 `/admin/*`；系统种子会幂等迁移历史 `/user`、`/system` 绝对根路径，并把 user 模块根路由、个人资料和设置路由标记为不生成菜单。模块种子幂等创建 `system-management`、`authorization-management` 和 `approval` 三个基础模块，路由种子按模块补齐系统管理（存储、通知、调度、业务编号、配置、数据字典、组织架构、租户管理）、授权管理（用户、角色、模块、路由、权限）与审批中心（待办/已办/我提交的/抄送我的、流程定义、实例监控）；历史授权菜单中的组织架构与租户管理会幂等迁移到系统管理并同步根菜单权限并集。审批模块通过后端 `IModuleSeedContributor` 自带模块、权限、路由与业务编号种子，Admin 平台种子不再直接引用审批模块。组件路径相对 Web `src/modules` 保存，对应页面位于 `web/src/modules/{systemManagement,authorization,approval}`；新增业务模块时同步补后端种子与业务应用侧的组件注册。头像下拉只保留个人信息和退出，不再生成失效的 `/user/settings` 入口。并发导航复用同一个动态路由加载 Promise，避免重复请求和重复注册；退出时移除会话动态路由、清空标签，并隔离迟到响应。动态组件路径统一相对 Web `src/modules` 保存，前端兼容历史种子路径，后端系统种子重复执行时会将旧路径迁移为当前规范。后台嵌套路由只在最内层缓存最终页面，避免两层 `KeepAlive` 重复传播激活事件。标签支持显式关闭按钮和右键菜单：关闭当前标签才导航到相邻页，关闭后台标签不会改变当前路由；右键菜单可关闭全部、左侧或右侧可关闭标签，批量操作始终保留固定标签，若当前标签被关闭则导航到右键目标或最近的保留标签。固定标签不可关闭并始终位于可关闭标签左侧。外链型菜单项由 `CustomMenu` 按路由名从 `src/config/externalMenuLinks.ts` 的解析表查找目标（动态路由 meta 契约无法携带函数），同步打开空白标签保留点击手势后再写入解析出的地址；当前解析表为空，业务接入外链时再登记。

Router 组合、纯认证决策和导航副作用分别位于 `src/router/index.ts`、`src/router/guards/authNavigation.ts` 和 `navigationGuards.ts`。受保护路由和 `/login` 都会先由 User Store 尝试恢复 Cookie 会话：登录页恢复成功后直接跳转后台，失败才允许显示登录表单；普通匿名页面默认不触发恢复，但硬刷新后若本地仍有持久化用户身份，会先恢复 Cookie 会话再渲染认证相关控件。并发 401 统一复用 User Store 的单个刷新 Promise；轮换间隔内的刷新不更换 Cookie，进一步降低并发轮换风险。同一原请求最多刷新重试一次，二次 401 清理完整会话；刷新遇暂时性限流（429）会按 Retry-After 等待后重试一次，而不是直接清理会话。请求取消键由方法、URL、查询参数和请求体共同组成，并使用控制器身份校验避免旧请求清理新请求。缓存列表页通过统一生命周期工具避免首次显示时 `onMounted` 与 `onActivated` 重复加载。

Web 文件能力分为两类：`FileWorkbench` 通过 System 模块文件接口处理上传和 Office-to-PDF，共享头像弹窗 `AvatarEditor` 在浏览器 Canvas 内完成单图 1:1 裁剪（选区内拖动、滚轮或双指缩放）并统一输出 WebP。系统个人信息将头像置于所有字段之前，点击后打开该弹窗。`systemManagement` 的存储配置使用单一共用 Form、提供器导航和单编辑区，编辑区可对当前表单值发起连接测试（后端 `FileStorageConnectionTester` 按 provider 执行本地目录写入、数据库查询或对象存储列桶）；调度页面提供四类 Job 的列表、新建、删除、Cron 和启停管理。

Web Jenkins 流水线使用官方 Playwright 镜像，依次执行锁定依赖安装、OpenAPI 类型漂移、生产依赖漏洞/许可证、TypeScript 类型检查、零警告 ESLint、单元测试、生产构建、包体预算和 Chromium 登录 E2E。生产构建不加载 DevTools，按 Vue、UI、内容工具分包；动态图标使用显式按需注册表。

Web 本地化运行时位于 `src/i18n/index.ts`，自有静态文案分组位于 `src/i18n/messages`，后端资源加载位于 `src/i18n/backend.ts`。前端只维护组件、页面、客户端 HTTP/网络状态；后端拥有业务错误、菜单和站点配置。应用启动时按两种文化加载后端资源，站点名通过响应式 branding 状态传播；维护模式由共享站点状态 store 收敛：页面刷新后读取一次状态接口，之后通过匿名 SignalR `/hubs/site-status` 推送变更；业务请求的 503 响应由统一拦截器写回状态并转换为维护页。路由 `meta.title` 保存 `Menu:*` key，侧栏、Header、标签页和文档标题直接调用 `t()` 解析；前端不再维护历史标题别名表，历史文字标题由种子重复执行时永久迁移为 key。`core`、`system`、`account` 中文语言包随入口注册，英文整包按需加载；`approval`、`canvas`、`organization`、`authz`、`notifications`、`e2ee` 由 `lazyPacks.ts` 登记为按需语言包，`DynamicRouteLoader` 在导入路由组件前按组件路径前缀注册对应语言包，规则未命中时静默回退为原始 key。前端自有静态 key 与后端统一为 ABP 风格「冒号分隔 + 大驼峰」（`Common:Search`、`Approval:Todo:Title`、`Error:404`）；运行时 `t()` 按该格式解析，动态拼接键由 `messageKey(...parts)` 生成，一致性测试以 canonical 正则阻止点号键回流。

## 前后端协作关系

- Web 端接口基地址由 `VITE_API_URL` 配置，本地开发保持为空并通过 Vite 把同源 `/api` 代理到后端。
- 请求层从页面内存读取 Access Token 并附加 `Authorization: Bearer <token>`，不再把令牌写入 `localStorage`。
- Access Token 过期后，前端携带 HttpOnly Cookie 和 CSRF 请求头调用 `/api/system/user/refresh-token`；明确认证失败时清理内存会话并跳转登录页，暂时性限流（429）先按 Retry-After 等待重试。
- 后端模块 Swagger 分组和前端业务模块并非一一绑定，但建议新增业务能力时保持命名一致，方便定位。
- Web 将界面语言映射为后端支持的 `zh-Hans` 或 `en` 文化；后端返回的 `error.code` 是稳定契约，`error.message` 是当前请求文化下的展示文本。
- 系统模块提供模块/路由配置能力，Web 端登录后可加载动态路由。

企业级质量属性、当前能力缺口和分阶段治理顺序记录在 [`ENTERPRISE_SYSTEM_GUIDE.md`](ENTERPRISE_SYSTEM_GUIDE.md)。架构或模块边界变化时，应同时更新本文档与对应分册。

可编辑工程图位于 [`diagrams/simframework-software-engineering.drawio`](../diagrams/simframework-software-engineering.drawio)，其中涉及已裁剪模块的页面需按当前底座重新维护。后端/Web 的维护入口分别见 [`../../api/docs/ARCHITECTURE.md`](../../api/docs/ARCHITECTURE.md)、[`../../api/docs/DEVELOPMENT.md`](../../api/docs/DEVELOPMENT.md) 与 [`../../web/docs/ARCHITECTURE.md`](../../web/docs/ARCHITECTURE.md)、[`../../web/docs/DEVELOPMENT.md`](../../web/docs/DEVELOPMENT.md)。

## 通知平台边界

通知实现遵守现有解决方案分层，优先复用现有项目；只有部署裁剪、依赖隔离或独立发布边界出现明确需要时才拆分适配项目：

```text
业务 Application
  -> Sim.System.Application.Contracts.Notifications
  -> Framework IMessageBus -> SqlSugar Outbox

Sim.Framework.Domain.Shared
  -> Messaging / Caching 稳定抽象
Sim.Framework.Application
  -> Local / RabbitMQ.Client / Confluent.Kafka / Apache RocketMQ Transport
  -> Local / StackExchange.Redis / Hybrid Cache
Sim.SqlSugar.Core
  -> framework_message_outbox / framework_message_inbox

Sim.System.Domain.Shared
  -> 通知渠道 SPI 与常量
Sim.System.Domain
  -> 配置、模板、规则、Webhook、Delivery、站内信实体
Sim.System.Application
  -> 规划、渲染、重试、配置保护与全部实际 Provider

Sim.System.Application.Contracts
  -> INotificationAccountDirectory 稳定账号解析端口
Sim.System.Application
  -> 账号目录实现（System 不引用 RBAC）
```

普通通知是“至少一次消息传输 + Inbox/Delivery 幂等”，不承诺外部 Provider exactly-once。一个事件匹配多个 Webhook Site 时，每个 Site 创建独立 Delivery；`Unknown` 不自动切换 Provider。安全验证码不进入 Outbox 和长期 Delivery。部署级 MQ/Cache 配置不进入后台页面；后台维护的记录统一称为“通知配置”，其中 Provider 只是供应商实现选项，Secret 不回显。

RabbitMQ、Kafka 和 RocketMQ 均使用成熟原生客户端；Kafka 采用手动 Offset 提交与失败转移 Topic，RocketMQ 使用 5.x gRPC SDK 的 SimpleConsumer 并在处理成功后 ACK。Redis 使用共享 ConnectionMultiplexer，业务模块只依赖 Framework 抽象。SMTP 使用 MailKit，腾讯云/阿里云短信与语音使用官方 SDK。浏览器提醒当前采用“站内信持久化 + 鉴权 SignalR 用户失效事件 + API 权威读取 + 焦点感知提示”：页面可见且聚焦时展示站内通知卡片，页面隐藏或失焦时仅在用户显式授权后调用 Notification API，避免同一消息双重提示。SignalR 断线自动重连；通知 API 仅在收到实时变更事件或展开面板时读取。System Contracts 只定义实时发布端口，Host 组合 SignalR 适配器；落库和已读更新先成功，实时发送失败不回滚。Service Worker 后台 Web Push 仍未实现；多实例 SignalR backplane 由 `Framework:SignalR:UseRedisBackplane` 控制，Development 默认关闭、其他环境默认开启，启用后必须验证 Redis 连接与跨实例路由。

Web 管理端以“通知配置”展示记录，`providerSchemas.ts` 只负责把稳定的 Settings/Secrets 字典契约投影为所选 Provider 专属的分组表单、默认值和帮助提示，System 后端不依赖前端表单 Schema。高级 JSON 是兼容未知扩展字段的折叠入口；结构化字段覆盖同名 JSON 字段。Secret 更新采用按键合并，空输入表示保留，只有显式清空操作会删除全部凭据。

通知收件人允许显式 `Address` 或系统账号 `UserId`。显式地址优先；未提供地址时，System 通过 `INotificationAccountDirectory` 按渠道解析启用账号的邮箱、手机号或用户 ID。账号原始联系方式不通过管理 API 回传，账号搜索只返回掩码；同一解析链同时用于异步普通通知和 Provider 测试，避免两套目标选择规则漂移。

## 系统 E2EE 约束

System 的 E2EE 身份能力只解决“用户主密钥如何被安全包裹与解锁”，不包含服务端解密：`system_user_e2ee_identity` 保存 KDF 参数、口令包裹与恢复密钥包裹，接口仅限当前用户，不接收口令、UMK 或明文恢复密钥。加密口令与离线恢复密钥由浏览器生成和保存，服务端只校验身份归属与包裹结果。

- 密钥材料不得写入明文业务字段、日志、指标标签、请求 URL 或错误消息；业务模块不得新增服务端解密或明文回填路径。
- 本机受信任设备缓存使用原生 Web Crypto 不可导出 AES-GCM CryptoKey 直接保护 UMK，禁止退回原始设备密钥字节存储；该能力不宣称硬件绑定，也不防同源脚本调用解密。
- 通行密钥只辅助解锁，不把 WebAuthn 验证成功等同于直接读取缓存；必须通过 PRF 输出派生包裹密钥，PRF 输出不得上传或持久化。服务器使用成熟库验证挑战/Origin/RP/UV/签名，挑战数据库单次消费且失败也失效；存在本机缓存时优先静默解锁，通行密钥用于没有缓存时的显式解锁。

# AI 编码规范

本文档约定 AI 编码助手在通用系统平台基座中修改代码时应遵守的规则。目标是让 AI 生成的改动可读、可验证、贴合现有架构，并且不会遗漏文档同步。

## 基本原则

- 先读代码，再写代码。修改前应理解相关模块、现有命名、目录结构和调用链。

- 优先沿用项目现有模式，不为单次需求引入新的架构风格。

- 改动范围要小，避免顺手重构无关代码。

- 编辑文本文件时保持统一行尾：普通文本使用 CRLF；`*.sh`、`Dockerfile` 和 `Jenkinsfile` 使用 LF，禁止同一文件混合 CRLF/LF。

- 不覆盖用户已有改动，不执行破坏性 Git 操作。

- 代码和文档都应反映当前真实状态。

## 修改前检查

AI 开始实现前，应先确认：

- 需求影响的是后端、Web，还是项目级文档。

- 是否已有相同功能、工具函数、组件、服务或模块模板。

- 是否涉及接口、认证、路由、数据库、环境变量、构建或部署。

- 是否需要同步更新 README 或 `docs` 文档。

- 是否改变 `docs/core/ENTERPRISE_SYSTEM_GUIDE.md` 中的企业级能力、当前基线、风险或完成定义。

常见入口：

- 后端入口：`api/src/Sim.Admin.HttpApi.Host`

- 后端业务模块：`api/modules`

- Web 模块注册：`web/src/config/businessModules.ts`（业务模块）；平台模块在 `web/src/framework/platformModules.ts`

- Web 路由：`web/src/router`

- Web 请求封装：`web/src/utils/request.ts`
- WPF 客户端请求与主题：`client/Sim.Admin.Wpf.Client/Services/ApiClient.cs`、`client/Sim.Admin.Wpf.Client/Theme/ThemeManager.cs`

## 后端编码规范

- 按现有分层组织代码：`Domain.Shared`、`Domain`、`Application.Contracts`、`Application`、`SqlSugar`。
- System 模块各层内部按功能目录组织：`Application/<Feature>`、`Domain/<Feature>`、`SqlSugar/Repositories/<Feature>`，命名空间跟随文件夹；新增类型不要平铺在 `Services`、`Entities`、`IRepositories` 等聚合目录。

- 应用服务优先复用 `CrudAppService` 等已有基础服务。

- 仓储访问优先复用现有仓储接口和 SqlSugar 封装。

- 涉及多个写操作时，按项目现有方式使用工作单元。修改事务边界时必须确认连接随请求释放：`ISqlSugarClient` 使用按请求作用域的普通 `SqlSugarClient`，不要改回按请求注册的 `SqlSugarScope`；事务提交/回滚失败与请求结束都由工作单元兜底关闭连接。批量写接口应限制单批数量并让每批共享一个 HTTP 请求，避免大批量导入退化为数百次串行事务请求。

- 后台轮询服务（Outbox、通知投递、文件转换等）必须在循环内捕获瞬时依赖异常并记录日志，不能让其逃逸终止 `ExecuteAsync`；Host 级 `BackgroundServiceExceptionBehavior` 固定为 `Ignore`，数据库抖动不得导致整个 API 进程退出。

- 认证、权限、菜单、角色相关逻辑应优先参考 `system` 模块。新增外部登录 Provider 必须实现 `IExternalLoginProvider`、挂到 Provider Registry，并从 `ExternalLogin:*` 配置读取 `Enabled`；协议细节留在该 Provider 内，不在登录服务中写死协议分支，也不保存提供方 access token。

- 权限控制使用 `PermissionAttribute` 和 ASP.NET Core Authorization handler；新增管理写接口必须定义明确权限码，不能只依赖 `[Authorize]`。

- 对象映射使用 ABP Mapperly 集成；新增映射应添加编译期 Mapperly mapper，不要重新引入 AutoMapper。

- 数据库驱动按实际使用显式引用官方 ADO.NET 包；不要用聚合驱动包把未使用的原生库传递到所有模块。

- 可编辑业务实体必须继承审计基类，数据表统一具备创建人、创建时间、修改人、修改时间；列表 DTO 必须输出四项字段，管理端列表统一展示并批量解析昵称（空时回退账号名）。仅追加日志、关联表和不可变快照可只保留创建审计，不得为凑字段伪造修改人语义。

- SqlSugar 查询表达式（`GetAsync`、`Where`、`Any` 等）内不得使用 `Nullable<T>.HasValue` / `Value` 的短路判断（如 `!excludedId.HasValue || entity.Id != excludedId.Value`）。当前 `SqlSugarCoreNoDrive` 版本无法翻译这类表达式，会生成 `NOT(  IS NOT NULL )` 等非法 SQL 并触发数据库语法错误（PostgreSQL `42601`）。应先在 C# 层判断可空参数，再分别构造只含普通比较的查询表达式。

- 后端 NuGet 版本统一维护在 `api/Directory.Packages.props`；项目文件只添加无版本 `PackageReference`，不得使用 `Version` 或 `VersionOverride` 绕过中央管理。

- 新增或升级后端依赖时同步更新 Windows/Linux 锁文件和许可证策略；非 Web 项目不得通过公共 props 隐式扩大 ASP.NET Core Shared Framework 依赖。接口变更必须重生成 OpenAPI 当前契约并运行兼容检查，不能为通过测试擅自覆盖兼容基线。

- 修改认证 Cookie/CSRF、数据库连接、迁移或健康探针时，优先扩展启动完整 Host 的集成测试，不能只以孤立单元测试代替运行时契约验证。

- 后端日志统一使用 `ILogger<T>` 或 Host 的 Serilog 上下文，不得自行注册 `AddConsole` 或直接使用 `Console.Write`/`Debug.Write`；新增日志不得记录密码、Token、Cookie、完整连接串或 SQL 参数值。

- 日志模块标签只允许归并到 `ScheduleTask`、`System`、`RBAC`、`Framework`、`Admin`；不要把命名空间、类或数据库子系统直接变成新的顶层标签。

- 新增模块需要同步检查 Host 的 `Business/BusinessProjects.props` 与 `Business/BusinessModuleRegistration.cs`、Swagger 分组配置，以及 Web 的 `businessModules.ts`。

- 文件存储、模板、导出和媒体处理属于 framework 端口能力：业务模块只依赖稳定接口，云厂商 SDK、SqlSugar 实体和本地路径逻辑留在独立适配项目，并由 Host 组合。

- 文件键必须规范化并阻止绝对路径/路径穿越；上传必须限制大小，模板限制执行复杂度，外部进程禁用 shell 拼接并设置超时、独立临时目录和失败清理。启用新的文件类型或命令前必须重新做威胁评估。

- 业务正文引用文件时保存 provider 无关的内容文件 ID/稳定 URL，并通过 `IResourceAccessService` 同步资源所有者、公开/私有策略和关联文件；不得把对象键、短期预签名 URL 或分享原始令牌持久化到业务正文/日志。私有资源必须同时保护主资源和所有托管文件；无令牌或令牌无效/过期/吊销时新私有资源默认 fail-closed，并记录授权/拒绝审计。系统 E2EE 身份解锁不得在日志、错误上报或审计中记录口令、UMK、包裹密钥或恢复密钥；前端国密实现随 `web/src/e2ee` 懒加载，不引入第三方加密库或把密钥写入 localStorage。

- 存储 provider 的运行配置必须通过 System 模块数据库接口维护；Secret 使用 Data Protection 加密且不回显。头像必须经过客户端裁剪/压缩和服务端大小/魔数校验，不接受用户输入的远程 URL。

- Worker 内置任务由业务模块声明定义，System 模块管理 HTTP、Command、Service Method 动态 Job 与所有 Quartz Trigger。不得开放任意反射调用、Shell 命令拼接或内网 HTTP 目标；执行参数必须加密且不进入审计请求体。Job 必须可重入或显式禁止并发。

- `[RateLimit]` 无参数时使用配置默认值，显式参数只用于已确认的业务配额；分区名必须稳定。当前内存限流不能宣称为多实例全局限流。

- 用户可见业务异常使用稳定 `Sim:*` 错误码，只能在对应 `Domain.Shared` 的 `<Module>ErrorCodes.cs` 单点声明，文件名、类名和 `Sim:<Module>:` 前缀必须与项目模块一致；调用方只引用常量并同步维护 `zh-Hans`/`en` 资源，不得在业务代码中硬编码错误码，也不得在 `BusinessException` 中直接写死单一语言消息。

- 不在文档或代码中写入生产密钥、真实连接串、真实账号密码。

## Web 编码规范

- 新页面优先放在对应 `src/modules/<module>` 下。

- 新模块必须提供 `index.ts` 和 `routes.ts`，并在 `src/config/businessModules.ts` 注册（平台模块除外）。

- API 调用优先放在模块内 `api` 目录，复用 `src/utils/request.ts`。

- 状态管理使用 Pinia，持久化 key 统一参考 `src/stores/persistentKeys.ts`。

- UI 优先沿用现有布局、组件和 Naive UI 风格；全站视觉基线为 Paper Ocean 设计系统，新代码只消费 `--po-*` 设计令牌与 `.po-*` 组件类，规范与页面范式见 `docs/design/ui/IMPLEMENTATION.md`。

- 表单中的实际必填字段必须显示必填标记；Web 通过 `Form.asteriskColor` 统一提供标记颜色（当前为珊瑚深色），不要在业务页面局部改写标记颜色。

- 表单校验时机：必填、长度和格式失焦时提示并在提交时全量复验；唯一性输入时防抖查重（400ms，只发送最后一次请求）；跨字段一致性在依赖字段变化时复验；文件上传、复杂条件配置、审批画布等提交时校验。不得只显示必填星号而无实际规则，条件字段切换后必须恢复校验状态。唯一性前端校验只用于即时反馈，后端唯一约束与提交校验仍是最终判定。提交校验失败时必须调用 `scrollToFirstFormError`（`src/utils/formErrors.ts`）把第一个错误字段滚入视野并聚焦，字段区内部滚动的表单同样适用。

- 路由元信息应明确 `title`、`menu`、`tab`、`keepAlive`、`icon`、`order` 等实际需要的字段。

- 挂载到 `AdminLayout` 的静态和后端动态模块根路由必须使用相对路径，禁止用 `/module` 绝对路径绕开 `/admin` 菜单范围；默认跳转应指向最终 `/admin/*` 地址。

- 管理端菜单只使用 `meta.order` 排序；静态模块顶层顺序由注册器统一设为 `0`，不要在菜单组件中增加另一套来源优先级。隐藏模块菜单时应同时处理可见子路由，避免父级隐藏后子级被提升到顶层。

- 修改登录、令牌、401、动态路由时，必须同步检查路由守卫和请求拦截器。

- 前端可取消请求必须使用包含方法、URL、参数和请求体的稳定标识；清理旧请求时校验控制器身份。401 原请求最多刷新重试一次，并为取消竞争和重试上限补充回归测试。

- 服务器错误由统一请求拦截器展示时，业务 `catch` 不得再次调用 `message.error` 追加通用提示；应使用 `@/utils/errorHandler` 的 `handleError`，它通过 `isRequestErrorDisplayed` 识别拦截器已展示的错误并自动去重，避免同一次失败弹出两条提示。需要保留自定义业务文案时，必须在请求配置显式设置 `showError: false` 后自行提示一次。`catch` 只负责取消判断、日志、状态回滚、重试或返回失败。批量操作可展示汇总结果，但不得重复单项错误。

- 后端契约变化时重生成 `web/src/api/definition` 并运行 `pnpm generate:api-types`，不手改 `src/api/{group}`。

- 新增动态图标必须加入显式注册表，不得 Namespace Import 整套图标库；生产构建需通过包体预算。

- 样式只使用 `src/styles/theme-variables.css` 的 `--po-*` 设计令牌与 `src/styles/paper-ocean.css` 的 `.po-*` 组件类，禁止在页面里硬编码颜色、圆角与模糊阴影（Paper Ocean 只允许硬投影）；需要新的全局类时加入 `paper-ocean.css` 并同步 `docs/design/ui/IMPLEMENTATION.md`。

- 新增前端静态文案必须进入 `src/i18n/messages` 对应领域的中英文资源并通过 `t()` 使用，key 统一为与后端一致的「冒号分隔 + 大驼峰」格式（`Domain:Segment[:Segment]`，如 `Common:Search`、`Authz:PermissionGroupSystem`）；动态拼接键必须经由 `messageKey(...parts)` 生成，禁止 `domain.camelCase` 点号键。入口包与按需包的归属：`core`、`system`、`account` 随入口注册，`approval`、`organization`、`authz`、`notifications`、`e2ee` 为按需包；新增按需包必须同时登记 `src/i18n/lazyPacks.ts` 的 `packLoaders` 与组件路径前缀，并同步 `lazyPacks.test.ts` 期望。后端错误、菜单和站点配置只进入后端资源及客户端本地化白名单，前端不得复制 `Sim:*` 或 `Menu:*` 翻译。请求语言由统一请求层发送。

- 公共文件/图片组件必须兼顾桌面和手机 Web，保持键盘焦点、语义标签、至少 44px 主要触控目标、加载/失败状态和大文件内存边界；业务图片使用 `ManagedContentImage`/`LazyImage` 的懒加载与认证 Blob 读取，不直接把受保护文件 URL 暴露到公网。网络 DTO 优先使用 OpenAPI 生成类型。

## WPF 客户端编码规范

新增接口或调整视觉时，先核对 Web API 契约与 `web/src/config/themes.ts` 四套风格的明暗配色，再同步 `client/Sim.Admin.Wpf.Client/README.md` 和相关项目级文档。客户端会话凭据只留在进程内存，不跳过 TLS 证书验证。

- 优先复用 `Controls` 下的按钮、卡片、表格、表单和输入弹窗；页面内不要复制一套视觉或交互。
- 组件视觉统一走 Naive UI 复刻控件族：`Controls/Inputs`、`Display`、`Container`、`Overlay` 提供对应组件，样式只写在 `Theme/Controls.*.xaml` 并由 `Theme/CustomControls.xaml` 合并进 `App.xaml`。新增控件类后必须补同族样式，禁止在页面里内联重写模板。
- WPF 端不使用任何投影：禁止 `Effect`/`DropShadowEffect`，禁止用偏移的第二层 `Border` 模拟硬投影；层级只由 2px `EdgeBrush` 描边、圆角和语义色表达。
- 颜色必须使用 `DynamicResource` 语义键（`InkBrush`/`MutedBrush`/`EdgeBrush`/`PrimaryBrush`/`ActionBrush`/`SuccessBrush`/`WarningBrush`/`DangerBrush` 及各 `*SoftBrush`/`HoverBrush`/`OverlayBrush`），尺寸使用 `CustomControlSize` 三档（30/36/42，圆角 8/10/12）。
- 不新增全局隐式 `TabItem`、`Button` 之外的基础控件样式之外的全站隐式样式；容器类控件（如 `CustomTabs`）用 `ItemContainerStyle` 局部引用 keyed 样式，避免影响既有页面。
- 组件外观与交互通过 `dotnet run --project client/Sim.Admin.Wpf.Client/Sim.Admin.Wpf.Client.csproj -- --gallery`（可加 `--dark`）在组件库窗口核对，再落地到业务页面。
- 通用管理页由 `ManagementCatalog` 的字段、列表路径、`CreatePath`/`UpdatePath`/`DeletePath`、HTTP 方法和行级动作描述；非标准 REST 契约在目录中表达，不在页面里写模块特例。
- 多步骤安全操作使用 `CustomPromptDialog` 或专用弹窗；密码、恢复码和 Access Token 不写日志、不落盘。
- 所有管理页在 `SessionExpiredException` 时调用统一的 `sessionExpired` 回调退出登录，不各自弹登录框。
- 状态、动作和日期使用共享转换器/格式化，不在每个列或页面上重复硬编码中文。

## 文档同步规范

只要代码变更影响使用、配置、扩展、测试或部署，就必须同步文档。

优先更新：

- 根目录 `README.md`

- `docs/core/ARCHITECTURE.md`

- `docs/core/DEVELOPMENT.md`

- `docs/core/DOCUMENTATION.md`

- `docs/core/ENTERPRISE_SYSTEM_GUIDE.md`

- 对应子项目 `README.md`

每次功能增强或缺陷修复都必须检查企业级指南。需要更新时与代码同批修改；无需更新时在最终说明中给出简短原因。

新增模块时至少说明：

- 模块用途

- 入口文件

- 注册位置

- 主要路由或接口

- 验证方式

## 验证规范

后端改动优先运行：

```bash
cd api
dotnet build Sim.slnx
```

Web 改动优先运行：

```bash
cd web
pnpm type-check
pnpm lint:check
```

如果因为环境缺少依赖、网络受限或外部服务不可用导致无法验证，应在最终说明中明确写出未验证项和原因。

## 输出规范

AI 完成修改后，最终说明应包含：

- 改了哪些文件。

- 解决了什么问题。

- 做了哪些验证。

- 哪些验证没有运行以及原因。

- 如果同步了文档，列出文档文件。

- 企业级审查只可输出建议和证据；用户要求“优化前询问”时，未被当前需求明确授权的优化不得直接实施，必须先确认范围、迁移和验收标准。

## 禁止事项

- 不要执行 `git reset --hard`、强制 checkout 或删除用户改动，除非用户明确要求。

- 不要为了通过检查而删除测试、绕过类型错误或关闭关键规则。

- 不要引入与项目技术栈无关的大型依赖，除非需求明确且已有理由。

- 不要把临时调试日志长期留在业务代码中。

- 不要生成与当前代码不一致的说明文档。

## 通知、消息与缓存约束

- Messaging/Caching 的抽象放在 `Sim.Framework.Domain.Shared`，运行实现放在 `Sim.Framework.Application`，Outbox/Inbox 持久化放在 `Sim.SqlSugar.Core`。

- 通知契约、领域模型、Provider SPI 和实际 Provider 优先沿用 System 的 Contracts、Domain.Shared、Domain、Application。不得仅为了目录对称创建空项目；当厂商依赖隔离、部署裁剪、独立发布或多实现组合确有收益时，可以新增职责单一的适配项目，并同步解决方案、Host 组合根和架构文档。

- 业务模块只能发布语义事件或调用安全通知端口，不能引用具体 Provider、MQ、Redis 或外部 URL。

- Secret 必须加密保存且不回显；验证码不得进入 Outbox、Delivery 正文、日志或指标标签。

- 后台记录统一称为“通知配置（Notification Configuration）”，Provider 仅指配置中的供应商实现选项。通知配置管理页必须使用与所选 Provider 对应的结构化分组表单；字段帮助使用可访问的信息图标 Tooltip，高级 JSON 只作为兼容入口。新增 Provider 时同步 Web Schema、中英文资源和测试。Secret 更新默认按键合并并保留空输入，清空必须是显式操作。

- 面向系统账号的通知只传稳定 `UserId`，由 `INotificationAccountDirectory` 按渠道解析；账号模块实现该端口，System 不得引用 RBAC。管理端搜索只能返回启用账号和脱敏目标，测试通知与普通通知必须复用同一解析规则；显式 `Address` 优先用于外部或覆盖目标。

- 站内实时通知必须先持久化或提交已读状态，再发布按用户定向的失效事件；实时帧不携带正文，SignalR 故障不得回滚权威数据。Web 必须保留断线重连，并仅在收到实时变更事件或展开通知面板时读取权威 API，显式展示已读/未读状态。

- 新未读消息的前台站内提示与失焦浏览器系统通知必须互斥；首次加载不得弹历史消息，浏览器权限只能由用户手势申请，未授权时静默保留站内数据，批量到达时避免逐条刷屏。

- SignalR Hub 必须鉴权，用户标识来自可信认证 Claim；WebSocket 查询参数 Token 必须在代理/访问日志中脱敏。多实例部署未配置 backplane 时，不得把单实例实时结果描述为集群一致。

- Webhook/HTTP Provider 必须使用 HTTPS、禁用重定向并拒绝显式本机/私网地址；生产还需出网代理层防 DNS 重绑定。

- `Unknown` 结果不得自动 failover；多 Site 必须分别建立幂等键、状态和重试记录。

## 系统 E2EE 约束

平台只保留系统级 E2EE 身份能力（解锁面板、设备缓存、通行密钥）。身份密钥运算放在本地 Worker，不得把口令、UMK、包裹密钥或恢复密钥写入日志、错误上报、审计或请求 URL；服务端只保存密文包裹与公钥材料。本机受信任设备缓存使用原生 Web Crypto 不可导出 CryptoKey 保护 UMK，禁止退回原始密钥字节存储，也不得宣称该能力等同于硬件绑定。

通行密钥只辅助 E2EE 解锁，不把 WebAuthn 验证成功等同于直接读取自动缓存。必须通过 PRF 输出派生包裹密钥，PRF 输出不得上传或持久化；服务器使用成熟库验证挑战/Origin/RP/UV/签名，挑战数据库单次消费且失败也失效。存在本机受信任设备缓存时优先用缓存静默解锁；通行密钥用于没有缓存时的显式解锁，不得在未完成 WebAuthn 验证或未登记缓存的情况下伪造自动解锁。

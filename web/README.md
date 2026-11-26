# 平台 Web 管理端

平台 Web 是基于 Vue 3、TypeScript、Vite、Pinia、Vue Router 和 Naive UI 的管理端前端，聚焦工作台、审批流、授权管理、系统管理、通知与账户安全；审批流程图使用 Vue Flow，系统级 E2EE 身份解锁基础设施保留。

全站采用 Paper Ocean 设计系统（奶油纸底 + 点阵纹理、海军蓝 2px 描边、无模糊硬投影、电光蓝强调、珊瑚色主行动，浅色「纸上海洋」与深色「夜航」共享同一套 `--po-*` 令牌），另提供玄青·鎏金、竹月·清雅、朱缃·典章 3 套商务风格；风格与明暗在侧栏「系统设置」中选择，登录用户偏好同步到服务端。

Windows WPF Client 的四套风格 × 明暗配色参照 `src/config/themes.ts`，并通过同一用户偏好接口同步风格；Web 调色盘仍是设计色值的维护入口，桌面端映射见 [`Sim.Admin.Wpf.Client/README.md`](../client/Sim.Admin.Wpf.Client/README.md)。

## 技术栈

开放平台管理页位于 `src/modules/openplatform`，提供第三方应用管理、授权范围配置、一次性 AppSecret 展示和令牌签发日志查询；菜单与路由由后端模块种子下发。

- Vue 3、TypeScript、Vite
- Pinia + pinia-plugin-persistedstate
- Vue Router、Naive UI、Tailwind CSS、Axios
- `@vue-flow/core`（审批流程画布）
- `@microsoft/signalr`（通知与站点状态）
- Vitest、Playwright
- ESLint / Prettier

## 快速开始

```bash
pnpm install
pnpm dev
```

开发服务器默认使用 `http://localhost:5173`，无需准备前端证书。需要本机 HTTPS 时，将证书和私钥分别放在被 Git 忽略的 `.cert/dev-cert.pem`、`.cert/dev-key.pem`；两者齐全时自动使用 `https://localhost:5173`，只存在一个时启动会报错。证书需包含 `localhost` 并在浏览器中受信任。

Web 使用 pnpm 11，并将仓库中经过评审的 `pnpm-lock.yaml` 作为可信安装基线：`pnpm-workspace.yaml` 的 `trustLockfile: true` 只跳过对既有锁文件条目的重复供应链等待期校验，新依赖解析和版本升级仍受 pnpm 默认的 24 小时发布等待期约束。依赖构建脚本采用显式白名单，依赖变更必须同步提交并评审锁文件。

常用脚本：

```bash
pnpm dev
pnpm type-check
pnpm lint
pnpm lint:check
pnpm format
pnpm test:unit
pnpm test:e2e
pnpm generate:api-types
pnpm build:test
pnpm build:staging
pnpm build:production
pnpm check:bundle-budget
pnpm preview
```

`e2e/authentication.spec.ts` 覆盖登录、受保护页面跳转和 Token 不进入 `localStorage`；`e2e/route-management.spec.ts` 覆盖动态路由注册。首次在本机运行 Playwright 时先安装 Chromium：

```bash
pnpm exec playwright install chromium
```

当前 `web/src` 下没有单元测试文件；恢复前端测试覆盖是待完成工作，CI 的单元测试步骤可能因找不到测试而失败。

`Jenkinsfile` 依次执行锁定安装、OpenAPI 类型重生成与漂移检查、生产依赖中危漏洞/许可证门禁、类型检查、零警告 ESLint、全部单元测试、生产构建、包体预算、Chromium 登录 E2E 和镜像构建/推送。`pnpm lint` 用于本地自动修复，CI 与提交前只读检查使用 `pnpm lint:check`。Node.js 版本要求见 `package.json`（`^20.19.0 || >=22.12.0`）。

## 项目结构

```text
web/
├── performance-budget.json # 生产包体预算
├── src/
│   ├── api/              # 跨模块 API、SDK 客户端与生成的 OpenAPI 类型
│   ├── components/       # 公共组件
│   ├── config/           # API、模块、主题、动态路由配置
│   ├── e2ee/             # 系统 E2EE 身份、设备缓存与通行密钥解锁
│   ├── framework/        # 可复用应用壳（createApp、平台模块、扩展入口）
│   ├── hooks/            # 组合式逻辑
│   ├── modules/          # 业务模块
│   ├── router/           # 路由、守卫、核心路由
│   ├── stores/           # Pinia 状态
│   ├── styles/           # 全局样式和主题变量
│   ├── types/            # 公共类型
│   ├── utils/            # 请求、路由注册等工具
│   ├── views/            # 公共页面
│   ├── App.vue
│   └── main.ts
├── e2e/                  # Playwright 测试
├── scripts/              # 项目脚本
├── public/
└── package.json
```

## 环境变量

环境文件包括 `.env`、`.env.test`、`.env.staging`、`.env.production`。

- `VITE_APP_TITLE`：页面标题前缀。
- `VITE_API_URL`：后端 API 基地址，供 `src/config/apiConfig.ts` 使用。
- `VITE_ICP_LICENSE`：页脚备案信息。
- `VITE_CUSTOM_ROOT_PATH`：自定义布局根路径，默认 `/`。
- `VITE_CUSTOM_REDIRECT`：访问自定义根路径时的跳转地址，默认 `/admin/home`。
- `VITE_ADMIN_ROOT_PATH`：后台布局根路径，默认 `/admin`。
- `VITE_ADMIN_DEFAULT_REDIRECT`：访问后台根路径时的跳转地址，默认 `/admin/home`。

## 路由和模块

应用入口 `src/main.ts` 只声明模块与菜单并调用 `src/framework` 的 `createApp`。框架平台模块统一从 `src/framework/platformModules.ts` 导出，业务模块清单在 `src/config/businessModules.ts` 汇总，`src/config/modules.ts` 负责合并两者；`pnpm build:lib` 以库模式构建框架包。

业务应用独立构建时，在入口先注册业务页面与语言包，再启动应用：

```ts
import { createApp, registerModuleComponents, registerMessagePacks } from '@sim/web-framework'

registerModuleComponents(import.meta.glob('./modules/**/*.vue'), 'modules/')
registerMessagePacks([{ name: 'qms', componentPrefixes: ['qms/'], load: () => import('./i18n/qms') }])
void createApp({ modules, menuConfig })
```

静态模块由 `src/config/modules.ts` 合并平台与业务清单；业务模块在 `src/config/businessModules.ts` 注册。当前注册：

- `home`：平台工作台（顶级路由 `/admin/home`），展示当前账号身份、角色与快捷入口，不请求业务聚合接口。
- `account`：当前用户的个人信息与账户安全；头像菜单提供个人信息、账户安全和退出。
- `approval`：审批流程模块，由后端种子下发 `/admin/approval` 菜单。「审批中心」含待办、已办、我提交的、抄送我的四个列表；「流程定义」使用 Vue Flow 工作台设计器维护节点与连线（条件、会签/或签/顺序会签、抄送、排他网关与并行 fork/join），支持发布、停用与另存新版本；画布空白处可用左键或鼠标中键拖动平移；「实例监控」按业务类型、状态与发起人过滤实例，详情只读渲染业务字段与审批记录并支持附件下载。
- `authorization`：用户、角色、权限、模块与路由管理。权限按路由、菜单与按钮三级门禁，无权限页面跳转 `/forbidden`；角色与模块编辑弹窗支持搜索、限高滚动与级联路由勾选。组织架构与租户管理的页面和接口仍位于该模块目录，菜单由后端种子挂到系统管理。
- `_template`、`_template-custom-layout`、`_template-no-layout`：模块开发模板。

`systemManagement` 与 `notifications` 由后端动态菜单/顶栏使用，不作为静态模块注册：

- `src/modules/systemManagement/views`：文件存储、定时任务、业务编号、系统配置、数据字典与通知服务管理页；组织架构与租户管理沿用 `authorization/views` 页面，由系统管理菜单进入。
- `src/modules/notifications`：当前用户站内通知接口、展示与 SignalR 实时刷新。
- `src/modules/siteStatus`：匿名站点状态接口与 `/hubs/site-status` 实时订阅。

模块布局类型：

- `admin`：挂载到 `AdminLayout`。
- `custom`：挂载到 `Layout`。
- `none`：直接作为顶级路由。

登录后，路由守卫通过 `DynamicRouteLoader` 加载后端动态路由；用户菜单接口一次返回可访问模块与跨模块统一组装的全局路由树，不再逐模块拉取和合并。`src/router/index.ts` 只负责 Router 组合，认证决策和导航副作用分别位于 `src/router/guards/authNavigation.ts` 与 `navigationGuards.ts`。动态路由组件使用相对 `src/modules` 的规范路径（如 `authorization/views/user/Profile`），加载器兼容旧版 `/modules/.../Xxx.vue` 路径以便数据库平滑升级。静态模块加载失败时记录警告但不会阻止其他模块启动；菜单只按 `meta.order` 排序，`admin` 布局模块根路由统一挂载为 `/admin/*`。标签页提供显式关闭按钮、右键批量关闭与 `Shift+F10` 菜单键入口，固定标签不可关闭并始终排在最左；关闭当前标签时导航到相邻页，过多时标签条横向滚动并自动定位激活项。

首次启动由公开路由 `/setup` 承接：路由守卫在认证判断前读取 `GET /api/admin/setup/status`，未完成初始化时把任意页面重定向到初始化页。初始化页分三步填写管理员账号密码、站点名称/默认语言/默认主题、开放注册/登录失败上限与锁定时长，提交后按 `GET /api/admin/setup/progress` 展示初始化步骤，完成后进入登录页。相关实现位于 `src/views/setup.vue`、`src/components/AuthBrandPanel`、`src/config/setup.ts` 与 `src/api/setup.ts`。

## 本地化

Web 支持简体中文（`zh-CN`）与英文（`en-US`）。`src/i18n/index.ts` 只负责语言状态和解析；中文的 `core`、`system`、`account` 随入口同步注册，英文整包由 `loadFrontendLocale('en-US')` 按需加载。`approval`、`organization`、`authz`、`notifications`、`e2ee` 由 `src/i18n/lazyPacks.ts` 登记为按需语言包，`DynamicRouteLoader` 在导入路由组件前按组件路径前缀注册对应语言包；规则未命中时静默回退为原始 key，因此新增按需分组必须同时登记 `packLoaders` 与组件路径前缀。静态模块复用其他模块组件时，必须在自身路由中显式 `ensureMessagePacks([...])` 后再加载组件。

前端 i18n key 统一采用与后端一致的 ABP 风格「冒号分隔 + 大驼峰」（如 `Common:Search`、`Authz:PermissionGroupSystem`）：静态文案直接写 `Domain:Segment[:Segment]`，动态拼接键（时间、HTTP 状态、审批枚举、定时任务、存储提供器等）经由 `messageKey(...parts)` 生成，禁止 `domain.camelCase` 点号键。`messages/consistency.test.ts` 会加载全部按需语言包并校验中英文 key 对齐，同时阻止 `Menu:*` 或 `Sim:*` 回流前端。

动态菜单、后端错误、站点名称和登录页品牌标语不在前端语言包重复维护：`src/i18n/backend.ts` 通过 `GET /api/admin/client-localization` 加载菜单资源、品牌标语与站点配置，`meta.title` 保存稳定的 `Menu:*` key。页面刷新后导航守卫只读取一次 `/api/admin/site-status`，随后由匿名 SignalR `/hubs/site-status` 推送维护开关；维护开启时导航进入 `/maintenance`，业务请求收到 503 时请求拦截器也会跳转维护页。

## 请求和认证

统一请求实例位于 `src/utils/request.ts`：

- Access Token 只保存在页面内存并自动添加 `Authorization: Bearer <token>`，不写入 `localStorage`。
- Refresh Token 由后端写入 `HttpOnly`、`SameSite=Strict` Cookie，JavaScript 不读取令牌值。
- 登录页“记住我”仅在 `localStorage` 保存 `login-remembered-user-name`，不保存密码或令牌；密码由浏览器密码库负责保存和自动填充。
- 登录页支持密码后的 TOTP、邮箱、短信或恢复码第二步验证，并提供邮箱/SMS/TOTP 找回密码流程。
- 登录页与 `/register` 共用同一卡片容器，开放注册时可在登录/注册表单间滑动切换；注册用户角色由后端系统配置 `security.registration-role-codes` 分配。
- 登录、注册补齐、2FA 和已有账号绑定统一使用 `/auth/external/callback` 处理提供方回调；已启用的微信、QQ、Steam、Microsoft、Google 外部登录见 [`docs/ops/EXTERNAL_LOGIN.md`](../docs/ops/EXTERNAL_LOGIN.md)。
- 账户安全页提供改密、双重验证、外部登录绑定、设备会话与账号注销；登录时发送持久化随机设备标识与 PC/Pad/Phone 类型，该标识不是认证凭据。
- 刷新和注销前获取 `/api/system/user/antiforgery-token` 并发送 `X-XSRF-TOKEN`；401 时携带 Cookie 调用 `/api/system/user/refresh-token`，并发 401 共享同一个刷新 Promise，每个原请求最多重试一次。
- 可取消请求按方法、URL、查询参数和请求体生成重复键；缓存列表页通过 `useKeepAliveRefresh` 区分首次挂载与重新激活。
- 进入受保护页面或直接访问 `/login` 时会先尝试通过 Cookie 恢复会话；刷新失败时清理内存会话并跳转登录页。
- 错误提示优先使用后端当前文化的 `error.message`；响应拦截器标记已展示错误，业务 `catch` 使用 `handleError` 避免重复提示。

本地开发保持 `VITE_API_URL` 为空，通过 Vite 将同源 `/api` 与 `/hubs` 代理到 `https://localhost:7777`；临时切换后端地址可在启动进程设置 `VITE_API_PROXY_TARGET`。跨源部署必须使用 HTTPS，并让后端 CORS 明确允许前端 Origin 和凭据。

## API 契约

契约单一来源为 `src/api/definition/*.json`（由后端 CI 导出提交），`src/api/{group}`（group 为 `admin`、`system`、`worker`、`tenant`、`approval`）由 `@hey-api/openapi-ts` 生成类型与 SDK 客户端，并通过 `src/api/setupClients.ts` 绑定统一 Axios 请求实例。后端契约同步后执行：

```bash
pnpm generate:api-types
pnpm type-check
```

生成文件必须提交；CI 会重生成并拒绝未提交的类型漂移，不应手工修改 `src/api/{group}`。

## 公共组件与能力

- `src/components/Table`：统一列表 `DataTable`（复选列、固定操作列、表格/卡片切换、列宽拖拽、分页）与 `DataPagination`；管理端列表优先使用。
- `src/components/FileWorkbench`：单文件选择/拖放、存储 provider、上传进度和 Office-to-PDF 转换。
- `src/components/ImageEditor`：共享 `AvatarEditor` 头像弹窗，浏览器 Canvas 内 1:1 裁剪并输出最大 512×512、质量 84 的 WebP。
- `src/components/AuditInfo`、`src/utils/auditColumns.ts`：统一展示创建人、创建时间、修改人、修改时间；显示名通过批量接口解析并按 ID 缓存。
- `src/api/fileManagement.ts`、`src/api/contentFile.ts`：框架文件与业务内容文件上传/下载封装，正文保存稳定文件 ID URL。
- `src/components/ManagedContentImage.vue`、`LazyImage.vue`：托管图片懒加载（视口前 200px 预热）、加载占位与 Blob 读取。
- `src/e2ee`：系统 E2EE 身份的解锁面板、设备缓存与通行密钥验证；`App.vue` 只异步挂载 `E2eeUnlockHost`，公开页面不加载加密包。

## 主题和样式

主要入口：

- `src/styles/style.css`：全局样式。
- `src/styles/theme-variables.css`：Paper Ocean 设计令牌（`--po-*`）与兼容别名，含深色「夜航」取值。
- `src/styles/paper-ocean.css`：基础排版、纸底点阵画布、`.po-*` 组件类与 Naive UI 全站换肤。
- `src/styles/business-skin.css`：商务皮肤层，仅在 `[data-skin='business']` 生效。
- `src/config/themes.ts`：4 套风格 × 明暗的调色盘注册表，是主题色值的唯一来源。
- `src/config/theme.ts`、`src/config/businessTheme.ts`：Naive UI 主题覆盖。
- `src/components/ThemeStylePicker`、`SystemPreferences`：风格与明暗选择。
- `src/stores/theme.ts`：明暗与风格的唯一状态源并负责持久化。
- `src/stores/userPreferences.ts`：登录用户偏好的加载与防抖回写。
- `src/stores/user.ts`：会话、角色与权限的唯一状态源。
- `src/stores/siteStatus.ts`：站点维护状态的唯一状态源。
- `src/stores/density.ts`：内容密度（小/中/大）状态源，由 `data-density` 驱动纵向间距。
- `src/components/ThemeToggle`、`LocaleSwitcher`、`DensityToggle`：顶栏外观控件。

主题偏好统一持久化在 `localStorage` 键 `naive-theme`。`App.vue` 根据当前状态写入全部 `--po-*` 令牌，并同步根节点 `data-theme`、`data-style`、`data-skin` 与 `data-density`。业务页面只消费令牌与 `.po-*` 类，不得新增页面级双色板；Naive 颜色字段必须是 hex / rgba 字面量。方案与验收结果见 [`../docs/design/ui/MULTI_THEME_DESIGN.md`](../docs/design/ui/MULTI_THEME_DESIGN.md)。

## 通知中心

`src/modules/systemManagement/views/notifications` 提供通知配置、模板、规则、多 Site Webhook 和投递记录管理；`src/modules/notifications` 提供当前用户站内通知接口。通知配置由 `providerSchemas.ts` 为每个 Provider 定义分组字段、默认值、Secret 和帮助文案，高级 JSON 仅作为折叠的兼容入口。已保存配置可在同一弹窗发送测试通知：邮件、短信、语音、站内和浏览器渠道支持搜索系统账号，账号选项只展示脱敏目标；Secret 输入为空表示保留已有值。

顶栏通过鉴权 `/hubs/user-notifications` 连接 SignalR，仅在收到 `notificationChanged` 或展开通知面板时重新读取站内通知 API；断线自动重连。列表与未读计数明确显示“未读/已读”，点击单条或“全部已读”先乐观更新，写入失败时回读权威数据回正。页面可见且聚焦时展示站内通知卡片，页面隐藏或失焦时仅在已授权的情况下弹浏览器系统通知。

动态菜单组件路径是 `systemManagement/views/notifications/index`，权限为 `system:notification:manage` 与 `system:notification:view-deliveries`。

## 新增模块

1. 在 `src/modules` 下创建模块目录。
2. 创建 `index.ts`，导出 `ModuleConfig`。
3. 创建 `routes.ts`，导出模块路由。
4. 在 `src/config/businessModules.ts` 的 `businessModules` 中注册模块。
5. 根据页面类型选择 `layout`。
6. 如新增菜单、标签页或动态路由约定，同步更新本文档。

可参考 `scripts/create-module.mjs` 和 `scripts/README.md`。

## 构建和部署

```bash
pnpm build:production
pnpm check:bundle-budget
```

构建产物输出到 `dist/`。`performance-budget.json` 限制入口、异步块、JavaScript 总量与 CSS 总量；Naive UI 整体合并为 `vendor-ui` 单 chunk，只被懒加载路由使用的依赖单独分包，SignalR 通过 `src/utils/signalrLoader.ts` 按需导入，英文语言包按需加载。动态图标只允许在 `src/utils/iconLoader.ts` 的显式注册表中按需引入。部署到 Nginx 时需要把前端路由回退到 `index.html`。

生产镜像把 `dist` 内置到 nginx，配置、TLS 与 `.htpasswd` 由 Compose 只读挂载：

```bash
pnpm build:production
docker build -f Dockerfile -t repo.dulm.net/sim-web:<tag> .
```

`Jenkinsfile` 在仓库根目录执行 CI，构建并推送 `sim-web` 镜像；`develop` 通过后自动部署测试，`v*` 标签经审批发布生产。Jenkins 作业和目标机配置见 [`../deploy/README.md`](../deploy/README.md)。

请求封装或认证拦截器改动至少运行 `pnpm type-check`、`pnpm lint:check` 和 `pnpm test:unit -- --run`；不得通过删除断言或关闭 lint 规则绕过。

## 文本与换行约定

Web 源码跟随仓库统一规则：普通文本使用 CRLF，`Jenkinsfile` 和 `*.sh` 使用 LF。提交前可用 `git ls-files --eol` 检查，不允许出现 `w/mixed`。

## 文档维护

修改前端模块、路由、布局、环境变量、请求封装、认证流程、主题系统、构建脚本或测试脚本时，请同步更新：

- 本 README
- 项目文档总索引 [`docs/README.md`](../docs/README.md)
- `docs/core/ARCHITECTURE.md`
- `docs/core/DEVELOPMENT.md`
- `docs/core/DOCUMENTATION.md`
- 项目级 [`docs/core/ENTERPRISE_SYSTEM_GUIDE.md`](../docs/core/ENTERPRISE_SYSTEM_GUIDE.md)，检查企业级能力、当前风险与完成定义是否变化
- [`web/docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) 与 [`web/docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)

# 平台 Web 架构说明

本文档说明平台 Web 的前端架构、模块体系、路由和请求认证机制。

## 总览

平台 Web 是通用系统平台基座的管理端前端，基于 Vue 3、TypeScript、Vite、Pinia、Vue Router 和 Naive UI 构建。

项目主要能力：

- 管理工作台。

- 审批中心，以及系统管理中的组织架构与租户管理。

- 用户、角色、权限、模块、路由管理。

- 静态模块注册。

- 登录后动态路由加载。

- 主题切换和响应式布局。

## 目录职责

```text
src/
├── api/              # 跨模块 API 和 OpenAPI 生成类型
├── components/       # 公共组件
├── config/           # API、模块、主题、动态路由配置
├── framework/        # 可复用应用壳：createApp、平台模块、扩展导出
├── hooks/            # 组合式逻辑
├── modules/          # 业务模块
├── router/           # 路由、守卫、核心路由
├── stores/           # Pinia 状态和持久化 key
├── styles/           # 全局样式、主题变量
├── types/            # 公共类型
├── utils/            # 请求、Markdown、动态路由、模块注册等工具
├── views/            # 公共页面
├── App.vue
└── main.ts
```

## 启动链路

1. 业务应用入口 `src/main.ts` 声明模块清单和菜单配置，调用 `createApp`。
2. `src/framework/bootstrap.ts` 创建 Pinia、i18n 与 Router，注册指令并挂载 `App.vue`。
3. `src/router/index.ts` 组合静态路由，`src/router/guards` 安装并测试导航守卫。
4. 静态模块由业务应用的模块清单提供，平台模块来自 `src/framework/platformModules.ts`。
5. 登录后通过 `DynamicRouteLoader` 加载后端动态路由。
6. 请求层根据本地 token 注入认证头，并处理 token 刷新。

框架以库模式构建（`pnpm build:lib`），模块保留并输出类型声明与 `style.css`。框架源码里的组件扫描只覆盖框架自身目录；业务应用需在调用 `createApp` 之前执行 `registerModuleComponents(import.meta.glob('./modules/**/*.vue'), 'modules/')`，业务语言包通过 `registerMessagePacks` 注册，`DynamicRouteLoader` 会在导入组件前按前缀加载对应语言包。

## 模块体系

静态模块入口为 `src/config/modules.ts`（合并平台模块）；业务模块在 `src/config/businessModules.ts` 注册，模块类型定义在 `src/types/module.ts`。

当前主要模块：

- `home`：管理工作台。

- `account`：个人资料与账户安全。

- `authorization`：用户、角色、权限、模块与路由管理；组织架构与租户管理页面仍在该模块目录，菜单归入系统管理。

- `approval`：审批中心、流程定义与实例监控。

- `notifications`：站内通知面板与通知相关页面。

- `systemManagement`：系统配置、数据字典、文件存储、定时任务、业务编号与通知服务；组织架构与租户管理菜单指向 `authorization` 模块页面。

- `_template`：默认布局模块模板。

- `_template-custom-layout`：自定义布局模块模板。

- `_template-no-layout`：无布局模块模板。

模块通常包含：

- `index.ts`：导出 `ModuleConfig`。

- `routes.ts`：导出模块路由。

- `api/`：模块接口。

- `views/`：页面组件。

## 布局与路由

核心路由位于：

- `src/router/index.ts`

- `src/router/routes.ts`

- `src/router/guards/authNavigation.ts`

- `src/router/guards/navigationGuards.ts`

- `src/utils/moduleRouteRegistration.ts`

- `src/utils/dynamicRouteLoader.ts`

模块支持三类布局：

- `admin`：挂载到 `AdminLayout`，用于后台管理。

- `custom`：挂载到通用 `Layout` 或模块自定义布局。

- `none`：直接作为顶级路由，用于登录、站点状态等全屏页面。

`AdminLayout` 由固定高度顶栏、可折叠侧栏、标签页内容画布和轻量页脚组成，整体限制在视口内，滚动交给标签内容区。`src/modules/home/views/index.vue` 提供管理工作台首页；`src/views/login.vue` 是不挂载后台壳层的双栏登录入口。两者和 Header、Sidebar、Tabs 均只消费主题语义变量，不自行判断深浅模式。

路由守卫负责：

- 页面标题。

- 登录态判断。

- 登录后动态路由加载。

- 未授权访问跳转。

- 404 和静态核心页面处理。

动态路由的组件字段以 `src/modules` 为根，不带 `/modules/` 前缀和 `.vue` 后缀，例如 `approval/views/todo/index`。`DynamicRouteLoader` 通过单次 `user-accessible-routes` 请求获取可访问模块与跨模块统一组装的全局路由树，顶层路由按子树中排序最前的可访问模块解析布局；加载 Promise 合并登录跳转期间的并发导航，用户菜单接口只初始化一轮；退出登录时通过注册回调移除本次会话的动态路由，并用加载代次忽略迟到响应。`src/config/dynamicRoute.ts` 为早期用户资料页种子路径保留显式别名，已有数据库无需先迁移也能正常加载；新配置不得继续使用历史路径。

## 请求与认证

统一请求实例位于 `src/utils/request.ts`。

请求流程：

1. 从页面内存读取 Access Token。
2. 自动附加 `Authorization: Bearer <token>`。
3. 遇到非会话接口的 401 时，先获取防伪令牌。
4. 浏览器携带 HttpOnly Refresh Token Cookie 调用 `/api/system/user/refresh-token`。
5. Pinia User Store 以单飞 Promise 合并并发刷新，成功后更新完整会话；每个 401 请求再重试自身请求。
6. 刷新失败时清理登录态并跳转登录页；前端 JavaScript 不读取 Refresh Token。

持久化 key 位于 `src/stores/persistentKeys.ts`。

后台列表页位于 `KeepAlive` 中。`AdminLayout` 遇到嵌套路由时不再重复缓存容器，把最终页面缓存交给内层 `Layout`。公共 `useKeepAliveRefresh` 在首次显示时只执行初始化，并把同一渲染周期内由嵌套缓存传播的多个 `onActivated` 合并为一次；之后从其他菜单返回时才刷新，防止同一查询并发多次并相互取消。

## API 分层

后端契约以 `src/api/definition/*.json` 为单一来源（由后端导出提交），`pnpm generate:api-types` 使用 `@hey-api/openapi-ts` 生成 `src/api/{group}`（group 为 admin/approval/system/openplatform 四个目录）的类型与 SDK 客户端。后端 Schema ID 使用短类名（如 `RouteConfigDto`，泛型为 `PagedResultDtoOfRouteConfigDto`），生成的类型名稳定且不携带程序集版本号，发布后不会漂移。业务 API 优先引用生成 Schema，再补充仅属于前端表单或视图的类型；生成目录只允许脚本更新。CI 通过重生成后的 Git 差异检查阻止契约漂移。SDK 函数按 method+path 生成（去掉 `/api` 前缀），通过 `src/api/setupClients.ts` 绑定统一 Axios 请求实例；业务 api 门面保留 `Promise<X>` 对外契约，内部以 `throwOnError` 调用 SDK 并断言响应类型。

新增业务接口时，优先放在对应模块：

```text
src/modules/{module}/api/
```

跨模块共享接口可放在：

```text
src/api/
```

Framework 文件 API 位于 `src/api/fileManagement.ts`。`FileWorkbench` 负责远程上传和 Office-to-PDF；个人信息页使用 `AvatarEditor` 头像弹窗，在浏览器 Canvas 内完成 1:1 裁剪（选区内拖动、滚轮或双指缩放）并统一输出 512px WebP 后自动上传，页面不再接受头像 URL。`systemManagement` 动态模块使用 provider 导航和一个共享表单维护数据库化存储配置；调度页面支持 HTTP、命令、Service 方法三类动态 Job 的新增/删除及内置 Job 的 Cron/启停。

请求封装应复用：

- `src/utils/request.ts`

- `src/api/{group}`（生成的类型与 SDK 客户端）

- `src/api/setupClients.ts`

- `src/types/api.ts`

## 主题与样式

主题相关入口：

- `src/config/themes.ts`
- `src/config/theme.ts`
- `src/config/businessTheme.ts`
- `src/hooks/useTheme.ts`
- `src/stores/theme.ts`
- `src/styles/theme-variables.css`
- `src/styles/business-skin.css`
- `src/components/SystemPreferences`
- `src/components/Layout/Sidebar.vue`
- `src/styles/style.css`

`src/stores/theme.ts` 是全站明暗与风格状态的唯一来源，`useTheme` 负责向页面和组件暴露响应式状态。跨路由页面及 Teleport 弹层必须读取该 Store，避免使用独立的 `prefers-color-scheme` 规则覆盖用户已持久化的选择。

主题分为 4 套风格（`paper-ocean` / `xuanqing` / `zhuyue` / `zhuxiang`）× 明暗两模式，风格与明暗相互独立。`src/config/themes.ts` 是唯一色值来源：注册表为每套风格提供 `light` / `dark` 调色盘、预览色、皮肤类型与 i18n 键，`paletteToCssVars()` 输出全量 `--po-*`，`paletteToThemeColors()` 供 Naive 工厂使用；未知风格回落纸上海洋。纸上海洋直接复用原有 `lightThemeColors` / `darkThemeColors` 与 `theme.ts` 常量，保证既有视觉不变；商务风格经 `src/config/businessTheme.ts` 的 `createBusinessThemeOverrides()` 生成覆盖。

`App.vue` 把 `cssTokens` 写入 `--po-*` 变量，并在根元素同步 `data-theme="light|dark"`、`data-style` 与 `data-skin="paper|business"`；`src/styles/theme-variables.css` 以 `--po-*` 语义令牌为准（画布、表面、强调色、阴影、尺寸和焦点环），`--ink-*`、`--theme-*`、`--admin-*` 仅作为存量页面的别名映射，新代码不得继续使用。商务皮肤的结构差异（1px 发丝边、柔光分层投影、收敛圆角、实心主按钮、字重 600、禁用倾斜）集中在 `src/styles/business-skin.css`，仅在 `[data-skin='business']` 生效，由 `main.ts` 在 `paper-ocean.css` 之后引入。Naive UI 的颜色及圆角覆盖分别位于 `theme.ts`（纸上海洋）与 `businessTheme.ts`（商务）；颜色字段只能是 hex / rgba 字面量。全局样式提供统一盒模型、字体渲染和 `prefers-reduced-motion` 降级，登录和后台壳层在桌面、平板和移动端使用 CSS 媒体查询收敛布局。主题偏好持久化在 `localStorage` 键 `naive-theme`，字段为 `currentThemeName` 与 `themeStyle`；入口为侧栏菜单下方的「系统设置」（`SystemPreferences`：宽屏右侧 440px 抽屉，窄屏居中弹窗），内部由 `ThemeStylePicker` 提供风格与明暗选择，个人资料页不再挂载外观卡。

响应式能力：

- `src/hooks/useResponsive.ts`

- `src/styles/utilities/responsive.scss`

- `src/styles/mixins/mobile.scss`

## 审批流程画布

审批定义编辑器位于 `src/modules/approval/views/definition/edit.vue`，使用 Vue Flow 渲染节点与连线：固定操作栏 + 左侧节点库 + 中央画布 + 右侧属性面板 + 底部校验区。画布坐标写入定义级 `layoutJson`，引擎不读取；节点、连线与条件由 `src/modules/approval/utils/flowGraph.ts` 转换与校验，发布前仍由后端图结构校验兜底。

## 与后端协作点

Web 前端默认通过 `VITE_API_URL` 连接后端 API。强耦合契约包括：

- 登录、刷新、登出接口。

- 审批定义、实例、任务与附件接口。

- System 模块用户、角色、权限、模块、路由、组织与租户接口。

- 系统 E2EE 身份与通行密钥接口（基础设施保留，当前没有业务加密内容）。

- 动态路由返回的组件路径、布局、菜单和元信息。

后端变更接口路径、响应结构或动态路由字段时，需要同步更新前端 API 类型和路由加载逻辑。

## 构建与性能边界

生产构建不加载 Vue DevTools，并将 Vue、Naive UI/图标、Vue Flow 审批设计器和其他第三方依赖拆分为独立块；`performance-budget.json` 与 `scripts/check-bundle-budget.mjs` 基于 Vite manifest 检查入口 JavaScript、单个异步块、JavaScript 总量和 CSS 总量。动态图标使用显式白名单注册，避免整套图标库进入首屏包。

系统 E2EE 身份基础设施保留在 `src/e2ee`：`App.vue` 异步挂载 `E2eeUnlockHost`，受信任设备使用 IndexedDB v3（本机不可导出 CryptoKey 直接保护 UMK）；通行密钥模式不持久化 PRF 输出或派生密钥，非秘密偏好保存在 device-preferences，UMK 密文通过服务端凭证接口验证后取回。当前平台基座没有业务加密内容，解锁面板保持待机，后续业务模块接入时复用同一套密钥与解锁边界。

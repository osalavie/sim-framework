# SimApp Web 开发指南

本文档说明前端本地开发、环境变量、模块扩展、测试和构建流程。

## 环境要求

- Node.js `^20.19.0 || >=22.12.0`
- pnpm
- 推荐 IDE：VS Code

## 安装与启动

在 `web` 目录执行：

```bash
pnpm install
pnpm dev
```

默认开发地址：

- `http://localhost:5173`

Playwright 本地测试默认也使用 `http://localhost:5173`。

## 常用命令

```bash
pnpm dev
pnpm type-check
pnpm lint
pnpm format
pnpm test:unit
pnpm test:e2e
pnpm generate:api-types
pnpm build:test
pnpm build:staging
pnpm build:production
pnpm build:lib
pnpm check:bundle-budget
pnpm preview
```

## 环境变量

环境文件：

- `.env`
- `.env.test`
- `.env.staging`
- `.env.production`

常用变量：

- `VITE_APP_TITLE`：页面标题前缀。
- `VITE_API_URL`：后端 API 基地址。
- `VITE_API_PROXY_TARGET`：仅供本地开发进程覆盖 Vite `/api` 代理目标。
- `VITE_ICP_LICENSE`：备案信息。
- `VITE_CUSTOM_ROOT_PATH`：自定义布局根路径。
- `VITE_CUSTOM_REDIRECT`：访问自定义根路径时的跳转地址。
- `VITE_ADMIN_ROOT_PATH`：后台布局根路径，默认 `/admin`。
- `VITE_ADMIN_DEFAULT_REDIRECT`：访问后台根路径时的跳转地址。

修改环境变量后需要重启 Vite 开发服务。

## 新增模块

推荐使用脚本：

```bash
node scripts/create-module.mjs
```

手动新增流程：

1. 在 `src/modules` 下创建模块目录。
2. 创建 `index.ts`，导出 `ModuleConfig`。
3. 创建 `routes.ts`，导出模块路由。
4. 如有接口，创建 `api/` 并定义请求方法和类型。
5. 如有页面，创建 `views/`。
6. 在 `src/config/businessModules.ts` 的 `businessModules` 中注册模块；平台模块由框架自带，不要改 `src/framework/platformModules.ts`。
7. 根据页面场景选择 `layout`：`admin`、`custom` 或 `none`。
8. 业务仓独立构建时，在入口先调用 `registerModuleComponents(import.meta.glob('./modules/**/*.vue'), 'modules/')`，再调用 `createApp`；业务文案用 `registerMessagePacks` 注册。
9. 运行 `pnpm type-check` 和必要测试。

## 新增页面

页面组件建议放在所属模块的 `views/` 目录。新增页面时检查：

- 路由 `name` 是否唯一。
- 路由 `path` 是否符合布局前缀。
- `meta.title` 是否可用于菜单或标签页。
- 是否需要登录或权限。
- 是否需要出现在菜单中。
- 是否需要与后端动态路由字段保持一致。

后端动态路由的组件路径必须相对 `src/modules` 填写，且不带开头 `/modules/` 与结尾 `.vue`，例如 `approval/views/todo/index`。用户菜单由后端从全局路由目录统一建树并跨模块补齐祖先，前端不再自行按模块拼装父子关系；调整 `user-accessible-routes` 契约或路由元数据字段后，需重生成 `src/api/definition/system.json` 与前端 SDK。

## 新增接口

接口建议放在所属模块的 `api/` 目录。

开发约定：

- 复用 `src/utils/request.ts`。
- 为请求参数和响应值定义 TypeScript 类型。
- 不在页面组件里拼接重复 URL。
- 401、刷新 token 和通用错误提示交给统一请求层处理。
- 与后端 DTO 对齐后再更新页面逻辑。

位于后台 `KeepAlive` 下且需要返回菜单时刷新的列表页统一使用 `useKeepAliveRefresh`。不要同时在 `onMounted` 和 `onActivated` 调用同一个加载函数，也不要让 `AdminLayout` 与嵌套路由 `Layout` 同时缓存最终页面；嵌套缓存会向后代重复传播激活事件，造成成对请求和取消竞争。

后端接口变化时，重生成 `src/api/definition/*.json` 后执行 `pnpm generate:api-types` 和 `pnpm type-check`。业务类型优先引用 `src/api/{group}` 中的 Schema；不要直接编辑生成文件。破坏性契约变更必须先完成版本与迁移评审后再提交。

系统管理页面位于 `src/modules/systemManagement`。存储配置的 provider 导航必须共用一个表单，不得缓存或回显 Secret；空密钥表示保留后端已有值。定时任务更新使用 `PUT /api/system/schedule-task?group=...&key=...`，动态 Job 通过 `POST /api/system/schedule-task` 新增并通过 `DELETE /api/system/schedule-task/{id}` 删除；Worker 类型只用于后端登记的内置 Job。个人信息头像必须复用 `AvatarEditor` 头像弹窗（点击空选区或拖入图片选图，选区内拖动/滚轮或双指缩放裁剪）并调用系统头像上传接口，不得恢复自由 URL 输入；个人信息页的头像位于最前并以点击弹窗编辑，处理参数当前为最大 512×512、WebP、质量 84，弹窗本身不得出现滚动条。

## 主题与样式开发

优先复用：

- `src/styles/theme-variables.css`
- `src/config/themes.ts`
- `src/config/theme.ts`
- `src/config/businessTheme.ts`
- `src/styles/business-skin.css`
- `src/hooks/useTheme.ts`
- `src/styles/style.css`

新增样式时注意：

- 公共样式放在 `src/styles`。
- 模块私有样式尽量靠近组件。
- 响应式逻辑优先复用 `useResponsive` 和已有 SCSS 工具。
- 页面和 Teleport 弹层的深浅主题统一通过 `useTheme` / `src/stores/theme.ts` 获取；`prefers-color-scheme` 只能用于首次主题策略，不能在组件样式中覆盖用户选择。
- 管理端画布、表面、阴影、强调色和布局尺寸优先使用 `src/styles/theme-variables.css` 中的 `--po-*` 语义令牌（`--admin-*` 等仅为存量别名）；不要在登录、Header、Sidebar、Tabs 或后台页面重复维护两套颜色。
- 新增风格只在 `src/config/themes.ts` 注册表补充调色盘与 i18n 键，不要再建第二份色值；商务风格的结构差异写进 `business-skin.css` 并加 `[data-skin='business']` 前缀。
- 组件里不写死 `2px solid var(--po-ink)`、`font-weight: 700/800/900` 或 `Npx Npx 0 var(--po-ink)`，改用 `--po-border*`、`--po-weight-*`、`--po-shadow-*`；否则商务皮肤无法统一切换。
- Naive UI 的颜色字段只能传 hex / rgba 字面量，CSS 变量仅可用于描边字符串；改动后至少执行 `pnpm type-check` 和 `pnpm test:unit`，并检查 4 套风格 × 明暗的「系统设置」抽屉 / 弹窗与一个列表页。
- 主题入口固定在侧栏菜单下方的「系统设置」（`SystemPreferences`）；宽屏用右侧抽屉、窄屏用居中弹窗，内容只挂 `ThemeStylePicker`。窄屏打开偏好后立即收起侧栏抽屉，关闭后焦点回到触发按钮；不要再把外观选择放回个人资料页或新增第三个入口。
- 新增图标按钮必须提供可读的 `aria-label`，表单字段保留可见 Label，焦点态不得移除；动画同时检查 `prefers-reduced-motion`。
- 修改 `AdminLayout` 的 Header/Footer 高度时，应同步调整对应令牌，不要在子组件中重新写 `100vh - 固定值`。

## 测试

单元测试：

```bash
pnpm test:unit
```

端到端测试：

```bash
pnpm exec playwright install chromium
pnpm test:e2e
```

认证 E2E 使用网络拦截提供确定性的登录响应，验证登录请求结构、进入受保护页面，以及 Access/Refresh Token 均不写入 `localStorage`。需要复用已手动启动的预览服务时，可设置 `PLAYWRIGHT_SKIP_WEBSERVER=true`。

工作台首页使用 `data-testid="admin-home"` 作为认证 E2E 的稳定到达标识，测试不依赖会随本地化或视觉设计变化的首页文案。

类型检查：

```bash
pnpm type-check
```

变更以下内容时建议至少运行类型检查：

- 路由。
- 模块配置。
- API 类型。
- Store。
- 公共组件。
- 请求封装。

## 构建与部署

生产构建：

```bash
pnpm build:production
pnpm check:bundle-budget
```

构建产物输出到 `dist/`。

部署到 Nginx 或其他静态服务器时，需要为前端路由配置回退到 `index.html`。示例：

```nginx
location / {
  try_files $uri $uri/ /index.html;
}
```

如果前后端不在同一域名，需要确认：

- `VITE_API_URL` 正确。
- 后端 CORS 配置允许前端域名。
- HTTPS、Cookie 或 Authorization Header 策略符合部署环境。

## 发布前检查

- `pnpm type-check` 通过。
- `pnpm build:production` 通过。
- `pnpm generate:api-types` 重生成后没有未提交差异。
- `pnpm check:bundle-budget` 通过；新增图标已按需加入显式注册表，没有引入整套图标 Namespace Import。
- `pnpm audit --registry=https://registry.npmjs.org --prod --audit-level moderate` 和生产依赖许可证白名单检查通过。
- 生产环境 `VITE_API_URL` 正确。
- 登录、刷新 token 和登出流程正常。
- 动态路由加载正常。
- 主要页面在移动端和桌面端可用。
- 文档已同步更新。
- `pnpm test:unit -- --run` 和 Chromium 认证 E2E 通过。
- `Jenkinsfile` 的锁定依赖安装、API 契约、依赖审计、类型检查、测试、生产构建、性能预算和 E2E 阶段全部通过。

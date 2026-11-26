# Paper Ocean 全站 UI 重构 · 实施手册

> 设计源：`docs/design/ui/prototypes/design-system.html` 与页面原型（`index.html`、`dashboard.html` … `login.html`）
> 样式源：`docs/design/ui/prototypes/assets/ui.css`（令牌与组件类的权威定义）
> 本文档用于统一 `web` 的落地方式，避免各页面各自为政。历史记录仅作追溯，不代表当前仓库能力。

## 1. 设计语言一句话

奶油纸底（`#fff9ee`）+ 点阵纹理 + 海军蓝 2px 描边 + 无模糊硬投影 + 电光蓝强调交互 + 珊瑚色主行动 + Maple Mono CN 圆角等宽字体。
「先让人看懂、状态永远可见、危险操作慢一点」是三条不可妥协的原则。

| 维度 | 规则 |
| --- | --- |
| 描边 | 结构元素 2px `--po-ink`；控件 2px；表格分隔 1px `--po-line` |
| 投影 | 只有硬投影。静止态收紧：`1px 1px 0 ink`（卡片、统计、分段控件、按钮、选项片、输入/搜索框）；悬停统一 `--po-shadow-hover`（3px），倾斜卡片外壳的实心阴影板偏移 6px，弹层 2px、弹窗 4px。**禁止模糊投影** |
| 圆角 | 卡片 24px、主按钮 15px、输入 / 下拉 16px、胶囊 999px、弹窗 24px |
| 强调色 | 电光蓝 `--po-accent` 只用于可交互元素、选中态、关键数据；珊瑚 `--po-coral` 只用于主行动 / 风险提示与 `:focus-visible` 焦点环 |
| 字体 | 全站唯一字体 Maple Mono CN（圆角等宽，汉字 1.0 em、拉丁 0.6 em，含 400 / 700 / 900 / 斜体四套字形；400/700 覆盖 GB 2312 两级共 6763 字，900/斜体覆盖一级 3755 字）：`--po-font-display`、`--po-font-body`、`--po-font-mono` 都指向同一个 `--po-font-stack`；中文按 `unicode-range` 切片懒加载，GB 2312 一级之外的生僻字回退系统中文字体。不要再引入第二套字体或硬编码 `Consolas`、`Georgia`、`Inter` 等等宽 / 衬线栈 |
| 暗色 | `[data-theme='dark']` 下自动切换「夜航」令牌，保持同样的描边与硬投影语言；投影颜色沿用 `--po-ink`（米白），让所有组件在深色底上共享同一套可见投影 |
| 动效 | 120–260ms；`prefers-reduced-motion` 下全部关闭 |

## 2. 令牌（`web/src/styles/theme-variables.css`）

新代码只使用 `--po-*`；`--ink-*` / `--theme-*` / `--admin-*` 是映射到新值的旧别名（存量页面自动换肤），不要在新代码里使用。

```
--po-bg #fff9ee       --po-bg-deep #f9f2e3   --po-paper #ffffff
--po-ink #14284b      --po-ink-soft #4f6282  --po-ink-faint #70819f
--po-line #d9e2f1     --po-line-mid #c0cde2
--po-accent #2f6bff   --po-accent-soft #e7f1ff
--po-coral #ff7a6b    --po-coral-deep #da4339   --po-on-bright #14284b   --po-on-coral #14284b
--po-sun #ffc94d      --po-sun-soft #fff0b8   --po-sky #dcebff
--po-peach #ffe3d6    --po-mint #bff0d8      --po-lilac #e6dcff
--po-good #018451     --po-warn #a56d1c      --po-danger #c1332f
--po-shadow-card 1px 1px 0 var(--po-ink)      --po-shadow-hard 2px 2px 0 var(--po-ink)
--po-shadow-soft 1px 1px 0 var(--po-ink)      --po-shadow-pop 2px 2px 0 var(--po-ink)
--po-shadow-modal 4px 4px 0 var(--po-ink)     --po-shadow-hover 3px 3px 0 var(--po-ink)
--po-focus-ring 0 0 0 3px coral@34%
--po-r-lg 24px / --po-r 14px / --po-r-control 16px / --po-r-button 15px / --po-r-sm 10px
```

多主题新增的令牌（2026-10-25）：`--po-primary / -hover / -pressed / -soft / -soft-hover / -soft-pressed`、`--po-on-primary`、`--po-edge` / `--po-edge-strong`、`--po-gold` / `--po-gold-soft`、`--po-border` / `-hover` / `-focus` / `-disabled` / `-thin` / `-hair`、`--po-weight-display` / `-strong` / `-bold`、`--po-shadow-tilt` / `-tilt-hover` / `-mark`，以及 `--po-success-soft` / `-warning-soft` / `-danger-soft` / `-info-soft`。纸上海洋的默认值与上表完全等价；商务风格由 `themes.ts` 覆盖。

## 3. 已经完成的全局层（不要重复实现、不要修改）

| 文件 | 职责 |
| --- | --- |
| `web/src/styles/theme-variables.css` | 全部令牌 + 旧变量别名 + 暗色「夜航」 |
| `web/src/styles/paper-ocean.css` | 基础排版、画布、焦点环、滚动条、`.po-*` 组件类、Naive UI 全站换肤、存量页面骨架兼容、响应式 |
| `web/src/config/theme.ts` | 纸上海洋的 Naive UI `GlobalThemeOverrides`（卡片 2px 描边、按钮硬投影色、输入 2px 描边、表头、菜单选中态、弹窗、分页、开关…） |
| `web/src/config/themes.ts` | 4 套风格的调色盘注册表与 `--po-*` 映射（唯一色值来源） |
| `web/src/config/businessTheme.ts` | 商务风格的 Naive UI 覆盖工厂（1px 发丝边、实心主按钮、收敛圆角） |
| `web/src/styles/business-skin.css` | 商务皮肤层，仅在 `[data-skin='business']` 生效；必须在 `paper-ocean.css` 之后加载 |
| `web/src/stores/theme.ts` | 明暗模式（月白 / 玄夜 / 跟随系统）+ 风格的持久化与派生；`themeStyle` 与旧明暗数据回落 |
| `web/src/main.ts` / `App.vue` / `index.html` | 样式加载顺序、首屏防白闪、加载态、根节点 `data-theme` / `data-style` / `data-skin` |

因此：**页面里不要再手写颜色、阴影、圆角**；只在需要偏离全局规则时用 `--po-*` 变量。

## 4. `.po-*` 组件类速查

原型 `ui.css` 中的类已加 `po-` 前缀避免与 Tailwind / Naive UI 冲突，权威实现见 `paper-ocean.css`。

| 原型类 | 项目类 | 用途 |
| --- | --- | --- |
| `.page-head` / `.kicker` | `.po-page-head` / `.po-kicker` | 页头（后台仅保留指标与右侧动作，页面标题由顶栏承担；记录详情保留 kicker / 标题 / 描述） |
| `.card` | `.po-card`（`--flush/--hard/--cream/--accent/--sun/--dashed/--interactive`） | 内容卡；`--interactive` 提供参考站式上浮硬投影 |
| `.stat` `.stat--featured` | `.po-stat` `.po-stat--featured` | 指标卡（值 `__value`、脚注 `__foot`、图标 `__icon`） |
| `.btn` `.btn--primary` `.btn--dark` `.btn--ghost` | `.po-btn` + `--primary/--dark/--ghost/--danger/--sm/--block` | 贴纸按钮（Naive 按钮已自动获得同类样式） |
| `.pill` `.pill--sun/--sky/--outline/--dot` | `.po-pill` + 同后缀 | 状态胶囊 |
| `.table` | `.po-table`（外层 `.po-table-wrap`） | 轻量表格；管理端列表统一使用 `components/Table/DataTable` 封装（内部仍为 `n-data-table`） |
| `.field` `.input` `.select` `.textarea` | `.po-field` `.po-input` `.po-select` `.po-textarea` | 原生表单控件 |
| `.grid` `.grid-2/3/4` `.stack` | `.po-grid--2/3/4` `.po-grid--sidebar` `.po-stack` | 栅格 / 纵向堆叠 |
| `.toolbar` `.segmented` `.chip` | `.po-toolbar` `.po-segmented` `.po-chip(-row/--sm)` | 筛选条 / 分段控件 / 选项片 |
| `.avatar` `.sticker` `.delta` `.note` `.empty` `.timeline` `.kv` `.list` `.progress` `.code-block` | `.po-avatar` `.po-sticker` `.po-delta` `.po-note` `.po-empty` `.po-timeline` `.po-kv` `.po-list` `.po-progress` `.po-code-block` | 其余原子 |
| — | `.po-search-box` `.po-icon-btn` `.po-form-grid` `.po-divider` | 搜索框 / 顶栏图标钮 / 两列表单 / 分隔线 |
| 原生 `title` 气泡 | `v-tooltip` + `.po-tooltip` | 主题悬浮提示（纸面底 + 2px 墨边 + 硬投影，140ms 延迟，视口边缘自动翻转），替代浏览器原生气泡 |

## 5. 页面改造清单

每个页面按「结构对齐原型 → 视觉使用新语言 → 暗色与响应式自检」三步走。原型只作参考，**不复制 mock 数据与后端逻辑**，保留现有路由、API、权限与 i18n 键。

| 模块 | 原型参考 | 页面 |
| --- | --- | --- |
| 壳层 / 登录 | `index.html`、`login.html` | `AdminLayout`、`Header`、`Sidebar`、`Tabs`、`Footer`、`views/login.vue`、404 / 回调页 |
| 工作台 | `dashboard.html` | `modules/home/views/index.vue` |
| 审批 | `approval.html` | `approval/views/{todo,handled,submitted,cc,monitor,detail,definition}` 及共享组件 |
| 权限 | `users.html`、`roles.html`、`organization.html` | `authorization/views/{user,role,permission,module,route,tenant,organization}` 及其弹窗 |
| 系统配置 | `settings.html`、`notifications.html` | `systemManagement/views/{configuration,data-dictionary,data-dictionary-data,notifications,numbering,schedule,storage}` |

### 页面结构范式

```vue
<template>
  <div class="po-page">
    <header class="po-page-head">
      <div class="po-page-head__actions">
        <n-button type="primary">{{ t('Common:Create') }}</n-button>
      </div>
    </header>

    <section class="po-grid po-grid--4">…po-stat…</section>

    <section class="po-card po-card--flush">
      <div class="po-card__head">…</div>
      <n-data-table … />
    </section>
  </div>
</template>
```

后台页面标题由顶栏统一展示，页头不再重复 kicker / h1 / 描述；记录详情页可加 `.po-page-head--record` 保留记录标题。

## 6. 硬性规范

1. **只改自己写入范围内的文件**；全局样式、主题配置、`main.ts`、`App.vue`、`index.html` 由 Lead 维护，不要修改。
2. **i18n**：优先复用已有键；确需新增时只写自己获配的语言包文件，中英两份同时补齐，键格式 `Namespace:PascalCase`；不得出现硬编码文案（`title` 等后端数据除外）。
3. **暗色**：只用 `--po-*` / 现有语义变量，不写死 `#fff`、`#000`；暗色下描边与文字必须有足够对比。
4. **响应式**：≥1280 完整布局用 `po-grid--4`；≤1080 收为 2 列 / 单列；≤640 页头纵向、操作按钮整行、表格改卡片列表（沿用页面既有 `useResponsive` 逻辑）；后台页头不再重复顶栏已展示的页面标题与描述（记录详情页除外），桌面与窄屏均只保留统计指标与页头操作。
5. **可访问性**：交互元素必须有可见 focus（已全局 `:focus-visible`）；图标按钮加 `aria-label`；状态不能只靠颜色，需配文字 / 图标。
6. **Naive UI**：优先用既有组件，全局换肤已生效；仅在原型有专属版式时使用 `.po-*` 标记，不新引入第三方 UI 库。
7. **禁止模糊阴影**（`box-shadow` 带 blur 半径）与渐变光斑（旧登录页的 orb 需删除）。
8. 文件行尾统一 **CRLF**（`*.sh` / `Dockerfile` / `Jenkinsfile` 用 LF），交付前 Lead 会统一校验。
9. 不提交 Git，不改后端，不动 `docs/design/ui/prototypes/assets`（设计源只读）。

## 7. 验证

```bash
cd web
pnpm type-check
pnpm lint:check
pnpm test:unit
pnpm build:production   # 打包 + CSS 体积预算
```

浏览器自检：`http://localhost:5173`（dev server 已运行）。重点检查：登录页、工作台、审批待办、用户列表、系统配置与通知，浅色 / 深色各一遍，1440 / 1024 / 390 三档宽度。

## 8. 交付说明要求

每个页面的交付说明需列出：改动的文件、原型对齐点、复用的 i18n 键（与新增键）、暗色与响应式验证结果、未完成项及原因。

## 9. 落地状态（2026-10-22）

以下为历史记录，只作追溯，不代表当前仓库能力。

- **全局层**：`web/src/styles/theme-variables.css`（`--po-*` 令牌 + 旧 `--ink-*`/`--theme-*`/`--admin-*` 别名）、`web/src/styles/paper-ocean.css`（排版、纸底点阵、`.po-*` 组件类、Naive UI 全站换肤、存量页面骨架兼容、响应式与 reduced-motion）、`web/src/config/theme.ts`（深浅两套 `GlobalThemeOverrides`）、`web/index.html` 防闪屏底色、`web/src/App.vue` 启动态。
- **壳层与登录**：`AdminLayout` 改为「侧栏整高 + 右侧顶栏/标签页/内容」结构，侧栏 252px（折叠 76px，≤640px 折叠隐藏、展开为抽屉），品牌块胶囊化；`Header`/`CustomMenu`/`CustomTabs`/`DensityToggle`/`ThemeToggle`/`LocaleSwitcher` 统一；登录页与首次初始化页共用左侧品牌主色面板 + 右侧奶油纸面表单栏的两栏布局，≤960px 收起为顶部品牌条，深色同步。
- **业务模块**：工作台（指标行 + 趋势/结构双栏 + 审批摘要）、审批中心（四个列表页 + 详情 + 流程定义/监控 + 状态标签与时间轴）、权限中心（用户/角色/权限/模块/路由/租户/组织架构 + 个人资料/安全/设置）、系统配置（配置中心/字典/字典数据/编号/计划任务/存储/通知）均已按 `.po-*` 范式改造。
- **共享组件**：`LoadingOverlay`/`ProgressBar`/`SearchPanel`/`Card` 系列的模糊阴影与玻璃拟态已移除；`ink-wash.css` 仅保留历史动效类。
- **验证**：`pnpm type-check`、`pnpm lint:check`、`pnpm test:unit`、`pnpm build:production` / `pnpm check:bundle-budget`、Chromium 登录 E2E 均作为交付门禁；视觉自检覆盖登录页、工作台、审批、权限与系统配置，浅色与深色、1440/1024/390 三档宽度。
- **静息态投影收敛（2026-10-22）**：`--po-shadow-card` 2px→1px、`--po-shadow-hard`/`--po-shadow-pop` 3px→2px、`--po-shadow-modal` 6px→4px，并新增 `--po-shadow-hover`（3px）承接悬停态；按钮、统计、分段控件、品牌标记、语言切换器与归档弹层的静息投影同步收小，`:hover` 反馈强度保持不变。
- **暗色投影统一（2026-10-22）**：暗色「夜航」不再单独写深色投影，所有 `--po-shadow-*` 继续引用 `--po-ink`，在深色下自动反相为米白；卡片、弹层、弹窗与按钮共享同一套可见硬投影。
- **Tooltip 气泡配色（2026-10-22）**：`theme.ts` 的 Tooltip peers 由原先的反相墨底 / 米白底改为 `--po-paper` 底 + `--po-ink` 文字与描边，浅色、深色都跟随主题纸面，不再出现“浅色深气泡、深色亮气泡”的相反配色。
- **按钮描边统一（2026-10-22）**：补回 Naive `secondary` 按钮的 2px 墨蓝描边（该版本 `secondary` 不渲染边框元素，主题里的描边变量不会生效），错误/警告/成功/信息类型改用 `currentColor` 保留语义色；`.po-icon-btn`、分页轨道、搜索弹窗关闭/清空、归档关闭与重试统一为 2px 墨蓝描边。纯文字操作（回复/点赞/加载更多）与语言切换、选项片等分段控件内部继续保持无描边。
- **登录两栏与首次初始化（2026-10-24）**：登录页恢复品牌叙事栏；新增共享组件 `AuthBrandPanel`（主色底、白色/深色反相文字、光环与点阵装饰、品牌标记），登录页与 `/setup` 初始化页右侧表单栏使用 `--po-bg` 奶油纸底。初始化页覆盖管理员账号密码、站点名称、默认语言、默认主题、开放注册、登录失败上限与锁定时长；提交后切换为进度视图，按 `GET /api/admin/setup/progress` 的阶段和百分比展示校验设置、创建管理员、创建系统账号、写入基础数据与写入站点配置五个步骤，完成后进入登录页；站点默认语言/主题由后端 `client-localization` 下发，仅在用户未手动选择时应用。验证：`vue-tsc --build`、改动文件 ESLint 与 Vite 生产构建通过。
- **登录品牌标语改为后端下发（2026-10-28）**：登录页左侧品牌叙事的主标语与二级描述不再由前端语言包维护，改为 `Layered/zh-Hans.json`、`en.json` 的 `Auth:BrandHeadline`（欢迎使用 SimApp / Welcome to SimApp）与 `Auth:BrandDescription`，经 `ClientLocalizationKeys` 白名单随 `GET /api/admin/client-localization` 下发。按用户要求，登录页品牌面板同时移除 kicker 胶囊与四个标签（`Auth:BrandKicker`、`Auth:BrandHighlight*` 前端文案一并删除），`AuthBrandPanel` 的 `kicker`/`highlights` 改为可选 prop，初始化页仍使用 `Setup:*` 标签；主标语、二级描述的字号与布局不变。验证：i18n 一致性单测、`Sim.Admin.Domain.Shared`/`Sim.Admin.Application` 构建与改动文件 ESLint 通过。
- **登录/注册卡片切换（2026-10-28）**：登录页与 `/register` 复用同一两栏布局和卡片容器，开放注册时登录卡片显示「立即注册」，`/register` 可直接访问；两种模式通过叠放卡片的方向性滑动切换（离场卡片淡出、入场卡片自侧向滑入，约 180ms；`prefers-reduced-motion` 下关闭位移与过渡），注册成功切回登录并预填用户名。注册表单含用户名、必填昵称、可选邮箱、密码与确认密码，前端按 8 位+大小写字母+数字+特殊字符校验，昵称查重/用户名占用/注册关闭等后端错误按错误码映射为本地化提示。入口由 `siteDefaults.registrationEnabled` 控制，登录页挂载时重新读取 `client-localization`，后台修改开放注册后重新进入登录页即生效。系统配置页的 `security.registration-role-codes` 在有 `system:role:manage` 时渲染非内置角色多选，否则保留文本配置。验证：`vue-tsc --build`、改动文件 ESLint、471 项 Vitest 与生产构建通过；入口 JS 1.57 MiB 超 1.53 MiB 预算为既有基线问题。
- **初始化分步向导（2026-10-24）**：`/setup` 表单拆为管理员账号、站点信息、安全策略三步，页头显示当前步标题与「第 n / 3 步」，底部「上一步 / 下一步」按步校验（Naive 只校验已挂载字段），最后一步改为「开始初始化」；步骤切换用方向性滑动淡出淡入（前进向右、后退向左），步骤内容固定最小高度避免按钮位置跳动。`AuthBrandPanel` 新增 `progress` 插槽与 `showProgress` 开关：第一步宽屏左栏保留「站点初始化」标题、简介与标签，进入第二、三步时整个标题区退场、设置进度（按已完成步数显示 33% / 67%）入场，返回第一步时反向换回。点「开始初始化」后设置进度先到 100%，收到后端首个进度快照后切换为「数据初始化中」，按进度接口下发的完整步骤列表（准备、管理员、系统账号、12 个种子步骤与收尾）逐项展示，列表限高约 6 行并在内部滚动，激活步骤变化时自动滚入视野；右侧表单同时替换为转圈状态卡（当前步骤 + 百分比），完成后转圈变为对勾与「初始化完成」，停留约 2.4 秒再进入登录页，失败时回到表单第三步。≤960px 左栏收起，数据初始化状态直接由右侧状态卡承担。验证：`vue-tsc --build`、改动文件 ESLint 通过，Chromium 截图覆盖浅色 / 深色、1440 与 390 宽度、前进与后退过渡、校验失败与初始化中状态。
- **Tooltip 尖角描边（2026-10-22）**：`paper-ocean.css` 为旋转 45° 的 `.n-popover-arrow` 按 `[v-placement]` 四个朝向补齐外缘两条边的 2px 墨蓝描边，并移除箭头自带的模糊阴影；气泡尖角与面板边框连成一体，不再出现没有边框的白色三角形。
- **统一分段切换组件（2026-10-22）**：新增 `SegmentedSwitch`（选项数组驱动、等宽墨色滑块、悬停跟随并在离开后复位、`hoverEffect` 可关闭、0.35s 弹性）；语言切换 `LocaleSwitcher` 改为复用该组件，用户 / 角色 / 权限 / 模块 / 路由的表格-卡片切换也改用同一组件，并从工具栏移到列表左下角。切换器与分页控件同高（约 34px）：表格模式 `bottom: 16px` 与分页同一水平线，卡片模式 `bottom: 20px` 对齐卡片分页；同时修复 `.po-icon-btn` 内 `n-icon` 包裹层（14px）与强制 18px SVG 尺寸不一致造成的图标左上偏移，主题、通知等圆形按钮图标恢复居中。
- **列表与树形细节修复（2026-10-22）**：①侧栏品牌区去掉底部 14px 不对称内边距，内容在 60px 品牌块内垂直居中；②表格卡去掉 `n-data-table` 自带的外框（`bordered` 的 wrapper 1px 与全局根节点 2px 一并置 0，外框统一由卡片 2px 描边负责），并在 `.n-card` / `.authz-table-card` 内容之上重绘 2px 描边，四角与边线粗细一致；③树形展开器用主题化 2px 细线 chevron 替代默认实心三角，展开时向下；组织架构左侧补文件夹 / 组织图标、引导线与 44px 搜索框，并修复列向 flex 下搜索框被拉高的问题；④侧栏菜单子项右侧箭头复用同一套 2px 细线 chevron，收起朝右、展开朝下，替换默认实心下箭头。
- **列表页头收敛（2026-10-22）**：授权中心用户 / 角色 / 权限 / 模块 / 路由 / 租户页把「批量删除 / 新建」从页头移到工具栏右侧（视图切换之后），统计卡片改为标题右侧的紧凑指标条（label / value / foot，首项数值用 `--po-accent` 强调），移除独立的四卡统计行；≤640px 时指标条按两列换行、工具栏动作整行右对齐，释放的纵向空间留给表格 / 卡片列表。
- **列表页分页与高度（2026-10-22）**：授权中心用户 / 角色 / 权限 / 模块 / 路由五个列表页的表格操作列按钮间距统一为 8px，操作列宽度 190px→220px；`.po-cell-actions` 去掉对 `.po-table` 祖先的依赖，避免按钮贴在一起。分页内的 `n-input` / `n-base-selection` 固定 34px 高、10px 圆角，修复卡片模式「每页条数 / 跳页」继承 50px 表单高度导致的错位与遮挡；表格分页补齐每页条数与快速跳页。五个页面增加 `.po-page--fill`，在 ≥1025px 时锁定为标签页剩余高度，表格卡片 `flex:1` + `flex-height`、卡片列表 `flex:1/min-height:0`，分页固定在底部，页面不产生外层滚动条。
- **列表表格交互补齐（2026-10-22）**：评论与审批列表的每个数据列显式开启 `resizable`（该版本 Naive 仅支持列级 `resizable`，表级属性不生效），操作列保持固定不参与拖拽；刷新按钮从页头移入工具栏，流程定义页补充名称 / 编码客户端搜索并把「新建流程」一并移入工具栏；`.n-pagination .n-pagination-item` 统一为 34px，与每页条数下拉、跳页输入同高。
- **输入焦点投影（2026-10-22）**：输入框、下拉、自定义搜索与评论输入框的聚焦效果与按钮对齐——整体上浮 2px 并加深硬投影（`translateY(-2px)` + `--po-shadow-hover` / `3px 3px 0 ink`），过渡 0.12s；不再使用珊瑚外扩焦点环或贴边粉色光晕。复选 / 单选保留硬投影反馈但不位移，避免标签文字错位；二元开关不再按钮式硬投影（鼠标悬停、点击与失焦后都不会留下「按钮悬停」的错觉，仅键盘 `:focus-visible` 保留硬投影作为可见焦点），状态反馈由滑块位移、轨道变色和按下时的滑块拉伸承担；`.n-input__state-border` / `.n-base-selection__state-border` 的焦点阴影清空，保留 2px 墨蓝描边。多个控件组成的 `n-input-group` 改为整组上浮，组内控件不再单独位移且不重复投影，避免相邻 2px 描边在接缝处裂开。
- **开关样式统一（2026-10-22）**：修复二元开关的几何与配色——Naive 的 `--n-offset` 按无描边轨道计算，叠加 2px 墨蓝描边后滑块会偏上、底部贴边，现按内容盒重新垂直居中（46×26 轨道 / 18px 白钮 / 2px 描边 / 1px 硬投影）；轨道内文字由次级色改为墨蓝并加粗。
- **按钮与输入统一（2026-10-22）**：`.po-btn` 与 Naive Button 的实心按钮统一为静止态 `--po-shadow-soft`（1px）、悬停 `--po-shadow-hover`（3px）、按下无阴影；主行动不再单独使用底边投影，`secondary` 不再使用 inset 描边投影。搜索 / 输入 / 下拉在静止态增加 1px 投影，聚焦时让位给全局焦点环。
- **统一列表表格组件（2026-10-22）**：新增 `web/src/components/Table/DataTable.vue`（配合 `DataPagination.vue` 与 `createActionColumn`），把复选列、固定操作列、表格/卡片切换、页码信息、列/行分割线、斑马纹、列宽拖拽与列表外框收敛到同一组件，并通过 `$attrs` 与动态插槽透传 NDataTable 的其余属性和事件；各项能力均可独立开关（访问日志关闭复选、通知与组织成员表不启用视图切换）。访问日志页统计信息移入页头，移除筛选区独立的「共 N 条」行，刷新 / 搜索 / 重置与筛选条件合并到同一行并右对齐；流程定义等页面恢复数据行分割线（组件默认 `bottom-bordered`，页面不再显式关闭）。授权 6 页、审批 6 页、评论管理、访问日志、数据字典、通知与组织架构成员表均已迁移。列宽调整改为组件内受控拖拽：渲染前按容器宽度补足列宽，拖动时保持总宽不变、左侧边缘固定并与光标 1:1 跟随；复选列收窄为 40px 且复选框水平垂直居中；模块列表的头像改为渲染模块 `icon`（缺省 `carbon:Application`），所有首字头像修正为 grid 居中。
- **表格卡双层描边修复（2026-10-22）**：换肤时全局 `.n-data-table` 新增的 2px 根节点描边会与表格卡的 2px 描边叠成 4px，且表格 16px 圆角小于卡片 24px 圆角、又被卡片 `overflow: hidden` 裁切，四边呈一条粗线、四角却出现内层弧线与缺口。`.authz-table-card > .n-data-table` 现统一 `border: 0` / `border-radius: 0` / `box-shadow: none`，外框完全由卡片（24px 圆角 + 2px 墨蓝描边 + `::after` 重绘）负责，与数据字典、通知、访问日志等 `po-table-card` 的既有写法保持一致。验证：临时页 Playwright 实测表格 `border: 0px none`、卡片 `2px solid`，1440px 下四边 2px、四角单一弧线且无缺口；仅样式变更，无接口、数据与类型变更。企业级建设清单、当前基线、风险优先级不变。
- **内容密度切换（2026-10-22）**：顶栏新增 `DensityToggle`（小 / 中 / 大三档，复用 `SegmentedSwitch`），顺序调整为「密度 → 语言 → 主题」，≤680px 收起密度开关并优先保留语言 / 主题 / 账号入口。密度只作用于垂直方向：`theme-variables.css` 的 `--po-density-*` 令牌分别控制页面上下内边距、页面纵向间距、堆叠/网格行距、筛选区上下内边距与控件高度、表格数据行上下留白与表格底栏内边距；水平内边距、控件宽度、列宽与横向间距三档保持一致。筛选区覆盖 `.po-toolbar`、访问日志 `.log-filters`、文章树 `.article-tree-view__search` 与组织架构 `.authz-tree-search`：搜索框与 Naive 输入 / 下拉 / 日期框高度 46 / 40 / 34px（组织架构紧凑搜索 40 / 35 / 30px）；按钮高度固定 40 / 34 / 28px，本轮整体收紧时不再下调。三档由宽到紧排列，「大」档为最宽松一档（较历史基线略收紧），「中」「小」继续逐级收紧；未设置 `data-density` 时使用「大」档。档位由 `stores/density.ts` 持久化（`localStorage` 键 `po-density`）并同步为根节点 `data-density`。验证：`vue-tsc --build` 通过、i18n 9 项单测通过；临时页 Playwright 实测数据行高 31 / 37 / 47px（表头恒 44px）、页面纵向间距 6 / 11 / 16px、页面上下内边距 13 / 20 / 26px、筛选区上下内边距 6 / 10 / 14px、搜索框与输入 / 下拉 34 / 40 / 46px、按钮 28 / 34 / 40px；三档下页面左右内边距 30px、工具栏左右内边距 16px、搜索框与筛选框宽度、按钮宽度、单元格左右内边距 20px 与表格宽度 1380px 均不变；初始选中「大」，刷新后保持档位，控制台无错误。
- **后台移动端页头收敛（2026-10-22）**：后台窄屏（≤768px）顶栏已展示当前页标题，内容区 `.po-page-head__main` 的大标题与描述不再重复渲染；统计指标条与页头操作保留，两者都不存在的空页头整体收起，避免在 `.po-page` 中留下多余行距。审批实例详情等记录页用 `.po-page-head--record` 保留记录标题；该规则只作用于 `.admin-shell`。
- **头像编辑弹窗滚动条修复（2026-10-23）**：`.n-card` 的边框重绘层 `::after`（`inset: -2px`）在卡片自身成为滚动容器（`overflow: auto`）时会在右、下各多出 2px 可滚动区域，导致头像编辑弹窗常驻一条竖向/横向滚动条，悬停按钮重绘时表现为滚动条闪烁或消失。弹窗卡片改为 `display: flex` + `max-height` 且不再滚动，滚动交给 `.n-card-content`（`flex: 1 1 auto; min-height: 0; overflow-y: auto`），标题栏固定；注意 Naive 内部类名是单下划线的 `.n-card-header` / `.n-card-content`。验证：Playwright 在 1440×900 / 1366×768 / 1280×720 下卡片 `overflow: visible`、无滚动条（`offset - client` 仅剩 4px 边框），内容区无需滚动；1440×520 下内容区按需滚动且标题固定，hover 前后尺寸不变，控制台无错误。
- **标签条底线与通知服务操作栏（2026-10-23）**：`CustomTabs` 使用 Naive `type="bar"`，bar 类型不会绘制导航底线，而原有 2px 墨蓝底线选择器只匹配 `--line-type`，导致标签条只剩活动页的珊瑚指示条、整条底线缺失。现改为对 `.n-tabs-nav-scroll-content` 统一描 2px 墨蓝底线，底线正好填满 48px 标签条，活动指示条落在底线上、不再悬空或被容器裁切。通知服务页移除页头刷新按钮，刷新并入「通知配置 / 模板 / 事件规则 / Webhook Sites」四个标签页的操作栏（主按钮左侧）；操作栏与内容区之间补 `--po-density-page-gap` 间距。`n-tabs-pane-wrapper` 的 `overflow: hidden` 会裁掉最右列卡片与表格卡的 1px 硬投影，配置卡片网格、已框选表格与投递记录卡片统一向右/下留出 3px 投影空间。定时任务 / 业务编号 / 数据字典把「新建」从页头移入工具栏（刷新之后），字典数据页保留记录标题与「返回列表」，只把「新建字典项」移入工具栏。验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Playwright 实测标签内容底部描边 `2px solid rgb(20, 40, 75)`、操作栏与卡片间距 16px（大档）、最右卡片 right=2367 而裁切边界 2370、表格卡与裁切边界留 3px，四个页面工具栏均含刷新与新建且页头无动作，控制台无错误。
- **搜索框图标居中（2026-10-23）**：`.po-search-box` 内的 Naive 图标包裹层默认 14px，而全局样式把 SVG 固定为 18px，图标向右下溢出约 2px，视觉上未与输入框垂直居中。现同步 `.po-search-box .n-icon` 为 18px 的居中 flex 容器（`inline-flex` + `align-items/justify-content: center`），图标中心与搜索框中心一致。验证：Playwright 在 roomy / standard / compact 三档实测图标与搜索框中心同 y（189 / 176 / 162），截图确认居中。
- **输入光标对齐与配色（2026-10-23）**：密度档位只压缩 `.n-input` 外壳，内层 `.n-input__input-el` 仍沿用主题的 50px 行高，导致紧凑/标准档位下工具栏输入框的文字与插入光标整体偏下（紧凑档 34px 外壳内出现 50px 行盒，光标贴着下边框）。现让 `.po-toolbar` / `.log-filters` / `.article-tree-view__search` 内的单行输入同步 `height` 与 `line-height` 为 `--po-density-toolbar-control-h`，分页跳页输入同步为 34px，三档下输入元素与外壳等高并垂直居中；全局 `input` / `textarea` / `[contenteditable='true']` 的 `caret-color` 改为 `--po-coral`。光标宽度由浏览器原生控制，CSS 无法调整。验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Playwright 在 roomy / standard / compact 三档实测输入与外壳同为 46 / 40 / 34px、行高一致、caret 颜色 `rgb(255, 122, 107)`，分页跳页输入同为 34 / 34px，光标与文字垂直居中。
- **组织架构操作收敛（2026-10-23）**：移除组织架构页头，将「刷新 / 新建根组织」移入左侧树面板的搜索框下方，形成「搜索 → 操作 → 树」的纵向顺序，操作行与搜索框、树各保持 12px 间距；按钮使用 `size="small"`（34px）以适配窄侧栏，≤1080px 单列时保持自然宽度、不溢出。验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Playwright 1600px 下页头不存在、按钮位于搜索框下方且均在树面板内、按钮高 34px（52 / 90px 宽），600px 下无横向溢出。
- **系统配置操作栏（2026-10-23）**：系统配置页新增 `.po-toolbar` 操作栏并移除页头，把「刷新 / 新建系统配置」移入操作栏右侧；≤640px 时操作栏纵向堆叠、按钮整行。同时把激活分组卡片的蓝色装饰条由 `left: -2px`（压在卡片描边外侧）移到卡片内侧近边缘（`left: 6px`），并以 `top: 18% / bottom: 18%` 自适应卡片高度、垂直居中，不再使用固定长度；蓝条完整落在卡片内部且不遮挡内容。验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Playwright 1600px 下页头不存在、操作栏含刷新与新建、与分组区间距 16px，600px 下按钮等分且无横向溢出；118px 高卡片蓝条上下各缩进 20.5px、总高约 73px，截图确认靠近内侧边缘且垂直居中。
- **文件存储操作区收敛（2026-10-23）**：移除文件存储页头，把「刷新 / 保存」移入「存储概览」卡片头部右侧，替换原「默认 · {provider}」胶囊（默认提供方仍由下方下拉与左侧导航的「默认」胶囊表达）。≤640px 时动作整行等分。验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Playwright 1600px 下动作右对齐、页头与冗余胶囊均不存在，600px 下按钮等分且无横向溢出。
- **侧栏菜单末项描边裁切修复（2026-10-23）**：Naive 的 `.n-menu .n-submenu .n-submenu-children` 为折叠动画固定 `overflow: hidden`，其底边与末个子项齐平；选中末项时 `2px 2px 0 ink` 硬投影会伸出 2px 并被容器裁掉，导致每个展开分组最后一项的底边只剩单线。侧栏为子菜单容器补 3px 下内边距（`.n-menu .n-submenu .n-submenu-children`），投影落在裁切范围内；顶层菜单仍有 `.n-menu` 自身 6px 下内边距兜底。验证：Playwright 以模拟登录进入后台并展开「授权管理」选中末项，容器底部由 361.33px 增至 364.33px、末项底部 361.33px，下投影完整显示；`vue-tsc --build` 通过、改动文件 ESLint 0 警告。
- **统一主题滚动条（2026-10-23）**：新增 `--po-scrollbar-*` 令牌，Blink / WebKit 原生滚动容器统一为 10px 胶囊滑块（纸面填充 + 2px 墨色描边、透明轨道、隐藏箭头），悬停填充强调色，深色自动反相；Firefox 通过 `-moz-appearance` 特性查询使用 `thin` + 主题双色。Naive `n-scrollbar` 保持默认几何，仅调整滑块 / 导轨颜色；数据表格内部的 Naive 滚动条单独放大到 10px，并套用同一胶囊描边 / 纸面填充样式。Naive 主题颜色必须使用字面量（CSS 变量会导致布局渲染异常）。验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Chromium 实测滚动条宽 10px，浅 / 深色胶囊与描边均正确，控制台无错误。
- **列表页窄屏吸顶工具栏（2026-10-23）**：`.po-page--fill > .po-toolbar` 使用 `position: sticky; top: 8px; z-index: 10`，并用 `0 -8px 0 var(--po-bg)` 阴影遮住滚入间隙的卡片。≥1025px 列表页高度锁定、卡片/表格内部滚动；窄屏由外层 `tabs-content` 滚动，工具栏在分割线下方 8px 吸顶，卡片继续滚动。验证：Playwright 600×900 视口模块管理卡片模式下，滚动 600 后工具栏 top 120、内容区顶部 112，间隙 8px。
- **授权中心卡片模式统一（2026-10-24）**：新增共享 `useInfiniteList`，用户 / 角色 / 权限 / 路由与模块管理统一为 232px 高、375px 宽（黄金比例）卡片，按屏幕宽度 auto-fill 换列；卡片模式无分页页脚，滚动到底通过 IntersectionObserver 追加下一页；视图切换移至各页工具栏（宽屏显示、≤768px 隐藏），表格模式保留分页。验证：Playwright 1280×900 四个页面卡片均 375×232、2 列、无溢出/页脚、工具栏切换可见、哨兵存在；600×900 单列、切换隐藏；类型检查与 ESLint 通过。
- **窄屏模块卡片细节（2026-10-23）**：模块管理窄屏将「新建模块」文本按钮替换为与搜索框同高的圆形加号按钮（搜索框右侧 10px，宽屏保留文本按钮）；模块卡片 padding 12px 14px、gap 7px、`.po-kv` 行距 4px；卡片模式取消分页页脚：`DataCardList` 滚动到底通过 IntersectionObserver 触发 `load-more`，模块页按已加载数量追加下一页；卡片固定 232px 高、375px 宽（黄金比），网格按 375px 轨道 auto-fill 随屏幕宽度换列并居中；表格 / 卡片切换移至模块工具栏，≤768px 隐藏。验证：Playwright 1280×900 实测 6 张 375×232 卡片、2 列、无页脚/浮动切换器、工具栏切换可见、网格可滚动；600×900 单列、无页脚、切换隐藏、哨兵存在；总量 6 < 页大小 10，追加逻辑已接入待大数据验证。
- **后台 404 内嵌布局（2026-10-25）**：后台未知地址原先落到布局外的顶层 404，整页替换掉侧栏与顶栏。现 `AdminLayout` 增加兜底子路由 `AdminNotFound`（`meta.tab: false`），`/admin/*` 未匹配时只在内容区渲染 404，保留侧栏、顶栏与标签栏；`404.vue` 新增 `embedded` 属性，内嵌形态去掉 `100dvh` 最小高度、独立画布与主题 / 语言切换，改用 `--po-density-page-pad-y` 页面内边距，非后台地址仍使用布局外的独立 404。验证：`vue-tsc --build`、改动文件 ESLint 0 警告、`iconLoader.test.ts` 42 项通过；Playwright（mock 会话与模块路由）实测 `/admin/blog/unknown-page` 的 404 与内容区同框（1188×776）且侧栏 / 顶栏 / 标签栏保留、1440 与 600 宽度无横向溢出、`/definitely-not-a-page` 仍为布局外 404。
- **工作台首屏高度收敛（2026-10-26）**：工作台趋势图 / 内容结构行原由内容撑高到 545px，最近文章 / 评论行随条目数变化。现为顶部 `.workspace-overview` 卡片锁定 300px（与设计红框一致），内容结构标题固定、分类列表 `flex: 1; min-height: 0` 内部滚动、常用标签块贴底且 `.po-chip-row` 只保留一行（高 26px、不换行、溢出裁切）；底部 `.workspace-feeds` 两卡锁定 300px，与顶行等高；`.po-list` 在卡片内滚动，并通过列表负外边距 + 等值内边距保留悬停底色铺满卡片内侧。验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Playwright（mock 工作台数据）1440×1000 下顶部两卡均 300px、趋势图 220px、分类区 128px（scrollHeight 338）、标签块底边与卡片内容底边对齐、6 个标签同一行且行高 26px；底部两卡均 300px、列表可视高 220px，文章列表内部可滚动 130px、评论列表可滚动 274px；文章行悬停底色 284–837px 与卡片内侧 282–839px 对齐（2px 边框）；600px / 390px 宽度下无横向溢出，深色主题正常。企业级建设清单、当前基线、风险优先级不变。
- **侧栏菜单滚动与系统设置停靠（2026-10-26）**：侧栏滚动内容原先只有 `min-height: 100%`，菜单区拿不到确定高度，整块内容由 sider 自带的 Naive 滚动条滚动，导致「系统设置」随内容滚走、菜单区自身的滚动始终不生效。现通过 `content-class` 把滚动内容改为 `height: 100%` 的纵向 flex，品牌区与页脚固定，菜单区 `flex: 1; min-height: 0` 独占剩余高度；菜单改用 `n-scrollbar` 独立滚动，并复用 `--po-scrollbar-*` 令牌做 10px 胶囊（2px 墨色描边、纸面填充、悬停强调色），sider 外层不再出现滚动条。侧栏菜单是本项目第二处放大到 10px 的 Naive 自绘滚动条（第一处为数据表格），与原生主题滚动条视觉一致。注意 scoped 样式不会落到 `n-scrollbar` 根节点，相关规则需从 `.admin-sidebar` 用 `:deep()` 穿透。验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Playwright 1440×430 下侧栏高 430px、页脚底边与侧栏底边重合（间距 0）、菜单区高 280px 且内部可滚动（scrollHeight 438）、外层滚动容器 scrollHeight 与 clientHeight 同为 430；滚动条实测 10px、`2px solid rgb(20, 40, 75)`、纸面白胶囊，深色主题反相为米白描边 + 深色胶囊；商务皮肤、76px 折叠态与 480px 窄屏抽屉下页脚同样贴底。企业级建设清单、当前基线、风险优先级不变。
- **租户列表表格填满高度（2026-10-26）**：租户管理页是列表页中唯一未接入统一骨架的页面——根节点缺少 `po-page--fill`，`DataTable` 也未开启 `flex-height`，表格卡只按数据高度渲染，横向滚动条贴在最后一行下方。现补 `po-page--fill` 与 `flex-height`，表格卡在 ≥1025px 占满标签页剩余高度，表体内部滚动，横向滚动条固定在同一张卡片的底部。验证：`vue-tsc --build` 通过；Playwright（mock 会话与模块路由）1440×900 下表格卡 546px、表体 498px、滚动条底边距卡片底边 6px；600×900 下卡片 535px、滚动条仍在表体底部且无高度塌陷。
- **定时任务新建弹窗滚动条修复（2026-10-28）**：新建弹窗卡片此前用 `.create-dialog { max-height; overflow: auto }` 让整张卡片滚动，而 `.n-card::after` 描边重绘层（`inset: -2px`）在卡片成为滚动容器时会向右/下各多出 2px 可滚动区域，表现为常驻横向滚动条，并在输入框聚焦（`.n-input--focus` 的 `translateY(-2px)` + 硬投影）与失焦之间闪动。现卡片保留 `max-height: calc(100vh - 32px)` 但不再滚动，滚动交给 `.n-card-content`（`min-height: 0; overflow-y: auto`），标题栏与页脚固定，内容区沿用统一 10px 原生主题滚动条；同步新增 `index.test.ts` 契约断言。验证：`vue-tsc --build`、改动文件 ESLint 0 警告、`index.test.ts` 2/2 通过；Playwright + Chrome（`--disable-features` 关闭覆盖式滚动条）加载真实页面（mock 定时任务接口）在 1440×900 / 1366×768 / 600×900 / 1440×1200 下卡片 `overflow: visible` 且无横向滚动，内容区 741/891（1440×900）按需滚动，标题栏与页脚在滚动前后位置不变（18/18、882/882），逐个聚焦全部输入控件与失焦后内容区 `scrollWidth/clientWidth`、`scrollHeight/clientHeight` 均稳定。企业级建设清单、当前基线、风险优先级不变。

- **WPF 侧栏改为 Web 同构手风琴菜单（2026-11-11）**：`CustomMenu` 由平铺 ListBox 重写为 n-menu 式树形控件（`CustomMenuNode` + `ItemsControl`）：顶层分组用 ToggleButton 展开/收起且同层只展开一个，叶节点与子项共用一组 RadioButton 单选，选中态沿用 SoftBrush + 2px `EdgeBrush` 描边 + 加粗；子项与顶层一样带 Segoe MDL2 图标，展开/收起指示统一为同一枚 16px 矢量 chevron（展开时 0.2s 缓动旋转 90° 并垂直居中）；子菜单复刻 Naive `fade-in-height-expand`：展开时按内容实测高度做 0.3s MaxHeight + 淡入动画，收起时收缩到 0，互斥展开时被收起的分组同样播放收起动画。侧栏去掉「我的工作 / 平台管理」标题与两个平铺菜单，按 Web 顺序改为 工作台 → 审批 / 系统管理 / 授权管理（子项与 Web 菜单同名同序）→ 个人资料 / 账号安全 / 通知中心；`AuthorizationWorkspace` / `SystemWorkspace` / `ApprovalWorkspace` 移除顶部模块 TabControl，改为 `ContentControl` + `ShowModule(key)` 懒加载，页面标题显示叶节点名称。移除与「审批 > 我的待办」重复的工作台审批待办页及其行内同意/驳回，工作台仍展示待办数量摘要。组件库新增导航菜单预览。验证：`dotnet build` 0 警告 0 错误；`--gallery` 实机截图核对分组互斥展开、子项选中与高亮；登录态页面切换需真实 API，未在本地验证。

- **WPF 表格按 Naive 默认外观重绘（2026-11-11）**：`CustomDataGrid` 去掉表格自身的 2px 描边与 16px 圆角，外框交给所在卡片；表头改为 `PageDeepBrush`（bgDeep）浅色底、13px Medium 字重与 1px 分隔线，数据行 42px 高、1px `LineBrush` 横线且不画竖线。`DataGridCheckBoxColumn` 的列级 ElementStyle 会覆盖隐式样式，因此 9 个复选列显式引用 `NaiveCheckBoxStyle`，由原生 Aero2 方框改为 18px Naive 方框。`ThemeManager` 按 `themes.ts` 补齐 bgDeep/line 令牌（`PageDeepBrush`/`LineBrush`）；登录会话与审批记录的时间列宽 140→152，避免尾字符截断；组件库 `--gallery` 新增数据表格预览。验证：`dotnet build` 0 警告 0 错误；`--gallery` 实机截图核对浅色表头、1px 行分隔线、复选列与 152px 时间列完整显示。企业级建设清单、当前基线、风险优先级不变。

- **WPF 表格操作列、排序与列宽手柄（2026-11-12）**：`ManagementPage` 末列新增「操作」列（编辑 + 定义动作 + 删除，按行按钮），列宽使用 `DataGridLength.Auto`，取本页最宽一行按钮组的实际宽度（`ActionsFixed` 仅保持末列与不参与拖拽；WPF 无右侧冻结列，冻结全部列会使表头与表体横向错位，已移除该实现）；复选列固定 44px 且不允许拖拽列宽；表头支持点击排序，`ManagementPage` 对行数据按字段做数字/日期/字符串比较，表头模板显示 ↑/↓ 指示器；所有表头补 2px 列宽拖拽指示线（`LineBrush`，悬停/拖拽变 `PrimaryBrush`）并保留 1px 竖分隔线，复选框列外的标题列同样有分隔线。表头样式改为组件库中的全局隐式 `DataGridColumnHeader` 样式（`Controls.Base.xaml`，移除 App.xaml 中的精简隐式样式），XAML 声明与程序化创建的列都自动获得统一表头，页面只调整列宽、是否显示操作列/固定等细节；表头模板用显式 `Rectangle` 边线，避免 `BorderThickness` 在 DataGrid 表头不渲染；日期列统一加宽到 160，避免 `yyyy-MM-dd HH:mm` 尾部被截断。`CustomDataGrid` 监听 `Columns` 变化，自动把最后一列设为 `CanUserResize=False`（含固定的操作列，列宽变化时恢复上一列的 resize），末列右侧不再出现拖拽指示线；筛选框改为左对齐表格；全选复选框在全选状态点击进入 indeterminate 时取消全选（其余状态点击全选）；`CustomPasswordInput` 外层设为 `Focusable=False`/`IsTabStop=False`，Tab 直接进入模板内的 PasswordBox，登录等表单可用 Tab 顺序切换输入框并自动聚焦。验证：`dotnet build` 0 警告 0 错误；登录开发环境（admin）实机核对用户管理表：表头与单元格左对齐、全列表头竖分隔线与底边线、斑马纹、操作列四个按钮完整显示；点击「账号」表头出现 ↑ 指示器并按账号排序。

- **WPF 原生控件扫描（2026-11-12）**：全项目扫描原生 WPF 控件：无 `DatePicker`/`Calendar`/`Slider`/`ProgressBar`/`RichTextBox`/`ListView`/`Expander`/`GroupBox` 等使用；租户分配弹窗的 `TabControl` 换成 `CustomTabs`，`CustomPromptDialog` 的单行输入换成 `CustomInput`，管理编辑器布尔字段、表格全选框与行复选框换成 `CustomCheckBox`。剩余 `ListBox`/`TreeView`/`ScrollBar` 作为基础控件由 `Controls.Base.xaml` 的全局隐式样式主题化为纸面外观；`PasswordBox`/`TextBox` 仅出现在 `CustomPasswordInput`/`CustomInputNumber` 的模板内部。验证：`dotnet build` 0 警告 0 错误。

- **WPF 移除组件库入口并统一登录页明暗按钮（2026-11-12）**：删除顶栏与登录页右上角的「组件库」按钮，组件库窗口仅保留 `--gallery`（可加 `--dark`）命令行入口；登录页移除主题风格下拉与「玄夜」开关，改为与顶栏一致的 34px 月亮/太阳图标按钮（`Segoe MDL2 Assets` 字形，ToolTip 随状态切换），两处按钮共用同一处理逻辑并同步图标与提示；主题风格仍可在登录后的个人菜单「主题」中切换并写回偏好，登录页明暗只影响当前显示。验证：`dotnet build` 0 警告 0 错误（运行中客户端占用默认输出，改用临时输出目录）；离屏渲染真实 `MainWindow` 的登录页与登录后壳层，确认登录页右上角只剩明暗图标按钮，顶栏也只剩同一图标按钮，无主题下拉、玄夜开关与组件库按钮。企业级建设清单、当前基线、风险优先级不变。

- **WPF 冻结列左侧分隔线单线修复（2026-11-12）**：`PART_FrozenColumnsHost` 与表头分隔线由左移 1px 改为左移 2px（超过 1px 线宽），冻结区不透明的表头/行背景盖住主表末列右边框，主表末列右边线与冻结列左边线不再并排渲染成 2px 粗线，边界只剩冻结区这一条 1px 分隔线；横向滚动、悬停、斑马纹与选中高亮表现不变。验证：`dotnet build` 0 警告 0 错误（运行中客户端占用默认输出，改用临时输出目录）；用引用真实组件与主题的临时宿主对 850–980px 共 131 个表格宽度逐像素扫描，修复前表头与数据行全部为 2px 双边线（131/131），修复后全部为 1px 单线（131/131）。

- **WPF 管理编辑弹窗滚动条贴边（2026-11-12）**：`ManagementEditor` 的滚动区右侧不再保留 24px 外边距，`ScrollViewer` 直接贴窗口右缘（与其他弹窗 `Content = ScrollViewer` 的布局一致）；表单内容右留 14px（补上 10px 滚动条宽度，输入框仍距窗口 24px），页脚按钮区右留 24px；Choice 字段由系统 `ComboBox` 换成 `CustomSelect`，下拉框与主题统一；审批处理弹窗的动作/加签位置选择、定时任务弹窗的类型/HTTP 方法/服务方法也一并替换，项目中不再有原生 `ComboBox` 实例（`CustomSelect` 本身继承 ComboBox，内部除外）。表单输入同步统一：`CustomFormField` 内部改用 `CustomInput`（10px 圆角），管理编辑器的多行字段、审批意见、定时任务的多行参数改用 `CustomTextarea`，定时任务/用户选择器的原生 `TextBox` 改为 `CustomInput`/`CustomTextarea`，项目中不再有原生 `TextBox` 表单控件。验证：`dotnet build` 0 警告 0 错误。

- **WPF 登录「记住我」（2026-11-12）**：登录卡密码框下新增「记住我」；勾选并成功登录后，`CredentialStore` 使用 Windows DPAPI（`CryptProtectData`，当前用户作用域）把账号密码序列化后加密写入 `%LOCALAPPDATA%\Sim.Admin.Wpf.Client\remembered-login.dat`，启动与退出/会话失效回到登录页时自动回填账号密码并勾选；取消勾选并成功登录删除该文件。磁盘上不保存明文，DPAPI 密文仅当前 Windows 用户可解密。`LoginAsync`/`CompleteTwoFactorAsync` 增加 `rememberMe` 参数，与勾选状态一起传给服务端控制刷新令牌寿命。验证：`dotnet build` 0 警告 0 错误。

- **WPF 侧栏字体与原生列表/树主题化（2026-11-12）**：`CustomMenu` 行样式补 `Cascadia Code` 字族，修复子项 ContentPresenter 回落到 Segoe UI 的问题；`Controls.Base.xaml` 新增全局隐式 `ListBox`/`ListBoxItem` 与 `TreeView`/`TreeViewItem` 样式：纸面底、2px `EdgeBrush` 描边、圆角、悬停 `HoverBrush`、选中 `SoftBrush + Edge + 加粗`，字体统一；`TreeViewItem` 用 2px 细线 chevron 替换系统三角、16px 层级缩进。文件存储的提供方列表（原生 ListBox）与组织架构树（原生 TreeView）不再呈现系统默认外观。验证：`dotnet build` 0 警告 0 错误。

- **WPF 滚动条与时间线修复（2026-11-12）**：`Controls.Base.xaml` 新增全局隐式 `ScrollBar` 样式：垂直/水平均为 10px 透明轨道（保留拖拽命中区），可见滑块收窄到 5px（左右/上下各留 2.5px）、`PaperBrush` 胶囊 + 1px `EdgeBrush` 描边（悬停/拖拽变 `PrimaryBrush`），隐藏默认箭头与灰色槽，所有 ScrollViewer 与 DataGrid 滚动条统一。`CustomTimelineItem` 的连接线改为跨两行、从节点圆心延伸到下一节点圆心（`Grid.RowSpan=2`，上边距 10、下边距 -26），节点 Paper 底色覆盖线头，末项不画线，节点之间不再断开。验证：`dotnet build` 0 警告 0 错误；组件库实机截图确认滚动条收窄为 10px 胶囊。

- **WPF 描述列表圆角与末行边线修复（2026-11-12）**：`CustomDescriptions` 在 `OnItemsChanged` 中为子项分配 `IsFirst`/`IsLast`；首项标签格左上、末项标签格左下按外框 12px 圆角减 2px 描边补 10px 圆角，标签底色不再盖住外框圆角；末项去掉行底部分隔线，消除与外框叠出的双线，单项时左下与左上同时圆角。验证：`dotnet build` 0 警告 0 错误。

- **WPF 业务表格补齐选择列与 Naive 表头（2026-11-12）**：`ManagementPage` 表格首列新增三态全选框（`NaiveCheckBoxStyle` 支持 indeterminate 横线）与行复选框；`ManagementRow` 增加 `IsSelected`，编辑/删除/行操作/关系操作优先取勾选记录，删除支持多行批量。`CustomDataGrid` 表头改为 14px/44px 高、表头与单元格补 1px 竖分隔线，行高 46px、斑马纹（`PageBrush`），首列半粗显示；用户表补充手机、性别列并调整列顺序为账号/昵称/姓名/手机/性别/邮箱。验证：`dotnet build` 0 警告 0 错误；登录态管理表格的实机截图本轮未能获取（当前运行实例占用输出且本地缺少可用账号），样式改动为共享 `CustomDataGrid` 与页面列定义，编译与 XAML 解析通过。

- **WPF 登录页复刻 Web 左右分屏（2026-11-12）**：登录页改为与 Web 后台一致的左右分屏：左侧 `ActionBrush` 品牌面板占满窗口高度，含品牌锁定（44px 描边标记 + Sim Client / 管理工作台）、垂直居中的「欢迎使用 Sim Client」大标语与底部版权；右侧 `PageBrush` 区域右上角放置主题/玄夜/组件库工具栏，中央为 430px 纸面登录卡（2px 描边、24px 圆角、32px 内边距），卡内为账户登录 kicker、欢迎回来、带必填标记的字段与登录按钮，双重验证沿用同一张卡。登录时隐藏全局顶栏并让分屏占满窗口，登录后恢复；登录工具栏与顶栏的主题选择/玄夜开关双向同步，主题偏好加载时同步两处控件。验证：`dotnet build` 0 警告 0 错误；临时输出实机截图核对浅色与玄夜下的分屏、430px 卡宽、品牌锁定/标语/版权与工具栏位置。

- **WPF 业务列表去重标题与勾选式批量（2026-11-12）**：所有业务页表格卡片去掉与内容区页头重复的标题（管理 / 租户 / 定时任务的卡片头、组织架构右侧成员卡标题），表格区域只保留工具栏与表格；去掉工具栏「编辑」按钮，双击行仍进入编辑，工具栏只服务复选框勾选。新增 `SelectableRow<T>` 包装器与共享 `SelectableRowCheckTemplate`，租户管理、定时任务、组织成员表格首列补 44px 复选列（居中、不可排序与拖拽），工具栏的启用 / 停用 / 删除 / 查看分配 / 立即执行 / 移除成员等动作只统计勾选行，光标选中不再计入；管理页工具栏同步改为只取勾选记录，全选/半选仍由三态表头复选框驱动。定时任务列宽收紧（类型 90、Cron 130、状态 90、运行时间 160）并给名称列 `MinWidth=120`，避免星号列在窄窗口被固定列挤成 0 宽；组织架构成员卡片标题同时从代码中移除。验证：`dotnet build` 0 警告 0 错误；临时输出实机截图确认三个页面无重复标题、无工具栏编辑按钮、勾选后批量按钮启用（内置任务删除保持禁用），定时任务全部列完整显示。

- **WPF 表格列宽补齐与末列可调整（2026-11-12）**：移除「最后一列非 fixed 列禁止 resize」的规则，除复选列外所有列都可拖拽调宽。resize 策略：无横向滚动条（表格已被列宽填满）时，缩小任一非末列由右侧相邻列自动补宽，保持无空白；末列缩小会被补回，不能缩出空白；扩大列宽不压缩邻列，总宽超出视口后由横向滚动条承载。横向滚动条横跨整张表宽（含冻结列与垂直滚动条区域），但仍只滚动非冻结列。初次加载按冻结列、垂直滚动条之后的实际视口分配列宽，冻结操作列变宽时自动收窄或补齐，既不出现空白填充列，也不产生无意义横向滚动；用户手动拖拽后停止自动补齐。验证：`dotnet build` 0 警告 0 错误；角色管理实机确认无溢出滚动条、列铺满且操作列冻结；路由管理确认数据列与冻结列铺满、无空白列。

- **WPF 操作列右侧冻结（2026-11-12）**：`CustomDataGrid` 模板新增 `PART_FrozenColumnsHost`，并新增 `FrozenRightColumn`：设置后操作列从主列集合移出，由同源只读副表在右侧渲染；副表同步 `ItemsSource`、垂直滚动偏移、选中行、斑马纹与行高，列宽按最宽一行的按钮组自适应。冻结宿主位于 `ScrollViewer` 模板的独立列（主内容与垂直滚动条之间），冻结区左侧绘制 1px `LineBrush` 分隔线（表头与单元格各一段），主表在剩余宽度内横向滚动，数据列不会被永久遮住，垂直滚动条保持在表格最右侧；`ActionsFixed=false` 时操作列仍作为普通末列加入主表。验证：`dotnet build` 0 警告 0 错误；用户管理/系统配置实机确认操作列固定在右侧、表头/行高/斑马纹与选中高亮和主表对齐，纵向滚动时操作列同步移动，垂直滚动条位于冻结列右侧。

- **WPF 表格固定列宽与横向滚动（2026-11-12）**：`CustomDataGrid` 首次加载时把列宽固化为绝对值——星号/未设宽度的列取 `DefaultColumnWidth`（默认 120）或列级 `MinWidth`（取较大值），剩余空间平均分给各数据列；`MinimumColumnWidth`（默认 60，页面可覆盖）作为拖拽下限，列级 `MinWidth` 更大时优先。列宽固定后拖拽只改变当前列，不再压缩相邻列，总宽超出视口时由 `ScrollViewer.HorizontalScrollBarVisibility=Auto` 提供横向滚动。验证：`dotnet build` 0 警告 0 错误；实机确认定时任务页按固定宽度渲染、底部出现横向滚动条且可滚动，租户等有余量的页面列宽平均补齐。

- **WPF 管理表单失焦校验（2026-11-12）**：`CustomFormField` 增加 `ValidationError` / `HasError` 与 `NumericOnly`：手机号字段只保留数字并限制 11 位（输入、粘贴、输入法回填都会过滤），输错时输入框切换 Danger 描边并在下方显示错误行。管理编辑器在失焦时校验：账号 3–20 位、昵称不超过 20 位、邮箱正则、手机号 `^1[3-9]\d{9}$`、必填项非空；保存前统一复验，存在错误时阻止提交并提示。验证：`dotnet build` 0 警告 0 错误。

- **WPF 头像上传统一到个人资料（2026-11-12）**：头像上传从用户管理编辑弹窗移到「个人资料」页：88px 圆形头像框，点击选图后打开裁剪弹窗（正方形取景、滚轮缩放、拖动画面，倍率显示在左下角），裁剪 PNG 限制在 512px 内，提交 `api/system/user/upload-my-avatar` 更新本人头像，并在框内即时显示、同步刷新左侧栏头像。用户管理不再包含头像字段，避免管理员为他人上传后文件归属导致目标用户无法读取。头像预览统一通过 `ApiClient.DownloadContentFileAsync` 带 Token 下载，失败回退昵称首字/「点击上传」。新增 `UploadContentFileAsync(fileName, bytes)` 重载。验证：`dotnet build` 0 警告 0 错误；实机确认头像框位于表单最前、点击可打开系统选图对话框；裁剪后的上传与回显未完成端到端自动化复验。

- **WPF 表格勾选统一到 CustomDataGrid（2026-11-12）**：复刻 Web `DataTable` 的 `selectable` + `row-key` 模式。`CustomDataGrid` 新增 `EnableSelection`（对应表格定义的 selectable，默认关闭）与 `RowKeyPath`（默认 `Id`）：开启后自动生成与用户管理页一致的 44px 固定复选列（`FrozenColumnCount=1` 固定在左侧、三态全选表头、`CustomCheckBox`、不可排序/拖拽），勾选状态由组件按行键维护在 `HashSet` 中，行容器通过附加属性 `IsRowChecked` 与复选模板双向绑定，并暴露 `CheckedItems` / `CheckedRowKeys` / `CheckedChanged`；勾选与光标选中相互独立。管理 / 租户 / 定时任务 / 组织成员页面不再包行、不再定义复选列和全选逻辑，数据模型保持原样，只声明 `EnableSelection` / `RowKeyPath` 并读取勾选集合。复选列双击在网格 `OnMouseDoubleClick` 中截断，只切换一次勾选、不触发行编辑；末尾列的 resize 逻辑不会把复选列恢复为可拖拽。用户 / 角色中 `isBuiltIn` 为 true 的记录不提供行删除，勾选含内置记录时工具栏删除保持禁用。验证：`dotnet build` 0 警告 0 错误；实机确认定时任务/租户全选、取消全选、半选表头、单行勾选、按钮启用，双击复选框只切换且不弹编辑窗；用户管理内置记录无行删除且工具栏删除禁用。

- **WPF 表格状态标志改为不可交互（2026-11-12）**：`Controls.Inputs.xaml` 新增 `NaiveStatusCheckBoxStyle`（基于 `NaiveCheckBoxStyle`，`IsHitTestVisible`/`Focusable`/`IsTabStop` 均为 false、光标为箭头），定时任务的启用/内置、租户启用、登录会话的当前/在线、组织成员的主组织、租户分配弹窗的启用/菜单、通知中心已读、组件库示例的状态列统一改用该样式；点击状态框只选中行，不切换勾选值，也不进入键盘焦点。行选择与全选仍使用可交互的 `CustomCheckBox`/`NaiveCheckBoxStyle`。验证：`dotnet build` 0 警告 0 错误；账号安全页实机点击「当前」「在线」后勾选状态保持不变，仅该行被选中。

- **WPF 登录页改为配置文件驱动（2026-11-12）**：移除登录页的 API 地址输入与「测试连接」按钮，后端地址统一从随程序复制的 `appsettings.json`（`Api:BaseUrl`，默认 `https://localhost:7777`）读取，配置缺失或为空时登录给出提示；删除窗口底部常驻状态栏，登录失败、会话失效等错误改用 `CustomMessage` 顶部居中消息（新增 `TopCenter` 停靠与 `TopError`/`TopInfo`），成功类状态文案不再常驻；`ApiClient.CheckConnectionAsync` 随按钮一并删除。验证：`dotnet build` 0 警告 0 错误；临时将输出目录 `appsettings.json` 指向不可达地址后登录，窗口顶部居中显示连接失败消息，登录卡片不再含 API 输入与底部状态行。

- **WPF 表格每页条数可调（2026-11-12）**：`CustomPagination` 新增 `PageSize`、`PageSizes`、`MinimumPageSize`、`MaximumPageSize` 与 `PageSizeChanged`，页脚左侧渲染可输入的每页条数下拉框（20/50/100/200/500，默认 20，手输值钳制在 20–500，非法输入回退当前值），下拉与输入共用 Naive 风格的 2px 描边、8px 圆角和主题色；`ManagementPage` 与 `ApprovalListPage` 改用 `Pager.PageSize` 组装 `skipCount`/`maxResultCount` 并重算页数，切换条数时回到第 1 页重新加载；后端 `LimitedResultRequestDto.MaxResultCount` 上限由 100 提升到 500。验证：`dotnet build` 0 警告 0 错误（客户端进程占用默认输出目录时改用临时输出目录），API Contracts 构建 0 警告 0 错误。

- **WPF 顶栏与个人菜单重构（2026-11-12）**：登录态壳层把定位信息移到顶栏：品牌右侧增加竖分隔线与「分组 / 叶节点」面包屑（父分组 `MutedBrush`、当前叶节点 `InkBrush` 加粗），内容区不再保留 24px 页面标题、描述与后端 API 地址。顶栏明暗切换由「玄夜」开关改为月亮/太阳图标按钮（`CustomButton` 内嵌 `Segoe MDL2 Assets` 字形 TextBlock，避免全局 TextBlock 字族覆盖；浅色显示月亮、玄夜显示太阳），保留「组件库」入口。左侧菜单移除个人资料、账号安全、通知中心三项，底部改为个人信息条：`CustomAvatar` 首字头像 + 显示名/角色，右侧为电源图标退出按钮；点击个人信息条展开 `Popup` 个人菜单，包含个人资料、账号安全、通知中心与「主题」（右侧显示当前风格）。「主题」打开「选择主题」窗口，用 `CustomRadio` 列出四套风格与说明，选中即时应用并同步登录页控件，关闭时把风格与明暗写回用户偏好；明暗按钮与偏好保存经过串行化，避免快速切换时后发请求被丢弃。`CustomMenu` 新增 `ClearSelection`，从个人菜单进入菜单外页面时清除侧栏选中态；登出时关闭个人菜单。验证：`dotnet build` 0 警告 0 错误（默认输出被运行中客户端占用，改用临时输出目录）；离屏渲染核对顶栏面包屑、月亮/太阳图标、个人菜单弹层与主题选择窗口，浅色/玄夜均正常。企业级建设清单、当前基线、风险优先级不变。

- **WPF 分配路由树勾选与横向滚动修复（2026-11-16）**：`SelectionDialog` 的路由树三处缺陷：①全选的父节点点击后只落到半选、子树保持勾选，无法一次取消——`OptionClick` 不再依赖 WPF 三态循环（true→null→false→true），改为按点击后的状态归一：仍为半选说明原先全选，整棵子树置为未选；其余情况（未选/半选）一律置为全选；②叶子节点取消勾选会停在半选——同一归一逻辑把它落回未选，树的勾选与 Web 路由树（naive-ui 级联）一致；③点击复选框后树被横向滚动、复选框贴到视口左边缘——复选框此前拉伸整行宽度，获得焦点时 `BringIntoView` 把超宽的焦点矩形对齐到视口左侧，现改为 `HorizontalAlignment="Left"`，焦点矩形只覆盖自身，横向滚动不再跳变；移除不再使用的 `PreviewMouseLeftButtonDown` 与 `_pendingClickState`。验证：`dotnet build` 0 警告 0 错误（运行中客户端占用默认输出，改用临时输出目录）；运行中实例需重启后生效，实机点击未在本轮复验。企业级建设清单、当前基线、风险优先级不变。

Windows WPF Client 已按 `web/src/config/themes.ts` 的四套风格 × 明暗配色建立桌面主题映射；桌面工作区使用系统控件和相同的语义色、卡片边框与主行动色，用户切换风格时通过 `/api/system/user-preference/my-preferences` 与 Web 同步。普通文字继承窗口前景色，主行动按钮由统一样式配对 `ActionBrush` 与 `ActionTextBrush`，深色背景须有对应的浅色文字；不得用全局文字样式覆盖控件自身的前景色。桌面端按 Naive UI 常用组件建立了原生 WPF 控件库，位于 `client/Sim.Admin.Wpf.Client/Controls` 的 `Inputs`（输入、数字、下拉、复选、单选、开关、表单项）、`Display`（标签、徽标、头像、分隔线、进度、加载、骨架屏、空状态、结果页、提示条、描述列表）、`Container`（手风琴菜单、标签页、折叠面板、分页、步骤条、时间线）、`Overlay`（弹窗、抽屉、二次确认、消息提示）四个族，样式集中在 `client/Sim.Admin.Wpf.Client/Theme/Controls.*.xaml`，由 `Theme/CustomControls.xaml` 合并进 `App.xaml`；组件预览入口为 `--gallery`（可加 `--dark`）启动参数。**WPF 端不使用任何投影**：禁止 `Effect`/`DropShadowEffect`，也禁止用偏移的第二层 `Border` 模拟硬投影（`MainWindow` 原外层深色 `Border` 已移除），层级只由 2px `EdgeBrush` 描边、圆角与语义色表达。颜色必须用 `DynamicResource` 语义键，尺寸用 `CustomControlSize` 三档（30/36/42，圆角 8/10/12），容器控件用 `ItemContainerStyle` 局部引用 keyed 样式而不定义全局隐式 `TabItem` 样式。管理页复用 `Features/Management` 的字段编辑器和 `ManagementCatalog` 定义；审批状态由 `Converters/ApprovalStatusConverter.cs` 统一映射。`App.xaml` 的 `ComboBoxItem` 样式负责下拉项前景色，避免逐个页面硬编码。调整 Web 调色盘时须同步检查 `client/Sim.Admin.Wpf.Client/Theme/ThemeManager.cs`（含 success/warning/info/hover/overlay 等桌面扩展语义色）。桌面工作区壳层与 Web 后台同构：登录后由左侧 `CustomMenu`（`n-menu` 复刻）手风琴菜单驱动内容切换，顶层分组可展开/收起且同级只展开一个，授权/系统/审批的模块作为子项直接进入对应页面，不再使用顶部 `CustomTabs`；`CustomDataGrid` 按 `n-data-table` 默认外观复刻：`PageDeepBrush` 浅色表头、1px `LineBrush` 行分隔线、行悬停与 Naive 复选列，表格自身不描边，外框由所在卡片承担；`ThemeManager` 同步补齐 `PageDeepBrush`（bgDeep）与 `LineBrush`（line）两枚调色板令牌。登录页的双栏品牌布局保持不变。

## 10. 多主题与商务皮肤（2026-10-25）

在 Paper Ocean 之上增加 3 套商务风格，共 4 套 × 月白 / 玄夜。纸上海洋的令牌默认值与 Naive 常量不改写，视为回归基线。

### 10.1 风格与入口

| id | 名称 | 皮肤 | 定位 |
| --- | --- | --- | --- |
| `paper-ocean` | 纸上海洋 | `paper` | 现有风格，首次访问默认 |
| `xuanqing` | 玄青 · 鎏金 | `business` | 深青底 + 金线，沉稳 |
| `zhuyue` | 竹月 · 清雅 | `business` | 青绿 + 月白，清透 |
| `zhuxiang` | 朱缃 · 典章 | `business` | 朱红 + 宣纸，庄重 |

入口固定在侧栏底边的「系统设置」文字行（圆形齿轮 `Settings` 图标，无边框与底板、直角点击区域，悬停浅蓝底与菜单项同色，齿轮进入时逆时针转一圈、离开时顺时针转回；菜单区独立滚动）：宽屏（>768px）从右侧打开「系统偏好」抽屉，窄屏（≤768px）打开居中 `n-modal` 弹窗并收起侧栏；两者内部都是 `ThemeStylePicker`，含风格单选组与明暗分段（月白 / 玄夜 / 跟随系统）。顶栏原有明暗开关保留，且只改明暗、不改风格。

### 10.2 分层职责

- `src/config/themes.ts`：`themeStyles` 注册表，含 `labelKey` / `descriptionKey` / `preview` / `skin` 与 `light` / `dark` 两份 `ThemePalette`。`getThemeStyle()` 对未知值回落纸上海洋；`paletteToCssVars()` 输出全量 `--po-*`；`paletteToThemeColors()` 供 Naive 工厂使用。色值只在此处维护。
- `src/stores/theme.ts`：持久化 `currentThemeName`（`light` / `dark`）与 `themeStyle`；派生 `styleDefinition` / `palette` / `themeColors` / `cssTokens` / `skin` / `currentThemeOverrides`。纸上海洋直接返回原有 `lightThemeColors` / `darkThemeColors` 与旧覆盖，保证视觉不变。
- `src/App.vue`：把 `cssTokens` 逐项写到 `<html>`，并设置 `data-theme` / `data-style` / `data-skin` / `data-density`。
- `src/config/businessTheme.ts`：`createBusinessThemeOverrides(palette, dark)`，生成 1px 描边、实心主按钮、收敛圆角与柔光投影的 Naive 覆盖。颜色只输出 hex / rgba；CSS 变量只出现在描边字符串中。
- `src/styles/business-skin.css`：仅在 `[data-skin='business']` 生效的结构覆盖——圆角、发丝边、字重 600、柔光分层投影、实心主按钮、取消倾斜与硬投影位移、深色投影加强。由 `main.ts` 在 `paper-ocean.css` 之后引入。
- `src/components/ThemeStylePicker/*`：外观选择内容，不含外层卡片；`role="radiogroup"` + `role="radio"` + roving tabindex；方向键 / Home / End 切换并移动焦点；预览色段随当前明暗取 `dark` 或 `preview`。
- `src/components/SystemPreferences/*`：系统偏好容器，按 `useResponsive().isNarrowScreen` 在宽屏右侧抽屉（440px）与窄屏居中弹窗之间切换，只挂载 `ThemeStylePicker`。
- `src/components/Layout/Sidebar.vue`：系统设置入口停靠在侧栏底边，作为纯文字页脚融入纸面（无按钮边框/底板/投影，悬停浅蓝底），菜单区独立滚动；折叠时保留图标并用 tooltip 显示名称，窄屏打开偏好后立即收起侧栏抽屉，关闭后把焦点交回顶栏导航按钮。

### 10.3 新代码约束

1. 组件里不再写死 `2px solid var(--po-ink)`、`font-weight: 700/800/900`、`Npx Npx 0 var(--po-ink)`；统一使用 `--po-border` / `--po-border-thin` / `--po-border-hair`、`--po-weight-display/strong/bold`、`--po-shadow-*`。
2. 商务风格需要的结构性差异只写进 `business-skin.css` 并加 `[data-skin='business']` 前缀；不要为某套风格复制页面级色板。
3. Naive UI 颜色继续用字面量，禁止把 CSS 变量传给 `GlobalThemeOverrides` 的颜色字段；描边可用变量。
4. 倾斜交互只在 `.po-tilt-shell` / `.po-card--interactive` 上禁用；文章正文、画布与编辑器内部变换不动。
5. 新增风格只需在 `themes.ts` 注册表增加条目并补齐 `account` 语言包；不要再建第二份颜色定义。

### 10.4 持久化

`localStorage` 键 `naive-theme` 保留，新增可选字段 `themeStyle` 与 `themeMode`：

```json
{ "currentThemeName": "dark", "themeMode": "system", "themeStyle": "zhuxiang" }
```

`themeMode` 取 `light / dark / system`；`currentThemeName` 保存解析后的明暗，仅用于兼容旧数据与外部读取。旧数据没有 `themeStyle` 时回落 `paper-ocean`；非法值同样回落。旧数据没有 `themeMode` 时按 `currentThemeName` 推断，站点配置的 `ui.default-theme` 仍只在用户未手动选择明暗时应用（`auto` 映射为 `system`），风格首次访问固定纸上海洋。登录用户的服务端偏好会覆盖本机选择，之后语言、明暗与主题色变化自动防抖写回。

### 10.5 已知硬编码残留

以下位置保留固定色值，本次未改为令牌，新增页面不要照抄：

- `AuthBrandPanel.vue` 的 `--bp-ink: #14284b`、`TopBar.vue` 的 `#4a6cf7`、`setup.vue` 的 `#14284b`：品牌识别色，换肤时刻意不变。

### 10.6 验证

- `pnpm type-check` 通过；`pnpm build` 通过（首次 Windows 文件句柄占用 `components.d.ts`，重试通过）。
- `themes.test.ts` 15 项通过：注册表回落、4 套唯一风格与预览色、令牌键一致、正文 ≥7:1 / 次级 ≥4.5:1 / 商务主按钮 ≥5:1。
- 全量 `pnpm test:unit` 436 通过 / 2 失败（共 438 项），均在 `ArticleImportModal.test.ts`，HEAD（a12bdc6）已存在，与主题无关。
- 视觉回归：4 套 × 明暗的个人设置、工作台、组织列表、登录页、对话框与窄屏个人设置，1440×1000 / 390×900。
- 交互：切换即时生效、刷新保持、顶栏明暗不改风格、键盘单选与焦点环、弹层与消息跟随主题；控制台除登录前预期 401 外无错误。

## 11. dshopen.org 组件细节基线（2026-10-22）

本轮把 Paper Ocean 的控件细节对齐到参考站 `https://dshopen.org/zh/`；页面结构、路由与业务逻辑不变。核心口径：

| 组件 | 细节 |
| --- | --- |
| 按钮 | 主行动珊瑚底、15px 圆角、静止态 `0 1px 0 ink` 底边投影，悬停 `translateY(-2px)` + `0 4px`，按下位移 4px；次级按钮与主行动共用 2px 墨蓝描边（错误/警告/成功/信息类型沿用语义色），纯文字按钮保持轻量；小按钮（详情、筛选）为 999px 胶囊 + 静止态 `1px 1px 0 ink`，悬停提升到 `2px 3px`；圆形图标按钮的 `n-icon` 包裹层撑满按钮并居中，SVG 不再左上溢出 |
| 分段切换 | 语言切换与表格 / 卡片视图切换共用 `SegmentedSwitch`：选项数组驱动、等宽墨色滑块、悬停跟随并在离开后回到选中项、0.35s 弹性，`hoverEffect` 可关闭悬停位移；视图切换固定在列表左下角，与分页控件同高、同一水平线 |
| 页头 / 工具栏 | 有操作栏的页面，刷新 / 新建 / 批量删除等动作统一放在工具栏右侧（搜索、视图切换之后），页头不再承载动作；无独立操作栏的配置页把刷新 / 保存放进主要卡片的头部（如存储概览）并移除冗余状态胶囊；统计信息压缩为页头右侧的指标条（标签 / 数值 / 说明，首项数值用 `--po-accent`），不再单独占一行四卡；≤640px 指标两列换行、动作整行右对齐；后台页头不再重复顶栏已展示的页面标题与描述（记录详情页除外），记录详情页保留标题与返回入口 |
| 选项片 | 40px 高、14px/800、2px 墨蓝描边；悬停上浮 2px 并微旋 -1° + 浅海蓝；选中为墨蓝底 + 纸色文字 + 珊瑚投影 |
| 标签 | 12px/800、`1px 11px` 内边距、2px 墨蓝描边、999px 胶囊；天空蓝 / 蜜桃 / 丁香 / 薄荷 / 贴纸黄为语义底色；贴纸黄用 `--po-on-bright` 固定墨色文字，珊瑚底用 `--po-on-coral`（纸上海洋同为墨色，商务风格的珊瑚即主色，随调色盘反相） |
| 输入 / 下拉 | 50px 高、16px 圆角、2px 墨蓝描边、15px/600；占位符 `#70819f`；聚焦保留墨蓝描边并叠加珊瑚色 3px 焦点环；下拉菜单 16px 圆角、6px 内边距、`2px 2px 0 ink` 投影，选项 10px 圆角、浅海蓝悬停 |
| 卡片 | 24px 圆角、2px 墨蓝描边、静止态 `1px 1px 0 ink` 硬投影；交互卡悬停 `translate(-5px,-9px) scale(1.035) rotate(-0.6deg)` + `10px 14px 0 ink`；Naive Card 同步 24px 圆角与 1px 静止投影，hoverable 悬停 6–8px；表格卡内部 `n-data-table` 不再绘制任何外框（wrapper 与根节点描边均为 0），四角与边线由卡片统一负责 |
| 树形列表 | 展开器为 2px 细线 chevron（展开向下、悬停浅海蓝底）；组织架构左栏使用文件夹 / 组织图标、主题色引导线与 44px 搜索框；侧栏菜单子项箭头复用同一 chevron（收起朝右、展开朝下） |
| 进度条 | 12px 高胶囊轨、2px 墨蓝描边、纸黄轨道；填充沿用 accent / coral / mint / sun 语义色，宽度过渡 0.4s |
| 开关 | 语言切换改为参考站式滑动分段开关：深/浅底胶囊（静止态 1px 投影）+ 墨蓝滑块，`0.35s cubic-bezier(0.5,1.6,0.4,1)`；二元开关 46×26、薄荷绿开启态、18px 白钮 + 2px 描边，滑块与轨道内文字按 `--po-border-width` 复算垂直居中（纸上海洋 2px、商务皮肤 1px）；后台弹窗头部的公开 / 私有开关用 `.po-switch-field` 把状态标签放在轨道外，聚焦不叠加按钮式硬投影 |
| 焦点 | 全站 `:focus-visible` 为珊瑚色 3px 环、偏移 3px；输入框 / 下拉 / 自定义搜索等可输入组件聚焦时改为与按钮一致的上浮 2px + 硬投影（`translateY(-2px)` + `3px 3px 0 ink`），不再叠加珊瑚环或贴边粉色光晕 |

验证：`vue-tsc --build`、改动文件 ESLint 通过；Playwright 在 1440px 宽度下检查登录页（浅色/深色）、忘记密码弹窗（含 Select 展开）与组件拼装预览，无 JS 运行错误。


## 工作台最近记录行高（2026-10-31）

工作台 `.workspace-feeds .po-card > .po-list` 使用 `align-content: start`：弹性填充列表容器时不拉伸 Grid 行，保证最近文章/评论在单条、多条及移动端均保持内容行高。


## 可选通行密钥解锁（2026-10-31）

设置页的端到端加密区域支持添加、选择和删除通行密钥；新凭证名称自动取设备名与浏览器环境（如 `PC · Win32 · Chrome 141`），不要求手动填写，已有 `SHA256:` 名称保持原样；解锁面板通过显式点击触发系统验证。解锁弹窗只在本浏览器已选用通行密钥（设置页“在此浏览器使用”）且该凭证仍属于当前身份版本时显示“使用通行密钥”，不再显示凭证下拉；账号在其他设备登记的凭证不会在此浏览器出现，避免无效验证。“信任此设备”开关始终显示；存在设备缓存时优先用缓存静默解锁，通行密钥只作为没有缓存时的显式解锁方式；在本浏览器选用通行密钥会清除既有缓存，但之后再次开启信任仍会写入缓存并优先生效。独立口令与离线恢复密钥仍可使用。

E2EE 设置“修改加密口令”将现有口令、已登记通行密钥与离线恢复密钥并列为三种验证方式：任一种在浏览器解出同一 UMK 后只更新 `PasswordWrap`；通行密钥验证为一次性解锁，不写入“在此浏览器使用”偏好，也不改变设备信任状态。没有已登记凭证、站点未启用 PRF 或浏览器不支持时隐藏通行密钥方式。存在已加密内容时仍可修改口令，但身份重置仅在没有已加密私有内容时开放。

口令解锁时的“信任此设备（仅当前浏览器）”为开关，置于“加密口令”标题行右侧并默认关闭；开启后本次解锁写入本机设备缓存，刷新页面或再次打开加密文章时静默解锁，显式退出登录即清除。不支持 PRF 或部署未开启时给出原因，不自动降级注册。通行密钥用于解锁加密身份，不改变账号登录方式。

添加时先创建凭证，再验证同一凭证能够取得 PRF，可能出现两次系统验证；未返回或两阶段 PRF 不一致时拒绝登记。解锁提示区分环境缺少 WebAuthn 能力和本次凭证未返回 PRF，避免把提供方问题笼统描述成浏览器不支持。

通行密钥操作失败后提供可展开的只读诊断日志，用户可手动复制；日志默认折叠、不自动上传，保留本标签页最近 100 条。原生 NotAllowedError 显示验证未完成而非一律取消；日志提示明确不含密钥材料、刷新清除。

## 表单字段说明气泡（2026-11-01）

后台表单的字段说明统一收进标题右侧的 info 气泡，不再在字段下方或区块上方铺说明文字。共享组件位于 `web/src/components/FormItem/`：

- `PoFormItem`：`n-form-item` / `n-form-item-gi` 的包装组件，新增 `description`（有条目时显示图标）与 `gi`（配合 `n-grid` 使用）两个 prop；其余属性、默认/`label`/`feedback` 插槽原样转发，并暴露 `validate` / `restoreValidation`，可继续用于字段级校验。组件带 `__GRID_ITEM__` 标记，`n-grid` 才会保留它作为网格项。
- `PoFieldLabel`：标题文字 + 说明图标，供自定义 `label` 插槽和自绘字段行（如系统配置项）复用。
- `PoHintIcon`：18px 圆形 info 按钮，hover 或点击展开气泡，点击外部或 Esc 关闭；打开时按图标左右剩余空间选择 `top-start` / `top-end` 对齐与最大宽度（窄屏 224px、宽屏 320px），宽度不够时优先换行拉高，超过 6 行再省略隐藏，气泡不会横向出屏。

约定：只给原本已有说明（规则、注意事项、字段含义）的字段加图标；说明尽量压到一句，删除原 hint 段落、卡片级说明和说明型 placeholder。系统配置页的动态配置项把 `configKey` 与后端 `description` 合并进气泡。已覆盖：审批（流程节点、审批动作）、授权（路由、租户、安全）、系统配置（存储、计划任务、配置中心、编号、通知）。验证：`vue-tsc --build`、改动文件 ESLint、Vitest 全量 716 项通过；Chromium 实登后检查 1440 与 390 宽度下的路由/存储/计划任务/编号/通知表单，气泡不溢出视口。

## 定时任务卡片标签并入标题行（2026-11-18）

定时任务卡片的类型 / 内置 / 启停标签原先堆在标题上方占一行，现与标题同处一个 `.task-title__row` 弹性行（`align-items: center; flex-wrap: wrap`），标签组紧贴标题右侧，描述仍在标题行下方；标题行整体随卡片宽度换行，窄屏下标签不会挤出卡片。企业级建设清单、当前基线、风险优先级不变。

验证：`vue-tsc --build`、改动文件 ESLint 0 警告；Playwright（mock 会话与系统模块路由，系统 Chrome）1440×900 下标签左边界大于标题右边界、标签组与标题垂直居中（中心偏差 < 4px）、描述位于标题行下方；390×844 下无横向溢出，标签随标题行整体换行。

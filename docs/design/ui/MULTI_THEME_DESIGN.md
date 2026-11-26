# 多主题与商务皮肤 · 升级方案

> 状态：已实施（2026-10-25）
> 设计源：`docs/design/ui/prototypes/theme-proposal.html` / `theme-proposal-preview.png`
> 影响范围：`web` 前端主题层、侧栏系统设置入口、Naive UI 覆盖；不涉及后端接口与数据库。

## 1. 范围与结论

| 决策 | 结论 |
| --- | --- |
| 风格数量 | 纸上海洋 + 玄青·鎏金 + 竹月·清雅 + 朱缃·典章，共 4 套 |
| 明暗 | 每套风格都有月白 / 玄夜两种模式，另支持「跟随系统」，切换不改变当前风格 |
| 选择入口 | 侧栏菜单下方新增带图标的「系统设置」按钮；宽屏从右侧打开「系统偏好」抽屉，窄屏打开居中弹窗；顶栏保留明暗快捷开关 |
| 视觉深度 | 配色 + 商务皮肤：1px 发丝边、柔光分层投影、收敛圆角、实心主按钮、展示字重 600 |
| 纸上海洋 | 保留为独立风格，令牌取值、Naive 覆盖与页面外观保持原样 |
| 默认值 | 首次访问仍使用站点配置的 `ui.default-theme`（浅色 / 深色 / 跟随系统）与纸上海洋风格 |

原始范围写的是「保留并只新增 2 套」，但已确认的色系为 3 套。三套共用同一注册表与工厂，增量成本只在配色验收；如需收缩，删除注册表中对应条目即可，不改架构。

## 2. 分层架构

```
主题注册表 (TS, 唯一色值来源)
  ├─ 运行时 CSS 令牌  → data-style / data-theme / data-skin
  ├─ Naive UI 覆盖工厂 → GlobalThemeOverrides
  └─ 商务皮肤层       → 结构令牌 + 局部选择器覆盖
```

| 层 | 位置 | 职责 |
| --- | --- | --- |
| 调色盘注册表 | `src/config/themes.ts`（新增） | 4 套 × 明暗的完整色值、预览色、皮肤类型、i18n 键 |
| 运行时令牌 | `src/App.vue` + `src/styles/theme-variables.css` | 把调色盘写成 `--po-*`；CSS 默认值继续作为纸上海洋的回退 |
| Naive 覆盖 | `src/config/theme.ts` + `src/config/businessTheme.ts`（新增） | 纸上海洋沿用现有常量；商务风格走 `createBusinessThemeOverrides` |
| 商务皮肤 | `src/styles/business-skin.css`（新增） | 圆角、投影、边框、字重、悬停/倾斜收敛，仅在 `[data-skin='business']` 生效 |
| 状态 | `src/stores/theme.ts` | `themeStyle` + `currentThemeName`，持久化与旧数据迁移 |

色值只在注册表里维护一次；CSS 不新增第二份商务色板，避免令牌与 Naive 覆盖漂移。

## 3. 调色盘契约

每套风格包含一份 `light` 与一份 `dark` 调色盘，字段分为四组：

| 组 | 字段 | 消费方 |
| --- | --- | --- |
| 表面与文字 | `bg` `bgDeep` `paper` `raised` `ink` `inkSoft` `inkFaint` `disabled` `line` `lineMid` `edge` `edgeStrong` | `--po-*`、卡片、描边、分隔线 |
| 主色 | `primary` `primaryHover` `primaryPressed` `primarySoft` `primarySoftHover` `primarySoftPressed` `onPrimary` `accent` `accentSoft` | Naive 主色、按钮、选中态、焦点 |
| 装饰 | `coral` `coralHover` `coralDeep` `onCoral` `sun` `sunSoft` `sky` `peach` `mint` `lilac` `dot` `scrim` | 存量 `--po-coral` / `--po-sun` 等别名 |
| 语义 | `success` `successSoft` `warning` `warningSoft` `danger` `dangerSoft` `info` `infoSoft` | 状态标签、提示、图表 |

映射规则：

- 商务风格的 `coral → primary`、`onCoral → onPrimary`、`sun → 鎏金 / 强调亮色`，存量组件不需要逐页改色即可换肤。
- `--po-on-bright` 与 `--po-on-coral` 是两件事：前者服务贴纸黄 / 柔色贴纸（各风格都是亮色，恒为墨色文字），后者服务珊瑚实心底。商务风格的珊瑚即主色，深色底需要浅色文字，因此 `--po-on-coral` 是调色盘字段而不是固定值；品牌 Logo 贴纸（侧栏、认证面板）与 `.po-pill--coral` 消费它。
- 商务风格的 `dot` 为透明，自动移除纸面点阵纹理。
- `--po-primary*`、`--po-edge*`、`--po-on-primary`、`--po-gold*` 为本次新增的语义令牌；纸上海洋默认映射到原电光蓝、墨色与贴纸黄。
- Naive UI 只接收十六进制或 rgba 字面量，禁止传 CSS 变量作为颜色；描边字符串可以继续使用 `var(--po-border*)`。

## 4. 状态与持久化

- 继续使用 `localStorage` 键 `naive-theme` 与字段 `currentThemeName`（`'light'` / `'dark'`），避免旧用户偏好丢失。
- 新增字段 `themeStyle`，默认 `paper-ocean`；旧数据反序列化后自动回落到纸上海洋。
- Store 派生：`styleDefinition`、`palette`、`themeColors`、`cssTokens`、`skin`、`currentThemeOverrides`。
- `applyConfiguredTheme` 行为不变：只在用户尚未手动选择时应用站点默认明暗；风格默认始终是纸上海洋。
- 无效的 `themeStyle` 在读取时回落到纸上海洋，避免持久化脏数据导致白屏。

## 5. 商务皮肤规范

| 维度 | 纸上海洋（不变） | 商务皮肤 |
| --- | --- | --- |
| 结构边 | 2px `--po-ink` | 1px `--po-edge`，悬停 `--po-edge-strong`，焦点 1px 主色 + 3px 光环 |
| 投影 | 无模糊硬投影 | 卡片 `0 1px 2px / 0 10px 28px`，弹层 `0 4px 12px / 0 18px 48px`，弹窗 `0 24px 64px` |
| 圆角 | 10 / 14 / 16 / 15 / 24 / 32 | 6 / 10 / 8 / 8 / 14 / 18 |
| 主按钮 | 珊瑚底 + 墨线 + 硬投影 | 实心主色 + 同色 1px 边 + 米白文字；悬停/按下使用调色盘的重色 |
| 字重 | 展示 900 / 强 800 / 粗 700 | 统一 600，正文 400 |
| 表格 | 表头次级底、1px 分隔 | 表头次级底、主色 5% 悬停、状态标签同色描边 + 8% 底 |
| 动效 | 卡片上浮、倾斜卡片旋转 | 取消倾斜与硬投影位移，保留 120–200ms 的颜色/阴影过渡 |

禁用倾斜的覆盖只作用于 `.po-tilt-shell` 与 `.po-card--interactive`；管理端页面与审批画布的内容变换不受影响。

Naive 开关的轨道内文字只有一个 `textColor`：未选中轨是浅色，工厂因此输出墨色；选中轨是实心主色，由 `business-skin.css` 把 `.n-switch__checked` 覆写为 `--po-on-primary`。

开关的描边补偿也不能写死：轨道描边、滑块与轨道内文字都锚定在同一个内容盒上，`top` 的修正量取 `--po-border-width`（纸上海洋 2px、商务皮肤 1px），否则商务皮肤下滑块与文字会整体偏上 1px。

## 6. 令牌下沉

商务皮肤无法只靠换色实现，需要先把存量硬编码收敛为令牌，默认值必须与现状完全一致：

| 现状 | 数量 | 替换为 | 纸上海洋默认值 |
| --- | --- | --- | --- |
| `2px solid var(--po-ink)` | 95 处 / 26 个文件 | `var(--po-border)` | `2px solid var(--po-ink)` |
| `1.5px / 1px solid var(--po-ink)` | 4 处 | `var(--po-border-thin)` / `var(--po-border-hair)` | 保持原宽度 |
| `font-weight: 900 / 800 / 700` | 51 / 65 / 69 处 | `var(--po-weight-display / strong / bold)` | 900 / 800 / 700 |
| `Npx Npx 0 var(--po-ink)` | 21 处 | `--po-shadow-*` | 与原偏移完全一致 |
| 按 2px 描边复算的几何补偿 | 1 处（开关滑块 `top`） | `var(--po-border-width)` | `2px` |

`src/config/theme.ts` 中纸上海洋的 `2px solid var(--po-ink)` 保留原样，不参与下沉；`src/config/businessTheme.ts` 的商务工厂直接生成 1px 描边，避免影响现有 Naive 覆盖。

## 7. 系统偏好入口

入口固定停靠在侧栏底边，菜单区独立滚动，以纯文字行融入纸面背景，与导航保持同一视觉语言：

- 触发按钮：圆形齿轮 `Settings` 图标 + 「系统设置」文案；无边框、底板与投影，悬停只显示与菜单项同色的浅蓝底且点击区域为直角；齿轮进入悬停时逆时针转满一圈，离开时顺时针转回；侧栏折叠时只保留图标，用 tooltip 显示完整名称；`aria-haspopup="dialog"`；关闭后宽屏焦点回到按钮，窄屏焦点回到顶栏导航按钮（入口随侧栏收起）。
- 宽屏（>768px）：从右侧打开 440px「系统偏好」抽屉，原生滚动，正文区独立滚动。
- 窄屏（≤768px）：打开居中 `n-modal` 卡片弹窗，并同时收起侧栏抽屉，避免两层浮层叠加。
- 弹层内容只有 `ThemeStylePicker`：
  - 风格卡：4 张，含 3 段预览色、名称、短描述与选中态；`role="radiogroup"`，方向键可切换。
  - 明暗分段：月白 / 玄夜 / 跟随系统，作用于当前风格；「跟随系统」实时响应 `prefers-color-scheme`，顶栏快捷开关仍只在月白 / 玄夜之间切换。
  - 不额外添加说明文案：偏好范围与站点默认值的行为已在文档中约定，界面上只保留风格与明暗两组控件。
- 个人资料页不再挂载外观卡，主题入口只有侧栏「系统设置」与顶栏明暗快捷开关。
- 风格名称、描述、明暗与入口文案进入常驻 `account` 语言包，中英文同步。

## 8. 文件变更

| 文件 | 变更 |
| --- | --- |
| `web/src/config/themes.ts` | 新增注册表、调色盘、CSS 令牌映射、语义色映射 |
| `web/src/config/theme.ts` | 纸上海洋常量保留；只调整类型导入，不含商务逻辑 |
| `web/src/config/businessTheme.ts` | 新增商务 Naive 覆盖工厂（`createBusinessThemeOverrides`） |
| `web/src/stores/theme.ts` | 新增 `themeStyle`、派生调色盘/令牌/皮肤，保持旧持久化兼容 |
| `web/src/App.vue` | 注入全部 `--po-*` 令牌，设置 `data-style` / `data-skin` |
| `web/src/hooks/useTheme.ts` | 从 Store 读取当前调色盘与风格 |
| `web/src/styles/theme-variables.css` | 新增边框、字重、倾斜投影、primary/edge/gold 令牌 |
| `web/src/styles/business-skin.css` | 新增商务皮肤层，`main.ts` 在 `paper-ocean.css` 之后引入 |
| `web/src/styles/paper-ocean.css` | 深色强调卡图标底色修正 |
| `web/src/components/ThemeStylePicker/*` | 外观选择内容（风格单选组 + 明暗分段），去掉外层卡片以复用 |
| `web/src/components/SystemPreferences/*` | 新增：宽屏右侧抽屉 / 窄屏居中弹窗，内部挂载 `ThemeStylePicker` |
| `web/src/components/Layout/Sidebar.vue` | 侧栏底部停靠「系统设置」入口（纯文字行）并挂载偏好弹层 |
| `web/src/modules/authorization/views/user/Profile.vue` | 移除原「外观」卡 |
| `web/src/i18n/messages/{zh-CN,en-US}.account.ts` | 主题名称、描述、明暗与入口文案 |
| `web/src/config/themes.test.ts` | 新增注册表、令牌与对比度测试 |
| 其余组件与页面 | 仅把边框/字重/投影字面量替换为等价令牌 |

## 9. 风险与处理

| 风险 | 处理 |
| --- | --- |
| 纸上海洋回归 | 令牌默认值与原字面量逐一等价；不重写现有 Naive 常量；截图对比登录、工作台、列表、审批 |
| 深色下柔光投影不可见 | 商务暗色使用更高透明度黑色投影，并由截图验收 |
| 组件硬编码颜色漏换 | 优先覆盖管理端主路径；截图暴露的漏点改为令牌，不新增页面级双套色 |
| Naive 派生色崩溃 | 工厂只输出 hex / rgba；描边允许 CSS 变量；类型检查兜底 |
| 旧偏好丢失 | 保留 `naive-theme.currentThemeName`，只增加可选 `themeStyle` |
| 字重/边框批量替换误伤 | 只替换精确字面量，先统计数量，再逐文件 diff；不使用宽泛正则 |

## 10. 验收

已完成（2026-10-25）：

1. 静态检查与构建：`pnpm type-check` 通过；`pnpm build` 通过（首次在 Windows 上因 `components.d.ts` 文件句柄占用报 `UNKNOWN`，重试通过）。
2. 单元测试：`src/config/themes.test.ts` 15 项通过，覆盖注册表回落、4 套风格与预览色唯一、令牌键一致、正文/次级/按钮对比度。全量 `pnpm test:unit` 为 436 通过 / 2 失败（共 438 项），失败都在 `ArticleImportModal.test.ts`，该文件在本次改动前的 HEAD（a12bdc6）即失败，与主题无关。
3. 视觉回归：4 套风格 × 明暗，覆盖侧栏系统设置入口（展开 / 折叠）、宽屏抽屉、窄屏弹窗、工作台、组织列表、登录页与对话框；1440×1000 与 390×900。
4. 交互检查：风格切换即时生效；刷新保持（`{"currentThemeName":"dark","themeStyle":"zhuxiang"}`）；顶栏明暗切换不改变风格；方向键与 Home / End 在风格卡间移动并保留焦点环；宽屏抽屉内容区占满视口高度，窄屏弹窗打开时侧栏收起；Escape 关闭后宽屏焦点回到「系统设置」按钮、窄屏回到顶栏导航按钮；个人资料页不再出现外观卡；弹层与消息跟随主题。控制台除登录前 `/refresh-token` 的预期 401 外无错误。
5. 文档同步：`docs/README.md`、`web/README.md`、`web/docs/ARCHITECTURE.md`、`web/docs/DEVELOPMENT.md`、`docs/design/ui/IMPLEMENTATION.md`；`ENTERPRISE_SYSTEM_GUIDE.md` 无需变更（纯前端表现层，不涉及企业级清单）。
6. 回归限制：纸上海洋的 CSS 默认值与 Naive 常量未改写；批量替换只命中精确字面量，逐文件 diff 确认等价。

## 11. 实施顺序

1. 注册表与调色盘 → 2. Store 与迁移 → 3. Naive 工厂与令牌注入 → 4. 令牌下沉与皮肤层 → 5. 外观选择与侧栏入口 → 6. 测试、构建与截图回归 → 7. 文档收尾。

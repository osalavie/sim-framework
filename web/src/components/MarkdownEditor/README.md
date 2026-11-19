# MarkdownEditor

`MarkdownEditor` 是 Tiptap/ProseMirror 单文档编辑器，编辑态与 `MarkdownViewer` 只读态共用 Schema。

- Canvas 使用 Vue Flow，drawio 式自由画布：`canvasShapes.ts` 形状注册表声明几何/尺寸/分组（通用、流程图、时序图、思维导图、数据流图、云与流水线，含不进面板的内部 `anchor` 形状），`CanvasShapeNode` 参数化渲染形状并集成 `NodeResizer` 缩放（按注册表 min/max 约束），`CanvasEdge` 自绘 30° 箭头 + 选中主题色光晕 + 折点增删移（正交线直角路由/贝塞尔 Catmull-Rom，`canvasEdgeGeometry.ts` 纯函数；端点按端口方位从 handle 盒沿回收到盒心），`CanvasStencilPanel` 图形库面板，`CanvasEdgeToolbar` 选中边样式工具条，`canvasGuides.ts` 拖动对齐参考线（参考线优先于网格吸附），`canvasExport.ts` 导出 PNG/JPG（html-to-image 捕获画布变换层，按全部元素回流为完整包围盒并留白；SVG 路径/箭头内联填充与描边；导出前把画布主题令牌内联到捕获根，深色主题下元素配色与屏幕一致；4096px 降采样；文件名 `{文章标题}-{各级标题}-{画布序号}.png|jpg`，标题路径取画布所在位置，序号为文章内画布次序（从 1 开始），空片段省略、全空回退 `canvas`），`canvasClipboard.ts` 画布内复制粘贴（内存剪贴板、跨画布实例、id 重映射偏移粘贴）与方向键微调（Shift 按网格步进）；自由连线：无"画线"模式按钮，空白处拖拽直接画线（未拖动仅取消选中），编辑模式平移为 Shift+左键拖拽（空格不再作为平移激活键，只读模式为左键/中键拖拽、Shift+拖拽为框选），Handle 拖出释放空白生成悬空端点锚点，右键菜单"线段"生成独立线段；端点锚点平时完全不可见（不参与选中，解析按形状注册表钳制尺寸，回环后保持 10×10），悬停/选中线段或改接时浮现为实心小点（无四向连接把手，保留端点吸附），线段中部可整体拖动（悬空端与折点同步平移）；元素连线端点与悬空端点行为同质：改接把手拖到元素 Handle（48px 内）吸附改接、拖到空白处脱离元素并在释放点生成悬空锚点（最小位移 8px，纯点击不改动），悬空锚点拖到元素 Handle 48px 内时高亮候选、松手吸附并回收锚点、依附端点全部改接（`canvasEdgeGeometry.ts` 的 `pickNearestHandle` 纯函数按半径选最近 Handle，锚点把手不参与命中），孤立锚点在线段删除/改接后自动回收；画布内编辑变更统一经 store 实体落点（渲染与序列化回写均以 store 为权威）；画布在挂载、文档重建与恢复可见时按退避序列强制补测节点内部尺寸（不依赖 ResizeObserver 首帧回调送达；祖先 transform 缩放期间（弹窗入场等）暂缓，待布局稳定后再测，末尾档兜底漏发事件）；画布文案走 `canvas` 按需语言包，挂载时注册。
- 画布文档模型 v2（`canvasSyntax.ts`）：` ```canvas ` 围栏 + JSON（nodes/edges），version≠2 按空画布兜底（无迁移）。
- `canvasCapture.ts` 是文章导出的桥接层：导出渲染面向下提供捕获注册表（`CANVAS_CAPTURE_KEY`），画布组件挂载时注册 `{ element, capture }`，`capture` 复用 `canvasExport.ts` 的 `renderCanvasImage` 返回 PNG dataURL；快照按 DOM 先后排序，与 Markdown 中 ` ```canvas ` 围栏顺序一一对应。导出渲染面同时提供 `ARTICLE_EXPORT_SNAPSHOTS_KEY`，画布节点改渲染快照图片，打印时不再依赖 Vue Flow 实例。
- 普通滚轮滚动页面，只有 Ctrl + 滚轮缩放画布。
- 画板响应式适配：容器宽度低于 800px 时图形库从固定侧栏变为覆盖式面板，低于 480px 时从底部展开；触屏（`pointer: coarse`）嵌入态画板可视高度封顶 `min(45vh, 360px)`（不写入文档高度，全屏仍铺满），不显示画布控件栏、缩放只用双指，空白处直接拖动平移画布，点击元素选中、拖动元素移动，选中后元素四边显示连接点（触屏扩大命中区），从连接点拖到另一元素即连线；打开文章时若画布有内容，按元素包围盒 `fitBounds({ padding: 0.2 })` 自动定位（空画布回到原点），进出全屏后重新定位。桌面端保留控件栏、空白拖动画线与 Shift + 左键平移。触控文字编辑与 P0/P3 真机验收仍待实施，状态见 `docs/design/blog/CANVAS_RESPONSIVE_DESIGN.md`。
- 剪贴板图片通过 `src/api/contentFile.ts` 上传，在粘贴选区插入 `/api/system/content-file/<id>` 稳定 URL；拖入图片文件走同一流程。上传前按插入点标题路径和章节内序号生成原始文件名（如 H1 `模块化架构实践` 下 H2 `分层职责` 的第 1 张图为 `模块化架构实践-分层职责-1.png`），保留图片扩展名；后端存储键仍由 provider 按 GUID 生成，存储逻辑不变。
- `ManagedImageNodeView.vue` 用统一请求层读取托管图片，支持私有文章所有者和限时分享令牌。
- 链接跳转：编辑态按住 Ctrl/Cmd 左键点击打开链接，普通点击仍进入编辑；只读阅读态普通左键点击即打开。链接在新标签页打开，`#` 锚点链接滚动到文档内对应标题；加密附件链接（`#ef=`）保持解密下载，`openOnClick` 关闭以免与根节点捕获点击重复打开。
- 全屏状态会发出 `fullscreen-change`；编辑器全屏会 Teleport 到 body。Modal 承载方不得按全屏状态切换 `trap-focus`（Naive UI 会重建弹窗内容并重置编辑器），应关闭弹窗自身 focus trap；全屏下按 Esc 只退出全屏并阻止冒泡。
- 正文不得保存 Blob URL、provider 对象键或短期预签名 URL。

最小验证：

```bash
pnpm type-check
pnpm lint:check
pnpm test:unit -- --run src/components/MarkdownEditor/index.test.ts
```

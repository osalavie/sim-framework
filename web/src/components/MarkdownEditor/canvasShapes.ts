/**
 * 画布形状注册表：形状几何（SVG path 生成器）、尺寸约束与分组统一声明，
 * 编辑态与只读态共用。面板缩略图直接复用几何函数渲染；画布中无轮廓的形状
 * 可额外声明 previewPaths 作为面板示意图标，不影响画布渲染。
 */

export type CanvasShapeGroup = 'general' | 'flowchart' | 'sequence' | 'mindmap' | 'dfd' | 'devops' | 'internal'

export interface CanvasShapePath {
  d: string
  dashed?: boolean
}

export interface CanvasSize {
  width: number
  height: number
}

export interface CanvasShapeDefinition {
  shape: string
  group: CanvasShapeGroup
  labelKey: string
  defaultSize: CanvasSize
  minSize: CanvasSize
  maxSize: CanvasSize
  /** 文本区内边距（px），菱形等形状需要更大留白避免文字出界 */
  labelInset: number
  /** 主轮廓是否填充画布底色；无边框文本形状为 false */
  fill: boolean
  /** 第一个 path 为主轮廓（参与填充），其余为装饰线（仅描边） */
  paths: (width: number, height: number) => CanvasShapePath[]
  /** 图形库面板缩略图专用的示意轮廓；缺省复用 paths，仅在形状本身无轮廓时使用 */
  previewPaths?: (width: number, height: number) => CanvasShapePath[]
  /** 自定义文本区（如生命线头部、框架标签页）；缺省为全节点内边距区域 */
  labelBox?: (width: number, height: number) => { x: number; y: number; width: number; height: number }
}

const CANVAS_SHAPE_FALLBACK = 'rect'

/** 面板拖拽携带 shape key 的 dataTransfer MIME。 */
export const CANVAS_SHAPE_DRAG_MIME = 'application/x-sim-canvas-shape'

const definitions: CanvasShapeDefinition[] = [
  {
    shape: 'note',
    group: 'general',
    labelKey: 'Editor:Canvas:Shape:Note',
    defaultSize: { width: 220, height: 112 },
    minSize: { width: 140, height: 80 },
    maxSize: { width: 480, height: 320 },
    labelInset: 12,
    fill: true,
    paths: (w, h) => {
      const fold = Math.min(18, w / 6, h / 4)
      return [
        { d: `M0 0H${w - fold}L${w} ${fold}V${h}H0Z` },
        { d: `M${w - fold} 0V${fold}H${w}` },
      ]
    },
  },
  {
    shape: 'text',
    group: 'general',
    labelKey: 'Editor:Canvas:Shape:Text',
    defaultSize: { width: 160, height: 40 },
    minSize: { width: 60, height: 24 },
    maxSize: { width: 480, height: 120 },
    labelInset: 2,
    fill: false,
    paths: () => [],
    // 文本形状在画布中无边框，面板缩略图用“矩形框 + 居中 T”示意
    previewPaths: (w, h) => {
      const cx = w / 2
      const cy = h / 2
      const halfBar = Math.min(4, w * 0.18)
      const arm = Math.min(3, h * 0.28)
      return [
        { d: `M0.8 0.8H${w - 0.8}V${h - 0.8}H0.8Z` },
        { d: `M${cx - halfBar} ${cy - arm}H${cx + halfBar}` },
        { d: `M${cx} ${cy - arm}V${cy + arm}` },
      ]
    },
  },
  {
    shape: 'terminator',
    group: 'flowchart',
    labelKey: 'Editor:Canvas:Shape:Terminator',
    defaultSize: { width: 140, height: 56 },
    minSize: { width: 90, height: 36 },
    maxSize: { width: 480, height: 200 },
    labelInset: 10,
    fill: true,
    paths: (w, h) => {
      const r = h / 2
      return [
        { d: `M${r} 0H${w - r}A${r} ${r} 0 0 1 ${w - r} ${h}H${r}A${r} ${r} 0 0 1 ${r} 0Z` },
      ]
    },
  },
  {
    shape: 'rect',
    group: 'flowchart',
    labelKey: 'Editor:Canvas:Shape:Rect',
    defaultSize: { width: 160, height: 80 },
    minSize: { width: 80, height: 40 },
    maxSize: { width: 480, height: 320 },
    labelInset: 10,
    fill: true,
    paths: (w, h) => [{ d: `M0 0H${w}V${h}H0Z` }],
  },
  {
    shape: 'diamond',
    group: 'flowchart',
    labelKey: 'Editor:Canvas:Shape:Diamond',
    defaultSize: { width: 160, height: 96 },
    minSize: { width: 90, height: 60 },
    maxSize: { width: 480, height: 360 },
    labelInset: 28,
    fill: true,
    paths: (w, h) => [{ d: `M${w / 2} 0L${w} ${h / 2}L${w / 2} ${h}L0 ${h / 2}Z` }],
  },
  {
    shape: 'parallelogram',
    group: 'flowchart',
    labelKey: 'Editor:Canvas:Shape:Parallelogram',
    defaultSize: { width: 180, height: 80 },
    minSize: { width: 100, height: 40 },
    maxSize: { width: 480, height: 320 },
    labelInset: 24,
    fill: true,
    paths: (w, h) => {
      const slant = Math.min(28, w * 0.18)
      return [{ d: `M${slant} 0H${w}L${w - slant} ${h}H0Z` }]
    },
  },
  {
    shape: 'document',
    group: 'flowchart',
    labelKey: 'Editor:Canvas:Shape:Document',
    defaultSize: { width: 180, height: 90 },
    minSize: { width: 100, height: 50 },
    maxSize: { width: 480, height: 320 },
    labelInset: 10,
    fill: true,
    paths: (w, h) => [
      {
        d: `M0 0H${w}V${h - 10}Q${w * 0.75} ${h + 6} ${w / 2} ${h - 4}Q${w * 0.25} ${h - 14} 0 ${h - 4}Z`,
      },
    ],
  },
  {
    shape: 'predefined',
    group: 'flowchart',
    labelKey: 'Editor:Canvas:Shape:Predefined',
    defaultSize: { width: 180, height: 80 },
    minSize: { width: 100, height: 40 },
    maxSize: { width: 480, height: 320 },
    labelInset: 16,
    fill: true,
    paths: (w, h) => {
      const inset = Math.min(10, w / 8)
      return [
        { d: `M0 0H${w}V${h}H0Z` },
        { d: `M${inset} 0V${h}` },
        { d: `M${w - inset} 0V${h}` },
      ]
    },
  },
  {
    shape: 'junction',
    group: 'flowchart',
    labelKey: 'Editor:Canvas:Shape:Junction',
    defaultSize: { width: 32, height: 32 },
    minSize: { width: 20, height: 20 },
    maxSize: { width: 80, height: 80 },
    labelInset: 2,
    fill: true,
    paths: (w, h) => {
      const r = Math.min(w, h) / 2
      const cx = w / 2
      const cy = h / 2
      return [
        { d: `M${cx - r} ${cy}A${r} ${r} 0 1 0 ${cx + r} ${cy}A${r} ${r} 0 1 0 ${cx - r} ${cy}Z` },
      ]
    },
  },
  {
    shape: 'actor',
    group: 'sequence',
    labelKey: 'Editor:Canvas:Shape:Actor',
    defaultSize: { width: 80, height: 120 },
    minSize: { width: 50, height: 80 },
    maxSize: { width: 200, height: 300 },
    labelInset: 0,
    fill: false,
    paths: (w, h) => {
      const cx = w / 2
      const headR = h * 0.14
      const bodyTop = headR * 2
      const hip = h * 0.62
      return [
        { d: `M${cx - headR} ${headR}a${headR} ${headR} 0 1 0 ${headR * 2} 0a${headR} ${headR} 0 1 0 ${-headR * 2} 0` },
        { d: `M${cx} ${bodyTop}V${hip}` },
        { d: `M${w * 0.15} ${h * 0.38}H${w * 0.85}` },
        { d: `M${cx} ${hip}L${w * 0.2} ${h}M${cx} ${hip}L${w * 0.8} ${h}` },
      ]
    },
  },
  {
    shape: 'lifeline',
    group: 'sequence',
    labelKey: 'Editor:Canvas:Shape:Lifeline',
    defaultSize: { width: 160, height: 360 },
    minSize: { width: 120, height: 160 },
    maxSize: { width: 320, height: 1100 },
    labelInset: 4,
    fill: true,
    paths: (w, h) => {
      const header = Math.min(48, h / 3)
      return [
        { d: `M0 0H${w}V${header}H0Z` },
        { d: `M${w / 2} ${header}V${h}`, dashed: true },
      ]
    },
    labelBox: (w, h) => ({ x: 4, y: 2, width: w - 8, height: Math.min(48, h / 3) - 4 }),
  },
  {
    shape: 'activation',
    group: 'sequence',
    labelKey: 'Editor:Canvas:Shape:Activation',
    defaultSize: { width: 16, height: 120 },
    minSize: { width: 12, height: 40 },
    maxSize: { width: 40, height: 600 },
    labelInset: 0,
    fill: true,
    paths: (w, h) => [{ d: `M0 0H${w}V${h}H0Z` }],
  },
  {
    shape: 'fragment',
    group: 'sequence',
    labelKey: 'Editor:Canvas:Shape:Fragment',
    defaultSize: { width: 260, height: 160 },
    minSize: { width: 160, height: 100 },
    maxSize: { width: 640, height: 600 },
    labelInset: 6,
    fill: true,
    paths: (w, h) => {
      const tagW = Math.min(w, Math.max(64, w * 0.25))
      const tagH = Math.min(26, h * 0.4)
      return [
        { d: `M0 0H${w}V${h}H0Z` },
        { d: `M0 0H${tagW}L${Math.max(0, tagW - 12)} ${tagH}H0Z` },
      ]
    },
    labelBox: (w, h) => {
      const tagW = Math.min(w, Math.max(64, w * 0.25))
      const tagH = Math.min(26, h * 0.4)
      return { x: 4, y: 2, width: Math.max(12, tagW - 18), height: Math.max(12, tagH - 4) }
    },
  },
  {
    shape: 'mind-central',
    group: 'mindmap',
    labelKey: 'Editor:Canvas:Shape:MindCentral',
    defaultSize: { width: 200, height: 72 },
    minSize: { width: 120, height: 48 },
    maxSize: { width: 480, height: 200 },
    labelInset: 10,
    fill: true,
    paths: (w, h) => {
      const r = Math.min(16, h / 2)
      return [
        { d: `M${r} 0H${w - r}Q${w} 0 ${w} ${r}V${h - r}Q${w} ${h} ${w - r} ${h}H${r}Q0 ${h} 0 ${h - r}V${r}Q0 0 ${r} 0Z` },
      ]
    },
  },
  {
    shape: 'mind-branch',
    group: 'mindmap',
    labelKey: 'Editor:Canvas:Shape:MindBranch',
    defaultSize: { width: 160, height: 48 },
    minSize: { width: 100, height: 32 },
    maxSize: { width: 400, height: 120 },
    labelInset: 10,
    fill: true,
    paths: (w, h) => {
      const r = Math.min(12, h / 2)
      return [
        { d: `M${r} 0H${w - r}Q${w} 0 ${w} ${r}V${h - r}Q${w} ${h} ${w - r} ${h}H${r}Q0 ${h} 0 ${h - r}V${r}Q0 0 ${r} 0Z` },
      ]
    },
  },
  {
    shape: 'mind-leaf',
    group: 'mindmap',
    labelKey: 'Editor:Canvas:Shape:MindLeaf',
    defaultSize: { width: 140, height: 36 },
    minSize: { width: 80, height: 24 },
    maxSize: { width: 400, height: 80 },
    labelInset: 4,
    fill: false,
    paths: (w, h) => [{ d: `M0 ${h}H${w}` }],
  },
  {
    shape: 'dfd-entity',
    group: 'dfd',
    labelKey: 'Editor:Canvas:Shape:DfdEntity',
    defaultSize: { width: 140, height: 80 },
    minSize: { width: 80, height: 48 },
    maxSize: { width: 400, height: 240 },
    labelInset: 10,
    fill: true,
    paths: (w, h) => [{ d: `M0 0H${w}V${h}H0Z` }],
  },
  {
    shape: 'dfd-process',
    group: 'dfd',
    labelKey: 'Editor:Canvas:Shape:DfdProcess',
    defaultSize: { width: 140, height: 100 },
    minSize: { width: 80, height: 60 },
    maxSize: { width: 400, height: 300 },
    labelInset: 18,
    fill: true,
    paths: (w, h) => [
      { d: `M0 ${h / 2}A${w / 2} ${h / 2} 0 1 0 ${w} ${h / 2}A${w / 2} ${h / 2} 0 1 0 0 ${h / 2}Z` },
    ],
  },
  {
    shape: 'dfd-store',
    group: 'dfd',
    labelKey: 'Editor:Canvas:Shape:DfdStore',
    defaultSize: { width: 160, height: 80 },
    minSize: { width: 100, height: 48 },
    maxSize: { width: 480, height: 240 },
    labelInset: 12,
    fill: false,
    paths: (w, h) => {
      const inset = Math.min(12, w / 8)
      return [
        { d: `M${w} 0H0V${h}H${w}` },
        { d: `M${inset} 0V${h}` },
      ]
    },
  },
  {
    shape: 'dev-service',
    group: 'devops',
    labelKey: 'Editor:Canvas:Shape:DevService',
    defaultSize: { width: 160, height: 90 },
    minSize: { width: 90, height: 50 },
    maxSize: { width: 480, height: 280 },
    labelInset: 22,
    fill: true,
    paths: (w, h) => [
      { d: `M${w * 0.12} 0H${w * 0.88}L${w} ${h / 2}L${w * 0.88} ${h}H${w * 0.12}L0 ${h / 2}Z` },
    ],
  },
  {
    shape: 'dev-database',
    group: 'devops',
    labelKey: 'Editor:Canvas:Shape:DevDatabase',
    defaultSize: { width: 140, height: 110 },
    minSize: { width: 80, height: 64 },
    maxSize: { width: 400, height: 300 },
    labelInset: 14,
    fill: true,
    paths: (w, h) => {
      // 顶口椭圆的纵向半径随高度走（封顶 20% 宽），小尺寸预览下也要能看出完整镜片
      const ry = Math.min(Math.max(h * 0.14, 3), w * 0.2)
      return [
        // 罐体：左侧壁 + 底部前弧 + 右侧壁 + 顶部后弧（sweep 0 向上鼓起）闭合，保证底色填充
        { d: `M0 ${ry}V${h - ry}A${w / 2} ${ry} 0 0 0 ${w} ${h - ry}V${ry}A${w / 2} ${ry} 0 0 0 0 ${ry}Z` },
        // 顶口前缘（与后弧合成完整椭圆镜片）+ 两道堆叠盘面，按罐高等分
        { d: `M0 ${ry}A${w / 2} ${ry} 0 0 0 ${w} ${ry}` },
        { d: `M0 ${h * 0.45}A${w / 2} ${ry} 0 0 0 ${w} ${h * 0.45}` },
        { d: `M0 ${h * 0.7}A${w / 2} ${ry} 0 0 0 ${w} ${h * 0.7}` },
      ]
    },
  },
  {
    shape: 'dev-queue',
    group: 'devops',
    labelKey: 'Editor:Canvas:Shape:DevQueue',
    defaultSize: { width: 170, height: 80 },
    minSize: { width: 100, height: 48 },
    maxSize: { width: 480, height: 200 },
    labelInset: 22,
    fill: true,
    paths: (w, h) => {
      const rx = Math.min(18, w * 0.15)
      return [
        { d: `M${rx} 0H${w - rx}A${rx} ${h / 2} 0 0 1 ${w - rx} ${h}H${rx}A${rx} ${h / 2} 0 0 1 ${rx} 0Z` },
        { d: `M${rx} 0A${rx} ${h / 2} 0 0 0 ${rx} ${h}` },
      ]
    },
  },
  {
    shape: 'dev-cloud',
    group: 'devops',
    labelKey: 'Editor:Canvas:Shape:DevCloud',
    defaultSize: { width: 190, height: 110 },
    minSize: { width: 110, height: 64 },
    maxSize: { width: 480, height: 280 },
    labelInset: 26,
    fill: true,
    paths: (w, h) => [
      {
        d: `M${w * 0.25} ${h}H${w * 0.8}C${w * 0.97} ${h} ${w} ${h * 0.72} ${w} ${h * 0.62}C${w} ${h * 0.44} ${w * 0.86} ${h * 0.3} ${w * 0.7} ${h * 0.3}C${w * 0.62} ${h * 0.1} ${w * 0.42} 0 ${w * 0.35} ${h * 0.14}C${w * 0.2} ${h * 0.2} ${w * 0.1} ${h * 0.34} ${w * 0.11} ${h * 0.48}C${w * 0.02} ${h * 0.55} 0 ${h * 0.72} 0 ${h * 0.8}C0 ${h * 0.94} ${w * 0.11} ${h} ${w * 0.25} ${h}Z`,
      },
    ],
  },
  {
    shape: 'dev-stage',
    group: 'devops',
    labelKey: 'Editor:Canvas:Shape:DevStage',
    defaultSize: { width: 160, height: 64 },
    minSize: { width: 100, height: 40 },
    maxSize: { width: 480, height: 200 },
    labelInset: 22,
    fill: true,
    paths: (w, h) => {
      const cut = Math.min(20, w * 0.15)
      return [{ d: `M0 0H${w - cut}L${w} ${h / 2}L${w - cut} ${h}H0L${cut} ${h / 2}Z` }]
    },
  },
  {
    // 自由连线端点锚点：不作为常规元素出现在面板，仅由 dangling 连线自动生成；
    // 呈现为实心小点（渲染取 --shape-accent 填充、无描边，见 CanvasShapeNode）
    shape: 'anchor',
    group: 'internal',
    labelKey: 'Editor:Canvas:Shape:Anchor',
    defaultSize: { width: 10, height: 10 },
    minSize: { width: 8, height: 8 },
    maxSize: { width: 24, height: 24 },
    labelInset: 0,
    fill: true,
    paths: (w, h) => {
      const r = Math.min(w, h) / 2 - 1.5
      const cx = w / 2
      const cy = h / 2
      return [
        { d: `M${cx - r} ${cy}A${r} ${r} 0 1 0 ${cx + r} ${cy}A${r} ${r} 0 1 0 ${cx - r} ${cy}Z` },
      ]
    },
  },
]

const registry = new Map(definitions.map((definition) => [definition.shape, definition]))

export const CANVAS_SHAPE_GROUPS: readonly CanvasShapeGroup[] = ['general', 'flowchart', 'sequence', 'mindmap', 'dfd', 'devops']

export const isCanvasShape = (value: unknown): value is string =>
  typeof value === 'string' && registry.has(value)

export const getCanvasShape = (shape: string): CanvasShapeDefinition =>
  registry.get(shape) ?? (registry.get(CANVAS_SHAPE_FALLBACK) as CanvasShapeDefinition)

export const canvasShapesByGroup = (group: CanvasShapeGroup): CanvasShapeDefinition[] =>
  definitions.filter((definition) => definition.group === group)

export const allCanvasShapes = (): CanvasShapeDefinition[] => definitions

export { CANVAS_SHAPE_FALLBACK }

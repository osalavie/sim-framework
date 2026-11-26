/**
 * 画布导出 PNG/JPG：html-to-image 捕获 `.vue-flow__transformationpane`——Vue Flow 中承载
 * pan/zoom 变换的那一层，节点、连线、箭头、边标签都在其中，Background/Controls/MiniMap/
 * 参考线不在该子树内。捕获根自身的画布变换由导出计划覆盖，所以出图与当前缩放、平移无关，
 * 也不会被视口裁剪：图像范围取全部元素的并集包围盒 + 留白，最长边降采样保护内存。
 */
import { toJpeg, toPng } from 'html-to-image'
import { sanitizeFileNameSegment } from './imageUploadNaming'

export type CanvasImageFormat = 'png' | 'jpeg'

export interface CanvasContentRect {
  x: number
  y: number
  width: number
  height: number
}

export interface CanvasFlowPoint {
  x: number
  y: number
}

export interface CanvasExportPlan {
  width: number
  height: number
  transform: string
  pixelRatio: number
  backgroundColor: string
}

/** 捕获根：Vue Flow 承载画布变换的层，节点/连线/箭头/边标签都在这一层之下。 */
export const CANVAS_EXPORT_ROOT_SELECTOR = '.vue-flow__transformationpane'

/** 参与包围盒计算的元素：节点、连线路径、自绘箭头、边标签。 */
export const CANVAS_EXPORT_CONTENT_SELECTOR = [
  '.vue-flow__node',
  '.vue-flow__edge-path',
  '.edge-arrowhead',
  '.edge-label',
].join(', ')

const PADDING = 24
const MAX_SIDE = 4096
const MAX_PIXEL_RATIO = 2

/** 元素集合的内容包围盒；空集合返回 null。 */
export function canvasContentBounds(rects: CanvasContentRect[]): CanvasContentRect | null {
  if (rects.length === 0) return null
  const minX = Math.min(...rects.map((rect) => rect.x))
  const minY = Math.min(...rects.map((rect) => rect.y))
  const maxX = Math.max(...rects.map((rect) => rect.x + rect.width))
  const maxY = Math.max(...rects.map((rect) => rect.y + rect.height))
  return { x: minX, y: minY, width: maxX - minX, height: maxY - minY }
}

/**
 * 导出计划：图像尺寸 = 包围盒 + 四周留白（流内坐标），
 * 捕获根平移到内容原点、scale(1) 抵消画布缩放，出图固定为 authored 像素尺寸；
 * pixelRatio 限制最长边 4096px（超出即降采样）。
 */
export function planCanvasExport(
  bounds: CanvasContentRect,
  backgroundColor: string,
): CanvasExportPlan {
  const width = Math.ceil(bounds.width + PADDING * 2)
  const height = Math.ceil(bounds.height + PADDING * 2)
  const originX = bounds.x - PADDING
  const originY = bounds.y - PADDING
  const pixelRatio = Math.min(MAX_PIXEL_RATIO, MAX_SIDE / Math.max(width, height, 1))
  return {
    width,
    height,
    transform: `translate(${-originX}px, ${-originY}px) scale(1)`,
    pixelRatio,
    backgroundColor,
  }
}

/**
 * 采集导出根的完整内容包围盒（流坐标）：逐个元素取屏幕矩形，再用画布的
 * screenToFlowCoordinate 换算回流坐标。节点盒、连线绕行、箭头与边标签只要有一处
 * 越出节点范围都会被计入，因此导出图包含完整元素区域，而不仅是被视口裁到的部分。
 */
export function measureCanvasContentBounds(
  root: ParentNode,
  screenToFlow: (point: CanvasFlowPoint) => CanvasFlowPoint,
): CanvasContentRect | null {
  const rects: CanvasContentRect[] = []
  root.querySelectorAll<Element>(CANVAS_EXPORT_CONTENT_SELECTOR).forEach((element) => {
    const box = element.getBoundingClientRect()
    if (box.width <= 0 && box.height <= 0) return
    const start = screenToFlow({ x: box.left, y: box.top })
    const end = screenToFlow({ x: box.right, y: box.bottom })
    rects.push({ x: start.x, y: start.y, width: end.x - start.x, height: end.y - start.y })
  })
  return canvasContentBounds(rects)
}

/**
 * 导出需要固化到捕获根上的画布主题令牌：html-to-image 只复制 documentElement 上的
 * 自定义属性，`.canvas-block` 上定义的 `--canvas-*` 不会进入克隆文档，而 SVG 填充/描边
 * 直接以 var() 引用这些令牌，丢失后会退回浅色兜底值（深色主题下节点变白、文字不可见）。
 * 捕获根是 shallow clone（保留内联 style），把令牌写到捕获根即可被整棵克隆子树继承。
 */
export const CANVAS_EXPORT_THEME_PROPERTIES = [
  '--canvas-surface',
  '--canvas-stage',
  '--canvas-border',
  '--canvas-text',
  '--canvas-muted',
  '--canvas-accent',
] as const

export function resolveCanvasThemeProperties(source: HTMLElement): Record<string, string> {
  const computed = getComputedStyle(source)
  const resolved: Record<string, string> = {}
  for (const property of CANVAS_EXPORT_THEME_PROPERTIES) {
    const value = computed.getPropertyValue(property).trim()
    if (value) resolved[property] = value
  }
  return resolved
}

/** 导出期间把主题令牌临时内联到捕获根，结束后恢复原有内联值。 */
export async function withCanvasThemeProperties<T>(
  root: HTMLElement,
  properties: Record<string, string>,
  run: () => Promise<T>,
): Promise<T> {
  const previous = Object.keys(properties).map((property) => ({
    property,
    value: root.style.getPropertyValue(property),
    priority: root.style.getPropertyPriority(property),
  }))
  try {
    for (const [property, value] of Object.entries(properties)) {
      root.style.setProperty(property, value)
    }
    return await run()
  } finally {
    for (const { property, value, priority } of previous) {
      if (value) root.style.setProperty(property, value, priority)
      else root.style.removeProperty(property)
    }
  }
}

/**
 * `{文章标题}-{各级标题}-{画布序号}.png|jpg`；
 * 空片段自动省略，全部为空时回退为 `canvas`。
 */
export const canvasExportFileName = (
  format: CanvasImageFormat,
  segments: readonly string[] = [],
) => {
  const stem = segments.map(sanitizeFileNameSegment).filter(Boolean).join('-')
  return `${stem || 'canvas'}.${format === 'jpeg' ? 'jpg' : 'png'}`
}

/** 渲染画布位图，返回 dataURL；下载与导出内嵌共用同一套捕获参数。 */
export async function renderCanvasImage(
  rootEl: HTMLElement,
  plan: CanvasExportPlan,
  format: CanvasImageFormat,
): Promise<string> {
  const options = {
    width: plan.width,
    height: plan.height,
    pixelRatio: plan.pixelRatio,
    backgroundColor: plan.backgroundColor,
    style: { transform: plan.transform, transformOrigin: '0 0' },
    ...(format === 'jpeg' ? { quality: 0.92 } : {}),
  }
  return format === 'jpeg' ? toJpeg(rootEl, options) : toPng(rootEl, options)
}

/** 画布底色是透明时用白底出图：PNG 会带 alpha，打印和 Markdown 预览都需要不透明白底。 */
export function resolveCanvasBackground(backgroundColor: string): string {
  const normalized = backgroundColor.trim().toLowerCase()
  if (!normalized || normalized === 'transparent' || /^rgba\([^)]*,\s*0\)$/.test(normalized)) {
    return '#ffffff'
  }
  return backgroundColor
}

export async function exportCanvasImage(
  rootEl: HTMLElement,
  plan: CanvasExportPlan,
  format: CanvasImageFormat,
  fileName: string,
): Promise<void> {
  const dataUrl = await renderCanvasImage(rootEl, plan, format)
  const link = document.createElement('a')
  link.href = dataUrl
  link.download = fileName
  link.click()
}

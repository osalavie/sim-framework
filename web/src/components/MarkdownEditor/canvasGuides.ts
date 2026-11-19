/**
 * 拖动对齐与吸附：helper-lines 模式的纯计算实现。
 * 拖动节点（含多选整体包围盒）与其余节点的边线/中线做候选比较，
 * 阈值内命中即给出吸附偏移与应显示的参考线坐标（画布坐标）；
 * 未命中参考线时回退为网格吸附（参考线优先）。
 */

export interface GuideRect {
  id: string
  x: number
  y: number
  width: number
  height: number
}

export interface CanvasGuideLines {
  vertical: number[]
  horizontal: number[]
}

export interface AlignmentResult {
  offsetX: number
  offsetY: number
  snappedX: boolean
  snappedY: boolean
  guides: CanvasGuideLines
}

export const EMPTY_GUIDES: CanvasGuideLines = { vertical: [], horizontal: [] }

const unionBounds = (rects: GuideRect[]): GuideRect => {
  const left = Math.min(...rects.map((rect) => rect.x))
  const top = Math.min(...rects.map((rect) => rect.y))
  const right = Math.max(...rects.map((rect) => rect.x + rect.width))
  const bottom = Math.max(...rects.map((rect) => rect.y + rect.height))
  return { id: '__bounds__', x: left, y: top, width: right - left, height: bottom - top }
}

const anchorsX = (rect: GuideRect) => [rect.x, rect.x + rect.width / 2, rect.x + rect.width]
const anchorsY = (rect: GuideRect) => [rect.y, rect.y + rect.height / 2, rect.y + rect.height]

const nearestSnap = (anchors: number[], candidates: number[], threshold: number) => {
  let best: { offset: number; line: number } | null = null
  for (const anchor of anchors) {
    for (const candidate of candidates) {
      const offset = candidate - anchor
      if (Math.abs(offset) <= threshold && (!best || Math.abs(offset) < Math.abs(best.offset))) {
        best = { offset, line: candidate }
      }
    }
  }
  return best
}

/** 吸附生效后，收集与任一锚点重合的全部候选线（同坐标去重）。 */
const matchedLines = (anchors: number[], candidates: number[], epsilon = 0.5) => {
  const lines: number[] = []
  for (const candidate of candidates) {
    if (anchors.some((anchor) => Math.abs(anchor - candidate) <= epsilon)) lines.push(candidate)
  }
  return [...new Set(lines)].sort((a, b) => a - b)
}

/** 参考线对齐：返回吸附偏移与命中后的全部可视参考线。 */
export function computeAlignmentGuides(
  moving: GuideRect[],
  others: GuideRect[],
  threshold: number,
): AlignmentResult {
  if (moving.length === 0 || others.length === 0 || threshold <= 0) {
    return { offsetX: 0, offsetY: 0, snappedX: false, snappedY: false, guides: EMPTY_GUIDES }
  }

  const bounds = unionBounds(moving)
  const candidatesX = [...new Set(others.flatMap(anchorsX))]
  const candidatesY = [...new Set(others.flatMap(anchorsY))]

  const snapX = nearestSnap(anchorsX(bounds), candidatesX, threshold)
  const snapY = nearestSnap(anchorsY(bounds), candidatesY, threshold)
  const offsetX = snapX?.offset ?? 0
  const offsetY = snapY?.offset ?? 0

  return {
    offsetX,
    offsetY,
    snappedX: snapX !== null,
    snappedY: snapY !== null,
    guides: {
      vertical: snapX
        ? matchedLines(anchorsX(bounds).map((anchor) => anchor + offsetX), candidatesX)
        : [],
      horizontal: snapY
        ? matchedLines(anchorsY(bounds).map((anchor) => anchor + offsetY), candidatesY)
        : [],
    },
  }
}

const gridDelta = (value: number, gridSize: number) =>
  Math.round(value / gridSize) * gridSize - value

/**
 * 拖动吸附总入口：轴向优先采用参考线吸附，未命中时按 gridSize 网格吸附
 * （网格基准为 gridAnchor——主动拖拽节点——的左上角，缺省取 moving 首个元素）。
 */
export function computeDragSnap(
  moving: GuideRect[],
  others: GuideRect[],
  threshold: number,
  gridSize: number,
  gridAnchor?: GuideRect,
): AlignmentResult {
  const alignment = computeAlignmentGuides(moving, others, threshold)
  const primary = gridAnchor ?? moving[0]
  const gridX = primary && gridSize > 0 && !alignment.snappedX ? gridDelta(primary.x, gridSize) : 0
  const gridY = primary && gridSize > 0 && !alignment.snappedY ? gridDelta(primary.y, gridSize) : 0

  return {
    offsetX: alignment.snappedX ? alignment.offsetX : gridX,
    offsetY: alignment.snappedY ? alignment.offsetY : gridY,
    snappedX: alignment.snappedX,
    snappedY: alignment.snappedY,
    guides: alignment.guides,
  }
}

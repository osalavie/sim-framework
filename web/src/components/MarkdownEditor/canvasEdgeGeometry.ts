import { Position } from '@vue-flow/core'

export interface CanvasGeometryPoint {
  x: number
  y: number
}

/** handle 盒子在全局 border-box 下总宽 11px（7px 内容 + 2px 边框 ×2），VueFlow 端点取盒子外沿中点。 */
export const CANVAS_HANDLE_HALF = 5.5

/**
 * 把 VueFlow 给出的边端点从 handle 盒子外沿回收到盒子中心（即锚点视觉位置）。
 * 不移正时连线端点会偏离锚点约半个 handle 盒（5.5px）。
 */
export function adjustCanvasEdgePoint(
  x: number,
  y: number,
  position: Position | undefined,
): { x: number; y: number } {
  switch (position) {
    case Position.Top:
      return { x, y: y + CANVAS_HANDLE_HALF }
    case Position.Bottom:
      return { x, y: y - CANVAS_HANDLE_HALF }
    case Position.Left:
      return { x: x + CANVAS_HANDLE_HALF, y }
    case Position.Right:
      return { x: x - CANVAS_HANDLE_HALF, y }
    default:
      return { x, y }
  }
}

export interface CanvasHandleCandidate {
  nodeId: string
  handleId: string | null
  /** handle 中心坐标（与查询点同一坐标系，通常为屏幕坐标） */
  x: number
  y: number
}

/** 半径内最近的 handle 命中；平局保留先收集到的候选（DOM 顺序）。 */
export function pickNearestHandle<T extends CanvasHandleCandidate>(
  candidates: readonly T[],
  x: number,
  y: number,
  radius: number,
): T | null {
  let best: T | null = null
  let bestDistance = Number.POSITIVE_INFINITY
  for (const candidate of candidates) {
    const distance = Math.hypot(x - candidate.x, y - candidate.y)
    if (distance > radius || distance >= bestDistance) continue
    best = candidate
    bestDistance = distance
  }
  return best
}

const round = (value: number) => Math.round(value * 100) / 100

export interface CanvasSegmentMidpoint extends CanvasGeometryPoint {
  /** 该线段属于出发点序列的第几段（0 = 起点→第一个折点） */
  insertAfter: number
}

export interface OrthogonalRoute {
  path: string
  midpoints: CanvasSegmentMidpoint[]
  /** 路由脊线（含插入的直角拐角点），末两点即到达目标的最终线段方向 */
  points: CanvasGeometryPoint[]
}

/**
 * 正交折线路由：相邻锚点坐标都不同时按主方向插入直角拐角，
 * 转角处用二次曲线倒角；同时给出各线段中点（用于“+”新增折点）。
 * 轴向对齐判断带 0.5px 容差——坐标来源精度不同（handle 全精度 vs 折点两位小数），
 * 严格相等会把共线误判为斜向、插入零长度拐角。
 */
export function buildOrthogonalRoute(
  anchors: CanvasGeometryPoint[],
  radius = 8,
): OrthogonalRoute {
  if (anchors.length < 2) return { path: '', midpoints: [], points: [] }

  const ALIGN_TOLERANCE = 0.5
  const route: Array<{ point: CanvasGeometryPoint; insertAfter: number }> = [
    { point: anchors[0] as CanvasGeometryPoint, insertAfter: 0 },
  ]
  for (let i = 0; i < anchors.length - 1; i++) {
    const from = anchors[i] as CanvasGeometryPoint
    const to = anchors[i + 1] as CanvasGeometryPoint
    const dx = to.x - from.x
    const dy = to.y - from.y
    const alignedX = Math.abs(dx) < ALIGN_TOLERANCE
    const alignedY = Math.abs(dy) < ALIGN_TOLERANCE
    if (!alignedX && !alignedY) {
      const horizontalFirst = Math.abs(dx) >= Math.abs(dy)
      const corner = horizontalFirst ? { x: to.x, y: from.y } : { x: from.x, y: to.y }
      route.push({ point: corner, insertAfter: i })
    }
    route.push({ point: to, insertAfter: i })
  }

  const midpoints: CanvasSegmentMidpoint[] = []
  for (let i = 0; i < route.length - 1; i++) {
    const a = route[i] as { point: CanvasGeometryPoint; insertAfter: number }
    const b = route[i + 1] as { point: CanvasGeometryPoint; insertAfter: number }
    midpoints.push({
      x: round((a.point.x + b.point.x) / 2),
      y: round((a.point.y + b.point.y) / 2),
      insertAfter: b.insertAfter,
    })
  }

  let path = `M${round(route[0]!.point.x)} ${round(route[0]!.point.y)}`
  for (let i = 1; i < route.length - 1; i++) {
    const prev = route[i - 1]!.point
    const current = route[i]!.point
    const next = route[i + 1]!.point
    const inLength = Math.hypot(current.x - prev.x, current.y - prev.y)
    const outLength = Math.hypot(next.x - current.x, next.y - current.y)
    // 圆角半径固定，仅按相邻段长上限收敛（不再取半段），拖动折点时拐角样式保持一致
    const r = Math.min(radius, inLength, outLength)
    if (r < 0.5) {
      path += `L${round(current.x)} ${round(current.y)}`
      continue
    }
    const entry = {
      x: current.x - ((current.x - prev.x) / inLength) * r,
      y: current.y - ((current.y - prev.y) / inLength) * r,
    }
    const exit = {
      x: current.x + ((next.x - current.x) / outLength) * r,
      y: current.y + ((next.y - current.y) / outLength) * r,
    }
    path += `L${round(entry.x)} ${round(entry.y)}Q${round(current.x)} ${round(current.y)} ${round(exit.x)} ${round(exit.y)}`
  }
  const last = route[route.length - 1]!.point
  path += `L${round(last.x)} ${round(last.y)}`
  return { path, midpoints, points: route.map((entry) => entry.point) }
}

/**
 * Catmull-Rom 转三次贝塞尔的平滑曲线，穿过所有给定点（含折点）。
 * 仅两点时退化为直线。
 */
export function buildCatmullRomPath(points: CanvasGeometryPoint[]): string {
  if (points.length < 2) return ''
  const first = points[0] as CanvasGeometryPoint
  if (points.length === 2) {
    const last = points[1] as CanvasGeometryPoint
    return `M${round(first.x)} ${round(first.y)}L${round(last.x)} ${round(last.y)}`
  }
  let path = `M${round(first.x)} ${round(first.y)}`
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[Math.max(i - 1, 0)] as CanvasGeometryPoint
    const p1 = points[i] as CanvasGeometryPoint
    const p2 = points[i + 1] as CanvasGeometryPoint
    const p3 = points[Math.min(i + 2, points.length - 1)] as CanvasGeometryPoint
    const c1 = { x: p1.x + (p2.x - p0.x) / 6, y: p1.y + (p2.y - p0.y) / 6 }
    const c2 = { x: p2.x - (p3.x - p1.x) / 6, y: p2.y - (p3.y - p1.y) / 6 }
    path += `C${round(c1.x)} ${round(c1.y)} ${round(c2.x)} ${round(c2.y)} ${round(p2.x)} ${round(p2.y)}`
  }
  return path
}

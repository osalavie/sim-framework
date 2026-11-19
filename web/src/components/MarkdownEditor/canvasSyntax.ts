import { CANVAS_SHAPE_FALLBACK, getCanvasShape, isCanvasShape } from './canvasShapes'

export type CanvasEdgeType = 'smoothstep' | 'straight' | 'bezier'
export type CanvasLineStyle = 'solid' | 'dashed'
export type CanvasArrowHead = 'none' | 'arrow' | 'open'

/** 端口方位沿用 Vue Flow Position 字符串，与连接时记录的 handle id 一致。 */
export type CanvasPort = 'top' | 'right' | 'bottom' | 'left'

export interface CanvasNodeSpec {
  id: string
  shape: string
  x: number
  y: number
  width: number
  height: number
  label: string
  color: string
}

export interface CanvasPoint {
  x: number
  y: number
}

export interface CanvasEdgeSpec {
  id: string
  source: string
  target: string
  sourcePort?: CanvasPort
  targetPort?: CanvasPort
  type: CanvasEdgeType
  line: CanvasLineStyle
  arrow: CanvasArrowHead
  label: string
  /** 折点（贝塞尔/正交线），画布坐标，按路径顺序排列。 */
  waypoints?: CanvasPoint[]
}

export interface CanvasDocument {
  version: 2
  height: number
  nodes: CanvasNodeSpec[]
  edges: CanvasEdgeSpec[]
}

export const EMPTY_CANVAS: CanvasDocument = { version: 2, height: 480, nodes: [], edges: [] }
export const CANVAS_PATTERN = /```canvas\s*\n([\s\S]*?)\n```/g

const MAX_NODES = 200
const MAX_EDGES = 400
const MAX_WAYPOINTS = 24
const DEFAULT_COLOR = '#2f7d6d'
const EDGE_TYPES: readonly CanvasEdgeType[] = ['smoothstep', 'straight', 'bezier']
const LINE_STYLES: readonly CanvasLineStyle[] = ['solid', 'dashed']
const ARROW_HEADS: readonly CanvasArrowHead[] = ['none', 'arrow', 'open']
const PORTS: readonly CanvasPort[] = ['top', 'right', 'bottom', 'left']

const normalizeCanvasColor = (color: unknown) =>
  typeof color === 'string' && /^#[0-9a-f]{6}$/i.test(color) ? color : DEFAULT_COLOR

const boundedNumber = (value: unknown, minimum: number, maximum: number, fallback: number) => {
  const parsed = Number(value)
  return Number.isFinite(parsed) ? Math.min(maximum, Math.max(minimum, parsed)) : fallback
}

const boundedText = (value: unknown, maximum: number) =>
  (typeof value === 'string' ? value : '').slice(0, maximum)

const oneOf = <T>(value: unknown, allowed: readonly T[], fallback: T): T =>
  allowed.includes(value as T) ? (value as T) : fallback

const normalizePort = (value: unknown): CanvasPort | undefined =>
  PORTS.includes(value as CanvasPort) ? (value as CanvasPort) : undefined

const normalizeWaypoints = (value: unknown): CanvasPoint[] | undefined => {
  if (!Array.isArray(value)) return undefined
  const points = value.slice(0, MAX_WAYPOINTS).map((point): CanvasPoint => ({
    x: boundedNumber(point?.x, 0, 5000, 0),
    y: boundedNumber(point?.y, 0, 2400, 0),
  }))
  return points.length > 0 ? points : undefined
}

export function cloneCanvas(document: CanvasDocument): CanvasDocument {
  return {
    version: 2,
    height: document.height,
    nodes: document.nodes.map((node) => ({ ...node })),
    edges: document.edges.map((edge) => ({ ...edge })),
  }
}

export function parseCanvas(source: string): CanvasDocument {
  const value = JSON.parse(source) as Partial<CanvasDocument>
  // 仅接受 v2 文档；v1 及未知版本按空画布兜底，不做迁移。
  if (value.version !== 2) return cloneCanvas(EMPTY_CANVAS)

  const nodes = Array.isArray(value.nodes)
    ? value.nodes.slice(0, MAX_NODES).map((node, index): CanvasNodeSpec => {
        const shape = isCanvasShape(node?.shape) ? (node.shape as string) : CANVAS_SHAPE_FALLBACK
        // 尺寸钳制按形状注册表取界：端点锚点等小形状有各自下限（如 10px），
        // 用通用下限会把回环解析后的锚点放大，端点圆点随之变大
        const definition = getCanvasShape(shape)
        return {
          id: String(node?.id || `node-${index}`),
          shape,
          x: boundedNumber(node?.x, 0, 5000, 0),
          y: boundedNumber(node?.y, 0, 2400, 0),
          width: boundedNumber(
            node?.width,
            definition.minSize.width,
            definition.maxSize.width,
            definition.defaultSize.width,
          ),
          height: boundedNumber(
            node?.height,
            definition.minSize.height,
            definition.maxSize.height,
            definition.defaultSize.height,
          ),
          label: boundedText(node?.label, 200),
          color: normalizeCanvasColor(node?.color),
        }
      })
    : []
  const nodeIds = new Set(nodes.map((node) => node.id))
  const edges = Array.isArray(value.edges)
    ? value.edges
        .slice(0, MAX_EDGES)
        .filter((edge) => nodeIds.has(String(edge?.source)) && nodeIds.has(String(edge?.target)))
        .map((edge, index): CanvasEdgeSpec => {
          const sourcePort = normalizePort(edge?.sourcePort)
          const targetPort = normalizePort(edge?.targetPort)
          const waypoints = normalizeWaypoints(edge?.waypoints)
          return {
            id: String(edge?.id || `edge-${index}`),
            source: String(edge?.source),
            target: String(edge?.target),
            ...(sourcePort ? { sourcePort } : {}),
            ...(targetPort ? { targetPort } : {}),
            type: oneOf(edge?.type, EDGE_TYPES, 'smoothstep'),
            line: oneOf(edge?.line, LINE_STYLES, 'solid'),
            arrow: oneOf(edge?.arrow, ARROW_HEADS, 'arrow'),
            label: boundedText(edge?.label, 120),
            ...(waypoints ? { waypoints } : {}),
          }
        })
    : []

  return {
    version: 2,
    height: boundedNumber(value.height, 240, 2400, 480),
    nodes,
    edges,
  }
}

export function stringifyCanvas(document: CanvasDocument): string {
  return `\`\`\`canvas\n${JSON.stringify(document)}\n\`\`\``
}

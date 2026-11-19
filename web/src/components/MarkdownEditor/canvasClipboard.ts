/**
 * 画布内复制粘贴的纯计算部分：从选中集合构建剪贴板载荷，
 * 粘贴时重新分配 id 并整体偏移。剪贴板存内存（模块级），支持跨画布粘贴。
 */
import type { CanvasEdgeSpec, CanvasNodeSpec } from './canvasSyntax'

export interface CanvasClipboardPayload {
  nodes: CanvasNodeSpec[]
  edges: CanvasEdgeSpec[]
}

/** 收集选中节点及两端均在选中集合内的边；无选中节点时返回 null。 */
export function buildClipboardPayload(
  selectedNodeIds: ReadonlySet<string>,
  nodes: CanvasNodeSpec[],
  edges: CanvasEdgeSpec[],
): CanvasClipboardPayload | null {
  const picked = nodes.filter((node) => selectedNodeIds.has(node.id)).map((node) => ({ ...node }))
  if (picked.length === 0) return null
  const pickedIds = new Set(picked.map((node) => node.id))
  return {
    nodes: picked,
    edges: edges
      .filter((edge) => pickedIds.has(edge.source) && pickedIds.has(edge.target))
      .map((edge) => ({ ...edge })),
  }
}

/** 粘贴重映射：节点/边换新 id，坐标整体偏移 offset，端点引用同步改写。 */
export function remapClipboardForPaste(
  payload: CanvasClipboardPayload,
  offset: number,
  createId: () => string,
): CanvasClipboardPayload {
  const idMap = new Map(payload.nodes.map((node) => [node.id, createId()]))
  return {
    nodes: payload.nodes.map((node) => ({
      ...node,
      id: idMap.get(node.id) as string,
      x: node.x + offset,
      y: node.y + offset,
    })),
    edges: payload.edges.map((edge) => ({
      ...edge,
      id: createId(),
      source: idMap.get(edge.source) ?? edge.source,
      target: idMap.get(edge.target) ?? edge.target,
    })),
  }
}

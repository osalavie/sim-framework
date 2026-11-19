import { describe, expect, it } from 'vitest'
import { buildClipboardPayload, remapClipboardForPaste } from './canvasClipboard'
import type { CanvasEdgeSpec, CanvasNodeSpec } from './canvasSyntax'

const nodes: CanvasNodeSpec[] = [
  { id: 'a', shape: 'rect', x: 20, y: 20, width: 160, height: 80, label: 'A', color: '#2f7d6d' },
  { id: 'b', shape: 'diamond', x: 260, y: 40, width: 160, height: 96, label: 'B', color: '#2f7d6d' },
  { id: 'c', shape: 'rect', x: 520, y: 60, width: 160, height: 80, label: 'C', color: '#2f7d6d' },
]

const edges: CanvasEdgeSpec[] = [
  { id: 'a-b', source: 'a', target: 'b', sourcePort: 'right', targetPort: 'left', type: 'smoothstep', line: 'solid', arrow: 'arrow', label: '' },
  { id: 'b-c', source: 'b', target: 'c', type: 'straight', line: 'dashed', arrow: 'open', label: '外' },
]

describe('buildClipboardPayload', () => {
  it('returns null when no node is selected', () => {
    expect(buildClipboardPayload(new Set(), nodes, edges)).toBeNull()
  })

  it('keeps only edges whose both endpoints are selected', () => {
    const payload = buildClipboardPayload(new Set(['a', 'b']), nodes, edges)
    expect(payload?.nodes.map((node) => node.id)).toEqual(['a', 'b'])
    expect(payload?.edges.map((edge) => edge.id)).toEqual(['a-b'])
  })
})

describe('remapClipboardForPaste', () => {
  it('reassigns ids, offsets positions and rewires edge endpoints', () => {
    let sequence = 0
    const payload = buildClipboardPayload(new Set(['a', 'b']), nodes, edges)!
    const remapped = remapClipboardForPaste(payload, 20, () => `new-${sequence++}`)

    expect(remapped.nodes.map((node) => node.id)).toEqual(['new-0', 'new-1'])
    expect(remapped.nodes[0]).toMatchObject({ x: 40, y: 40, label: 'A' })
    expect(remapped.edges[0]).toMatchObject({ id: 'new-2', source: 'new-0', target: 'new-1' })
    // 原载荷不被改写
    expect(payload.nodes[0]?.id).toBe('a')
    expect(payload.edges[0]?.source).toBe('a')
  })
})

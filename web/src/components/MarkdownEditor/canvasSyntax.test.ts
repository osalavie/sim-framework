import { describe, expect, it } from 'vitest'
import { EMPTY_CANVAS, parseCanvas, stringifyCanvas, type CanvasDocument } from './canvasSyntax'

const sampleDocument = (): CanvasDocument => ({
  version: 2,
  height: 480,
  nodes: [
    { id: 'a', shape: 'rect', x: 20, y: 20, width: 160, height: 80, label: '开始', color: '#2f7d6d' },
    { id: 'b', shape: 'diamond', x: 260, y: 40, width: 160, height: 96, label: '判断', color: '#aa3a3a' },
  ],
  edges: [
    {
      id: 'a-b',
      source: 'a',
      target: 'b',
      sourcePort: 'right',
      targetPort: 'left',
      type: 'straight',
      line: 'dashed',
      arrow: 'open',
      label: '是',
    },
  ],
})

describe('canvas syntax v2', () => {
  it('round-trips a v2 canvas fenced block', () => {
    const source = stringifyCanvas(sampleDocument())
    const json = source.slice(source.indexOf('\n') + 1, source.lastIndexOf('\n'))
    expect(parseCanvas(json)).toEqual(sampleDocument())
  })

  it('round-trips the empty canvas', () => {
    const source = stringifyCanvas(EMPTY_CANVAS)
    const json = source.slice(source.indexOf('\n') + 1, source.lastIndexOf('\n'))
    expect(parseCanvas(json)).toEqual(EMPTY_CANVAS)
  })

  it('falls back to an empty canvas for v1 documents (no migration)', () => {
    expect(parseCanvas('{"version":1,"height":420,"items":[]}')).toEqual(EMPTY_CANVAS)
  })

  it('clamps out-of-range geometry and falls back on unknown shapes and colors', () => {
    const value = parseCanvas(JSON.stringify({
      version: 2,
      height: 99999,
      nodes: [
        { id: 'a', shape: 'not-a-shape', x: -50, y: 99999, width: 1, height: 99999, label: 'x'.repeat(500), color: 'red' },
      ],
      edges: [],
    }))

    expect(value.height).toBe(2400)
    // 尺寸钳制按形状注册表取界：rect 为 80..480 / 40..320
    expect(value.nodes[0]).toMatchObject({ shape: 'rect', x: 0, y: 2400, width: 80, height: 320, color: '#2f7d6d' })
    expect(value.nodes[0]?.label).toHaveLength(200)
  })

  it('keeps small internal anchors at their own size on parse (round-trip regression)', () => {
    const value = parseCanvas(JSON.stringify({
      version: 2,
      height: 480,
      nodes: [
        { id: 'a', shape: 'anchor', x: 300, y: 120, width: 10, height: 10, label: '', color: '#2f7d6d' },
      ],
      edges: [],
    }))

    // 锚点下限 8/24 上限 24：10×10 不得被通用下限放大为 40×24
    expect(value.nodes[0]).toMatchObject({ shape: 'anchor', width: 10, height: 10 })
  })

  it('keeps only edges whose endpoints exist and normalizes edge style fields', () => {
    const value = parseCanvas(JSON.stringify({
      version: 2,
      height: 480,
      nodes: [
        { id: 'a', shape: 'rect', x: 0, y: 0, width: 160, height: 80, label: '', color: '#2f7d6d' },
        { id: 'b', shape: 'rect', x: 260, y: 0, width: 160, height: 80, label: '', color: '#2f7d6d' },
      ],
      edges: [
        { id: 'valid', source: 'a', target: 'b', sourcePort: 'left', targetPort: 'right', type: 'weird', line: 'dotted', arrow: 'fancy', label: 'ok' },
        { id: 'invalid', source: 'a', target: 'missing' },
      ],
    }))

    expect(value.edges).toEqual([
      { id: 'valid', source: 'a', target: 'b', sourcePort: 'left', targetPort: 'right', type: 'smoothstep', line: 'solid', arrow: 'arrow', label: 'ok' },
    ])
  })

  it('round-trips waypoints and clamps them to the canvas bounds', () => {
    const withWaypoints = parseCanvas(JSON.stringify({
      version: 2,
      height: 480,
      nodes: [
        { id: 'a', shape: 'rect', x: 0, y: 0, width: 160, height: 80, label: '', color: '#2f7d6d' },
        { id: 'b', shape: 'rect', x: 260, y: 0, width: 160, height: 80, label: '', color: '#2f7d6d' },
      ],
      edges: [
        { id: 'e', source: 'a', target: 'b', type: 'smoothstep', line: 'solid', arrow: 'arrow', label: '', waypoints: [{ x: 120, y: 40 }, { x: 9999, y: -50 }] },
      ],
    }))
    expect(withWaypoints.edges[0]?.waypoints).toEqual([{ x: 120, y: 40 }, { x: 5000, y: 0 }])

    const roundTripped = parseCanvas(JSON.stringify(withWaypoints))
    expect(roundTripped.edges[0]?.waypoints).toEqual(withWaypoints.edges[0]?.waypoints)

    const withoutWaypoints = parseCanvas(JSON.stringify({
      version: 2,
      height: 480,
      nodes: [],
      edges: [],
    }))
    expect(withoutWaypoints.edges).toEqual([])
  })

  it('drops invalid ports and caps nodes and edges', () => {
    const nodes = Array.from({ length: 250 }, (_, index) => ({ id: `n${index}`, shape: 'rect', x: 0, y: 0, width: 160, height: 80, label: '', color: '#2f7d6d' }))
    const edges = Array.from({ length: 450 }, (_, index) => ({ id: `e${index}`, source: 'n0', target: 'n1', sourcePort: 'middle', label: '' }))
    const value = parseCanvas(JSON.stringify({ version: 2, height: 480, nodes, edges }))

    expect(value.nodes).toHaveLength(200)
    expect(value.edges).toHaveLength(400)
    expect(value.edges[0]).not.toHaveProperty('sourcePort')
  })

  it('rejects malformed canvas JSON so the Markdown parser can keep the code block', () => {
    expect(() => parseCanvas('not-json')).toThrow()
  })
})

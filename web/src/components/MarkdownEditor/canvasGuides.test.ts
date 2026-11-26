import { describe, expect, it } from 'vitest'
import { computeAlignmentGuides, computeDragSnap, type GuideRect } from './canvasGuides'

const rect = (id: string, x: number, y: number, width = 100, height = 60): GuideRect => ({
  id,
  x,
  y,
  width,
  height,
})

describe('computeAlignmentGuides', () => {
  it('snaps the left edge to a nearby left edge and reports the vertical line', () => {
    const result = computeAlignmentGuides([rect('m', 98, 300)], [rect('o', 100, 40, 120, 60)], 6)
    expect(result.offsetX).toBe(2)
    expect(result.snappedX).toBe(true)
    expect(result.guides.vertical).toEqual([100])
    expect(result.guides.horizontal).toEqual([])
  })

  it('snaps centers horizontally and reports the horizontal center line', () => {
    // moving 顶边 127 与 other 顶边 130 差 3px → 吸附 +3
    const result = computeAlignmentGuides([rect('m', 300, 127)], [rect('o', 40, 130, 100, 80)], 6)
    expect(result.offsetY).toBe(3)
    expect(result.snappedY).toBe(true)
    expect(result.guides.horizontal).toEqual([130])
  })

  it('does not snap beyond the threshold', () => {
    const result = computeAlignmentGuides([rect('m', 90, 300)], [rect('o', 100, 40)], 6)
    expect(result.snappedX).toBe(false)
    expect(result.offsetX).toBe(0)
    expect(result.guides.vertical).toEqual([])
  })

  it('uses the union bounds of a multi-selection for anchors', () => {
    const moving = [rect('a', 200, 100, 50, 50), rect('b', 300, 200, 50, 50)]
    // union: x 200..350, centerX 275; other right edge at 348 → snap right edge 350 → -2
    const result = computeAlignmentGuides(moving, [rect('o', 248, 400, 100, 50)], 6)
    expect(result.offsetX).toBe(-2)
    expect(result.guides.vertical).toEqual([348])
  })

  it('merges coincident candidates into a single line', () => {
    const result = computeAlignmentGuides(
      [rect('m', 99, 300)],
      [rect('o1', 100, 40, 120, 60), rect('o2', 100, 500, 80, 60)],
      6,
    )
    expect(result.guides.vertical).toEqual([100])
  })
})

describe('computeDragSnap', () => {
  it('prefers guide snapping over grid snapping', () => {
    // 距参考线 3px（命中），距网格 2px，应采用参考线
    const result = computeDragSnap([rect('m', 97, 300)], [rect('o', 100, 40)], 6, 20)
    expect(result.offsetX).toBe(3)
    expect(result.snappedX).toBe(true)
  })

  it('falls back to grid snapping when no guide matches', () => {
    const result = computeDragSnap([rect('m', 92, 45)], [rect('o', 500, 500)], 6, 20)
    expect(result.offsetX).toBe(8) // 92 → 100
    expect(result.offsetY).toBe(-5) // 45 → 40
    expect(result.guides.vertical).toEqual([])
  })

  it('mixes guide and grid snapping per axis', () => {
    // x 命中参考线，y 未命中 → x 用参考线、y 用网格
    const result = computeDragSnap([rect('m', 98, 43)], [rect('o', 100, 500, 120, 60)], 6, 20)
    expect(result.offsetX).toBe(2)
    expect(result.offsetY).toBe(-3) // 43 → 40
    expect(result.guides.vertical).toEqual([100])
    expect(result.guides.horizontal).toEqual([])
  })
})

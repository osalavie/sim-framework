import { describe, expect, it } from 'vitest'
import { Position } from '@vue-flow/core'
import {
  CANVAS_HANDLE_HALF,
  adjustCanvasEdgePoint,
  buildCatmullRomPath,
  buildOrthogonalRoute,
  pickNearestHandle,
} from './canvasEdgeGeometry'

describe('pickNearestHandle', () => {
  const candidates = [
    { nodeId: 'a', handleId: 'left', x: 100, y: 100 },
    { nodeId: 'b', handleId: 'right', x: 130, y: 100 },
    { nodeId: 'c', handleId: null, x: 500, y: 500 },
  ]

  it('picks the nearest handle within the radius', () => {
    expect(pickNearestHandle(candidates, 120, 100, 48)).toMatchObject({ nodeId: 'b' })
    expect(pickNearestHandle(candidates, 110, 100, 48)).toMatchObject({ nodeId: 'a' })
  })

  it('returns null when nothing is within the radius', () => {
    expect(pickNearestHandle(candidates, 300, 300, 48)).toBeNull()
    expect(pickNearestHandle([], 0, 0, 48)).toBeNull()
  })

  it('keeps the first collected candidate on a tie', () => {
    const tied = [
      { nodeId: 'first', handleId: 'left', x: 0, y: 10 },
      { nodeId: 'second', handleId: 'top', x: 0, y: -10 },
    ]
    expect(pickNearestHandle(tied, 0, 0, 48)).toMatchObject({ nodeId: 'first' })
  })
})

describe('adjustCanvasEdgePoint', () => {
  it('reclaims the endpoint from the handle box rim to its center per position', () => {
    expect(adjustCanvasEdgePoint(100, 200, Position.Bottom)).toEqual({ x: 100, y: 200 - CANVAS_HANDLE_HALF })
    expect(adjustCanvasEdgePoint(100, 200, Position.Top)).toEqual({ x: 100, y: 200 + CANVAS_HANDLE_HALF })
    expect(adjustCanvasEdgePoint(100, 200, Position.Left)).toEqual({ x: 100 + CANVAS_HANDLE_HALF, y: 200 })
    expect(adjustCanvasEdgePoint(100, 200, Position.Right)).toEqual({ x: 100 - CANVAS_HANDLE_HALF, y: 200 })
  })

  it('keeps the point unchanged without a known position', () => {
    expect(adjustCanvasEdgePoint(1, 2, undefined)).toEqual({ x: 1, y: 2 })
  })
})

describe('buildOrthogonalRoute', () => {
  it('inserts elbows for diagonal anchor pairs and reports segment midpoints', () => {
    const route = buildOrthogonalRoute([
      { x: 0, y: 0 },
      { x: 200, y: 100 },
    ])
    expect(route.path).toMatch(/^M0 0/)
    expect(route.path).toContain('200')
    expect(route.midpoints).toHaveLength(2)
    expect(route.midpoints[0]?.insertAfter).toBe(0)
    // 主方向为水平 → 先水平到 200 再垂直
    expect(route.midpoints[0]).toMatchObject({ x: 100, y: 0 })
  })

  it('treats sub-pixel axis differences as aligned (precision mismatch regression)', () => {
    // 折点坐标两位小数（224.17）与 handle 全精度（224.171875）混用时不产生重复拐角
    const route = buildOrthogonalRoute([
      { x: 224.171875, y: 234 },
      { x: 224.17, y: 288 },
      { x: 224.171875, y: 342 },
    ])
    expect(route.midpoints).toEqual([
      { x: 224.17, y: 261, insertAfter: 0 },
      { x: 224.17, y: 315, insertAfter: 1 },
    ])
  })

  it('routes through a waypoint without extra elbows when axis aligned', () => {
    const route = buildOrthogonalRoute([
      { x: 0, y: 0 },
      { x: 0, y: 100 },
      { x: 200, y: 100 },
    ])
    expect(route.midpoints).toEqual([
      { x: 0, y: 50, insertAfter: 0 },
      { x: 100, y: 100, insertAfter: 1 },
    ])
  })

  it('returns an empty path for insufficient anchors', () => {
    expect(buildOrthogonalRoute([{ x: 0, y: 0 }])).toEqual({ path: '', midpoints: [], points: [] })
  })

  it('keeps a constant corner radius regardless of segment length', () => {
    const shortRoute = buildOrthogonalRoute([{ x: 0, y: 0 }, { x: 30, y: 30 }])
    const longRoute = buildOrthogonalRoute([{ x: 0, y: 0 }, { x: 300, y: 300 }])
    // 两者拐角倒角量都固定为 8：入口 = 拐角 - 8
    expect(shortRoute.path).toContain('L22 0Q30 0 30 8')
    expect(longRoute.path).toContain('L292 0Q300 0 300 8')
  })

  it('exposes the routed spine including inserted elbows', () => {
    const route = buildOrthogonalRoute([{ x: 0, y: 0 }, { x: 200, y: 100 }])
    expect(route.points).toEqual([{ x: 0, y: 0 }, { x: 200, y: 0 }, { x: 200, y: 100 }])
  })
})

describe('buildCatmullRomPath', () => {
  it('passes through all points with cubic segments', () => {
    const path = buildCatmullRomPath([
      { x: 0, y: 0 },
      { x: 60, y: 80 },
      { x: 200, y: 40 },
    ])
    expect(path.startsWith('M0 0')).toBe(true)
    expect(path.match(/C/g)).toHaveLength(2)
    expect(path.endsWith('200 40')).toBe(true)
  })

  it('degrades to a straight line for two points', () => {
    expect(buildCatmullRomPath([{ x: 0, y: 0 }, { x: 10, y: 20 }])).toBe('M0 0L10 20')
  })
})

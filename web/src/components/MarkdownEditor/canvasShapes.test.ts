import { describe, expect, it } from 'vitest'
import zhCN from '@/i18n/messages/zh-CN.canvas'
import enUS from '@/i18n/messages/en-US.canvas'
import {
  CANVAS_SHAPE_GROUPS,
  allCanvasShapes,
  canvasShapesByGroup,
  getCanvasShape,
  isCanvasShape,
} from './canvasShapes'

describe('canvas shape registry', () => {
  it('assigns every shape to a declared group with complete metadata', () => {
    for (const shape of allCanvasShapes()) {
      expect([...CANVAS_SHAPE_GROUPS, 'internal']).toContain(shape.group)
      expect(shape.labelKey).toMatch(/^Editor:Canvas:Shape:/)
      expect(shape.defaultSize.width).toBeGreaterThanOrEqual(shape.minSize.width)
      expect(shape.defaultSize.height).toBeGreaterThanOrEqual(shape.minSize.height)
      expect(shape.defaultSize.width).toBeLessThanOrEqual(shape.maxSize.width)
      expect(shape.defaultSize.height).toBeLessThanOrEqual(shape.maxSize.height)
      expect(shape.paths(shape.defaultSize.width, shape.defaultSize.height)).toBeDefined()
    }
  })

  it('resolves label keys in both lazy pack locales', () => {
    for (const shape of allCanvasShapes()) {
      expect(zhCN[shape.labelKey as keyof typeof zhCN], shape.labelKey).toBeTruthy()
      expect(enUS[shape.labelKey as keyof typeof enUS], shape.labelKey).toBeTruthy()
    }
  })

  it('generates a main outline for filled shapes; borderless shapes are stroke-only', () => {
    for (const shape of allCanvasShapes()) {
      const outlines = shape.paths(shape.defaultSize.width, shape.defaultSize.height)
      if (shape.fill) {
        expect(outlines.length, shape.shape).toBeGreaterThan(0)
        expect(outlines[0]?.d, shape.shape).toMatch(/^M/)
      }
    }
    // 无边框文本形状不绘制任何轮廓
    expect(getCanvasShape('text').paths(160, 40)).toHaveLength(0)
  })

  it('gives the borderless text shape a rectangle-with-T panel icon', () => {
    const text = getCanvasShape('text')
    const icon = text.previewPaths?.(26, 12) ?? []

    // 矩形外框 + 横笔 + 竖笔
    expect(icon).toHaveLength(3)
    expect(icon[0]?.d).toMatch(/^M0\.8 0\.8H/)
    // 画布渲染不读取 previewPaths，仍是纯文本
    expect(text.paths(160, 40)).toHaveLength(0)
  })

  it('looks up shapes with fallback to rect for unknown keys', () => {
    expect(isCanvasShape('rect')).toBe(true)
    expect(isCanvasShape('unknown')).toBe(false)
    expect(getCanvasShape('unknown').shape).toBe('rect')
    expect(getCanvasShape('rect').shape).toBe('rect')
  })

  it('lists shapes by group without duplicates', () => {
    const seen = new Set<string>()
    for (const group of CANVAS_SHAPE_GROUPS) {
      for (const shape of canvasShapesByGroup(group)) {
        expect(seen.has(shape.shape)).toBe(false)
        seen.add(shape.shape)
      }
    }
    // internal 组（锚点）不出现在面板分组中
    expect(seen.size + canvasShapesByGroup('internal').length).toBe(allCanvasShapes().length)
  })
})

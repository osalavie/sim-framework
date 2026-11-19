import { shallowMount } from '@vue/test-utils'
import { Position } from '@vue-flow/core'
import type { Component } from 'vue'
import { describe, expect, it } from 'vitest'
import CanvasEdge from './CanvasEdge.vue'
import { CANVAS_HANDLE_HALF } from './canvasEdgeGeometry'

const baseProps = {
  id: 'e1',
  source: 'a',
  target: 'b',
  sourceX: 100,
  sourceY: 50,
  targetX: 100,
  targetY: 150,
  sourcePosition: Position.Bottom,
  targetPosition: Position.Top,
  data: { type: 'straight', line: 'solid', arrow: 'arrow', label: '' },
}

// 单测只覆盖自绘箭头几何，完整 EdgeProps 由 VueFlow 运行时提供
const mountEdge = (props: Record<string, unknown> = {}) =>
  shallowMount(CanvasEdge as Component, {
    props: { ...baseProps, ...props },
    global: {
      stubs: {
        BaseEdge: { name: 'BaseEdge', props: ['id', 'path', 'style'], template: '<g />' },
        EdgeLabelRenderer: { template: '<div><slot /></div>' },
      },
    },
  })

describe('CanvasEdge arrowhead', () => {
  it('renders a 30° apex arrow in absolute flow coordinates', () => {
    const wrapper = mountEdge()
    const polygon = wrapper.find('.edge-arrowhead')
    expect(polygon.exists()).toBe(true)

    // 顶角 30°：半宽 = 14 × tan(15°) ≈ 3.75，底角各 75°。
    // 锚点回收 handle 半盒后位于 (100, 150 + 7.5)，straight 方向竖直向下，尖端回退 1px。
    const anchorY = baseProps.targetY + CANVAS_HANDLE_HALF - 1
    expect(polygon.attributes('points')).toBe(
      `100,${anchorY} 96.25,${anchorY - 14} 103.75,${anchorY - 14}`,
    )
    // 不再使用 SVG transform：html-to-image 克隆 computed style 时会破坏该属性。
    expect(polygon.attributes('transform')).toBeUndefined()
  })

  it('keeps export-critical SVG paint styles inline instead of scoped CSS', () => {
    const wrapper = mountEdge()
    const edge = wrapper.findComponent({ name: 'BaseEdge' })
    expect(edge.props('style')).toMatchObject({ fill: 'none', stroke: 'var(--canvas-muted, #8a8f98)' })
    expect(wrapper.find('.edge-arrowhead').attributes('style')).toContain(
      'fill: var(--canvas-muted, #8a8f98)',
    )
  })

  it('omits the arrowhead when arrow is none', () => {
    const wrapper = mountEdge({ data: { type: 'straight', line: 'solid', arrow: 'none', label: '' } })
    expect(wrapper.find('.edge-arrowhead').exists()).toBe(false)
  })
})

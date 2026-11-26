import { flushPromises, shallowMount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { describe, expect, it, vi } from 'vitest'
import CanvasBoard from './CanvasBoard.vue'
import { EMPTY_CANVAS } from './canvasSyntax'

const stubs = {
  VueFlow: {
    name: 'VueFlow',
    props: [
      'zoomOnScroll',
      'zoomActivationKeyCode',
      'connectionMode',
      'panOnDrag',
      'panActivationKeyCode',
      'selectionKeyCode',
      'nodesDraggable',
      'nodesConnectable',
      'elementsSelectable',
    ],
    template: '<div class="vue-flow-stub"><slot /></div>',
  },
  Background: true,
  MiniMap: true,
  Controls: true,
}

describe('CanvasBoard', () => {
  it('keeps page scrolling on a plain wheel and activates zoom with Control', () => {
    const wrapper = shallowMount(CanvasBoard, {
      props: { modelValue: EMPTY_CANVAS },
      global: { stubs },
    })

    const flow = wrapper.findComponent({ name: 'VueFlow' })
    expect(flow.props('zoomOnScroll')).toBe(false)
    expect(flow.props('zoomActivationKeyCode')).toBe('Control')
    expect(flow.props('connectionMode')).toBe('loose')
  })

  it('pans with Shift + left-drag on desktop, by drag on touch, and with button panning in readonly', () => {
    const editWrapper = shallowMount(CanvasBoard, {
      props: { modelValue: EMPTY_CANVAS },
      global: { stubs },
    })
    const editFlow = editWrapper.findComponent({ name: 'VueFlow' })
    expect(editFlow.props('panOnDrag')).toBe(false)
    expect(editFlow.props('panActivationKeyCode')).toBe('Shift')
    expect(editFlow.props('selectionKeyCode')).toBeNull()

    const readonlyWrapper = shallowMount(CanvasBoard, {
      props: { modelValue: EMPTY_CANVAS, readonly: true },
      global: { stubs },
    })
    const readonlyFlow = readonlyWrapper.findComponent({ name: 'VueFlow' })
    expect(readonlyFlow.props('panOnDrag')).toBe(true)
    expect(readonlyFlow.props('panActivationKeyCode')).toBe('Space')
  })

  it('pans by dragging empty canvas and keeps shapes selectable on touch devices', async () => {
    vi.stubGlobal(
      'matchMedia',
      vi.fn(() => ({
        matches: true,
        media: '(pointer: coarse)',
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      })),
    )
    const wrapper = shallowMount(CanvasBoard, {
      props: { modelValue: EMPTY_CANVAS },
      global: { stubs },
    })
    await nextTick()

    const flow = wrapper.findComponent({ name: 'VueFlow' })
    expect(flow.props('panOnDrag')).toBe(true)
    expect(flow.props('nodesDraggable')).toBe(true)
    expect(flow.props('nodesConnectable')).toBe(true)
    expect(flow.props('elementsSelectable')).toBe(true)
    expect(wrapper.find('.canvas-touch-scroll-gate').exists()).toBe(false)
    expect(wrapper.find('.canvas-touch-tools').exists()).toBe(false)
    // 触屏只保留双指缩放，不渲染画布控件栏
    expect(wrapper.findComponent({ name: 'Controls' }).exists()).toBe(false)
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    vi.unstubAllGlobals()
  })

  it('hides the stencil panel and header in readonly mode', async () => {
    const wrapper = shallowMount(CanvasBoard, {
      props: { modelValue: EMPTY_CANVAS, readonly: true },
      global: {
        stubs: {
          VueFlow: { name: 'VueFlow', template: '<div class="vue-flow-stub"><slot /></div>' },
          Background: true,
          MiniMap: true,
          Controls: true,
        },
      },
    })

    expect(wrapper.find('.canvas-header').exists()).toBe(false)
    expect(wrapper.findComponent({ name: 'CanvasStencilPanel' }).exists()).toBe(false)
    expect(wrapper.findComponent({ name: 'CanvasEdgeToolbar' }).exists()).toBe(false)
  })

  it('opens and closes the shape library without changing the canvas document', async () => {
    const wrapper = shallowMount(CanvasBoard, {
      props: { modelValue: EMPTY_CANVAS },
      global: { stubs },
    })
    const toggle = wrapper.find('.canvas-library-toggle')

    expect(toggle.attributes('aria-expanded')).toBe('false')
    await toggle.trigger('click')
    expect(wrapper.find('.canvas-block').classes()).toContain('is-library-open')
    expect(toggle.attributes('aria-expanded')).toBe('true')
    await wrapper.find('.canvas-library-scrim').trigger('click')
    expect(wrapper.find('.canvas-block').classes()).not.toContain('is-library-open')
    await toggle.trigger('click')
    await toggle.trigger('keydown', { key: 'Escape' })
    expect(wrapper.find('.canvas-block').classes()).not.toContain('is-library-open')
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('toggles fullscreen with fixed positioning and exits on Escape', async () => {
    const wrapper = shallowMount(CanvasBoard, {
      props: { modelValue: EMPTY_CANVAS },
      global: { stubs },
    })
    await vi.dynamicImportSettled()
    await flushPromises()
    await nextTick()

    const block = wrapper.find('.canvas-block')
    const toggle = wrapper.find('.canvas-fullscreen-toggle')
    expect(toggle.exists()).toBe(true)

    await toggle.trigger('click')
    expect(block.classes()).toContain('is-fullscreen')
    expect(block.attributes('style')).toContain('position: fixed')

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await nextTick()
    expect(block.classes()).not.toContain('is-fullscreen')
    expect(block.attributes('style') ?? '').not.toContain('position: fixed')
  })
})

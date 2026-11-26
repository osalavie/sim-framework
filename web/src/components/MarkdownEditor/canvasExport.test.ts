import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  CANVAS_EXPORT_ROOT_SELECTOR,
  canvasContentBounds,
  canvasExportFileName,
  exportCanvasImage,
  measureCanvasContentBounds,
  planCanvasExport,
  withCanvasThemeProperties,
} from './canvasExport'

const htmlToImageMocks = vi.hoisted(() => ({
  toPng: vi.fn(),
  toJpeg: vi.fn(),
}))

vi.mock('html-to-image', () => htmlToImageMocks)

describe('canvasContentBounds', () => {
  it('returns null for an empty canvas', () => {
    expect(canvasContentBounds([])).toBeNull()
  })

  it('encloses all nodes', () => {
    const bounds = canvasContentBounds([
      { x: 100, y: 40, width: 160, height: 80 },
      { x: 20, y: 200, width: 100, height: 60 },
    ])
    expect(bounds).toEqual({ x: 20, y: 40, width: 240, height: 220 })
  })
})

describe('planCanvasExport', () => {
  it('pads the bounds and translates the viewport to the padded origin', () => {
    const plan = planCanvasExport({ x: 20, y: 40, width: 200, height: 100 }, 'rgb(10, 10, 10)')
    expect(plan.width).toBe(248)
    expect(plan.height).toBe(148)
    expect(plan.transform).toBe('translate(4px, -16px) scale(1)')
    expect(plan.backgroundColor).toBe('rgb(10, 10, 10)')
    expect(plan.pixelRatio).toBe(2)
  })

  it('downscales the pixel ratio beyond the 4096px side limit', () => {
    const plan = planCanvasExport({ x: 0, y: 0, width: 5000, height: 100 }, '#fff')
    expect(plan.pixelRatio).toBeCloseTo(4096 / 5048, 5)
  })
})

/** 画布无旋转，屏幕坐标 ≈ 流坐标 × zoom + 平移，这里按 zoom=2、平移=0 模拟。 */
const screenToFlowAtDoubleScale = (point: { x: number; y: number }) => ({
  x: point.x / 2,
  y: point.y / 2,
})

const domRect = (x: number, y: number, width: number, height: number): DOMRect =>
  ({
    x,
    y,
    left: x,
    top: y,
    right: x + width,
    bottom: y + height,
    width,
    height,
    toJSON: () => ({}),
  }) as DOMRect

const appendBox = (
  root: HTMLElement,
  className: string,
  box: [number, number, number, number],
): HTMLElement => {
  const element = document.createElement('div')
  element.className = className
  element.getBoundingClientRect = () => domRect(...box)
  root.appendChild(element)
  return element
}

describe('measureCanvasContentBounds', () => {
  it('unions nodes, edge paths, arrowheads and edge labels in flow coordinates', () => {
    const root = document.createElement('div')
    appendBox(root, 'vue-flow__node', [0, 0, 200, 100])
    appendBox(root, 'vue-flow__edge-path', [100, 150, 100, 50])
    appendBox(root, 'edge-arrowhead', [190, 150, 20, 20])
    appendBox(root, 'edge-label', [-40, -20, 60, 30])
    // 面板/网格等不在捕获子树内，即使越界也不参与包围盒
    appendBox(root, 'vue-flow__background', [-900, -900, 4000, 4000])

    expect(measureCanvasContentBounds(root, screenToFlowAtDoubleScale)).toEqual({
      x: -20,
      y: -10,
      width: 125,
      height: 110,
    })
  })

  it('skips unmeasured elements and returns null when nothing is measurable', () => {
    const root = document.createElement('div')
    appendBox(root, 'vue-flow__node', [0, 0, 0, 0])
    appendBox(root, 'vue-flow__node', [10, 10, 100, 0])
    appendBox(root, 'vue-flow__node', [10, 10, 0, 100])

    expect(measureCanvasContentBounds(root, screenToFlowAtDoubleScale)).toEqual({
      x: 5,
      y: 5,
      width: 50,
      height: 50,
    })

    expect(measureCanvasContentBounds(document.createElement('div'), screenToFlowAtDoubleScale)).toBeNull()
  })

  it('captures the vue-flow transform pane instead of the clipping viewport', () => {
    expect(CANVAS_EXPORT_ROOT_SELECTOR).toBe('.vue-flow__transformationpane')
  })
})

describe('exportCanvasImage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    htmlToImageMocks.toPng.mockResolvedValue('data:image/png;base64,x')
    htmlToImageMocks.toJpeg.mockResolvedValue('data:image/jpeg;base64,x')
  })

  it('renders jpeg with quality and background and downloads the file', async () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const el = document.createElement('div')
    const plan = planCanvasExport({ x: 0, y: 0, width: 100, height: 60 }, 'rgb(1, 2, 3)')

    await exportCanvasImage(el, plan, 'jpeg', 'canvas-test.jpg')

    expect(htmlToImageMocks.toJpeg).toHaveBeenCalledWith(el, expect.objectContaining({
      width: 148,
      height: 108,
      quality: 0.92,
      backgroundColor: 'rgb(1, 2, 3)',
      style: expect.objectContaining({ transformOrigin: '0 0' }),
    }))
    expect(click).toHaveBeenCalled()
    click.mockRestore()
  })

  it('renders png without quality', async () => {
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const el = document.createElement('div')
    const plan = planCanvasExport({ x: 0, y: 0, width: 100, height: 60 }, '#fff')

    await exportCanvasImage(el, plan, 'png', 'canvas-test.png')

    const options = htmlToImageMocks.toPng.mock.calls[0]?.[1] as Record<string, unknown>
    expect(options).not.toHaveProperty('quality')
  })
})

describe('withCanvasThemeProperties', () => {
  it('inlines the theme tokens during capture and restores the previous values', async () => {
    const root = document.createElement('div')
    root.style.setProperty('--canvas-surface', '#111111')

    const valueDuringCapture = await withCanvasThemeProperties(
      root,
      { '--canvas-surface': '#222222', '--canvas-text': '#eeeeee' },
      async () => ({
        surface: root.style.getPropertyValue('--canvas-surface'),
        text: root.style.getPropertyValue('--canvas-text'),
      }),
    )

    expect(valueDuringCapture).toEqual({ surface: '#222222', text: '#eeeeee' })
    expect(root.style.getPropertyValue('--canvas-surface')).toBe('#111111')
    expect(root.style.getPropertyValue('--canvas-text')).toBe('')
  })
})

describe('canvasExportFileName', () => {
  it('joins article title, heading path and canvas index per format', () => {
    expect(canvasExportFileName('png', ['模块化架构实践', '分层职责', '2'])).toBe(
      '模块化架构实践-分层职责-2.png',
    )
    expect(canvasExportFileName('jpeg', ['架构/实践', '边界: 条件?', '12'])).toBe(
      '架构实践-边界-条件-12.jpg',
    )
  })

  it('omits blank segments and falls back to the canvas stem', () => {
    expect(canvasExportFileName('png', ['', '分层职责', ''])).toBe('分层职责.png')
    expect(canvasExportFileName('png', [])).toBe('canvas.png')
  })
})

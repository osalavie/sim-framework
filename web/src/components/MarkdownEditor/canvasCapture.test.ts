import { describe, expect, it } from 'vitest'
import type { Editor } from '@tiptap/core'
import { canvasIndexAt, createCanvasCaptureRegistry, sortByDocumentOrder } from './canvasCapture'

type ProseMirrorDoc = Editor['state']['doc']

/** 用扁平节点表模拟 ProseMirror 的 descendants（只关心节点类型与位置）。 */
function fakeDoc(nodes: Array<{ name: string; pos: number }>): ProseMirrorDoc {
  return {
    descendants(visit: (node: { type: { name: string } }, pos: number) => boolean | void) {
      for (const node of nodes) {
        visit({ type: { name: node.name } }, node.pos)
      }
    },
  } as unknown as ProseMirrorDoc
}

describe('canvasIndexAt', () => {
  const doc = fakeDoc([
    { name: 'paragraph', pos: 0 },
    { name: 'canvas', pos: 10 },
    { name: 'paragraph', pos: 12 },
    { name: 'canvas', pos: 30 },
  ])

  it('统计当前位置之前的画布数量', () => {
    expect(canvasIndexAt(doc, 0)).toBe(0)
    expect(canvasIndexAt(doc, 10)).toBe(0)
    expect(canvasIndexAt(doc, 11)).toBe(1)
    expect(canvasIndexAt(doc, 30)).toBe(1)
    expect(canvasIndexAt(doc, 31)).toBe(2)
  })

  it('没有位置时返回 -1', () => {
    expect(canvasIndexAt(doc, undefined)).toBe(-1)
  })
})

describe('sortByDocumentOrder', () => {
  it('按 DOM 顺序排列并保留其它字段', () => {
    const root = document.createElement('div')
    const second = document.createElement('div')
    const first = document.createElement('div')
    root.append(first, second)

    const entries = [
      { element: second, capture: 'b' },
      { element: first, capture: 'a' },
    ]

    expect(sortByDocumentOrder(entries).map((entry) => entry.capture)).toEqual(['a', 'b'])
  })
})

describe('createCanvasCaptureRegistry', () => {
  it('注册后可注销，重复注册不回写', () => {
    const registry = createCanvasCaptureRegistry()
    const first = { element: document.createElement('div'), capture: async () => null }
    const second = { element: document.createElement('div'), capture: async () => null }

    const unregisterFirst = registry.register(first)
    registry.register(second)
    expect(registry.entries()).toHaveLength(2)

    unregisterFirst()
    expect(registry.entries()).toEqual([second])
  })
})

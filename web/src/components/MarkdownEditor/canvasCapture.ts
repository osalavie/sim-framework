import type { InjectionKey, Ref } from 'vue'
import type { Editor } from '@tiptap/core'

type ProseMirrorDoc = Editor['state']['doc']

/** 单个画布的导出捕获入口：元素用于按文档顺序配对，capture 返回 PNG dataURL。 */
export interface CanvasCaptureEntry {
  element: HTMLElement
  capture: () => Promise<string | null>
}

export interface CanvasCaptureRegistry {
  register: (entry: CanvasCaptureEntry) => () => void
  entries: () => CanvasCaptureEntry[]
}

/** 导出渲染面向下提供的捕获注册表；普通编辑/阅读页不提供，画布因此零改动。 */
export const CANVAS_CAPTURE_KEY: InjectionKey<CanvasCaptureRegistry> = Symbol('canvas-capture')

/** 导出渲染面提供的画布快照（按文档顺序），存在时画布节点直接渲染图片。 */
export const ARTICLE_EXPORT_SNAPSHOTS_KEY: InjectionKey<Ref<Array<string | null>>> = Symbol(
  'article-export-snapshots',
)

export function createCanvasCaptureRegistry(): CanvasCaptureRegistry {
  const registered: CanvasCaptureEntry[] = []
  return {
    register(entry) {
      registered.push(entry)
      return () => {
        const index = registered.indexOf(entry)
        if (index >= 0) registered.splice(index, 1)
      }
    },
    entries: () => [...registered],
  }
}

/** 按 DOM 顺序排序，保证捕获结果与 Markdown 中 ```canvas 围栏顺序一致。 */
export function sortByDocumentOrder<T extends { element: Element }>(entries: T[]): T[] {
  return [...entries].sort((left, right) => {
    const relation = left.element.compareDocumentPosition(right.element)
    if (relation & Node.DOCUMENT_POSITION_FOLLOWING) return -1
    if (relation & Node.DOCUMENT_POSITION_PRECEDING) return 1
    return 0
  })
}

/** 画布节点在文档中的序号（从 0 开始），用于与 markdown 围栏、快照数组对齐。 */
export function canvasIndexAt(doc: ProseMirrorDoc, position: number | undefined): number {
  if (typeof position !== 'number') return -1
  let index = 0
  doc.descendants((node, offset) => {
    if (offset >= position) return false
    if (node.type.name === 'canvas') {
      index += 1
      return false
    }
    return true
  })
  return index
}

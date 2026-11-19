import { mergeAttributes, Node, type MarkdownToken } from '@tiptap/core'
import { VueNodeViewRenderer } from '@tiptap/vue-3'
import CanvasNodeView from './CanvasNodeView.vue'
import {
  cloneCanvas,
  EMPTY_CANVAS,
  parseCanvas,
  stringifyCanvas,
  type CanvasDocument,
} from './canvasSyntax'

const CANVAS_FENCE =
  /^```canvas[^\S\r\n]*\r?\n([\s\S]*?)\r?\n```(?:\r?\n|$)/

const normalizeDocument = (value: unknown): CanvasDocument => {
  if (typeof value === 'string') {
    try {
      return parseCanvas(value)
    } catch {
      return cloneCanvas(EMPTY_CANVAS)
    }
  }

  try {
    return parseCanvas(JSON.stringify(value))
  } catch {
    return cloneCanvas(EMPTY_CANVAS)
  }
}

export interface CanvasExtensionOptions {
  /** 画布导出文件名使用的文章标题。 */
  resolveDocumentTitle?: () => string
}

export const CanvasExtension = Node.create<CanvasExtensionOptions>({
  name: 'canvas',
  group: 'block',
  atom: true,
  selectable: true,
  isolating: true,

  addOptions() {
    return { resolveDocumentTitle: undefined }
  },

  addAttributes() {
    return {
      document: {
        default: cloneCanvas(EMPTY_CANVAS),
      },
    }
  },

  parseHTML() {
    return [
      {
        tag: 'section[data-canvas-document]',
        getAttrs: (element) => {
          if (!(element instanceof HTMLElement)) return false
          const encoded = element.dataset.canvasDocument
          if (!encoded) return { document: cloneCanvas(EMPTY_CANVAS) }
          try {
            return { document: normalizeDocument(decodeURIComponent(encoded)) }
          } catch {
            return { document: cloneCanvas(EMPTY_CANVAS) }
          }
        },
      },
    ]
  },

  renderHTML({ HTMLAttributes, node }) {
    const document = normalizeDocument(node.attrs.document)
    const safeAttributes = Object.fromEntries(
      Object.entries(HTMLAttributes).filter(([name]) => name !== 'document'),
    )
    return [
      'section',
      mergeAttributes(safeAttributes, {
        'data-canvas-document': encodeURIComponent(JSON.stringify(document)),
      }),
    ]
  },

  markdownTokenName: 'canvas',

  markdownTokenizer: {
    name: 'canvas',
    level: 'block',
    start(source: string) {
      const match = /(^|\n)```canvas(?:[^\S\r\n]|$)/.exec(source)
      return match ? match.index + (match[1] ? 1 : 0) : -1
    },
    tokenize(source: string): MarkdownToken | undefined {
      const match = CANVAS_FENCE.exec(source)
      if (!match?.[1]) return undefined

      try {
        return {
          type: 'canvas',
          raw: match[0],
          document: parseCanvas(match[1]),
        }
      } catch {
        return undefined
      }
    },
  },

  parseMarkdown(token, helpers) {
    return helpers.createNode('canvas', {
      document: normalizeDocument(token.document),
    })
  },

  renderMarkdown(node) {
    return stringifyCanvas(normalizeDocument(node.attrs?.document))
  },

  addNodeView() {
    return VueNodeViewRenderer(CanvasNodeView)
  },
})

/**
 * Markdown 渲染和安全处理工具
 */

import { marked, type Token, type Tokens } from 'marked'
import DOMPurify from 'dompurify'
import hljs from 'highlight.js/lib/common'
import { CANVAS_PATTERN, parseCanvas } from '@/components/MarkdownEditor/canvasSyntax'

export interface TocItem {
  id: string
  text: string
  level: number
}

/**
 * canvas 围栏块渲染为占位元素（与 tiptap renderHTML 输出一致）。
 * 生产渲染链路由只读 MarkdownEditor 接管并挂载 Vue Flow 画布；
 * 这里不再静态绘制图形，避免双渲染器。
 */
const renderCanvasPlaceholder = (source: string) => {
  const document = parseCanvas(source)
  return `<section data-canvas-document="${encodeURIComponent(JSON.stringify(document))}"></section>`
}

// 配置 marked 的代码高亮
const renderer = new marked.Renderer()
renderer.code = function({ text, lang }: { text: string; lang?: string }) {
  const validLanguage = lang && hljs.getLanguage(lang) ? lang : 'plaintext'
  const highlighted = hljs.highlight(text, { language: validLanguage }).value

  return `<div class="code-block-wrapper">
    <div class="code-block-header">
      <span class="code-window-controls" aria-hidden="true">
        <i class="is-close"></i><i class="is-minimize"></i><i class="is-expand"></i>
      </span>
      <span class="code-language">${validLanguage}</span>
      <button class="code-copy-btn" data-code="${encodeURIComponent(text)}">
        <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
          <path d="M5.5 2.5h7v9h-7v-9z" stroke="currentColor" stroke-width="1.5" fill="none"/>
          <path d="M3.5 4.5v9h7" stroke="currentColor" stroke-width="1.5" fill="none"/>
        </svg>
        <span>复制</span>
      </button>
    </div>
    <pre><code class="hljs language-${validLanguage}">${highlighted}</code></pre>
  </div>`
}

// 配置 marked
marked.setOptions({
  gfm: true, // 启用 GitHub Flavored Markdown
  breaks: true, // 支持换行符转换为 <br>
  renderer: renderer
})

/**
 * 渲染 Markdown 为 HTML（带安全处理）
 */
export function renderMarkdown(markdown: string): string {
  markdown = markdown.replace(CANVAS_PATTERN, (_match, source: string) => {
    try {
      return renderCanvasPlaceholder(source)
    } catch { return _match }
  })
  // 使用 marked 渲染 Markdown
  const html = marked.parse(markdown) as string

  // 使用 DOMPurify 进行 XSS 防护
  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
      'p', 'br', 'hr',
      'strong', 'em', 'u', 's', 'del',
      'a', 'img',
      'ul', 'ol', 'li',
      'blockquote',
      'code', 'pre',
      'table', 'thead', 'tbody', 'tr', 'th', 'td',
      'div', 'span', 'button', 'section', 'svg', 'path', 'i'
    ],
    ALLOWED_ATTR: [
      'href', 'title', 'target', 'rel',
      'src', 'alt', 'width', 'height',
      'id', 'class',
      'align', 'colspan', 'rowspan',
      'data-code', 'viewBox', 'fill', 'stroke', 'stroke-width', 'd', 'style', 'aria-hidden'
    ],
    ALLOW_DATA_ATTR: true,
    ALLOW_UNKNOWN_PROTOCOLS: false
  })
}

/**
 * 从 Markdown 内容中提取目录
 */
export function extractToc(markdown: string): TocItem[] {
  const tocItems: TocItem[] = []

  // marked parses code fences as code tokens, so comments such as "# install"
  // inside a code block cannot leak into the table of contents.
  const collectHeadings = (tokens: Token[]) => {
    tokens.forEach((token) => {
      if (token.type === 'heading') {
        const heading = token as Tokens.Heading
        const text = heading.text.trim()
        if (heading.depth >= 1 && heading.depth <= 4 && text) {
          tocItems.push({
            id: `heading-${tocItems.length}`,
            text,
            level: heading.depth,
          })
        }
      }

      const nestedTokens = (token as Tokens.Generic).tokens
      if (nestedTokens?.length) {
        collectHeadings(nestedTokens)
      }
    })
  }

  collectHeadings(marked.lexer(markdown))
  return tocItems
}

/**
 * 为 HTML 中的标题添加 ID
 */
export function addHeadingIds(html: string, tocItems: TocItem[]): string {
  let result = html
  let tocIndex = 0

  // 为每个标题标签添加 id
  result = result.replace(/<h([1-4])>(.*?)<\/h\1>/gi, (match, level: string, content: string) => {
    const currentToc = tocItems[tocIndex]
    if (tocIndex < tocItems.length && currentToc) {
      const id = currentToc.id
      tocIndex++
      return `<h${level} id="${id}">${content}</h${level}>`
    }
    return match
  })

  return result
}



/**
 * 完整的 Markdown 渲染流程
 */
export function processMarkdown(markdown: string): {
  html: string
  toc: TocItem[]
} {
  // 1. 提取目录
  const toc = extractToc(markdown)

  // 2. 渲染 Markdown
  let html = renderMarkdown(markdown)

  // 3. 为标题添加 ID
  html = addHeadingIds(html, toc)

  return { html, toc }
}

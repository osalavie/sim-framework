import { sanitizeFileNameSegment } from '@/components/MarkdownEditor/imageUploadNaming'
import { formatDate } from '@/utils/date'

const CONTENT_FILE_PREFIX = '/api/system/content-file/'
const IMAGE_SYNTAX = /!\[([^\]]*)\]\(\s*([^)\s]+)(\s+(?:"[^"]*"|'[^']*'))?\s*\)/g
const FENCE_OPEN = /^```(\S*)\s*$/
const FENCE_CLOSE = /^```\s*$/
const HEADING = /^(#{1,6})\s+(.*)$/

export interface PreparedImage {
  fileName: string
  /** Blob 的 MIME，用于在缺少扩展名时推断 */
  type: string
  bytes: Uint8Array
}

export interface ExportFile {
  path: string
  data: Uint8Array
}

export interface ExportMetaLabels {
  author: string
  published: string
  updated: string
  tags: string
  cover: string
  copyright: string
  /** 日期格式（含 YYYY/MM/DD/HH/mm 令牌），中文用「2026年10月28日 14:56」 */
  dateFormat: string
}

/** 文章元信息：写进 frontmatter，并在正文标题下生成带样式的日期与标签块 */
export interface ExportMeta {
  creationTime?: string | null
  lastModificationTime?: string | null
  tags?: string[] | null
  cover?: string | null
  authorName?: string | null
  authorNo?: string | null
  authorId?: string | null
  copyright?: string | null
  labels: ExportMetaLabels
}

export interface RewriteOptions {
  title: string
  markdown: string
  /** 与正文中 ```canvas 围栏顺序一致的截图；null 表示空画布，该围栏整体移除 */
  snapshots: Array<string | null>
  /** 站内内容文件 URL → 已读取的图片；缺失的地址保持原链接 */
  images: Map<string, PreparedImage>
  meta?: ExportMeta
}

export interface RewriteResult {
  markdown: string
  files: ExportFile[]
  /** 未打包、保持外链的图片地址 */
  externalImages: string[]
}

function forEachOutsideFence(markdown: string, visit: (line: string) => void): void {
  let inFence = false
  for (const line of markdown.split(/\r?\n/)) {
    if (inFence) {
      if (FENCE_CLOSE.test(line)) inFence = false
      continue
    }
    if (FENCE_OPEN.test(line)) {
      inFence = true
      continue
    }
    visit(line)
  }
}

/** 正文中引用站内内容文件的图片地址（按出现顺序去重），用于导出前预热 blob。 */
export function collectContentImageUrls(markdown: string, cover?: string | null): string[] {
  const urls = new Set<string>()
  const coverUrl = cover?.trim()
  if (coverUrl?.startsWith(CONTENT_FILE_PREFIX)) urls.add(coverUrl)
  forEachOutsideFence(markdown, (line) => {
    IMAGE_SYNTAX.lastIndex = 0
    let match: RegExpExecArray | null
    while ((match = IMAGE_SYNTAX.exec(line))) {
      const url = match[2] ?? ''
      if (url.startsWith(CONTENT_FILE_PREFIX)) urls.add(url)
    }
  })
  return [...urls]
}

function extensionFromType(type: string): string {
  const map: Record<string, string> = {
    'image/png': 'png',
    'image/jpeg': 'jpg',
    'image/gif': 'gif',
    'image/webp': 'webp',
    'image/avif': 'avif',
    'image/svg+xml': 'svg',
  }
  return map[type.toLowerCase()] ?? ''
}

function extensionFromUrl(url: string): string {
  const match = /\.([a-z0-9]{2,5})(?:[?#]|$)/i.exec(url)
  return match?.[1]?.toLowerCase() ?? ''
}

function splitExtension(fileName: string): { stem: string; extension: string } {
  const match = /^(.*)\.([a-z0-9]{2,5})$/i.exec(fileName)
  if (!match) return { stem: fileName, extension: '' }
  return { stem: match[1] ?? fileName, extension: (match[2] ?? '').toLowerCase() }
}

function uniqueFileName(stem: string, extension: string, used: Set<string>): string {
  const base = stem || 'image'
  const suffix = extension ? `.${extension}` : ''
  let candidate = `${base}${suffix}`
  let counter = 2
  while (used.has(candidate)) {
    candidate = `${base}-${counter}${suffix}`
    counter += 1
  }
  used.add(candidate)
  return candidate
}

/** PNG dataURL → 字节；非 base64 的 dataURL 走文本解码。 */
export function dataUrlToBytes(dataUrl: string): Uint8Array {
  const comma = dataUrl.indexOf(',')
  const header = comma >= 0 ? dataUrl.slice(0, comma) : ''
  const payload = comma >= 0 ? dataUrl.slice(comma + 1) : dataUrl
  if (!header.includes(';base64')) return new TextEncoder().encode(decodeURIComponent(payload))
  const binary = atob(payload)
  const bytes = new Uint8Array(binary.length)
  for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index)
  return bytes
}

interface HeadingEntry {
  level: number
  text: string
}

function pushHeading(stack: HeadingEntry[], line: string): void {
  const match = HEADING.exec(line)
  if (!match) return
  const level = (match[1] ?? '').length
  const text = (match[2] ?? '').replace(/\s+/g, ' ').trim()
  while (stack.length > 0 && (stack[stack.length - 1]?.level ?? 0) >= level) stack.pop()
  if (text) stack.push({ level, text })
}

function canvasFileName(stack: HeadingEntry[], sequence: number, used: Set<string>): string {
  const path = stack.map((entry) => sanitizeFileNameSegment(entry.text)).filter(Boolean)
  const stem = path.length > 0 ? `${path.join('-')}-canvas` : 'canvas'
  return uniqueFileName(`${stem}-${sequence}`, 'png', used)
}

/** 正文通常不含 H1（标题单独存储），导出时补一行，保证 .md 打开有标题。 */
function ensureTitle(markdown: string, title: string): string {
  const trimmed = markdown.trimStart()
  if (!title.trim() || /^#\s/.test(trimmed)) return markdown
  return `# ${title.trim()}\n\n${trimmed}`
}

function formatMetaTime(value: string | null | undefined, format: string): string {
  return value ? formatDate(value, format) : ''
}

/** 没有更新日期时，发布日期就是最新更新日期。 */
function effectiveUpdatedTime(meta: ExportMeta): string | null | undefined {
  return meta.lastModificationTime || meta.creationTime
}

function cleanList(values?: string[] | null): string[] {
  return (values ?? []).map((value) => value.trim()).filter(Boolean)
}

/** 行内代码里不能出现反引号，做最小替换，避免破坏标签格式。 */
function inlineCode(value: string): string {
  return `\`${value.replace(/`/g, "'")}\``
}

/** YAML 双引号字符串；JSON 转义与 YAML 双引号兼容。 */
function yamlString(value: string): string {
  return JSON.stringify(value)
}

/**
 * 文件元信息：YAML frontmatter 保存机器可读的作者、日期、标签与版权，
 * 正文标题下仍用引用块保留人可读版本。
 */
function buildFrontmatter(title: string, meta: ExportMeta): string[] {
  const lines = ['---', `title: ${yamlString(title)}`]
  const authorName = meta.authorName?.trim()
  if (authorName) lines.push(`author: ${yamlString(authorName)}`)
  if (meta.authorId?.trim()) lines.push(`authorId: ${yamlString(meta.authorId.trim())}`)
  if (meta.authorNo?.trim()) lines.push(`authorNo: ${yamlString(meta.authorNo.trim())}`)
  if (meta.creationTime) lines.push(`publishedAt: ${yamlString(meta.creationTime)}`)
  const updated = effectiveUpdatedTime(meta)
  if (updated) lines.push(`updatedAt: ${yamlString(updated)}`)
  const tags = cleanList(meta.tags)
  if (tags.length > 0) {
    lines.push('tags:')
    for (const tag of tags) lines.push(`  - ${yamlString(tag)}`)
  }
  if (meta.copyright?.trim()) lines.push(`copyright: ${yamlString(meta.copyright.trim())}`)
  lines.push('---')
  return lines
}

/**
 * 标题下的元信息块：纯 Markdown 引用块 + 行内代码标签。
 * 不依赖内联 HTML，任何查看器（VS Code 预览、GitHub、Obsidian 等）都能正常渲染。
 */
function buildMetaBlockquote(meta: ExportMeta): string[] {
  const lines: string[] = []
  const authorName = meta.authorName?.trim()
  if (authorName) {
    const authorNo = meta.authorNo?.trim()
    lines.push(`> **${meta.labels.author}**：${authorName}${authorNo ? ` (${authorNo})` : ''}`)
  }
  const published = formatMetaTime(meta.creationTime, meta.labels.dateFormat)
  if (published) lines.push(`> **${meta.labels.published}**：${published}`)
  const updated = formatMetaTime(effectiveUpdatedTime(meta), meta.labels.dateFormat)
  if (updated) lines.push(`> **${meta.labels.updated}**：${updated}`)
  const tags = cleanList(meta.tags).map(inlineCode)
  if (tags.length > 0) lines.push(`> **${meta.labels.tags}**：${tags.join(' ')}`)
  if (meta.copyright?.trim()) {
    lines.push(`> **${meta.labels.copyright}**：${meta.copyright.trim()}`)
  }
  return lines
}

/** 元信息块插到 H1 之后（ensureTitle 之后正文一定有 H1）。 */
function insertMetaBlockquote(markdown: string, meta: ExportMeta): string {
  const block = buildMetaBlockquote(meta)
  if (block.length === 0) return markdown
  const lines = markdown.split('\n')
  const insertAt = lines[0]?.startsWith('# ') ? 1 : 0
  lines.splice(insertAt, 0, '', ...block)
  return lines.join('\n')
}

/**
 * 生成自包含的 Markdown：canvas 围栏替换为图片、站内图片改写到 `images/`，
 * 其余内容逐行原样保留（代码围栏内的图片语法不动）。
 */
export function rewriteMarkdownForExport(options: RewriteOptions): RewriteResult {
  const lines = options.markdown.split(/\r?\n/)
  const stack: HeadingEntry[] = []
  const used = new Set<string>()
  const files: ExportFile[] = []
  const externalImages = new Set<string>()
  const imageNames = new Map<string, string>()
  const output: string[] = []
  let inFence = false
  let canvasSequence = 0

  // 封面先占用文件名，避免与正文图片重名；站内封面随 zip 一起打包
  let coverPath = ''
  const coverUrl = options.meta?.cover?.trim() ?? ''
  if (coverUrl) {
    if (coverUrl.startsWith(CONTENT_FILE_PREFIX)) {
      const prepared = options.images.get(coverUrl)
      if (prepared) {
        const extension = extensionFromType(prepared.type) || extensionFromUrl(coverUrl) || 'png'
        const fileName = uniqueFileName('cover', extension, used)
        imageNames.set(coverUrl, fileName)
        files.push({ path: `images/${fileName}`, data: prepared.bytes })
        coverPath = `images/${fileName}`
      } else {
        externalImages.add(coverUrl)
        coverPath = coverUrl
      }
    } else {
      if (/^https?:\/\//i.test(coverUrl)) externalImages.add(coverUrl)
      coverPath = coverUrl
    }
  }

  const rewriteImages = (line: string): string =>
    line.replace(IMAGE_SYNTAX, (whole, alt: string, url: string, titlePart = '') => {
      if (!url.startsWith(CONTENT_FILE_PREFIX)) {
        if (/^https?:\/\//i.test(url)) externalImages.add(url)
        return whole
      }

      const existing = imageNames.get(url)
      if (existing) return `![${alt}](images/${existing}${titlePart})`

      const prepared = options.images.get(url)
      if (!prepared) {
        externalImages.add(url)
        return whole
      }

      const altParts = splitExtension(sanitizeFileNameSegment(alt ?? ''))
      const extension =
        altParts.extension || extensionFromType(prepared.type) || extensionFromUrl(url) || 'png'
      const fallbackStem = splitExtension(sanitizeFileNameSegment(prepared.fileName)).stem
      const fileName = uniqueFileName(
        altParts.stem || fallbackStem || `image-${used.size + 1}`,
        extension,
        used,
      )
      imageNames.set(url, fileName)
      files.push({ path: `images/${fileName}`, data: prepared.bytes })
      return `![${alt || fileName}](images/${fileName}${titlePart})`
    })

  let index = 0
  while (index < lines.length) {
    const line = lines[index] ?? ''

    if (inFence) {
      if (FENCE_CLOSE.test(line)) inFence = false
      output.push(line)
      index += 1
      continue
    }

    const fenceOpen = FENCE_OPEN.exec(line)
    if (fenceOpen) {
      if (fenceOpen[1] === 'canvas') {
        let closing = index + 1
        while (closing < lines.length && !FENCE_CLOSE.test(lines[closing] ?? '')) closing += 1
        const snapshot = options.snapshots[canvasSequence] ?? null
        canvasSequence += 1
        if (snapshot) {
          const fileName = canvasFileName(stack, canvasSequence, used)
          const label = stack.map((entry) => entry.text).join(' > ') || options.title
          files.push({ path: `images/${fileName}`, data: dataUrlToBytes(snapshot) })
          output.push(`![${label}](images/${fileName})`)
        }
        index = closing + 1
        continue
      }
      inFence = true
      output.push(line)
      index += 1
      continue
    }

    if (HEADING.test(line)) {
      pushHeading(stack, line)
      output.push(line)
      index += 1
      continue
    }

    output.push(rewriteImages(line))
    index += 1
  }

  let body = ensureTitle(output.join('\n'), options.title)
  const sections: string[] = []
  if (options.meta) {
    sections.push(buildFrontmatter(options.title, options.meta).join('\n'))
    body = insertMetaBlockquote(body, options.meta)
    if (coverPath) sections.push(`![${options.meta.labels.cover}](${coverPath})`)
  }
  sections.push(body)

  return {
    markdown: sections.join('\n\n'),
    files,
    externalImages: [...externalImages],
  }
}

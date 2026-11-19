import type { Editor } from '@tiptap/core'

type ProseMirrorNode = Editor['state']['doc']
type ProseMirrorDoc = ProseMirrorNode

interface HeadingEntry {
  level: number
  text: string
}

const INVALID_FILE_NAME_CHARS = /[<>:"/\\|?*\u0000-\u001f\u007f]/g

function normalizeHeadingText(text: string): string {
  return text.replace(/\s+/g, ' ').trim()
}

export function sanitizeFileNameSegment(text: string): string {
  return normalizeHeadingText(text)
    .replace(INVALID_FILE_NAME_CHARS, '')
    .replace(/\s+/g, '-')
    .replace(/-{2,}/g, '-')
    .replace(/^[-.]+|[-.]+$/g, '')
}

function updateHeadingStack(stack: HeadingEntry[], node: ProseMirrorNode): void {
  if (node.type.name !== 'heading') return
  const level = Number(node.attrs.level)
  if (!Number.isFinite(level) || level <= 0) return

  while (stack.length > 0) {
    const current = stack[stack.length - 1]
    if (!current || current.level < level) break
    stack.pop()
  }
  const text = normalizeHeadingText(node.textContent)
  if (text) stack.push({ level, text })
}

function sameHeadingPath(stack: HeadingEntry[], headingPath: string[]): boolean {
  if (stack.length !== headingPath.length) return false
  return stack.every((entry, index) => entry.text === headingPath[index])
}

/** 插入点之前最近的标题栈（H1 > H2 > H3...），用于生成图片文件名。 */
export function headingPathAt(doc: ProseMirrorDoc, position: number): string[] {
  const stack: HeadingEntry[] = []
  doc.descendants((node, offset) => {
    if (offset >= position) return false
    if (node.type.name === 'heading') {
      updateHeadingStack(stack, node)
      return false
    }
    return true
  })
  return stack.map((entry) => entry.text)
}

/** 文档中属于同一标题章节的图片数量，编号在章节内递增。 */
export function countSectionImages(doc: ProseMirrorDoc, headingPath: string[]): number {
  const stack: HeadingEntry[] = []
  let count = 0

  doc.descendants((node) => {
    if (node.type.name === 'heading') {
      updateHeadingStack(stack, node)
      return false
    }
    if (node.type.name === 'image') {
      if (sameHeadingPath(stack, headingPath)) count += 1
      return false
    }
    return true
  })

  return count
}

function resolveExtension(originalName: string, contentType: string): string {
  const originalExtension = /\.[^./\\]+$/.exec(originalName.trim())?.[0]
  if (originalExtension) return originalExtension

  const subtype = contentType.split('/')[1]?.split('+')[0]?.toLowerCase()
  switch (subtype) {
    case 'jpeg':
    case 'jpg':
      return '.jpg'
    case 'png':
    case 'gif':
    case 'bmp':
    case 'webp':
    case 'svg':
      return `.${subtype}`
    default:
      return ''
  }
}

/**
 * 生成上传文件名：插入位置标题路径 + 章节内序号，保留原图片扩展名。
 * 后端存储键仍由 provider 按 GUID 生成，此名称只作为原始文件名元数据。
 */
export function buildSectionImageFileName(
  headingPath: string[],
  sequence: number,
  originalName: string,
  contentType = '',
): string {
  const headingStem = headingPath
    .map(sanitizeFileNameSegment)
    .filter(Boolean)
    .join('-')
  const originalStem = originalName.replace(/\.[^./\\]*$/, '')
  const fallbackStem = sanitizeFileNameSegment(originalStem) || 'image'
  const normalizedSequence = Math.max(1, Math.trunc(sequence))
  return `${headingStem || fallbackStem}-${normalizedSequence}${resolveExtension(originalName, contentType)}`
}

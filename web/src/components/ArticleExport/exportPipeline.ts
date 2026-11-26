import { nextTick } from 'vue'
import { contentFileApi } from '@/api/contentFile'
import {
  sortByDocumentOrder,
  type CanvasCaptureRegistry,
} from '@/components/MarkdownEditor/canvasCapture'
import { sanitizeFileNameSegment } from '@/components/MarkdownEditor/imageUploadNaming'
import {
  collectContentImageUrls,
  rewriteMarkdownForExport,
  type ExportFile,
  type ExportMeta,
  type PreparedImage,
} from './markdownBundle'

const IMAGE_WAIT_TIMEOUT = 15_000

/**
 * 等待渲染面内所有图片落定：导出面要求"整篇可打印"，
 * 未完成的图片会在 PDF 里留下占位块，因此逐个等 load/error，超时兜底放行。
 */
export async function waitForImages(root: HTMLElement, timeoutMs = IMAGE_WAIT_TIMEOUT): Promise<void> {
  const images = Array.from(root.querySelectorAll('img'))
  await Promise.all(
    images.map(
      (image) =>
        new Promise<void>((resolve) => {
          if (image.complete && image.naturalWidth > 0) {
            resolve()
            return
          }
          const settle = () => resolve()
          image.addEventListener('load', settle, { once: true })
          image.addEventListener('error', settle, { once: true })
          window.setTimeout(settle, timeoutMs)
        }),
    ),
  )
}

/** 逐块截图：顺序按 DOM（即 Markdown 围栏顺序），空画布返回 null。 */
export async function captureCanvasSnapshots(
  registry: CanvasCaptureRegistry,
): Promise<Array<string | null>> {
  const entries = sortByDocumentOrder(registry.entries())
  const snapshots = await Promise.all(
    entries.map(async (entry) => {
      try {
        return await entry.capture()
      } catch {
        return null
      }
    }),
  )
  await nextTick()
  return snapshots
}

function fileNameFromUrl(url: string): string {
  const segment = url.split('/').pop() ?? ''
  const decoded = decodeURIComponent(segment)
  return /\.[a-z0-9]{2,5}$/i.test(decoded) ? decoded : ''
}

/** 随包保存的签名文件；payload 与 signature 原样写入 signature.json。 */
export interface ExportSignaturePackage {
  format: string
  algorithm: string
  keyId: string
  publicKey: string
  payload: string
  signature: string
  exportedAt: string
  verification: Record<string, string>
}

export type ExportSignatureStatus = 'signed' | 'skipped' | 'failed'

/**
 * 打包：正文原样保留（仅重写围栏与站内图片），图片走 blobCache——
 * 页面上已加载过的图片不会再次请求。作者本人导出时由 sign 回调补充签名文件。
 */
export async function buildMarkdownBundle(options: {
  title: string
  markdown: string
  snapshots: Array<string | null>
  meta?: ExportMeta
  /** 仅在作者本人导出时传入；抛错时仍导出未签名文件。 */
  sign?: (files: ExportFile[]) => Promise<ExportSignaturePackage>
}): Promise<{
  blob: Blob
  fileName: string
  externalImages: string[]
  signatureStatus: ExportSignatureStatus
}> {
  const urls = collectContentImageUrls(options.markdown, options.meta?.cover)
  const images = new Map<string, PreparedImage>()
  await Promise.all(
    urls.map(async (url) => {
      try {
        const blob = await contentFileApi.getBlob(url)
        images.set(url, {
          fileName: fileNameFromUrl(url),
          type: blob.type,
          bytes: new Uint8Array(await blob.arrayBuffer()),
        })
      } catch {
        // 取不到的图片保持原链接，交给 externalImages 提示
      }
    }),
  )

  const result = rewriteMarkdownForExport({
    title: options.title,
    markdown: options.markdown,
    snapshots: options.snapshots,
    images,
    meta: options.meta,
  })

  const { zip } = await import('fflate')
  const stem = sanitizeFileNameSegment(options.title) || 'article'
  const markdownFileName = `${stem}.md`
  const exportFiles: ExportFile[] = [
    { path: markdownFileName, data: new TextEncoder().encode(result.markdown) },
    ...result.files,
  ]

  let signature: ExportSignaturePackage | null = null
  let signatureStatus: ExportSignatureStatus = 'skipped'
  if (options.sign) {
    try {
      signature = await options.sign(exportFiles)
      signatureStatus = 'signed'
    } catch {
      signatureStatus = 'failed'
    }
  }

  const entries: Record<string, Uint8Array> = {}
  for (const file of exportFiles) entries[file.path] = file.data
  if (signature) {
    entries['signature.json'] = new TextEncoder().encode(JSON.stringify(signature, null, 2))
  }

  const zipped = await new Promise<Uint8Array>((resolve, reject) => {
    zip(entries, { level: 0 }, (error, data) => (error ? reject(error) : resolve(data)))
  })

  return {
    blob: new Blob([zipped as unknown as BlobPart], { type: 'application/zip' }),
    fileName: `${stem}.zip`,
    externalImages: result.externalImages,
    signatureStatus,
  }
}

export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 0)
}

import request from '@/utils/request'
import { postSystemContentFileUpload } from '@/api/system'
import type { ContentFileDto } from '@/api/system'
import { createBlobCache } from '@/utils/blobCache'

/** 内容文件（后端契约 ContentFileDto） */
export type ContentFile = ContentFileDto & {
  id: string
  provider: string
  key: string
  fileName: string
  contentType: string
  url: string
}

export const contentFileApi = {
  async upload(file: File, encrypted = false) {
    const prepared = encrypted ? await (await import('@/e2ee/fileTransfer')).prepareEncryptedUpload(file) : null
    const result = await postSystemContentFileUpload({
      body: { file: prepared?.file ?? file } as never,
      throwOnError: true,
    }).then((res) => res.data as ContentFile)
    return prepared ? { ...result, fileName: file.name, url: result.url + prepared.fragment } : result
  },

  /**
   * 获取文件内容（blob）
   *
   * 注：url 为预签名/内部 HMAC 动态地址，不属于契约路径，保留原生 request 调用。
   */
  async getBlob(url: string) {
    const rawUrl = url.split('#')[0]!
    const blob = await blobCache.get(rawUrl)
    if (url.includes('#ef=')) return (await import('@/e2ee/fileTransfer')).decryptDownloadedFile(blob, url)
    return blob
  },
}

const blobCache = createBlobCache((url: string) =>
  request
    .get<Blob>(url, {
      responseType: 'blob',
      // 同一文件可同时被封面和正文引用，去重会取消先发出的请求
      ...({ cancelable: false } as Record<string, unknown>),
    })
    .then((response) => response.data),
)

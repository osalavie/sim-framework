import type { AxiosProgressEvent } from 'axios'
import request from '@/utils/request'
import {
  postSystemFileManagementUpload,
  getSystemFileManagementDownloadUrl,
  deleteSystemFileManagement,
  postSystemFileManagementRenderTemplate,
  postSystemFileManagementExport,
  postSystemFileManagementConvertToPdf,
  postSystemFileManagementProcessImage,
} from '@/api/system'
import type {
  StoredFileDto,
  FileAccessUrlDto as ContractFileAccessUrlDto,
} from '@/api/system'

export type FileStorageProvider = 'local' | 'database' | 's3' | 'tencent-cos' | 'aliyun-oss'

/** 上传结果（后端契约 StoredFileDto） */
export type StoredFile = StoredFileDto & {
  provider: string
  key: string
  fileName: string
  contentType: string
}

/** 预签名下载 URL（后端契约 FileAccessUrlDto） */
export type FileAccessUrlDto = ContractFileAccessUrlDto & {
  url: string
  isExternalUrl: boolean
}

export interface UploadFileOptions {
  key?: string
  provider?: FileStorageProvider
  onProgress?: (progress: number) => void
}

export interface ImageProcessOptions {
  cropX?: number
  cropY?: number
  cropWidth?: number
  cropHeight?: number
  maxWidth?: number
  maxHeight?: number
  quality?: number
  outputExtension?: 'jpg' | 'jpeg' | 'png' | 'webp'
}

function progressHandler(callback?: (progress: number) => void) {
  if (!callback) return undefined
  return (event: AxiosProgressEvent) => {
    if (!event.total) return
    callback(Math.min(100, Math.round((event.loaded / event.total) * 100)))
  }
}

export const fileManagementApi = {
  upload(file: File, options: UploadFileOptions = {}) {
    return postSystemFileManagementUpload({
      body: { file } as never,
      query: { key: options.key, provider: options.provider },
      onUploadProgress: progressHandler(options.onProgress),
      throwOnError: true,
    }).then((res) => res.data as StoredFile)
  },

  /**
   * 获取预签名下载 URL（推荐方式，替代直接代理下载）
   * S3 返回外部预签名 URL；local/database 返回 HMAC 签名的内部 URL
   */
  getDownloadUrl(key: string, provider?: FileStorageProvider) {
    return getSystemFileManagementDownloadUrl({
      query: { key, provider },
      throwOnError: true,
    }).then((res) => res.data as FileAccessUrlDto)
  },

  /**
   * 下载文件（两步：先获取预签名 URL，再 fetch 下载）
   */
  async download(key: string, provider?: FileStorageProvider): Promise<Blob> {
    const { url, isExternalUrl } = await this.getDownloadUrl(key, provider)

    if (isExternalUrl) {
      // S3 等外部预签名 URL：直接 fetch 下载
      const response = await fetch(url)
      if (!response.ok) throw new Error(`Download failed: ${response.status}`)
      return response.blob()
    }

    // local/database 内部 HMAC URL：走 axios 携带认证信息
    return request
      .get<Blob>(url, { responseType: 'blob' })
      .then((response) => response.data)
  },

  delete(key: string, provider?: FileStorageProvider) {
    return deleteSystemFileManagement({
      query: { key, provider },
      throwOnError: true,
    }).then((res) => res.data)
  },

  renderTemplate(input: {
    template: string
    modelJson: string
    fileName: string
    contentType?: string
  }) {
    return postSystemFileManagementRenderTemplate({
      body: input as never,
      responseType: 'blob',
      throwOnError: true,
    }).then((res) => res.data as Blob)
  },

  exportTable(input: {
    fileName: string
    columns: string[]
    rows: Array<Record<string, string | null>>
    format: 0 | 1 | 2
  }) {
    return postSystemFileManagementExport({
      body: input as never,
      responseType: 'blob',
      throwOnError: true,
    }).then((res) => res.data as Blob)
  },

  convertToPdf(file: File, onProgress?: (progress: number) => void) {
    return postSystemFileManagementConvertToPdf({
      body: { file } as never,
      responseType: 'blob',
      onUploadProgress: progressHandler(onProgress),
      throwOnError: true,
    }).then((res) => res.data as Blob)
  },

  processImage(file: File, options: ImageProcessOptions = {}) {
    return postSystemFileManagementProcessImage({
      body: { file } as never,
      query: options,
      responseType: 'blob',
      throwOnError: true,
    }).then((res) => res.data as Blob)
  },
}

export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 0)
}

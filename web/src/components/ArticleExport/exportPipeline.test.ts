import { describe, expect, it, vi } from 'vitest'
import { strFromU8, unzipSync } from 'fflate'
import { buildMarkdownBundle, type ExportSignaturePackage } from './exportPipeline'
import type { ExportFile } from './markdownBundle'

const META = {
  creationTime: '2026-10-28T14:56:00',
  lastModificationTime: null,
  tags: [],
  cover: null,
  authorName: '林知远',
  authorNo: 'Author.000001',
  authorId: '00000000-0000-0000-0000-000000000001',
  copyright: '© 2026 林知远',
  labels: {
    author: '作者',
    published: '发布',
    updated: '更新',
    tags: '标签',
    cover: '封面',
    copyright: '版权',
    dateFormat: 'YYYY年MM月DD日 HH:mm',
  },
}

const signaturePackage: ExportSignaturePackage = {
  format: 'sim-article-signature/v1',
  algorithm: 'SM2-SM3',
  keyId: 'a'.repeat(32),
  publicKey: 'public-key',
  payload: '{"format":"sim-article-signature/v1"}',
  signature: 'signature',
  exportedAt: '2026-11-02T00:00:00Z',
  verification: { algorithm: 'SM2-SM3', files: 'SHA-256' },
}

const readBlob = (blob: Blob): Promise<Uint8Array> =>
  new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(new Uint8Array(reader.result as ArrayBuffer))
    reader.onerror = () => reject(reader.error)
    reader.readAsArrayBuffer(blob)
  })

const unzip = async (blob: Blob): Promise<Record<string, Uint8Array>> =>
  unzipSync(await readBlob(blob))

describe('buildMarkdownBundle 签名包', () => {
  it('作者签名时把 signature.json 与文件摘要一起打包', async () => {
    const sign = vi.fn<(files: ExportFile[]) => Promise<ExportSignaturePackage>>(
      async () => signaturePackage,
    )
    const result = await buildMarkdownBundle({
      title: '标题',
      markdown: '# 标题\n\n正文',
      snapshots: [],
      meta: META,
      sign,
    })

    expect(result.signatureStatus).toBe('signed')
    expect(sign).toHaveBeenCalledTimes(1)
    const files = sign.mock.calls[0]?.[0] ?? []
    expect(files.map((file) => file.path)).toEqual(['标题.md'])

    const entries = await unzip(result.blob)
    expect(strFromU8(entries['标题.md'] ?? new Uint8Array())).toContain('author: "林知远"')
    expect(JSON.parse(strFromU8(entries['signature.json'] ?? new Uint8Array()))).toMatchObject({
      signature: 'signature',
      payload: '{"format":"sim-article-signature/v1"}',
    })
  })

  it('签名失败时仍导出未签名文件', async () => {
    const result = await buildMarkdownBundle({
      title: '标题',
      markdown: '# 标题\n\n正文',
      snapshots: [],
      meta: META,
      sign: async () => {
        throw new Error('sign failed')
      },
    })

    expect(result.signatureStatus).toBe('failed')
    const entries = await unzip(result.blob)
    expect(entries['signature.json']).toBeUndefined()
    expect(entries['标题.md']).toBeDefined()
  })

  it('未传 sign 时不生成签名文件', async () => {
    const result = await buildMarkdownBundle({
      title: '标题',
      markdown: '# 标题\n\n正文',
      snapshots: [],
      meta: META,
    })

    expect(result.signatureStatus).toBe('skipped')
    const entries = await unzip(result.blob)
    expect(entries['signature.json']).toBeUndefined()
  })
})

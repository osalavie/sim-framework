import { describe, expect, it } from 'vitest'
import {
  collectContentImageUrls,
  dataUrlToBytes,
  rewriteMarkdownForExport,
  type PreparedImage,
} from './markdownBundle'

const PNG_DATA_URL =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=='

function imageOf(url: string, fileName = 'shot.png'): PreparedImage {
  return {
    fileName,
    type: 'image/png',
    bytes: dataUrlToBytes(PNG_DATA_URL),
  }
}

describe('collectContentImageUrls', () => {
  it('只收集代码围栏之外的站内图片', () => {
    const markdown = [
      '![a](/api/system/content-file/1)',
      '```md',
      '![b](/api/system/content-file/2)',
      '```',
      '![c](https://cdn.example.com/x.png)',
      '![a again](/api/system/content-file/1 "标题")',
    ].join('\n')

    expect(collectContentImageUrls(markdown)).toEqual(['/api/system/content-file/1'])
  })
})

describe('dataUrlToBytes', () => {
  it('解码 base64 dataURL', () => {
    const bytes = dataUrlToBytes(PNG_DATA_URL)
    expect(bytes[0]).toBe(0x89)
    expect(bytes[1]).toBe(0x50)
  })
})

describe('rewriteMarkdownForExport', () => {
  it('站内图片改写为 images/ 并带上文件', () => {
    const url = '/api/system/content-file/9'
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: `# 已有标题\n\n正文\n\n![封面图](${url})`,
      snapshots: [],
      images: new Map([[url, imageOf(url)]]),
    })

    expect(result.markdown).toContain('![封面图](images/封面图.png)')
    expect(result.files.map((file) => file.path)).toEqual(['images/封面图.png'])
    expect(result.externalImages).toEqual([])
  })

  it('同一地址复用同一文件名，不重复打包', () => {
    const url = '/api/system/content-file/9'
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: `# 标题\n\n![图](${url})\n\n![图](${url})`,
      snapshots: [],
      images: new Map([[url, imageOf(url)]]),
    })

    expect(result.files).toHaveLength(1)
    expect(result.markdown.match(/images\/图\.png/g)).toHaveLength(2)
  })

  it('重名图片自动加序号', () => {
    const one = '/api/system/content-file/1'
    const two = '/api/system/content-file/2'
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: `# 标题\n\n![图](${one})\n\n![图](${two})`,
      snapshots: [],
      images: new Map([
        [one, imageOf(one)],
        [two, imageOf(two)],
      ]),
    })

    expect(result.files.map((file) => file.path)).toEqual(['images/图.png', 'images/图-2.png'])
  })

  it('外链图片原样保留并统计', () => {
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: '# 标题\n\n![外链](https://cdn.example.com/a.png)',
      snapshots: [],
      images: new Map(),
    })

    expect(result.markdown).toContain('![外链](https://cdn.example.com/a.png)')
    expect(result.externalImages).toEqual(['https://cdn.example.com/a.png'])
  })

  it('canvas 围栏按标题链命名并替换为图片', () => {
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: [
        '# 一级标题',
        '',
        '## 流程',
        '',
        '```canvas',
        '{"version":2,"nodes":[],"edges":[]}',
        '```',
      ].join('\n'),
      snapshots: [PNG_DATA_URL],
      images: new Map(),
    })

    expect(result.markdown).toContain('![一级标题 > 流程](images/一级标题-流程-canvas-1.png)')
    expect(result.markdown).not.toContain('```canvas')
    expect(result.files.map((file) => file.path)).toEqual(['images/一级标题-流程-canvas-1.png'])
  })

  it('空画布截图时整块移除围栏', () => {
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: '# 标题\n\n```canvas\n{}\n```\n\n正文',
      snapshots: [null],
      images: new Map(),
    })

    expect(result.markdown).not.toContain('canvas')
    expect(result.files).toEqual([])
  })

  it('代码围栏内的图片语法保持不变', () => {
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: [
        '# 标题',
        '',
        '```md',
        '![示例](/api/system/content-file/1)',
        '```',
      ].join('\n'),
      snapshots: [],
      images: new Map([['/api/system/content-file/1', imageOf('/api/system/content-file/1')]]),
    })

    expect(result.markdown).toContain('![示例](/api/system/content-file/1)')
    expect(result.files).toEqual([])
  })

  it('正文没有 H1 时补标题，已有则不重复', () => {
    const withoutTitle = rewriteMarkdownForExport({
      title: '文章标题',
      markdown: '正文开始',
      snapshots: [],
      images: new Map(),
    })
    expect(withoutTitle.markdown).toBe('# 文章标题\n\n正文开始')

    const withTitle = rewriteMarkdownForExport({
      title: '文章标题',
      markdown: '# 已有标题\n\n正文',
      snapshots: [],
      images: new Map(),
    })
    expect(withTitle.markdown.startsWith('# 已有标题')).toBe(true)
  })

  it('缺少已读取图片时保持原链接并计入外链', () => {
    const url = '/api/system/content-file/404'
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: `# 标题\n\n![缺失](${url})`,
      snapshots: [],
      images: new Map(),
    })

    expect(result.markdown).toContain(url)
    expect(result.externalImages).toEqual([url])
  })
})

const META = {
  creationTime: '2026-10-28T14:56:00',
  lastModificationTime: '2026-10-29T11:13:00',
  tags: ['ABP', 'TypeScript'],
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

describe('文章元信息导出', () => {
  it('frontmatter 与引用块都带作者、日期、标签与版权', () => {
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: '# 标题\n\n正文',
      snapshots: [],
      images: new Map(),
      meta: META,
    })

    expect(result.markdown.startsWith('---\n')).toBe(true)
    expect(result.markdown).toContain('title: "标题"')
    expect(result.markdown).toContain('author: "林知远"')
    expect(result.markdown).toContain('authorId: "00000000-0000-0000-0000-000000000001"')
    expect(result.markdown).toContain('authorNo: "Author.000001"')
    expect(result.markdown).toContain('publishedAt: "2026-10-28T14:56:00"')
    expect(result.markdown).toContain('updatedAt: "2026-10-29T11:13:00"')
    expect(result.markdown).toContain('  - "ABP"')
    expect(result.markdown).toContain('copyright: "© 2026 林知远"')
    expect(result.markdown).toContain('> **作者**：林知远 (Author.000001)')
    expect(result.markdown).toContain('> **发布**：2026年10月28日 14:56')
    expect(result.markdown).toContain('> **更新**：2026年10月29日 11:13')
    expect(result.markdown).toContain('> **标签**：`ABP` `TypeScript`')
    expect(result.markdown).toContain('> **版权**：© 2026 林知远')
    // 不依赖内联 HTML，任何查看器都能渲染
    expect(result.markdown).not.toContain('<div')
    expect(result.markdown).not.toContain('<svg')
    expect(result.markdown.indexOf('**作者**')).toBeLessThan(result.markdown.indexOf('正文'))
  })

  it('没有更新日期时 frontmatter 与引用块都回退发布日期', () => {
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: '# 标题\n\n正文',
      snapshots: [],
      images: new Map(),
      meta: { ...META, lastModificationTime: null },
    })

    expect(result.markdown).toContain('updatedAt: "2026-10-28T14:56:00"')
    expect(result.markdown).toContain('> **更新**：2026年10月28日 14:56')
  })

  it('标签中的反引号不会破坏行内代码', () => {
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: '# 标题\n\n正文',
      snapshots: [],
      images: new Map(),
      meta: { ...META, tags: ["a`b"] },
    })

    expect(result.markdown).toContain("> **标签**：`a'b`")
  })

  it('站内封面打包进 images/ 并引用相对路径', () => {
    const cover = '/api/system/content-file/5'
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: '# 标题\n\n正文',
      snapshots: [],
      images: new Map([[cover, imageOf(cover, 'upload.jpg')]]),
      meta: { ...META, cover },
    })

    expect(result.markdown).toContain('![封面](images/cover.png)')
    expect(result.markdown.indexOf('![封面]')).toBeLessThan(result.markdown.indexOf('# 标题'))
    expect(result.files.map((file) => file.path)).toEqual(['images/cover.png'])
    expect(result.externalImages).toEqual([])
  })

  it('外链封面保持原地址并计入外链', () => {
    const cover = 'https://cdn.example.com/cover.png'
    const result = rewriteMarkdownForExport({
      title: '标题',
      markdown: '# 标题\n\n正文',
      snapshots: [],
      images: new Map(),
      meta: { ...META, cover },
    })

    expect(result.markdown).toContain(`![封面](${cover})`)
    expect(result.externalImages).toEqual([cover])
  })
})

describe('collectContentImageUrls 封面', () => {
  it('把站内封面地址一并收集', () => {
    expect(collectContentImageUrls('正文', '/api/system/content-file/cover')).toEqual([
      '/api/system/content-file/cover',
    ])
  })
})

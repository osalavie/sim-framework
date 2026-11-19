import { describe, expect, it } from 'vitest'
import { buildSectionImageFileName } from './imageUploadNaming'

describe('buildSectionImageFileName', () => {
  it('joins ancestor heading titles with the section sequence', () => {
    expect(
      buildSectionImageFileName(
        ['模块化架构实践', '分层职责'],
        1,
        'clipboard.png',
        'image/png',
      ),
    ).toBe('模块化架构实践-分层职责-1.png')
  })

  it('falls back to the original file stem when the insertion point has no heading', () => {
    expect(buildSectionImageFileName([], 3, 'screen shot.webp', 'image/webp')).toBe(
      'screen-shot-3.webp',
    )
  })

  it('removes characters that are invalid in file names', () => {
    expect(buildSectionImageFileName(['架构/实践', '边界: 条件?'], 2, 'image.png')).toBe(
      '架构实践-边界-条件-2.png',
    )
  })

  it('derives an extension from the content type when the clipboard name has none', () => {
    expect(buildSectionImageFileName(['章节'], 1, 'image', 'image/jpeg')).toBe(
      '章节-1.jpg',
    )
  })
})

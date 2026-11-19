/** 阅读与导出共用：没有更新日期时，以发布日期作为最新更新日期。 */
export function effectiveUpdatedAt(article: {
  creationTime: string
  lastModificationTime?: string | null
}): string {
  return article.lastModificationTime || article.creationTime
}

const yearOf = (value: string | null | undefined): number | undefined => {
  const match = /^(\d{4})/.exec(value ?? '')
  if (!match) return undefined
  const year = Number(match[1])
  return Number.isFinite(year) ? year : undefined
}

/** 版权声明取发布年份；跨年更新时使用年份区间。 */
export function buildCopyrightNotice(
  creationTime: string,
  lastModificationTime: string | null | undefined,
  authorName: string | null | undefined,
): string {
  const name = authorName?.trim()
  const start = yearOf(creationTime)
  if (!name || start === undefined) return ''

  const end = yearOf(lastModificationTime) ?? start
  const years = end > start ? `${start}–${end}` : `${start}`
  return `© ${years} ${name}`
}

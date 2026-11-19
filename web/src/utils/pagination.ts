/** Web 端逐页拉取的单页上限；服务端上限为 500，Web 仍按 100 控制请求体积。 */
export const MAX_PAGE_SIZE = 100

interface PagedResponse<T> {
  items?: T[] | null
  totalCount?: number | null
}

/**
 * 逐页拉取直到 totalCount。仅用于必须一次拿到全量选项或树节点的场景；
 * 普通列表分页请继续使用 useInfiniteList。
 */
export async function collectAllPages<T>(
  fetchPage: (skipCount: number, maxResultCount: number) => Promise<PagedResponse<T>>,
): Promise<T[]> {
  const items: T[] = []
  let skipCount = 0
  while (true) {
    const response = await fetchPage(skipCount, MAX_PAGE_SIZE)
    const pageItems = response.items ?? []
    items.push(...pageItems)
    skipCount += pageItems.length
    if (!pageItems.length || skipCount >= (response.totalCount ?? 0)) break
  }
  return items
}

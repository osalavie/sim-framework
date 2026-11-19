import { computed, type Ref } from 'vue'

interface PageState {
  page: number
  pageSize: number
  pageCount: number
  itemCount: number
}

interface PageResult<T> {
  items?: T[]
  totalCount?: number
}

interface Options<T> {
  items: Ref<T[]>
  loading: Ref<boolean>
  loadingMore: Ref<boolean>
  pagination: Ref<PageState>
  fetchPage: (skipCount: number, maxResultCount: number) => Promise<PageResult<T>>
  onError?: (error: unknown) => void
}

/** 卡片模式无限滚动：按已加载数量追加下一页。 */
export function useInfiniteList<T>({
  items,
  loading,
  loadingMore,
  pagination,
  fetchPage,
  onError,
}: Options<T>) {
  const hasMore = computed(() => items.value.length < pagination.value.itemCount)

  const loadMore = async () => {
    if (loading.value || loadingMore.value || !hasMore.value) return
    loadingMore.value = true
    try {
      const response = await fetchPage(items.value.length, pagination.value.pageSize)
      const next = response.items || []
      items.value = [...items.value, ...next]
      pagination.value.itemCount = response.totalCount || 0
      pagination.value.pageCount = Math.ceil(
        (response.totalCount || 0) / pagination.value.pageSize,
      )
      pagination.value.page = Math.max(
        1,
        Math.ceil(items.value.length / pagination.value.pageSize),
      )
    } catch (error) {
      onError?.(error)
    } finally {
      loadingMore.value = false
    }
  }

  return { hasMore, loadMore }
}

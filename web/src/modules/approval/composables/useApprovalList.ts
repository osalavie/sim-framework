import { ref, type Ref } from 'vue'
import { useDiscreteApi } from '@/hooks/discreteApi'
import type { PageData } from '@/types/api'
import type { ApprovalPaginationState } from '../utils'

/** 表格分页状态：与 naive-ui data-table / pagination 的字段保持一致。 */
export interface ApprovalListPagination extends ApprovalPaginationState {
  itemCount: number
  pageSizes: number[]
  showSizePicker: boolean
}

export interface UseApprovalListOptions<T, F, Q> {
  /** 初始筛选状态工厂，重置时复用。 */
  createFilter: () => F
  /** 界面筛选状态 + 分页信息 → 接口查询参数。 */
  buildQuery: (filter: F, pagination: ApprovalPaginationState) => Q
  /** 分页拉取。 */
  fetch: (query: Q) => Promise<PageData<T>>
  /** 加载失败提示文案。 */
  errorMessage: string
  pageSize?: number
}

/**
 * 审批列表通用状态：筛选、分页与加载。
 * 各视图只负责声明列与筛选表单，避免四份重复的加载逻辑。
 */
export function useApprovalList<T, F, Q>(options: UseApprovalListOptions<T, F, Q>) {
  const items = ref([]) as Ref<T[]>
  const loading = ref(false)
  const filter = ref(options.createFilter()) as Ref<F>
  const pagination = ref<ApprovalListPagination>({
    page: 1,
    pageSize: options.pageSize ?? 20,
    itemCount: 0,
    showSizePicker: true,
    pageSizes: [10, 20, 50],
  })

  const load = async () => {
    loading.value = true
    try {
      const response = await options.fetch(options.buildQuery(filter.value, pagination.value))
      items.value = response.items ?? []
      pagination.value.itemCount = response.totalCount ?? 0
    } catch {
      useDiscreteApi.message.error(options.errorMessage)
    } finally {
      loading.value = false
    }
  }

  const search = () => {
    pagination.value.page = 1
    void load()
  }

  const reset = () => {
    filter.value = options.createFilter()
    search()
  }

  const handlePageChange = (page: number) => {
    pagination.value.page = page
    void load()
  }

  const handlePageSizeChange = (pageSize: number) => {
    pagination.value.page = 1
    pagination.value.pageSize = pageSize
    void load()
  }

  return {
    items,
    loading,
    filter,
    pagination,
    load,
    search,
    reset,
    handlePageChange,
    handlePageSizeChange,
  }
}

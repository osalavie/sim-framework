import type { PaginationProps } from 'naive-ui'

export interface TablePaginationState {
  page: number
  pageSize: number
  itemCount: number
}

export const TABLE_PAGE_SIZES = [10, 20, 30, 50] as const

/** Shared data-table pagination config: page numbers + size picker + quick jumper. */
export function createTablePagination(state: TablePaginationState): PaginationProps {
  return {
    page: state.page,
    pageSize: state.pageSize,
    itemCount: state.itemCount,
    pageSizes: [...TABLE_PAGE_SIZES],
    showSizePicker: true,
    showQuickJumper: true,
  }
}

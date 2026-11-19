/**
 * 查询参数构建工具
 * 用于构建符合 ABP 格式的查询参数
 */

/**
 * 排序信息
 */
export interface SortInfo {
  field: string
  order: 'ascend' | 'descend'
}

/**
 * 基础查询参数
 */
export interface BaseQueryParams {
  skipCount?: number
  maxResultCount?: number
  keyword?: string
  sorting?: string
}

/**
 * 分页信息
 */
export interface PaginationInfo {
  page: number
  pageSize: number
}

/**
 * 构建 ABP 格式的排序字符串
 *
 * @param sortFields - 排序字段数组
 * @returns 排序字符串，格式为 "字段名 asc/desc"，多个排序用逗号分隔
 *
 * @example
 * buildSortingString([
 *   { field: 'name', order: 'ascend' },
 *   { field: 'creationTime', order: 'descend' }
 * ])
 * // 返回: "name asc, creationTime desc"
 */
export function buildSortingString(sortFields: SortInfo[]): string | undefined {
  if (!sortFields || sortFields.length === 0) {
    return undefined
  }

  return sortFields
    .map(sort => {
      const direction = sort.order === 'ascend' ? 'asc' : 'desc'
      return `${sort.field} ${direction}`
    })
    .join(', ')
}

/**
 * 构建查询参数对象
 *
 * @param options - 查询选项
 * @param options.pagination - 分页信息
 * @param options.keyword - 搜索关键词
 * @param options.sortFields - 排序字段数组
 * @param options.additionalParams - 额外的查询参数
 * @returns 符合 ABP 格式的查询参数对象
 *
 * @example
 * buildQueryParams({
 *   pagination: { page: 2, pageSize: 20 },
 *   keyword: 'test',
 *   sortFields: [{ field: 'name', order: 'ascend' }],
 *   additionalParams: { moduleId: '123' }
 * })
 * // 返回: {
 * //   skipCount: 20,
 * //   maxResultCount: 20,
 * //   keyword: 'test',
 * //   sorting: 'name asc',
 * //   moduleId: '123'
 * // }
 */
export function buildQueryParams<T extends Record<string, unknown> = Record<string, unknown>>(options: {
  pagination: PaginationInfo
  keyword?: string
  sortFields?: SortInfo[]
  additionalParams?: T
}): BaseQueryParams & T {
  const { pagination, keyword, sortFields, additionalParams } = options

  const params: BaseQueryParams = {
    skipCount: (pagination.page - 1) * pagination.pageSize,
    maxResultCount: pagination.pageSize,
  }

  // 添加关键词（如果存在且不为空）
  if (keyword && keyword.trim()) {
    params.keyword = keyword.trim()
  }

  // 添加排序（如果存在）
  const sorting = buildSortingString(sortFields || [])
  if (sorting) {
    params.sorting = sorting
  }

  // 合并额外参数
  if (additionalParams) {
    return { ...params, ...additionalParams } as BaseQueryParams & T
  }

  return params as BaseQueryParams & T
}

/**
 * 计算分页总页数
 *
 * @param totalCount - 总记录数
 * @param pageSize - 每页大小
 * @returns 总页数
 */
export function calculatePageCount(totalCount: number, pageSize: number): number {
  if (pageSize <= 0) {
    return 0
  }
  return Math.ceil(totalCount / pageSize)
}

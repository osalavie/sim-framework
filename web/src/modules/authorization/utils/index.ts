/**
 * 授权模块工具函数
 */

import type { SortInfo } from '../types'
import { SORT_ORDERS, SEX_LABELS } from '../constants'

/**
 * 构建排序字符串
 * 将排序信息数组转换为 ABP 格式的排序字符串
 */
export function buildSortingString(sortFields: SortInfo[]): string | undefined {
  if (!sortFields || sortFields.length === 0) {
    return undefined
  }

  return sortFields
    .map((sort) => {
      const direction = sort.order === 'ascend' ? SORT_ORDERS.ascend : SORT_ORDERS.descend
      return `${sort.field} ${direction}`
    })
    .join(', ')
}

/**
 * 构建查询参数
 * 将分页、排序、搜索等参数组合成查询对象
 */
export function buildQueryParams(options: {
  page: number
  pageSize: number
  keyword?: string
  sortFields?: SortInfo[]
  [key: string]: unknown
}) {
  const { page, pageSize, keyword, sortFields, ...rest } = options

  return {
    skipCount: (page - 1) * pageSize,
    maxResultCount: pageSize,
    keyword: keyword || undefined,
    sorting: buildSortingString(sortFields || []),
    ...rest,
  }
}

/**
 * 判断是否为取消错误
 */
export function isCancelError(error: unknown): boolean {
  if (!error) return false

  // Axios 取消错误
  if ((error as { __CANCEL__?: boolean }).__CANCEL__) return true

  // 标准取消错误
  const err = error as { name?: string; code?: string }
  return err.name === 'CanceledError' || err.code === 'ERR_CANCELED'
}

/**
 * 格式化错误消息
 */
export function formatErrorMessage(error: unknown): string {
  if (typeof error === 'string') {
    return error
  }

  if (error instanceof Error) {
    return error.message
  }

  if (typeof error === 'object' && error !== null) {
    const err = error as Record<string, unknown>
    const message = err.message ?? err.error ?? err.msg
    return typeof message === 'string' ? message : '操作失败'
  }

  return '未知错误'
}

/**
 * 获取性别标签
 */
export function getSexLabel(sex?: number): string {
  if (sex === undefined || sex === null) {
    return SEX_LABELS[0] ?? '未知'
  }
  return SEX_LABELS[sex] ?? SEX_LABELS[0] ?? '未知'
}

/**
 * 验证用户名
 */
export function validateUserName(userName: string): boolean {
  return /^[a-zA-Z0-9_]{3,20}$/.test(userName)
}

/**
 * 验证邮箱
 */
export function validateEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
}

/**
 * 验证手机号
 */
export function validatePhone(phone: string): boolean {
  return /^1[3-9]\d{9}$/.test(phone)
}

/**
 * 验证编码
 */
export function validateCode(code: string): boolean {
  return /^[a-zA-Z0-9_.-]{2,50}$/.test(code)
}

/**
 * 验证路径
 */
export function validatePath(path: string): boolean {
  return /^\/[a-zA-Z0-9/_-]*$/.test(path)
}

/**
 * 深度克隆对象
 */
export function deepClone<T>(obj: T): T {
  if (obj === null || typeof obj !== 'object') {
    return obj
  }

  if (obj instanceof Date) {
    return new Date(obj.getTime()) as T
  }

  if (obj instanceof Array) {
    return obj.map((item) => deepClone(item)) as T
  }

  if (obj instanceof Object) {
    const clonedObj = {} as T
    for (const key in obj) {
      if (obj.hasOwnProperty(key)) {
        clonedObj[key] = deepClone(obj[key])
      }
    }
    return clonedObj
  }

  return obj
}

/**
 * 比较两个对象是否相等
 */
export function isEqual(obj1: unknown, obj2: unknown): boolean {
  if (obj1 === obj2) return true

  if (
    typeof obj1 !== 'object' ||
    obj1 === null ||
    typeof obj2 !== 'object' ||
    obj2 === null
  ) {
    return false
  }

  const keys1 = Object.keys(obj1)
  const keys2 = Object.keys(obj2)
  const record1 = obj1 as Record<string, unknown>
  const record2 = obj2 as Record<string, unknown>

  if (keys1.length !== keys2.length) return false

  for (const key of keys1) {
    if (!keys2.includes(key) || !isEqual(record1[key], record2[key])) {
      return false
    }
  }

  return true
}

/**
 * 过滤空值
 * 移除对象中值为 undefined、null 或空字符串的属性
 */
export function filterEmptyValues<T extends Record<string, unknown>>(obj: T): Partial<T> {
  const result: Partial<T> = {}

  for (const key in obj) {
    if (obj.hasOwnProperty(key)) {
      const value = obj[key]
      if (value !== undefined && value !== null && value !== '') {
        result[key] = value
      }
    }
  }

  return result
}

/**
 * 生成唯一 ID
 */
let idCounter = 0
export function generateId(prefix = 'id'): string {
  return `${prefix}-${Date.now()}-${++idCounter}`
}

/**
 * 延迟执行
 */
export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

/**
 * 重试函数
 */
export async function retry<T>(
  fn: () => Promise<T>,
  options: {
    maxAttempts?: number
    delayMs?: number
    onRetry?: (attempt: number, error: unknown) => void
  } = {}
): Promise<T> {
  const { maxAttempts = 3, delayMs = 1000, onRetry } = options

  let lastError: unknown

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      return await fn()
    } catch (error) {
      lastError = error

      if (attempt < maxAttempts) {
        if (onRetry) {
          onRetry(attempt, error)
        }
        await delay(delayMs * attempt)
      }
    }
  }

  throw lastError
}

/**
 * 安全的 JSON 解析
 */
export function safeJsonParse<T>(json: string, defaultValue: T): T {
  try {
    return JSON.parse(json)
  } catch {
    return defaultValue
  }
}

/**
 * 安全的 JSON 字符串化
 */
export function safeJsonStringify(obj: unknown, defaultValue = '{}'): string {
  try {
    return JSON.stringify(obj)
  } catch {
    return defaultValue
  }
}

/**
 * 截断文本
 */
export function truncate(text: string, maxLength: number, suffix = '...'): string {
  if (text.length <= maxLength) {
    return text
  }
  return text.slice(0, maxLength - suffix.length) + suffix
}

/**
 * 首字母大写
 */
export function capitalize(text: string): string {
  if (!text) return ''
  return text.charAt(0).toUpperCase() + text.slice(1)
}

/**
 * 驼峰转横线
 */
export function camelToKebab(text: string): string {
  return text.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()
}

/**
 * 横线转驼峰
 */
export function kebabToCamel(text: string): string {
  return text.replace(/-([a-z])/g, (_, letter) => letter.toUpperCase())
}

/**
 * 数组去重
 */
export function unique<T>(array: T[]): T[] {
  return Array.from(new Set(array))
}

/**
 * 数组分组
 */
export function groupBy<T>(array: T[], key: keyof T): Record<string, T[]> {
  return array.reduce((result, item) => {
    const groupKey = String(item[key])
    if (!result[groupKey]) {
      result[groupKey] = []
    }
    result[groupKey].push(item)
    return result
  }, {} as Record<string, T[]>)
}

/**
 * 数组排序
 */
export function sortBy<T>(array: T[], key: keyof T, order: 'asc' | 'desc' = 'asc'): T[] {
  return [...array].sort((a, b) => {
    const aVal = a[key]
    const bVal = b[key]

    if (aVal < bVal) return order === 'asc' ? -1 : 1
    if (aVal > bVal) return order === 'asc' ? 1 : -1
    return 0
  })
}

/**
 * 构建树形结构
 */
export function buildTree<T extends { id: string; parentId?: string | null }>(
  items: T[],
  parentId: string | null = null
): (T & { children?: T[] })[] {
  return items
    .filter((item) => item.parentId === parentId)
    .map((item) => ({
      ...item,
      children: buildTree(items, item.id),
    }))
}

/**
 * 扁平化树形结构
 */
export function flattenTree<T extends { children?: T[] }>(tree: T[]): T[] {
  const result: T[] = []

  function traverse(nodes: T[]) {
    for (const node of nodes) {
      const { children, ...rest } = node
      result.push(rest as T)
      if (children && children.length > 0) {
        traverse(children)
      }
    }
  }

  traverse(tree)
  return result
}

/**
 * 错误处理工具模块
 *
 * 提供统一的错误处理功能，包括：
 * - 检测和忽略请求取消错误
 * - 格式化错误消息
 * - 统一的错误处理和消息显示
 * - 成功消息显示
 * - 错误处理函数包装器
 *
 * @module errorHandler
 *
 * 使用示例：
 * ```typescript
 * import { isCancelError, handleError, handleSuccess } from '@/utils/errorHandler'
 *
 * // 在 composable 中使用
 * const loadData = async () => {
 *   loading.value = true
 *   try {
 *     const response = await api.getData()
 *     data.value = response.items
 *   } catch (error) {
 *     if (isCancelError(error)) {
 *       return // 忽略取消错误
 *     }
 *     handleError(error, '加载数据失败', { logPrefix: 'DataService' })
 *   } finally {
 *     loading.value = false
 *   }
 * }
 *
 * const createItem = async (formData) => {
 *   try {
 *     await api.create(formData)
 *     handleSuccess('创建成功')
 *     return true
 *   } catch (error) {
 *     handleError(error, '创建失败')
 *     return false
 *   }
 * }
 * ```
 */

import axios, { type AxiosError } from 'axios'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'
import { isRequestErrorDisplayed } from '@/utils/request'

/**
 * 检查错误是否是请求取消错误
 *
 * 请求取消错误不应该显示给用户，因为它们通常是由于用户操作（如快速切换页面）
 * 或者是应用程序主动取消的请求（如防抖、节流等场景）
 *
 * @param error - 要检查的错误对象
 * @returns 如果是取消错误返回 true，否则返回 false
 *
 * @example
 * ```typescript
 * try {
 *   await api.getData()
 * } catch (error) {
 *   if (isCancelError(error)) {
 *     return // 忽略取消错误
 *   }
 *   handleError(error)
 * }
 * ```
 */
export function isCancelError(error: unknown): boolean {
  if (!error) return false

  // 检查 axios 的取消错误
  if (axios.isCancel(error)) {
    return true
  }

  // 检查错误对象的 name 和 code 属性
  const err = error as { name?: string; code?: string }
  return err.name === 'CanceledError' || err.code === 'ERR_CANCELED'
}

/**
 * 服务端不可达（停止、断网、超时）时 Axios 没有 response。
 * 编辑器的本机草稿与自动重试只接管这一类失败，业务校验失败仍按原样提示。
 */
export function isNetworkError(error: unknown): boolean {
  return axios.isAxiosError(error) && !error.response && !isCancelError(error)
}

/**
 * 格式化错误消息
 *
 * 将各种类型的错误转换为用户友好的错误消息字符串
 *
 * @param error - 错误对象
 * @param defaultMessage - 默认错误消息（当无法从错误对象中提取消息时使用）
 * @returns 格式化后的错误消息字符串
 *
 * @example
 * ```typescript
 * const errorMsg = formatErrorMessage(error, '操作失败')
 * displayError(errorMsg)
 * ```
 */
export function formatErrorMessage(error: unknown, defaultMessage = t('Error:OperationFailed')): string {
  if (!error) {
    return defaultMessage
  }

  // 如果是 AxiosError
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<{
      error?: { code?: string | null; message?: string }
      message?: string
    }>

    // 尝试从响应中获取错误消息
    if (axiosError.response?.data) {
      const data = axiosError.response.data

      // ABP 框架的错误格式
      if (data.error?.message) {
        return data.error.message
      }
      if (data.error?.code) {
        return data.error.code
      }

      // 通用错误格式
      if (data.message) {
        return data.message
      }
    }

    // 使用 axios 错误消息
    if (axiosError.message) {
      return axiosError.message
    }
  }

  // 如果是 Error 对象
  if (error instanceof Error) {
    return error.message || defaultMessage
  }

  // 如果是字符串
  if (typeof error === 'string') {
    return error
  }

  // 如果是对象，尝试提取 message 属性
  if (typeof error === 'object') {
    const err = error as { message?: string }
    if (err.message) {
      return err.message
    }
  }

  return defaultMessage
}

/**
 * 处理错误并显示错误消息
 *
 * 统一的错误处理函数，会自动忽略取消错误，并显示格式化的错误消息
 *
 * @param error - 错误对象
 * @param defaultMessage - 默认错误消息
 * @param options - 配置选项
 * @param options.showMessage - 是否显示错误消息（默认为 true）
 * @param options.logError - 是否在控制台记录错误（默认为 true）
 * @param options.logPrefix - 控制台日志前缀
 *
 * @example
 * ```typescript
 * try {
 *   await api.deleteUser(id)
 * } catch (error) {
 *   handleError(error, '删除用户失败', { logPrefix: 'UserService' })
 * }
 * ```
 */
export function handleError(
  error: unknown,
  defaultMessage = t('Error:OperationFailed'),
  options: {
    showMessage?: boolean
    logError?: boolean
    logPrefix?: string
  } = {},
): void {
  const { showMessage = true, logError = true, logPrefix = '' } = options

  // 忽略取消错误
  if (isCancelError(error)) {
    return
  }

  // 请求拦截器已展示的 HTTP/网络错误不再重复提示
  const requestErrorDisplayed = isRequestErrorDisplayed(error)

  // 记录错误到控制台
  if (logError) {
    const prefix = logPrefix ? `[${logPrefix}] ` : ''
    console.error(`${prefix}Error:`, error)
  }

  // 显示错误消息
  if (showMessage && !requestErrorDisplayed) {
    const errorMessage = formatErrorMessage(error, defaultMessage)
    try {
      useDiscreteApi.message.error(errorMessage)
    } catch (e) {
      // 如果显示消息失败，至少记录到控制台
      console.error('Failed to show error message:', e)
    }
  }
}

/**
 * 处理成功消息
 *
 * 统一的成功消息显示函数
 *
 * @param message - 成功消息
 *
 * @example
 * ```typescript
 * await api.createUser(data)
 * handleSuccess('用户创建成功')
 * ```
 */
export function handleSuccess(message: string): void {
  try {
    useDiscreteApi.message.success(message)
  } catch (e) {
    console.error('Failed to show success message:', e)
  }
}

/**
 * 创建带错误处理的异步函数包装器
 *
 * 将异步函数包装为自动处理错误的版本
 *
 * @param fn - 要包装的异步函数
 * @param errorMessage - 错误消息
 * @param options - 配置选项
 * @returns 包装后的函数
 *
 * @example
 * ```typescript
 * const safeDeleteUser = withErrorHandler(
 *   (id: string) => api.deleteUser(id),
 *   '删除用户失败'
 * )
 *
 * await safeDeleteUser(userId)
 * ```
 */
export function withErrorHandler<TArgs extends unknown[], TResult>(
  fn: (...args: TArgs) => Promise<TResult>,
  errorMessage: string,
  options?: {
    showMessage?: boolean
    logError?: boolean
    logPrefix?: string
  },
): (...args: TArgs) => Promise<TResult> {
  return async (...args: TArgs) => {
    try {
      return await fn(...args)
    } catch (error) {
      handleError(error, errorMessage, options)
      throw error
    }
  }
}

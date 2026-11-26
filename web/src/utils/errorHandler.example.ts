/* eslint-disable @typescript-eslint/no-explicit-any */
/* eslint-disable @typescript-eslint/no-unused-vars */
/**
 * 错误处理工具使用示例
 *
 * 本文件展示了如何在实际项目中使用 errorHandler 工具函数
 * 这些示例可以作为重构现有代码的参考
 */

import { ref } from 'vue'
import { isCancelError, handleError, handleSuccess, formatErrorMessage, withErrorHandler } from './errorHandler'

// ============================================
// 示例 1: 在 composable 中使用基本错误处理
// ============================================

export function useDataLoadingExample() {
  const loading = ref(false)
  const data = ref<any[]>([])

  const loadData = async () => {
    loading.value = true
    try {
      // 模拟 API 调用
      const response = await fetch('/api/data')
      data.value = await response.json()
    } catch (error) {
      // 使用 isCancelError 检查是否是取消错误
      if (isCancelError(error)) {
        return // 忽略取消错误，不显示消息
      }

      // 使用 handleError 统一处理错误
      handleError(error, '加载数据失败', {
        logPrefix: 'DataService',
        showMessage: true,
        logError: true,
      })
    } finally {
      loading.value = false
    }
  }

  return { loading, data, loadData }
}

// ============================================
// 示例 2: 在 CRUD 操作中使用
// ============================================

export function useCrudExample() {
  const submitting = ref(false)

  const createItem = async (formData: any) => {
    submitting.value = true
    try {
      // 模拟 API 调用
      await fetch('/api/items', {
        method: 'POST',
        body: JSON.stringify(formData),
      })

      // 使用 handleSuccess 显示成功消息
      handleSuccess('创建成功')
      return true
    } catch (error) {
      // 使用 handleError 处理错误
      handleError(error, '创建失败')
      return false
    } finally {
      submitting.value = false
    }
  }

  const updateItem = async (id: string, formData: any) => {
    submitting.value = true
    try {
      await fetch(`/api/items/${id}`, {
        method: 'PUT',
        body: JSON.stringify(formData),
      })
      handleSuccess('更新成功')
      return true
    } catch (error) {
      handleError(error, '更新失败')
      return false
    } finally {
      submitting.value = false
    }
  }

  const deleteItem = async (id: string) => {
    try {
      await fetch(`/api/items/${id}`, { method: 'DELETE' })
      handleSuccess('删除成功')
    } catch (error) {
      handleError(error, '删除失败')
    }
  }

  return { submitting, createItem, updateItem, deleteItem }
}

// ============================================
// 示例 3: 使用 formatErrorMessage 自定义错误处理
// ============================================

export function useCustomErrorHandlingExample() {
  const processData = async () => {
    try {
      // 模拟 API 调用
      await fetch('/api/process')
    } catch (error) {
      if (isCancelError(error)) {
        return
      }

      // 获取格式化的错误消息
      const errorMessage = formatErrorMessage(error, '处理数据失败')

      // 自定义错误处理逻辑
      console.error('Processing error:', errorMessage)

      // 可以根据错误消息做不同的处理
      if (errorMessage.includes('权限')) {
        // 处理权限错误
      } else {
        // 显示通用错误
        handleError(error, '处理数据失败')
      }
    }
  }

  return { processData }
}

// ============================================
// 示例 4: 使用 withErrorHandler 包装函数
// ============================================

// 原始 API 函数
async function apiDeleteUser(id: string): Promise<void> {
  await fetch(`/api/users/${id}`, { method: 'DELETE' })
}

async function apiCreateUser(data: any): Promise<any> {
  const response = await fetch('/api/users', {
    method: 'POST',
    body: JSON.stringify(data),
  })
  return response.json()
}

// 使用 withErrorHandler 包装，自动处理错误
export const safeDeleteUser = withErrorHandler(
  apiDeleteUser,
  '删除用户失败',
  { logPrefix: 'UserService' }
)

export const safeCreateUser = withErrorHandler(
  apiCreateUser,
  '创建用户失败',
  { logPrefix: 'UserService' }
)

// 使用包装后的函数
export function useWrappedApiExample() {
  const deleteUser = async (id: string) => {
    try {
      await safeDeleteUser(id)
      handleSuccess('用户删除成功')
    } catch (error) {
      // 错误已经被 withErrorHandler 处理过了
      // 这里只需要处理业务逻辑
    }
  }

  return { deleteUser }
}

// ============================================
// 示例 5: 重构现有代码的对比
// ============================================

// 重构前的代码
export function useOldStyleExample() {
  const loading = ref(false)
  const data = ref<any[]>([])

  const loadData = async () => {
    loading.value = true
    try {
      const response = await fetch('/api/data')
      data.value = await response.json()
    } catch (error) {
      // 重复的错误检查逻辑
      if (!error) return
      const err = error as { name?: string; code?: string }
      if (err.name === 'CanceledError' || err.code === 'ERR_CANCELED') {
        return
      }

      // 重复的错误处理逻辑
      console.error('Failed to load data:', error)
      try {
        // 需要手动导入和使用 useDiscreteApi
        // useDiscreteApi.message.error('加载数据失败')
      } catch (e) {
        console.error('Failed to show error message:', e)
      }
    } finally {
      loading.value = false
    }
  }

  return { loading, data, loadData }
}

// 重构后的代码（使用 errorHandler 工具）
export function useNewStyleExample() {
  const loading = ref(false)
  const data = ref<any[]>([])

  const loadData = async () => {
    loading.value = true
    try {
      const response = await fetch('/api/data')
      data.value = await response.json()
    } catch (error) {
      // 简洁的错误处理
      if (isCancelError(error)) return
      handleError(error, '加载数据失败', { logPrefix: 'DataService' })
    } finally {
      loading.value = false
    }
  }

  return { loading, data, loadData }
}

// ============================================
// 使用建议
// ============================================

/**
 * 1. 在所有 API 调用中使用 isCancelError 检查取消错误
 * 2. 使用 handleError 替代手动的错误消息显示
 * 3. 使用 handleSuccess 统一成功消息的显示
 * 4. 对于需要自定义错误处理的场景，使用 formatErrorMessage
 * 5. 对于频繁调用的 API 函数，考虑使用 withErrorHandler 包装
 *
 * 重构优先级：
 * 1. 首先重构 composables 中的错误处理（useUserData, useRoleData 等）
 * 2. 然后重构 API 层的错误处理
 * 3. 最后考虑在组件中直接使用这些工具
 */

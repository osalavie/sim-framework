import { ref } from 'vue'
import { userApi } from '@/modules/authorization/api/user'
import { roleApi } from '@/modules/authorization/api/role'
import type { User, UserFormData } from '@/modules/authorization/api/user/userModels'
import type { Role } from '@/modules/authorization/api/role/roleModels'
import { useUserStore } from '@/stores/user'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { useInfiniteList } from '@/hooks/useInfiniteList'
import { useKeepAliveRefresh } from '@/hooks/useKeepAliveRefresh'
import { collectAllPages } from '@/utils/pagination'
import axios from 'axios'
import { t } from '@/i18n'

export interface SortInfo {
  field: string
  order: 'ascend' | 'descend'
}

// 检查是否是取消错误
const isCancelError = (error: unknown) => {
  if (!error) return false
  const err = error as { name?: string; code?: string }
  return axios.isCancel(error) || err.name === 'CanceledError' || err.code === 'ERR_CANCELED'
}

export function useUserData() {
  const loading = ref(false)
  const loadingMore = ref(false)
  const submitting = ref(false)
  const users = ref<User[]>([])
  const roles = ref<Role[]>([])
  const searchKeyword = ref('')
  const sortFields = ref<SortInfo[]>([])

  const pagination = ref({
    page: 1,
    pageSize: 10,
    pageCount: 1,
    itemCount: 0,
  })

  // 构建排序字符串（ABP 格式：字段名 asc/desc，多个排序用逗号分隔）
  const buildSortingString = () => {
    if (!sortFields.value || sortFields.value.length === 0) {
      return undefined
    }
    return sortFields.value
      .map(sort => {
        const direction = sort.order === 'ascend' ? 'asc' : 'desc'
        return `${sort.field} ${direction}`
      })
      .join(', ')
  }

  // 加载可分配角色
  const loadRoles = async () => {
    if (!useUserStore().hasPermission('system:role:manage')) {
      roles.value = []
      return
    }

    try {
      roles.value = await collectAllPages((skipCount, maxResultCount) =>
        roleApi.getList({ skipCount, maxResultCount }),
      )
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load roles:', error)
      roles.value = []
    }
  }

  // 加载用户列表
  const loadUsers = async () => {
    loading.value = true
    try {
      const response = await userApi.getList({
        skipCount: (pagination.value.page - 1) * pagination.value.pageSize,
        maxResultCount: pagination.value.pageSize,
        keyword: searchKeyword.value,
        sorting: buildSortingString(),
      })
      users.value = response.items || []
      pagination.value.pageCount = Math.ceil((response.totalCount || 0) / pagination.value.pageSize)
      pagination.value.itemCount = response.totalCount || 0
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load users:', error)
      users.value = []
      try {
        useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:User') }))
      } catch (e) {
        console.error('Failed to show error message:', e)
      }
    } finally {
      loading.value = false
    }
  }

  const { hasMore, loadMore } = useInfiniteList<User>({
    items: users,
    loading,
    loadingMore,
    pagination,
    fetchPage: (skipCount, maxResultCount) =>
      userApi.getList({
        skipCount,
        maxResultCount,
        keyword: searchKeyword.value,
        sorting: buildSortingString(),
      }),
    onError: (error) => {
      if (isCancelError(error)) return
      console.error('Failed to load more users:', error)
      useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:User') }))
    },
  })

  // 搜索
  const handleSearch = () => {
    pagination.value.page = 1
    loadUsers()
  }

  // 分页
  const handlePageChange = (page: number) => {
    pagination.value.page = page
    loadUsers()
  }

  // 分页大小变化
  const handlePageSizeChange = (pageSize: number) => {
    pagination.value.pageSize = pageSize
    pagination.value.page = 1
    loadUsers()
  }

  // 排序变化 - 支持多列排序
  const handleSortChange = (sorts: SortInfo[]) => {
    sortFields.value = sorts
    pagination.value.page = 1
    loadUsers()
  }

  // 创建用户
  const createUser = async (data: UserFormData) => {
    submitting.value = true
    try {
      await userApi.create(data)
      useDiscreteApi.message.success(t('Authz:UserCreatedWithDefaultPassword'))
      await loadUsers()
      return true
    } catch (error) {
      console.error('Failed to create user:', error)
      return false
    } finally {
      submitting.value = false
    }
  }

  // 更新用户
  const updateUser = async (id: string, data: UserFormData) => {
    submitting.value = true
    try {
      await userApi.update(id, data)
      useDiscreteApi.message.success(t('Feedback:UpdateSuccess', { entity: t('Entity:User') }))
      await loadUsers()
      return true
    } catch (error) {
      console.error('Failed to update user:', error)
      return false
    } finally {
      submitting.value = false
    }
  }

  // 重置密码为系统配置的默认密码
  const resetUserPassword = async (id: string) => {
    try {
      await userApi.resetUserPassword(id)
      useDiscreteApi.message.success(t('Authz:ResetPasswordSuccess'))
      return true
    } catch (error) {
      console.error('Failed to reset user password:', error)
      return false
    }
  }

  // 删除用户
  const deleteUser = async (id: string) => {
    try {
      await userApi.delete(id)
      useDiscreteApi.message.success(t('Feedback:DeleteSuccess', { entity: t('Entity:User') }))
      await loadUsers()
    } catch (error) {
      console.error('Failed to delete user:', error)
    }
  }

  const deleteUsers = async (ids: string[]) => {
    let successCount = 0

    for (const id of ids) {
      try {
        await userApi.delete(id)
        successCount += 1
      } catch (error) {
        console.error('Failed to delete user:', id, error)
      }
    }

    if (successCount === ids.length) {
      useDiscreteApi.message.success(t('Feedback:BatchDeleteSuccess', { count: successCount, entity: t('Entity:User') }))
    } else if (successCount > 0) {
      useDiscreteApi.message.warning(
        t('Feedback:BatchDeletePartial', { success: successCount, total: ids.length, entity: t('Entity:User') }),
      )
    } else {
      useDiscreteApi.message.error(t('Feedback:BatchDeleteFailed', { entity: t('Entity:User') }))
    }

    await loadUsers()
    return successCount === ids.length
  }

  useKeepAliveRefresh(async () => {
    await Promise.all([loadUsers(), loadRoles()])
  })

  return {
    // 状态
    loading,
    loadingMore,
    submitting,
    users,
    roles,
    searchKeyword,
    sortFields,
    pagination,
    hasMore,

    // 方法
    loadUsers,
    loadRoles,
    loadMore,
    handleSearch,
    handlePageChange,
    handlePageSizeChange,
    handleSortChange,
    createUser,
    updateUser,
    resetUserPassword,
    deleteUser,
    deleteUsers,
  }
}

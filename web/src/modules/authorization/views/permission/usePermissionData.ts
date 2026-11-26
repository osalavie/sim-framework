import { ref } from 'vue'
import { permissionApi } from '@/modules/authorization/api/permission'
import type { Permission, PermissionFormData } from '@/modules/authorization/api/permission/permissionModels'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { useInfiniteList } from '@/hooks/useInfiniteList'
import { useKeepAliveRefresh } from '@/hooks/useKeepAliveRefresh'
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

export function usePermissionData() {
  const loading = ref(false)
  const loadingMore = ref(false)
  const submitting = ref(false)
  const permissions = ref<Permission[]>([])
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

  // 加载权限列表
  const loadPermissions = async () => {
    loading.value = true
    try {
      const response = await permissionApi.getList({
        skipCount: (pagination.value.page - 1) * pagination.value.pageSize,
        maxResultCount: pagination.value.pageSize,
        keyword: searchKeyword.value,
        sorting: buildSortingString(),
      })
      permissions.value = response.items || []
      pagination.value.pageCount = Math.ceil((response.totalCount || 0) / pagination.value.pageSize)
      pagination.value.itemCount = response.totalCount || 0
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load permissions:', error)
      permissions.value = []
      try {
        useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Permission') }))
      } catch (e) {
        console.error('Failed to show error message:', e)
      }
    } finally {
      loading.value = false
    }
  }

  const { hasMore, loadMore } = useInfiniteList<Permission>({
    items: permissions,
    loading,
    loadingMore,
    pagination,
    fetchPage: (skipCount, maxResultCount) =>
      permissionApi.getList({
        skipCount,
        maxResultCount,
        keyword: searchKeyword.value,
        sorting: buildSortingString(),
      }),
    onError: (error) => {
      if (isCancelError(error)) return
      console.error('Failed to load more permissions:', error)
      useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Permission') }))
    },
  })

  // 搜索
  const handleSearch = () => {
    pagination.value.page = 1
    loadPermissions()
  }

  // 分页
  const handlePageChange = (page: number) => {
    pagination.value.page = page
    loadPermissions()
  }

  // 分页大小变化
  const handlePageSizeChange = (pageSize: number) => {
    pagination.value.pageSize = pageSize
    pagination.value.page = 1
    loadPermissions()
  }

  // 排序变化 - 支持多列排序
  const handleSortChange = (sorts: SortInfo[]) => {
    sortFields.value = sorts
    pagination.value.page = 1
    loadPermissions()
  }

  // 创建权限
  const createPermission = async (data: PermissionFormData) => {
    submitting.value = true
    try {
      await permissionApi.create(data)
      useDiscreteApi.message.success(t('Feedback:CreateSuccess', { entity: t('Entity:Permission') }))
      await loadPermissions()
      return true
    } catch (error) {
      console.error('Failed to create permission:', error)
      useDiscreteApi.message.error(t('Feedback:CreateFailed', { entity: t('Entity:Permission') }))
      return false
    } finally {
      submitting.value = false
    }
  }

  // 更新权限
  const updatePermission = async (id: string, data: PermissionFormData) => {
    submitting.value = true
    try {
      await permissionApi.update(id, data)
      useDiscreteApi.message.success(t('Feedback:UpdateSuccess', { entity: t('Entity:Permission') }))
      await loadPermissions()
      return true
    } catch (error) {
      console.error('Failed to update permission:', error)
      useDiscreteApi.message.error(t('Feedback:UpdateFailed', { entity: t('Entity:Permission') }))
      return false
    } finally {
      submitting.value = false
    }
  }

  // 删除权限
  const deletePermission = async (id: string) => {
    try {
      await permissionApi.delete(id)
      useDiscreteApi.message.success(t('Feedback:DeleteSuccess', { entity: t('Entity:Permission') }))
      await loadPermissions()
    } catch (error) {
      console.error('Failed to delete permission:', error)
      useDiscreteApi.message.error(t('Feedback:DeleteFailed', { entity: t('Entity:Permission') }))
    }
  }

  const deletePermissions = async (ids: string[]) => {
    let successCount = 0

    for (const id of ids) {
      try {
        await permissionApi.delete(id)
        successCount += 1
      } catch (error) {
        console.error('Failed to delete permission:', id, error)
      }
    }

    if (successCount === ids.length) {
      useDiscreteApi.message.success(t('Feedback:BatchDeleteSuccess', { count: successCount, entity: t('Entity:Permission') }))
    } else if (successCount > 0) {
      useDiscreteApi.message.warning(
        t('Feedback:BatchDeletePartial', { success: successCount, total: ids.length, entity: t('Entity:Permission') }),
      )
    } else {
      useDiscreteApi.message.error(t('Feedback:BatchDeleteFailed', { entity: t('Entity:Permission') }))
    }

    await loadPermissions()
    return successCount === ids.length
  }

  useKeepAliveRefresh(loadPermissions)

  return {
    // 状态
    loading,
    loadingMore,
    submitting,
    permissions,
    searchKeyword,
    sortFields,
    pagination,
    hasMore,

    // 方法
    loadPermissions,
    loadMore,
    handleSearch,
    handlePageChange,
    handlePageSizeChange,
    handleSortChange,
    createPermission,
    updatePermission,
    deletePermission,
    deletePermissions,
  }
}

import { ref } from 'vue'
import { roleApi } from '@/modules/authorization/api/role'
import { moduleApi } from '@/modules/authorization/api/module'
import { permissionApi } from '@/modules/authorization/api/permission'
import type { Role, RoleFormData } from '@/modules/authorization/api/role/roleModels'
import type { Module } from '@/modules/authorization/api/module/moduleModels'
import type { Permission } from '@/modules/authorization/api/permission/permissionModels'
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

export function useRoleData() {
  const loading = ref(false)
  const loadingMore = ref(false)
  const submitting = ref(false)
  const roles = ref<Role[]>([])
  const modules = ref<Module[]>([])
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

  // 加载模块列表（用于角色-模块关联）
  const loadModules = async () => {
    try {
      modules.value = await collectAllPages((skipCount, maxResultCount) =>
        moduleApi.getList({ skipCount, maxResultCount }),
      )
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load modules:', error)
      modules.value = []
    }
  }

  // 加载权限清单（用于角色-权限关联）
  const loadPermissions = async () => {
    try {
      permissions.value = await collectAllPages((skipCount, maxResultCount) =>
        permissionApi.getList({ skipCount, maxResultCount }),
      )
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load permissions:', error)
      permissions.value = []
    }
  }

  // 加载角色列表
  const loadRoles = async () => {
    loading.value = true
    try {
      const response = await roleApi.getList({
        skipCount: (pagination.value.page - 1) * pagination.value.pageSize,
        maxResultCount: pagination.value.pageSize,
        keyword: searchKeyword.value,
        sorting: buildSortingString(),
      })
      roles.value = response.items || []
      pagination.value.pageCount = Math.ceil((response.totalCount || 0) / pagination.value.pageSize)
      pagination.value.itemCount = response.totalCount || 0
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load roles:', error)
      roles.value = []
      try {
        useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Role') }))
      } catch (e) {
        console.error('Failed to show error message:', e)
      }
    } finally {
      loading.value = false
    }
  }

  const { hasMore, loadMore } = useInfiniteList<Role>({
    items: roles,
    loading,
    loadingMore,
    pagination,
    fetchPage: (skipCount, maxResultCount) =>
      roleApi.getList({
        skipCount,
        maxResultCount,
        keyword: searchKeyword.value,
        sorting: buildSortingString(),
      }),
    onError: (error) => {
      if (isCancelError(error)) return
      console.error('Failed to load more roles:', error)
      useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Role') }))
    },
  })

  // 搜索
  const handleSearch = () => {
    pagination.value.page = 1
    loadRoles()
  }

  // 分页
  const handlePageChange = (page: number) => {
    pagination.value.page = page
    loadRoles()
  }

  // 分页大小变化
  const handlePageSizeChange = (pageSize: number) => {
    pagination.value.pageSize = pageSize
    pagination.value.page = 1
    loadRoles()
  }

  // 排序变化 - 支持多列排序
  const handleSortChange = (sorts: SortInfo[]) => {
    sortFields.value = sorts
    pagination.value.page = 1
    loadRoles()
  }

  // 创建角色
  const createRole = async (data: RoleFormData) => {
    submitting.value = true
    try {
      const role = await roleApi.create(data)

      // 如果有模块分配，则分配模块
      if (data.moduleIds && data.moduleIds.length > 0) {
        await roleApi.assignModules(role.id, data.moduleIds)
      }

      if (data.permissionIds && data.permissionIds.length > 0) {
        await roleApi.assignPermissions(role.id, data.permissionIds)
      }

      useDiscreteApi.message.success(t('Feedback:CreateSuccess', { entity: t('Entity:Role') }))
      await loadRoles()
      return true
    } catch (error) {
      console.error('Failed to create role:', error)
      useDiscreteApi.message.error(t('Feedback:CreateFailed', { entity: t('Entity:Role') }))
      return false
    } finally {
      submitting.value = false
    }
  }

  // 更新角色
  const updateRole = async (id: string, data: RoleFormData) => {
    submitting.value = true
    try {
      await roleApi.update(id, data)

      // 如果有模块分配，则更新模块
      if (data.moduleIds !== undefined) {
        await roleApi.assignModules(id, data.moduleIds)
      }

      if (data.permissionIds !== undefined) {
        await roleApi.assignPermissions(id, data.permissionIds)
      }

      useDiscreteApi.message.success(t('Feedback:UpdateSuccess', { entity: t('Entity:Role') }))
      await loadRoles()
      return true
    } catch (error) {
      console.error('Failed to update role:', error)
      useDiscreteApi.message.error(t('Feedback:UpdateFailed', { entity: t('Entity:Role') }))
      return false
    } finally {
      submitting.value = false
    }
  }

  // 删除角色
  const deleteRole = async (id: string) => {
    try {
      await roleApi.delete(id)
      useDiscreteApi.message.success(t('Feedback:DeleteSuccess', { entity: t('Entity:Role') }))
      await loadRoles()
    } catch (error) {
      console.error('Failed to delete role:', error)
      useDiscreteApi.message.error(t('Feedback:DeleteFailed', { entity: t('Entity:Role') }))
    }
  }

  const deleteRoles = async (ids: string[]) => {
    let successCount = 0

    for (const id of ids) {
      try {
        await roleApi.delete(id)
        successCount += 1
      } catch (error) {
        console.error('Failed to delete role:', id, error)
      }
    }

    if (successCount === ids.length) {
      useDiscreteApi.message.success(t('Feedback:BatchDeleteSuccess', { count: successCount, entity: t('Entity:Role') }))
    } else if (successCount > 0) {
      useDiscreteApi.message.warning(
        t('Feedback:BatchDeletePartial', { success: successCount, total: ids.length, entity: t('Entity:Role') }),
      )
    } else {
      useDiscreteApi.message.error(t('Feedback:BatchDeleteFailed', { entity: t('Entity:Role') }))
    }

    await loadRoles()
    return successCount === ids.length
  }

  // 获取角色的模块列表；加载失败返回 undefined，避免编辑弹窗把空数组当作“清空”
  const getRoleModules = async (roleId: string): Promise<string[] | undefined> => {
    try {
      return await roleApi.getRoleModules(roleId)
    } catch (error) {
      console.error('Failed to get role modules:', error)
      return undefined
    }
  }

  // 获取角色的权限列表；加载失败返回 undefined，避免编辑弹窗把空数组当作“清空”
  const getRolePermissions = async (roleId: string): Promise<string[] | undefined> => {
    try {
      return await roleApi.getRolePermissions(roleId)
    } catch (error) {
      console.error('Failed to get role permissions:', error)
      return undefined
    }
  }

  useKeepAliveRefresh(
    async () => {
      await Promise.all([loadModules(), loadPermissions(), loadRoles()])
    },
    loadRoles,
  )

  return {
    // 状态
    loading,
    loadingMore,
    submitting,
    roles,
    modules,
    permissions,
    searchKeyword,
    sortFields,
    pagination,
    hasMore,

    // 方法
    loadRoles,
    loadMore,
    loadModules,
    loadPermissions,
    handleSearch,
    handlePageChange,
    handlePageSizeChange,
    handleSortChange,
    createRole,
    updateRole,
    deleteRole,
    deleteRoles,
    getRoleModules,
    getRolePermissions,
  }
}

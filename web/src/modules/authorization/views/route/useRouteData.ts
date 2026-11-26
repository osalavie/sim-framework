import { ref } from 'vue'
import { routeApi } from '@/modules/authorization/api/route'
import { moduleApi } from '@/modules/authorization/api/module'
import { permissionApi } from '@/modules/authorization/api/permission'
import type { Route, RouteFormData } from '@/modules/authorization/api/route/routeModels'
import type { Module } from '@/modules/authorization/api/module/moduleModels'
import type { Permission } from '@/modules/authorization/api/permission/permissionModels'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { useInfiniteList } from '@/hooks/useInfiniteList'
import { useKeepAliveRefresh } from '@/hooks/useKeepAliveRefresh'
import { collectAllPages } from '@/utils/pagination'
import { handleError } from '@/utils/errorHandler'
import axios from 'axios'
import { t } from '@/i18n'

// 检查是否是取消错误
const isCancelError = (error: unknown) => {
  if (!error) return false
  const err = error as { name?: string; code?: string }
  return axios.isCancel(error) || err.name === 'CanceledError' || err.code === 'ERR_CANCELED'
}

export function useRouteData() {
  const loading = ref(false)
  const loadingMore = ref(false)
  const submitting = ref(false)
  const treeRoutes = ref<Route[]>([])  // 树形结构的路由（后端返回）
  const statsRoutes = ref<Route[]>([]) // 全量路由树，仅用于顶部统计，不受分页影响
  const modules = ref<Module[]>([])
  const permissions = ref<Permission[]>([])
  const searchKeyword = ref('')
  const selectedModuleId = ref<string | undefined>(undefined)

  const pagination = ref({
    page: 1,
    pageSize: 10,
    pageCount: 1,
    itemCount: 0,
  })

  // 加载模块列表（用于列表筛选）
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

  // 加载权限清单（用于路由权限码下拉）
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

  // 加载路由列表（树形结构，后端分页）
  const loadRoutes = async () => {
    loading.value = true
    try {
      // 使用后端的分页树形结构API
      const response = await routeApi.getRoutesWithMetaPaged({
        skipCount: (pagination.value.page - 1) * pagination.value.pageSize,
        maxResultCount: pagination.value.pageSize,
        keyword: searchKeyword.value,
        moduleId: selectedModuleId.value,
      })

      treeRoutes.value = response.items || []
      pagination.value.pageCount = Math.ceil((response.totalCount || 0) / pagination.value.pageSize)
      pagination.value.itemCount = response.totalCount || 0
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load routes:', error)
      treeRoutes.value = []
      try {
        useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Route') }))
      } catch (e) {
        console.error('Failed to show error message:', e)
      }
    } finally {
      loading.value = false
    }
  }

  // 加载全量路由树用于统计：分页接口的 totalCount 只统计根路由，
  // 页面级统计无法反映系统整体配置情况。
  const loadStatsRoutes = async () => {
    try {
      statsRoutes.value = await routeApi.getRoutesWithMeta()
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load route statistics:', error)
    }
  }

  const refreshRoutes = async () => {
    await Promise.all([loadRoutes(), loadStatsRoutes()])
  }

  const { hasMore, loadMore } = useInfiniteList<Route>({
    items: treeRoutes,
    loading,
    loadingMore,
    pagination,
    fetchPage: (skipCount, maxResultCount) =>
      routeApi.getRoutesWithMetaPaged({
        skipCount,
        maxResultCount,
        keyword: searchKeyword.value,
        moduleId: selectedModuleId.value,
      }),
    onError: (error) => {
      if (isCancelError(error)) return
      console.error('Failed to load more routes:', error)
      useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Route') }))
    },
  })

  // 搜索（重新加载并过滤）
  const handleSearch = () => {
    pagination.value.page = 1
    loadRoutes()
  }

  // 模块过滤（重新加载并过滤）
  const handleModuleFilter = (moduleId: string | undefined) => {
    selectedModuleId.value = moduleId
    pagination.value.page = 1
    loadRoutes()
  }

  // 分页
  const handlePageChange = (page: number) => {
    pagination.value.page = page
    loadRoutes()
  }

  // 分页大小变化
  const handlePageSizeChange = (pageSize: number) => {
    pagination.value.pageSize = pageSize
    pagination.value.page = 1
    loadRoutes()
  }

  // 创建路由
  const createRoute = async (data: RouteFormData) => {
    submitting.value = true
    try {
      await routeApi.create(data)
      useDiscreteApi.message.success(t('Feedback:CreateSuccess', { entity: t('Entity:Route') }))
      await refreshRoutes()
      return true
    } catch (error) {
      handleError(error, t('Feedback:CreateFailed', { entity: t('Entity:Route') }), {
        logPrefix: 'Route',
      })
      return false
    } finally {
      submitting.value = false
    }
  }

  // 更新路由
  const updateRoute = async (id: string, data: RouteFormData) => {
    submitting.value = true
    try {
      await routeApi.update(id, data)
      useDiscreteApi.message.success(t('Feedback:UpdateSuccess', { entity: t('Entity:Route') }))
      await refreshRoutes()
      return true
    } catch (error) {
      handleError(error, t('Feedback:UpdateFailed', { entity: t('Entity:Route') }), {
        logPrefix: 'Route',
      })
      return false
    } finally {
      submitting.value = false
    }
  }

  // 删除路由
  const deleteRoute = async (id: string) => {
    try {
      await routeApi.delete(id)
      useDiscreteApi.message.success(t('Feedback:DeleteSuccess', { entity: t('Entity:Route') }))
      await refreshRoutes()
    } catch (error) {
      handleError(error, t('Feedback:DeleteFailed', { entity: t('Entity:Route') }), {
        logPrefix: 'Route',
      })
    }
  }

  const deleteRoutes = async (ids: string[]) => {
    // 后端单条删除为级联删除：删除父路由会连带删除全部子孙路由。
    // 因此只保留顶层选中记录（祖先不在删除列表中的记录），避免子孙已被
    // 祖先的级联删除移除后再单独删除而抛出"路由不存在"错误。
    const idSet = new Set(ids)
    const rootIds: string[] = []
    const collectRootIds = (routes: Route[]) => {
      for (const route of routes) {
        if (idSet.has(route.id)) {
          rootIds.push(route.id)
          // 子孙路由会随本记录级联删除，无需继续检查
        } else if (route.children?.length) {
          collectRootIds(route.children)
        }
      }
    }
    collectRootIds(treeRoutes.value)

    let successCount = 0

    for (const id of rootIds) {
      try {
        await routeApi.delete(id)
        successCount += 1
      } catch (error) {
        console.error('Failed to delete route:', id, error)
      }
    }

    if (successCount === rootIds.length && rootIds.length > 0) {
      useDiscreteApi.message.success(t('Feedback:BatchDeleteSuccess', { count: successCount, entity: t('Entity:Route') }))
    } else if (successCount > 0) {
      useDiscreteApi.message.warning(
        t('Feedback:BatchDeletePartial', { success: successCount, total: rootIds.length, entity: t('Entity:Route') }),
      )
    } else {
      useDiscreteApi.message.error(t('Feedback:BatchDeleteFailed', { entity: t('Entity:Route') }))
    }

    await refreshRoutes()
    return successCount === rootIds.length && rootIds.length > 0
  }

  useKeepAliveRefresh(
    async () => {
      await Promise.all([loadModules(), loadPermissions(), refreshRoutes()])
    },
    refreshRoutes,
  )

  return {
    // 状态
    loading,
    loadingMore,
    submitting,
    treeRoutes,
    statsRoutes,
    modules,
    permissions,
    searchKeyword,
    selectedModuleId,
    pagination,
    hasMore,

    // 方法
    loadRoutes,
    loadMore,
    loadModules,
    loadPermissions,
    handleSearch,
    handleModuleFilter,
    handlePageChange,
    handlePageSizeChange,
    createRoute,
    updateRoute,
    deleteRoute,
    deleteRoutes,
  }
}

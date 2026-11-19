import { computed, ref } from 'vue'
import { moduleApi } from '@/modules/authorization/api/module'
import { routeApi } from '@/modules/authorization/api/route'
import type { Module, ModuleFormData } from '@/modules/authorization/api/module/moduleModels'
import type { Route } from '@/modules/authorization/api/route/routeModels'
import { useDiscreteApi } from '@/hooks/discreteApi'
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

export function useModuleData() {
  const loading = ref(false)
  const loadingMore = ref(false)
  const submitting = ref(false)
  const modules = ref<Module[]>([])
  const routes = ref<Route[]>([])
  const statsModules = ref<Module[]>([]) // 全量模块，仅用于顶部启用/停用统计
  const searchKeyword = ref('')
  const sortFields = ref<SortInfo[]>([])

  const pagination = ref({
    page: 1,
    pageSize: 10,
    pageCount: 1,
    itemCount: 0,
  })

  const hasMore = computed(() => modules.value.length < pagination.value.itemCount)

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

  // 加载模块列表
  const loadModules = async () => {
    loading.value = true
    try {
      const response = await moduleApi.getList({
        skipCount: (pagination.value.page - 1) * pagination.value.pageSize,
        maxResultCount: pagination.value.pageSize,
        keyword: searchKeyword.value,
        sorting: buildSortingString(),
      })
      modules.value = response.items || []
      pagination.value.pageCount = Math.ceil((response.totalCount || 0) / pagination.value.pageSize)
      pagination.value.itemCount = response.totalCount || 0
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load modules:', error)
      modules.value = []
      try {
        useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Module') }))
      } catch (e) {
        console.error('Failed to show error message:', e)
      }
    } finally {
      loading.value = false
    }
  }

  // 启用/停用统计需要全量模块，分页结果只能反映当前页
  const loadModuleStats = async () => {
    try {
      statsModules.value = await collectAllPages((skipCount, maxResultCount) =>
        moduleApi.getList({ skipCount, maxResultCount }),
      )
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load module statistics:', error)
    }
  }

  // 模块编辑的关联路由树
  const loadRoutes = async () => {
    try {
      routes.value = await routeApi.getRoutesWithMeta()
    } catch (error) {
      if (isCancelError(error)) {
        return
      }
      console.error('Failed to load routes:', error)
      routes.value = []
    }
  }

  const refreshModules = async () => {
    await Promise.all([loadModules(), loadModuleStats(), loadRoutes()])
  }

  // 卡片模式无限滚动：按当前已加载数量继续取下一页并追加
  const loadMore = async () => {
    if (loading.value || loadingMore.value || !hasMore.value) return
    loadingMore.value = true
    try {
      const response = await moduleApi.getList({
        skipCount: modules.value.length,
        maxResultCount: pagination.value.pageSize,
        keyword: searchKeyword.value,
        sorting: buildSortingString(),
      })
      const items = response.items || []
      modules.value = [...modules.value, ...items]
      pagination.value.itemCount = response.totalCount || 0
      pagination.value.pageCount = Math.ceil(
        (response.totalCount || 0) / pagination.value.pageSize,
      )
      pagination.value.page = Math.max(
        1,
        Math.ceil(modules.value.length / pagination.value.pageSize),
      )
    } catch (error) {
      if (!isCancelError(error)) {
        console.error('Failed to load more modules:', error)
        useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Module') }))
      }
    } finally {
      loadingMore.value = false
    }
  }

  // 搜索
  const handleSearch = () => {
    pagination.value.page = 1
    loadModules()
  }

  // 分页
  const handlePageChange = (page: number) => {
    pagination.value.page = page
    loadModules()
  }

  // 分页大小变化
  const handlePageSizeChange = (pageSize: number) => {
    pagination.value.pageSize = pageSize
    pagination.value.page = 1
    loadModules()
  }

  // 排序变化 - 支持多列排序
  const handleSortChange = (sorts: SortInfo[]) => {
    sortFields.value = sorts
    pagination.value.page = 1
    loadModules()
  }

  // 创建模块
  const createModule = async (data: ModuleFormData) => {
    submitting.value = true
    try {
      await moduleApi.create(data)
      useDiscreteApi.message.success(t('Feedback:CreateSuccess', { entity: t('Entity:Module') }))
      await refreshModules()
      return true
    } catch (error) {
      console.error('Failed to create module:', error)
      useDiscreteApi.message.error(t('Feedback:CreateFailed', { entity: t('Entity:Module') }))
      return false
    } finally {
      submitting.value = false
    }
  }

  // 更新模块
  const updateModule = async (id: string, data: ModuleFormData) => {
    submitting.value = true
    try {
      await moduleApi.update(id, data)
      useDiscreteApi.message.success(t('Feedback:UpdateSuccess', { entity: t('Entity:Module') }))
      await refreshModules()
      return true
    } catch (error) {
      console.error('Failed to update module:', error)
      useDiscreteApi.message.error(t('Feedback:UpdateFailed', { entity: t('Entity:Module') }))
      return false
    } finally {
      submitting.value = false
    }
  }

  // 删除模块
  const deleteModule = async (id: string) => {
    try {
      await moduleApi.delete(id)
      useDiscreteApi.message.success(t('Feedback:DeleteSuccess', { entity: t('Entity:Module') }))
      await refreshModules()
    } catch (error) {
      console.error('Failed to delete module:', error)
      useDiscreteApi.message.error(t('Feedback:DeleteFailed', { entity: t('Entity:Module') }))
    }
  }

  const deleteModules = async (ids: string[]) => {
    let successCount = 0

    for (const id of ids) {
      try {
        await moduleApi.delete(id)
        successCount += 1
      } catch (error) {
        console.error('Failed to delete module:', id, error)
      }
    }

    if (successCount === ids.length) {
      useDiscreteApi.message.success(t('Feedback:BatchDeleteSuccess', { count: successCount, entity: t('Entity:Module') }))
    } else if (successCount > 0) {
      useDiscreteApi.message.warning(
        t('Feedback:BatchDeletePartial', { success: successCount, total: ids.length, entity: t('Entity:Module') }),
      )
    } else {
      useDiscreteApi.message.error(t('Feedback:BatchDeleteFailed', { entity: t('Entity:Module') }))
    }

    await refreshModules()
    return successCount === ids.length
  }

  // 检查模块是否有关联路由（权限与模块没有数据关联）
  const checkModuleAssociations = async (moduleId: string): Promise<{ hasRoutes: boolean; routeCount: number }> => {
    try {
      const routes = await moduleApi.getModuleRoutes(moduleId)
      return {
        hasRoutes: (routes?.length || 0) > 0,
        routeCount: routes?.length || 0,
      }
    } catch (error) {
      console.error('Failed to check module associations:', error)
      return {
        hasRoutes: false,
        routeCount: 0,
      }
    }
  }

  // 获取模块的路由列表
  const getModuleRoutes = async (moduleId: string) => {
    try {
      return await moduleApi.getModuleRoutes(moduleId)
    } catch (error) {
      console.error('Failed to get module routes:', error)
      return []
    }
  }

  useKeepAliveRefresh(refreshModules)

  return {
    // 状态
    loading,
    loadingMore,
    submitting,
    modules,
    routes,
    statsModules,
    searchKeyword,
    sortFields,
    pagination,
    hasMore,

    // 方法
    loadModules,
    loadRoutes,
    loadMore,
    handleSearch,
    handlePageChange,
    handlePageSizeChange,
    handleSortChange,
    createModule,
    updateModule,
    deleteModule,
    deleteModules,
    checkModuleAssociations,
    getModuleRoutes,
  }
}

import {
  getSystemModuleConfig,
  getSystemModuleConfigId,
  postSystemModuleConfig,
  putSystemModuleConfigId,
  deleteSystemModuleConfigId,
  getSystemModuleConfigUserModules,
  getSystemRouteConfigModuleRoutesModuleId,
} from '@/api/system'
import type { Module, ModuleFormData, ModuleQueryParams } from './moduleModels'
import type { PageData } from '@/types/api'
import type { Route } from '../route/routeModels'

// 导出类型定义
export * from './moduleModels'

/**
 * 模块 API
 * 遵循 userApi、roleApi 和 permissionApi 的模式，提供完整的 CRUD 操作
 */
export const moduleApi = {
  /**
   * 获取模块列表（分页）
   * GET /api/system/module-config
   * 支持关键词搜索、排序、分页和按启用状态过滤
   */
  getList: (params?: ModuleQueryParams) => {
    return getSystemModuleConfig({ query: params as never, throwOnError: true }).then(
      (res) => res.data as PageData<Module>,
    )
  },

  /**
   * 根据 ID 获取模块详情
   * GET /api/system/module-config/{id}
   */
  get: (id: string) => {
    return getSystemModuleConfigId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as Module,
    )
  },

  /**
   * 创建模块
   * POST /api/system/module-config
   */
  create: (data: ModuleFormData) => {
    return postSystemModuleConfig({ body: data as never, throwOnError: true }).then(
      (res) => res.data as Module,
    )
  },

  /**
   * 更新模块
   * PUT /api/system/module-config/{id}
   */
  update: (id: string, data: ModuleFormData) => {
    return putSystemModuleConfigId({
      path: { id },
      body: data as never,
      throwOnError: true,
    }).then((res) => res.data as Module)
  },

  /**
   * 删除模块
   * DELETE /api/system/module-config/{id}
   */
  delete: (id: string) => {
    return deleteSystemModuleConfigId({ path: { id }, throwOnError: true }).then((res) => res.data)
  },

  /**
   * 获取当前用户可访问的模块列表
   * GET /api/system/module-config/user-modules
   */
  getUserModules: () => {
    return getSystemModuleConfigUserModules({ throwOnError: true }).then(
      (res) => res.data as Module[],
    )
  },

  /**
   * 获取模块的路由列表
   * GET /api/system/route-config/module-routes/{moduleId}
   */
  getModuleRoutes: (moduleId: string) => {
    return getSystemRouteConfigModuleRoutesModuleId({
      path: { moduleId },
      throwOnError: true,
    }).then((res) => res.data as Route[])
  },
}

// 保持向后兼容的别名
export const getModuleList = moduleApi.getList
export const getModule = moduleApi.get
export const createModule = moduleApi.create
export const updateModule = moduleApi.update
export const deleteModule = moduleApi.delete

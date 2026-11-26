import {
  getSystemRouteConfig,
  getSystemRouteConfigId,
  postSystemRouteConfig,
  putSystemRouteConfig,
  deleteSystemRouteConfigId,
  getSystemRouteConfigModuleRoutesModuleId,
  getSystemRouteConfigRoutesWithMetaPaged,
  getSystemRouteConfigRoutesWithMeta,
} from '@/api/system'
import type { Route, RouteFormData, RouteQueryParams } from './routeModels'
import type { PageData } from '@/types/api'

// 导出类型定义
export * from './routeModels'

/**
 * 路由 API
 * 统一通过 SDK 调用，类型引用生成 Schema（见 routeModels.ts）
 */
export const routeApi = {
  /**
   * 获取路由列表（分页）
   * GET /api/system/route-config
   * 支持关键词搜索、排序、分页和按模块过滤
   */
  getList: (params?: RouteQueryParams) => {
    return getSystemRouteConfig({ query: params as never, throwOnError: true }).then(
      (res) => res.data as PageData<Route>,
    )
  },

  /**
   * 根据 ID 获取路由详情
   * GET /api/system/route-config/{id}
   */
  get: (id: string) => {
    return getSystemRouteConfigId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as Route,
    )
  },

  /**
   * 创建路由
   * POST /api/system/route-config
   */
  create: (data: RouteFormData) => {
    return postSystemRouteConfig({ body: data as never, throwOnError: true }).then(
      (res) => res.data as Route,
    )
  },

  /**
   * 更新路由 (ABP的Update方法需要在body中包含id)
   * PUT /api/system/route-config
   */
  update: (id: string, data: RouteFormData) => {
    return putSystemRouteConfig({ body: data as never, throwOnError: true }).then(
      (res) => res.data as Route,
    )
  },

  /**
   * 删除路由
   * DELETE /api/system/route-config/{id}
   */
  delete: (id: string) => {
    return deleteSystemRouteConfigId({ path: { id }, throwOnError: true }).then((res) => res.data)
  },

  /**
   * 获取模块的路由列表（树形结构）
   * GET /api/system/route-config/module-routes/{moduleId}
   */
  getModuleRoutes: (moduleId: string) => {
    return getSystemRouteConfigModuleRoutesModuleId({
      path: { moduleId },
      throwOnError: true,
    }).then((res) => res.data as Route[])
  },

  /**
   * 获取路由列表（分页，树形结构）
   * GET /api/system/route-config/routes-with-meta-paged
   * 分页只针对根路由，子路由会自动包含
   */
  getRoutesWithMetaPaged: (params?: RouteQueryParams) => {
    return getSystemRouteConfigRoutesWithMetaPaged({
      query: params as never,
      throwOnError: true,
    }).then((res) => res.data as PageData<Route>)
  },

  /**
   * 获取用户可访问的所有路由（无分页，树形结构）
   * GET /api/system/route-config/routes-with-meta
   * 返回完整的树形结构
   */
  getRoutesWithMeta: () => {
    return getSystemRouteConfigRoutesWithMeta({ throwOnError: true }).then(
      (res) => res.data as Route[],
    )
  },
}

// 保持向后兼容的别名
export const getRouteList = routeApi.getList
export const getRoute = routeApi.get
export const createRoute = routeApi.create
export const updateRoute = routeApi.update
export const deleteRoute = routeApi.delete

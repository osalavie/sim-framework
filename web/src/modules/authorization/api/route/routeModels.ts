import type {
  RouteConfigDto as RouteConfigDtoSchema,
  CreateRouteConfigDto as CreateRouteConfigDtoSchema,
  UpdateRouteConfigDto as UpdateRouteConfigDtoSchema,
  RouteMetaDto as RouteMetaDtoSchema,
} from '@/api/system'

/**
 * 路由模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅表单/视图模型（RouteFormData、RouteQueryParams）保留前端自有类型。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/** 后端路由配置 DTO */
export type RouteConfigDto = RouteConfigDtoSchema

/** 创建路由请求 DTO */
export type CreateRouteConfigDto = CreateRouteConfigDtoSchema

/** 更新路由请求 DTO */
export type UpdateRouteConfigDto = UpdateRouteConfigDtoSchema

/** 路由元数据（生成 Schema） */
export type RouteMeta = RouteMetaDtoSchema

// ===== 视图模型 =====

/**
 * 路由信息（树形表格/表单使用）
 * 字段继承生成 Schema，并对前端必用的主键/标识字段做必填断言（后端实际总是返回）
 */
export type Route = Omit<RouteConfigDto, 'children'> & {
  id: string
  moduleIds: string[]
  name: string
  path: string
  children?: Route[]
}

/**
 * 路由表单数据（用于创建和更新）
 * 表单视图模型：component 允许留空、parentId 允许清空为 null
 */
export interface RouteFormData {
  id?: string
  parentId?: string | null
  name: string
  path: string
  component?: string
  redirect?: string
  meta?: RouteMeta
}

/**
 * 路由查询参数
 */
export interface RouteQueryParams {
  skipCount?: number
  maxResultCount?: number
  keyword?: string
  sorting?: string
  moduleId?: string // 按模块过滤
}

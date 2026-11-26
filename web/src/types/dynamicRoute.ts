/**
 * 动态路由相关类型定义
 *
 * 契约来源：后端 OpenAPI 生成的 RouteConfigDto / RouteMetaDto / ModuleConfigDto。
 * 对契约可空字段按前端视图消费断言为非空，契约变更时编译期报错。
 */
import type { RouteConfigDto, RouteMetaDto, ModuleConfigDto } from '@/api/system'

/**
 * 路由元信息（后端契约 RouteMetaDto）
 */
export type RouteMeta = Omit<RouteMetaDto, 'title' | 'module'> & {
  /** 页面标题 */
  title?: string
  /** 所属模块 */
  module?: string
  /** 访问该页面所需的权限码；多个逗号分隔，按 permissionRequireAll 决定任一或全部满足，空表示不限制 */
  permissionCode?: string | null
}

/**
 * 后端返回的路由配置（后端契约 RouteConfigDto）
 */
export type BackendRoute = Omit<
  RouteConfigDto,
  'path' | 'name' | 'component' | 'meta' | 'children' | 'redirect'
> & {
  /** 路由路径 */
  path: string
  /** 路由名称 */
  name: string
  /** 组件路径（相对于 views 或 modules 目录） */
  component: string
  /** 路由元信息 */
  meta?: RouteMeta
  /** 子路由 */
  children?: BackendRoute[]
  /** 重定向路径 */
  redirect?: string
}

/**
 * 路由模块配置（后端契约 ModuleConfigDto）
 */
export type ModuleInfo = Omit<ModuleConfigDto, 'id' | 'name' | 'layout'> & {
  /** 模块 ID */
  id: string
  /** 模块名称 */
  name: string
  /**
   * 布局类型
   * - 'admin': 使用全局 AdminLayout
   * - 'custom': 使用自定义 Layout 组件
   * - 'none': 顶级路由，不使用任何布局
   */
  layout?: 'admin' | 'custom' | 'none'
}

/**
 * 用户菜单：可访问模块与后端统一组装的全局路由树。
 * 路由树跨模块按父级关系组装，子路由不会因父路由属于其他模块而变成根路由。
 */
export interface UserMenu {
  /** 当前用户可访问的启用模块 */
  modules: ModuleInfo[]
  /** 已完成权限裁剪的全局路由树 */
  routes: BackendRoute[]
}

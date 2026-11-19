import type { RouteRecordRaw } from 'vue-router'

/**
 * 路由布局类型
 */
export type LayoutType = 'admin' | 'custom' | 'none'

/**
 * 模块配置接口
 */
export interface ModuleConfig {
  /** 模块唯一标识 */
  id: string
  /** 模块名称 */
  name: string
  /** 模块描述 */
  description?: string
  /** 模块版本 */
  version?: string
  /** 是否启用 */
  enabled: boolean
  /** 模块路由 */
  routes: RouteRecordRaw[]
  /**
   * 布局类型
   * - 'admin': 使用全局 AdminLayout
   * - 'custom': 使用自定义 Layout 组件
   * - 'none': 顶级路由，不使用任何布局
   */
  layout: LayoutType

  /**
   * 未匹配跳转地址
   */
  emptyRedirect?: string
  /** 模块初始化函数（可选） */
  setup?: () => void | Promise<void>
  /** 模块销毁函数（可选） */
  teardown?: () => void | Promise<void>
}

/**
 * 菜单配置接口
 */
export interface MenuConfig {
  /** 默认打开的页面路径 */
  defaultOpen: string[]
}

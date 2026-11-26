/**
 * 路由相关 API（跨模块共享）
 */
import type { UserMenu } from '@/types/dynamicRoute'
import { getSystemRouteConfigUserAccessibleRoutes } from '@/api/system'

/**
 * 路由 API
 */
export const routeApi = {
  /**
   * 获取当前用户的菜单：可访问模块 + 跨模块统一组装的全局路由树
   * GET /api/system/route-config/user-accessible-routes
   */
  getUserMenu: () => {
    return getSystemRouteConfigUserAccessibleRoutes({ throwOnError: true }).then(
      (res) => res.data as UserMenu,
    )
  },
}

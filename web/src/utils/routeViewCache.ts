import type { RouteLocationNormalizedLoaded } from 'vue-router'

type RouteViewInfo = Pick<RouteLocationNormalizedLoaded, 'fullPath' | 'matched'>

/**
 * AdminLayout 渲染的是嵌套路由容器时，由容器内部的 KeepAlive 缓存最终页面。
 * 避免同一页面被两层 KeepAlive 包裹后收到多次 activated 事件。
 */
export function shouldDelegateKeepAlive(route: RouteViewInfo): boolean {
  return route.matched.length > 2
}

/**
 * 同一个一级动态路由下切换子页面时复用同一容器实例。
 */
export function getAdminRouteViewKey(route: RouteViewInfo): string {
  const directChild = route.matched[1]
  return String(directChild?.name ?? directChild?.path ?? route.fullPath)
}

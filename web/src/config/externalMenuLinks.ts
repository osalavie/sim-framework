import type { RouteLocationRaw } from 'vue-router'

/**
 * 外链菜单项解析表（路由名 → 目标地址解析函数）。
 *
 * 管理端菜单由后端动态路由驱动，meta 契约无法携带函数，因此
 * 在菜单点击时按路由名查找解析函数，同步打开空白标签后再写入
 * 目标地址。解析函数内部按需懒加载业务 API，避免进入首屏包。
 */

const externalMenuResolvers: Record<string, () => Promise<RouteLocationRaw>> = {}

export function getExternalMenuResolver(routeName: string) {
  return externalMenuResolvers[routeName]
}

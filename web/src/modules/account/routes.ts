import type { Component } from 'vue'
import type { RouteRecordRaw } from 'vue-router'
import { ensureMessagePacks } from '@/i18n/lazyPacks'

/**
 * 当前用户资料路由。
 *
 * 路径使用相对地址，由模块系统统一挂载到 AdminLayout 下。
 *
 * 页面实现位于 authorization 模块，文案来自其按需语言包；静态路由不经过
 * `dynamicRouteLoader` 的前缀匹配，需要在这里显式加载。
 */
const loadAuthorizedUserView =
  (loader: () => Promise<{ default: Component }>): (() => Promise<Component>) =>
  async () => {
    await ensureMessagePacks(['authz'])
    const view = await loader()
    return view.default
  }

export const accountRoutes: RouteRecordRaw[] = [
  {
    path: 'profile',
    name: 'UserProfile',
    component: loadAuthorizedUserView(
      () => import('@/modules/authorization/views/user/Profile.vue'),
    ),
    meta: {
      title: 'Menu:Profile',
      menu: false,
      tab: true,
      keepAlive: false,
      pinned: false,
      transition: 'fade',
    },
  },
  {
    path: 'security',
    name: 'UserSecurity',
    component: loadAuthorizedUserView(
      () => import('@/modules/authorization/views/user/Security.vue'),
    ),
    meta: {
      title: 'Account:Security',
      menu: false,
      tab: true,
      keepAlive: false,
      pinned: false,
      transition: 'fade',
    },
  },
]

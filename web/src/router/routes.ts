import type { RouteRecordRaw } from 'vue-router'

/**
 * 公开路由（不需要 Layout）
 */
export const publicRoutes: RouteRecordRaw[] = [
  {
    path: '/setup',
    name: 'Setup',
    component: () => import('@/views/setup.vue'),
    meta: {
      title: 'Setup:Title',
      keepAlive: false,
      transition: 'fade',
      anonymous: true,
    },
  },
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/login.vue'),
    meta: {
      title: 'Auth:SignIn',
      keepAlive: false,
      transition: 'fade',
      anonymous: true, // 允许匿名访问
    },
  },
  {
    path: '/register',
    name: 'Register',
    component: () => import('@/views/login.vue'),
    meta: {
      title: 'Auth:RegisterTitle',
      keepAlive: false,
      transition: 'fade',
      anonymous: true,
    },
  },
  {
    path: '/forbidden',
    name: 'Forbidden',
    component: () => import('@/views/core/403.vue'),
    meta: {
      title: 'Route:Forbidden',
      keepAlive: false,
      transition: 'fade',
    },
  },
  {
    path: '/auth/external/callback',
    name: 'ExternalAuthCallback',
    component: () => import('@/views/auth/ExternalCallback.vue'),
    meta: {
      title: 'Auth:ExternalCallbackTitle',
      keepAlive: false,
      transition: 'fade',
      anonymous: true,
    },
  },
]

/**
 * 后台兜底路由：未知后台地址在 AdminLayout 的内容区渲染 404，保留侧栏、顶栏与标签栏框架
 */
export const adminFallbackRoutes: RouteRecordRaw[] = [
  {
    path: ':pathMatch(.*)*',
    name: 'AdminNotFound',
    component: () => import('@/views/core/404.vue'),
    props: { embedded: true },
    meta: {
      title: 'Route:NotFound',
      tab: false,
      keepAlive: false,
      transition: 'fade',
    },
  },
]

/**
 * 核心路由（需要 Layout）
 */
export const coreRoutes: RouteRecordRaw[] = [
  {
    path: '/maintenance',
    name: 'Maintenance',
    component: () => import('@/views/core/maintenance.vue'),
    meta: {
      title: 'Maintenance:Title',
      keepAlive: false,
      transition: 'fade',
      anonymous: true,
    },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    component: () => import('@/views/core/404.vue'),
    meta: {
      title: 'Route:NotFound',
      tab: true,
      keepAlive: true,
      transition: 'fade',
    },
  },
]

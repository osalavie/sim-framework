import type { RouteRecordRaw } from 'vue-router'

/**
 * 无布局模块路由（顶级路由）
 */
export const noLayoutRoutes: RouteRecordRaw[] = [
  {
    path: '/fullscreen',
    name: 'Fullscreen',
    component: () => import('./views/fullscreen.vue'),
    meta: {
      title: 'Route:FullscreenPage',
    },
  },
  {
    path: '/standalone',
    name: 'Standalone',
    component: () => import('./views/standalone.vue'),
    meta: {
      title: 'Route:StandalonePage',
    },
  },
]

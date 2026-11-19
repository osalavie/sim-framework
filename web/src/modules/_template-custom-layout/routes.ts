import type { RouteRecordRaw } from 'vue-router'

/**
 * 自定义布局模块路由
 */
export const customLayoutRoutes: RouteRecordRaw[] = [
  {
    path: '/custom/page1',
    name: 'CustomPage1',
    component: () => import('./views/page1.vue'),
    meta: {
      title: 'Route:CustomPage1',
    },
  },
  {
    path: '/custom/page2',
    name: 'CustomPage2',
    component: () => import('./views/page2.vue'),
    meta: {
      title: 'Route:CustomPage2',
    },
  },
]

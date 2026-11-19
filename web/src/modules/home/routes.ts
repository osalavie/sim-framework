import type { RouteRecordRaw } from 'vue-router'

/**
 * 工作台路由
 *
 * layout: 'admin' 时路由注册为 AdminLayout 的子路由，
 * 因此这里使用相对路径，最终路径为 /admin/home。
 */
export const homeRoutes: RouteRecordRaw[] = [
  {
    path: 'home',
    name: 'HomeIndex',
    component: () => import('@/modules/home/views/index.vue'),
    meta: {
      title: 'Menu:Workspace',
      menu: true,
      tab: true,
      keepAlive: true,
      pinned: true,
      icon: 'HomeOutline',
      order: 0,
    },
  },
]

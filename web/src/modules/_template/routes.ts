import type { RouteRecordRaw } from 'vue-router'

/**
 * 模板模块路由配置
 */
export const templateRoutes: RouteRecordRaw[] = [
  {
    path: 'template/list',
    name: 'TemplateList',
    component: () => import('./views/list.vue'),
    meta: {
      title: 'Route:TemplateList',
      menu: true,
      tab: true,
      keepAlive: true,
      icon: 'list',
    },
  },
]

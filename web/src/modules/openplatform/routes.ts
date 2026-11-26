import type { RouteRecordRaw } from 'vue-router'

/**
 * 开放平台模块路由配置
 *
 * 后端种子已创建同名菜单（`/admin/openplatform` 与「应用管理」「访问日志」），
 * 组件路径按 `src/modules` 相对保存，此处维护同构的静态声明：
 * - Applications 应用管理
 * - AccessLogs 访问日志
 */
export const openPlatformRoutes: RouteRecordRaw[] = [
  {
    path: 'openplatform',
    name: 'OpenPlatform',
    redirect: { name: 'OpenPlatformApplications' },
    meta: {
      title: 'Openplatform:Title',
      menu: true,
      icon: 'carbon:api',
      order: 30,
    },
  },
  {
    path: 'openplatform/applications',
    name: 'OpenPlatformApplications',
    component: () => import('./views/ApplicationsView.vue'),
    meta: {
      title: 'Openplatform:Applications:Title',
      menu: true,
      tab: true,
      keepAlive: true,
      order: 1,
    },
  },
  {
    path: 'openplatform/access-logs',
    name: 'OpenPlatformAccessLogs',
    component: () => import('./views/AccessLogsView.vue'),
    meta: {
      title: 'Openplatform:Logs:Title',
      menu: true,
      tab: true,
      keepAlive: true,
      order: 2,
    },
  },
]

import type { RouteRecordRaw } from 'vue-router'

/**
 * 授权模块路由配置
 */
export const authorizationRoutes: RouteRecordRaw[] = [
  {
    path: 'user',
    name: 'UserManagement',
    component: () => import('@/modules/authorization/views/user/index.vue'),
    meta: {
      title: 'Menu:UserManagement',
      menu: true,
      tab: true,
      pinned: true,
      keepAlive: true,
      icon: 'PeopleOutline',
      order: 1,
      permissionCode: 'system:user:query',
    },
  },
  {
    path: 'role',
    name: 'RoleManagement',
    component: () => import('@/modules/authorization/views/role/index.vue'),
    meta: {
      title: 'Menu:RoleManagement',
      menu: true,
      tab: true,
      keepAlive: true,
      icon: 'ShieldCheckmarkOutline',
      order: 2,
      permissionCode: 'system:role:manage',
    },
  },
  {
    path: 'permission',
    name: 'PermissionManagement',
    component: () => import('@/modules/authorization/views/permission/index.vue'),
    meta: {
      title: 'Menu:PermissionManagement',
      menu: true,
      tab: true,
      keepAlive: true,
      icon: 'KeyOutline',
      order: 3,
      permissionCode: 'system:permission:manage',
    },
  },
  {
    path: 'module',
    name: 'ModuleManagement',
    component: () => import('@/modules/authorization/views/module/index.vue'),
    meta: {
      title: 'Menu:ModuleManagement',
      menu: true,
      tab: true,
      keepAlive: true,
      icon: 'GridOutline',
      order: 4,
      permissionCode: 'system:module:create,system:module:update,system:module:delete',
    },
  },
  {
    path: 'route',
    name: 'RouteManagement',
    component: () => import('@/modules/authorization/views/route/index.vue'),
    meta: {
      title: 'Menu:RouteManagement',
      menu: true,
      tab: true,
      keepAlive: true,
      icon: 'GitNetworkOutline',
      order: 5,
      permissionCode: 'system:route:create,system:route:update,system:route:delete',
    },
  },
  {
    path: 'tenant',
    name: 'TenantManagement',
    component: () => import('@/modules/authorization/views/tenant/index.vue'),
    meta: {
      title: 'Menu:TenantManagement',
      menu: true,
      tab: true,
      keepAlive: true,
      icon: 'BusinessOutline',
      order: 6,
      permissionCode: 'system:tenant:manage',
    },
  },
  {
    path: 'organization',
    name: 'OrganizationManagement',
    component: () => import('@/modules/authorization/views/organization/index.vue'),
    meta: {
      title: 'Menu:OrganizationManagement',
      menu: true,
      tab: true,
      keepAlive: true,
      icon: 'carbon:TreeView',
      order: 7,
      permissionCode: 'system:organization:manage,system:organization:view',
    },
  },
]

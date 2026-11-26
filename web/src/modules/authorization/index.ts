import type { ModuleConfig } from '@/types/module'
import { authorizationRoutes } from './routes'

/**
 * 授权管理模块配置
 */
export const authorizationModule: ModuleConfig = {
  id: 'authorization',
  name: '授权管理',
  description: '用户、角色、权限、模块和路由管理功能',
  version: '1.0.0',
  enabled: false,
  layout: 'admin',
  emptyRedirect: '/admin/authorization/user',
  routes: authorizationRoutes,
}

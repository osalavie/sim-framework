import type { ModuleConfig } from '@/types/module'
import { accountRoutes } from './routes'

/**
 * 当前用户自助维护入口。
 *
 * 这里只注册系统账户资料页面。
 */
export const accountModule: ModuleConfig = {
  id: 'account',
  name: '个人中心',
  description: '当前用户的系统账户个人信息',
  version: '1.0.0',
  enabled: true,
  layout: 'admin',
  routes: accountRoutes,
}

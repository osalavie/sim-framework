import type { ModuleConfig } from '@/types/module'
import { homeRoutes } from './routes'

/**
 * 菜单嵌套测试模块配置
 *
 * 注意：
 * - layout: 'admin' 表示路由会被注册为 AdminLayout 的子路由
 * - emptyRedirect 用于创建根路由重定向
 * - 实际路由路径会是 /admin/test/parent（因为路由定义是 'test/parent'）
 */
export const homeModule: ModuleConfig = {
  id: 'home-module',
  name: '常用功能模块',
  description: '常用功能模块',
  version: '1.0.0',
  enabled: true,
  layout: 'admin',
  routes: homeRoutes,
}

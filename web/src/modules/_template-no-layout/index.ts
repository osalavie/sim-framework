import type { ModuleConfig } from '@/types/module'
import { noLayoutRoutes } from './routes'

/**
 * 无布局模块示例
 *
 * 这个模块的路由是顶级路由，不使用任何布局
 * 适用于：全屏页面、独立应用、特殊页面等
 */
export const noLayoutModule: ModuleConfig = {
  id: 'no-layout-template',
  name: '无布局模块',
  description: '顶级路由，不使用任何布局的模块示例',
  version: '1.0.0',
  enabled: false, // 默认禁用，需要时启用
  layout: 'none', // 不使用任何布局
  emptyRedirect: '/no-layout/page1',
  routes: noLayoutRoutes,
}

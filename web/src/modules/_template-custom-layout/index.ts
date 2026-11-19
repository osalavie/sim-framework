import type { ModuleConfig } from '@/types/module'
import { customLayoutRoutes } from './routes'

/**
 * 自定义布局模块示例
 *
 * 这个模块使用自己的布局组件，不使用全局 Layout
 */
export const customLayoutModule: ModuleConfig = {
  id: 'custom-layout',
  name: '自定义布局模块',
  description: '使用自定义布局的模块示例',
  version: '1.0.0',
  enabled: false, // 默认禁用，需要时启用
  layout: 'custom', // 使用自定义布局
  emptyRedirect: '/custom/page1',
  routes: customLayoutRoutes,
}

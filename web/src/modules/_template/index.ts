import type { ModuleConfig } from '@/types/module'
import { templateRoutes } from './routes'

/**
 * 模板模块配置
 *
 * 复制此模板创建新模块：
 * 1. 复制 _template 文件夹并重命名
 * 2. 修改此文件中的配置信息
 * 3. 在 src/config/businessModules.ts 中注册模块
 */
export const templateModule: ModuleConfig = {
  id: 'template',
  name: '模板模块',
  description: '这是一个模块模板',
  version: '1.0.0',
  enabled: false, // 默认禁用
  routes: templateRoutes,
  layout: 'admin',
}

import type { ModuleConfig, MenuConfig } from '@/types/module'
import { platformModules } from '@/framework/platformModules'
import { businessModules } from './businessModules'
import { validateModules } from '@/utils/moduleValidation'

/**
 * 注册的所有静态模块（未验证）
 *
 * 平台模块由框架提供；业务模块在 `src/config/businessModules.ts` 中追加，
 * 业务仓只维护该文件，基座同步不会覆盖。
 */
const rawModules: ModuleConfig[] = [...platformModules, ...businessModules]

/**
 * 验证后的模块列表
 * 只包含通过验证的模块，无效模块会被过滤并记录错误
 */
export const modules: ModuleConfig[] = validateModules(rawModules)

/**
 * 菜单配置
 */
export const menuConfig: MenuConfig = {
  defaultOpen: ['/admin/home'],
}

/**
 * 获取所有启用的静态模块
 */
export function getEnabledModules(): ModuleConfig[] {
  return modules.filter((module) => module.enabled)
}

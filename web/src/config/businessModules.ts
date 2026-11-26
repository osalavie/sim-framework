import type { ModuleConfig } from '@/types/module'

/**
 * 业务系统模块清单。
 *
 * 基座仓保持为空数组；业务仓在此追加自己的模块，基座同步不会修改本文件。
 * 模块页面放在 `src/modules/<模块>/`，并在入口通过 `registerModuleComponents` 注册组件。
 */
export const businessModules: ModuleConfig[] = []

/** 基座仓没有业务装配；业务仓在自己的入口实现语言包、SDK 与扩展点注册。 */
export function bootstrapBusinessModules(): void {}

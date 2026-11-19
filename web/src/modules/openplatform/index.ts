import type { ModuleConfig } from '@/types/module'
import { openPlatformRoutes } from './routes'

/**
 * 开放平台模块（数据库驱动）
 * 模块、权限、菜单由后端 DataSeeder 创建，前端通过动态路由加载；
 * 菜单可见性由角色模块分配（RoleModule）控制，故静态注册保持禁用。
 */
export const openPlatformModule: ModuleConfig = { id: 'openplatform', name: '开放平台', description: '第三方应用接入、AppSecret 管理与令牌签发审计', version: '1.0.0', enabled: false, layout: 'admin', emptyRedirect: '/admin/openplatform/applications', routes: openPlatformRoutes }

import type { ModuleConfig } from '@/types/module'
import { approvalRoutes } from './routes'

/**
 * 审批模块（数据库驱动）
 * 模块、权限、菜单由后端 DataSeeder 创建，前端通过动态路由加载；
 * 菜单可见性由角色模块分配（RoleModule）控制，故静态注册保持禁用。
 */
export const approvalModule: ModuleConfig = { id: 'approval', name: '审批', description: '审批流程定义、审批中心待办处理与实例监控', version: '1.0.0', enabled: false, layout: 'admin', emptyRedirect: '/admin/approval/todo', routes: approvalRoutes }

/**
 * 租户模型定义
 * 对应后端 Sim.System.Application.Contracts.Tenant
 */

/**
 * 租户信息（对应 TenantDto）
 */
export interface Tenant {
  id: string
  name: string
  code: string
  isActive: boolean
  adminEmail?: string | null
  connectionString?: string | null
  creationTime: string
  creatorId?: string | null
  lastModificationTime?: string | null
  lastModifierId?: string | null
}

/**
 * 创建租户表单数据（对应 CreateTenantDto）
 */
export interface TenantFormData {
  name: string
  code: string
  adminEmail?: string
  connectionString?: string
  /** 租户管理员用户名（可选，默认 "admin"） */
  adminUserName?: string
  /** 租户管理员初始密码（可选，默认使用系统配置） */
  adminPassword?: string
  /** 分配给该租户的模块 ID 列表（Host 级模板的 ID） */
  moduleIds: string[]
  /** 分配给该租户的路由 ID 列表（Host 级模板的 ID） */
  routeIds: string[]
  /** 分配给该租户的权限 ID 列表（Host 级模板的 ID） */
  permissionIds: string[]
}

/**
 * 更新租户数据（对应 UpdateTenantDto）
 */
export interface TenantUpdateData {
  name?: string
  adminEmail?: string
  connectionString?: string
}

/**
 * 创建租户的结果（对应 CreateTenantResultDto）
 * 包含管理员初始凭证，创建成功后展示给 Host
 */
export interface CreateTenantResult {
  tenantId: string
  tenantName: string
  tenantCode: string
  adminUserId: string
  adminUserName: string
  initialPassword: string
  assignedModuleCount: number
  assignedRouteCount: number
  assignedPermissionCount: number
}

/**
 * 模板模块（对应 TemplateModuleDto）
 */
export interface TemplateModule {
  id: string
  name: string
  description?: string | null
  icon?: string | null
  order: number
  enabled: boolean
}

/**
 * 模板路由（对应 TemplateRouteDto）
 */
export interface TemplateRoute {
  id: string
  parentId?: string | null
  name: string
  path: string
  title?: string | null
  icon?: string | null
  menu: boolean
  order: number
}

/**
 * 模板权限（对应 TemplatePermissionDto）
 */
export interface TemplatePermission {
  id: string
  name: string
  code: string
  description?: string | null
}

/**
 * 租户可分配的模板配置（对应 TenantTemplateDto）
 */
export interface TenantTemplate {
  modules: TemplateModule[]
  routes: TemplateRoute[]
  permissions: TemplatePermission[]
}

/**
 * 租户已分配的配置详情（对应 TenantAssignmentDto）
 */
export interface TenantAssignment {
  tenantId: string
  tenantName: string
  modules: TemplateModule[]
  routes: TemplateRoute[]
  permissions: TemplatePermission[]
}

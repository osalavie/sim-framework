import {
  getSystemTenant,
  getSystemTenantId,
  postSystemTenant,
  putSystemTenantId,
  deleteSystemTenantId,
  postSystemTenantIdEnable,
  postSystemTenantIdDisable,
  getSystemTenantTemplates,
  getSystemTenantIdAssignments,
} from '@/api/system'
import type {
  Tenant,
  TenantFormData,
  TenantUpdateData,
  CreateTenantResult,
  TenantTemplate,
  TenantAssignment,
} from './tenantModels'

// 导出类型定义
export * from './tenantModels'

/**
 * 租户 API
 * 后端路由前缀: /api/system/tenant
 */
export const tenantApi = {
  /**
   * 获取租户列表
   * GET /api/system/tenant
   * 后端返回 List<TenantDto>（非分页）
   */
  getList: () => {
    return getSystemTenant({ throwOnError: true }).then((res) => res.data as Tenant[])
  },

  /**
   * 根据 ID 获取租户详情
   * GET /api/system/tenant/{id}
   */
  get: (id: string) => {
    return getSystemTenantId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as Tenant,
    )
  },

  /**
   * 创建租户（包含配置复制和管理员初始化）
   * POST /api/system/tenant
   * 返回创建结果（含管理员初始凭证）
   */
  create: (data: TenantFormData) => {
    return postSystemTenant({ body: data, throwOnError: true }).then(
      (res) => res.data as CreateTenantResult,
    )
  },

  /**
   * 更新租户基本信息
   * PUT /api/system/tenant/{id}
   */
  update: (id: string, data: TenantUpdateData) => {
    return putSystemTenantId({ path: { id }, body: data, throwOnError: true }).then(
      (res) => res.data as Tenant,
    )
  },

  /**
   * 删除租户
   * DELETE /api/system/tenant/{id}
   */
  delete: (id: string) => {
    return deleteSystemTenantId({ path: { id }, throwOnError: true }).then((res) => res.data)
  },

  /**
   * 启用租户
   * POST /api/system/tenant/{id}/enable
   */
  enable: (id: string) => {
    return postSystemTenantIdEnable({ path: { id }, throwOnError: true }).then((res) => res.data)
  },

  /**
   * 禁用租户
   * POST /api/system/tenant/{id}/disable
   */
  disable: (id: string) => {
    return postSystemTenantIdDisable({ path: { id }, throwOnError: true }).then((res) => res.data)
  },

  /**
   * 获取 Host 级可选模板配置（模块/路由/权限列表）
   * GET /api/system/tenant/templates
   */
  getTemplates: () => {
    return getSystemTenantTemplates({ throwOnError: true }).then(
      (res) => res.data as TenantTemplate,
    )
  },

  /**
   * 获取指定租户已分配的配置详情
   * GET /api/system/tenant/{id}/assignments
   */
  getAssignments: (id: string) => {
    return getSystemTenantIdAssignments({ path: { id }, throwOnError: true }).then(
      (res) => res.data as TenantAssignment,
    )
  },
}

// 保持向后兼容的别名
export const getTenantList = tenantApi.getList
export const getTenant = tenantApi.get
export const createTenant = tenantApi.create
export const updateTenant = tenantApi.update
export const deleteTenant = tenantApi.delete
export const enableTenant = tenantApi.enable
export const disableTenant = tenantApi.disable
export const getTenantTemplates = tenantApi.getTemplates
export const getTenantAssignments = tenantApi.getAssignments

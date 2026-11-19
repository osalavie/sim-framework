import type {
  RoleDto,
  CreateRoleDto as CreateRoleDtoSchema,
  UpdateRoleDto as UpdateRoleDtoSchema,
} from '@/api/system'

/**
 * 角色模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅表单/查询参数保留前端自有类型。
 */

// ===== 网络 DTO（来自生成 Schema）=====

export const isBuiltInRole = (role: Pick<Role, 'isBuiltIn'>) => role.isBuiltIn === true

/** 角色信息（后端契约 RoleDto） */
export type Role = RoleDto & {
  id: string
  code: string
  name: string
  isBuiltIn?: boolean
}

/** 创建角色请求 DTO */
export type CreateRoleDto = CreateRoleDtoSchema

/** 更新角色请求 DTO */
export type UpdateRoleDto = UpdateRoleDtoSchema

// ===== 视图模型 =====

/**
 * 角色表单数据（用于创建和更新）
 * moduleIds 为前端关联模块专用，创建/更新后单独调用 assignModules
 */
export interface RoleFormData {
  code: string
  name: string
  description?: string
  moduleIds?: string[] // 关联的模块 ID
  permissionIds?: string[] // 关联的权限 ID
}

/**
 * 角色查询参数
 */
export interface RoleQueryParams {
  skipCount?: number
  maxResultCount?: number
  keyword?: string
  sorting?: string
}

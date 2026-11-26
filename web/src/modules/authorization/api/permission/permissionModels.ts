import type {
  PermissionDto,
  CreatePermissionDto as CreatePermissionDtoSchema,
  UpdatePermissionDto as UpdatePermissionDtoSchema,
} from '@/api/system'

/**
 * 权限模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅表单/查询参数保留前端自有类型。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/**
 * 权限信息（后端契约 PermissionDto）。
 * 权限与模块没有数据关联；权限码前缀（system/approval）只用于展示分组。
 */
export type Permission = PermissionDto & {
  id: string
  code: string
  name: string
}

/** 创建权限请求 DTO */
export type CreatePermissionDto = CreatePermissionDtoSchema

/** 更新权限请求 DTO */
export type UpdatePermissionDto = UpdatePermissionDtoSchema

// ===== 视图模型 =====

/**
 * 权限表单数据（用于创建和更新）
 */
export interface PermissionFormData {
  code: string
  name: string
  description?: string
}

/**
 * 权限查询参数
 */
export interface PermissionQueryParams {
  skipCount?: number
  maxResultCount?: number
  keyword?: string
  sorting?: string
}

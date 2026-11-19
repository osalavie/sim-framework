import type {
  ModuleConfigDto,
  CreateModuleConfigDto as CreateModuleConfigDtoSchema,
  UpdateModuleConfigDto as UpdateModuleConfigDtoSchema,
} from '@/api/system'

/**
 * 模块模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅表单/查询参数保留前端自有类型。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/**
 * 模块信息（后端契约 ModuleConfigDto）
 * 后端 DTO 不含 version，version 为前端视图专用字段（运行时可能为 undefined）。
 */
export type Module = ModuleConfigDto & {
  id: string
  name: string
  enabled: boolean
  order: number
  /** 前端视图专用：模块版本号 */
  version?: string
}

/** 创建模块请求 DTO */
export type CreateModuleConfigDto = CreateModuleConfigDtoSchema

/** 更新模块请求 DTO */
export type UpdateModuleConfigDto = UpdateModuleConfigDtoSchema

// ===== 视图模型 =====

/**
 * 模块表单数据（用于创建和更新）
 */
export interface ModuleFormData {
  name: string
  description?: string
  version?: string
  enabled: boolean
  icon?: string
  order: number
  layout?: string
  emptyRedirect?: string
  routeIds?: string[]
}

/**
 * 模块查询参数
 */
export interface ModuleQueryParams {
  skipCount?: number
  maxResultCount?: number
  keyword?: string
  sorting?: string
  enabled?: boolean // 按启用状态过滤
}

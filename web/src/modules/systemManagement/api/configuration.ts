import {
  getSystemConfigurationItem,
  postSystemConfigurationItem,
  putSystemConfigurationItemId,
  deleteSystemConfigurationItemId,
} from '@/api/system'
import type {
  SystemConfigurationItemDto,
  CreateSystemConfigurationItemDto,
  UpdateSystemConfigurationItemDto,
} from '@/api/system'

/**
 * 系统配置模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅对前端必用的字段做必填断言。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/** 系统配置项（后端契约 SystemConfigurationItemDto） */
export type SystemConfigurationItem =
  SystemConfigurationItemDto & {
    id: string
    groupKey: string
    groupName: string
    configKey: string
    configName: string
    value: string
    sort: number
    enabled: boolean
  }

/** 创建系统配置项请求 DTO（后端契约 CreateSystemConfigurationItemDto） */
export type CreateSystemConfigurationItem = CreateSystemConfigurationItemDto

/** 更新系统配置项请求 DTO（后端契约 UpdateSystemConfigurationItemDto） */
export type UpdateSystemConfigurationItem = UpdateSystemConfigurationItemDto

export const systemConfigurationApi = {
  getList: (groupKey?: string) =>
    getSystemConfigurationItem({
      query: groupKey ? { groupKey } : undefined,
      throwOnError: true,
    }).then((res) => res.data as SystemConfigurationItem[]),
  create: (input: CreateSystemConfigurationItem) =>
    postSystemConfigurationItem({ body: input, throwOnError: true }).then(
      (res) => res.data as SystemConfigurationItem,
    ),
  update: (id: string, input: UpdateSystemConfigurationItem) =>
    putSystemConfigurationItemId({ path: { id }, body: input, throwOnError: true }).then(
      (res) => res.data as SystemConfigurationItem,
    ),
  delete: (id: string) =>
    deleteSystemConfigurationItemId({ path: { id }, throwOnError: true }).then((res) => res.data),
}

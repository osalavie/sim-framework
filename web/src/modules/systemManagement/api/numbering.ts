import {
  getSystemBusinessNumberDefinition,
  postSystemBusinessNumberDefinition,
  putSystemBusinessNumberDefinitionId,
} from '@/api/system'
import type {
  BusinessNumberDefinitionDto,
  CreateBusinessNumberDefinitionDto,
  UpdateBusinessNumberDefinitionDto,
} from '@/api/system'

/**
 * 业务编号模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅对前端必用的字段做必填断言。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/** 业务编号定义（后端契约 BusinessNumberDefinitionDto） */
export type BusinessNumberDefinition =
  BusinessNumberDefinitionDto & {
    id: string
    businessKey: string
    displayName: string
    format: string
    initialSequence: number
    enabled: boolean
  }

/** 创建业务编号定义请求 DTO（后端契约 CreateBusinessNumberDefinitionDto） */
export type CreateBusinessNumberDefinition = CreateBusinessNumberDefinitionDto

/** 更新业务编号定义请求 DTO（后端契约 UpdateBusinessNumberDefinitionDto） */
export type UpdateBusinessNumberDefinition = UpdateBusinessNumberDefinitionDto

export const businessNumberDefinitionApi = {
  getList: () =>
    getSystemBusinessNumberDefinition({ throwOnError: true }).then(
      (res) => res.data as BusinessNumberDefinition[],
    ),
  create: (input: CreateBusinessNumberDefinition) =>
    postSystemBusinessNumberDefinition({ body: input, throwOnError: true }).then(
      (res) => res.data as BusinessNumberDefinition,
    ),
  update: (id: string, input: UpdateBusinessNumberDefinition) =>
    putSystemBusinessNumberDefinitionId({ path: { id }, body: input, throwOnError: true }).then(
      (res) => res.data as BusinessNumberDefinition,
    ),
}

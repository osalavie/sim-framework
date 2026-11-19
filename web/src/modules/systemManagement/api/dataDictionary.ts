import {
  getSystemDataDictionary,
  postSystemDataDictionary,
  putSystemDataDictionaryId,
  deleteSystemDataDictionaryId,
  getSystemDataDictionaryItem,
  postSystemDataDictionaryItem,
  putSystemDataDictionaryItemId,
  deleteSystemDataDictionaryItemId,
} from '@/api/system'
import type {
  DataDictionaryValueType,
  DataDictionaryDto,
  CreateDataDictionaryDto,
  UpdateDataDictionaryDto,
  DataDictionaryItemDto,
  CreateDataDictionaryItemDto,
  UpdateDataDictionaryItemDto,
} from '@/api/system'
export type { DataDictionaryValueType } from '@/api/system'

/**
 * 数据字典模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅对前端必用的字段做必填断言。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/** 数据字典（后端契约 DataDictionaryDto） */
export type DataDictionary =
  DataDictionaryDto & {
    id: string
    dictionaryKey: string
    displayName: string
    valueType: DataDictionaryValueType
    valuePrefix: string
    valueSuffix: string
    enabled: boolean
    itemCount: number
  }

/** 创建数据字典请求 DTO（后端契约 CreateDataDictionaryDto） */
export type CreateDataDictionary = CreateDataDictionaryDto

/** 更新数据字典请求 DTO（后端契约 UpdateDataDictionaryDto） */
export type UpdateDataDictionary =
  UpdateDataDictionaryDto & {
    valueType: DataDictionaryValueType
  }

/** 数据字典项（后端契约 DataDictionaryItemDto） */
export type DataDictionaryItem =
  DataDictionaryItemDto & {
    id: string
    dictionaryId: string
    itemKey: string
    itemName: string
    value: string
    sort: number
    enabled: boolean
  }

/** 创建数据字典项请求 DTO（后端契约 CreateDataDictionaryItemDto） */
export type CreateDataDictionaryItem = CreateDataDictionaryItemDto

/** 更新数据字典项请求 DTO（后端契约 UpdateDataDictionaryItemDto） */
export type UpdateDataDictionaryItem = UpdateDataDictionaryItemDto

export const dataDictionaryApi = {
  getList: () =>
    getSystemDataDictionary({ throwOnError: true }).then((res) => res.data as DataDictionary[]),
  create: (input: CreateDataDictionary) =>
    postSystemDataDictionary({ body: input, throwOnError: true }).then(
      (res) => res.data as DataDictionary,
    ),
  update: (id: string, input: UpdateDataDictionary) =>
    putSystemDataDictionaryId({ path: { id }, body: input, throwOnError: true }).then(
      (res) => res.data as DataDictionary,
    ),
  delete: (id: string) =>
    deleteSystemDataDictionaryId({ path: { id }, throwOnError: true }).then((res) => res.data),
}

export const dataDictionaryItemApi = {
  getList: (dictionaryId?: string) =>
    getSystemDataDictionaryItem({
      query: dictionaryId ? { dictionaryId } : undefined,
      throwOnError: true,
    }).then((res) => res.data as DataDictionaryItem[]),
  create: (input: CreateDataDictionaryItem) =>
    postSystemDataDictionaryItem({ body: input, throwOnError: true }).then(
      (res) => res.data as DataDictionaryItem,
    ),
  update: (id: string, input: UpdateDataDictionaryItem) =>
    putSystemDataDictionaryItemId({ path: { id }, body: input, throwOnError: true }).then(
      (res) => res.data as DataDictionaryItem,
    ),
  delete: (id: string) =>
    deleteSystemDataDictionaryItemId({ path: { id }, throwOnError: true }).then(
      (res) => res.data,
    ),
}

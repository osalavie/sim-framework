import {
  getSystemFileStorageConfiguration,
  postSystemFileStorageConfigurationTest,
  putSystemFileStorageConfiguration,
} from '@/api/system'
import type {
  FileStorageConnectionTestResultDto,
  FileStorageProviderConfigurationDto,
  FileStorageConfigurationDto,
  TestFileStorageProviderConnectionDto,
  UpdateFileStorageProviderConfigurationDto,
  UpdateFileStorageConfigurationDto,
} from '@/api/system'

/**
 * 文件存储配置模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅对前端必用的字段做必填断言。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/** 文件存储提供方配置（后端契约 FileStorageProviderConfigurationDto） */
export type FileStorageProviderConfiguration =
  FileStorageProviderConfigurationDto & {
    provider: string
    enabled: boolean
    hasAccessKeySecret: boolean
    hasSessionToken: boolean
    forcePathStyle: boolean
  }

/** 文件存储配置（后端契约 FileStorageConfigurationDto） */
export type FileStorageConfiguration = Omit<
  FileStorageConfigurationDto,
  'maxUploadBytes' | 'providers'
> & {
  defaultProvider: string
  maxUploadBytes: number
  providers: FileStorageProviderConfiguration[]
}

/** 更新文件存储提供方配置请求 DTO（后端契约 UpdateFileStorageProviderConfigurationDto） */
export type UpdateFileStorageProviderConfiguration =
  UpdateFileStorageProviderConfigurationDto & {
    /** 前端视图专用：当前是否已配置密钥（更新接口不返回，来自读取接口） */
    hasAccessKeySecret?: boolean
    /** 前端视图专用：当前是否已配置会话令牌（更新接口不返回，来自读取接口） */
    hasSessionToken?: boolean
  }

/** 更新文件存储配置请求 DTO（后端契约 UpdateFileStorageConfigurationDto） */
export type UpdateFileStorageConfiguration = Omit<
  UpdateFileStorageConfigurationDto,
  'maxUploadBytes' | 'providers'
> & {
  maxUploadBytes: number
  providers: UpdateFileStorageProviderConfiguration[]
}

/** 测试存储连接请求 DTO（后端契约 TestFileStorageProviderConnectionDto） */
export type TestFileStorageProviderConnection = TestFileStorageProviderConnectionDto & {
  provider: string
}

/** 测试存储连接结果（后端契约 FileStorageConnectionTestResultDto） */
export type FileStorageConnectionTestResult = FileStorageConnectionTestResultDto & {
  success: boolean
}

export const fileStorageConfigurationApi = {
  get: () =>
    getSystemFileStorageConfiguration({ throwOnError: true }).then(
      (res) => res.data as FileStorageConfiguration,
    ),
  update: (data: UpdateFileStorageConfiguration) =>
    putSystemFileStorageConfiguration({ body: data, throwOnError: true }).then(
      (res) => res.data as FileStorageConfiguration,
    ),
  testConnection: (data: TestFileStorageProviderConnection) =>
    postSystemFileStorageConfigurationTest({
      body: data,
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
    }).then((res) => res.data as FileStorageConnectionTestResult),
}

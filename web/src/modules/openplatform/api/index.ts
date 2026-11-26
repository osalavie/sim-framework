import {
  getOpenplatformOpenPlatformApplication,
  getOpenplatformOpenPlatformApplicationId,
  postOpenplatformOpenPlatformApplication,
  putOpenplatformOpenPlatformApplicationId,
  deleteOpenplatformOpenPlatformApplicationId,
  postOpenplatformOpenPlatformApplicationIdEnable,
  postOpenplatformOpenPlatformApplicationIdDisable,
  postOpenplatformOpenPlatformApplicationIdResetSecret,
  getOpenplatformOpenPlatformAccessLog,
  getOpenplatformOpenPlatformScope,
} from '@/api/openplatform'
import type { PageData } from '@/types/api'

/**
 * 开放平台第三方应用。
 * AppSecret 不回显：仅在创建与重置密钥时由接口一次性明文返回。
 */
export interface OpenPlatformApplication {
  id: string
  name: string
  appId: string
  description?: string
  isActive: boolean
  scopes: string[]
  allowedIps: string[]
  creationTime: string
  creatorId?: string | null
  lastModificationTime?: string | null
  lastModifierId?: string | null
}

export interface CreateOpenPlatformApplication {
  name: string
  description?: string
  scopes: string[]
  allowedIps: string[]
}

export interface UpdateOpenPlatformApplication {
  name?: string
  description?: string
  scopes?: string[]
  allowedIps?: string[]
}

export interface CreateOpenPlatformApplicationResult {
  id: string
  appId: string
  appSecret: string
}

export interface ResetOpenPlatformSecretResult {
  appSecret: string
}

/** 令牌签发审计记录（只读，由令牌端点写入）。 */
export interface OpenPlatformAccessLog {
  id: string
  applicationId: string
  appId: string
  succeeded: boolean
  reason?: string
  clientIp?: string
  creationTime: string
}

export interface OpenPlatformAccessLogQuery {
  appId?: string
  succeeded?: boolean
  clientIp?: string
  startTime?: string
  endTime?: string
  skipCount?: number
  maxResultCount?: number
}

/** 可授予第三方应用的权限范围（来自系统权限清单，只读）。 */
export interface OpenPlatformScope {
  code: string
  name: string
  description?: string
}

export const openPlatformApplicationApi = {
  getList: () =>
    getOpenplatformOpenPlatformApplication({ throwOnError: true }).then(
      (res) => res.data as OpenPlatformApplication[],
    ),
  get: (id: string) =>
    getOpenplatformOpenPlatformApplicationId({ path: { id }, throwOnError: true }).then(
      (res) => res.data as OpenPlatformApplication,
    ),
  create: (input: CreateOpenPlatformApplication) =>
    postOpenplatformOpenPlatformApplication({ body: input, throwOnError: true }).then(
      (res) => res.data as CreateOpenPlatformApplicationResult,
    ),
  update: (id: string, input: UpdateOpenPlatformApplication) =>
    putOpenplatformOpenPlatformApplicationId({
      path: { id },
      body: input,
      throwOnError: true,
    }).then((res) => res.data as OpenPlatformApplication),
  enable: (id: string) =>
    postOpenplatformOpenPlatformApplicationIdEnable({ path: { id }, throwOnError: true }).then(
      (res) => res.data,
    ),
  disable: (id: string) =>
    postOpenplatformOpenPlatformApplicationIdDisable({ path: { id }, throwOnError: true }).then(
      (res) => res.data,
    ),
  resetSecret: (id: string) =>
    postOpenplatformOpenPlatformApplicationIdResetSecret({ path: { id }, throwOnError: true }).then(
      (res) => res.data as ResetOpenPlatformSecretResult,
    ),
  delete: (id: string) =>
    deleteOpenplatformOpenPlatformApplicationId({ path: { id }, throwOnError: true }).then(
      (res) => res.data,
    ),
}

/** 授权范围清单（只读，供管理端下拉选择）。 */
export const openPlatformScopeApi = {
  getList: () =>
    getOpenplatformOpenPlatformScope({ throwOnError: true }).then(
      (res) => res.data as OpenPlatformScope[],
    ),
}

export const openPlatformAccessLogApi = {
  getList: (query: OpenPlatformAccessLogQuery) =>
    getOpenplatformOpenPlatformAccessLog({ query: query as never, throwOnError: true }).then(
      (res) => res.data as unknown as PageData<OpenPlatformAccessLog>,
    ),
}

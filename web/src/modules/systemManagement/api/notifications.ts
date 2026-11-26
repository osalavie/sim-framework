import {
  getSystemNotificationAdministration,
  getSystemNotificationAdministrationDeliveries,
  getSystemNotificationAdministrationAccounts,
  postSystemNotificationAdministrationSaveProvider,
  postSystemNotificationAdministrationSaveTemplate,
  postSystemNotificationAdministrationSaveRule,
  postSystemNotificationAdministrationSaveWebhook,
  postSystemNotificationAdministrationIdTestProvider,
} from '@/api/system'
import type {
  NotificationProviderConfigurationDto,
  NotificationTemplateDto,
  NotificationRuleDto,
  WebhookEndpointDto,
  NotificationDeliveryDto,
  NotificationAdministrationSnapshotDto,
  NotificationAccountDto,
} from '@/api/system'

/**
 * 通知管理模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅对前端必用的字段做必填断言。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/** 通知提供方配置（后端契约 NotificationProviderConfigurationDto） */
export type NotificationProviderConfiguration =
  NotificationProviderConfigurationDto & {
    id: string
    channel: string
    provider: string
    displayName: string
    enabled: boolean
    priority: number
  }

/** 通知模板（后端契约 NotificationTemplateDto） */
export type NotificationTemplate =
  NotificationTemplateDto & {
    id: string
    key: string
    channel: string
    locale: string
    body: string
    enabled: boolean
  }

/** 通知规则（后端契约 NotificationRuleDto） */
export type NotificationRule =
  NotificationRuleDto & {
    id: string
    eventName: string
    channel: string
    templateKey: string
    enabled: boolean
  }

/** Webhook 端点（后端契约 WebhookEndpointDto） */
export type WebhookEndpoint =
  WebhookEndpointDto & {
    id: string
    site: string
    name: string
    url: string
    enabled: boolean
  }

/** 通知投递记录（后端契约 NotificationDeliveryDto） */
export type NotificationDelivery =
  NotificationDeliveryDto & {
    id: string
    eventId: string
    eventName: string
    channel: string
    provider: string
    destinationMasked: string
    status: string
    attempts: number
    creationTime: string
  }

/** 通知管理快照（后端契约 NotificationAdministrationSnapshotDto） */
export type NotificationAdministrationSnapshot =
  NotificationAdministrationSnapshotDto & {
    providers: NotificationProviderConfiguration[]
    templates: NotificationTemplate[]
    rules: NotificationRule[]
    webhooks: WebhookEndpoint[]
  }

/** 通知账号（后端契约 NotificationAccountDto） */
export type NotificationAccount =
  NotificationAccountDto & {
    userId: string
    account: string
    destinationMasked: string
  }

export const notificationAdministrationApi = {
  get: () =>
    getSystemNotificationAdministration({ throwOnError: true }).then(
      (res) => res.data as NotificationAdministrationSnapshot,
    ),
  getDeliveries: () =>
    getSystemNotificationAdministrationDeliveries({ throwOnError: true }).then(
      (res) => res.data as NotificationDelivery[],
    ),
  getAccounts: (channel: string, keyword?: string) =>
    getSystemNotificationAdministrationAccounts({
      query: { channel, keyword, maxCount: 20 },
      throwOnError: true,
    }).then((res) => res.data as NotificationAccount[]),
  saveProvider: (data: Record<string, unknown>) =>
    postSystemNotificationAdministrationSaveProvider({
      body: data as never,
      throwOnError: true,
    }).then((res) => res.data as NotificationProviderConfiguration),
  saveTemplate: (data: Record<string, unknown>) =>
    postSystemNotificationAdministrationSaveTemplate({
      body: data as never,
      throwOnError: true,
    }).then((res) => res.data as NotificationTemplate),
  saveRule: (data: Record<string, unknown>) =>
    postSystemNotificationAdministrationSaveRule({
      body: data as never,
      throwOnError: true,
    }).then((res) => res.data as NotificationRule),
  saveWebhook: (data: Record<string, unknown>) =>
    postSystemNotificationAdministrationSaveWebhook({
      body: data as never,
      throwOnError: true,
    }).then((res) => res.data as WebhookEndpoint),
  testProvider: (id: string, target: { destination?: string; userId?: string }) =>
    postSystemNotificationAdministrationIdTestProvider({
      path: { id },
      query: target,
      throwOnError: true,
    }).then((res) => res.data as { succeeded: boolean; status: string; errorCode?: string }),
}

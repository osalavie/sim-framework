import {
  getSystemUserNotification,
  postSystemUserNotificationIdMarkRead,
  postSystemUserNotificationMarkAllRead,
} from '@/api/system'
import type { UserNotificationDto, UserNotificationSummaryDto } from '@/api/system'

/**
 * 用户通知模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅对前端必用的字段做必填断言。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/** 用户通知（后端契约 UserNotificationDto） */
export type UserNotification = UserNotificationDto & {
  id: string
  title: string
  body: string
  isRead: boolean
  creationTime: string
}

/** 用户通知摘要（后端契约 UserNotificationSummaryDto） */
export type UserNotificationSummary = Omit<UserNotificationSummaryDto, 'items'> & {
  unreadCount: number
  items: UserNotification[]
}

export const userNotificationApi = {
  get: (maxCount = 20) =>
    getSystemUserNotification({ query: { maxCount }, throwOnError: true }).then(
      (res) => res.data as UserNotificationSummary,
    ),
  markRead: (id: string) =>
    postSystemUserNotificationIdMarkRead({ path: { id }, throwOnError: true }).then(
      (res) => res.data,
    ),
  markAllRead: () =>
    postSystemUserNotificationMarkAllRead({ throwOnError: true }).then((res) => res.data),
}

import {
  getSystemScheduleTask,
  getSystemScheduleTaskServiceMethods,
  postSystemScheduleTask,
  putSystemScheduleTask,
  deleteSystemScheduleTaskId,
} from '@/api/system'
import type { ScheduleTaskDto, ScheduleTaskServiceMethodDto } from '@/api/system'

/**
 * 定时任务模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅任务类型枚举、创建表单等前端视图模型保留自有类型。
 */

// ===== 网络 DTO（来自生成 Schema）=====

/** 定时任务（后端契约 ScheduleTaskDto） */
export type ScheduleTask =
  ScheduleTaskDto & {
    id: string
    key: string
    group: string
    displayName: string
    cronExpression: string
    enabled: boolean
    taskType: ScheduleTaskType
    isBuiltIn: boolean
    canDelete: boolean
    status: string
    previousRunTime?: string
    nextRunTime?: string
  }

/** 定时任务服务方法（后端契约 ScheduleTaskServiceMethodDto） */
export type ScheduleTaskServiceMethod =
  ScheduleTaskServiceMethodDto & {
    key: string
    displayName: string
    description: string
  }

// ===== 视图模型 =====

/** 定时任务类型（前端视图枚举，后端以字符串返回） */
export type ScheduleTaskType = 'Http' | 'Command' | 'ServiceMethod' | 'Worker'

/** 创建定时任务表单（视图模型，字段与后端契约 CreateScheduleTaskDto 对齐） */
export interface CreateScheduleTask {
  displayName: string
  description?: string
  taskType: Exclude<ScheduleTaskType, 'Worker'>
  cronExpression: string
  enabled: boolean
  httpMethod?: string
  url?: string
  httpHeaders?: Record<string, string>
  httpBody?: string
  httpContentType?: string
  command?: string
  commandArguments?: string[]
  workingDirectory?: string
  timeoutSeconds?: number
  serviceMethodKey?: string
  serviceArgumentsJson?: string
}

export const scheduleTaskApi = {
  getList: () =>
    getSystemScheduleTask({ throwOnError: true }).then((res) => res.data as ScheduleTask[]),
  getServiceMethods: () =>
    getSystemScheduleTaskServiceMethods({ throwOnError: true }).then(
      (res) => res.data as ScheduleTaskServiceMethod[],
    ),
  create: (data: CreateScheduleTask) =>
    postSystemScheduleTask({ body: data as never, throwOnError: true }).then(
      (res) => res.data as ScheduleTask,
    ),
  update: (task: ScheduleTask) =>
    putSystemScheduleTask({
      query: { group: task.group, key: task.key },
      body: { cronExpression: task.cronExpression, enabled: task.enabled },
      throwOnError: true,
    }).then((res) => res.data as ScheduleTask),
  delete: (id: string) =>
    deleteSystemScheduleTaskId({ path: { id }, throwOnError: true }).then((res) => res.data),
}

/**
 * 审批模块视图模型：后端枚举取值、标签文案键与标签色的集中映射。
 * 枚举字符串与 Sim.Approval.Domain.Shared/ApprovalEnums.cs 一一对应。
 */

import { messageKey } from '@/i18n'

export const INSTANCE_STATUSES = [
  'Running',
  'Approved',
  'Rejected',
  'Returned',
  'Withdrawn',
  'Terminated',
] as const

export const TASK_STATUSES = ['Pending', 'Approved', 'Rejected', 'Skipped', 'Canceled'] as const

export const TASK_TYPES = ['Approval', 'AddSignBefore', 'AddSignAfter', 'Transfer'] as const

export const NODE_TYPES = [
  'Approval',
  'Cc',
  'Start',
  'ExclusiveGateway',
  'ParallelGateway',
  'End',
] as const

export const APPROVER_TYPES = [
  'SpecifiedUsers',
  'Role',
  'OrganizationManager',
  'OrganizationMembers',
  'Initiator',
] as const

export const MULTI_APPROVE_MODES = ['All', 'Any', 'Sequential'] as const

export const JOIN_POLICIES = ['AnyArrival', 'AllArrival'] as const

export const NODE_PROGRESS_STATUSES = [
  'NotStarted',
  'Active',
  'Approved',
  'Rejected',
  'Skipped',
  'Canceled',
] as const

export const FLOW_STATUSES = ['Draft', 'Published', 'Disabled'] as const

export const ADD_SIGN_POSITIONS = ['Before', 'After'] as const

/** 处理记录动作，与 ApprovalAction 枚举对应。 */
export const APPROVAL_ACTIONS = [
  'Submit',
  'Approve',
  'Reject',
  'ReturnToPrevious',
  'ReturnToInitiator',
  'Resubmit',
  'Withdraw',
  'Terminate',
  'Transfer',
  'AddSign',
  'Urge',
  'TimeoutNotify',
  'AutoPass',
  'Cc',
] as const

export type TagType = 'default' | 'info' | 'success' | 'warning' | 'error'

const INSTANCE_STATUS_TAG: Record<string, TagType> = {
  Running: 'info',
  Approved: 'success',
  Rejected: 'error',
  Returned: 'warning',
  Withdrawn: 'default',
  Terminated: 'default',
}

const TASK_STATUS_TAG: Record<string, TagType> = {
  Pending: 'info',
  Approved: 'success',
  Rejected: 'error',
  Skipped: 'default',
  Canceled: 'default',
}

const FLOW_STATUS_TAG: Record<string, TagType> = {
  Draft: 'warning',
  Published: 'success',
  Disabled: 'default',
}

const NODE_PROGRESS_TAG: Record<string, TagType> = {
  NotStarted: 'default',
  Active: 'info',
  Approved: 'success',
  Rejected: 'error',
  Skipped: 'default',
  Canceled: 'default',
}

const FALLBACK_LABEL = 'Approval:Enum:Unknown'

/** 实例状态文案键。 */
export const instanceStatusLabelKey = (status?: string | null) =>
  status ? messageKey('approval', 'status', status) : FALLBACK_LABEL

/** 实例状态标签色。 */
export const instanceStatusTagType = (status?: string | null): TagType =>
  (status && INSTANCE_STATUS_TAG[status]) || 'default'

/** 是否为终态实例（终态不再产生待办）。 */
export const isTerminalInstance = (status?: string | null) =>
  status === 'Approved' || status === 'Rejected' || status === 'Withdrawn' || status === 'Terminated'

/** 待办任务状态文案键。 */
export const taskStatusLabelKey = (status?: string | null) =>
  status ? messageKey('approval', 'taskStatus', status) : FALLBACK_LABEL

/** 待办任务状态标签色。 */
export const taskStatusTagType = (status?: string | null): TagType =>
  (status && TASK_STATUS_TAG[status]) || 'default'

/** 任务类型文案键；普通审批任务不额外标注。 */
export const taskTypeLabelKey = (taskType?: string | null) =>
  taskType && taskType !== 'Approval' ? messageKey('approval', 'taskType', taskType) : ''

/** 流程定义状态文案键。 */
export const flowStatusLabelKey = (status?: string | null) =>
  status ? messageKey('approval', 'flowStatus', status) : FALLBACK_LABEL

/** 流程定义状态标签色。 */
export const flowStatusTagType = (status?: string | null): TagType =>
  (status && FLOW_STATUS_TAG[status]) || 'default'

/** 节点类型文案键。 */
export const nodeTypeLabelKey = (nodeType?: string | null) =>
  nodeType ? messageKey('approval', 'nodeType', nodeType) : FALLBACK_LABEL

/** 节点进度状态文案键。 */
export const nodeProgressStatusLabelKey = (status?: string | null) =>
  status ? messageKey('approval', 'nodeStatus', status) : FALLBACK_LABEL

/** 节点进度状态标签色。 */
export const nodeProgressStatusTagType = (status?: string | null): TagType =>
  (status && NODE_PROGRESS_TAG[status]) || 'default'

/** 审批人来源文案键。 */
export const approverTypeLabelKey = (approverType?: string | null) =>
  approverType ? messageKey('approval', 'approverType', approverType) : FALLBACK_LABEL

/** 多人审批模式文案键。 */
export const multiApproveModeLabelKey = (mode?: string | null) =>
  mode ? messageKey('approval', 'multiApproveMode', mode) : FALLBACK_LABEL

/** 汇聚策略文案键（并行 join 由服务端强制为 AllArrival）。 */
export const joinPolicyLabelKey = (policy?: string | null) =>
  policy ? messageKey('approval', 'joinPolicy', policy) : FALLBACK_LABEL

/** 处理记录动作文案键。 */
export const actionLabelKey = (action?: string | null) =>
  action ? messageKey('approval', 'actionType', action) : FALLBACK_LABEL

/** 加签位置文案键。 */
export const addSignPositionLabelKey = (position?: string | null) =>
  position ? messageKey('approval', 'addSignPosition', position) : FALLBACK_LABEL

/** 业务类型文案键：优先取前端资源，缺失时回退到服务端显示名。 */
export const businessTypeLabelKey = (businessType?: string | null) =>
  businessType ? messageKey('approval', 'businessType', businessType) : FALLBACK_LABEL

/**
 * 字段类型是否可用于条件比较；附件字段无标量取值，
 * 与后端 `ApprovalFieldType.IsConditionComparable` 保持一致。
 */
export const isFieldConditionComparable = (fieldType?: string | null) =>
  fieldType !== 'Attachment'
/** Paper Ocean 状态胶囊色调：与 .po-pill--* 修饰类一一对应，空串表示基础白底胶囊。 */
export type StatusTone =
  | ''
  | 'po-pill--sky'
  | 'po-pill--mint'
  | 'po-pill--sun'
  | 'po-pill--coral'
  | 'po-pill--lilac'
  | 'po-pill--outline'

const INSTANCE_STATUS_TONE: Record<string, StatusTone> = {
  Running: 'po-pill--sky',
  Approved: 'po-pill--mint',
  Rejected: 'po-pill--coral',
  Returned: 'po-pill--sun',
  Withdrawn: 'po-pill--outline',
  Terminated: 'po-pill--outline',
}

const TASK_STATUS_TONE: Record<string, StatusTone> = {
  Pending: 'po-pill--sky',
  Approved: 'po-pill--mint',
  Rejected: 'po-pill--coral',
  Skipped: 'po-pill--outline',
  Canceled: 'po-pill--outline',
}

const FLOW_STATUS_TONE: Record<string, StatusTone> = {
  Draft: 'po-pill--sun',
  Published: 'po-pill--mint',
  Disabled: 'po-pill--outline',
}

const NODE_PROGRESS_TONE: Record<string, StatusTone> = {
  NotStarted: 'po-pill--outline',
  Active: 'po-pill--sky',
  Approved: 'po-pill--mint',
  Rejected: 'po-pill--coral',
  Skipped: 'po-pill--outline',
  Canceled: 'po-pill--outline',
}

/** 实例状态胶囊色调。 */
export const instanceStatusToneClass = (status?: string | null): StatusTone =>
  (status && INSTANCE_STATUS_TONE[status]) || ''

/** 待办任务状态胶囊色调。 */
export const taskStatusToneClass = (status?: string | null): StatusTone =>
  (status && TASK_STATUS_TONE[status]) || ''

/** 流程定义状态胶囊色调。 */
export const flowStatusToneClass = (status?: string | null): StatusTone =>
  (status && FLOW_STATUS_TONE[status]) || ''

/** 节点进度状态胶囊色调。 */
export const nodeProgressStatusToneClass = (status?: string | null): StatusTone =>
  (status && NODE_PROGRESS_TONE[status]) || ''

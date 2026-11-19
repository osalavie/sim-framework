import { messageKey, t } from '@/i18n'
import type { TagType } from '../api/approvalModels'
import type {
  ApprovalContextFieldDto,
  ApprovalContextSnapshotDto,
  ApprovalFieldValueDto,
  ApprovalFlowNodeDto,
  ApprovalMonitorQuery,
  ApprovalPendingQuery,
  SaveApprovalFlowNodeDto,
} from '../api'

/** 待办/已办筛选状态（界面层，未提交给接口前的原始值）。 */
export interface ApprovalTaskFilterState {
  keyword: string
  definitionCode: string
  businessType: string
}

/** 实例列表筛选状态；status 为 all 表示不过滤状态。 */
export interface ApprovalInstanceFilterState {
  keyword: string
  status: string
  businessType: string
  definitionCode: string
  initiatorUserId: string
}

/** 分页入参，页码从 1 开始。 */
export interface ApprovalPaginationState {
  page: number
  pageSize: number
}

/** 截止时间进入「临期」的窗口（24 小时）。 */
export const DUE_SOON_WINDOW_MS = 24 * 60 * 60 * 1000

export function createTaskFilter(): ApprovalTaskFilterState {
  return { keyword: '', definitionCode: '', businessType: '' }
}

export function createInstanceFilter(): ApprovalInstanceFilterState {
  return { keyword: '', status: 'all', businessType: '', definitionCode: '', initiatorUserId: '' }
}

/**
 * 待办/已办查询参数：分页换算为 SkipCount/MaxResultCount，空筛选不下发，
 * 避免后端按空字符串过滤。
 */
export function buildTaskQuery(
  filter: ApprovalTaskFilterState,
  pagination: ApprovalPaginationState,
): ApprovalPendingQuery {
  const query: ApprovalPendingQuery = {
    SkipCount: (pagination.page - 1) * pagination.pageSize,
    MaxResultCount: pagination.pageSize,
  }

  const keyword = filter.keyword.trim()
  if (keyword) query.Keyword = keyword

  const definitionCode = filter.definitionCode.trim()
  if (definitionCode) query.DefinitionCode = definitionCode

  const businessType = filter.businessType.trim()
  if (businessType) query.BusinessType = businessType

  return query
}

/** 实例列表查询参数：在待办筛选基础上支持状态与发起人过滤（监控视图使用）。 */
export function buildInstanceQuery(
  filter: ApprovalInstanceFilterState,
  pagination: ApprovalPaginationState,
): ApprovalMonitorQuery {
  const query: ApprovalMonitorQuery = {
    ...buildTaskQuery(filter, pagination),
  }

  if (filter.status !== 'all') query.Status = filter.status

  const initiatorUserId = filter.initiatorUserId.trim()
  if (initiatorUserId) query.InitiatorUserId = initiatorUserId

  return query
}

/** 枚举下拉选项：值取后端枚举名，标签走 i18n。 */
export function enumOptions(values: readonly string[], labelKey: (value: string) => string) {
  return values.map((value) => ({ value, label: t(labelKey(value)) }))
}

/**
 * 解析业务字段文本为字段值数组：每行 `键=值`，忽略空行与缺少分隔符的行。
 * 用于重新提交表单的字段输入，提交后由服务端按业务类型声明补齐展示元数据。
 */
export function parseContextInput(value: string): ApprovalFieldValueDto[] {
  const fields: ApprovalFieldValueDto[] = []

  for (const line of value.split(/\r?\n/)) {
    const separator = line.indexOf('=')
    if (separator <= 0) continue

    const key = line.slice(0, separator).trim()
    if (!key) continue
    fields.push({ key, value: line.slice(separator + 1).trim() })
  }

  return fields
}

/** 将实例字段快照格式化为 `键=值` 多行文本，供重新提交表单回填。 */
export function formatContextInput(
  snapshot: ApprovalContextSnapshotDto | null | undefined,
): string {
  return (snapshot?.fields ?? [])
    .filter((field) => Boolean(field.key))
    .map((field) => `${field.key}=${field.value ?? ''}`)
    .join('\n')
}

/**
 * 业务字段快照 → 详情页展示项；字段名优先取声明标签，缺失时回退字段标识，
 * 使旧实例（扁平字典迁移而来）与未声明字段同样可渲染。
 */
export function contextFields(
  snapshot: ApprovalContextSnapshotDto | null | undefined,
): { key: string; displayName: string; value: string }[] {
  return (snapshot?.fields ?? [])
    .filter((field) => Boolean(field.key))
    .map((field) => ({
      key: field.key as string,
      displayName: field.displayName?.trim() || (field.key as string),
      value: fieldValueText(field),
    }))
}

/** 字段取值文案：枚举字段按声明的选项标签展示，未命中时回退原始取值。 */
function fieldValueText(field: ApprovalContextFieldDto): string {
  const value = field.value ?? ''
  if (!value) return ''

  return field.options?.find((option) => option.value === value)?.label?.trim() || value
}

/** 截止状态：none 未设置、normal 正常、dueSoon 临期、overdue 已超时。 */
export type ApprovalDueState = 'none' | 'normal' | 'dueSoon' | 'overdue'

/** 实例级操作：撤回（发起人）、终止（监控）、重新提交（退回后发起人）。 */
export type InstanceActionMode = 'withdraw' | 'terminate' | 'resubmit'

const DUE_STATE_TAG: Record<ApprovalDueState, TagType> = {
  none: 'default',
  normal: 'default',
  dueSoon: 'warning',
  overdue: 'error',
}

/** 截止状态文案键。 */
export const dueStateLabelKey = (state: ApprovalDueState) => messageKey('approval', 'due', state)

/** 截止状态标签色。 */
export const dueStateTagType = (state: ApprovalDueState): TagType => DUE_STATE_TAG[state]

export function resolveDueState(dueAt?: string | null, now: number = Date.now()): ApprovalDueState {
  if (!dueAt) return 'none'

  const due = new Date(dueAt).getTime()
  if (Number.isNaN(due)) return 'none'
  if (due < now) return 'overdue'
  return due - now <= DUE_SOON_WINDOW_MS ? 'dueSoon' : 'normal'
}

/** 条件规则草稿（编辑器界面模型，对应后端 ConditionRule）。 */
export interface ConditionRuleDraft {
  field: string
  operator: string
  value: string
}

/** 节点激活条件草稿；rules 为空表示无条件激活。 */
export interface ConditionDraft {
  /** all 或 any；序列化时非 any 一律按 all 处理。 */
  match: string
  rules: ConditionRuleDraft[]
}

export const CONDITION_MATCHES = ['all', 'any'] as const

/** 与后端 ApprovalConditionEvaluator 支持的运算符一一对应。 */
export const CONDITION_OPERATORS = [
  'equals',
  'notEquals',
  'greaterThan',
  'greaterOrEqual',
  'lessThan',
  'lessOrEqual',
  'contains',
  'notContains',
  'in',
  'notIn',
  'isEmpty',
  'isNotEmpty',
  'isTrue',
  'isFalse',
] as const

/** 无需填写比较值的运算符。 */
const VALUELESS_OPERATORS = new Set(['isEmpty', 'isNotEmpty', 'isTrue', 'isFalse'])

export function conditionOperatorNeedsValue(operator: string): boolean {
  return !VALUELESS_OPERATORS.has(operator)
}

export const conditionMatchLabelKey = (match: string) => messageKey('approval', 'condition', 'match', match)

export const conditionOperatorLabelKey = (operator: string) =>
  messageKey('approval', 'condition', 'operator', operator)

export function createConditionDraft(): ConditionDraft {
  return { match: 'all', rules: [] }
}

/**
 * 解析条件 JSON 为编辑器草稿；结构非法时返回空草稿（保存侧仍由服务端做最终校验），
 * 避免编辑器因历史脏数据崩溃。
 */
export function parseCondition(conditionJson?: string | null): ConditionDraft {
  if (!conditionJson?.trim()) return createConditionDraft()

  try {
    const parsed = JSON.parse(conditionJson) as {
      match?: string
      rules?: { field?: string; operator?: string; value?: string }[]
    }

    return {
      match: parsed.match === 'any' ? 'any' : 'all',
      rules: (parsed.rules ?? []).map((rule) => ({
        field: rule.field ?? '',
        operator: rule.operator ?? 'equals',
        value: rule.value ?? '',
      })),
    }
  } catch {
    return createConditionDraft()
  }
}

/** 序列化条件草稿；无有效规则时返回 null（视为无条件激活）。 */
export function formatCondition(draft: ConditionDraft): string | null {
  const rules = draft.rules
    .filter((rule) => rule.field.trim())
    .map((rule) => ({
      field: rule.field.trim(),
      operator: rule.operator,
      ...(conditionOperatorNeedsValue(rule.operator) ? { value: rule.value } : {}),
    }))

  if (!rules.length) return null

  return JSON.stringify({ match: draft.match, rules })
}

/** 抄送配置草稿（对应后端 ApprovalCcConfiguration）。 */
export interface CcConfigDraft {
  userIds: string[]
  roles: string[]
  initiator: boolean
}

export function createCcConfigDraft(): CcConfigDraft {
  return { userIds: [], roles: [], initiator: false }
}

export function parseCcConfig(ccConfigJson?: string | null): CcConfigDraft {
  if (!ccConfigJson?.trim()) return createCcConfigDraft()

  try {
    const parsed = JSON.parse(ccConfigJson) as {
      userIds?: string[]
      roles?: string[]
      initiator?: boolean
    }

    return {
      userIds: parsed.userIds ?? [],
      roles: parsed.roles ?? [],
      initiator: Boolean(parsed.initiator),
    }
  } catch {
    return createCcConfigDraft()
  }
}

/** 序列化抄送配置；未配置任何抄送对象时返回 null。 */
export function formatCcConfig(draft: CcConfigDraft): string | null {
  const userIds = draft.userIds.filter(Boolean)
  const roles = draft.roles.map((role) => role.trim()).filter(Boolean)

  if (!userIds.length && !roles.length && !draft.initiator) return null

  return JSON.stringify({ userIds, roles, initiator: draft.initiator })
}

/** 抄送配置是否为空（用于提交前校验抄送节点）。 */
export function isCcConfigEmpty(draft: CcConfigDraft): boolean {
  return !draft.userIds.filter(Boolean).length && !draft.roles.filter(Boolean).length && !draft.initiator
}

/** 流程节点编辑草稿（编辑器界面模型）。 */
export interface ApprovalFlowNodeDraft {
  nodeKey: string
  name: string
  nodeType: string
  approverType: string
  approverUserIds: string[]
  approverRoleName: string
  /** 审批组织（ApproverType = OrganizationManager / OrganizationMembers 时生效）。 */
  approverOrganizationId: string | null
  multiApproveMode: string
  /** 汇聚策略：AnyArrival（任一路到达）/ AllArrival（全部入边到达）；并行 join 由服务端强制为 AllArrival。 */
  joinPolicy: string
  condition: ConditionDraft
  allowTransfer: boolean
  allowAddSign: boolean
  allowReturnToInitiator: boolean
  timeoutHours: number | null
  ccConfig: CcConfigDraft
}

export const DEFAULT_NODE_APPROVER_TYPE = 'SpecifiedUsers'

/** 审批人来源是否基于所选组织（组织负责人 / 组织人员）。 */
export const isOrganizationApproverType = (approverType: string) =>
  approverType === 'OrganizationManager' || approverType === 'OrganizationMembers'

export const DEFAULT_NODE_MULTI_APPROVE_MODE = 'Any'

export const DEFAULT_NODE_JOIN_POLICY = 'AnyArrival'

export function createNodeDraft(index: number): ApprovalFlowNodeDraft {
  return {
    nodeKey: `node${index}`,
    name: '',
    nodeType: 'Approval',
    approverType: DEFAULT_NODE_APPROVER_TYPE,
    approverUserIds: [],
    approverRoleName: '',
    approverOrganizationId: null,
    multiApproveMode: DEFAULT_NODE_MULTI_APPROVE_MODE,
    joinPolicy: DEFAULT_NODE_JOIN_POLICY,
    condition: createConditionDraft(),
    allowTransfer: true,
    allowAddSign: true,
    allowReturnToInitiator: true,
    timeoutHours: null,
    ccConfig: createCcConfigDraft(),
  }
}

export function nodeDraftFromDto(dto: ApprovalFlowNodeDto): ApprovalFlowNodeDraft {
  return {
    nodeKey: dto.nodeKey ?? '',
    name: dto.name ?? '',
    nodeType: dto.nodeType ?? 'Approval',
    approverType: dto.approverType ?? DEFAULT_NODE_APPROVER_TYPE,
    approverUserIds: dto.approverUserIds ?? [],
    approverRoleName: dto.approverRoleName ?? '',
    approverOrganizationId: dto.approverOrganizationId ?? null,
    multiApproveMode: dto.multiApproveMode ?? DEFAULT_NODE_MULTI_APPROVE_MODE,
    joinPolicy: dto.joinPolicy ?? DEFAULT_NODE_JOIN_POLICY,
    condition: parseCondition(dto.conditionJson),
    allowTransfer: Boolean(dto.allowTransfer),
    allowAddSign: Boolean(dto.allowAddSign),
    allowReturnToInitiator: Boolean(dto.allowReturnToInitiator),
    timeoutHours: dto.timeoutHours ?? null,
    ccConfig: parseCcConfig(dto.ccConfigJson),
  }
}

/** 草稿 → 保存入参；节点顺序与生效字段由服务端再次校验。 */
export function nodeDraftToSaveDto(draft: ApprovalFlowNodeDraft): SaveApprovalFlowNodeDto {
  const isCcNode = draft.nodeType === 'Cc'

  return {
    nodeKey: draft.nodeKey.trim(),
    name: draft.name.trim(),
    nodeType: draft.nodeType,
    approverType: draft.approverType,
    approverUserIds: isCcNode ? [] : draft.approverUserIds,
    approverRoleName:
      !isCcNode && draft.approverType === 'Role' ? draft.approverRoleName.trim() || null : null,
    approverOrganizationId:
      !isCcNode && isOrganizationApproverType(draft.approverType)
        ? draft.approverOrganizationId
        : null,
    multiApproveMode: draft.multiApproveMode,
    joinPolicy: draft.joinPolicy || DEFAULT_NODE_JOIN_POLICY,
    conditionJson: formatCondition(draft.condition),
    allowTransfer: draft.allowTransfer,
    allowAddSign: draft.allowAddSign,
    allowReturnToInitiator: draft.allowReturnToInitiator,
    timeoutHours: draft.timeoutHours ?? null,
    ccConfigJson: isCcNode ? formatCcConfig(draft.ccConfig) : null,
  }
}

/** 节点草稿校验，返回提示文案的 i18n 键；通过时返回 null。 */
export function validateNodeDraft(draft: ApprovalFlowNodeDraft): string | null {
  if (!draft.name.trim()) return 'Approval:Definition:NodeError:Name'
  if (!draft.nodeKey.trim()) return 'Approval:Definition:NodeError:NodeKey'

  if (draft.nodeType === 'Cc') {
    return isCcConfigEmpty(draft.ccConfig) ? 'Approval:Definition:NodeError:CcTarget' : null
  }

  // 开始/网关/结束节点不承载审批人配置（与后端 ValidateNode 一致）。
  if (draft.nodeType !== 'Approval') return null

  if (draft.approverType === 'SpecifiedUsers' && !draft.approverUserIds.length) {
    return 'Approval:Definition:NodeError:Approvers'
  }

  if (draft.approverType === 'Role' && !draft.approverRoleName.trim()) {
    return 'Approval:Definition:NodeError:Role'
  }

  if (isOrganizationApproverType(draft.approverType) && !draft.approverOrganizationId) {
    return 'Approval:Definition:NodeError:Organization'
  }

  return null
}
/** 截止时间状态的 Paper Ocean 胶囊色调。 */
export const dueStateToneClass = (state: ApprovalDueState): string =>
  state === 'overdue' ? 'po-pill--coral' : state === 'dueSoon' ? 'po-pill--sun' : ''

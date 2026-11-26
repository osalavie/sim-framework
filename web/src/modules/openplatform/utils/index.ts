import type { OpenPlatformAccessLogQuery, OpenPlatformScope } from '../api'

/** 访问日志筛选状态（界面层，未提交给接口前的原始值）。 */
export interface AccessLogFilterState {
  appId: string
  /** 签发结果：all 不过滤。 */
  succeeded: 'all' | 'succeeded' | 'failed'
  clientIp: string
  /** 起始时间，null 表示不过滤。 */
  startTime: string | null
  /** 结束时间，null 表示不过滤。 */
  endTime: string | null
}

export function createAccessLogFilter(): AccessLogFilterState {
  return { appId: '', succeeded: 'all', clientIp: '', startTime: null, endTime: null }
}

/**
 * 解析多行 / 逗号 / 分号分隔的文本为去重列表。
 * 用于应用表单的来源 IP 白名单输入。
 */
export function parseListInput(value: string): string[] {
  const items: string[] = []
  const seen = new Set<string>()

  for (const segment of value.split(/[\n,，;；]+/)) {
    const item = segment.trim()
    if (!item || seen.has(item)) continue
    seen.add(item)
    items.push(item)
  }

  return items
}

/** 将列表格式化为多行文本，供编辑表单回填。 */
export function formatListInput(values: readonly string[] | null | undefined): string {
  return (values ?? []).join('\n')
}

/** 授权范围下拉选项：标签展示「权限名称（权限码）」。 */
export interface ScopeOption {
  label: string
  value: string
}

/** 授权范围下拉分组：按权限码的模块段归类（system / blog / openplatform）。 */
export interface ScopeGroup {
  module: string
  options: ScopeOption[]
}

/** 无法从权限码解析模块段时的兜底分组键。 */
export const SCOPE_GROUP_OTHER = 'other'

/**
 * 将权限清单转换为按模块分组的下拉选项：去掉空权限码并按权限码去重，组内与组间保持服务端排序。
 * 找不到名称（或名称与权限码相同）时只展示权限码。
 */
export function buildScopeGroups(
  scopes: readonly OpenPlatformScope[] | null | undefined,
): ScopeGroup[] {
  const groups: ScopeGroup[] = []
  const groupsByModule = new Map<string, ScopeGroup>()
  const seen = new Set<string>()

  for (const scope of scopes ?? []) {
    const code = scope.code.trim()
    if (!code || seen.has(code)) continue
    seen.add(code)

    const module = /^([a-z][a-z0-9-]*):/.exec(code)?.[1] ?? SCOPE_GROUP_OTHER

    let group = groupsByModule.get(module)
    if (!group) {
      group = { module, options: [] }
      groupsByModule.set(module, group)
      groups.push(group)
    }

    const name = scope.name.trim()
    group.options.push({
      label: name && name !== code ? `${name}（${code}）` : code,
      value: code,
    })
  }

  return groups
}

/**
 * 构建访问日志查询参数：分页 + 非空筛选条件。
 * 空筛选不下发，避免后端按空字符串做过滤。
 */
export function buildAccessLogQuery(
  filter: AccessLogFilterState,
  pagination: { page: number; pageSize: number },
): OpenPlatformAccessLogQuery {
  const query: OpenPlatformAccessLogQuery = {
    skipCount: (pagination.page - 1) * pagination.pageSize,
    maxResultCount: pagination.pageSize,
  }

  const appId = filter.appId.trim()
  if (appId) query.appId = appId

  if (filter.succeeded !== 'all') {
    query.succeeded = filter.succeeded === 'succeeded'
  }

  const clientIp = filter.clientIp.trim()
  if (clientIp) query.clientIp = clientIp

  const startTime = filter.startTime?.trim() ?? ''
  if (startTime) query.startTime = startTime

  const endTime = filter.endTime?.trim() ?? ''
  if (endTime) query.endTime = endTime

  return query
}

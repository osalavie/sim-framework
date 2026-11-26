/** 将多选结果写回路由元数据：去空白、去重后以逗号连接。 */
export function joinPermissionCodes(codes?: readonly string[] | null): string {
  const normalized: string[] = []
  const seen = new Set<string>()

  for (const raw of codes ?? []) {
    const code = raw.trim()
    if (!code || seen.has(code)) continue
    seen.add(code)
    normalized.push(code)
  }

  return normalized.join(',')
}

/** 权限码下拉选项：label 用于选项与搜索（「权限名称（权限码）」），name 用于选中标签。 */
export interface PermissionCodeOption {
  label: string
  value: string
  name?: string
}

/** 权限码下拉分组：按权限码的模块段归类。 */
export interface PermissionCodeGroup {
  module: string
  options: PermissionCodeOption[]
}

const SUPER_PERMISSION_CODE = '*:*:*'
export const PERMISSION_CODE_GROUP_OTHER = 'other'

/**
 * 将权限清单转换为按模块分组的下拉选项：去掉空权限码并按权限码去重，组内与组间保持服务端排序。
 * 找不到名称（或名称与权限码相同）时只展示权限码。
 */
export function buildPermissionCodeGroups(
  permissions: readonly { code?: string | null; name?: string | null }[] | null | undefined,
): PermissionCodeGroup[] {
  const groups: PermissionCodeGroup[] = []
  const groupsByModule = new Map<string, PermissionCodeGroup>()
  const seen = new Set<string>()

  for (const permission of permissions ?? []) {
    const code = permission.code?.trim()
    if (!code || seen.has(code)) continue
    seen.add(code)

    const module =
      code === SUPER_PERMISSION_CODE
        ? 'super'
        : (/^([a-z][a-z0-9-]*):/.exec(code)?.[1] ?? PERMISSION_CODE_GROUP_OTHER)

    let group = groupsByModule.get(module)
    if (!group) {
      group = { module, options: [] }
      groupsByModule.set(module, group)
      groups.push(group)
    }

    const name = permission.name?.trim()
    group.options.push({
      label: name && name !== code ? `${name}（${code}）` : code,
      value: code,
      name: name && name !== code ? name : undefined,
    })
  }

  return groups
}

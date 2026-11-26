/**
 * 路由元数据中的权限码支持逗号分隔的“任一满足”语义。
 */
export function splitPermissionCodes(value?: string | null): string[] {
  if (!value) return []
  return value
    .split(',')
    .map((code) => code.trim())
    .filter(Boolean)
}

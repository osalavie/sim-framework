import { reactive, watch } from 'vue'
import { getSystemUserDisplayNames } from '@/api/system'

export interface AuditFields {
  creationTime?: string | null
  creatorId?: string | null
  lastModificationTime?: string | null
  lastModifierId?: string | null
}

interface AuditNameEntry {
  name: string
  account?: string
}

const BATCH_SIZE = 200

const nameCache = reactive<Record<string, AuditNameEntry>>({})
const pendingIds = reactive(new Set<string>())
const failedIds = new Set<string>()

function loadNames(ids: string[]) {
  const missing = Array.from(new Set(ids)).filter(
    (id) => !nameCache[id] && !pendingIds.has(id) && !failedIds.has(id),
  )
  for (let offset = 0; offset < missing.length; offset += BATCH_SIZE) {
    const batch = missing.slice(offset, offset + BATCH_SIZE)
    batch.forEach((id) => pendingIds.add(id))
    getSystemUserDisplayNames({
      query: { userIds: batch },
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
    })
      .then((response) => {
        Object.entries(response.data ?? {}).forEach(([id, identity]) => {
          const name = identity?.displayName?.trim()
          if (!name) return
          nameCache[id] = { name, account: identity.userName?.trim() || undefined }
        })
      })
      .catch(() => {
        batch.forEach((id) => failedIds.add(id))
      })
      .finally(() => {
        batch.forEach((id) => pendingIds.delete(id))
      })
  }
}

/**
 * 批量解析创建人/修改人显示名（昵称优先，空时回退账号名）与账号名。表格渲染时按 ID 读取响应式缓存，
 * 名称加载完成后对应单元格会自动重新渲染。
 */
export function useAuditNames(ids: () => Array<string | null | undefined>) {
  watch(
    () => ids().filter((id): id is string => Boolean(id)),
    (list) => loadNames(list),
    { immediate: true },
  )

  const resolveName = (id?: string | null) => {
    if (!id) return '-'
    return nameCache[id]?.name ?? (pendingIds.has(id) ? '…' : id)
  }

  /** 悬停气泡展示的账号名；未解析或解析失败时为空，调用方据此不展示气泡。 */
  const resolveAccount = (id?: string | null) => (id ? nameCache[id]?.account : undefined)

  return { resolveName, resolveAccount }
}

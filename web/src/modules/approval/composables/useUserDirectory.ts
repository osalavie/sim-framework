import { computed, ref } from 'vue'
import { approvalDefinitionApi } from '../api'

const DIRECTORY_PAGE_SIZE = 50
const SEARCH_DELAY_MS = 250

export interface DirectoryUser {
  id: string
  label: string
  userName: string
}

const defaultUsers = ref<DirectoryUser[]>([])
const labels = ref<Record<string, string>>({})
let defaultPending: Promise<DirectoryUser[]> | null = null
let defaultLoaded = false

/** 通过审批模块的最小用户目录查询，避免依赖系统用户管理权限。 */
async function requestDirectory(keyword = '') {
  const items = await approvalDefinitionApi.users(keyword.trim(), DIRECTORY_PAGE_SIZE)
  const result = items
    .filter((user) => Boolean(user.id))
    .map((user) => ({
      id: user.id as string,
      label: user.displayName || user.userName || (user.id as string).slice(0, 8),
      userName: user.userName ?? '',
    }))
  labels.value = {
    ...labels.value,
    ...Object.fromEntries(result.map((user) => [user.id, user.label])),
  }
  return result
}

/**
 * 共享用户目录（模块级缓存）：审批人选择、发起人与处理人名称展示共用，
 * 首次使用时拉取一次，同时发起的调用复用同一个请求。
 */
export function useUserDirectory() {
  const users = ref<DirectoryUser[]>(defaultUsers.value)
  const loading = ref(false)
  let requestVersion = 0
  let searchTimer: ReturnType<typeof setTimeout> | null = null

  const ensureLoaded = async () => {
    if (defaultLoaded) {
      users.value = defaultUsers.value
      return
    }

    loading.value = true
    try {
      defaultPending ??= requestDirectory().then((result) => {
        defaultUsers.value = result
        defaultLoaded = true
        return result
      })
      users.value = await defaultPending
    } catch {
      users.value = []
    } finally {
      defaultPending = null
      loading.value = false
    }
  }

  const search = (keyword: string) => {
    if (searchTimer) clearTimeout(searchTimer)
    searchTimer = setTimeout(() => {
      const currentVersion = ++requestVersion
      loading.value = true
      void requestDirectory(keyword)
        .then((result) => {
          if (currentVersion === requestVersion) users.value = result
        })
        .catch(() => {
          if (currentVersion === requestVersion) users.value = []
        })
        .finally(() => {
          if (currentVersion === requestVersion) loading.value = false
        })
    }, SEARCH_DELAY_MS)
  }

  const label = (userId?: string | null) =>
    userId ? (labels.value[userId] ?? userId.slice(0, 8)) : ''

  const options = computed(() => users.value.map((user) => ({ label: user.label, value: user.id })))

  return { users, loading, options, label, ensureLoaded, search }
}

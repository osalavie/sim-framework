import { readonly, ref } from 'vue'
import { fetchSetupStatus, type SetupStatus } from '@/api/setup'

const initializedState = ref(true)
let statusPromise: Promise<boolean> | null = null

export const setupInitialized = readonly(initializedState)

/**
 * 读取系统初始化状态。接口不可用时视为已初始化，避免把整个站点锁在初始化页。
 */
export function refreshSetupStatus(): Promise<boolean> {
  if (statusPromise) return statusPromise

  statusPromise = fetchSetupStatus()
    .then((status) => {
      initializedState.value = status.initialized !== false
      return initializedState.value
    })
    .catch(() => {
      initializedState.value = true
      return true
    })

  return statusPromise
}

export function resetSetupStatus(): void {
  statusPromise = null
}

export async function loadSetupStatus(): Promise<SetupStatus | null> {
  try {
    const status = await fetchSetupStatus()
    initializedState.value = status.initialized !== false
    return status
  } catch {
    return null
  }
}

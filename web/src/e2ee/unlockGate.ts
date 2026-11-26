import { reactive } from 'vue'
import { E2eeCancelledError } from './types'

/** encrypt 用于新启用加密前的身份验证；unlock 用于读取或保存已加密内容。 */
export type E2eeUnlockPurpose = 'unlock' | 'encrypt'

interface UnlockGateState {
  visible: boolean
  reason: string
  purpose: E2eeUnlockPurpose
}

const state = reactive<UnlockGateState>({ visible: false, reason: '', purpose: 'unlock' })

interface PendingUnlock {
  promise: Promise<void>
  resolve: () => void
  reject: (error: unknown) => void
}

let pending: PendingUnlock | null = null

/** 打开全局解锁宿主；同一时刻只允许一个解锁请求，更严格的加密授权会排在读取请求之后。 */
export function requestE2eeUnlock(reason = '', purpose: E2eeUnlockPurpose = 'unlock'): Promise<void> {
  if (pending) {
    if (purpose === 'encrypt' && state.purpose !== 'encrypt') {
      return pending.promise.then(() => requestE2eeUnlock(reason, purpose))
    }
    return pending.promise
  }

  let resolve!: () => void
  let reject!: (error: unknown) => void
  const promise = new Promise<void>((res, rej) => {
    resolve = res
    reject = rej
  })

  pending = { promise, resolve, reject }
  state.purpose = purpose
  state.reason = reason
  state.visible = true
  return promise
}

export function completeE2eeUnlock(): void {
  state.visible = false
  state.reason = ''
  const current = pending
  pending = null
  current?.resolve()
}

export function cancelE2eeUnlock(): void {
  state.visible = false
  state.reason = ''
  const current = pending
  pending = null
  current?.reject(new E2eeCancelledError())
}

export function useE2eeUnlockGateState(): UnlockGateState {
  return state
}

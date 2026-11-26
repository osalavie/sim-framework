import { clearDeviceCache, loadDeviceCache } from './deviceStore'
import { E2eeCancelledError, E2eeLockedError } from './types'
import { requestE2eeUnlock } from './unlockGate'
import { cancelE2eeKeyOperations } from './keysAsync'

let umk: Uint8Array | null = null
let boundUserId: string | null = null
let keyVersion = 1
let unlockEpoch = 0

export function isE2eeUnlocked(targetUserId?: string): boolean {
  return umk !== null && (!targetUserId || boundUserId === targetUserId)
}

/** 只读取当前已解锁的 UMK；不会触发解锁面板，供列表/工作台降级显示使用。 */
export function getE2eeUmkForUser(userId: string): Uint8Array | null {
  return umk && boundUserId === userId ? umk : null
}

export function getE2eeUmk(): Uint8Array {
  if (!umk) {
    throw new E2eeLockedError()
  }
  return umk
}

export function getE2eeKeyVersion(): number {
  return keyVersion
}

export function setE2eeUmk(value: Uint8Array, userId: string, version = 1): void {
  lockE2eeVault()
  umk = value.slice()
  boundUserId = userId
  keyVersion = version
}

export function lockE2eeVault(): void {
  unlockEpoch += 1
  cancelE2eeKeyOperations()
  umk?.fill(0)
  umk = null
  boundUserId = null
  keyVersion = 1
}

export async function clearE2eeDeviceCache(userId?: string): Promise<void> {
  await clearDeviceCache(userId)
}

async function tryUnlockFromDevice(userId: string): Promise<boolean> {
  const epoch = unlockEpoch
  let cache
  try {
    cache = await loadDeviceCache(userId)
  } catch (error) {
    console.warn('E2EE device cache read failed:', error)
    return false
  }
  if (!cache) return false
  if (epoch !== unlockEpoch) { cache.umk.fill(0); return isE2eeUnlocked(userId) }

  try {
    if (cache.userId !== userId || cache.umk.length !== 16) throw new Error('Invalid cached UMK.')
    const value = cache.umk
    setE2eeUmk(value, userId, cache.keyVersion)
    value.fill(0)
    cache.umk.fill(0)
    return true
  } catch (error) {
    console.warn('E2EE device cache decryption failed, clearing cache:', error)
    cache.umk.fill(0)
    await clearE2eeDeviceCache(userId)
    return false
  }
}

/** 需要 UMK 的高层流程先调用它；本机缓存可用时静默解锁，否则弹出口令/恢复密钥面板。 */
export async function ensureE2eeUnlocked(userId: string): Promise<Uint8Array> {
  if (umk && boundUserId === userId) {
    return umk
  }

  const epoch = unlockEpoch
  if (await tryUnlockFromDevice(userId)) {
    return getE2eeUmk()
  }
  if (epoch !== unlockEpoch) throw new E2eeCancelledError()

  await requestE2eeUnlock()
  if (!umk || boundUserId !== userId) {
    throw new E2eeLockedError()
  }
  return umk
}

/** 列表类界面使用：只尝试本机缓存静默解锁，绝不弹面板。 */
export async function tryE2eeDeviceUnlock(userId: string): Promise<Uint8Array | null> {
  if (umk && boundUserId === userId) {
    return umk
  }
  return (await tryUnlockFromDevice(userId)) ? umk : null
}

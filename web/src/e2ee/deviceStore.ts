/**
 * “信任此设备”缓存：UMK 由不可导出的 CryptoKey 直接加密保存。
 * IndexedDB 只在当前浏览器有效；用户取消勾选或清除站点数据后删除。
 * 存在缓存时优先静默解锁，通行密钥只在没有缓存时作为显式解锁方式。
 * 不可导出仅限制标准 API 导出；不保证硬件绑定，不能抵御同源恶意脚本调用解密。
 */
import { protectDeviceCache, unprotectDeviceCache, migrateLegacyDeviceCache } from './deviceCacheCipher'
import type { DeviceCacheRecord, ProtectedDeviceCacheRecord, LegacyDeviceCacheRecord, ProtectedLegacyDeviceCacheRecord } from './deviceCacheCipher'
import { getE2eeProfile } from './profile'
export type { DeviceCacheRecord } from './deviceCacheCipher'

const STORE_NAME = 'device-umk'
const PREFERENCES_STORE = 'device-preferences'
const DATABASE_VERSION = 3

let cacheEpoch = 0

export function isDeviceCacheAvailable(): boolean {
  return typeof indexedDB !== 'undefined'
}

export async function saveDeviceCache(record: DeviceCacheRecord): Promise<void> {
  if (!isDeviceCacheAvailable()) throw new Error('Trusted-device storage is unavailable.')
  const epoch = cacheEpoch
  const protectedRecord = await protectDeviceCache(record)
  if (epoch !== cacheEpoch) throw new Error('Trusted-device storage was cleared.')
  const database = await openDatabase()
  try {
    if (epoch !== cacheEpoch) throw new Error('Trusted-device storage was cleared.')
    await runTransaction(database, 'readwrite', (store) => store.put(protectedRecord))
  } finally {
    database.close()
  }
}

export async function loadDeviceCache(userId: string): Promise<DeviceCacheRecord | null> {
  if (!isDeviceCacheAvailable()) return null
  const epoch = cacheEpoch
  const database = await openDatabase()
  let result: LegacyDeviceCacheRecord | ProtectedLegacyDeviceCacheRecord | ProtectedDeviceCacheRecord | undefined
  try {
    result = await runTransaction<typeof result>(
      database,
      'readonly',
      (store) => store.get(userId),
    )
  } finally {
    database.close()
  }
  if (!result) return null
  let record: DeviceCacheRecord | undefined
  try {
    if (epoch !== cacheEpoch) {
      if ('deviceKey' in result && result.deviceKey instanceof Uint8Array) result.deviceKey.fill(0)
      return null
    }
    if (result.userId !== userId) throw new Error('Trusted-device owner mismatch.')
    if ('format' in result && result.format === 'device-cache-v3') {
      record = await unprotectDeviceCache(result)
    } else {
      record = await migrateLegacyDeviceCache(result as LegacyDeviceCacheRecord | ProtectedLegacyDeviceCacheRecord)
      await saveDeviceCache(record)
    }
    if (epoch !== cacheEpoch) { record.umk.fill(0); return null }
    return record
  } catch {
    record?.umk.fill(0)
    if ('deviceKey' in result && result.deviceKey instanceof Uint8Array) result.deviceKey.fill(0)
    if (epoch === cacheEpoch) await clearDeviceCache(userId)
    return null
  }
}

/** 只判断本机是否信任了当前用户，不把 UMK 留在调用方。 */
export async function hasDeviceCache(userId: string): Promise<boolean> {
  const record = await loadDeviceCache(userId)
  if (!record) return false
  record.umk.fill(0)
  return true
}

export async function clearDeviceCache(userId?: string): Promise<void> {
  cacheEpoch += 1
  if (!isDeviceCacheAvailable()) return
  const database = await openDatabase()
  try {
    await runTransaction(database, 'readwrite', (store) =>
      userId ? store.delete(userId) : store.clear(),
    )
  } finally {
    database.close()
  }
}

/** Non-secret browser preference; passkey UMK wraps remain on the server. */
export async function loadPasskeyPreference(userId: string): Promise<string | null> {
  if (!isDeviceCacheAvailable()) return null
  const database = await openDatabase()
  try {
    const record = await runTransaction<{ userId: string; passkeyId: string } | undefined>(database, 'readonly',
      (store) => store.get(userId), PREFERENCES_STORE)
    return record?.userId === userId && typeof record.passkeyId === 'string' ? record.passkeyId : null
  } finally { database.close() }
}

export async function selectPasskeyUnlock(userId: string, passkeyId: string | null): Promise<void> {
  // Invalidate old auto-unlock writes before selecting the verification-required path.
  await clearDeviceCache(userId)
  const database = await openDatabase()
  try {
    await runTransaction(database, 'readwrite', (store) => passkeyId
      ? store.put({ userId, passkeyId }) : store.delete(userId), PREFERENCES_STORE)
  } finally { database.close() }
  // Cover a save started between the first clear and preference commit.
  await clearDeviceCache(userId)
}

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    let blocked = false
    const request = indexedDB.open(getE2eeProfile().deviceDatabaseName, DATABASE_VERSION)
    request.onupgradeneeded = () => {
      const database = request.result
      if (!database.objectStoreNames.contains(STORE_NAME)) {
        database.createObjectStore(STORE_NAME, { keyPath: 'userId' })
      }
      if (!database.objectStoreNames.contains(PREFERENCES_STORE)) {
        database.createObjectStore(PREFERENCES_STORE, { keyPath: 'userId' })
      }
    }
    request.onsuccess = () => {
      const database = request.result
      if (blocked) { database.close(); return }
      database.onversionchange = () => database.close()
      resolve(database)
    }
    request.onblocked = () => {
      blocked = true
      reject(new Error('Close older site tabs before trusting this device.'))
    }
    request.onerror = () => reject(request.error ?? new Error('Failed to open E2EE device cache.'))
  })
}

function runTransaction<T = undefined>(
  database: IDBDatabase,
  mode: IDBTransactionMode,
  operation: (store: IDBObjectStore) => IDBRequest,
  storeName = STORE_NAME,
): Promise<T> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(storeName, mode)
    const request = operation(transaction.objectStore(storeName))
    // A request succeeding does not guarantee that its containing transaction committed.
    transaction.oncomplete = () => resolve(request.result as T)
    request.onerror = () => reject(request.error ?? new Error('E2EE device cache request failed.'))
    transaction.onerror = () =>
      reject(transaction.error ?? new Error('E2EE device cache transaction failed.'))
    transaction.onabort = () =>
      reject(transaction.error ?? new Error('E2EE device cache transaction aborted.'))
  })
}

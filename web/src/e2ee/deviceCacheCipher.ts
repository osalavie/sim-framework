import { unwrapUmkWithDeviceKey } from './keys'
import { getE2eeProfile } from './profile'

export interface DeviceCacheRecord {
  userId: string
  keyVersion: number
  umk: Uint8Array
}

export interface ProtectedDeviceCacheRecord {
  format: 'device-cache-v3'
  userId: string
  keyVersion: number
  wrappingKey: CryptoKey
  nonce: Uint8Array<ArrayBuffer>
  encryptedUmk: ArrayBuffer
}

function aad(record: Pick<DeviceCacheRecord, 'userId' | 'keyVersion'>): Uint8Array<ArrayBuffer> {
  if (!record.userId || !Number.isSafeInteger(record.keyVersion) || record.keyVersion < 1)
    throw new Error('Invalid trusted-device cache metadata.')
  return new TextEncoder().encode(JSON.stringify([`${getE2eeProfile().deviceCacheAadPrefix}-v3`, record.userId, record.keyVersion]))
}

export async function protectDeviceCache(record: DeviceCacheRecord): Promise<ProtectedDeviceCacheRecord> {
  const metadata = { userId: record.userId, keyVersion: record.keyVersion }
  const additionalData = aad(metadata)
  if (record.umk.length !== 16) throw new Error('Invalid UMK.')
  const umk = new Uint8Array(record.umk)
  try {
    const wrappingKey = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    const nonce = crypto.getRandomValues(new Uint8Array(12))
    const encryptedUmk = await crypto.subtle.encrypt(
      { name: 'AES-GCM', iv: nonce, additionalData, tagLength: 128 }, wrappingKey, umk,
    )
    return { format: 'device-cache-v3', ...metadata, wrappingKey, nonce, encryptedUmk }
  } finally { umk.fill(0) }
}

export async function unprotectDeviceCache(record: ProtectedDeviceCacheRecord): Promise<DeviceCacheRecord> {
  const key = record.wrappingKey
  if (record.format !== 'device-cache-v3' || key?.extractable !== false || key.type !== 'secret' ||
      key.algorithm.name !== 'AES-GCM' || (key.algorithm as AesKeyAlgorithm).length !== 256 ||
      key.usages.length !== 2 || !key.usages.includes('encrypt') || !key.usages.includes('decrypt') ||
      record.nonce?.byteLength !== 12 || record.encryptedUmk?.byteLength !== 32)
    throw new Error('Invalid trusted-device cache envelope.')
  const plaintext = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: record.nonce, additionalData: aad(record), tagLength: 128 }, key, record.encryptedUmk,
  )
  return { userId: record.userId, keyVersion: record.keyVersion, umk: new Uint8Array(plaintext) }
}

/** Read-only compatibility: v1/v2 used an extra SM4 device key. */
export async function migrateLegacyDeviceCache(value: LegacyDeviceCacheRecord | ProtectedLegacyDeviceCacheRecord): Promise<DeviceCacheRecord> {
  const record = 'format' in value ? await unprotectLegacyDeviceCache(value) : value
  try {
    return { userId: record.userId, keyVersion: record.keyVersion,
      umk: unwrapUmkWithDeviceKey(record.deviceKey, record.wrappedUmk, record.userId, record.keyVersion) }
  } finally { record.deviceKey.fill(0) }
}

export interface LegacyDeviceCacheRecord {
  userId: string
  keyVersion: number
  deviceKey: Uint8Array
  wrappedUmk: string
}

export interface ProtectedLegacyDeviceCacheRecord {
  format: 'device-cache-v2'
  userId: string
  keyVersion: number
  wrappingKey: CryptoKey
  nonce: Uint8Array<ArrayBuffer>
  encryptedDeviceKey: ArrayBuffer
  wrappedUmk: string
}

function legacyAad(record: Pick<LegacyDeviceCacheRecord, 'userId' | 'keyVersion' | 'wrappedUmk'>): Uint8Array<ArrayBuffer> {
  if (!record.userId || !Number.isSafeInteger(record.keyVersion) || record.keyVersion < 1 ||
      !record.wrappedUmk.startsWith('wrap:v1:') || record.wrappedUmk.length > 512) {
    throw new Error('Invalid trusted-device cache metadata.')
  }
  return new TextEncoder().encode(JSON.stringify([`${getE2eeProfile().deviceCacheAadPrefix}-v2`, record.userId, record.keyVersion, record.wrappedUmk]))
}

async function unprotectLegacyDeviceCache(record: ProtectedLegacyDeviceCacheRecord): Promise<LegacyDeviceCacheRecord> {
  const key = record.wrappingKey
  if (record.format !== 'device-cache-v2' || key?.extractable !== false || key.type !== 'secret' ||
      key.algorithm.name !== 'AES-GCM' || (key.algorithm as AesKeyAlgorithm).length !== 256 ||
      key.usages.length !== 2 || !key.usages.includes('encrypt') || !key.usages.includes('decrypt') ||
      record.nonce?.byteLength !== 12 || record.encryptedDeviceKey?.byteLength !== 32) {
    throw new Error('Invalid trusted-device cache envelope.')
  }
  const plaintext = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: record.nonce, additionalData: legacyAad(record), tagLength: 128 }, key, record.encryptedDeviceKey,
  )
  return { userId: record.userId, keyVersion: record.keyVersion, wrappedUmk: record.wrappedUmk,
    deviceKey: new Uint8Array(plaintext) }
}

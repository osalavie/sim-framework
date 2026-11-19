// @vitest-environment node
import { webcrypto } from 'node:crypto'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { protectDeviceCache, unprotectDeviceCache, migrateLegacyDeviceCache } from './deviceCacheCipher'
import { createDeviceWrap } from './keys'
import { getE2eeProfile } from './profile'

describe('trusted-device key protection', () => {
  beforeEach(() => vi.stubGlobal('crypto', webcrypto))
  afterEach(() => vi.unstubAllGlobals())

  const record = () => ({ userId: 'owner', keyVersion: 1, umk: new Uint8Array(16).fill(7) })

  it('directly protects the UMK with a non-exportable key', async () => {
    const original = record()
    const protectedRecord = await protectDeviceCache(original)
    expect(protectedRecord).not.toHaveProperty('deviceKey')
    expect(protectedRecord).not.toHaveProperty('umk')
    expect(protectedRecord.wrappingKey.extractable).toBe(false)
    await expect(crypto.subtle.exportKey('raw', protectedRecord.wrappingKey)).rejects.toThrow()
    const restored = await unprotectDeviceCache(structuredClone(protectedRecord))
    expect(restored.umk).toEqual(original.umk)
    expect(original.umk).toEqual(new Uint8Array(16).fill(7))
    restored.umk.fill(0)
  })

  it.each(['owner', 'version', 'ciphertext', 'key'])('rejects modified %s without returning a key', async (change) => {
    const protectedRecord = await protectDeviceCache(record())
    if (change === 'owner') protectedRecord.userId = 'other'
    if (change === 'version') protectedRecord.keyVersion += 1
    if (change === 'ciphertext') new Uint8Array(protectedRecord.encryptedUmk)[0]! ^= 1
    if (change === 'key') protectedRecord.wrappingKey = (await protectDeviceCache(record())).wrappingKey
    await expect(unprotectDeviceCache(protectedRecord)).rejects.toThrow()
  })

  it('rejects extractable keys and unavailable Web Crypto rather than saving a raw fallback', async () => {
    const protectedRecord = await protectDeviceCache(record())
    protectedRecord.wrappingKey = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt'])
    await expect(unprotectDeviceCache(protectedRecord)).rejects.toThrow()
    vi.stubGlobal('crypto', {})
    await expect(protectDeviceCache(record())).rejects.toThrow()
  })

  it.each([1, 2])('migrates v%s without changing the UMK and writes only a direct v3 wrap', async (version) => {
    const original = record()
    const old = { userId: original.userId, keyVersion: original.keyVersion,
      ...createDeviceWrap(original.umk, original.userId, original.keyVersion) }
    const wrappingKey = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    const nonce = crypto.getRandomValues(new Uint8Array(12))
    const encryptedDeviceKey = await crypto.subtle.encrypt({ name: 'AES-GCM', iv: nonce,
      additionalData: new TextEncoder().encode(JSON.stringify([`${getE2eeProfile().deviceCacheAadPrefix}-v2`, old.userId, old.keyVersion, old.wrappedUmk])) }, wrappingKey, new Uint8Array(old.deviceKey))
    const migrated = await migrateLegacyDeviceCache(version === 1 ? old : {
      format: 'device-cache-v2', userId: old.userId, keyVersion: old.keyVersion, wrappedUmk: old.wrappedUmk, wrappingKey, nonce, encryptedDeviceKey,
    })
    expect(migrated.umk).toEqual(original.umk)
    const stored = await protectDeviceCache(migrated)
    expect(stored.format).toBe('device-cache-v3')
    expect(stored).not.toHaveProperty('deviceKey')
    expect(stored).not.toHaveProperty('wrappedUmk')
    expect(stored).not.toHaveProperty('encryptedDeviceKey')
    migrated.umk.fill(0)
    old.deviceKey.fill(0)
  })
})

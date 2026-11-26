// @vitest-environment node
import { webcrypto } from 'node:crypto'
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { toBase64Url } from './encoding'
import { unwrapUmkWithPasskey, wrapUmkWithPasskey } from './passkeyCipher'

describe('passkey PRF UMK wraps', () => {
  beforeEach(() => vi.stubGlobal('crypto', webcrypto))
  afterEach(() => vi.unstubAllGlobals())
  const context = { userId: 'owner', credentialId: 'credential', keyVersion: 1, rpId: 'example.test', prfSalt: toBase64Url(new Uint8Array(32).fill(1)) }
  const secret = new Uint8Array(32).fill(2)
  const umk = new Uint8Array(16).fill(3)

  it('recovers the same UMK from reproducible PRF output without caching a derived key', async () => {
    const wrapped = await wrapUmkWithPasskey(secret, umk, context)
    expect(wrapped).toMatch(/^passkey:v1:/)
    expect(await unwrapUmkWithPasskey(secret.slice(), wrapped, context)).toEqual(umk)
    expect(secret).toEqual(new Uint8Array(32).fill(2))
    expect(umk).toEqual(new Uint8Array(16).fill(3))
  })

  it.each(['userId', 'credentialId', 'keyVersion', 'rpId', 'prfSalt', 'secret', 'ciphertext'])('rejects changed %s', async (field) => {
    let wrapped = await wrapUmkWithPasskey(secret, umk, context)
    const changed = { ...context }
    if (field === 'keyVersion') changed.keyVersion += 1
    else if (field === 'prfSalt') changed.prfSalt = toBase64Url(new Uint8Array(32).fill(4))
    else if (field === 'userId' || field === 'credentialId' || field === 'rpId') changed[field] += '-other'
    if (field === 'ciphertext') wrapped = wrapped.slice(0, 15) + (wrapped[15] === 'A' ? 'B' : 'A') + wrapped.slice(16)
    await expect(unwrapUmkWithPasskey(field === 'secret' ? new Uint8Array(32).fill(4) : secret, wrapped, changed)).rejects.toThrow()
  })
})

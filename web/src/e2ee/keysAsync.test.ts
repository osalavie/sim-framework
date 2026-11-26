import { afterEach, describe, expect, it, vi } from 'vitest'
import { cancelE2eeKeyOperations, unlockUmkWithPassphraseAsync } from './keysAsync'
import { toBase64Url } from './encoding'

const identity = { kdfSalt: toBase64Url(new Uint8Array(16)), kdfIterations: 600_000,
  keyVersion: 1, passwordWrappedKey: 'wrap:v1:test', recoveryWrappedKey: 'wrap:v1:test' }

class TestWorker {
  static latest: TestWorker
  onmessage?: (event: { data: unknown }) => void
  onerror?: () => void
  postMessage = vi.fn()
  terminate = vi.fn()
  constructor() { TestWorker.latest = this }
}

afterEach(() => { cancelE2eeKeyOperations(); vi.unstubAllGlobals() })

describe('key worker boundary', () => {
  it('rejects untrusted KDF parameters before starting a worker', async () => {
    const worker = vi.fn()
    vi.stubGlobal('Worker', worker)
    for (const kdfIterations of [0, 9999, 10_000_001, Infinity, 0.5]) {
      await expect(unlockUmkWithPassphraseAsync('secret', { ...identity, kdfIterations }, 'user')).rejects.toThrow()
    }
    await expect(unlockUmkWithPassphraseAsync('secret', { ...identity, kdfSalt: 'AA' }, 'user')).rejects.toThrow()
    expect(worker).not.toHaveBeenCalled()
  })

  it('terminates a completed worker and returns its result', async () => {
    vi.stubGlobal('Worker', TestWorker)
    const promise = unlockUmkWithPassphraseAsync('secret', identity, 'user')
    const key = new Uint8Array(16).fill(7)
    TestWorker.latest.onmessage?.({ data: { ok: true, value: key } })
    expect(await promise).toBe(key)
    expect(TestWorker.latest.terminate).toHaveBeenCalledOnce()
  })

  it('cancels pending derivations when the vault locks', async () => {
    vi.stubGlobal('Worker', TestWorker)
    const promise = unlockUmkWithPassphraseAsync('secret', identity, 'user')
    const rejection = expect(promise).rejects.toThrow('cancelled')
    cancelE2eeKeyOperations()
    await rejection
    expect(TestWorker.latest.terminate).toHaveBeenCalledOnce()
  })

  it('does not expose worker errors or passwords', async () => {
    vi.stubGlobal('Worker', TestWorker)
    const promise = unlockUmkWithPassphraseAsync('secret', identity, 'user')
    TestWorker.latest.onmessage?.({ data: { ok: false, error: 'secret' } })
    await expect(promise).rejects.toThrow('E2EE key operation failed.')
    expect(TestWorker.latest.terminate).toHaveBeenCalledOnce()
  })
})

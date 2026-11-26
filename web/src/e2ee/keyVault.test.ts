import { beforeEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => ({
  loadDeviceCache: vi.fn(),
  clearDeviceCache: vi.fn(),
  requestE2eeUnlock: vi.fn(),
}))

vi.mock('./deviceStore', () => ({
  loadDeviceCache: mocks.loadDeviceCache,
  clearDeviceCache: mocks.clearDeviceCache,
}))
vi.mock('./unlockGate', () => ({ requestE2eeUnlock: mocks.requestE2eeUnlock }))

import {
  getE2eeUmkForUser,
  ensureE2eeUnlocked,
  lockE2eeVault,
  setE2eeUmk,
  tryE2eeDeviceUnlock,
} from './keyVault'

const USER_ID = 'user-1'
const UMK = new Uint8Array(16).fill(7)

describe('tryE2eeDeviceUnlock', () => {
  beforeEach(() => {
    lockE2eeVault()
    vi.clearAllMocks()
  })

  it('unlocks from the remembered device cache without prompting', async () => {
    mocks.loadDeviceCache.mockResolvedValue({
      userId: USER_ID,
      keyVersion: 2,
      umk: UMK.slice(),
    })

    const umk = await tryE2eeDeviceUnlock(USER_ID)

    expect(umk).not.toBeNull()
    expect(Array.from(umk ?? [])).toEqual(Array.from(UMK))
    expect(getE2eeUmkForUser(USER_ID)).not.toBeNull()
    expect(mocks.requestE2eeUnlock).not.toHaveBeenCalled()
  })

  it('returns null when no cache exists and never prompts', async () => {
    mocks.loadDeviceCache.mockResolvedValue(null)

    const umk = await tryE2eeDeviceUnlock(USER_ID)

    expect(umk).toBeNull()
    expect(mocks.requestE2eeUnlock).not.toHaveBeenCalled()
  })

  it('drops a corrupt cache and never prompts', async () => {
    mocks.loadDeviceCache.mockResolvedValue({
      userId: USER_ID,
      keyVersion: 1,
      umk: UMK.slice(),
    })
    mocks.loadDeviceCache.mockResolvedValue({ userId: USER_ID, keyVersion: 1, umk: new Uint8Array(0) })

    const umk = await tryE2eeDeviceUnlock(USER_ID)

    expect(umk).toBeNull()
    expect(mocks.clearDeviceCache).toHaveBeenCalledWith(USER_ID)
    expect(mocks.requestE2eeUnlock).not.toHaveBeenCalled()
  })

  it.each([false, true])('cannot unlock after locking during a cache read (interactive=%s)', async (interactive) => {
    let finish!: (record: unknown) => void
    mocks.loadDeviceCache.mockReturnValue(new Promise((resolve) => { finish = resolve }))
    const loading = interactive ? ensureE2eeUnlocked(USER_ID) : tryE2eeDeviceUnlock(USER_ID)
    const expected = interactive ? expect(loading).rejects.toThrow('cancelled') : expect(loading).resolves.toBeNull()
    lockE2eeVault()
    const umkBytes = new Uint8Array(16).fill(3)
    finish({ userId: USER_ID, keyVersion: 1, umk: umkBytes })
    await expected
    expect(umkBytes.every((byte) => byte === 0)).toBe(true)
    expect(getE2eeUmkForUser(USER_ID)).toBeNull()
    expect(mocks.requestE2eeUnlock).not.toHaveBeenCalled()
  })

  it('reuses the already unlocked vault without touching IndexedDB', async () => {
    setE2eeUmk(UMK, USER_ID, 1)

    const umk = await tryE2eeDeviceUnlock(USER_ID)

    expect(umk).not.toBeNull()
    expect(mocks.loadDeviceCache).not.toHaveBeenCalled()
    expect(mocks.requestE2eeUnlock).not.toHaveBeenCalled()
  })
})

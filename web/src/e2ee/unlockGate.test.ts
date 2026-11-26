import { afterEach, describe, expect, it } from 'vitest'
import { cancelE2eeUnlock, completeE2eeUnlock, requestE2eeUnlock, useE2eeUnlockGateState } from './unlockGate'

describe('per-article encryption authorization gate', () => {
  afterEach(() => completeE2eeUnlock())

  it('requires a fresh verification prompt for each additional encryption action', async () => {
    const first = requestE2eeUnlock('', 'encrypt')
    expect(useE2eeUnlockGateState()).toMatchObject({ visible: true, purpose: 'encrypt' })
    completeE2eeUnlock()
    await first
    const next = requestE2eeUnlock('', 'encrypt')
    expect(useE2eeUnlockGateState().visible).toBe(true)
    cancelE2eeUnlock()
    await expect(next).rejects.toMatchObject({ name: 'E2eeCancelledError' })
  })

  it('does not reuse an open read prompt to authorize new encryption', async () => {
    const reading = requestE2eeUnlock()
    const encrypting = requestE2eeUnlock('', 'encrypt')
    completeE2eeUnlock()
    await reading
    await Promise.resolve()
    expect(useE2eeUnlockGateState()).toMatchObject({ visible: true, purpose: 'encrypt' })
    completeE2eeUnlock()
    await encrypting
  })
})

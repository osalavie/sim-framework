import { describe, expect, it } from 'vitest'
import { toBase64Url } from './encoding'
import {
  createIdentitySecrets,
  rewrapWithPassphrase,
  unlockUmkWithPassphrase,
  unlockUmkWithRecoveryKey,
} from './keys'

describe('encryption passphrase change', () => {
  it('keeps the same UMK and recovery key while replacing the old passphrase', () => {
    const userId = 'test-user'
    const secrets = createIdentitySecrets('old passphrase', userId, 1, 2)
    const identity = {
      kdfSalt: toBase64Url(secrets.salt),
      kdfIterations: 2,
      keyVersion: 1,
      passwordWrappedKey: secrets.passwordWrappedKey,
      recoveryWrappedKey: secrets.recoveryWrappedKey,
    }

    const umk = unlockUmkWithPassphrase('old passphrase', identity, userId)
    identity.passwordWrappedKey = rewrapWithPassphrase('new passphrase', umk, userId, identity)

    expect(unlockUmkWithPassphrase('new passphrase', identity, userId)).toEqual(umk)
    expect(unlockUmkWithRecoveryKey(secrets.recoveryKey, identity, userId)).toEqual(umk)
    expect(() => unlockUmkWithPassphrase('old passphrase', identity, userId)).toThrow()
    umk.fill(0)
  })
})

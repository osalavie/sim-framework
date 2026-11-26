import { describe, expect, it } from 'vitest'
import { toBase64Url } from './encoding'
import { createIdentitySecrets, rewrapWithPassphrase, unlockUmkWithPassphrase, unlockUmkWithRecoveryKey } from './keys'

describe('offline recovery without login-password backup', () => {
  it('reuses the offline recovery key after resetting the encryption passphrase', () => {
    const owner = 'owner'
    const secrets = createIdentitySecrets('original passphrase', owner, 1, 2)
    const identity = {
      kdfSalt: toBase64Url(secrets.salt), kdfIterations: 2, keyVersion: 1,
      passwordWrappedKey: secrets.passwordWrappedKey, recoveryWrappedKey: secrets.recoveryWrappedKey,
    }
    const recovered = unlockUmkWithRecoveryKey(secrets.recoveryKey, identity, owner)
    expect(recovered).toEqual(secrets.umk)
    identity.passwordWrappedKey = rewrapWithPassphrase('new passphrase', recovered, owner, identity)
    const again = unlockUmkWithRecoveryKey(secrets.recoveryKey, identity, owner)
    const withNewPassphrase = unlockUmkWithPassphrase('new passphrase', identity, owner)
    expect(again).toEqual(secrets.umk)
    expect(withNewPassphrase).toEqual(secrets.umk)
    expect(() => unlockUmkWithPassphrase('original passphrase', identity, owner)).toThrow()
    expect(() => unlockUmkWithRecoveryKey(secrets.recoveryKey, identity, 'other-owner')).toThrow()
    recovered.fill(0)
    again.fill(0)
    withNewPassphrase.fill(0)
    secrets.umk.fill(0)
    secrets.recoveryKey.fill(0)
  })
})

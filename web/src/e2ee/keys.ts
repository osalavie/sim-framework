import { fromBase64Url, randomBytes } from './encoding'
import { unwrapKey, wrapKey, wrapUmkAad } from './envelope'
import { deriveKek, KDF_ITERATIONS, KDF_SALT_BYTES, SYMMETRIC_KEY_BYTES, validateIdentityKdf } from './kdf'

export function deriveShareKey(password: string, salt: Uint8Array, iterations = KDF_ITERATIONS): Uint8Array {
  validateIdentityKdf(salt, iterations)
  return deriveKek(password, salt, iterations)
}
import type { UserE2EeIdentityDto } from '@/api/system'

export interface IdentitySecrets {
  salt: Uint8Array
  umk: Uint8Array
  recoveryKey: Uint8Array
  passwordWrappedKey: string
  recoveryWrappedKey: string
  iterations: number
}

export interface IdentityWraps {
  kdfSalt: string
  kdfIterations: number
  keyVersion: number
  passwordWrappedKey: string
  recoveryWrappedKey: string
}

/** 首次启用：随机生成 UMK/RK/salt，再分别用口令 KEK 与 RK 包裹 UMK。 */
export function createIdentitySecrets(
  passphrase: string,
  userId: string,
  keyVersion = 1,
  iterations = KDF_ITERATIONS,
): IdentitySecrets {
  const salt = randomBytes(KDF_SALT_BYTES)
  const umk = randomBytes(SYMMETRIC_KEY_BYTES)
  const recoveryKey = randomBytes(SYMMETRIC_KEY_BYTES)
  const kek = deriveKek(passphrase, salt, iterations)

  try {
    return {
      salt,
      umk,
      recoveryKey,
      passwordWrappedKey: wrapKey(kek, umk, wrapUmkAad(userId, 'password', keyVersion)),
      recoveryWrappedKey: wrapKey(
        recoveryKey,
        umk,
        wrapUmkAad(userId, 'recovery', keyVersion),
      ),
      iterations,
    }
  } finally {
    kek.fill(0)
  }
}

/** 用加密口令解开 UMK；口令错误由 SM4-GCM 标签校验发现。 */
export function unlockUmkWithPassphrase(
  passphrase: string,
  identity: IdentityWraps | UserE2EeIdentityDto,
  userId: string,
): Uint8Array {
  const salt = fromBase64Url(requireString(identity.kdfSalt, 'KDF salt'))
  const iterations = identity.kdfIterations || KDF_ITERATIONS
  const keyVersion = identity.keyVersion ?? 1
  const kek = deriveKek(passphrase, salt, iterations)

  try {
    return unwrapKey(
      kek,
      requireString(identity.passwordWrappedKey, 'password wrapped key'),
      wrapUmkAad(userId, 'password', keyVersion),
    )
  } finally {
    kek.fill(0)
  }
}

/** 用恢复密钥解开 UMK。 */
export function unlockUmkWithRecoveryKey(
  recoveryKey: Uint8Array,
  identity: IdentityWraps | UserE2EeIdentityDto,
  userId: string,
): Uint8Array {
  const keyVersion = identity.keyVersion ?? 1
  return unwrapKey(
    recoveryKey,
    requireString(identity.recoveryWrappedKey, 'recovery wrapped key'),
    wrapUmkAad(userId, 'recovery', keyVersion),
  )
}

/** 修改加密口令：UMK 不变，只重新生成 PasswordWrap。 */
export function rewrapWithPassphrase(
  passphrase: string,
  umk: Uint8Array,
  userId: string,
  identity: IdentityWraps | UserE2EeIdentityDto,
): string {
  const salt = fromBase64Url(requireString(identity.kdfSalt, 'KDF salt'))
  const iterations = identity.kdfIterations || KDF_ITERATIONS
  const kek = deriveKek(passphrase, salt, iterations)
  try {
    return wrapKey(kek, umk, wrapUmkAad(userId, 'password', identity.keyVersion ?? 1))
  } finally {
    kek.fill(0)
  }
}

/** 轮换恢复密钥：生成新 RK 并重新包裹 UMK。 */
export function rewrapRecoveryKey(
  umk: Uint8Array,
  userId: string,
  keyVersion = 1,
): { recoveryKey: Uint8Array; recoveryWrappedKey: string } {
  const recoveryKey = randomBytes(SYMMETRIC_KEY_BYTES)
  return {
    recoveryKey,
    recoveryWrappedKey: wrapKey(recoveryKey, umk, wrapUmkAad(userId, 'recovery', keyVersion)),
  }
}

/** “信任此设备”：生成随机设备密钥并包裹 UMK；设备密钥只存本机 IndexedDB。 */
export function createDeviceWrap(
  umk: Uint8Array,
  userId: string,
  keyVersion = 1,
): { deviceKey: Uint8Array; wrappedUmk: string } {
  const deviceKey = randomBytes(SYMMETRIC_KEY_BYTES)
  return {
    deviceKey,
    wrappedUmk: wrapKey(deviceKey, umk, wrapUmkAad(userId, 'device', keyVersion)),
  }
}

export function unwrapUmkWithDeviceKey(
  deviceKey: Uint8Array,
  wrappedUmk: string,
  userId: string,
  keyVersion = 1,
): Uint8Array {
  return unwrapKey(deviceKey, wrappedUmk, wrapUmkAad(userId, 'device', keyVersion))
}

function requireString(value: string | null | undefined, label: string): string {
  if (!value) {
    throw new Error(`E2EE identity is missing ${label}.`)
  }
  return value
}

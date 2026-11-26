import { createHmacSm3 } from './gm/sm3'
import { utf8Encode } from './encoding'

/** v1 固定 PBKDF2-HMAC-SM3（国密摘要）。参数随身份行保存。 */
export const KDF_ALGORITHM = 'PBKDF2-SM3'
export const KDF_ITERATIONS = 600_000
export const KDF_SALT_BYTES = 16
export const SYMMETRIC_KEY_BYTES = 16
export const KDF_MAX_ITERATIONS = 10_000_000
export const KDF_MIN_IDENTITY_ITERATIONS = 10_000

export function validateIdentityKdf(salt: Uint8Array, iterations: number): void {
  if (salt.length !== KDF_SALT_BYTES || !Number.isInteger(iterations) ||
      iterations < KDF_MIN_IDENTITY_ITERATIONS || iterations > KDF_MAX_ITERATIONS) {
    throw new Error('Unsupported E2EE KDF parameters.')
  }
}

/**
 * PBKDF2-HMAC-SM3。v1 只需要 16 字节 SM4 密钥，因此只计算一个块；
 * 通过预吸收 ipad/opad 的 HMAC 状态，每次迭代只做 2 次 SM3 压缩。
 */
export function deriveKek(passphrase: string, salt: Uint8Array, iterations = KDF_ITERATIONS): Uint8Array {
  if (!Number.isInteger(iterations) || iterations < 1 || iterations > KDF_MAX_ITERATIONS) {
    throw new Error('Invalid PBKDF2 iteration count.')
  }
  if (salt.length === 0) {
    throw new Error('PBKDF2 salt must not be empty.')
  }

  const passwordBytes = utf8Encode(passphrase)
  const hmac = createHmacSm3(passwordBytes)
  passwordBytes.fill(0)
  const firstBlock = new Uint8Array(salt.length + 4)
  firstBlock.set(salt)
  firstBlock[salt.length + 3] = 1

  let u = hmac.compute(firstBlock)
  const derived = u.slice()
  for (let iteration = 1; iteration < iterations; iteration += 1) {
    const previous = u
    u = hmac.compute(previous)
    previous.fill(0)
    for (let i = 0; i < derived.length; i += 1) {
      derived[i] = (derived[i] ?? 0) ^ (u[i] ?? 0)
    }
  }

  // PBKDF2 dkLen = 16（SM4-128 密钥）：取第一个块的前 16 字节。
  const key = derived.slice(0, SYMMETRIC_KEY_BYTES)
  u.fill(0)
  derived.fill(0)
  firstBlock.fill(0)
  return key
}

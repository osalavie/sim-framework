import { describe, expect, it } from 'vitest'
import { deriveKek, KDF_ITERATIONS } from './kdf'

function hex(bytes: Uint8Array): string {
  return Array.from(bytes)
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
}

describe('PBKDF2-HMAC-SM3', () => {
  it('is deterministic for the same passphrase and salt', () => {
    const salt = new Uint8Array(16).fill(0x42)
    const first = deriveKek('correct horse battery staple', salt, 1000)
    const second = deriveKek('correct horse battery staple', salt, 1000)
    expect(hex(first)).toBe(hex(second))
    expect(first.length).toBe(16)
  })

  it('changes with salt, passphrase and iteration count', () => {
    const salt = new Uint8Array(16).fill(1)
    const base = hex(deriveKek('passphrase value', salt, 1000))
    expect(hex(deriveKek('passphrase value', new Uint8Array(16).fill(2), 1000))).not.toBe(base)
    expect(hex(deriveKek('passphrase value!', salt, 1000))).not.toBe(base)
    expect(hex(deriveKek('passphrase value', salt, 1001))).not.toBe(base)
  })

  it('rejects invalid parameters', () => {
    expect(() => deriveKek('x', new Uint8Array(0), 1)).toThrow()
    expect(() => deriveKek('x', new Uint8Array(16), 0)).toThrow()
  })

  it('completes the production iteration count', () => {
    const started = performance.now()
    deriveKek('a sufficiently long passphrase', new Uint8Array(16).fill(9), KDF_ITERATIONS)
    const elapsed = performance.now() - started
    // 记录真实成本便于观察；宽松阈值避免 CI 抖动导致误报。
    console.info(`PBKDF2-SM3 ${KDF_ITERATIONS} iterations took ${elapsed.toFixed(0)} ms`)
    expect(elapsed).toBeLessThan(30_000)
  }, 30_000)
})

import { describe, expect, it } from 'vitest'
import { Sm3, sm3 } from './sm3'
import { Sm4 } from './sm4'
import { sm4GcmDecrypt, sm4GcmEncrypt } from './gcm'

function hex(bytes: Uint8Array): string {
  return Array.from(bytes)
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
}

function bytes(text: string): Uint8Array {
  return new TextEncoder().encode(text)
}

describe('GM primitives', () => {
  it('hashes "abc" with the GB/T 32905 SM3 vector', () => {
    expect(hex(sm3(bytes('abc')))).toBe(
      '66c7f0f462eeedd9d1f2d46bdc10e4e24167c4875cf2f7a2297da02b8f4ba8e0',
    )
  })

  it('handles multi-block input and incremental updates', () => {
    const payload = bytes('abcd'.repeat(64))
    const oneShot = sm3(payload)
    const incremental = new Sm3()
    incremental.update(payload.subarray(0, 37))
    incremental.update(payload.subarray(37, 130))
    incremental.update(payload.subarray(130))
    expect(hex(incremental.digest())).toBe(hex(oneShot))
  })

  it('encrypts the GB/T 32907 SM4 block vector', () => {
    const key = Uint8Array.from([
      0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0xfe, 0xdc, 0xba, 0x98, 0x76, 0x54, 0x32,
      0x10,
    ])
    const plaintext = key.slice()
    const cipher = new Sm4(key)
    expect(hex(cipher.encryptBlockBytes(plaintext))).toBe('681edf34d206965e86b3e94f536e4246')
  })

  it('round-trips SM4-GCM and rejects tampered tags', () => {
    const key = new Uint8Array(16).fill(7)
    const nonce = new Uint8Array(12).fill(3)
    const aad = bytes('aad-value')
    const plaintext = bytes('私有文章正文 content')

    const encrypted = sm4GcmEncrypt(key, nonce, plaintext, aad)
    expect(plaintext.length).toBe(encrypted.ciphertext.length)
    expect(
      new TextDecoder().decode(
        sm4GcmDecrypt(key, nonce, encrypted.ciphertext, encrypted.tag, aad) ?? new Uint8Array(),
      ),
    ).toBe('私有文章正文 content')

    const tag = encrypted.tag.slice()
    tag[0] = (tag[0] ?? 0) ^ 1
    expect(sm4GcmDecrypt(key, nonce, encrypted.ciphertext, tag, aad)).toBeNull()
  })

  it('matches the BouncyCastle SM4-GCM cross-implementation vector', () => {
    const key = Uint8Array.from({ length: 16 }, (_, index) => index)
    const nonce = Uint8Array.from({ length: 12 }, (_, index) => 0x10 + index)
    const aad = bytes('aad')
    const plaintext = bytes('hello sm4-gcm')

    const encrypted = sm4GcmEncrypt(key, nonce, plaintext, aad)
    expect(hex(encrypted.ciphertext) + hex(encrypted.tag)).toBe(
      'd353af8141994e98da1afbc21c592deda266a345394a985613f593aa6a',
    )
  })

  it('rejects SM4-GCM with a different AAD', () => {
    const key = new Uint8Array(16).fill(1)
    const nonce = new Uint8Array(12).fill(2)
    const encrypted = sm4GcmEncrypt(key, nonce, bytes('secret'), bytes('aad-1'))
    expect(
      sm4GcmDecrypt(key, nonce, encrypted.ciphertext, encrypted.tag, bytes('aad-2')),
    ).toBeNull()
  })
})

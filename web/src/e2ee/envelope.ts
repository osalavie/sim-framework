import { concatBytes, fromBase64Url, guidN, randomBytes, toBase64Url, utf8Decode, utf8Encode } from './encoding'
import { sm4GcmDecrypt, sm4GcmEncrypt } from './gm/gcm'
import { SYMMETRIC_KEY_BYTES } from './kdf'
import { getE2eeProfile } from './profile'
import { E2eeDecryptError } from './types'

export const FIELD_PREFIX = 'e2ee:v1:'
export const WRAP_PREFIX = 'wrap:v1:'
export const NONCE_BYTES = 12
export const TAG_BYTES = 16
const MIN_FIELD_PAYLOAD = NONCE_BYTES + TAG_BYTES

export type WrapPurpose = 'password' | 'recovery' | 'device' | 'account'

/** 生成 `{namespace}|...` 形式的 AAD；领域语义由调用方拼装。 */
export function e2eeAad(...segments: Array<string | number>): Uint8Array {
  return utf8Encode(
    [getE2eeProfile().aadNamespace, ...segments.map((segment) => String(segment))].join('|'),
  )
}

export function wrapUmkAad(userId: string, purpose: WrapPurpose, umkVersion: number): Uint8Array {
  return e2eeAad('umk', 'v1', guidN(userId), purpose, umkVersion)
}

export function encryptField(key: Uint8Array, plaintext: string, aad: Uint8Array): string {
  assertKey(key)
  const nonce = randomBytes(NONCE_BYTES)
  const bytes = utf8Encode(plaintext)
  try {
    const { ciphertext, tag } = sm4GcmEncrypt(key, nonce, bytes, aad)
    return FIELD_PREFIX + toBase64Url(concatBytes(nonce, tag, ciphertext))
  } finally {
    bytes.fill(0)
  }
}

export function decryptField(key: Uint8Array, envelope: string, aad: Uint8Array): string {
  const parts = decodeEnvelope(envelope, FIELD_PREFIX, MIN_FIELD_PAYLOAD)
  const plaintext = sm4GcmDecrypt(key, parts.nonce, parts.ciphertext, parts.tag, aad)
  if (plaintext === null) {
    throw new E2eeDecryptError()
  }
  try {
    return utf8Decode(plaintext)
  } catch {
    throw new E2eeDecryptError('Decrypted payload is not valid UTF-8.')
  } finally {
    plaintext.fill(0)
  }
}

export function wrapKey(wrappingKey: Uint8Array, key: Uint8Array, aad: Uint8Array): string {
  assertKey(wrappingKey)
  assertKey(key)
  const nonce = randomBytes(NONCE_BYTES)
  const { ciphertext, tag } = sm4GcmEncrypt(wrappingKey, nonce, key, aad)
  return WRAP_PREFIX + toBase64Url(concatBytes(nonce, tag, ciphertext))
}

export function unwrapKey(wrappingKey: Uint8Array, envelope: string, aad: Uint8Array): Uint8Array {
  const parts = decodeEnvelope(envelope, WRAP_PREFIX, NONCE_BYTES + TAG_BYTES + SYMMETRIC_KEY_BYTES)
  const key = sm4GcmDecrypt(wrappingKey, parts.nonce, parts.ciphertext, parts.tag, aad)
  if (key === null || key.length !== SYMMETRIC_KEY_BYTES) {
    key?.fill(0)
    throw new E2eeDecryptError('Wrapped key is invalid or was tampered with.')
  }
  return key
}

export function isFieldEnvelope(value: string): boolean {
  return value.startsWith(FIELD_PREFIX) && value.length > FIELD_PREFIX.length + 8
}

export function isWrapEnvelope(value: string | null | undefined): value is string {
  return Boolean(value && value.startsWith(WRAP_PREFIX) && value.length > WRAP_PREFIX.length + 8)
}

interface EnvelopeParts {
  nonce: Uint8Array
  tag: Uint8Array
  ciphertext: Uint8Array
}

function decodeEnvelope(value: string, prefix: string, minimumPayload: number): EnvelopeParts {
  if (!value.startsWith(prefix)) {
    throw new E2eeDecryptError('Unsupported E2EE envelope version.')
  }

  let payload: Uint8Array
  try {
    payload = fromBase64Url(value.slice(prefix.length))
  } catch {
    throw new E2eeDecryptError('E2EE envelope is not valid base64url.')
  }

  if (payload.length < minimumPayload) {
    throw new E2eeDecryptError('E2EE envelope is truncated.')
  }

  return {
    nonce: payload.subarray(0, NONCE_BYTES),
    tag: payload.subarray(NONCE_BYTES, NONCE_BYTES + TAG_BYTES),
    ciphertext: payload.subarray(NONCE_BYTES + TAG_BYTES),
  }
}

function assertKey(key: Uint8Array): void {
  if (key.length !== SYMMETRIC_KEY_BYTES) {
    throw new Error(`SM4 key must be ${SYMMETRIC_KEY_BYTES} bytes.`)
  }
}

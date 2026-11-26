import { randomBytes, concatBytes, utf8Encode, utf8Decode } from './encoding'
import { e2eeAad } from './envelope'
import { sm4GcmEncrypt, sm4GcmDecrypt } from './gm/gcm'
import { getE2eeProfile } from './profile'
import { E2eeDecryptError } from './types'

export const MAX_ENCRYPTED_FILE_BYTES = 20 * 1024 * 1024

export function encryptedFileType(): string {
  return getE2eeProfile().fileMimeType
}

/** Provider-independent authenticated binary file. Original metadata is encrypted too. */
export async function encryptFile(file: File): Promise<{ blob: Blob; key: Uint8Array }> {
  if (file.size > MAX_ENCRYPTED_FILE_BYTES) throw new Error('Encrypted file exceeds 20 MiB.')
  const metadata = utf8Encode(JSON.stringify({ name: file.name, type: file.type, size: file.size }))
  if (metadata.length > 4096) { throw new Error('Invalid file metadata.') }
  const length = new Uint8Array(4)
  new DataView(length.buffer).setUint32(0, metadata.length)
  const bytes = new Uint8Array(await file.arrayBuffer())
  const payload = concatBytes(length, metadata, bytes)
  const key = randomBytes(16)
  const nonce = randomBytes(12)
  try {
    const magic = utf8Encode(getE2eeProfile().fileMagic)
    const { ciphertext, tag } = sm4GcmEncrypt(key, nonce, payload, e2eeAad('file', 'v1'))
    return { blob: new Blob([concatBytes(magic, nonce, tag, ciphertext)], { type: encryptedFileType() }), key }
  } catch (error) { key.fill(0); throw error } finally { bytes.fill(0); payload.fill(0); metadata.fill(0) }
}

export async function decryptFile(blob: Blob, key: Uint8Array): Promise<File> {
  if (blob.size < 42 || blob.size > MAX_ENCRYPTED_FILE_BYTES + 8192) throw new E2eeDecryptError()
  const bytes = new Uint8Array(await blob.arrayBuffer())
  const magic = utf8Encode(getE2eeProfile().fileMagic)
  if (!magic.every((value, index) => value === bytes[index])) throw new E2eeDecryptError()
  const nonceStart = magic.length
  const tagStart = nonceStart + 12
  const cipherStart = tagStart + 16
  const plain = sm4GcmDecrypt(
    key,
    bytes.subarray(nonceStart, tagStart),
    bytes.subarray(cipherStart),
    bytes.subarray(tagStart, cipherStart),
    e2eeAad('file', 'v1'),
  )
  if (!plain || plain.length < 4) { plain?.fill(0); throw new E2eeDecryptError() }
  try {
    const size = new DataView(plain.buffer, plain.byteOffset, 4).getUint32(0)
    if (size > 4096 || size + 4 > plain.length) throw new E2eeDecryptError()
    const meta = JSON.parse(utf8Decode(plain.subarray(4, 4 + size)))
    if (typeof meta.name !== 'string' || typeof meta.type !== 'string' || meta.size !== plain.length - 4 - size) throw new E2eeDecryptError()
    const name = meta.name.replace(/[\\/\x00-\x1f]/g, '_')
    return new File([plain.slice(4 + size)], name, { type: meta.type })
  } finally { plain.fill(0) }
}

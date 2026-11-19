import { concatBytes, fromBase64Url, guidN, randomBytes, toBase64Url, utf8Encode } from './encoding'
import { getE2eeProfile } from './profile'

export interface PasskeyWrapContext {
  userId: string
  credentialId: string
  keyVersion: number
  rpId: string
  prfSalt: string
}

function contextBytes(context: PasskeyWrapContext): Uint8Array<ArrayBuffer> {
  if (!context.userId || !context.credentialId || !context.rpId || !Number.isSafeInteger(context.keyVersion) || context.keyVersion < 1)
    throw new Error('Invalid passkey wrap context.')
  return utf8Encode(JSON.stringify([getE2eeProfile().passkeyAadPrefix, guidN(context.userId),
    context.credentialId, context.keyVersion, context.rpId, context.prfSalt])) as Uint8Array<ArrayBuffer>
}

async function derive(prf: Uint8Array, context: PasskeyWrapContext): Promise<CryptoKey> {
  const salt = fromBase64Url(context.prfSalt)
  if (prf.length !== 32 || salt.length !== 32) throw new Error('Invalid passkey PRF material.')
  const secret = new Uint8Array(prf)
  try {
    const source = await crypto.subtle.importKey('raw', secret, 'HKDF', false, ['deriveKey'])
    return await crypto.subtle.deriveKey({ name: 'HKDF', hash: 'SHA-256', salt: new Uint8Array(salt), info: contextBytes(context) },
      source, { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
  } finally { secret.fill(0) }
}

export async function wrapUmkWithPasskey(prf: Uint8Array, umk: Uint8Array, context: PasskeyWrapContext): Promise<string> {
  if (umk.length !== 16) throw new Error('Invalid UMK.')
  const plaintext = new Uint8Array(umk)
  try {
    const key = await derive(prf, context)
    const nonce = randomBytes(12)
    const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv: new Uint8Array(nonce), additionalData: contextBytes(context), tagLength: 128 }, key, plaintext)
    return 'passkey:v1:' + toBase64Url(concatBytes(nonce, new Uint8Array(ciphertext)))
  } finally { plaintext.fill(0) }
}

export async function unwrapUmkWithPasskey(prf: Uint8Array, envelope: string, context: PasskeyWrapContext): Promise<Uint8Array> {
  if (!envelope.startsWith('passkey:v1:') || envelope.length > 128) throw new Error('Invalid passkey wrap.')
  const payload = fromBase64Url(envelope.slice(11))
  if (payload.length !== 44) throw new Error('Invalid passkey wrap length.')
  const key = await derive(prf, context)
  return new Uint8Array(await crypto.subtle.decrypt({ name: 'AES-GCM', iv: new Uint8Array(payload.slice(0, 12)),
    additionalData: contextBytes(context), tagLength: 128 }, key, new Uint8Array(payload.slice(12))))
}

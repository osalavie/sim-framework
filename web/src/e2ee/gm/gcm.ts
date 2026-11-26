import { Sm4 } from './sm4'

/**
 * SM4-GCM（GB/T 36624-2018 / NIST SP 800-38D 的 GCM 结构，分组密码为 SM4）。
 * nonce 固定 12 字节，认证标签 16 字节；AAD 参与 GHASH。
 */
const BLOCK_SIZE = 16
const TAG_SIZE = 16
const R = 0xe1n << 120n

export interface Sm4GcmResult {
  ciphertext: Uint8Array
  tag: Uint8Array
}

export function sm4GcmEncrypt(
  key: Uint8Array,
  nonce: Uint8Array,
  plaintext: Uint8Array,
  aad: Uint8Array,
): Sm4GcmResult {
  assertKeyAndNonce(key, nonce)
  const sm4 = new Sm4(key)
  const h = bytesToBigInt(sm4.encryptBlockBytes(new Uint8Array(BLOCK_SIZE)))

  const j0 = new Uint8Array(BLOCK_SIZE)
  j0.set(nonce)
  j0[BLOCK_SIZE - 1] = 1

  const ciphertext = ctrCrypt(sm4, j0, plaintext)
  const s = ghash(h, aad, ciphertext)
  const ekj0 = sm4.encryptBlockBytes(j0)
  const tag = new Uint8Array(TAG_SIZE)
  for (let i = 0; i < TAG_SIZE; i += 1) {
    tag[i] = (ekj0[i] ?? 0) ^ (s[i] ?? 0)
  }

  return { ciphertext, tag }
}

export function sm4GcmDecrypt(
  key: Uint8Array,
  nonce: Uint8Array,
  ciphertext: Uint8Array,
  tag: Uint8Array,
  aad: Uint8Array,
): Uint8Array | null {
  assertKeyAndNonce(key, nonce)
  if (tag.length !== TAG_SIZE) {
    return null
  }

  const sm4 = new Sm4(key)
  const h = bytesToBigInt(sm4.encryptBlockBytes(new Uint8Array(BLOCK_SIZE)))

  const j0 = new Uint8Array(BLOCK_SIZE)
  j0.set(nonce)
  j0[BLOCK_SIZE - 1] = 1

  const s = ghash(h, aad, ciphertext)
  const ekj0 = sm4.encryptBlockBytes(j0)
  let mismatch = 0
  for (let i = 0; i < TAG_SIZE; i += 1) {
    mismatch |= ((ekj0[i] ?? 0) ^ (s[i] ?? 0) ^ (tag[i] ?? 0)) & 0xff
  }
  if (mismatch !== 0) {
    return null
  }

  return ctrCrypt(sm4, j0, ciphertext)
}

function ctrCrypt(sm4: Sm4, j0: Uint8Array, input: Uint8Array): Uint8Array {
  const output = new Uint8Array(input.length)
  const counter = j0.slice()
  for (let offset = 0; offset < input.length; offset += BLOCK_SIZE) {
    incrementCounter(counter)
    const keystream = sm4.encryptBlockBytes(counter)
    const end = Math.min(offset + BLOCK_SIZE, input.length)
    for (let i = offset; i < end; i += 1) {
      output[i] = (input[i] ?? 0) ^ (keystream[i - offset] ?? 0)
    }
  }
  return output
}

function incrementCounter(counter: Uint8Array): void {
  for (let i = BLOCK_SIZE - 1; i >= BLOCK_SIZE - 4; i -= 1) {
    const next = ((counter[i] ?? 0) + 1) & 0xff
    counter[i] = next
    if (next !== 0) {
      return
    }
  }
}

function ghash(h: bigint, aad: Uint8Array, ciphertext: Uint8Array): Uint8Array {
  let y = 0n
  y = absorbBlocks(y, h, aad)
  y = absorbBlocks(y, h, ciphertext)

  const lengthBlock = new Uint8Array(BLOCK_SIZE)
  const aadBits = BigInt(aad.length) * 8n
  const cipherBits = BigInt(ciphertext.length) * 8n
  lengthBlock.set(bigIntToBytes(aadBits, 8), 0)
  lengthBlock.set(bigIntToBytes(cipherBits, 8), 8)
  y = gfMultiply(y ^ bytesToBigInt(lengthBlock), h)
  return bigIntToBytes(y, BLOCK_SIZE)
}

function absorbBlocks(initial: bigint, h: bigint, data: Uint8Array): bigint {
  let y = initial
  for (let offset = 0; offset < data.length; offset += BLOCK_SIZE) {
    const block = new Uint8Array(BLOCK_SIZE)
    block.set(data.subarray(offset, Math.min(offset + BLOCK_SIZE, data.length)))
    y = gfMultiply(y ^ bytesToBigInt(block), h)
  }
  return y
}

/** NIST SP 800-38D Algorithm 1：bit 从最高位开始处理，多项式 R = 11100001 || 0^120。 */
function gfMultiply(x: bigint, y: bigint): bigint {
  let z = 0n
  let v = y
  for (let i = 0; i < 128; i += 1) {
    if (((x >> BigInt(127 - i)) & 1n) === 1n) {
      z ^= v
    }
    v = (v & 1n) === 1n ? (v >> 1n) ^ R : v >> 1n
  }
  return z
}

function bytesToBigInt(bytes: Uint8Array): bigint {
  let value = 0n
  for (let i = 0; i < bytes.length; i += 1) {
    value = (value << 8n) | BigInt(bytes[i] ?? 0)
  }
  return value
}

function bigIntToBytes(value: bigint, length: number): Uint8Array {
  const output = new Uint8Array(length)
  let remaining = value
  for (let i = length - 1; i >= 0; i -= 1) {
    output[i] = Number(remaining & 0xffn)
    remaining >>= 8n
  }
  return output
}

function assertKeyAndNonce(key: Uint8Array, nonce: Uint8Array): void {
  if (key.length !== 16) {
    throw new Error('SM4-GCM key must be 16 bytes.')
  }
  if (nonce.length !== 12) {
    throw new Error('SM4-GCM nonce must be 12 bytes.')
  }
}

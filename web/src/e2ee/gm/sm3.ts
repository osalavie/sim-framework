/**
 * GB/T 32905-2016 SM3 摘要（纯 TypeScript，供浏览器端 E2EE 懒加载包使用）。
 * 分块压缩是按块增量实现的：HMAC/PBKDF2 会克隆“已吸收 ipad/opad 块”的状态，
 * 每次迭代只需 2 次压缩，避免为 60 万次迭代反复处理填充块。
 */
const IV = new Uint32Array([
  0x7380166f, 0x4914b2b9, 0x172442d7, 0xda8a0600, 0xa96f30bc, 0x163138aa, 0xe38dee4d, 0xb0fb0e4e,
])

const BLOCK_SIZE = 64

function rotl(value: number, bits: number): number {
  const shift = bits & 31
  if (shift === 0) return value >>> 0
  return ((value << shift) | (value >>> (32 - shift))) >>> 0
}

function p0(value: number): number {
  return (value ^ rotl(value, 9) ^ rotl(value, 17)) >>> 0
}

function p1(value: number): number {
  return (value ^ rotl(value, 15) ^ rotl(value, 23)) >>> 0
}

export class Sm3 {
  private readonly state = new Uint32Array(IV)
  private readonly buffer = new Uint8Array(BLOCK_SIZE)
  private bufferLength = 0
  private totalLength = 0

  update(data: Uint8Array): this {
    let offset = 0
    this.totalLength += data.length

    if (this.bufferLength > 0) {
      const need = BLOCK_SIZE - this.bufferLength
      const take = Math.min(need, data.length)
      this.buffer.set(data.subarray(0, take), this.bufferLength)
      this.bufferLength += take
      offset += take
      if (this.bufferLength === BLOCK_SIZE) {
        compress(this.state, this.buffer, 0)
        this.bufferLength = 0
      }
    }

    while (offset + BLOCK_SIZE <= data.length) {
      compress(this.state, data, offset)
      offset += BLOCK_SIZE
    }

    if (offset < data.length) {
      this.buffer.set(data.subarray(offset), 0)
      this.bufferLength = data.length - offset
    }

    return this
  }

  digest(): Uint8Array {
    const copy = this.fork()
    return copy.finalize()
  }

  fork(): Sm3 {
    const clone = new Sm3()
    clone.state.set(this.state)
    clone.buffer.set(this.buffer)
    clone.bufferLength = this.bufferLength
    clone.totalLength = this.totalLength
    return clone
  }

  private finalize(): Uint8Array {
    const bitLengthHigh = Math.floor(this.totalLength / 0x20000000)
    const bitLengthLow = (this.totalLength * 8) >>> 0

    const padding = new Uint8Array(1)
    padding[0] = 0x80
    this.update(padding)

    const zeroCount = this.bufferLength <= 56 ? 56 - this.bufferLength : 120 - this.bufferLength
    if (zeroCount > 0) {
      this.update(new Uint8Array(zeroCount))
    }

    const lengthBlock = new Uint8Array(8)
    writeUint32(lengthBlock, 0, bitLengthHigh)
    writeUint32(lengthBlock, 4, bitLengthLow)
    this.update(lengthBlock)

    const output = new Uint8Array(32)
    for (let i = 0; i < 8; i += 1) {
      const value = this.state[i] ?? 0
      output[i * 4] = value >>> 24
      output[i * 4 + 1] = (value >>> 16) & 0xff
      output[i * 4 + 2] = (value >>> 8) & 0xff
      output[i * 4 + 3] = value & 0xff
    }
    return output
  }
}

export function sm3(data: Uint8Array): Uint8Array {
  return new Sm3().update(data).digest()
}

function compress(state: Uint32Array, block: Uint8Array, offset: number): void {
  const w = new Uint32Array(68)
  for (let i = 0; i < 16; i += 1) {
    const index = offset + i * 4
    w[i] =
      ((block[index] ?? 0) << 24) |
      ((block[index + 1] ?? 0) << 16) |
      ((block[index + 2] ?? 0) << 8) |
      (block[index + 3] ?? 0)
  }

  for (let i = 16; i < 68; i += 1) {
    const w3 = w[i - 3] ?? 0
    const w13 = w[i - 13] ?? 0
    const w16 = w[i - 16] ?? 0
    const w9 = w[i - 9] ?? 0
    const w6 = w[i - 6] ?? 0
    w[i] = (p1((w16 ^ w9 ^ rotl(w3, 15)) >>> 0) ^ rotl(w13, 7) ^ w6) >>> 0
  }

  let a = state[0] ?? 0
  let b = state[1] ?? 0
  let c = state[2] ?? 0
  let d = state[3] ?? 0
  let e = state[4] ?? 0
  let f = state[5] ?? 0
  let g = state[6] ?? 0
  let h = state[7] ?? 0

  for (let j = 0; j < 64; j += 1) {
    const tj = j < 16 ? 0x79cc4519 : 0x7a879d8a
    const a12 = rotl(a, 12)
    const ss1 = rotl((((a12 + e + rotl(tj, j)) >>> 0) + 0) >>> 0, 7)
    const ss2 = (ss1 ^ a12) >>> 0
    const wj = w[j] ?? 0
    const wj4 = w[j + 4] ?? 0
    const w1 = (wj ^ wj4) >>> 0

    const ff = j < 16 ? (a ^ b ^ c) >>> 0 : (((a & b) | (a & c) | (b & c)) >>> 0)
    const gg = j < 16 ? (e ^ f ^ g) >>> 0 : (((e & f) | (~e & g)) >>> 0)

    const tt1 = ((((ff + d) >>> 0) + ss2 + w1) >>> 0)
    const tt2 = ((((gg + h) >>> 0) + ss1 + wj) >>> 0)

    d = c
    c = rotl(b, 9)
    b = a
    a = tt1
    h = g
    g = rotl(f, 19)
    f = e
    e = p0(tt2)
  }

  state[0] = (state[0] ?? 0) ^ a
  state[1] = (state[1] ?? 0) ^ b
  state[2] = (state[2] ?? 0) ^ c
  state[3] = (state[3] ?? 0) ^ d
  state[4] = (state[4] ?? 0) ^ e
  state[5] = (state[5] ?? 0) ^ f
  state[6] = (state[6] ?? 0) ^ g
  state[7] = (state[7] ?? 0) ^ h
}

function writeUint32(target: Uint8Array, offset: number, value: number): void {
  target[offset] = value >>> 24
  target[offset + 1] = (value >>> 16) & 0xff
  target[offset + 2] = (value >>> 8) & 0xff
  target[offset + 3] = value & 0xff
}

export function hmacSm3(key: Uint8Array, message: Uint8Array): Uint8Array {
  return createHmacSm3(key).compute(message)
}

export interface HmacSm3 {
  compute(message: Uint8Array): Uint8Array
}

/**
 * 预吸收 ipad/opad 块的 HMAC-SM3。返回的 compute 每次都会 fork 基础状态，
 * 适合 PBKDF2 这类同一密钥反复计算大量消息的场景。
 */
export function createHmacSm3(key: Uint8Array): HmacSm3 {
  const normalized = key.length > BLOCK_SIZE ? sm3(key) : key
  const keyBlock = new Uint8Array(BLOCK_SIZE)
  keyBlock.set(normalized)

  const innerPad = new Uint8Array(BLOCK_SIZE)
  const outerPad = new Uint8Array(BLOCK_SIZE)
  for (let i = 0; i < BLOCK_SIZE; i += 1) {
    const value = keyBlock[i] ?? 0
    innerPad[i] = value ^ 0x36
    outerPad[i] = value ^ 0x5c
  }

  const innerBase = new Sm3().update(innerPad)
  const outerBase = new Sm3().update(outerPad)

  return {
    compute(message: Uint8Array): Uint8Array {
      const inner = innerBase.fork().update(message).digest()
      return outerBase.fork().update(inner).digest()
    },
  }
}

// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { encryptFile, decryptFile, MAX_ENCRYPTED_FILE_BYTES } from './fileCipher'
import { getE2eeProfile } from './profile'

describe('encrypted binary files', () => {
  it('stores arbitrary binary data as an authenticated file and hides metadata', async () => {
    const original = new File([new Uint8Array([0, 255, 13, 10, 42])], 'secret-image.png', { type: 'image/png' })
    const { blob, key } = await encryptFile(original)
    const bytes = new Uint8Array(await blob.arrayBuffer())
    expect(new TextDecoder().decode(bytes.subarray(0, 8))).toBe(getE2eeProfile().fileMagic)
    expect(new TextDecoder().decode(bytes)).not.toContain(original.name)
    const restored = await decryptFile(blob, key)
    expect(restored.name).toBe(original.name)
    expect(restored.type).toBe(original.type)
    expect(new Uint8Array(await restored.arrayBuffer())).toEqual(new Uint8Array(await original.arrayBuffer()))
    await expect(decryptFile(blob, new Uint8Array(16))).rejects.toThrow()
    bytes[bytes.length - 1] = (bytes[bytes.length - 1] ?? 0) ^ 1
    await expect(decryptFile(new Blob([bytes]), key)).rejects.toThrow()
    await expect(decryptFile(blob.slice(0, blob.size - 1), key)).rejects.toThrow()
  })
  it('supports empty files and rejects oversized input before reading it', async () => {
    const original = new File([], 'empty.txt', { type: 'text/plain' })
    const { blob, key } = await encryptFile(original)
    expect((await decryptFile(blob, key)).size).toBe(0)
    const large = { size: MAX_ENCRYPTED_FILE_BYTES + 1 } as File
    await expect(encryptFile(large)).rejects.toThrow('20 MiB')
  })
})

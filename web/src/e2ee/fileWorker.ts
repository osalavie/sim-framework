import { encryptFile, decryptFile } from './fileCipher'

self.onmessage = async (event: MessageEvent) => {
  const { file, key } = event.data
  try {
    const value = key ? await decryptFile(file, key) : await encryptFile(file)
    self.postMessage({ ok: true, value })
  } catch { self.postMessage({ ok: false }) }
  finally { key?.fill(0) }
}

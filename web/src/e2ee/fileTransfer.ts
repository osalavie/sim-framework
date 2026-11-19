import { fromBase64Url, toBase64Url } from './encoding'
import { encryptedFileType, MAX_ENCRYPTED_FILE_BYTES } from './fileCipher'
import { registerE2eeCancellation } from './keysAsync'
import { getE2eeProfile } from './profile'

function fileJob<T>(file: Blob, key?: Uint8Array): Promise<T> {
  return new Promise((resolve, reject) => {
    const worker = new Worker(new URL('./fileWorker.ts', import.meta.url), { type: 'module' })
    const unregister = registerE2eeCancellation(() => { finish(); reject(new Error('Encrypted file operation cancelled.')) })
    const finish = () => { clearTimeout(timer); worker.terminate(); unregister() }
    const timer = setTimeout(() => { finish(); reject(new Error('Encrypted file operation timed out.')) }, 120_000)
    worker.onmessage = (event) => { finish(); if (event.data.ok) resolve(event.data.value); else reject(new Error('Encrypted file operation failed.')) }
    worker.onerror = () => { finish(); reject(new Error('Encrypted file worker failed.')) }
    try { worker.postMessage({ file, key }) } catch { finish(); reject(new Error('Encrypted file operation could not start.')) }
  })
}

export function isEncryptedFileUrl(url: string): boolean { return /#ef=[A-Za-z0-9_-]{22}$/.test(url) }

export async function prepareEncryptedUpload(file: File): Promise<{ file: File; fragment: string }> {
  if (file.size > MAX_ENCRYPTED_FILE_BYTES) throw new Error('Encrypted file exceeds 20 MiB.')
  const encrypted = await fileJob<{ blob: Blob; key: Uint8Array }>(file)
  try {
    return { file: new File([encrypted.blob], `${crypto.randomUUID()}${getE2eeProfile().fileExtension}`, { type: encryptedFileType() }),
      fragment: `#ef=${toBase64Url(encrypted.key)}` }
  } finally { encrypted.key.fill(0) }
}

export async function decryptDownloadedFile(blob: Blob, url: string): Promise<File> {
  if (!isEncryptedFileUrl(url)) throw new Error('Missing encrypted file key.')
  const key = fromBase64Url(url.slice(url.lastIndexOf('#ef=') + 4))
  try { return await fileJob<File>(blob, key) } finally { key.fill(0) }
}

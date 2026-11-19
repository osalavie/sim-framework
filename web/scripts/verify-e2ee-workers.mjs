import { createServer } from 'node:http'
import { readFile, readdir } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { chromium } from '@playwright/test'

const dist = fileURLToPath(new URL('../dist/', import.meta.url))
const assets = await readdir(path.join(dist, 'assets'))
const keyWorker = assets.find((name) => /^keyWorker-.*\.js$/.test(name))
const fileWorker = assets.find((name) => /^fileWorker-.*\.js$/.test(name))
if (!keyWorker || !fileWorker) throw new Error('Build both E2EE module workers before verification.')
const server = createServer(async (request, response) => {
  response.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; worker-src 'self'; object-src 'none'")
  if (request.url === '/') { response.end('<!doctype html><title>Worker verification</title>'); return }
  const name = assets.find((entry) => request.url === `/assets/${entry}`)
  if (!name) { response.writeHead(404).end(); return }
  response.setHeader('Content-Type', 'text/javascript')
  response.end(await readFile(path.join(dist, 'assets', name)))
})
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve))
let browser
try {
  browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHROMIUM_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHROMIUM_CHANNEL } : {}) })
  const page = await browser.newPage()
  await page.goto(`http://127.0.0.1:${server.address().port}/`)
  const result = await page.evaluate(async ({ keyWorker, fileWorker }) => {
    const job = (script, payload) => new Promise((resolve, reject) => {
      const worker = new Worker(`/assets/${script}`, { type: 'module' })
      const timer = setTimeout(() => { worker.terminate(); reject(new Error('Worker timeout')) }, 60_000)
      worker.onmessage = (event) => { clearTimeout(timer); worker.terminate(); resolve(event.data) }
      worker.onerror = () => { clearTimeout(timer); worker.terminate(); reject(new Error('Worker loading failed')) }
      worker.postMessage(payload)
    })
    let ticks = 0
    const timer = setInterval(() => { ticks++ }, 10)
    const created = await job(keyWorker, { operation: 'createIdentitySecrets', args: ['test-only strong password', 'test-user', 1, 600_000] })
    clearInterval(timer)
    if (!created.ok || ticks < 2) throw new Error('KDF blocked the page or failed')
    const secrets = created.value
    const salt = btoa(String.fromCharCode(...secrets.salt)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
    const identity = { kdfSalt: salt, kdfIterations: 600_000, keyVersion: 1, passwordWrappedKey: secrets.passwordWrappedKey }
    const unlocked = await job(keyWorker, { operation: 'unlockUmkWithPassphrase', args: ['test-only strong password', identity, 'test-user'] })
    if (!unlocked.ok || !unlocked.value.every((value, index) => value === secrets.umk[index])) throw new Error('Worker key round-trip failed')
    const encrypted = await job(fileWorker, { file: new File(['test-only attachment'], 'private.txt', { type: 'text/plain' }) })
    if (!encrypted.ok) throw new Error('File encryption failed')
    const decrypted = await job(fileWorker, { file: encrypted.value.blob, key: encrypted.value.key })
    if (!decrypted.ok || decrypted.value.name !== 'private.txt' || await decrypted.value.text() !== 'test-only attachment') throw new Error('File worker round-trip failed')
    const wrongKey = await job(fileWorker, { file: encrypted.value.blob, key: new Uint8Array(16) })
    if (wrongKey.ok) throw new Error('File worker accepted a wrong key')
    secrets.umk.fill(0); secrets.recoveryKey.fill(0); unlocked.value.fill(0); encrypted.value.key.fill(0)
    return { responsive: true, keyRoundTrip: true, fileRoundTrip: true, wrongKeyRejected: true }
  }, { keyWorker, fileWorker })
  console.log(JSON.stringify(result))
} finally {
  await browser?.close()
  await new Promise((resolve) => server.close(resolve))
}

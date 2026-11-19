import { build } from 'vite'
import { chromium } from '@playwright/test'
import { createServer } from 'node:http'
import { readFile, mkdir, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../', import.meta.url))
const directory = `${root}node_modules/.cache/e2ee-device-cache-check`
await mkdir(directory, { recursive: true })
await writeFile(`${directory}/entry.ts`, `export * from ${JSON.stringify(`${root}src/e2ee/deviceStore.ts`)}; export { createDeviceWrap } from ${JSON.stringify(`${root}src/e2ee/keys.ts`)};`)
await build({ configFile: false, root, logLevel: 'error', build: {
  outDir: directory, emptyOutDir: false,
  lib: { entry: `${directory}/entry.ts`, formats: ['es'], fileName: () => 'device-store.js' },
} })
const script = await readFile(`${directory}/device-store.js`)
const server = createServer((request, response) => {
  response.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; object-src 'none'")
  if (request.url === '/') { response.end('<!doctype html><title>Trusted device verification</title>'); return }
  if (request.url !== '/device-store.js') { response.writeHead(404).end(); return }
  response.setHeader('Content-Type', 'text/javascript')
  response.end(script)
})
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve))
let browser
try {
  browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHROMIUM_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHROMIUM_CHANNEL } : {}) })
  const context = await browser.newContext()
  const page = await context.newPage()
  await page.goto(`http://127.0.0.1:${server.address().port}/`)
  for (const phase of ['save', 'reload']) {
    if (phase === 'reload') await page.reload()
    await page.evaluate(async (phase) => {
      const cache = await import('/device-store.js')
      const raw = async (update) => {
        const db = await new Promise((resolve, reject) => {
          const request = indexedDB.open('sim-e2ee')
          request.onsuccess = () => resolve(request.result)
          request.onerror = () => reject(request.error)
        })
        try {
          return await new Promise((resolve, reject) => {
            const tx = db.transaction('device-umk', update ? 'readwrite' : 'readonly')
            const request = update ? tx.objectStore('device-umk').put(update) : tx.objectStore('device-umk').get('owner')
            tx.oncomplete = () => resolve(request.result)
            tx.onabort = () => reject(tx.error)
            tx.onerror = () => reject(tx.error)
          })
        } finally { db.close() }
      }
      const input = { userId: 'owner', keyVersion: 1, umk: new Uint8Array(16).fill(7) }
      window.readRawForTest = raw
      if (phase === 'save') {
        await cache.saveDeviceCache(input)
        const stored = await raw()
        if ('deviceKey' in stored || 'umk' in stored || 'wrappedUmk' in stored || stored.wrappingKey.extractable || stored.format !== 'device-cache-v3') throw new Error('Raw or exportable key persisted')
        let blocked = false
        try { await crypto.subtle.exportKey('raw', stored.wrappingKey) } catch { blocked = true }
        if (!blocked) throw new Error('Device wrapping key is exportable')
        return
      }
      const restored = await cache.loadDeviceCache('owner')
      if (!restored?.umk.every((byte) => byte === 7)) throw new Error('IndexedDB key did not survive reload')
      restored.umk.fill(0)
      await raw({ userId: input.userId, keyVersion: input.keyVersion, ...cache.createDeviceWrap(input.umk, input.userId, input.keyVersion) })
      const legacy = await cache.loadDeviceCache('owner')
      if (!legacy?.umk.every((byte) => byte === 7) || 'deviceKey' in await raw()) throw new Error('Legacy cache was not upgraded')
      legacy.umk.fill(0)
      const tampered = await raw()
      tampered.keyVersion += 1
      await raw(tampered)
      if (await cache.loadDeviceCache('owner') !== null || await raw()) throw new Error('Corrupt cache was accepted or retained')
      await cache.selectPasskeyUnlock('owner', 'selected-passkey')
      if (await cache.loadDeviceCache('owner') || await raw()) throw new Error('Passkey selection retained an auto-unlock cache')
      await cache.saveDeviceCache(input)
      const trusted = await cache.loadDeviceCache('owner')
      if (!trusted?.umk.every((byte) => byte === 7)) throw new Error('Trusted device cache is blocked by passkey preference')
      trusted.umk.fill(0)
      await cache.clearDeviceCache('owner')
      await cache.selectPasskeyUnlock('owner', null)
      const encrypt = crypto.subtle.encrypt.bind(crypto.subtle)
      let release
      let started
      const ready = new Promise((resolve) => { started = resolve })
      crypto.subtle.encrypt = async (...args) => {
        started()
        await new Promise((resolve) => { release = resolve })
        return encrypt(...args)
      }
      const saving = cache.saveDeviceCache(input).then(() => false, () => true)
      await ready
      await cache.clearDeviceCache('owner')
      release()
      if (!await saving || await raw()) throw new Error('Pending save restored a cleared trust record')
      crypto.subtle.encrypt = encrypt
    }, phase)
  }
  console.log(JSON.stringify({ nonExportable: true, survivesReload: true, legacyMigration: true, tamperRejected: true, pendingSaveCancelled: true, passkeyPreferredCacheAllowed: true }))
} finally {
  await browser?.close()
  await new Promise((resolve) => server.close(resolve))
}

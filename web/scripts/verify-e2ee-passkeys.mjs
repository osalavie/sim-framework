import { build } from 'vite'
import { chromium } from '@playwright/test'
import { createServer } from 'node:http'
import { mkdir, writeFile, readFile } from 'node:fs/promises'
import { randomBytes, randomUUID } from 'node:crypto'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../', import.meta.url))
const directory = `${root}node_modules/.cache/e2ee-passkey-check`
await mkdir(directory, { recursive: true })
await writeFile(`${directory}/entry.ts`, `export * from ${JSON.stringify(`${root}src/e2ee/passkeys.ts`)};
export { cancelE2eeKeyOperations } from ${JSON.stringify(`${root}src/e2ee/keysAsync.ts`)};
export { getPasskeyDiagnosticReport } from ${JSON.stringify(`${root}src/e2ee/passkeyDiagnostics.ts`)};
export { loadDeviceCache } from ${JSON.stringify(`${root}src/e2ee/deviceStore.ts`)};
export { formatPasskeyName } from ${JSON.stringify(`${root}src/utils/deviceEnvironment.ts`)};`)
await build({ configFile: false, root, logLevel: 'error', resolve: { alias: { '@': `${root}src` } }, build: {
  outDir: directory, emptyOutDir: false,
  lib: { entry: `${directory}/entry.ts`, formats: ['es'], fileName: () => 'passkeys.js' },
} })
const script = await readFile(`${directory}/passkeys.js`)
const server = createServer((request, response) => {
  if (request.url === '/') { response.end('<!doctype html><title>Passkey verification</title>'); return }
  if (request.url !== '/passkeys.js') { response.writeHead(404).end(); return }
  response.setHeader('Content-Type', 'text/javascript'); response.end(script)
})
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve))
let browser
try {
  browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHROMIUM_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHROMIUM_CHANNEL } : {}) })
  const page = await browser.newPage()
  const cdp = await page.context().newCDPSession(page)
  await cdp.send('WebAuthn.enable')
  const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', { options: {
    protocol: 'ctap2', ctap2Version: 'ctap2_1', transport: 'internal', hasResidentKey: true, hasUserVerification: true,
    isUserVerified: true, automaticPresenceSimulation: true, hasPrf: true,
  } })
  const salt = randomBytes(32).toString('base64url')
  const id = randomUUID()
  let saved
  let registrations = 0
  let assertions = 0
  await page.route('**/api/system/e2ee-passkey/**', async (route) => {
    const url = new URL(route.request().url())
    let json
    if (url.pathname.endsWith('/registration-options')) json = {
      challengeId: randomUUID(), keyVersion: 1, prfSalt: salt, publicKey: {
        challenge: randomBytes(32).toString('base64url'), rp: { id: 'localhost', name: 'E2EE test' },
        user: { id: randomBytes(32).toString('base64url'), name: 'owner', displayName: 'Owner' },
        pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
      },
    }
    else if (url.pathname.endsWith('/registration')) {
      const body = route.request().postDataJSON()
      if (JSON.stringify(body).includes('clientExtensionResults') || JSON.stringify(body).includes('"first"')) throw new Error('PRF result sent to server')
      saved = body
      registrations++
      json = { id, keyVersion: 1, name: body.name, rpId: 'localhost' }
    }
    else if (url.pathname.endsWith('/unlock-options')) json = {
      challengeId: randomUUID(), keyVersion: 1, prfSalt: salt, publicKey: {
        challenge: randomBytes(32).toString('base64url'), rpId: 'localhost',
        allowCredentials: [{ type: 'public-key', id: saved.credential.rawId }],
      },
    }
    else if (url.pathname.endsWith('/unlock')) {
      const body = route.request().postDataJSON()
      if (JSON.stringify(body).includes('clientExtensionResults') || JSON.stringify(body).includes('"first"')) throw new Error('PRF result sent to server')
      assertions++
      json = { credentialId: saved.credential.rawId, rpId: 'localhost', prfSalt: salt, wrappedUmk: saved.wrappedUmk, keyVersion: 1 }
    } else throw new Error('Unexpected passkey API request')
    await route.fulfill({ json })
  })
  await page.goto(`http://localhost:${server.address().port}/`)
  await page.evaluate(async () => {
    const module = await import('/passkeys.js')
    const umk = new Uint8Array(16).fill(7)
    await module.registerPasskey('owner', umk, 1)
    if (await module.loadDeviceCache('owner') !== null) throw new Error('Passkey registration retained an auto-unlock cache')
    umk.fill(0)
  })
  const expectedName = await page.evaluate(async () => (await import('/passkeys.js')).formatPasskeyName())
  if (saved.name !== expectedName) throw new Error('Passkey name does not match the device environment')
  if (saved.name.startsWith('SHA256:')) throw new Error('Passkey name must not contain a credential fingerprint')
  await page.reload()
  await page.evaluate(async (id) => {
    const module = await import('/passkeys.js')
    const umk = await module.unlockWithPasskey('owner', id, 1)
    if (!umk.every((byte) => byte === 7)) throw new Error('PRF did not recover the original UMK')
    umk.fill(0)
  }, id)
  await cdp.send('WebAuthn.setAutomaticPresenceSimulation', { authenticatorId, enabled: false })
  await page.evaluate((id) => {
    window.pendingUnlock = import('/passkeys.js').then((module) => module.unlockWithPasskey('owner', id, 1)).then(() => 'unexpected', (error) => error.code)
  }, id)
  await page.waitForTimeout(100)
  await page.evaluate(async () => (await import('/passkeys.js')).cancelE2eeKeyOperations())
  if (await page.evaluate(() => window.pendingUnlock) !== 'cancelled' || assertions !== 1) throw new Error('Cancelled operation continued')
  await cdp.send('WebAuthn.setAutomaticPresenceSimulation', { authenticatorId, enabled: true })
  const missingPrf = await page.evaluate(async (id) => {
    const module = await import('/passkeys.js')
    const original = navigator.credentials.get.bind(navigator.credentials)
    navigator.credentials.get = async (...args) => {
      const result = await original(...args)
      Object.defineProperty(result, 'getClientExtensionResults', { value: () => ({}) })
      return result
    }
    try {
      let registrationRejected = false
      let unlockRejected = false
      try { await module.registerPasskey('owner', new Uint8Array(16).fill(7), 1) }
      catch (error) { registrationRejected = error.code === 'prf-unavailable' }
      try { await module.unlockWithPasskey('owner', id, 1) }
      catch (error) { unlockRejected = error.code === 'prf-unavailable' }
      return registrationRejected && unlockRejected
    } finally { navigator.credentials.get = original }
  }, id)
  if (!missingPrf || registrations !== 1 || assertions !== 1) throw new Error('Missing assertion PRF was accepted or submitted')
  const inconsistentPrf = await page.evaluate(async () => {
    const original = navigator.credentials.create.bind(navigator.credentials)
    navigator.credentials.create = async (...args) => {
      const result = await original(...args)
      Object.defineProperty(result, 'getClientExtensionResults', { value: () => ({
        prf: { enabled: true, results: { first: new Uint8Array(32).fill(255).buffer } },
      }) })
      return result
    }
    try {
      await (await import('/passkeys.js')).registerPasskey('owner', new Uint8Array(16).fill(7), 1)
      return false
    } catch (error) { return error.code === 'prf-unavailable' }
    finally { navigator.credentials.create = original }
  })
  if (!inconsistentPrf || registrations !== 1) throw new Error('Inconsistent PRF was registered')
  const deniedDiagnostic = await page.evaluate(async () => {
    const module = await import('/passkeys.js')
    const original = navigator.credentials.create.bind(navigator.credentials)
    navigator.credentials.create = async () => { throw new DOMException('sensitive-native-error-message', 'NotAllowedError') }
    let code
    try { await module.registerPasskey('owner', new Uint8Array(16).fill(7), 1) }
    catch (error) { code = error.code }
    finally { navigator.credentials.create = original }
    const report = module.getPasskeyDiagnosticReport()
    const events = JSON.parse(report).events
    return code === 'not-allowed' && events.some(event => event.stage === 'credential-create.failed' && event.exception === 'NotAllowedError') &&
      events.some(event => event.stage === 'registration-get-result' && event.firstLength === 0) && !report.includes('sensitive-native-error-message')
  })
  if (!deniedDiagnostic) throw new Error('Passkey failure diagnostics missing or leaking native messages')
  await cdp.send('WebAuthn.removeVirtualAuthenticator', { authenticatorId })
  await cdp.send('WebAuthn.addVirtualAuthenticator', { options: {
    protocol: 'ctap2', transport: 'internal', hasResidentKey: true, hasUserVerification: true,
    isUserVerified: true, automaticPresenceSimulation: true, hasPrf: false,
  } })
  const unsupported = await page.evaluate(async () => {
    try { await (await import('/passkeys.js')).registerPasskey('owner', new Uint8Array(16).fill(7), 1); return false }
    catch (error) { return error.code === 'prf-unavailable' }
  })
  if (!unsupported || registrations !== 1) throw new Error('Unsupported credential was registered')
  console.log(JSON.stringify({ virtualPrfRegistration: true, deviceEnvironmentName: true, unlockAfterReload: true, noPrfSentToServer: true, noAutoUnlockBypass: true, cancellation: true, unsupportedRejected: true, missingPrfRegistrationRejected: true, missingPrfUnlockRejected: true, inconsistentPrfRejected: true, safeStageDiagnostics: true }))
} finally {
  await browser?.close()
  await new Promise((resolve) => server.close(resolve))
}

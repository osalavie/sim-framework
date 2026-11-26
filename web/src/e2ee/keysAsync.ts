import type * as Keys from './keys'
import { KDF_ITERATIONS, validateIdentityKdf } from './kdf'
import { fromBase64Url } from './encoding'

export type KeyOperation =
  | 'createIdentitySecrets'
  | 'unlockUmkWithPassphrase'
  | 'rewrapWithPassphrase'
  | 'deriveShareKey'

const pending = new Set<() => void>()
export function registerE2eeCancellation(cancel: () => void): () => void {
  pending.add(cancel)
  return () => pending.delete(cancel)
}

/** Logout/locking must prevent an outstanding derivation from unlocking again. */
export function cancelE2eeKeyOperations(): void {
  for (const cancel of [...pending]) cancel()
}

function runKeyOperation<T extends KeyOperation>(
  operation: T,
  args: Parameters<(typeof Keys)[T]>,
): Promise<ReturnType<(typeof Keys)[T]>> {
  return new Promise((resolve, reject) => {
    // Vue DTOs can be proxies; copy the flat identity before structured cloning.
    const values = args.map((arg) => arg && typeof arg === 'object' && !(arg instanceof Uint8Array)
      ? { ...arg } : arg)
    if (operation === 'deriveShareKey') {
      validateIdentityKdf(values[1] as Uint8Array, (values[2] as number | undefined) ?? KDF_ITERATIONS)
    } else if (operation === 'createIdentitySecrets') {
      validateIdentityKdf(new Uint8Array(16), (values[3] as number | undefined) ?? KDF_ITERATIONS)
    } else {
      const identity = values[operation === 'rewrapWithPassphrase' ? 3 : 1] as Keys.IdentityWraps
      validateIdentityKdf(fromBase64Url(identity.kdfSalt ?? ''),
        identity.kdfIterations ?? KDF_ITERATIONS)
    }
    const worker = new Worker(new URL('./keyWorker.ts', import.meta.url), { type: 'module' })
    const cancel = () => { finish(); reject(new Error('E2EE key operation cancelled.')) }
    const finish = () => { clearTimeout(timeout); worker.terminate(); pending.delete(cancel) }
    const timeout = setTimeout(() => {
      finish()
      reject(new Error('E2EE key operation timed out.'))
    }, 120_000)
    pending.add(cancel)
    worker.onmessage = (event) => {
      finish()
      if (event.data.ok) resolve(event.data.value)
      else reject(new Error('E2EE key operation failed.'))
    }
    worker.onerror = () => {
      finish()
      reject(new Error('E2EE key worker failed.'))
    }
    try { worker.postMessage({ operation, args: values }) } catch {
      finish()
      reject(new Error('E2EE key operation could not start.'))
    }
  })
}

export const createIdentitySecretsAsync = (...args: Parameters<typeof Keys.createIdentitySecrets>) =>
  runKeyOperation('createIdentitySecrets', args)
export const unlockUmkWithPassphraseAsync = (...args: Parameters<typeof Keys.unlockUmkWithPassphrase>) =>
  runKeyOperation('unlockUmkWithPassphrase', args)
export const rewrapWithPassphraseAsync = (...args: Parameters<typeof Keys.rewrapWithPassphrase>) =>
  runKeyOperation('rewrapWithPassphrase', args)
export const deriveShareKeyAsync = (...args: Parameters<typeof Keys.deriveShareKey>) =>
  runKeyOperation('deriveShareKey', args)

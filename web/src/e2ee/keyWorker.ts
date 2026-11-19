import * as keys from './keys'
import type { KeyOperation } from './keysAsync'

const scope = self as unknown as {
  onmessage: ((event: MessageEvent) => void) | null
  postMessage: (message: unknown) => void
}

scope.onmessage = (event: MessageEvent<{ operation: KeyOperation; args: unknown[] }>) => {
  const { operation, args } = event.data
  try {
    const operations = {
      createIdentitySecrets: keys.createIdentitySecrets,
      unlockUmkWithPassphrase: keys.unlockUmkWithPassphrase,
      rewrapWithPassphrase: keys.rewrapWithPassphrase,
      deriveShareKey: keys.deriveShareKey,
    }
    if (!Object.prototype.hasOwnProperty.call(operations, operation)) throw new Error('Unsupported key operation.')
    const operationFn = operations[operation] as (...args: unknown[]) => unknown
    scope.postMessage({ ok: true, value: operationFn(...args) })
  } catch {
    // Do not send exception messages or caller data across the worker boundary.
    scope.postMessage({ ok: false })
  } finally {
    for (const arg of args) if (arg instanceof Uint8Array) arg.fill(0)
  }
}

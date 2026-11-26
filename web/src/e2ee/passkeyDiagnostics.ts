import { getE2eeProfile } from './profile'

/** Local, bounded diagnostics. Never accept credentials, extension objects, HTTP bodies or errors as log fields. */
type Details = {
  secureContext?: boolean; webAuthn?: boolean; create?: boolean; get?: boolean; webCrypto?: boolean;
  browser?: string; rpId?: string; origin?: string; attachment?: string; allowCount?: number; excludeCount?: number; saltLength?: number;
  prfPresent?: boolean; prfEnabled?: boolean; firstLength?: number; credentialMatches?: boolean;
  outputsMatch?: boolean; httpStatus?: number; exception?: string; code?: string;
}
const history: Array<Record<string, unknown>> = []
const fields = new Set(['secureContext', 'webAuthn', 'create', 'get', 'webCrypto', 'browser', 'rpId',
  'origin', 'attachment', 'allowCount', 'excludeCount', 'saltLength', 'prfPresent', 'prfEnabled', 'firstLength',
  'credentialMatches', 'outputsMatch', 'httpStatus', 'exception', 'code'])
const exceptionNames = new Set(['AbortError', 'NotAllowedError', 'NotSupportedError', 'SecurityError',
  'InvalidStateError', 'ConstraintError', 'TypeError', 'OperationError', 'DataError', 'AxiosError', 'PasskeyError'])
export function getPasskeyDiagnosticReport(): string {
  return JSON.stringify({ format: getE2eeProfile().passkeyDiagnosticsFormat, events: history }, null, 2)
}
export function beginPasskeyDiagnostic(kind: 'registration' | 'unlock') {
  const id = globalThis.crypto?.randomUUID?.() ?? 'trace-' + Math.random().toString(36).slice(2)
  let stage = 'start'
  function mark(next: string, details: Details = {}) {
    stage = next
    const event: Record<string, unknown> = { time: new Date().toISOString(), id, kind, stage }
    for (const [key, value] of Object.entries(details)) {
      if (!fields.has(key)) continue
      if (typeof value === 'boolean' || typeof value === 'number' && Number.isFinite(value)) event[key] = value
      else if (typeof value === 'string' && value.length <= 253 && /^[a-zA-Z0-9._:/ -]+$/.test(value)) event[key] = value
    }
    history.push(event)
    if (history.length > 100) history.shift()
    // Serialize immediately: DevTools must not keep a live object containing future mutations.
    console.info('[E2EE Passkey] ' + JSON.stringify(event))
  }
  function fail(value: unknown, code: string) {
    const error = value as { name?: unknown; response?: { status?: unknown } } | null
    const exception = typeof error?.name === 'string' && exceptionNames.has(error.name) ? error.name : 'OtherError'
    const status = error?.response?.status
    mark(stage + '.failed', { exception, code, ...(typeof status === 'number' ? { httpStatus: status } : {}) })
  }
  const browser = typeof navigator === 'undefined' ? '' : (navigator.userAgent.match(/Edg\/[\d.]+/) ?? navigator.userAgent.match(/(?:Chrome|Firefox)\/[\d.]+/))?.[0] ?? 'Other'
  mark('start', { browser, origin: typeof window !== 'undefined' ? window.location.origin : undefined, secureContext: typeof window !== 'undefined' && window.isSecureContext,
    webAuthn: typeof PublicKeyCredential !== 'undefined', create: typeof globalThis.navigator?.credentials?.create === 'function',
    get: typeof globalThis.navigator?.credentials?.get === 'function', webCrypto: Boolean(globalThis.crypto?.subtle) })
  return { mark, fail }
}

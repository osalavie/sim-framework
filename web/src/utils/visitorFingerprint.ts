import { BRAND_NAME } from '@/config/branding'

const STORAGE_KEY = 'SIM_VISITOR_FINGERPRINT'
let fingerprintPromise: Promise<string> | undefined

function readStoredFingerprint(): string | undefined {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return value && /^[a-f0-9]{64}$/.test(value) ? value : undefined
  } catch {
    return undefined
  }
}

function storeFingerprint(value: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, value)
  } catch {
    // Privacy modes may disable storage; the deterministic fingerprint still works for this session.
  }
}

function getCanvasSignature(): string {
  const canvas = document.createElement('canvas')
  canvas.width = 220
  canvas.height = 48
  const context = canvas.getContext('2d')
  if (!context) return 'canvas-unavailable'

  context.textBaseline = 'alphabetic'
  context.fillStyle = '#d55d35'
  context.fillRect(4, 6, 34, 18)
  context.fillStyle = '#1d211f'
  context.font = '15px Arial'
  context.fillText(`${BRAND_NAME} · Time Capture`, 8, 38)
  return canvas.toDataURL()
}

function fallbackHash(value: string): string {
  const parts = Array.from({ length: 8 }, (_, index) => {
    let hash = 0x811c9dc5 ^ index
    for (let offset = 0; offset < value.length; offset += 1) {
      hash ^= value.charCodeAt(offset)
      hash = Math.imul(hash, 0x01000193)
    }
    return (hash >>> 0).toString(16).padStart(8, '0')
  })
  return parts.join('')
}

async function createFingerprint(): Promise<string> {
  const navigatorWithMemory = navigator as Navigator & { deviceMemory?: number }
  const signals = [
    navigator.userAgent,
    navigator.platform,
    navigator.language,
    navigator.languages.join(','),
    navigator.hardwareConcurrency,
    navigatorWithMemory.deviceMemory ?? 'unknown',
    `${screen.width}x${screen.height}x${screen.colorDepth}`,
    Intl.DateTimeFormat().resolvedOptions().timeZone,
    getCanvasSignature(),
  ].join('|')

  if (crypto.subtle) {
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(signals))
    return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('')
  }

  return fallbackHash(signals)
}

export function getVisitorFingerprint(): Promise<string> {
  const stored = readStoredFingerprint()
  if (stored) return Promise.resolve(stored)

  fingerprintPromise ??= createFingerprint().then((fingerprint) => {
    storeFingerprint(fingerprint)
    return fingerprint
  })
  return fingerprintPromise
}

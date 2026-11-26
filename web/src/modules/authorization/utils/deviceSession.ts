import { DEVICE_ID_KEY } from '@/stores/persistentKeys'
import {
  detectDeviceModel,
  detectDevicePlatform,
  getDeviceEnvironment,
  shortPlatformLabel,
  type DeviceType,
} from '@/utils/deviceEnvironment'
import { getVisitorFingerprint } from '@/utils/visitorFingerprint'

export type LoginDeviceType = DeviceType

export interface LoginDeviceDescriptor {
  deviceId: string
  deviceType: LoginDeviceType
  deviceName: string
}

function createDeviceId(): string {
  if (typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  return Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('')
}

export async function getLoginDeviceDescriptor(): Promise<LoginDeviceDescriptor> {
  let deviceId: string | null = null
  try {
    deviceId = localStorage.getItem(DEVICE_ID_KEY)
  } catch {
    // Storage can be unavailable in hardened/private browser contexts.
  }
  if (!deviceId) {
    deviceId = createDeviceId()
    try {
      localStorage.setItem(DEVICE_ID_KEY, deviceId)
    } catch {
      // The current login still receives an ephemeral identifier.
    }
  }

  const { deviceType, platform } = getDeviceEnvironment()
  const userAgent = typeof navigator === 'undefined' ? '' : navigator.userAgent
  const maxTouchPoints = typeof navigator === 'undefined' ? 0 : navigator.maxTouchPoints
  // 指纹只是展示前缀，采集失败不得阻断登录。
  const [model, fingerprint] = await Promise.all([
    detectDeviceModel(),
    getVisitorFingerprint().catch(() => ''),
  ])
  // 有型号时用型号；无型号时用可读系统名。第二段始终是浏览器平台，例如
  // `PMA110 · armv81 · 3f9a1c`、`Windows · Win32 · 3f9a1c`。
  const platformLabel = shortPlatformLabel(platform, userAgent)
  const primaryLabel = model || detectDevicePlatform(userAgent, platform, maxTouchPoints)
  const deviceName = [...new Set([primaryLabel, platformLabel, fingerprint.slice(0, 6)])]
    .filter(Boolean)
    .join(' · ')

  return { deviceId, deviceType, deviceName }
}

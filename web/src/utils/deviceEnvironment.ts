/** 设备与浏览器环境描述：登录会话与通行密钥名称共用同一套判定，不采集硬件指纹。 */

export type DeviceType = 'PC' | 'Pad' | 'Phone'

export interface DeviceEnvironment {
  deviceType: DeviceType
  platform: string
  browser: string
  /** 设备名，例如 `PC · Win32`。 */
  deviceName: string
}

export function detectDeviceType(userAgent: string, maxTouchPoints = 0): DeviceType {
  const isPad =
    /iPad|Tablet/i.test(userAgent) ||
    (/Android/i.test(userAgent) && !/Mobile/i.test(userAgent)) ||
    (/Macintosh/i.test(userAgent) && maxTouchPoints > 1)
  if (isPad) return 'Pad'
  return /Mobile|iPhone|Android/i.test(userAgent) ? 'Phone' : 'PC'
}

/** 浏览器名称与主版本，例如 `Chrome 141`；未知浏览器回退为 `Browser`。 */
export function detectBrowser(userAgent: string): string {
  const rules: Array<[RegExp, string]> = [
    [/Edg\/(\d+)/, 'Edge'],
    [/OPR\/(\d+)/, 'Opera'],
    [/SamsungBrowser\/(\d+)/, 'Samsung Internet'],
    [/(?:Firefox|FxiOS)\/(\d+)/, 'Firefox'],
    [/(?:Chrome|CriOS)\/(\d+)/, 'Chrome'],
    [/Version\/(\d+).*Safari/, 'Safari'],
  ]
  for (const [pattern, label] of rules) {
    const version = userAgent.match(pattern)?.[1]
    if (version) return `${label} ${version}`
  }
  return 'Browser'
}

export function getDeviceEnvironment(): DeviceEnvironment {
  const userAgent = typeof navigator === 'undefined' ? '' : navigator.userAgent
  const maxTouchPoints = typeof navigator === 'undefined' ? 0 : navigator.maxTouchPoints
  const platform = typeof navigator === 'undefined' ? '' : (navigator.platform?.trim() ?? '')
  const deviceType = detectDeviceType(userAgent, maxTouchPoints)
  return {
    deviceType,
    platform,
    browser: detectBrowser(userAgent),
    deviceName: platform ? `${deviceType} · ${platform}` : deviceType,
  }
}

/** Readable OS fallback for passkey and session labels. */
export function detectDevicePlatform(userAgent: string, platform = '', maxTouchPoints = 0): string {
  if (/iPad/i.test(userAgent) || (/Macintosh/i.test(userAgent) && maxTouchPoints > 1)) return 'iPadOS'
  if (/iPhone|iPod/i.test(userAgent)) return 'iOS'
  if (/Android/i.test(userAgent)) return 'Android'
  if (/Windows/i.test(userAgent) || /^Win/i.test(platform)) return 'Windows'
  if (/CrOS/i.test(userAgent)) return 'ChromeOS'
  if (/Mac/i.test(userAgent) || /Mac/i.test(platform)) return 'macOS'
  if (/Linux/i.test(userAgent) || /Linux/i.test(platform)) return 'Linux'
  return 'Device'
}

type PasskeyNavigator = Pick<Navigator, 'userAgent' | 'platform' | 'maxTouchPoints'> & {
  userAgentData?: { getHighEntropyValues(hints: string[]): Promise<{ model?: string }> }
}

/** 平台短名：去掉 `Linux` 前缀保留架构，例如 `Linux armv81` → `armv81`。 */
export function shortPlatformLabel(platform: string, userAgent = ''): string {
  const value = platform.replace(/^Linux\s+/i, '').trim()
  return value || detectDevicePlatform(userAgent)
}

/** Model is optional and may be withheld. Never delay registration indefinitely for a label. */
export async function detectDeviceModel(
  source: PasskeyNavigator | undefined = typeof navigator === 'undefined' ? undefined : navigator,
): Promise<string> {
  let timer: ReturnType<typeof setTimeout> | undefined
  try {
    if (source?.userAgentData) {
      const hints = await Promise.race([
        source.userAgentData.getHighEntropyValues(['model']),
        new Promise<{ model?: string }>(resolve => { timer = setTimeout(() => resolve({}), 500) }),
      ])
      const model = (hints.model ?? '').replace(/\s+/g, ' ').trim().slice(0, 60)
      // Some Android devices report `K` as a placeholder model.
      return model === 'K' ? '' : model
    }
  } catch { /* Unsupported or denied hints are only a naming fallback. */ }
  finally { if (timer !== undefined) clearTimeout(timer) }
  return ''
}

export async function formatPasskeyName(source: PasskeyNavigator | undefined = typeof navigator === 'undefined' ? undefined : navigator): Promise<string> {
  const userAgent = source?.userAgent ?? ''
  const platform = detectDevicePlatform(userAgent, source?.platform, source?.maxTouchPoints)
  const model = await detectDeviceModel(source)
  return `${model || platform} · ${detectBrowser(userAgent).replace(/ \d+$/, '')}`
}

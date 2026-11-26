import { apiConfig } from '@/config/apiConfig'
import { applySiteConfiguration } from '@/config/branding'
import type { AppLocale } from '@/i18n'
import type { ClientLocalizationDto } from '@/api/admin'

type MessageDictionary = Record<string, string>

interface ClientLocalizationResponse {
  culture: string
  site: {
    name: string
    defaultLanguage: string
    defaultTheme: string
    registrationEnabled: boolean
  }
  texts: MessageDictionary
}

type ClientLocalizationContract = ClientLocalizationDto

export const backendMessages: Record<AppLocale, MessageDictionary> = {
  'zh-CN': {},
  'en-US': {},
}

let initializationPromise: Promise<void> | undefined
let siteConfigurationRefreshPromise: Promise<void> | undefined

async function loadLocale(locale: AppLocale): Promise<ClientLocalizationResponse> {
  const culture = locale === 'zh-CN' ? 'zh-Hans' : 'en'
  const baseUrl = apiConfig.baseUrl.replace(/\/$/, '')
  // culture 同时写入查询参数：与请求头相比，查询参数会进入缓存键，
  // 避免浏览器或反向代理把另一种语言的响应复用给当前语言。
  const query = new URLSearchParams({ culture, 'ui-culture': culture })
  const response = await fetch(`${baseUrl}/api/admin/client-localization?${query.toString()}`, {
    headers: {
      Accept: 'application/json',
      'Accept-Language': culture,
    },
    cache: 'no-store',
  })

  if (!response.ok) {
    throw new Error(`Backend localization request failed (${response.status})`)
  }

  const payload = (await response.json()) as ClientLocalizationContract
  return {
    culture: payload.culture ?? culture,
    site: {
      name: payload.site?.name ?? '',
      defaultLanguage: payload.site?.defaultLanguage ?? '',
      defaultTheme: payload.site?.defaultTheme ?? '',
      registrationEnabled: payload.site?.registrationEnabled ?? false,
    },
    texts: payload.texts ?? {},
  }
}

export function initializeBackendLocalization(
  onLoaded?: () => void,
): Promise<void> {
  if (initializationPromise) return initializationPromise

  initializationPromise = Promise.all([
    loadLocale('zh-CN'),
    loadLocale('en-US'),
  ])
    .then(([zh, en]) => {
      backendMessages['zh-CN'] = zh.texts
      backendMessages['en-US'] = en.texts
      applySiteConfiguration(zh.site)
      onLoaded?.()
    })
    .catch((error) => {
      initializationPromise = undefined
      console.warn('[i18n] Backend-owned localization resources are unavailable.', error)
    })

  return initializationPromise
}

/**
 * 重新读取站点配置（名称、默认语言/主题、自助注册开关）。
 * 登录页进入时调用，使后台修改的安全策略无需整页刷新即可生效。
 */
export function refreshSiteConfiguration(): Promise<void> {
  if (siteConfigurationRefreshPromise) return siteConfigurationRefreshPromise

  siteConfigurationRefreshPromise = loadLocale('zh-CN')
    .then((payload) => {
      applySiteConfiguration(payload.site)
    })
    .catch((error) => {
      console.warn('[i18n] Failed to refresh site configuration.', error)
    })
    .finally(() => {
      siteConfigurationRefreshPromise = undefined
    })

  return siteConfigurationRefreshPromise
}

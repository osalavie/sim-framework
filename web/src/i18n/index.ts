import type { App, InjectionKey, Ref } from 'vue'
import { computed, readonly, ref } from 'vue'
import {
  backendMessages,
  initializeBackendLocalization as loadBackendLocalization,
  refreshSiteConfiguration as refreshBackendSiteConfiguration,
} from './backend'
import { frontendMessages, frontendMessagesVersion, loadFrontendLocale } from './messages'
import { siteDefaults } from '@/config/branding'

export type AppLocale = 'zh-CN' | 'en-US'
type MessageParams = Record<string, string | number>

const STORAGE_KEY = 'SIM_LOCALE'
const localeState = ref<AppLocale>(detectLocale())
const backendResourceVersion = ref(0)

export const locale = readonly(localeState) as Readonly<Ref<AppLocale>>
export const acceptLanguage = computed(() => (localeState.value === 'zh-CN' ? 'zh-Hans' : 'en'))

function detectLocale(): AppLocale {
  const stored = localStorage.getItem(STORAGE_KEY)
  if (stored === 'zh-CN' || stored === 'en-US') return stored
  return navigator.language.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en-US'
}

export function initializeBackendLocalization(): Promise<void> {
  return loadBackendLocalization(() => {
    backendResourceVersion.value += 1
    applyConfiguredLocale()
  })
}

/** 刷新后端下发的站点配置，供登录页在运行时感知注册开关变化。 */
export function refreshSiteConfiguration(): Promise<void> {
  return refreshBackendSiteConfiguration()
}

/** 首次访问且用户未手动选择语言时，跟随站点默认语言。 */
function applyConfiguredLocale() {
  if (localStorage.getItem(STORAGE_KEY)) return

  const normalized = siteDefaults.defaultLanguage.value.trim().toLowerCase()
  const nextLocale: AppLocale | null =
    normalized === 'zh-hans' || normalized === 'zh-cn' || normalized === 'zh'
      ? 'zh-CN'
      : normalized === 'en' || normalized.startsWith('en-')
        ? 'en-US'
        : null

  if (!nextLocale || nextLocale === localeState.value) return
  localeState.value = nextLocale
  document.documentElement.lang = nextLocale
}

export function t(key: string, params: MessageParams = {}): string {
  // Make consumers reactive when a late backend resource or lazy frontend pack loads.
  void backendResourceVersion.value
  void frontendMessagesVersion()
  const template =
    frontendMessages[localeState.value][key] ??
    backendMessages[localeState.value][key] ??
    frontendMessages['zh-CN'][key] ??
    backendMessages['zh-CN'][key] ??
    key

  return template.replace(/\{(\w+)\}/g, (match, name: string) =>
    Object.prototype.hasOwnProperty.call(params, name) ? String(params[name]) : match,
  )
}

/**
 * 统一前端国际化键格式：与后端既有一致，使用「冒号分隔 + 大驼峰」。
 * 参数内部再按 '.' 逐段转大驼峰，用于拼接动态键，例如
 * Time:MinutesAgo、Approval:NodeType:Approval、Error:404、Schedule:Type:Command。
 */
export function messageKey(...parts: string[]): string {
  return parts
    .flatMap((part) => part.split('.'))
    .filter((segment) => segment.length > 0)
    .map((segment) => segment.charAt(0).toUpperCase() + segment.slice(1))
    .join(':')
}

function applyLocale(nextLocale: AppLocale) {
  localeState.value = nextLocale
  localStorage.setItem(STORAGE_KEY, nextLocale)
  document.documentElement.lang = nextLocale
  if (Object.keys(backendMessages[nextLocale]).length === 0) {
    void initializeBackendLocalization()
  }
}

/**
 * 切换语言。中文文案随入口包加载，切换是同步的；其它语言首次切换时先按需
 * 拉取语言包再落地，期间界面保持原语言，避免出现回退文案。
 */
export function setLocale(nextLocale: AppLocale): void | Promise<void> {
  if (nextLocale !== 'zh-CN' && Object.keys(frontendMessages[nextLocale]).length === 0) {
    return loadFrontendLocale(nextLocale).then(
      () => applyLocale(nextLocale),
      (error: unknown) => {
        // 语言包不可用时保持当前语言；若仍切换 locale，后端菜单会立即换语言，
        // 而前端文案回退中文，形成半中半英的界面。
        console.warn('[i18n] Failed to load the language pack; keeping the current locale.', error)
      },
    )
  }

  applyLocale(nextLocale)
}

/** 首屏挂载前确保当前语言的前端文案可用（英文包按需加载）。 */
export function ensureFrontendLocale(targetLocale: AppLocale): Promise<void> {
  return loadFrontendLocale(targetLocale)
}

export const localeOptions = computed(() => [
  { label: t('Locale:Zh-CN'), value: 'zh-CN' as const },
  { label: t('Locale:En-US'), value: 'en-US' as const },
])

export const i18nKey: InjectionKey<{
  locale: typeof locale
  t: typeof t
  setLocale: typeof setLocale
}> = Symbol('sim-i18n')

export const i18n = {
  install(app: App) {
    document.documentElement.lang = localeState.value
    const api = { locale, t, setLocale }
    app.provide(i18nKey, api)
    app.config.globalProperties.$t = t
  },
}

declare module 'vue' {
  interface ComponentCustomProperties {
    $t: typeof t
  }
}

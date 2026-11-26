import { readonly, ref } from 'vue'

/**
 * 品牌名 — 面向用户展示层标识的集中配置入口。
 *
 * 品牌名（SimApp）面向用户和维护人员，用于 UI、i18n、文档等展示层。
 * 框架名（Sim Framework）面向开发人员，用于镜像名、包名、工程名等文件级标识符。
 *
 * 修改品牌名时只需改 .env 中的 VITE_APP_BRAND；此处仅保留环境变量缺失时的兜底值。
 */
export const BRAND_NAME = import.meta.env.VITE_APP_BRAND || 'SimApp'

const siteNameState = ref(BRAND_NAME)
const defaultLanguageState = ref('')
const defaultThemeState = ref('')
const registrationEnabledState = ref(false)

/** Runtime site name supplied by the backend site configuration. */
export const siteName = readonly(siteNameState)

/** Runtime defaults supplied by the backend site configuration (empty until loaded). */
export const siteDefaults = {
  defaultLanguage: readonly(defaultLanguageState),
  defaultTheme: readonly(defaultThemeState),
  registrationEnabled: readonly(registrationEnabledState),
}

export function applySiteConfiguration(configuration: {
  name?: string
  defaultLanguage?: string
  defaultTheme?: string
  registrationEnabled?: boolean
}): void {
  const name = configuration.name?.trim()
  if (name) siteNameState.value = name

  const defaultLanguage = configuration.defaultLanguage?.trim()
  if (defaultLanguage) defaultLanguageState.value = defaultLanguage

  const defaultTheme = configuration.defaultTheme?.trim()
  if (defaultTheme) defaultThemeState.value = defaultTheme

  if (typeof configuration.registrationEnabled === 'boolean') {
    registrationEnabledState.value = configuration.registrationEnabled
  }
}

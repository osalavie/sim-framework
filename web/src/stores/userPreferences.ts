import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { ThemeMode, ThemeStyleId } from '@/config/themes'
import { locale, setLocale, type AppLocale } from '@/i18n'
import { userApi } from '@/modules/authorization/api'
import { useThemeStore } from './theme'
import { useUserStore } from './user'

/** 需要多端同步的用户偏好快照。 */
export interface UserPreferenceSnapshot {
  language: AppLocale
  themeMode: ThemeMode
  themeStyle: ThemeStyleId
}

const SUPPORTED_LANGUAGES: readonly AppLocale[] = ['zh-CN', 'en-US']
const SAVE_DELAY_MS = 400

const isSupportedLanguage = (value: unknown): value is AppLocale =>
  typeof value === 'string' && SUPPORTED_LANGUAGES.includes(value as AppLocale)

const isSupportedThemeMode = (value: unknown): value is ThemeMode =>
  value === 'light' || value === 'dark' || value === 'system'

/**
 * 登录用户偏好的加载与自动同步。
 *
 * - 每次页面加载（刷新）在路由守卫中调用一次 load，应用服务端偏好；
 * - 未保存过偏好时保持本机选择，不替用户写入默认值；
 * - 语言 / 明暗模式 / 主题色变化后防抖 PUT，失败只记录日志，不打断使用。
 */
export const useUserPreferencesStore = defineStore('user-preferences', () => {
  const themeStore = useThemeStore()
  const loadedUserId = ref<string | null>(null)
  const applyingRemote = ref(false)

  let loadPromise: Promise<boolean> | undefined
  let loadingUserId: string | undefined
  let saveTimer: ReturnType<typeof setTimeout> | undefined
  let saveChain: Promise<unknown> = Promise.resolve()

  const snapshot = (): UserPreferenceSnapshot => ({
    language: locale.value,
    themeMode: themeStore.mode,
    themeStyle: themeStore.styleDefinition.id,
  })

  const applyRemotePreference = async (preference: {
    language?: string | null
    themeMode?: string | null
    themeStyle?: string | null
  }) => {
    applyingRemote.value = true
    try {
      if (isSupportedThemeMode(preference.themeMode)) {
        themeStore.setThemeMode(preference.themeMode)
      }
      if (preference.themeStyle) {
        themeStore.setThemeStyle(preference.themeStyle)
      }
      if (isSupportedLanguage(preference.language)) {
        await setLocale(preference.language)
      }
    } finally {
      applyingRemote.value = false
    }
  }

  /** 加载当前登录用户的服务端偏好；同一用户只请求一次。 */
  const load = (): Promise<boolean> => {
    const userStore = useUserStore()
    const userId = userStore.user?.id
    if (!userId || !userStore.isLoggedIn()) {
      loadedUserId.value = null
      return Promise.resolve(false)
    }
    if (loadedUserId.value === userId) return Promise.resolve(true)
    if (loadPromise && loadingUserId === userId) return loadPromise

    const request = userApi
      .getMyPreferences()
      .then(async (preference) => {
        // 请求期间切换了账号：丢弃过期响应。
        if (useUserStore().user?.id !== userId) return false
        if (preference) {
          await applyRemotePreference(preference)
        }
        loadedUserId.value = userId
        return true
      })
      .catch((error) => {
        console.warn('[preferences] Failed to load user preferences.', error)
        return false
      })

    loadPromise = request
    loadingUserId = userId
    void request.finally(() => {
      if (loadPromise === request) {
        loadPromise = undefined
        loadingUserId = undefined
      }
    })

    return request
  }

  /** 登录用户在前端切换偏好后安排一次防抖保存。 */
  const scheduleSave = () => {
    const userStore = useUserStore()
    const userId = userStore.user?.id
    if (!userId || loadedUserId.value !== userId || applyingRemote.value) return

    if (saveTimer) clearTimeout(saveTimer)
    saveTimer = setTimeout(() => {
      saveTimer = undefined
      const currentUserId = useUserStore().user?.id
      if (!currentUserId || loadedUserId.value !== currentUserId) return

      const payload = snapshot()
      saveChain = saveChain
        .then(() => userApi.updateMyPreferences(payload))
        .catch((error) => {
          console.warn('[preferences] Failed to save user preferences.', error)
        })
    }, SAVE_DELAY_MS)
  }

  /** 退出登录或切换账号时丢弃本用户的内存状态。 */
  const reset = () => {
    loadedUserId.value = null
    if (saveTimer) {
      clearTimeout(saveTimer)
      saveTimer = undefined
    }
  }

  return {
    loadedUserId,
    load,
    scheduleSave,
    reset,
  }
})

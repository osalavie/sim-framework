import { defineStore } from 'pinia'
import { ref, computed, type ComputedRef } from 'vue'
import { lightTheme, darkTheme, type GlobalTheme, type GlobalThemeOverrides } from 'naive-ui'
import { STORAGE_KEY } from './persistentKeys'
import {
  lightThemeOverrides,
  darkThemeOverrides,
  lightThemeColors,
  darkThemeColors,
} from '@/config/theme'
import { createBusinessThemeOverrides } from '@/config/businessTheme'
import {
  DEFAULT_THEME_STYLE_ID,
  getThemePalette,
  getThemeStyle,
  paletteToCssVars,
  paletteToThemeColors,
  type ThemeColors,
  type ThemeMode,
  type ThemePalette,
  type ThemeSkin,
  type ThemeStyleDefinition,
  type ThemeStyleId,
} from '@/config/themes'

// Keep a simple string in the store for persistence. The plugin will persist only these keys.
const PERSIST_CONFIG = {
  key: STORAGE_KEY,
  storage: localStorage,
  pick: ['currentThemeName', 'themeMode', 'themeStyle'],
}

const prefersDarkQuery = () =>
  typeof window !== 'undefined' && typeof window.matchMedia === 'function'
    ? window.matchMedia('(prefers-color-scheme: dark)')
    : undefined

export const useThemeStore = defineStore(
  STORAGE_KEY,
  () => {
    // persisted: currentThemeName（旧版本明暗状态）、themeMode（light / dark / system）与
    // themeStyle（风格）。旧数据没有 themeMode，回退按 currentThemeName 推断，避免升级后
    // 把用户已经选择的玄夜重置成月白。
    const currentThemeName = ref<string>(lightTheme.name)
    const themeMode = ref<ThemeMode | undefined>(undefined)
    const themeStyle = ref<ThemeStyleId>(DEFAULT_THEME_STYLE_ID)

    const mediaQuery = prefersDarkQuery()
    const systemPrefersDark = ref(mediaQuery?.matches ?? false)

    const mode: ComputedRef<ThemeMode> = computed(() => {
      if (themeMode.value) return themeMode.value
      return currentThemeName.value === darkTheme.name ? 'dark' : 'light'
    })

    const isDark = computed(
      () => mode.value === 'dark' || (mode.value === 'system' && systemPrefersDark.value),
    )

    const applyResolvedThemeName = () => {
      currentThemeName.value = isDark.value ? darkTheme.name : lightTheme.name
    }

    const setThemeMode = (nextMode: ThemeMode) => {
      themeMode.value = nextMode
      // currentThemeName 继续保存解析后的明暗，兼容旧代码与旧数据迁移。
      currentThemeName.value =
        nextMode === 'dark' || (nextMode === 'system' && systemPrefersDark.value)
          ? darkTheme.name
          : lightTheme.name
    }

    mediaQuery?.addEventListener?.('change', (event) => {
      systemPrefersDark.value = event.matches
      if (mode.value === 'system') applyResolvedThemeName()
    })

    const currentTheme: ComputedRef<GlobalTheme | null> = computed(() =>
      isDark.value ? darkTheme : null,
    )

    const styleDefinition: ComputedRef<ThemeStyleDefinition> = computed(() =>
      getThemeStyle(themeStyle.value),
    )

    const skin: ComputedRef<ThemeSkin> = computed(() => styleDefinition.value.skin)

    const palette: ComputedRef<ThemePalette> = computed(() =>
      getThemePalette(styleDefinition.value.id, isDark.value),
    )

    const themeColors: ComputedRef<ThemeColors> = computed(() =>
      skin.value === 'paper'
        ? isDark.value
          ? darkThemeColors
          : lightThemeColors
        : paletteToThemeColors(palette.value),
    )

    const cssTokens: ComputedRef<Record<string, string>> = computed(() =>
      paletteToCssVars(palette.value),
    )

    const currentThemeOverrides: ComputedRef<GlobalThemeOverrides> = computed(() =>
      skin.value === 'paper'
        ? isDark.value
          ? darkThemeOverrides
          : lightThemeOverrides
        : createBusinessThemeOverrides(palette.value, isDark.value),
    )

    /** 直接指定明暗（顶栏切换按钮）：从「跟随系统」切换时会变成明确选择。 */
    const setTheme = (dark: boolean) => {
      setThemeMode(dark ? 'dark' : 'light')
    }

    const toggleTheme = () => setTheme(!isDark.value)

    /** 切换风格；未知 id 回落到纸上海洋，避免持久化脏数据导致空白主题。 */
    const setThemeStyle = (id: string) => {
      themeStyle.value = getThemeStyle(id).id
    }

    /** 用户尚未手动选择主题时，应用站点初始化时设置的默认主题。 */
    const applyConfiguredTheme = (configured?: string | null) => {
      if (!configured || localStorage.getItem(STORAGE_KEY) !== null) return

      const theme = configured.trim().toLowerCase()
      if (theme === 'auto' || theme === 'system') {
        setThemeMode('system')
        return
      }

      setTheme(theme === 'dark')
    }

    return {
      currentThemeName,
      themeMode,
      mode,
      themeStyle,
      currentTheme,
      currentThemeOverrides,
      styleDefinition,
      skin,
      palette,
      themeColors,
      cssTokens,
      isDark,
      setTheme,
      setThemeMode,
      toggleTheme,
      setThemeStyle,
      applyConfiguredTheme,
    }
  },
  { persist: PERSIST_CONFIG },
)

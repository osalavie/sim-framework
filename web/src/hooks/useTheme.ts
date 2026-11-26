import { computed } from 'vue'
import { useThemeStore } from '@/stores/theme'
import type { ThemeColors, ThemeMode, ThemeStyleId } from '@/config/themes'

/**
 * 主题 Hook - 提供当前风格、语义色与切换工具
 * 用于在组件中访问当前主题的颜色变量与风格状态。
 */
export function useTheme() {
  const themeStore = useThemeStore()

  // 当前主题语义色
  const themeColors = computed<ThemeColors>(() => themeStore.themeColors)

  // 是否为深色主题（system 模式按系统偏好解析）
  const isDark = computed(() => themeStore.isDark)

  // 当前明暗模式：light / dark / system
  const mode = computed<ThemeMode>(() => themeStore.mode)

  // 当前风格
  const themeStyle = computed(() => themeStore.styleDefinition.id)

  // 切换明暗
  const toggleTheme = () => themeStore.toggleTheme()

  // 设置明暗
  const setTheme = (dark: boolean) => themeStore.setTheme(dark)

  // 设置明暗模式（含跟随系统）
  const setThemeMode = (nextMode: ThemeMode) => themeStore.setThemeMode(nextMode)

  // 设置风格
  const setThemeStyle = (id: ThemeStyleId) => themeStore.setThemeStyle(id)

  return {
    themeColors,
    isDark,
    mode,
    themeStyle,
    toggleTheme,
    setTheme,
    setThemeMode,
    setThemeStyle,
  }
}

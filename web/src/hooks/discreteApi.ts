import { createDiscreteApi, type ConfigProviderProps } from 'naive-ui'
import { useThemeStore } from '@/stores/theme'
import { computed } from 'vue'

let _discreteApi: ReturnType<typeof createDiscreteApi> | null = null

function initDiscreteApi() {
  if (_discreteApi) return _discreteApi
  // call useThemeStore lazily to ensure Pinia is active
  const themeStore = useThemeStore()
  const configProviderProps = computed<ConfigProviderProps>(() => ({
    theme: themeStore.currentTheme,
    themeOverrides: themeStore.currentThemeOverrides,
  }))
  _discreteApi = createDiscreteApi(['message', 'notification', 'dialog', 'loadingBar', 'modal'], {
    configProviderProps,
  })
  return _discreteApi
}

// export a proxy so existing imports don't need to change; the real API will be
// initialized on first property access or method call.
export const useDiscreteApi = new Proxy(
  {},
  {
    get(_, prop: string) {
      const api = initDiscreteApi() as Record<string, unknown>
      const value = api[prop]
      // if the value is a function, bind it to the api instance
      if (typeof value === 'function') return (value as (...args: unknown[]) => unknown).bind(api)
      return value
    },
    set(_, prop: string, value) {
      const api = initDiscreteApi() as Record<string, unknown>
      api[prop] = value
      return true
    },
  },
) as ReturnType<typeof createDiscreteApi>

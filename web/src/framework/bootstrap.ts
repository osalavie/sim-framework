import { createApp as createVueApp } from 'vue'
import type { App as VueApp } from 'vue'
import { createPinia } from 'pinia'
import piniaPluginPersistedState from 'pinia-plugin-persistedstate'
import type { Router } from 'vue-router'
import App from '@/App.vue'
import { createRouter } from '@/router'
import { ensureFrontendLocale, i18n, initializeBackendLocalization, locale, setLocale } from '@/i18n'
import { initWebVitals } from '@/utils/web-vitals'
import { setupApiClients } from '@/api/setupClients'
import { permissionDirective } from '@/directives/permission'
import { tooltipDirective } from '@/directives/tooltip'
import type { MenuConfig, ModuleConfig } from '@/types/module'
import 'animate.css'
import '@/styles/style.css'
import '@/styles/theme-variables.css'
import '@/styles/maple-mono.css'
import '@/styles/ink-wash.css'
import '@/styles/paper-ocean.css'
import '@/styles/business-skin.css'

export interface AppOptions {
  /** 应用挂载的模块清单（平台模块 + 业务模块）。 */
  modules: ModuleConfig[]
  /** 默认打开的标签页等菜单配置。 */
  menuConfig: MenuConfig
  /** 浏览器标题使用的应用名，默认取 VITE_APP_TITLE。 */
  appTitle?: string
  /** 挂载节点选择器，默认 #app。 */
  mountTarget?: string
}

export interface AppInstance {
  app: VueApp
  router: Router
}

export async function createApp(options: AppOptions): Promise<AppInstance> {
  setupApiClients()

  const pinia = createPinia()
  pinia.use(piniaPluginPersistedState)

  // 英文语言包按需加载：挂载前只补齐当前语言，避免两套文案进入入口包。
  await ensureFrontendLocale(locale.value).catch((error: unknown) => {
    console.warn('[i18n] Falling back to zh-CN because the language pack failed to load.', error)
    setLocale('zh-CN')
  })

  const router = createRouter({
    modules: options.modules,
    menuConfig: options.menuConfig,
    appTitle: options.appTitle,
  })

  const app = createVueApp(App)
  void initializeBackendLocalization()
  app.use(pinia)
  app.use(i18n)
  app.use(router)
  app.directive('permission', permissionDirective)
  app.directive('tooltip', tooltipDirective)

  initWebVitals({
    enableLogging: import.meta.env.DEV,
    onReport: (vitals) => {
      if (import.meta.env.PROD) {
        console.debug('[WebVitals] Final report:', vitals)
      }
    },
  })

  app.mount(options.mountTarget ?? '#app')
  return { app, router }
}

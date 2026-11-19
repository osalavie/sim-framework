/// <reference types="vite/client" />

// Vite 环境变量类型定义
declare interface ImportMetaEnv {
  readonly BASE_URL: string
  readonly VITE_APP_TITLE: string
  readonly VITE_API_URL: string
  readonly VITE_API_URL_PREFIX: string
  readonly VITE_ICP_LICENSE: string
  readonly VITE_CUSTOM_REDIRECT: string
  readonly VITE_CUSTOM_ROOT_PATH: string
  readonly VITE_ADMIN_DEFAULT_REDIRECT: string
  readonly VITE_ADMIN_ROOT_PATH: string
  readonly ENABLE_LOAD_LOGGING: boolean
}

declare interface ImportMeta {
  readonly env: ImportMetaEnv
}

// Vue 组件类型声明
declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<object, object, unknown>
  export default component
}

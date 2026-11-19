import { ref } from 'vue'
import type { AppLocale } from '@/i18n'
import zhCore from './zh-CN.core'
import zhSystem from './zh-CN.system'
import zhAccount from './zh-CN.account'

export type MessageDictionary = Record<string, string>

const registry: Record<AppLocale, MessageDictionary> = { 'zh-CN': {}, 'en-US': {} }

/**
 * 懒加载语言包注册后需要让 t() 的消费者重新渲染；仅靠后端资源版本
 * 无法覆盖按需加载的前端语言包。
 */
const registryVersion = ref(0)

export function frontendMessagesVersion(): number {
  return registryVersion.value
}

/**
 * 注册语言包到当前会话的消息表；重复键直接抛错，避免合并期静默覆盖。
 *
 * 中文是站点默认语言，随入口包一起加载；英文整包与功能模块自己的语言包
 * 按需注册（英文见 `loadFrontendLocale`，模块包见 `src/i18n/lazyPacks.ts`）。
 */
export function registerMessages(locale: AppLocale, ...groups: MessageDictionary[]): void {
  let changed = false
  for (const group of groups) {
    for (const [key, value] of Object.entries(group)) {
      if (key in registry[locale]) {
        throw new Error(`Duplicate frontend localization key: ${key}`)
      }
      registry[locale][key] = value
      changed = true
    }
  }

  if (changed) {
    registryVersion.value += 1
  }
}

registerMessages('zh-CN', zhCore, zhSystem, zhAccount)

let englishMessages: Promise<void> | undefined

/**
 * 按需注册指定语言的前端文案。
 *
 * 英文整包约 50KB，随入口打包会拖慢所有中文用户的首屏，因此只在当前语言
 * 或用户切换语言时加载；加载完成前 `t()` 会回退到中文文案。
 */
export function loadFrontendLocale(locale: AppLocale): Promise<void> {
  if (locale === 'zh-CN') return Promise.resolve()

  englishMessages ??= Promise.all([
    import('./en-US.core'),
    import('./en-US.system'),
    import('./en-US.account'),
  ])
    .then(([core, system, account]) => {
      registerMessages('en-US', core.default, system.default, account.default)
    })
    .catch((error: unknown) => {
      // 失败结果不能缓存，否则本次会话后续切换语言永远不会重试。
      englishMessages = undefined
      throw error
    })

  return englishMessages
}

export const frontendMessages = registry

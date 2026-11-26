import { ensureMessagePacks } from '@/i18n/lazyPacks'

let loading: Promise<void> | null = null

/** E2EE 文案独立成按需语言包，避免解锁/加密 UI 文案进入首屏。 */
export function ensureE2eeMessages(): Promise<void> {
  loading ??= ensureMessagePacks(['e2ee'])
  return loading
}

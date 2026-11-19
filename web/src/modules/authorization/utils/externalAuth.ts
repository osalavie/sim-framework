import type { LoginResponse } from '@/modules/authorization/api/user/userModels'

const RESULT_KEY = 'sim.external-auth.result'
const ORIGIN_KEY = 'sim.external-auth.origin'
const NOTICE_KEY = 'sim.external-auth.notice'

const MAX_AGE_MS = 10 * 60 * 1000
/** 提示只服务紧接着的一次跳转，避免陈旧错误在后续访问时再次弹出。 */
const NOTICE_MAX_AGE_MS = 60 * 1000

/** 第三方认证的发起页；回调失败时回到该页面展示错误。 */
export type ExternalAuthOrigin = 'login' | 'security'

interface PendingExternalAuthResult {
  result: LoginResponse
  savedAt: number
}

interface PendingExternalAuthOrigin {
  origin: ExternalAuthOrigin
  savedAt: number
}

interface PendingExternalAuthNotice {
  message: string
  savedAt: number
}

function readPending<T extends { savedAt: number }>(key: string, maxAgeMs: number): T | undefined {
  const raw = sessionStorage.getItem(key)
  sessionStorage.removeItem(key)
  if (!raw) return undefined

  try {
    const payload = JSON.parse(raw) as T
    if (Date.now() - payload.savedAt > maxAgeMs) return undefined
    return payload
  } catch {
    return undefined
  }
}

function writePending(key: string, payload: { savedAt: number }) {
  sessionStorage.setItem(key, JSON.stringify(payload))
}

export function savePendingExternalAuthResult(result: LoginResponse) {
  const payload: PendingExternalAuthResult = {
    result,
    savedAt: Date.now(),
  }
  writePending(RESULT_KEY, payload)
}

export function consumePendingExternalAuthResult(): LoginResponse | undefined {
  return readPending<PendingExternalAuthResult>(RESULT_KEY, MAX_AGE_MS)?.result
}

/**
 * 发起第三方登录或绑定时记录来源页。回调失败后由回调落点据此回到原页面，
 * 而不是停留在中间页让用户手工选择返回目标。
 */
export function markExternalAuthOrigin(origin: ExternalAuthOrigin) {
  const payload: PendingExternalAuthOrigin = {
    origin,
    savedAt: Date.now(),
  }
  writePending(ORIGIN_KEY, payload)
}

export function consumeExternalAuthOrigin(): ExternalAuthOrigin | undefined {
  const origin = readPending<PendingExternalAuthOrigin>(ORIGIN_KEY, MAX_AGE_MS)?.origin
  return origin === 'login' || origin === 'security' ? origin : undefined
}

/** 回调失败提示交给发起页展示；不放进 URL，避免进入浏览器历史和服务端访问日志。 */
export function savePendingExternalAuthNotice(message: string) {
  const payload: PendingExternalAuthNotice = {
    message,
    savedAt: Date.now(),
  }
  writePending(NOTICE_KEY, payload)
}

export function consumePendingExternalAuthNotice(): string | undefined {
  return readPending<PendingExternalAuthNotice>(NOTICE_KEY, NOTICE_MAX_AGE_MS)?.message || undefined
}

import axios, {
  AxiosError,
  type AxiosInstance,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from 'axios'
import type { RequestConfig } from '@/types/api'
import { apiConfig } from '@/config/apiConfig'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { clearAccessToken, getAccessToken } from '@/utils/authSession'
import { acceptLanguage, messageKey, t } from '@/i18n'
import { getVisitorFingerprint } from '@/utils/visitorFingerprint'
import { useSiteStatusStore } from '@/stores/siteStatus'
import { getRouter } from '@/router/registry'

type AuthRequestConfig = InternalAxiosRequestConfig &
  RequestConfig & {
    _authRetry?: boolean
  }

const displayedRequestErrors = new WeakSet<object>()

function isObject(value: unknown): value is object {
  return typeof value === 'object' && value !== null
}

export function isRequestErrorDisplayed(error: unknown): boolean {
  return isObject(error) && displayedRequestErrors.has(error)
}

function markRequestErrorDisplayed(error: unknown): void {
  if (isObject(error)) displayedRequestErrors.add(error)
}

// 请求去重/取消管理（对 SDK 调用与手写调用统一生效）
const pendingRequests = new Map<string, AbortController>()

interface PendingRequestConfig extends InternalAxiosRequestConfig {
  _pendingKey?: string
}

function generateRequestKey(config: InternalAxiosRequestConfig): string {
  const { method, url, params, data } = config
  return [method, url, JSON.stringify(params), JSON.stringify(data)].join('&')
}

function registerPendingRequest(config: PendingRequestConfig) {
  if ((config as AuthRequestConfig).cancelable === false) return
  const key = generateRequestKey(config)
  if (pendingRequests.has(key)) {
    // 存在相同请求时取消前一个
    pendingRequests.get(key)?.abort()
  }
  const controller = new AbortController()
  config.signal = controller.signal
  config._pendingKey = key
  pendingRequests.set(key, controller)
}

function removePendingRequest(config?: PendingRequestConfig) {
  if (!config?._pendingKey) return
  const controller = pendingRequests.get(config._pendingKey)
  if (controller?.signal === config.signal) {
    pendingRequests.delete(config._pendingKey)
  }
  delete config._pendingKey
}

export function cancelAllRequests() {
  pendingRequests.forEach((controller) => controller.abort())
  pendingRequests.clear()
}

const instance: AxiosInstance = axios.create({
  baseURL: apiConfig.baseUrl,
  timeout: apiConfig.timeout,
  withCredentials: true,
})

async function redirectToLogin() {
  const router = getRouter()
  const current = router.currentRoute.value
  // 初始导航尚未完成时，401 由路由守卫统一处理，避免重复跳转。
  if (!current.name) return

  // 已停留在任一登录入口时不再跳转。
  if (current.name === 'Login' || current.name === 'Register') {
    return
  }

  await router.push({ name: 'Login', query: { redirect: current.fullPath } }).catch(() => {})
}

async function clearSessionAndRedirect() {
  clearAccessToken()
  const { useUserStore } = await import('@/stores/user')
  useUserStore().clearSession()
  await redirectToLogin()
}

instance.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    const token = getAccessToken()
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`
    }
    if (config.headers) config.headers['Accept-Language'] = acceptLanguage.value
    if (config.headers && config.url?.startsWith('/api/system/content-file/')) {
      config.headers['X-Visitor-Fingerprint'] = await getVisitorFingerprint()
    }

    registerPendingRequest(config as PendingRequestConfig)

    return config
  },
  (error: AxiosError) => Promise.reject(error),
)

instance.interceptors.response.use(
  (response: AxiosResponse) => {
    removePendingRequest(response.config as PendingRequestConfig)
    return response
  },
  async (error: AxiosError) => {
    removePendingRequest(error.config as PendingRequestConfig)
    const customConfig = error.config as AuthRequestConfig | undefined

    if (error.response?.status === 401 && customConfig) {
      const isSessionEndpoint =
        customConfig.url?.includes('/login') ||
        customConfig.url?.includes('/refresh-token') ||
        customConfig.url?.includes('/revoke-refresh-token') ||
        customConfig.url?.includes('/antiforgery-token')

      if (!isSessionEndpoint && !customConfig._authRetry) {
        customConfig._authRetry = true
        try {
          const { useUserStore } = await import('@/stores/user')
          const newAccessToken = await useUserStore().refreshAccessToken()

          if (customConfig.headers) {
            customConfig.headers.Authorization = `Bearer ${newAccessToken}`
          }

          return instance(customConfig)
        } catch (refreshError) {
          await clearSessionAndRedirect()
          return Promise.reject(refreshError)
        }
      }

      await clearSessionAndRedirect()
      return Promise.reject(error)
    }

    if (error.response?.status === 503 && customConfig?.url !== '/api/admin/site-status') {
      await redirectToMaintenance(error)
      return Promise.reject(error)
    }

    if (customConfig?.showError !== false) {
      handleHttpError(error)
      markRequestErrorDisplayed(error)
    }

    return Promise.reject(error)
  },
)

async function redirectToMaintenance(error: AxiosError) {
  const data = error.response?.data as
    | { maintenanceMode?: boolean; maintenanceMessage?: string | null }
    | undefined

  if (data?.maintenanceMode === true) {
    useSiteStatusStore().applyPayload({
      maintenanceMode: true,
      maintenanceMessage: data.maintenanceMessage,
    })
  }

  const router = getRouter()
  if (router.currentRoute.value.name === 'Maintenance') return

  await router.push({ name: 'Maintenance' }).catch(() => {})
}

function handleHttpError(error: AxiosError) {
  if (axios.isCancel(error) || error.code === 'ERR_CANCELED') {
    return
  }

  if (error.response) {
    const status = error.response.status
    if (status !== 401) {
      const data = error.response.data as
        | { error?: { code?: string | null; message?: string | null }; message?: string }
        | undefined
      const code = data?.error?.code
      const localizedCode = code && t(code) !== code ? t(code) : undefined
      const serverMessage = data?.error?.message || data?.message
      const statusMessage = t(messageKey('error', String(status)))
      useDiscreteApi.message.error(
        localizedCode ||
          serverMessage ||
          (statusMessage !== messageKey('error', String(status))
            ? statusMessage
            : t('Error:RequestFailedStatus', { status })),
      )
    }
  } else if (error.request) {
    useDiscreteApi.message.error(t('Error:Network'))
  } else {
    useDiscreteApi.message.error(error.message || t('Error:RequestFailed'))
  }
}

export default instance

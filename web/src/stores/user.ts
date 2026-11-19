import { defineStore } from 'pinia'
import { t } from '@/i18n'
import { ref } from 'vue'
import { USER_KEY } from './persistentKeys'
import type {
  UserProfile,
  RoleInfo,
  PermissionInfo,
  LoginResponse,
  ExternalLoginCallback,
  ExternalLoginRegistrationRequest,
  LinkWithExternalLoginRequest,
} from '@/modules/authorization/api'
import { userApi } from '@/modules/authorization/api'
import { clearAccessToken, getAccessToken, setAccessToken } from '@/utils/authSession'

const PERSIST_CONFIG = {
  key: USER_KEY,
  storage: localStorage,
}

let restoreAttempted = false
let restorePromise: Promise<boolean> | undefined
let sessionRefreshPromise: Promise<string> | undefined

const getRetryAfterSeconds = (error: unknown): number => {
  const axiosError = error as {
    response?: { status?: number; headers?: Record<string, string> }
  }
  if (axiosError.response?.status !== 429) return 0
  const retryAfter = axiosError.response.headers?.['retry-after']
  if (!retryAfter) return 0
  const seconds = Number(retryAfter)
  return Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : 0
}

export const useUserStore = defineStore(
  USER_KEY,
  () => {
    const user = ref<UserProfile>()
    const roles = ref<RoleInfo[]>([])
    const permissions = ref<PermissionInfo[]>([])

    // 资料只在登录或页面刷新（首次恢复会话）时读取一次，后续导航直接复用 store。
    let profileLoadedUserId: string | undefined
    let profileLoadPromise: Promise<void> | undefined

    const applySession = (response: LoginResponse) => {
      if (!response.accessToken || !response.id || !response.userName) {
        throw new Error(t('Error:InvalidLoginSession'))
      }

      setAccessToken(response.accessToken)
      if (profileLoadedUserId !== response.id) {
        profileLoadedUserId = undefined
      }
      user.value = {
        id: response.id,
        userName: response.userName,
        avatar: response.avatar ?? undefined,
      }
      roles.value = response.roles || []
      permissions.value = response.permissions || []
    }

    // 会话失效（401、刷新失败）只擦内存 UMK；只有显式退出登录才删除本机设备缓存，
    // 避免网络抖动导致“信任此设备”被误清除。
    const clearSession = (options: { clearDeviceCache?: boolean } = {}) => {
      const e2eeUserId = user.value?.id
      clearAccessToken()
      profileLoadedUserId = undefined
      profileLoadPromise = undefined
      user.value = undefined
      roles.value = []
      permissions.value = []
      return import('@/e2ee/keyVault')
        .then((module) => {
          module.lockE2eeVault()
          if (!options.clearDeviceCache) return undefined
          // 显式退出：清空本机“信任此设备”的口令密钥缓存；取不到账号 ID 时整库清空，避免残留
          return module.clearE2eeDeviceCache(e2eeUserId || undefined)
        })
        .catch(() => undefined)
    }

    const updateProfile = (profile: Partial<UserProfile>) => {
      if (!user.value) return
      user.value = { ...user.value, ...profile }
    }

    // 后台头部头像依赖完整资料；登录响应只包含 id/userName/roles/permissions，
    // 头像等字段需另外拉取。失败时不影响登录态——降级用登录响应里的 avatar（若有）。
    // 同一账号在本次页面生命周期内只请求一次，避免每次菜单跳转都等待资料接口。
    const loadMyProfile = (force = false): Promise<void> => {
      const userId = user.value?.id
      if (!userId || !getAccessToken()) return Promise.resolve()
      if (!force && profileLoadedUserId === userId) return Promise.resolve()
      if (profileLoadPromise) return profileLoadPromise

      const request = userApi
        .getMyProfile()
        .then((profile) => {
          // 请求期间切换了账号：丢弃过期响应。
          if (user.value?.id !== userId) return
          updateProfile(profile)
        })
        .catch(() => {
          // 静默失败：Header 会回退到用户名首字母
        })
        .finally(() => {
          // 切换账号后不要用旧请求的结果标记新账号已加载。
          if (user.value?.id === userId) {
            profileLoadedUserId = userId
          }
          if (profileLoadPromise === request) {
            profileLoadPromise = undefined
          }
        })

      profileLoadPromise = request
      return request
    }

    const login = async (userName: string, password: string, rememberMe = false) => {
      const response = await userApi.login(userName, password, rememberMe)
      restoreAttempted = true
      if (response.requiresTwoFactor) {
        return response
      }
      applySession(response)
      void loadMyProfile()
      return response
    }


    const completeExternalLogin = async (provider: string, query: ExternalLoginCallback) => {
      const response = await userApi.completeExternalLogin(provider, query)
      restoreAttempted = true
      if (
        response.requiresExternalLoginRegistration ||
        response.requiresTwoFactor ||
        response.externalLoginBindingCompleted
      ) {
        return response
      }
      applySession(response)
      void loadMyProfile()
      return response
    }

    const registerWithExternalLogin = async (data: ExternalLoginRegistrationRequest) => {
      const response = await userApi.registerWithExternalLogin(data)
      restoreAttempted = true
      if (response.requiresTwoFactor) {
        return response
      }
      applySession(response)
      void loadMyProfile()
      return response
    }
    const linkWithExternalLogin = async (data: LinkWithExternalLoginRequest) => {
      const response = await userApi.linkWithExternalLogin(data)
      restoreAttempted = true
      if (response.requiresTwoFactor) {
        return response
      }
      applySession(response)
      void loadMyProfile()
      return response
    }
    const completeTwoFactorLogin = async (
      challengeId: string,
      method: 'authenticator' | 'email' | 'sms' | 'recovery',
      code: string,
      rememberMe = false,
    ) => {
      const response = await userApi.completeTwoFactorLogin(challengeId, method, code, rememberMe)
      restoreAttempted = true
      applySession(response)
      void loadMyProfile()
      return response
    }

    const logout = async () => {
      try {
        await userApi.revokeRefreshToken()
      } catch {
        // 本地会话必须始终清理；服务端 Cookie 缺失或已失效不阻塞退出。
      } finally {
        restoreAttempted = true
        // 等待本机口令缓存真正删除后再结束退出流程，避免立刻重新登录时读到旧缓存
        await clearSession({ clearDeviceCache: true })
      }
    }

    const refreshOnce = () =>
      userApi.refreshToken().then((response) => {
        applySession(response)
        return response.accessToken!
      })

    const refreshAccessToken = () => {
      if (sessionRefreshPromise) return sessionRefreshPromise

      sessionRefreshPromise = refreshOnce()
        .catch(async (error) => {
          // 429 是暂时性的限流：按 Retry-After（上限 10 秒）等待后重试一次，
          // 不要因为一次限流就把会话清掉强制重新登录。
          const retryAfterSeconds = getRetryAfterSeconds(error)
          if (retryAfterSeconds > 0) {
            await new Promise((resolve) =>
              setTimeout(resolve, Math.min(retryAfterSeconds, 10) * 1000),
            )
            try {
              return await refreshOnce()
            } catch (retryError) {
              clearSession()
              throw retryError
            }
          }

          clearSession()
          throw error
        })
        .finally(() => {
          sessionRefreshPromise = undefined
        })

      return sessionRefreshPromise
    }

    const restoreSession = async () => {
      if (getAccessToken()) {
        void loadMyProfile()
        return true
      }
      if (restoreAttempted) return false
      if (restorePromise) return restorePromise

      restoreAttempted = true
      restorePromise = refreshAccessToken()
        .then(() => {
          void loadMyProfile()
          return true
        })
        .catch(() => false)
        .finally(() => {
          restorePromise = undefined
        })

      return restorePromise
    }

    const hasPermission = (permissionCode: string) =>
      permissions.value.some(
        (permission) => permission.code === '*:*:*' || permission.code === permissionCode,
      )

    const hasAnyPermission = (permissionCodes: string | readonly string[]) => {
      const required = typeof permissionCodes === 'string' ? [permissionCodes] : permissionCodes
      if (required.length === 0) return true
      return permissions.value.some(
        (permission) =>
          permission.code === '*:*:*' ||
          (!!permission.code && required.includes(permission.code)),
      )
    }

    const hasAllPermissions = (permissionCodes: string | readonly string[]) => {
      const required = typeof permissionCodes === 'string' ? [permissionCodes] : permissionCodes
      if (required.length === 0) return true
      const granted = new Set(
        permissions.value.map((permission) => permission.code).filter((code): code is string => !!code),
      )
      return granted.has('*:*:*') || required.every((code) => granted.has(code))
    }

    const hasRole = (roleCode: string) => roles.value.some((role) => role.code === roleCode)

    const isLoggedIn = () => !!getAccessToken() && !!user.value?.id

    return {
      user,
      roles,
      permissions,
      login,
      completeExternalLogin,
      registerWithExternalLogin,
      linkWithExternalLogin,
      completeTwoFactorLogin,
      logout,
      refreshAccessToken,
      clearSession,
      restoreSession,
      loadMyProfile,
      hasRole,
      hasPermission,
      hasAnyPermission,
      hasAllPermissions,
      isLoggedIn,
      updateProfile,
    }
  },
  { persist: PERSIST_CONFIG },
)

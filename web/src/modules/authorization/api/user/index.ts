import {
  getSystemUserAntiforgeryToken,
  getSystemUserMyProfile,
  putSystemUserMyProfile,
  postSystemUserUploadMyAvatar,
  getSystemUser,
  getSystemUserId,
  postSystemUser,
  putSystemUser,
  deleteSystemUserId,
  postSystemUserIdResetUserPassword,
  postSystemUserLogin,
  postSystemUserRegister,
  getSystemExternalLoginProviders,
  getSystemExternalLoginRedirectProviderId,
  getSystemExternalLoginCallbackProviderId,
  postSystemExternalLoginRegisterWithExternalLogin,
  postSystemExternalLoginLinkWithExternalLogin,
  getSystemExternalLoginMyBindings,
  getSystemExternalLoginMyBindingRedirectProviderId,
  deleteSystemExternalLoginMyBindingProviderId,
  postSystemUserSendTwoFactorCode,
  postSystemUserCompleteTwoFactorLogin,
  postSystemUserChangePassword,
  postSystemUserDeleteMyAccount,
  postSystemUserRequestPasswordReset,
  postSystemUserResetPassword,
  getSystemUserTwoFactorStatus,
  postSystemUserBeginAuthenticatorSetup,
  postSystemUserConfirmAuthenticatorSetup,
  postSystemUserRequestMessageTwoFactorSetup,
  postSystemUserConfirmMessageTwoFactorSetup,
  postSystemUserDisableTwoFactor,
  postSystemUserRefreshToken,
  postSystemUserRevokeRefreshToken,
  getSystemUserMyLoginSessions,
  postSystemUserRevokeMyLoginSessionSessionId,
  getSystemUserNickNameAvailability,
  getSystemUserPreferenceMyPreferences,
  putSystemUserPreferenceMyPreferences,
} from '@/api/system'
import type { UpdateUserPreferenceDto, UserPreferenceDto } from '@/api/system'
import type {
  User,
  UserFormData,
  UserQueryParams,
  LoginResponse,
  UpdateMyProfileData,
  TwoFactorMethod,
  PasswordResetChallenge,
  TwoFactorStatus,
  AuthenticatorSetup,
  AuthenticatorRecoveryCodes,
  LoginSession,
  ExternalLoginProvider,
  ExternalLoginRedirect,
  ExternalLoginCallback,
  ExternalLoginBinding,
  ExternalLoginRegistrationRequest,
  LinkWithExternalLoginRequest,
} from './userModels'
import type { PageData } from '@/types/api'
import { getLoginDeviceDescriptor } from '@/modules/authorization/utils/deviceSession'

async function getAntiforgeryToken() {
  const res = await getSystemUserAntiforgeryToken({
    throwOnError: true,
    ...({ showError: false } as Record<string, unknown>),
  })
  return res.data.requestToken ?? ''
}

async function withXsrf<T>(call: (token: string) => Promise<T>): Promise<T> {
  const token = await getAntiforgeryToken()
  return call(token)
}

// 导出类型定义
export * from './userModels'

/**
 * 用户 API
 * 基于原始 src/api/user/index.ts，扩展支持完整的 CRUD 操作
 */
export const userApi = {
  getMyProfile: () =>
    getSystemUserMyProfile({ throwOnError: true }).then((res) => res.data as User),

  updateMyProfile: (data: UpdateMyProfileData) =>
    // 个人资料按字段内联保存，失败提示由调用方按字段上下文展示，避免全局拦截器重复弹窗。
    putSystemUserMyProfile({
      body: data,
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
    }).then((res) => res.data as User),

  uploadMyAvatar: (file: File) =>
    postSystemUserUploadMyAvatar({ body: { file } as never, throwOnError: true }).then(
      (res) => res.data as User,
    ),

  /**
   * 获取当前用户信息
   * GET /api/system/user/my-profile
   */
  getUserInfo: () => {
    return getSystemUserMyProfile({ throwOnError: true }).then((res) => res.data as User)
  },

  /**
   * 获取用户列表（分页）
   * GET /api/system/user
   * 支持关键词搜索、排序和分页
   */
  getList: (params?: UserQueryParams) => {
    return getSystemUser({ query: params as never, throwOnError: true }).then(
      (res) => res.data as PageData<User>,
    )
  },

  /**
   * 检查昵称是否可用（编辑时传 excludeUserId 排除当前账号）
   * GET /api/system/user/nick-name-availability
   */
  checkNickNameAvailability: (nickName: string, excludeUserId?: string) =>
    getSystemUserNickNameAvailability({
      query: { nickName, excludeUserId },
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
    }).then((res) => Boolean(res.data)),

  /**
   * 根据 ID 获取用户详情
   * GET /api/system/user/{id}
   */
  getById: (id: string) => {
    return getSystemUserId({ path: { id }, throwOnError: true }).then((res) => res.data as User)
  },

  /**
   * 创建用户
   * POST /api/system/user
   */
  create: (data: UserFormData) => {
    return postSystemUser({ body: data as never, throwOnError: true }).then(
      (res) => res.data as User,
    )
  },

  /**
   * 更新用户 (ABP的Update方法需要在body中包含id)
   * PUT /api/system/user
   */
  update: (id: string, data: UserFormData) => {
    return putSystemUser({ body: { ...data, id } as never, throwOnError: true }).then(
      (res) => res.data as User,
    )
  },

  /**
   * 删除用户
   * DELETE /api/system/user/{id}
   */
  delete: (id: string) => {
    return deleteSystemUserId({ path: { id }, throwOnError: true }).then((res) => res.data)
  },

  /**
   * 将用户密码重置为系统配置的默认密码
   * POST /api/system/user/{id}/reset-user-password
   */
  resetUserPassword: (id: string) => {
    return postSystemUserIdResetUserPassword({ path: { id }, throwOnError: true }).then(
      (res) => res.data,
    )
  },

  /**
   * 用户登录
   * POST /api/system/user/login
   */
  login: async (username: string, password: string, rememberMe = false) => {
    const device = await getLoginDeviceDescriptor()
    return postSystemUserLogin({
      body: { userName: username, password, rememberMe, ...device },
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as LoginResponse)
  },

  /**
   * 自助注册
   * POST /api/system/user/register
   * 角色由后端按系统配置 security.registration-role-codes 分配，前端不提交角色。
   */
  register: (data: {
    userName: string
    nickName: string
    email?: string | null
    password: string
  }) =>
    postSystemUserRegister({
      body: data as never,
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as User),

  /**
   * 获取已启用的外部登录提供方
   * GET /api/system/external-login/providers
   */
  getExternalLoginProviders: () =>
    getSystemExternalLoginProviders({
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as ExternalLoginProvider[]),

  /**
   * 获取外部登录跳转地址
   * GET /api/system/external-login/redirect/{providerId}
   */
  getExternalLoginRedirect: (provider: string) =>
    getSystemExternalLoginRedirectProviderId({
      path: { providerId: provider },
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as ExternalLoginRedirect),

  /**
   * 完成外部登录回调
   * GET /api/system/external-login/callback/{providerId}
   */
  completeExternalLogin: (provider: string, query: ExternalLoginCallback) =>
    getSystemExternalLoginCallbackProviderId({
      path: { providerId: provider },
      query: query as never,
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as LoginResponse),

  registerWithExternalLogin: (data: ExternalLoginRegistrationRequest) =>
    postSystemExternalLoginRegisterWithExternalLogin({
      body: data,
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as LoginResponse),

  /**
   * 用已有账号密码验证后把外部身份关联到该账号
   * POST /api/system/external-login/link-with-external-login
   */
  linkWithExternalLogin: (data: LinkWithExternalLoginRequest) =>
    postSystemExternalLoginLinkWithExternalLogin({
      body: data,
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as LoginResponse),

  getMyExternalLoginBindings: () =>
    getSystemExternalLoginMyBindings({ throwOnError: true }).then(
      (res) => res.data as ExternalLoginBinding[],
    ),

  getMyBindingRedirect: (provider: string) =>
    getSystemExternalLoginMyBindingRedirectProviderId({
      path: { providerId: provider },
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as ExternalLoginRedirect),

  deleteMyBinding: (provider: string) =>
    deleteSystemExternalLoginMyBindingProviderId({
      path: { providerId: provider },
      throwOnError: true,
    }).then((res) => res.data),

  sendTwoFactorCode: (challengeId: string, method: TwoFactorMethod) =>
    postSystemUserSendTwoFactorCode({
      body: { challengeId, method },
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data),

  completeTwoFactorLogin: async (
    challengeId: string,
    method: TwoFactorMethod,
    code: string,
    rememberMe = false,
  ) => {
    const device = await getLoginDeviceDescriptor()
    return postSystemUserCompleteTwoFactorLogin({
      body: { challengeId, method, code, rememberMe, ...device },
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as LoginResponse)
  },

  changePassword: (
    currentPassword: string,
    newPassword: string,
    confirmPassword: string,
  ) =>
    postSystemUserChangePassword({
      body: { currentPassword, newPassword, confirmPassword },
      throwOnError: true,
    }).then((res) => res.data),

  deleteMyAccount: (password: string) =>
    postSystemUserDeleteMyAccount({
      body: { password },
      throwOnError: true,
    }).then((res) => res.data),

  requestPasswordReset: (account: string, method: Exclude<TwoFactorMethod, 'recovery'>) =>
    postSystemUserRequestPasswordReset({
      body: { account, method },
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data as PasswordResetChallenge),

  resetPassword: (
    challengeId: string,
    code: string,
    newPassword: string,
    confirmPassword: string,
  ) =>
    postSystemUserResetPassword({
      body: { challengeId, code, newPassword, confirmPassword },
      throwOnError: true,
      ...({ cancelable: false } as Record<string, unknown>),
    }).then((res) => res.data),

  getTwoFactorStatus: () =>
    getSystemUserTwoFactorStatus({ throwOnError: true }).then((res) => res.data as TwoFactorStatus),

  beginAuthenticatorSetup: (currentPassword: string) =>
    postSystemUserBeginAuthenticatorSetup({
      body: { currentPassword },
      throwOnError: true,
    }).then((res) => res.data as AuthenticatorSetup),

  confirmAuthenticatorSetup: (code: string) =>
    postSystemUserConfirmAuthenticatorSetup({
      body: { code },
      throwOnError: true,
    }).then((res) => res.data as AuthenticatorRecoveryCodes),

  requestMessageTwoFactorSetup: (
    method: Extract<TwoFactorMethod, 'email' | 'sms'>,
    currentPassword: string,
  ) =>
    postSystemUserRequestMessageTwoFactorSetup({
      body: { method, currentPassword },
      throwOnError: true,
    }).then((res) => res.data as PasswordResetChallenge),

  confirmMessageTwoFactorSetup: (challengeId: string, code: string) =>
    postSystemUserConfirmMessageTwoFactorSetup({
      body: { challengeId, code },
      throwOnError: true,
    }).then((res) => res.data),

  disableTwoFactor: (method: Exclude<TwoFactorMethod, 'recovery'>, currentPassword: string) =>
    postSystemUserDisableTwoFactor({
      body: { method, currentPassword },
      throwOnError: true,
    }).then((res) => res.data),

  /**
   * 刷新令牌
   * POST /api/system/user/refresh-token
   */
  refreshToken: () => {
    return withXsrf((token) =>
      postSystemUserRefreshToken({
        headers: { 'X-XSRF-TOKEN': token },
        throwOnError: true,
        ...({ showError: false } as Record<string, unknown>),
        ...({ cancelable: false } as Record<string, unknown>),
      }).then((res) => res.data as LoginResponse),
    )
  },

  /**
   * 撤销刷新令牌
   * POST /api/system/user/revoke-refresh-token
   */
  revokeRefreshToken: () => {
    return withXsrf((token) =>
      postSystemUserRevokeRefreshToken({
        headers: { 'X-XSRF-TOKEN': token },
        throwOnError: true,
        ...({ showError: false } as Record<string, unknown>),
        ...({ cancelable: false } as Record<string, unknown>),
      }).then((res) => res.data),
    )
  },

  getMyLoginSessions: () =>
    getSystemUserMyLoginSessions({ throwOnError: true }).then((res) => res.data as LoginSession[]),

  revokeMyLoginSession: (sessionId: string) =>
    postSystemUserRevokeMyLoginSessionSessionId({
      path: { sessionId },
      throwOnError: true,
    }).then((res) => res.data),

  /**
   * 当前登录用户已保存的前端偏好。
   * 从未保存过时后端返回 204/空响应，这里归一化为 null。
   */
  getMyPreferences: () =>
    getSystemUserPreferenceMyPreferences({
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
    }).then((res) => (res.data as UserPreferenceDto | null) || null),

  updateMyPreferences: (data: UpdateUserPreferenceDto) =>
    putSystemUserPreferenceMyPreferences({
      body: data,
      throwOnError: true,
      ...({ showError: false } as Record<string, unknown>),
    }).then((res) => res.data as UserPreferenceDto),
}

// 保持向后兼容的别名
export const getUserList = userApi.getList
export const createUser = userApi.create
export const updateUser = userApi.update
export const deleteUser = userApi.delete

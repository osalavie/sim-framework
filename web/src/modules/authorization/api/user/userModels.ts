/**
 * 用户模型定义
 * 网络 DTO 优先引用 OpenAPI 生成 Schema（src/api/system/types.gen.ts）；
 * 仅表单/查询参数等前端自有视图模型保留手写类型。
 */
import type {
  UserDto,
  LoginUserDto,
  ExternalLoginProviderDto,
  ExternalLoginRedirectDto,
  ExternalLoginBindingDto,
  ExternalLoginRegistrationRequest as ExternalLoginRegistrationRequestSchema,
  LinkWithExternalLoginRequest as LinkWithExternalLoginRequestSchema,
  LoginSessionDto,
  PasswordResetChallengeDto,
  TwoFactorStatusDto,
  AuthenticatorSetupDto,
  AuthenticatorRecoveryCodesDto,
  RoleInfo as RoleInfoSchema,
  PermissionInfo as PermissionInfoSchema,
} from '@/api/system'

// ===== 网络 DTO（来自生成 Schema）=====

/** 用户资料 DTO（前端必用的主键/登录名断言为必填，可空字段去除 null 以匹配视图） */
export type User = UserDto & {
  id: string
  userName: string
  nickName?: string
  name?: string
  description?: string
  avatar?: string
  isBuiltIn?: boolean
  roles?: RoleInfoSchema[]
}

/** 登录响应 DTO */
export type LoginResponse = LoginUserDto

/** 外部登录提供方 */
export type ExternalLoginProvider = ExternalLoginProviderDto & {
  provider: string
  displayName: string
}

/** 外部登录跳转响应 */
export type ExternalLoginRedirect = ExternalLoginRedirectDto

/** 外部登录绑定信息 */
export type ExternalLoginBinding = ExternalLoginBindingDto

/** 外部登录注册请求 */
export type ExternalLoginRegistrationRequest = ExternalLoginRegistrationRequestSchema

/** 外部登录关联已有账号请求 */
export type LinkWithExternalLoginRequest = LinkWithExternalLoginRequestSchema

/** 登录会话 */
export type LoginSession = LoginSessionDto & {
  id: string
  deviceType: 'PC' | 'Pad' | 'Phone'
  deviceName: string
  lastSeenAt: string
}

/** 密码重置挑战（challengeId 断言必填，视图依赖） */
export type PasswordResetChallenge = PasswordResetChallengeDto & {
  challengeId: string
}

/** 双因子状态 */
export type TwoFactorStatus = TwoFactorStatusDto

/** 验证器设置（sharedKey/authenticatorUri 断言为必填，视图依赖） */
export type AuthenticatorSetup = AuthenticatorSetupDto & {
  sharedKey: string
  authenticatorUri: string
}

/** 验证器恢复码 */
export type AuthenticatorRecoveryCodes = AuthenticatorRecoveryCodesDto & {
  recoveryCodes: string[]
}

/** 角色信息 */
export type RoleInfo = RoleInfoSchema

/** 权限信息 */
export type PermissionInfo = PermissionInfoSchema

// ===== 视图/表单模型 =====

/**
 * 用户表单数据（用于创建和更新）
 */
export interface UserFormData {
  userName: string
  nickName?: string
  name?: string
  email?: string
  phone?: string
  address?: string
  description?: string
  sex?: number
  roleIds?: string[]
}

export interface UpdateMyProfileData {
  nickName: string
  name?: string
  description?: string
}

/**
 * 用户查询参数
 */
export interface UserQueryParams {
  skipCount?: number
  maxResultCount?: number
  keyword?: string
  sorting?: string
}

/**
 * 外部登录回调参数。后端按 provider 原生协议解析 query，前端仅原样透传。
 */
export type ExternalLoginCallback = Record<string, string>

export type TwoFactorMethod = 'authenticator' | 'email' | 'sms' | 'recovery'

// 保持向后兼容的别名
export type UserProfile = User

import {
  getSystemE2EeIdentity,
  postSystemE2EeIdentity,
  postSystemE2EeResetIdentity,
  putSystemE2EePassphrase,
  putSystemE2EeRecoveryKey,
} from '@/api/system'
import type {
  CreateUserE2EeIdentityDto,
  ResetUserE2EeIdentityDto,
  UpdateUserE2EePassphraseDto,
  UpdateUserE2EeRecoveryKeyDto,
  UserE2EeIdentityDto,
} from '@/api/system'

/** 读取当前用户身份；未启用时后端返回 204，这里统一归一化为 null。 */
export async function fetchE2eeIdentity(): Promise<UserE2EeIdentityDto | null> {
  const response = await getSystemE2EeIdentity({ throwOnError: true })
  const data = response.data as UserE2EeIdentityDto | '' | undefined
  return data && typeof data === 'object' ? data : null
}

export async function createE2eeIdentity(
  input: CreateUserE2EeIdentityDto,
): Promise<UserE2EeIdentityDto> {
  const response = await postSystemE2EeIdentity({ body: input, throwOnError: true })
  return response.data as UserE2EeIdentityDto
}

export async function updateE2eePassphrase(
  input: UpdateUserE2EePassphraseDto,
): Promise<UserE2EeIdentityDto> {
  const response = await putSystemE2EePassphrase({ body: input, throwOnError: true })
  return response.data as UserE2EeIdentityDto
}

export async function updateE2eeRecoveryKey(
  input: UpdateUserE2EeRecoveryKeyDto,
): Promise<UserE2EeIdentityDto> {
  const response = await putSystemE2EeRecoveryKey({ body: input, throwOnError: true })
  return response.data as UserE2EeIdentityDto
}

/** 重置身份：服务端仅在没有已加密私有文章时允许。 */
export async function resetE2eeIdentity(
  input: ResetUserE2EeIdentityDto,
): Promise<UserE2EeIdentityDto> {
  const response = await postSystemE2EeResetIdentity({ body: input, throwOnError: true })
  return response.data as UserE2EeIdentityDto
}

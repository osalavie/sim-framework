import {
  getSystemE2EePasskeyConfiguration, getSystemE2EePasskey, postSystemE2EePasskeyRegistrationOptions,
  postSystemE2EePasskeyRegistration, postSystemE2EePasskeyIdUnlockOptions, postSystemE2EePasskeyUnlock,
  deleteSystemE2EePasskeyId, putSystemE2EePasskeyIdName,
} from '@/api/system'
import type { E2EePasskeyConfigurationDto, E2EePasskeyDto, E2EePasskeyOptionsDto, E2EePasskeyWrapDto,
  CompleteE2EePasskeyRegistrationDto, CompleteE2EePasskeyUnlockDto } from '@/api/system'

export const passkeyApi = {
  configuration: async () => (await getSystemE2EePasskeyConfiguration({ throwOnError: true })).data as E2EePasskeyConfigurationDto,
  list: async () => (await getSystemE2EePasskey({ throwOnError: true })).data as E2EePasskeyDto[],
  registrationOptions: async (signal: AbortSignal) => (await postSystemE2EePasskeyRegistrationOptions({ signal, throwOnError: true })).data as E2EePasskeyOptionsDto,
  register: async (body: CompleteE2EePasskeyRegistrationDto, signal: AbortSignal) =>
    (await postSystemE2EePasskeyRegistration({ body, signal, throwOnError: true })).data as E2EePasskeyDto,
  unlockOptions: async (id: string, signal: AbortSignal) =>
    (await postSystemE2EePasskeyIdUnlockOptions({ path: { id }, signal, throwOnError: true })).data as E2EePasskeyOptionsDto,
  unlock: async (body: CompleteE2EePasskeyUnlockDto, signal: AbortSignal) =>
    (await postSystemE2EePasskeyUnlock({ body, signal, throwOnError: true })).data as E2EePasskeyWrapDto,
  remove: async (id: string) => { await deleteSystemE2EePasskeyId({ path: { id }, throwOnError: true }) },
  rename: async (id: string, name: string) =>
    (await putSystemE2EePasskeyIdName({ path: { id }, body: { name }, throwOnError: true })).data as E2EePasskeyDto,
}

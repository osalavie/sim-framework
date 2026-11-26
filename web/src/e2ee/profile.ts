/**
 * E2EE 持久化格式标识。已有历史数据的业务系统必须在应用启动时
 * 通过 configureE2eeProfile 固定当时的标识，否则旧密文无法解密。
 */
export interface E2eeProfile {
  aadNamespace: string
  deviceDatabaseName: string
  deviceCacheAadPrefix: string
  fileMimeType: string
  fileMagic: string
  fileExtension: string
  passkeyAadPrefix: string
  passkeyDiagnosticsFormat: string
  recoveryHeader: string
}

const DEFAULT_PROFILE: E2eeProfile = {
  aadNamespace: 'sim',
  deviceDatabaseName: 'sim-e2ee',
  deviceCacheAadPrefix: 'sim-device-cache',
  fileMimeType: 'application/vnd.sim.e2ee',
  fileMagic: 'SIME2EE1',
  fileExtension: '.sime2ee',
  passkeyAadPrefix: 'sim-passkey-prf-hkdf-sha256-aes256gcm-v1',
  passkeyDiagnosticsFormat: 'sim-passkey-diagnostics-v1',
  recoveryHeader: 'SIM-E2EE-RECOVERY-KEY v1',
}

let profile: E2eeProfile = { ...DEFAULT_PROFILE }

export function configureE2eeProfile(next: Partial<E2eeProfile>): void {
  profile = { ...profile, ...next }
}

export function getE2eeProfile(): Readonly<E2eeProfile> {
  return profile
}

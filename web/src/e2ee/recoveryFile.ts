import { fromBase64Url, guidN, toBase64Url } from './encoding'
import { sm3 } from './gm/sm3'
import { getE2eeProfile } from './profile'

/** 恢复密钥文件是唯一的离线恢复材料；文本格式便于抄写/打印。 */
export function buildRecoveryKeyFile(userId: string, keyVersion: number, recoveryKey: Uint8Array): string {
  return [
    getE2eeProfile().recoveryHeader,
    `User: ${guidN(userId)}`,
    `KeyVersion: ${keyVersion}`,
    `RecoveryKey: ${toBase64Url(recoveryKey)}`,
    `Checksum: ${checksum(recoveryKey)}`,
    '',
  ].join('\n')
}

export interface ParsedRecoveryKey {
  userId: string | null
  keyVersion: number
  recoveryKey: Uint8Array
}

/** 接受完整恢复密钥文件，或直接粘贴的 base64url 恢复密钥。 */
export function parseRecoveryKeyInput(input: string): ParsedRecoveryKey | null {
  const text = input.trim()
  if (!text) return null

  if (!text.includes(getE2eeProfile().recoveryHeader)) {
    const key = tryDecodeKey(text)
    return key ? { userId: null, keyVersion: 1, recoveryKey: key } : null
  }

  const fields = new Map<string, string>()
  for (const line of text.split(/\r?\n/u)) {
    const separator = line.indexOf(':')
    if (separator <= 0) continue
    fields.set(line.slice(0, separator).trim().toLowerCase(), line.slice(separator + 1).trim())
  }

  const key = tryDecodeKey(fields.get('recoverykey') ?? '')
  if (!key) return null

  const expected = fields.get('checksum')
  if (expected && expected.toLowerCase() !== checksum(key)) {
    return null
  }

  const keyVersion = Number.parseInt(fields.get('keyversion') ?? '1', 10)
  return {
    userId: fields.get('user') ?? null,
    keyVersion: Number.isFinite(keyVersion) && keyVersion > 0 ? keyVersion : 1,
    recoveryKey: key,
  }
}

export function recoveryKeyChecksum(recoveryKey: Uint8Array): string {
  return checksum(recoveryKey)
}

function checksum(recoveryKey: Uint8Array): string {
  const digest = sm3(recoveryKey)
  return Array.from(digest.subarray(0, 8))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('')
}

function tryDecodeKey(value: string): Uint8Array | null {
  if (!/^[A-Za-z0-9_-]+$/u.test(value.trim())) return null
  try {
    const bytes = fromBase64Url(value.trim())
    return bytes.length === 16 ? bytes : null
  } catch {
    return null
  }
}

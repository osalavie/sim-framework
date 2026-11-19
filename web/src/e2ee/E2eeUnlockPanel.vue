<template>
  <n-modal
    :show="visible"
    :mask-closable="!busy"
    :close-on-esc="!busy"
    preset="card"
    :title="title"
    :style="{ width: 'min(560px, 92vw)' }"
    :segmented="{ content: 'soft' }"
    transform-origin="center"
    @update:show="handleUpdateShow"
  >
    <n-spin :show="busy">
      <n-space vertical size="large">
        <n-alert v-if="gate.reason && stage !== 'setup'" type="info" :show-icon="false">
          {{ gate.reason }}
        </n-alert>

        <template v-if="stage === 'loading'">
          <p>{{ t('E2ee:LoadingIdentity') }}</p>
        </template>

        <template v-else-if="stage === 'load-failed'">
          <n-alert type="error" :show-icon="false">{{ t('E2ee:LoadFailed') }}</n-alert>
          <n-space justify="end">
            <n-button @click="close">{{ t('Common:Cancel') }}</n-button>
            <n-button type="primary" @click="loadIdentity">{{ t('Common:Retry') }}</n-button>
          </n-space>
        </template>

        <template v-else-if="stage === 'setup'">
          <n-form label-placement="top" @submit.prevent>
            <n-form-item>
              <template #label>
                <span class="e2ee-label">
                  {{ t('E2ee:Passphrase') }}
                  <PoHintIcon :text="t('E2ee:ImageBoundaryNote')" />
                </span>
              </template>
              <n-input
                v-model:value="passphrase"
                type="password"
                show-password-on="click"
                :placeholder="t('E2ee:PassphraseHint')"
              />
            </n-form-item>
            <n-form-item :label="t('E2ee:PassphraseConfirm')">
              <n-input
                v-model:value="passphraseConfirm"
                type="password"
                show-password-on="click"
                @keyup.enter="setupIdentity"
              />
            </n-form-item>
          </n-form>
          <n-checkbox v-if="!passkeyPreference" v-model:checked="rememberDevice">{{ t('E2ee:RememberDevice') }}</n-checkbox>
          <n-alert type="error" :show-icon="false">{{ t('E2ee:NoBackdoor') }}</n-alert>
          <n-space justify="end">
            <n-button :disabled="busy" @click="close">{{ t('Common:Cancel') }}</n-button>
            <n-button type="primary" :loading="busy" @click="setupIdentity">
              {{ t('E2ee:SetupAction') }}
            </n-button>
          </n-space>
        </template>

        <template v-else-if="stage === 'recovery-saved'">
          <n-alert type="warning" :show-icon="false">{{ t('E2ee:RecoveryIntro') }}</n-alert>
          <n-input
            :value="recoveryFileText"
            type="textarea"
            readonly
            :rows="6"
            class="e2ee-recovery-text"
          />
          <n-space justify="space-between" align="center">
            <n-button secondary @click="downloadRecoveryFile">
              {{ t('E2ee:RecoveryDownload') }}
            </n-button>
            <n-checkbox v-model:checked="recoveryConfirmed">
              {{ t('E2ee:RecoveryConfirm') }}
            </n-checkbox>
          </n-space>
          <n-space justify="end">
            <n-button
              type="primary"
              :disabled="!recoveryConfirmed"
              @click="finishSetup"
            >
              {{ t('E2ee:Finish') }}
            </n-button>
          </n-space>
        </template>

        <template v-else-if="stage === 'unlock'">
          <template v-if="preferredPasskey">
            <n-button type="primary" :loading="busy" :disabled="busy" @click="unlockUsingPasskey">{{ t('E2ee:UsePasskey') }}</n-button>
            <p class="po-small po-muted">{{ t('E2ee:PasskeyUnlockHint') }}</p>
          </template>
          <template v-if="unlockMode === 'passphrase'">
            <n-form label-placement="top" @submit.prevent>
              <n-form-item>
                <template #label>
                  <span class="e2ee-label-bar">
                    <span class="e2ee-label">
                      {{ t('E2ee:Passphrase') }}
                      <PoHintIcon :text="t('E2ee:ImageBoundaryNote')" />
                    </span>
                    <span class="e2ee-trust-device">
                      <span class="po-small po-muted">{{ t('E2ee:RememberDevice') }}</span>
                      <n-switch
                        v-model:value="rememberDevice"
                        size="small"
                        :disabled="busy"
                        :aria-label="t('E2ee:RememberDevice')"
                      />
                    </span>
                  </span>
                </template>
                <n-input
                  v-model:value="passphrase"
                  type="password"
                  show-password-on="click"
                  :placeholder="t('E2ee:PassphraseHint')"
                  @keyup.enter="unlockWithPassphrase"
                />
              </n-form-item>
            </n-form>
            <n-space justify="space-between">
              <n-space>
                <n-button text type="primary" :disabled="busy" @click="switchToRecovery">
                  {{ t('E2ee:UseRecoveryKey') }}
                </n-button>
              </n-space>
              <n-space>
                <n-button :disabled="busy" @click="close">{{ t('Common:Cancel') }}</n-button>
                <n-button type="primary" :loading="busy" :disabled="busy" @click="unlockWithPassphrase">
                  {{ unlockActionLabel }}
                </n-button>
              </n-space>
            </n-space>
          </template>
          <template v-else-if="unlockMode === 'recovery'">
            <n-form label-placement="top" @submit.prevent>
              <n-form-item :label="t('E2ee:RecoveryKey')">
                <n-input
                  v-model:value="recoveryInput"
                  type="textarea"
                  :rows="4"
                  :placeholder="t('E2ee:RecoveryKeyPlaceholder')"
                />
              </n-form-item>
            </n-form>
            <n-space justify="space-between">
              <n-space>
                <n-button text type="primary" :disabled="busy" @click="switchToPassphrase">
                  {{ t('E2ee:UsePassphrase') }}
                </n-button>
              </n-space>
              <n-space>
                <n-button :disabled="busy" @click="close">{{ t('Common:Cancel') }}</n-button>
                <n-button type="primary" :loading="busy" :disabled="busy" @click="unlockWithRecovery">
                  {{ unlockActionLabel }}
                </n-button>
              </n-space>
            </n-space>
          </template>
        </template>

        <template v-else-if="stage === 'reset-passphrase'">
          <n-alert type="success" :show-icon="false">{{ t('E2ee:RecoveryAccepted') }}</n-alert>
          <n-alert type="warning" :show-icon="false">{{ t('E2ee:ResetIntro') }}</n-alert>
          <n-form label-placement="top" @submit.prevent>
            <n-form-item>
              <template #label>
                <span class="e2ee-label">
                  {{ t('E2ee:NewPassphrase') }}
                  <PoHintIcon :text="t('E2ee:ImageBoundaryNote')" />
                </span>
              </template>
              <n-input
                v-model:value="passphrase"
                type="password"
                show-password-on="click"
                :placeholder="t('E2ee:PassphraseHint')"
              />
            </n-form-item>
            <n-form-item :label="t('E2ee:PassphraseConfirm')">
              <n-input
                v-model:value="passphraseConfirm"
                type="password"
                show-password-on="click"
                @keyup.enter="resetPassphrase"
              />
            </n-form-item>
          </n-form>
          <n-checkbox v-if="!passkeyPreference" v-model:checked="rememberDevice">{{ t('E2ee:RememberDevice') }}</n-checkbox>
          <n-space justify="end">
            <n-button :disabled="busy" @click="close">{{ t('Common:Cancel') }}</n-button>
            <n-button type="primary" :loading="busy" @click="resetPassphrase">
              {{ t('E2ee:ResetAction') }}
            </n-button>
          </n-space>
        </template>

        <n-alert v-if="errorMessage" type="error" :show-icon="false">{{ errorMessage }}</n-alert>
        <PasskeyDiagnostics v-if="errorMessage && passkeyDiagnosticFailure" />
      </n-space>
    </n-spin>
  </n-modal>
</template>

<script setup lang="ts">
import {
  createIdentitySecretsAsync as createIdentitySecrets,
  unlockUmkWithPassphraseAsync as unlockUmkWithPassphrase,
  rewrapWithPassphraseAsync as rewrapWithPassphrase,
} from '@/e2ee/keysAsync'

import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import PoHintIcon from '@/components/FormItem/PoHintIcon.vue'
import { useUserStore } from '@/stores/user'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { passkeyApi } from './passkeyApi'
import { supportsPasskeyUnlock, unlockWithPasskey, PasskeyError } from './passkeys'
import type { E2EePasskeyDto } from '@/api/system'
import { t } from '@/i18n'
import PasskeyDiagnostics from './PasskeyDiagnostics.vue'
import { toBase64Url } from '@/e2ee/encoding'
import {
  createE2eeIdentity,
  fetchE2eeIdentity,
  updateE2eePassphrase,
} from '@/e2ee/identityApi'
import {
  unlockUmkWithRecoveryKey,
} from '@/e2ee/keys'
import { clearDeviceCache, saveDeviceCache, loadPasskeyPreference } from '@/e2ee/deviceStore'
import type { IdentitySecrets } from '@/e2ee/keys'
import { cancelE2eeKeyOperations } from '@/e2ee/keysAsync'
import { KDF_ALGORITHM } from '@/e2ee/kdf'
import { setE2eeUmk } from '@/e2ee/keyVault'
import { buildRecoveryKeyFile, parseRecoveryKeyInput } from '@/e2ee/recoveryFile'
import { E2eeDecryptError } from '@/e2ee/types'
import { ensureE2eeMessages } from '@/e2ee/messages'
import { completeE2eeUnlock, cancelE2eeUnlock, useE2eeUnlockGateState } from '@/e2ee/unlockGate'
import type { UserE2EeIdentityDto } from '@/api/system'

type Stage =
  | 'loading'
  | 'load-failed'
  | 'setup'
  | 'recovery-saved'
  | 'unlock'
  | 'reset-passphrase'

const MIN_PASSPHRASE_LENGTH = 6

const gate = useE2eeUnlockGateState()
const userStore = useUserStore()

const stage = ref<Stage>('loading')
const busy = ref(false)
const errorMessage = ref('')
const passkeyDiagnosticFailure = ref(false)
const identity = ref<UserE2EeIdentityDto | null>(null)

const passphrase = ref('')
const passphraseConfirm = ref('')
const unlockMode = ref<'passphrase' | 'recovery'>('passphrase')
const recoveryInput = ref('')
const recoveryFileText = ref('')
const recoveryConfirmed = ref(false)
const pendingUmk = ref<Uint8Array | null>(null)
const rememberDevice = ref(false)
const passkeyRows = ref<E2EePasskeyDto[]>([])
const passkeyPreference = ref<string | null>(null)
const passkeysSupported = supportsPasskeyUnlock()

/** 只在本浏览器已选用通行密钥、且该凭证仍属于当前身份版本时提供通行密钥解锁。 */
const preferredPasskey = computed(() =>
  passkeysSupported && passkeyPreference.value
    ? passkeyRows.value.find(row => row.id === passkeyPreference.value) ?? null
    : null,
)
const title = ref('')

function updateTitle() {
  title.value =
    stage.value === 'setup' || stage.value === 'recovery-saved'
      ? t('E2ee:SetupTitle')
      : stage.value === 'reset-passphrase'
        ? t('E2ee:ResetPassphraseTitle')
        : gate.purpose === 'encrypt'
          ? t('E2ee:EncryptAuthTitle')
          : t('E2ee:UnlockTitle')
}

const unlockActionLabel = computed(() =>
  t(gate.purpose === 'encrypt' ? 'E2ee:EncryptAuthAction' : 'E2ee:UnlockAction'),
)

const visible = ref(true)

onMounted(() => {
  visible.value = true
  void loadIdentity()
})

async function loadIdentity() {
  stage.value = 'loading'
  updateTitle()
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
  await ensureE2eeMessages()
  try {
    identity.value = await fetchE2eeIdentity()
    stage.value = identity.value ? 'unlock' : 'setup'
    passkeyPreference.value = requireUserId() ? await loadPasskeyPreference(requireUserId()!) : null
    if (identity.value && passkeysSupported) {
      try {
        const configuration = await passkeyApi.configuration()
        if (configuration.enabled) {
          passkeyRows.value = await passkeyApi.list()
        }
      } catch { passkeyRows.value = [] }
    }
  } catch (error) {
    console.error('Failed to load E2EE identity:', error)
    stage.value = 'load-failed'
  }
  updateTitle()
}

function requireUserId(): string | null {
  return userStore.user?.id ?? null
}

async function setupIdentity() {
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
  const userId = requireUserId()
  if (!userId) {
    errorMessage.value = t('E2ee:LoginRequired')
    return
  }

  const validation = validatePassphrase()
  if (validation) {
    errorMessage.value = validation
    return
  }

  busy.value = true
  let secrets: IdentitySecrets | undefined
  try {
    await yieldToPaint()
    secrets = await createIdentitySecrets(passphrase.value, userId, 1)
    await createE2eeIdentity({
      kdfAlgorithm: KDF_ALGORITHM,
      kdfSalt: toBase64Url(secrets.salt),
      kdfIterations: secrets.iterations,
      passwordWrappedKey: secrets.passwordWrappedKey,
      recoveryWrappedKey: secrets.recoveryWrappedKey,
    })

    pendingUmk.value = secrets.umk.slice()
    recoveryFileText.value = buildRecoveryKeyFile(userId, 1, secrets.recoveryKey)
    passphrase.value = ''
    passphraseConfirm.value = ''
    stage.value = 'recovery-saved'
    updateTitle()
  } catch (error) {
    console.error('Failed to create E2EE identity:', formatError(error))
    errorMessage.value = t('E2ee:SetupFailed')
  } finally {
    secrets?.umk.fill(0)
    secrets?.recoveryKey.fill(0)
    passphrase.value = ''
    passphraseConfirm.value = ''
    busy.value = false
  }
}

async function finishSetup() {
  const userId = requireUserId()
  if (!userId || !pendingUmk.value || !recoveryConfirmed.value) {
    return
  }
  const umk = pendingUmk.value
  await persistDeviceCache(umk, userId, 1)
  setE2eeUmk(umk, userId, 1)
  umk.fill(0)
  pendingUmk.value = null
  completeE2eeUnlock()
  resetState()
}

async function unlockUsingPasskey() {
  const userId = requireUserId()
  const passkeyId = preferredPasskey.value?.id
  if (busy.value || !userId || !identity.value || !passkeyId) return
  busy.value = true
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
  let umk: Uint8Array | null = null
  try {
    const version = identity.value.keyVersion ?? 1
    umk = await unlockWithPasskey(userId, passkeyId, version)
    if (requireUserId() !== userId) return
    setE2eeUmk(umk, userId, version)
    completeE2eeUnlock()
    resetState()
  } catch (error) {
    passkeyDiagnosticFailure.value = true
    errorMessage.value = t(error instanceof PasskeyError && error.code === 'unsupported' ? 'E2ee:PasskeyUnsupported'
    : error instanceof PasskeyError && error.code === 'prf-unavailable' ? 'E2ee:PasskeyPrfUnavailable'
    : error instanceof PasskeyError && error.code === 'not-allowed' ? 'E2ee:PasskeyNotAllowed'
      : error instanceof PasskeyError && error.code === 'cancelled' ? 'E2ee:PasskeyCancelled' : 'E2ee:PasskeyFailed')
  } finally { umk?.fill(0); busy.value = false }
}

async function unlockWithPassphrase() {
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
  const userId = requireUserId()
  if (!userId || !identity.value) {
    errorMessage.value = t('E2ee:LoginRequired')
    return
  }
  if (!passphrase.value) {
    errorMessage.value = t('E2ee:UnlockFailed')
    return
  }

  let unlocked: Uint8Array | undefined
  busy.value = true
  try {
    await yieldToPaint()
    const umk = unlocked = await unlockUmkWithPassphrase(passphrase.value, identity.value, userId)
    const version = identity.value.keyVersion ?? 1
    await persistDeviceCache(umk, userId, version)
    setE2eeUmk(umk, userId, version)
    umk.fill(0)
    passphrase.value = ''
    completeE2eeUnlock()
    resetState()
  } catch (error) {
    errorMessage.value = t('E2ee:UnlockFailed')
    console.warn('E2EE unlock failed:', formatError(error))
  } finally {
    unlocked?.fill(0)
    passphrase.value = ''
    busy.value = false
  }
}

async function unlockWithRecovery() {
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
  const userId = requireUserId()
  if (!userId || !identity.value) {
    errorMessage.value = t('E2ee:LoginRequired')
    return
  }

  const parsed = parseRecoveryKeyInput(recoveryInput.value)
  if (!parsed) {
    errorMessage.value = t('E2ee:RecoveryKeyInvalid')
    return
  }
  if (parsed.userId && parsed.userId !== userId.replace(/-/g, '').toLowerCase()) {
    parsed.recoveryKey.fill(0)
    errorMessage.value = t('E2ee:RecoveryKeyInvalid')
    return
  }

  busy.value = true
  try {
    await yieldToPaint()
    pendingUmk.value = unlockUmkWithRecoveryKey(parsed.recoveryKey, identity.value, userId)
    parsed.recoveryKey.fill(0)
    recoveryInput.value = ''
    passphrase.value = ''
    passphraseConfirm.value = ''
    stage.value = 'reset-passphrase'
    updateTitle()
  } catch (error) {
    errorMessage.value = t('E2ee:RecoveryKeyInvalid')
    console.warn('E2EE recovery key unlock failed:', formatError(error))
  } finally {
    parsed.recoveryKey.fill(0)
    recoveryInput.value = ''
    busy.value = false
  }
}

async function resetPassphrase() {
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
  const userId = requireUserId()
  if (!userId || !identity.value || !pendingUmk.value) {
    return
  }

  const validation = validatePassphrase()
  if (validation) {
    errorMessage.value = validation
    return
  }

  busy.value = true
  try {
    await yieldToPaint()
    const wrapped = await rewrapWithPassphrase(passphrase.value, pendingUmk.value, userId, identity.value)
    const updated = await updateE2eePassphrase({
      passwordWrappedKey: wrapped,
      expectedRevision: identity.value.revision ?? undefined,
    })
    const version = updated.keyVersion ?? 1
    await persistDeviceCache(pendingUmk.value, userId, version)
    setE2eeUmk(pendingUmk.value, userId, version)
    pendingUmk.value.fill(0)
    pendingUmk.value = null
    passphrase.value = ''
    passphraseConfirm.value = ''
    completeE2eeUnlock()
    resetState()
  } catch (error) {
    console.error('Failed to reset E2EE passphrase:', formatError(error))
    errorMessage.value = t('E2ee:ResetFailed')
  } finally {
    busy.value = false
  }
}

async function persistDeviceCache(umk: Uint8Array, userId: string, version: number): Promise<void> {
  try {
    if (!rememberDevice.value) {
      await clearDeviceCache(userId)
      return
    }

    await saveDeviceCache({ userId, keyVersion: version, umk })
  } catch (error) {
    console.warn('Failed to update E2EE device cache:', error)
    useDiscreteApi.message.warning(t('E2ee:DeviceTrustFailed'))
  }
}

/** 只对设置/重置生效；解锁不校验长度，兼容历史上设置的短口令。 */
function validatePassphrase(): string {
  if (!passphrase.value) {
    return t('E2ee:PassphraseRequired')
  }
  if (passphrase.value.length < MIN_PASSPHRASE_LENGTH) {
    return t('E2ee:PassphraseTooShort')
  }
  if (passphrase.value !== passphraseConfirm.value) {
    return t('E2ee:PassphraseMismatch')
  }
  return ''
}

function switchToRecovery() {
  unlockMode.value = 'recovery'
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
}

function switchToPassphrase() {
  unlockMode.value = 'passphrase'
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
}

function downloadRecoveryFile() {
  const userId = requireUserId()
  if (!userId || !recoveryFileText.value) return

  const blob = new Blob([recoveryFileText.value], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `sim-e2ee-recovery-${userId.replace(/-/g, '').toLowerCase()}-${new Date()
    .toISOString()
    .slice(0, 10)}.txt`
  link.click()
  URL.revokeObjectURL(url)
}

function handleUpdateShow(show: boolean) {
  if (show || busy.value) return
  close()
}

function close() {
  if (busy.value) return
  pendingUmk.value?.fill(0)
  pendingUmk.value = null
  resetState()
  cancelE2eeUnlock()
}

function resetState() {
  passphrase.value = ''
  passphraseConfirm.value = ''
  recoveryInput.value = ''
  recoveryFileText.value = ''
  recoveryConfirmed.value = false
  rememberDevice.value = false
  passkeyRows.value = []
  passkeyPreference.value = null
  errorMessage.value = ''
  passkeyDiagnosticFailure.value = false
  unlockMode.value = 'passphrase'
  identity.value = null
  stage.value = 'loading'
  visible.value = false
}

function formatError(error: unknown): string {
  return error instanceof E2eeDecryptError ? error.name : 'E2eeOperationError'
}

onBeforeUnmount(() => {
  cancelE2eeKeyOperations()
  pendingUmk.value?.fill(0)
  pendingUmk.value = null
  resetState()
})

/** KDF 是同步长任务，先让浏览器完成一帧渲染，加载态才可见。 */
async function yieldToPaint(): Promise<void> {
  await nextTick()
  await new Promise<void>((resolve) => {
    const fallback = setTimeout(resolve, 100)
    requestAnimationFrame(() => {
      clearTimeout(fallback)
      setTimeout(resolve, 0)
    })
  })
}
</script>

<style scoped>
.e2ee-recovery-text :deep(textarea) {
  font-family: var(--po-font-mono, monospace);
  font-size: 12px;
  line-height: 1.6;
}

.e2ee-label {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.e2ee-label-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  width: 100%;
}

.e2ee-trust-device {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-weight: 400;
}
</style>

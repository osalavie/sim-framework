<template>
  <div class="po-page">
    <header class="po-page-head">
      <div class="po-page-head__actions">
        <n-button secondary :loading="sessionsLoading" @click="loadLoginSessions">
          {{ t('Common:Refresh') }}
        </n-button>
      </div>
    </header>

    <n-spin :show="loading">
      <div class="po-grid po-grid--sidebar">
        <div class="po-stack">
          <section class="po-card">
            <div class="po-list">
              <article class="po-list__item authz-factor-row">
                <div class="authz-factor-main">
                  <div class="authz-factor-title">
                    <b>{{ t('UserSecurity:Password') }}</b>
                  </div>
                </div>
                <n-button type="primary" secondary @click="openPasswordDialog">
                  {{ t('UserSecurity:Modify') }}
                </n-button>
              </article>

              <article v-for="factor in factorItems" :key="factor.method" class="po-list__item authz-factor-row">
                <div class="authz-factor-main">
                  <div class="authz-factor-title">
                    <b>{{ factor.title }}</b>
                    <span class="po-pill" :class="factor.enabled ? 'po-pill--mint' : 'po-pill--outline'">
                      {{ factor.enabled ? t('Common:Enabled') : t('UserSecurity:Disabled') }}
                    </span>
                  </div>
                  <p class="po-small po-muted">{{ factor.description }}</p>
                </div>
                <n-button :type="factor.enabled ? 'default' : 'primary'" :secondary="factor.enabled"
                  @click="openFactorDialog(factor.method, factor.enabled)">
                  {{ factor.enabled ? t('UserSecurity:Disable') : t('UserSecurity:Enable') }}
                </n-button>
              </article>

              <article v-for="provider in externalProviders" :key="provider.provider" class="po-list__item authz-factor-row">
                <div class="authz-factor-main">
                  <div class="authz-factor-title">
                    <b>{{ provider.displayName }}</b>
                    <span class="po-pill" :class="boundBinding(provider.provider) ? 'po-pill--mint' : 'po-pill--outline'">
                      {{
                        boundBinding(provider.provider)
                          ? t('UserProfile:ExternalBindingBound')
                          : t('UserProfile:ExternalBindingUnbound')
                      }}
                    </span>
                  </div>
                  <p v-if="boundBinding(provider.provider)" class="po-small po-muted">
                    {{ boundBinding(provider.provider)?.providerUserName }}
                    <span v-if="boundBinding(provider.provider)?.email"> · {{ boundBinding(provider.provider)?.email }}</span>
                  </p>
                  <p v-else class="po-small po-muted">{{ t('UserProfile:ExternalBindingUnboundHint') }}</p>
                </div>
                <n-button
                  v-if="!boundBinding(provider.provider)"
                  type="primary"
                  secondary
                  :loading="bindingProvider === provider.provider"
                  @click="bindExternalLogin(provider.provider)"
                >
                  {{ t('UserProfile:BindAction') }}
                </n-button>
                <n-popconfirm
                  v-else
                  :positive-text="t('Common:Confirm')"
                  :negative-text="t('Common:Cancel')"
                  @positive-click="unbindExternalLogin(provider.provider)"
                >
                  <template #trigger>
                    <n-button type="error" secondary :loading="bindingProvider === provider.provider">
                      {{ t('UserProfile:UnbindAction') }}
                    </n-button>
                  </template>
                  {{ t('UserProfile:BindConfirm') }}
                </n-popconfirm>
              </article>
            </div>

            <p v-if="twoFactorStatus?.authenticatorEnabled" class="po-small po-muted authz-recovery-status">
              {{ t('UserSecurity:RecoveryRemaining') }}:
              {{ twoFactorStatus.recoveryCodesRemaining }}
            </p>
          </section>

          <section v-if="userStore.user?.isBuiltIn !== true" class="po-card">
            <div class="po-list">
              <article class="po-list__item authz-factor-row">
                <div class="authz-factor-main">
                  <div class="authz-factor-title">
                    <b>{{ t('UserSecurity:DeleteAccountTitle') }}</b>
                  </div>
                  <p class="po-small po-muted">{{ t('UserSecurity:DeleteAccountDescription') }}</p>
                </div>
                <n-button type="error" secondary @click="openDeleteAccountDialog">
                  {{ t('UserSecurity:DeleteAccountAction') }}
                </n-button>
              </article>
            </div>
          </section>
        </div>

        <section class="po-card authz-session-card">
          <header class="po-card__head">
            <div>
              <h3>{{ t('LoginSessions:Kicker') }}</h3>
            </div>
            <n-button size="small" secondary :loading="sessionsLoading" @click="loadLoginSessions">
              {{ t('Common:Refresh') }}
            </n-button>
          </header>

          <n-spin :show="sessionsLoading">
            <p v-if="!loginSessions.length" class="po-small po-muted">{{ t('LoginSessions:Empty') }}</p>
            <div v-else class="po-list">
              <article v-for="session in loginSessions" :key="session.id" class="po-list__item authz-session-row">
                <div class="authz-session-main">
                  <div class="authz-factor-title">
                    <b>{{ session.deviceName }}</b>
                    <span class="po-pill po-pill--outline">{{ session.deviceType }}</span>
                    <span v-if="session.isCurrent" class="po-pill po-pill--accent">
                      {{ t('LoginSessions:Current') }}
                    </span>
                    <span class="po-pill" :class="session.isOnline ? 'po-pill--mint' : 'po-pill--outline'">
                      {{ session.isOnline ? t('LoginSessions:Online') : t('LoginSessions:Offline') }}
                    </span>
                  </div>
                  <p class="po-small po-muted">
                    {{ t('LoginSessions:LastSeen') }} {{ formatSessionTime(session.lastSeenAt) }}
                    <span v-if="session.ipAddress"> · IP {{ session.ipAddress }}</span>
                  </p>
                </div>
                <n-popconfirm
                  :positive-text="t('Common:Confirm')"
                  :negative-text="t('Common:Cancel')"
                  @positive-click="revokeLoginSession(session)"
                >
                  <template #trigger>
                    <n-button type="error" secondary size="small" :loading="revokingSessionId === session.id">
                      {{
                        session.isCurrent
                          ? t('LoginSessions:LogoutCurrent')
                          : t('LoginSessions:Revoke')
                      }}
                    </n-button>
                  </template>
                  {{ t('LoginSessions:RevokeConfirm') }}
                </n-popconfirm>
              </article>
            </div>
          </n-spin>
        </section>
      </div>

      <n-modal v-model:show="showSecurityDialog" :mask-closable="!securitySaving">
        <n-card class="authz-security-dialog" :title="securityDialogTitle" closable role="dialog" aria-modal="true"
          @close="closeSecurityDialog">
          <template v-if="securityAction === 'disable'">
            <PoFormItem
              :label="t('UserSecurity:CurrentPassword')"
              :description="t('UserSecurity:DisableHint')"
              required
            >
              <n-input v-model:value="securityPassword" type="password" show-password-on="click"
                :input-props="{ autocomplete: 'current-password' }" />
            </PoFormItem>
          </template>
          <template v-else-if="securityMethod === 'authenticator'">
            <template v-if="!authenticatorSetup">
              <PoFormItem
                :label="t('UserSecurity:CurrentPassword')"
                :description="t('UserSecurity:AuthenticatorStartHint')"
                required
              >
                <n-input v-model:value="securityPassword" type="password" show-password-on="click"
                  :input-props="{ autocomplete: 'current-password' }" />
              </PoFormItem>
            </template>
            <template v-else>
              <div class="authz-authenticator-setup">
                <p class="po-small po-muted">{{ t('UserSecurity:AuthenticatorScanHint') }}</p>
                <div v-if="!authenticatorQrCodeFailed" class="authz-qr-frame">
                  <n-spin v-if="!authenticatorQrCode" size="small" />
                  <LazyImage v-else :src="authenticatorQrCode" :alt="t('UserSecurity:AuthenticatorQrAlt')" width="220"
                    height="220" />
                </div>
                <n-button text type="primary" @click="showAuthenticatorKey = !showAuthenticatorKey">
                  {{
                    showAuthenticatorKey
                      ? t('UserSecurity:HideManualKey')
                      : t('UserSecurity:ShowManualKey')
                  }}
                </n-button>
                <div v-if="showAuthenticatorKey" class="po-card po-card--cream authz-manual-key">
                  <p class="po-small po-muted">{{ t('UserSecurity:AuthenticatorManualHint') }}</p>
                  <div class="authz-shared-key">{{ authenticatorSetup.sharedKey }}</div>
                  <n-button size="small" secondary @click="copyAuthenticatorKey">
                    {{ t('UserSecurity:CopyManualKey') }}
                  </n-button>
                </div>
              </div>
              <PoFormItem :label="t('UserSecurity:VerificationCode')" required>
                <n-input v-model:value="securityCode" :maxlength="6"
                  :input-props="{ autocomplete: 'one-time-code', inputmode: 'numeric' }" />
              </PoFormItem>
            </template>
          </template>
          <template v-else>
            <template v-if="!messageSetupChallengeId">
              <p class="po-small po-muted authz-editor-hint">{{ selectedFactorDescription }}</p>
              <PoFormItem :label="t('UserSecurity:CurrentPassword')" required>
                <n-input v-model:value="securityPassword" type="password" show-password-on="click"
                  :input-props="{ autocomplete: 'current-password' }" />
              </PoFormItem>
            </template>
            <PoFormItem v-else :label="t('UserSecurity:VerificationCode')">
              <n-input v-model:value="securityCode" :maxlength="6"
                :input-props="{ autocomplete: 'one-time-code', inputmode: 'numeric' }" />
            </PoFormItem>
          </template>

          <n-alert v-if="recoveryCodes.length" type="warning" :title="t('UserSecurity:SaveRecoveryCodes')">
            <div class="authz-recovery-codes">
              <code v-for="code in recoveryCodes" :key="code">{{ code }}</code>
            </div>
          </n-alert>

          <template #footer>
            <n-space justify="end">
              <n-button secondary @click="closeSecurityDialog">{{ t('Common:Cancel') }}</n-button>
              <n-button v-if="recoveryCodes.length" type="primary" @click="closeSecurityDialog">
                {{ t('Common:Confirm') }}
              </n-button>
              <n-button v-else type="primary" :loading="securitySaving" @click="submitSecurityAction">
                {{ securitySubmitLabel }}
              </n-button>
            </n-space>
          </template>
        </n-card>
      </n-modal>

      <n-modal v-model:show="showPasswordDialog" :mask-closable="!passwordSaving">
        <n-card class="authz-password-dialog" :title="t('UserSecurity:ChangePassword')" closable role="dialog"
          aria-modal="true" @close="closePasswordDialog">
          <n-form ref="passwordFormRef" :model="passwordForm" :rules="passwordRules" label-placement="top">
            <PoFormItem :label="t('UserSecurity:CurrentPassword')" path="currentPassword" required>
              <n-input v-model:value="passwordForm.currentPassword" type="password" show-password-on="click"
                :input-props="{ autocomplete: 'current-password' }" />
            </PoFormItem>

            <PoFormItem
              :label="t('Auth:NewPassword')"
              :description="t('UserSecurity:ChangePasswordHint')"
              path="newPassword"
              required
            >
              <n-input v-model:value="passwordForm.newPassword" type="password" show-password-on="click"
                :input-props="{ autocomplete: 'new-password' }" />
            </PoFormItem>

            <div v-if="passwordForm.newPassword" class="authz-password-strength">
              <div class="authz-password-strength__head">
                <span>{{ t('UserSecurity:PasswordStrength') }}</span>
                <b class="authz-password-strength__label" :data-level="passwordStrength.level">
                  {{ passwordStrength.label }}
                </b>
              </div>
              <div class="authz-password-strength__meter" role="meter" :aria-label="t('UserSecurity:PasswordStrength')"
                aria-valuemin="0" :aria-valuemax="passwordSignals.length" :aria-valuenow="passwordStrength.met"
                :aria-valuetext="passwordStrength.label">
                <span v-for="segment in passwordSignals.length" :key="segment" class="authz-password-strength__segment"
                  :class="segment <= passwordStrength.met ? ['is-filled', `is-${passwordStrength.level}`] : []" />
              </div>
              <ul class="authz-password-rules">
                <li v-for="signal in passwordSignals" :key="signal.key" class="authz-password-rule"
                  :class="{ 'is-met': signal.met }">
                  {{ signal.label }}
                </li>
              </ul>
            </div>

            <PoFormItem
              ref="confirmPasswordItemRef"
              :label="t('Auth:ConfirmPassword')"
              path="confirmPassword"
              required
            >
              <n-input v-model:value="passwordForm.confirmPassword" type="password" show-password-on="click"
                :input-props="{ autocomplete: 'new-password' }" />
            </PoFormItem>
          </n-form>

          <template #footer>
            <n-space justify="end">
              <n-button secondary :disabled="passwordSaving" @click="closePasswordDialog">
                {{ t('Common:Cancel') }}
              </n-button>
              <n-button type="primary" :loading="passwordSaving" @click="submitPasswordChange">
                {{ t('UserSecurity:ChangePassword') }}
              </n-button>
            </n-space>
          </template>
        </n-card>
      </n-modal>

      <n-modal v-model:show="showDeleteAccountDialog" :mask-closable="!deleteAccountSaving">
        <n-card class="authz-delete-account-dialog" :title="t('UserSecurity:DeleteAccountTitle')" closable
          role="dialog" aria-modal="true" @close="closeDeleteAccountDialog">
          <template v-if="deleteAccountStep === 'password'">
            <PoFormItem
              :label="t('UserSecurity:CurrentPassword')"
              :description="t('UserSecurity:DeleteAccountPasswordHint')"
              required
            >
              <n-input v-model:value="deleteAccountPassword" type="password" show-password-on="click"
                :input-props="{ autocomplete: 'current-password' }" @keyup.enter="goToDeleteAgreement" />
            </PoFormItem>
          </template>

          <template v-else>
            <div class="authz-delete-agreement">
              <p class="po-small">{{ t('UserSecurity:DeleteAccountAgreementAccount') }}</p>
              <p class="po-small">{{ t('UserSecurity:DeleteAccountAgreementContent') }}</p>
              <p class="po-small">{{ t('UserSecurity:DeleteAccountAgreementComments') }}</p>
              <p class="po-small authz-delete-agreement__danger">
                {{ t('UserSecurity:DeleteAccountAgreementIrreversible') }}
              </p>
            </div>
            <n-form-item :label="t('UserSecurity:DeleteAccountConfirmLabel', { phrase: deleteAccountPhrase })" required>
              <n-input v-model:value="deleteAccountPhraseInput" :placeholder="deleteAccountPhrase"
                @keyup.enter="submitDeleteAccount" />
            </n-form-item>
          </template>

          <template #footer>
            <n-space justify="end">
              <n-button secondary :disabled="deleteAccountSaving" @click="closeDeleteAccountDialog">
                {{ t('Common:Cancel') }}
              </n-button>
              <n-button v-if="deleteAccountStep === 'agreement'" secondary :disabled="deleteAccountSaving"
                @click="deleteAccountStep = 'password'">
                {{ t('UserSecurity:DeleteAccountBack') }}
              </n-button>
              <n-button v-if="deleteAccountStep === 'password'" type="error"
                :disabled="!deleteAccountPassword.trim()" @click="goToDeleteAgreement">
                {{ t('UserSecurity:DeleteAccountNext') }}
              </n-button>
              <n-button v-else type="error" :loading="deleteAccountSaving" :disabled="!deleteAccountPhraseMatches"
                @click="submitDeleteAccount">
                {{ t('UserSecurity:DeleteAccountConfirm') }}
              </n-button>
            </n-space>
          </template>
        </n-card>
      </n-modal>
    </n-spin>
  </div>
</template>
<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { FormInst, FormItemInst, FormRules } from 'naive-ui'
import { useDiscreteApi } from '@/hooks/discreteApi'
import LazyImage from '@/components/LazyImage.vue'
import { t } from '@/i18n'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import { userApi } from '@/modules/authorization/api/user'
import type {
  AuthenticatorSetup,
  ExternalLoginBinding,
  ExternalLoginProvider,
  LoginSession,
  TwoFactorMethod,
  TwoFactorStatus,
} from '@/modules/authorization/api/user/userModels'
import { useUserStore } from '@/stores/user'
import { handleError } from '@/utils/errorHandler'
import { scrollToFirstFormError } from '@/utils/formErrors'
import {
  consumePendingExternalAuthNotice,
  markExternalAuthOrigin,
} from '@/modules/authorization/utils/externalAuth'
import QRCode from 'qrcode'

const loading = ref(false)
const userStore = useUserStore()
const passwordSaving = ref(false)
const showPasswordDialog = ref(false)
const showDeleteAccountDialog = ref(false)
const deleteAccountStep = ref<'password' | 'agreement'>('password')
const deleteAccountPassword = ref('')
const deleteAccountPhraseInput = ref('')
const deleteAccountSaving = ref(false)
const passwordFormRef = ref<FormInst>()
const confirmPasswordItemRef = ref<FormItemInst>()
const twoFactorStatus = ref<TwoFactorStatus>()
const passwordForm = ref({ currentPassword: '', newPassword: '', confirmPassword: '' })
const showSecurityDialog = ref(false)
const securitySaving = ref(false)
const securityAction = ref<'enable' | 'disable'>('enable')
const securityMethod = ref<Exclude<TwoFactorMethod, 'recovery'>>('authenticator')
const securityPassword = ref('')
const securityCode = ref('')
const authenticatorSetup = ref<AuthenticatorSetup>()
const authenticatorQrCode = ref('')
const authenticatorQrCodeFailed = ref(false)
const showAuthenticatorKey = ref(false)
const messageSetupChallengeId = ref('')
const recoveryCodes = ref<string[]>([])
const loginSessions = ref<LoginSession[]>([])
const sessionsLoading = ref(false)
const revokingSessionId = ref('')
const externalProviders = ref<ExternalLoginProvider[]>([])
const externalBindings = ref<ExternalLoginBinding[]>([])
const bindingProvider = ref('')
const factorItems = computed(() => [
  {
    method: 'authenticator' as const,
    title: t('UserSecurity:Authenticator'),
    description: t('UserSecurity:AuthenticatorDescription'),
    enabled: twoFactorStatus.value?.authenticatorEnabled ?? false,
  },
  {
    method: 'email' as const,
    title: t('UserSecurity:Email'),
    description:
      twoFactorStatus.value?.maskedEmail || t('UserSecurity:EmailUnavailable'),
    enabled: twoFactorStatus.value?.emailEnabled ?? false,
  },
  {
    method: 'sms' as const,
    title: t('UserSecurity:Sms'),
    description:
      twoFactorStatus.value?.maskedPhone || t('UserSecurity:SmsUnavailable'),
    enabled: twoFactorStatus.value?.smsEnabled ?? false,
  },
])
const securityDialogTitle = computed(() =>
  securityAction.value === 'disable'
    ? t('UserSecurity:DisableTitle')
    : factorItems.value.find((item) => item.method === securityMethod.value)?.title || '',
)
const selectedFactorDescription = computed(
  () => factorItems.value.find((item) => item.method === securityMethod.value)?.description || '',
)
const securitySubmitLabel = computed(() => {
  if (securityAction.value === 'disable') return t('UserSecurity:Disable')
  if (securityMethod.value === 'authenticator' && !authenticatorSetup.value) {
    return t('UserSecurity:StartSetup')
  }
  if (
    (securityMethod.value === 'email' || securityMethod.value === 'sms') &&
    !messageSetupChallengeId.value
  ) {
    return t('UserSecurity:SendCode')
  }
  return t('UserSecurity:VerifyEnable')
})

// 与后端 SecurityOptions 默认密码策略一致：至少 8 位，且包含大小写字母、数字和特殊字符。
const passwordSignals = computed(() => {
  const value = passwordForm.value.newPassword
  return [
    { key: 'length', label: t('UserSecurity:PasswordRuleLength'), met: value.length >= 8 },
    { key: 'uppercase', label: t('UserSecurity:PasswordRuleUppercase'), met: /[A-Z]/.test(value) },
    { key: 'lowercase', label: t('UserSecurity:PasswordRuleLowercase'), met: /[a-z]/.test(value) },
    { key: 'digit', label: t('UserSecurity:PasswordRuleDigit'), met: /\d/.test(value) },
    {
      key: 'special',
      label: t('UserSecurity:PasswordRuleSpecial'),
      met: /[!@#$%^&*()_+\-=[\]{};':"\\|,.<>\/?]/.test(value),
    },
  ]
})

const passwordStrength = computed(() => {
  const met = passwordSignals.value.filter((signal) => signal.met).length
  if (met === passwordSignals.value.length) {
    return { met, level: 'strong', label: t('UserSecurity:StrengthStrong') }
  }
  if (met >= 3) {
    return { met, level: 'medium', label: t('UserSecurity:StrengthMedium') }
  }
  return { met, level: 'weak', label: t('UserSecurity:StrengthWeak') }
})

const passwordRules = computed<FormRules>(() => ({
  currentPassword: [
    { required: true, message: t('UserSecurity:CurrentPasswordRequired'), trigger: 'blur' },
  ],
  newPassword: [
    { required: true, message: t('UserSecurity:NewPasswordRequired'), trigger: 'blur' },
    {
      validator: () => {
        if (!passwordForm.value.newPassword) return true
        if (!passwordSignals.value.every((signal) => signal.met)) {
          return new Error(t('UserSecurity:PasswordRuleInvalid'))
        }
        if (passwordForm.value.newPassword === passwordForm.value.currentPassword) {
          return new Error(t('UserSecurity:PasswordSameAsCurrent'))
        }
        return true
      },
      trigger: 'blur',
    },
  ],
  confirmPassword: [
    { required: true, message: t('UserSecurity:ConfirmPasswordRequired'), trigger: 'blur' },
    {
      validator: (_rule, value: string) =>
        !value ||
        value === passwordForm.value.newPassword ||
        new Error(t('UserSecurity:PasswordMismatch')),
      trigger: ['input', 'blur'],
    },
  ],
}))

watch(
  () => passwordForm.value.newPassword,
  () => {
    if (!passwordForm.value.confirmPassword) return
    void confirmPasswordItemRef.value?.validate({ trigger: 'input' }).catch(() => {})
  },
)

const loadTwoFactorStatus = async () => {
  try {
    twoFactorStatus.value = await userApi.getTwoFactorStatus()
  } catch {
    useDiscreteApi.message.error(t('UserSecurity:StatusLoadFailed'))
  }
}

const openPasswordDialog = () => {
  passwordForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
  showPasswordDialog.value = true
}

const closePasswordDialog = () => {
  if (passwordSaving.value) return
  showPasswordDialog.value = false
  passwordForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
}

const deleteAccountPhrase = computed(() => t('UserSecurity:DeleteAccountPhrase'))
const deleteAccountPhraseMatches = computed(
  () => deleteAccountPhraseInput.value.trim() === deleteAccountPhrase.value,
)

const openDeleteAccountDialog = () => {
  deleteAccountStep.value = 'password'
  deleteAccountPassword.value = ''
  deleteAccountPhraseInput.value = ''
  showDeleteAccountDialog.value = true
}

const closeDeleteAccountDialog = () => {
  if (deleteAccountSaving.value) return
  showDeleteAccountDialog.value = false
  deleteAccountStep.value = 'password'
  deleteAccountPassword.value = ''
  deleteAccountPhraseInput.value = ''
}

const goToDeleteAgreement = () => {
  if (!deleteAccountPassword.value.trim()) return
  deleteAccountStep.value = 'agreement'
}

const submitDeleteAccount = async () => {
  if (!deleteAccountPhraseMatches.value || !deleteAccountPassword.value) return
  deleteAccountSaving.value = true
  try {
    await userApi.deleteMyAccount(deleteAccountPassword.value)
    showDeleteAccountDialog.value = false
    useDiscreteApi.message.success(t('UserSecurity:DeleteAccountSuccess'))
    await userStore.logout()
    window.location.assign('/login')
  } catch (error) {
    handleError(error, t('UserSecurity:DeleteAccountFailed'), { logPrefix: 'UserSecurity' })
  } finally {
    deleteAccountSaving.value = false
  }
}

const submitPasswordChange = async () => {
  if (!passwordFormRef.value) return

  try {
    await passwordFormRef.value.validate()
  } catch {
    void scrollToFirstFormError(passwordFormRef.value)
    return
  }

  passwordSaving.value = true
  try {
    await userApi.changePassword(
      passwordForm.value.currentPassword,
      passwordForm.value.newPassword,
      passwordForm.value.confirmPassword,
    )
    showPasswordDialog.value = false
    passwordForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
    useDiscreteApi.message.success(t('UserSecurity:PasswordChanged'))
    await userStore.logout()
    window.location.assign('/login')
  } catch (error) {
    handleError(error, t('UserSecurity:PasswordChangeFailed'), { logPrefix: 'UserSecurity' })
  } finally {
    passwordSaving.value = false
  }
}

const openFactorDialog = (
  method: Exclude<TwoFactorMethod, 'recovery'>,
  enabled: boolean,
) => {
  securityMethod.value = method
  securityAction.value = enabled ? 'disable' : 'enable'
  showSecurityDialog.value = true
}

const closeSecurityDialog = () => {
  showSecurityDialog.value = false
  securityPassword.value = ''
  securityCode.value = ''
  authenticatorSetup.value = undefined
  authenticatorQrCode.value = ''
  authenticatorQrCodeFailed.value = false
  showAuthenticatorKey.value = false
  messageSetupChallengeId.value = ''
  recoveryCodes.value = []
}

const validateSecurityAction = () => {
  const needsPassword =
    securityAction.value === 'disable' ||
    (securityMethod.value === 'authenticator' && !authenticatorSetup.value) ||
    (securityMethod.value !== 'authenticator' && !messageSetupChallengeId.value)
  if (needsPassword && !securityPassword.value) {
    useDiscreteApi.message.warning(t('UserSecurity:CurrentPasswordRequired'))
    return false
  }

  const needsCode =
    (securityMethod.value === 'authenticator' && Boolean(authenticatorSetup.value)) ||
    (securityMethod.value !== 'authenticator' && Boolean(messageSetupChallengeId.value))
  if (needsCode && !securityCode.value.trim()) {
    useDiscreteApi.message.warning(t('UserSecurity:VerificationCodeRequired'))
    return false
  }
  return true
}

const submitSecurityAction = async () => {
  if (!validateSecurityAction()) return
  securitySaving.value = true
  try {
    if (securityAction.value === 'disable') {
      await userApi.disableTwoFactor(securityMethod.value, securityPassword.value)
      useDiscreteApi.message.success(t('UserSecurity:DisabledSuccess'))
      closeSecurityDialog()
      await loadTwoFactorStatus()
      return
    }

    if (securityMethod.value === 'authenticator') {
      if (!authenticatorSetup.value) {
        authenticatorSetup.value = await userApi.beginAuthenticatorSetup(securityPassword.value)
        try {
          authenticatorQrCode.value = await QRCode.toDataURL(
            authenticatorSetup.value.authenticatorUri,
            {
              width: 220,
              margin: 1,
              errorCorrectionLevel: 'M',
              color: { dark: '#102a2aff', light: '#ffffffff' },
            },
          )
        } catch {
          authenticatorQrCodeFailed.value = true
          showAuthenticatorKey.value = true
          useDiscreteApi.message.warning(t('UserSecurity:QrCodeFailed'))
        }
        return
      }
      const result = await userApi.confirmAuthenticatorSetup(securityCode.value)
      recoveryCodes.value = result.recoveryCodes
      await loadTwoFactorStatus()
      return
    }

    if (!messageSetupChallengeId.value) {
      const result = await userApi.requestMessageTwoFactorSetup(
        securityMethod.value,
        securityPassword.value,
      )
      messageSetupChallengeId.value = result.challengeId
      useDiscreteApi.message.success(t('Auth:CodeSent'))
      return
    }

    await userApi.confirmMessageTwoFactorSetup(messageSetupChallengeId.value, securityCode.value)
    useDiscreteApi.message.success(t('UserSecurity:EnabledSuccess'))
    closeSecurityDialog()
    await loadTwoFactorStatus()
  } catch {
    useDiscreteApi.message.error(t('UserSecurity:ActionFailed'))
  } finally {
    securitySaving.value = false
  }
}

const copyAuthenticatorKey = async () => {
  if (!authenticatorSetup.value) return
  await navigator.clipboard.writeText(authenticatorSetup.value.sharedKey)
  useDiscreteApi.message.success(t('UserSecurity:ManualKeyCopied'))
}

const loadLoginSessions = async () => {
  sessionsLoading.value = true
  try {
    loginSessions.value = await userApi.getMyLoginSessions()
  } catch {
    useDiscreteApi.message.error(t('LoginSessions:LoadFailed'))
  } finally {
    sessionsLoading.value = false
  }
}

const boundBinding = (provider: string) =>
  externalBindings.value.find((item) => item.provider === provider)

const loadExternalLoginBindings = async () => {
  try {
    const [providers, bindings] = await Promise.all([
      userApi.getExternalLoginProviders(),
      userApi.getMyExternalLoginBindings(),
    ])
    externalProviders.value = providers.filter((provider) => !!provider.provider)
    externalBindings.value = bindings
  } catch {
    useDiscreteApi.message.error(t('UserProfile:ExternalBindingLoadFailed'))
  }
}

const bindExternalLogin = async (provider: string) => {
  bindingProvider.value = provider
  try {
    const redirect = await userApi.getMyBindingRedirect(provider)
    if (!redirect.redirectUrl) {
      throw new Error(t('UserProfile:ExternalBindingBindFailed'))
    }
    // 记录发起页，回调失败时回到账户安全页展示错误，而不是停留在回调中间页。
    markExternalAuthOrigin('security')
    window.location.assign(redirect.redirectUrl)
  } catch {
    useDiscreteApi.message.error(t('UserProfile:ExternalBindingBindFailed'))
    bindingProvider.value = ''
  }
}

const unbindExternalLogin = async (provider: string) => {
  bindingProvider.value = provider
  try {
    await userApi.deleteMyBinding(provider)
    useDiscreteApi.message.success(t('UserProfile:ExternalBindingUnboundSuccess'))
    await loadExternalLoginBindings()
  } catch {
    useDiscreteApi.message.error(t('UserProfile:ExternalBindingUnbindFailed'))
  } finally {
    bindingProvider.value = ''
  }
}

const formatSessionTime = (value: string) =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))

const revokeLoginSession = async (session: LoginSession) => {
  revokingSessionId.value = session.id
  try {
    await userApi.revokeMyLoginSession(session.id)
    if (session.isCurrent) {
      await userStore.logout()
      window.location.assign('/login')
      return
    }
    useDiscreteApi.message.success(t('LoginSessions:Revoked'))
    await loadLoginSessions()
  } catch {
    useDiscreteApi.message.error(t('LoginSessions:RevokeFailed'))
  } finally {
    revokingSessionId.value = ''
  }
}

onMounted(() => {
  const pendingExternalAuthNotice = consumePendingExternalAuthNotice()
  if (pendingExternalAuthNotice) {
    useDiscreteApi.message.error(pendingExternalAuthNotice)
  }

  void loadTwoFactorStatus()
  void loadLoginSessions()
  void loadExternalLoginBindings()
})
</script>

<style scoped>
.authz-password-dialog {
  width: min(480px, calc(100vw - 32px));
}

.authz-delete-account-dialog {
  width: min(520px, calc(100vw - 32px));
}

.authz-delete-agreement {
  margin-bottom: 16px;
  padding: 14px 16px;
  border: 2px dashed var(--po-line-mid);
  border-radius: var(--po-r-sm);
  background: var(--po-bg-deep);
}

.authz-delete-agreement p {
  margin: 0 0 8px;
}

.authz-delete-agreement p:last-child {
  margin-bottom: 0;
}

.authz-delete-agreement__danger {
  color: var(--po-danger);
  font-weight: var(--po-weight-strong);
}

.authz-password-strength {
  margin: 2px 0 18px;
  padding: 12px 14px;
  border: 2px dashed var(--po-line-mid);
  border-radius: var(--po-r-sm);
  background: var(--po-bg-deep);
}

.authz-password-strength__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  font-size: 12.5px;
  font-weight: var(--po-weight-strong);
}

.authz-password-strength__label[data-level='weak'] {
  color: var(--po-danger);
}

.authz-password-strength__label[data-level='medium'] {
  color: var(--po-warn);
}

.authz-password-strength__label[data-level='strong'] {
  color: var(--po-good);
}

.authz-password-strength__meter {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 6px;
  margin: 8px 0 10px;
}

.authz-password-strength__segment {
  height: 8px;
  border: 2px solid var(--po-line-mid);
  border-radius: 999px;
}

.authz-password-strength__segment.is-filled {
  border-color: var(--po-ink);
}

.authz-password-strength__segment.is-filled.is-weak {
  background: var(--po-danger);
}

.authz-password-strength__segment.is-filled.is-medium {
  background: var(--po-warn);
}

.authz-password-strength__segment.is-filled.is-strong {
  background: var(--po-good);
}

.authz-password-rules {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 14px;
  margin: 0;
  padding: 0;
  list-style: none;
  color: var(--po-ink-soft);
  font-size: 12px;
}

.authz-password-rule {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.authz-password-rule::before {
  content: '';
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--po-line-mid);
}

.authz-password-rule.is-met {
  color: var(--po-ink);
}

.authz-password-rule.is-met::before {
  background: var(--po-good);
}

.authz-factor-row,
.authz-session-row {
  align-items: center;
}

.authz-factor-main,
.authz-session-main {
  flex: 1 1 auto;
  min-width: 0;
}

/* 会话卡填满左列高度时，行内容保持从顶部排列，不被网格拉伸居中 */
.authz-session-card {
  align-content: start;
}

.authz-factor-title {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.authz-recovery-status {
  margin: 12px 0 0;
}

.authz-security-dialog {
  width: min(560px, calc(100vw - 32px));
}

.authz-editor-hint {
  margin: 0 0 14px;
}

.authz-authenticator-setup {
  display: grid;
  justify-items: center;
  gap: 12px;
  margin-bottom: 18px;
  text-align: center;
}

.authz-qr-frame {
  display: grid;
  place-items: center;
  width: 236px;
  height: 236px;
  padding: 8px;
  box-sizing: border-box;
  border: 2px solid var(--po-line-mid);
  border-radius: var(--po-r-md, 12px);
  /* 二维码需保持纯白背景以保证可扫描性 */
  background: #fff;
}

.authz-qr-frame img {
  display: block;
}

.authz-manual-key {
  width: 100%;
  padding: 14px;
  text-align: left;
}

.authz-shared-key {
  margin: 10px 0;
  padding: 12px;
  overflow-wrap: anywhere;
  border: 1px dashed var(--po-line-mid);
  border-radius: var(--po-r-md, 10px);
  font-family: var(--po-font-mono);
  letter-spacing: 0.08em;
  text-align: center;
}

.authz-recovery-codes {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  margin-top: 10px;
}

.authz-recovery-codes code {
  padding: 6px;
  border-radius: 6px;
  background: var(--po-bg-deep);
  text-align: center;
}

@media (max-width: 640px) {
  .authz-factor-row,
  .authz-session-row {
    align-items: stretch;
    flex-direction: column;
  }

  .authz-recovery-codes {
    grid-template-columns: 1fr;
  }
}
</style>

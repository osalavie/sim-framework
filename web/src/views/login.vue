<template>
  <main class="login-page" :class="{ 'is-centered-auth': centeredAuth }">
    <div class="login-toolbar">
      <ThemeToggle />
      <LocaleSwitcher />
    </div>

    <RouterLink
      v-if="authHomeTarget"
      class="auth-home-brand"
      :to="authHomeTarget"
      :aria-label="authHomeLabel || undefined"
    >
      <span class="auth-home-brand__mark" aria-hidden="true">
        <BrandLogo :size="22" />
      </span>
      <span class="auth-home-brand__text">
        <strong>{{ siteName }}</strong>
      </span>
    </RouterLink>

    <AuthBrandPanel v-if="!centeredAuth" :headline="t('Auth:BrandHeadline')" />

    <section class="login-panel" :aria-label="authPanelLabel">
      <div class="login-shell">
        <div class="login-card">
          <div class="auth-card-stack">
            <div
              class="auth-card"
              :class="registerCardClass"
              :aria-hidden="authMode !== 'register'"
            >
              <header class="login-header">
                <span class="po-kicker">{{ registerKicker }}</span>
                <h2>{{ t('Auth:RegisterTitle') }}</h2>
                <p>{{ registerHint }}</p>
              </header>

              <n-form
                ref="registerFormRef"
                :model="registerForm"
                :rules="registerRules"
                size="large"
                label-placement="top"
                @submit.prevent="handleRegister"
              >
                <div class="auth-form-scroll">
                  <n-form-item path="userName" :label="t('Field:Username')">
                    <n-input
                      v-model:value="registerForm.userName"
                      :placeholder="t('Auth:UsernamePlaceholder')"
                      :input-props="{
                        name: 'username',
                        autocomplete: 'username',
                        'aria-label': t('Field:Username'),
                      }"
                      clearable
                    />
                  </n-form-item>

                  <n-form-item path="nickName" :label="t('Field:Nickname')">
                    <n-input
                      v-model:value="registerForm.nickName"
                      :placeholder="t('Auth:NicknamePlaceholder')"
                      :maxlength="20"
                      show-count
                      :input-props="{
                        name: 'nickname',
                        autocomplete: 'off',
                        'aria-label': t('Field:Nickname'),
                      }"
                    />
                  </n-form-item>

                  <n-form-item path="email" :label="t('Auth:EmailOptionalLabel')">
                    <n-input
                      v-model:value="registerForm.email"
                      placeholder="name@example.com"
                      :input-props="{
                        name: 'email',
                        autocomplete: 'email',
                        'aria-label': t('Auth:EmailOptionalLabel'),
                      }"
                      clearable
                    />
                  </n-form-item>

                  <n-form-item path="password" :label="t('Field:Password')">
                    <n-input
                      v-model:value="registerForm.password"
                      type="password"
                      :placeholder="t('Auth:PasswordPlaceholder')"
                      :input-props="{
                        name: 'new-password',
                        autocomplete: 'new-password',
                        'aria-label': t('Field:Password'),
                      }"
                      show-password-on="click"
                      clearable
                    />
                  </n-form-item>
                  <p class="auth-field-hint">{{ t('Auth:PasswordPolicyHint') }}</p>

                  <n-form-item path="confirmPassword" :label="t('Auth:ConfirmPassword')">
                    <n-input
                      v-model:value="registerForm.confirmPassword"
                      type="password"
                      :placeholder="t('Auth:ConfirmPasswordRequired')"
                      :input-props="{
                        name: 'confirm-password',
                        autocomplete: 'new-password',
                        'aria-label': t('Auth:ConfirmPassword'),
                      }"
                      show-password-on="click"
                      clearable
                    />
                  </n-form-item>
                </div>

                <n-button
                  class="login-submit"
                  type="primary"
                  attr-type="submit"
                  block
                  :loading="registerLoading"
                >
                  {{ registerLoading ? t('Auth:Registering') : t('Auth:RegisterSubmit') }}
                </n-button>
              </n-form>

              <p class="auth-switch-hint">
                {{ t('Auth:HaveAccount') }}
                <n-button text type="primary" @click="goLogin">{{
                  t('Auth:BackToSignIn')
                }}</n-button>
              </p>
            </div>

            <div class="auth-card" :class="loginCardClass" :aria-hidden="authMode !== 'login'">
              <header v-if="loginStep === 'credentials'" class="login-header">
                <span class="po-kicker">{{ signInKicker }}</span>
                <h2>{{ t('Auth:Welcome') }}</h2>
                <p>{{ signInHint }}</p>
              </header>

              <n-form
                v-if="loginStep === 'credentials'"
                ref="formRef"
                :model="formData"
                :rules="rules"
                size="large"
                label-placement="top"
                @submit.prevent="handleLogin"
              >
                <n-form-item path="userName" :label="t('Field:Username')">
                  <n-input
                    v-model:value="formData.userName"
                    :placeholder="t('Auth:UsernamePlaceholder')"
                    :input-props="{
                      name: 'username',
                      autocomplete: 'username',
                      'aria-label': t('Field:Username'),
                    }"
                    clearable
                  >
                    <template #prefix>
                      <n-icon :component="PersonOutline" />
                    </template>
                  </n-input>
                </n-form-item>

                <n-form-item path="password" :label="t('Field:Password')">
                  <n-input
                    v-model:value="formData.password"
                    type="password"
                    :placeholder="t('Auth:PasswordPlaceholder')"
                    :input-props="{
                      name: 'password',
                      autocomplete: 'current-password',
                      'aria-label': t('Field:Password'),
                    }"
                    show-password-on="click"
                    clearable
                  >
                    <template #prefix>
                      <n-icon :component="LockClosedOutline" />
                    </template>
                  </n-input>
                </n-form-item>

                <div class="form-options">
                  <n-checkbox v-model:checked="formData.remember">{{
                    t('Auth:Remember')
                  }}</n-checkbox>
                  <n-button text type="primary" @click="showResetDialog = true">
                    {{ t('Auth:ForgotPassword') }}
                  </n-button>
                </div>

                <n-button
                  class="login-submit"
                  type="primary"
                  attr-type="submit"
                  block
                  :loading="loading"
                >
                  {{ loading ? t('Auth:SigningIn') : t('Auth:SignIn') }}
                </n-button>
              </n-form>

              <div
                v-if="loginStep === 'credentials' && externalProviders.length > 0"
                class="external-login"
              >
                <div class="external-divider" aria-hidden="true">
                  {{ t('Auth:OrContinueWith') }}
                </div>
                <div class="external-actions">
                  <n-tooltip
                    v-for="provider in externalProviders"
                    :key="provider.provider"
                    placement="top"
                  >
                    <template #trigger>
                      <n-button
                        circle
                        secondary
                        size="large"
                        :aria-label="provider.displayName"
                        :disabled="externalLoginProcessing"
                        @click="handleExternalLogin(provider.provider)"
                      >
                        <template #icon>
                          <n-icon :component="providerIcon(provider.provider)" />
                        </template>
                      </n-button>
                    </template>
                    {{ provider.displayName }}
                  </n-tooltip>
                </div>
              </div>

              <p v-if="loginStep === 'credentials' && registrationEnabled" class="auth-switch-hint">
                {{ t('Auth:NoAccount') }}
                <n-button text type="primary" @click="goRegister">{{
                  t('Auth:Register')
                }}</n-button>
              </p>

              <div v-if="loginStep === 'external-choice'" class="factor-step">
                <div class="factor-heading">
                  <span class="po-kicker">{{
                    t('Auth:ExternalAccountKicker', { provider: externalProviderName })
                  }}</span>
                  <h3>{{ t('Auth:ExternalAccountTitle') }}</h3>
                  <p>{{ externalAccountHint }}</p>
                </div>
                <div class="external-choice">
                  <n-button class="login-submit" type="primary" block @click="chooseExternalLink">
                    {{ t('Auth:LinkExistingAccount') }}
                  </n-button>
                  <n-button
                    v-if="externalRegisterAllowed"
                    class="login-submit"
                    block
                    secondary
                    @click="chooseExternalRegister"
                  >
                    {{ t('Auth:RegisterNewAccount') }}
                  </n-button>
                  <p v-else class="auth-field-hint">{{ t('Auth:ExternalRegistrationClosed') }}</p>
                </div>
                <n-button text class="back-button" @click="backToExternalLogin">
                  {{ t('Auth:BackToPassword') }}
                </n-button>
              </div>

              <n-form
                v-if="loginStep === 'external-link'"
                ref="externalLinkFormRef"
                :model="externalLinkForm"
                :rules="externalLinkRules"
                size="large"
                label-placement="top"
                @submit.prevent="handleExternalLink"
              >
                <div class="step-heading">
                  <h3>{{ t('Auth:LinkExistingTitle') }}</h3>
                  <n-button text class="step-back" @click="backToExternalChoice">
                    <template #icon>
                      <n-icon :component="ArrowBackOutline" />
                    </template>
                    {{ t('Auth:Back') }}
                  </n-button>
                </div>
                <p class="step-hint">{{ t('Auth:LinkExistingHint') }}</p>

                <n-form-item path="userName" :label="t('Field:Username')">
                  <n-input
                    v-model:value="externalLinkForm.userName"
                    :placeholder="t('Auth:UsernamePlaceholder')"
                    :input-props="{ name: 'username', autocomplete: 'username' }"
                    clearable
                  />
                </n-form-item>

                <n-form-item path="password" :label="t('Field:Password')">
                  <n-input
                    v-model:value="externalLinkForm.password"
                    type="password"
                    :placeholder="t('Auth:PasswordPlaceholder')"
                    :input-props="{ name: 'password', autocomplete: 'current-password' }"
                    show-password-on="click"
                    clearable
                  />
                </n-form-item>

                <n-button
                  class="login-submit"
                  type="primary"
                  attr-type="submit"
                  block
                  :loading="externalLinkLoading"
                >
                  {{ t('Auth:LinkAndSignIn') }}
                </n-button>
              </n-form>

              <n-form
                v-if="loginStep === 'external-register'"
                ref="externalRegisterFormRef"
                :model="externalRegisterForm"
                :rules="externalRegisterRules"
                size="large"
                label-placement="top"
                @submit.prevent="handleExternalRegister"
              >
                <div class="step-heading">
                  <h3>{{ t('Auth:CompleteExternalRegistration') }}</h3>
                  <n-button text class="step-back" @click="backToExternalChoice">
                    <template #icon>
                      <n-icon :component="ArrowBackOutline" />
                    </template>
                    {{ t('Auth:Back') }}
                  </n-button>
                </div>

                <div class="auth-form-scroll">
                  <n-form-item path="userName" :label="t('Field:Username')">
                    <n-input
                      v-model:value="externalRegisterForm.userName"
                      :placeholder="t('Auth:UsernamePlaceholder')"
                      :input-props="{ autocomplete: 'username' }"
                      clearable
                    />
                  </n-form-item>

                  <n-form-item path="nickName" :label="t('Field:Nickname')">
                    <n-input
                      v-model:value="externalRegisterForm.nickName"
                      :maxlength="20"
                      show-count
                    />
                  </n-form-item>

                  <n-form-item path="email" :label="t('Auth:EmailOptionalLabel')">
                    <n-input
                      v-model:value="externalRegisterForm.email"
                      placeholder="name@example.com"
                      :input-props="{ autocomplete: 'email' }"
                      clearable
                    />
                  </n-form-item>

                  <n-form-item path="phone" :label="t('Auth:PhoneOptionalLabel')">
                    <n-input
                      v-model:value="externalRegisterForm.phone"
                      :maxlength="20"
                      :input-props="{ autocomplete: 'tel' }"
                      clearable
                    />
                  </n-form-item>

                  <n-form-item path="password" :label="t('Field:Password')">
                    <n-input
                      v-model:value="externalRegisterForm.password"
                      type="password"
                      :input-props="{ autocomplete: 'new-password' }"
                      show-password-on="click"
                      clearable
                    />
                  </n-form-item>

                  <n-form-item path="confirmPassword" :label="t('Auth:ConfirmPassword')">
                    <n-input
                      v-model:value="externalRegisterForm.confirmPassword"
                      type="password"
                      :input-props="{ autocomplete: 'new-password' }"
                      show-password-on="click"
                      clearable
                    />
                  </n-form-item>
                </div>

                <n-button
                  class="login-submit"
                  type="primary"
                  attr-type="submit"
                  block
                  :loading="externalRegisterLoading"
                >
                  {{ t('Auth:RegisterNow') }}
                </n-button>
              </n-form>

              <div v-if="loginStep === 'factor'" class="factor-step">
                <div class="factor-heading">
                  <span class="po-kicker">{{ t('Auth:TwoFactorKicker') }}</span>
                  <h3>{{ t('Auth:TwoFactorTitle') }}</h3>
                  <p>{{ t('Auth:TwoFactorHint') }}</p>
                </div>
                <n-radio-group v-model:value="factorMethod" class="factor-methods">
                  <n-radio-button v-for="method in factorMethods" :key="method" :value="method">
                    {{ factorMethodLabel(method) }}
                  </n-radio-button>
                </n-radio-group>
                <n-input
                  v-model:value="factorCode"
                  :placeholder="t('Auth:VerificationCodePlaceholder')"
                  :maxlength="16"
                  :input-props="{ autocomplete: 'one-time-code', inputmode: 'numeric' }"
                  size="large"
                  @keyup.enter="handleFactorLogin"
                />
                <div class="factor-actions">
                  <n-button
                    v-if="factorMethod === 'email' || factorMethod === 'sms'"
                    secondary
                    :loading="sendingCode"
                    @click="sendFactorCode"
                  >
                    {{ t('Auth:ResendCode') }}
                  </n-button>
                  <n-button type="primary" :loading="loading" @click="handleFactorLogin">
                    {{ t('Auth:VerifyAndSignIn') }}
                  </n-button>
                </div>
                <n-button text class="back-button" @click="resetLoginStep">
                  {{ t('Auth:BackToPassword') }}
                </n-button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <n-modal v-model:show="showResetDialog">
        <n-card
          class="reset-dialog"
          :title="t('Auth:ResetPasswordTitle')"
          closable
          role="dialog"
          aria-modal="true"
          @close="closeResetDialog"
        >
          <n-form ref="resetFormRef" :model="resetForm" :rules="resetRules" label-placement="top">
            <template v-if="resetStep === 'request'">
              <n-form-item :label="t('Auth:Account')" path="account" required>
                <n-input
                  v-model:value="resetForm.account"
                  :placeholder="t('Auth:AccountPlaceholder')"
                />
              </n-form-item>
              <n-form-item :label="t('Auth:RecoveryMethod')" path="method" required>
                <n-select v-model:value="resetForm.method" :options="recoveryMethodOptions" />
              </n-form-item>
              <n-alert type="info" :show-icon="false">{{ t('Auth:ResetPrivacyHint') }}</n-alert>
            </template>
            <template v-else>
              <n-form-item :label="t('Auth:VerificationCode')" path="code" required>
                <n-input
                  v-model:value="resetForm.code"
                  :maxlength="16"
                  :input-props="{ autocomplete: 'one-time-code' }"
                />
              </n-form-item>
              <n-form-item :label="t('Auth:NewPassword')" path="newPassword" required>
                <n-input
                  v-model:value="resetForm.newPassword"
                  type="password"
                  show-password-on="click"
                  :input-props="{ autocomplete: 'new-password' }"
                />
              </n-form-item>
              <n-form-item :label="t('Auth:ConfirmPassword')" path="confirmPassword" required>
                <n-input
                  v-model:value="resetForm.confirmPassword"
                  type="password"
                  show-password-on="click"
                  :input-props="{ autocomplete: 'new-password' }"
                />
              </n-form-item>
            </template>
          </n-form>
          <template #footer>
            <div class="dialog-actions">
              <n-button @click="closeResetDialog">{{ t('Common:Cancel') }}</n-button>
              <n-button type="primary" :loading="resetLoading" @click="handlePasswordReset">
                {{ resetStep === 'request' ? t('Auth:SendCode') : t('Auth:ResetPassword') }}
              </n-button>
            </div>
          </template>
        </n-card>
      </n-modal>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { Component } from 'vue'
import type { FormInst, FormItemRule, FormRules } from 'naive-ui'
import {
  ArrowBackOutline,
  ChatbubblesOutline,
  LockClosedOutline,
  LogInOutline,
  LogoGoogle,
  LogoMicrosoft,
  LogoSteam,
  LogoWechat,
  PersonOutline,
} from '@vicons/ionicons5'
import { useUserStore } from '@/stores/user'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { routeConfig } from '@/config/routeConfig'
import { siteDefaults, siteName } from '@/config/branding'
import { refreshSiteConfiguration, t } from '@/i18n'
import { AuthBrandPanel } from '@/components/AuthBrandPanel'
import { BrandLogo } from '@/components/BrandLogo'
import { LocaleSwitcher } from '@/components/LocaleSwitcher'
import { ThemeToggle } from '@/components/ThemeToggle'
import { handleError } from '@/utils/errorHandler'
import { scrollToFirstFormError } from '@/utils/formErrors'
import {
  clearRememberedLoginUserName,
  getRememberedLoginUserName,
  saveRememberedLoginUserName,
} from '@/utils/loginRemember'
import { userApi } from '@/modules/authorization/api/user'
import {
  consumePendingExternalAuthNotice,
  consumePendingExternalAuthResult,
  markExternalAuthOrigin,
} from '@/modules/authorization/utils/externalAuth'
import type {
  ExternalLoginProvider,
  LoginResponse,
  TwoFactorMethod,
} from '@/modules/authorization/api/user/userModels'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()

const metaString = (key: string): string | undefined => {
  const value = route.meta[key]
  return typeof value === 'string' && value ? value : undefined
}

const centeredAuth = computed(() => metaString('authLayout') === 'centered-brand')
const authMode = computed<'login' | 'register'>(() => {
  const configured = metaString('authMode')
  if (configured === 'register' || configured === 'login') return configured
  return route.name === 'Register' ? 'register' : 'login'
})
const authCopy = (key: string): string => {
  const namespace = metaString('authCopyNamespace')
  return t(namespace ? `${namespace}:${key}` : `Auth:${key}`)
}
const signInKicker = computed(() => authCopy('SignInKicker'))
const signInHint = computed(() => authCopy('SignInHint'))
const registerKicker = computed(() => authCopy('RegisterKicker'))
const registerHint = computed(() => authCopy('RegisterHint'))
const authHomeRoute = computed(() => metaString('authHomeRoute'))
const authHomeTarget = computed(() => (authHomeRoute.value ? { name: authHomeRoute.value } : null))
const authHomeLabel = computed(() => {
  const key = metaString('authHomeLabelKey')
  return key ? t(key) : ''
})
const loginCardClass = computed(() =>
  authMode.value === 'login' ? 'is-active' : 'is-hidden is-hidden--left',
)
const registerCardClass = computed(() =>
  authMode.value === 'register' ? 'is-active' : 'is-hidden is-hidden--right',
)
const registrationEnabled = siteDefaults.registrationEnabled

const formRef = ref<FormInst>()
const loading = ref(false)
const sendingCode = ref(false)
const loginStep = ref<
  'credentials' | 'factor' | 'external-choice' | 'external-link' | 'external-register'
>('credentials')
const factorChallengeId = ref('')
const factorMethods = ref<TwoFactorMethod[]>([])
const factorMethod = ref<TwoFactorMethod>('authenticator')
const factorCode = ref('')
const showResetDialog = ref(false)
const resetLoading = ref(false)
const resetStep = ref<'request' | 'confirm'>('request')
const resetChallengeId = ref('')
const externalProviders = ref<ExternalLoginProvider[]>([])
const externalLoginProcessing = ref(false)
const providerIcons: Record<string, Component> = {
  microsoft: LogoMicrosoft,
  google: LogoGoogle,
  wechat: LogoWechat,
  steam: LogoSteam,
  qq: ChatbubblesOutline,
}
const providerIcon = (provider: string) => providerIcons[provider.toLowerCase()] ?? LogInOutline
const externalRegisterFormRef = ref<FormInst>()
const externalRegisterLoading = ref(false)
const externalRegisterToken = ref('')
const externalProviderName = ref('')
const externalAccount = ref('')
const externalRegisterAllowed = ref(true)
const externalLinkFormRef = ref<FormInst>()
const externalLinkLoading = ref(false)
const externalLinkForm = ref({ userName: '', password: '' })

const authPanelLabel = computed(() =>
  loginStep.value.startsWith('external-') ? t('Auth:ExternalAccountTitle') : t('Auth:Welcome'),
)

const externalAccountHint = computed(() =>
  externalAccount.value
    ? t('Auth:ExternalAccountHint', { account: externalAccount.value })
    : t('Auth:ExternalAccountHintNoAccount'),
)

const registerFormRef = ref<FormInst>()
const registerLoading = ref(false)
const registerForm = reactive({
  userName: '',
  nickName: '',
  email: '',
  password: '',
  confirmPassword: '',
})

const isStrongPassword = (value: string) =>
  value.length >= 8 &&
  /[A-Z]/.test(value) &&
  /[a-z]/.test(value) &&
  /[0-9]/.test(value) &&
  /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>/?]/.test(value)

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const optionalEmailRule: FormItemRule = {
  validator: (_: FormItemRule, value: string) =>
    !value?.trim() || EMAIL_PATTERN.test(value.trim()) || new Error(t('Auth:EmailInvalid')),
  trigger: ['input', 'blur'],
}

const registerRules = computed<FormRules>(() => ({
  userName: [
    { required: true, message: t('Auth:UsernameRequired'), trigger: 'blur' },
    { min: 3, max: 20, message: t('Auth:UsernameLength'), trigger: 'blur' },
  ],
  nickName: [
    { required: true, message: t('Auth:NicknameRequired'), trigger: 'blur' },
    { max: 20, message: t('Auth:NicknameLength'), trigger: 'blur' },
  ],
  email: [optionalEmailRule],
  password: [
    { required: true, message: t('Auth:PasswordRequired'), trigger: 'blur' },
    { max: 28, message: t('Auth:PasswordMaxLength'), trigger: 'blur' },
    {
      validator: (_, value: string) =>
        isStrongPassword(value) || new Error(t('Auth:PasswordPolicyInvalid')),
      trigger: ['input', 'blur'],
    },
  ],
  confirmPassword: [
    { required: true, message: t('Auth:ConfirmPasswordRequired'), trigger: 'blur' },
    {
      validator: (_, value: string) =>
        value === registerForm.password || new Error(t('Auth:PasswordMismatch')),
      trigger: ['input', 'blur'],
    },
  ],
}))

const currentAuthQuery = () =>
  typeof route.query.redirect === 'string' ? { redirect: route.query.redirect } : undefined

const authRouteName = (mode: 'login' | 'register') => {
  const configured = metaString(mode === 'login' ? 'authLoginRoute' : 'authRegisterRoute')
  return configured ?? (mode === 'login' ? 'Login' : 'Register')
}

const goLogin = () => {
  loginStep.value = 'credentials'
  void router.push({ name: authRouteName('login'), query: currentAuthQuery() })
}

const goRegister = () => {
  if (!registrationEnabled.value) return
  loginStep.value = 'credentials'
  void router.push({ name: authRouteName('register'), query: currentAuthQuery() })
}

const resolveRegisterErrorMessage = (error: unknown): string => {
  const code = (error as { response?: { data?: { error?: { code?: string } } } })?.response?.data
    ?.error?.code
  if (code === 'Sim:System:SelfRegistrationDisabled') return t('Auth:RegisterDisabled')
  if (code === 'Sim:System:UserNameAlreadyExists') return t('Auth:UsernameTaken')
  if (code === 'Sim:System:NickNameAlreadyExists') return t('Auth:NicknameTaken')
  if (code?.startsWith('Sim:System:Password')) return t('Auth:PasswordPolicyInvalid')
  return t('Auth:RegisterFailed')
}

const handleRegister = async () => {
  if (!registerFormRef.value) return

  try {
    await registerFormRef.value.validate()
  } catch {
    void scrollToFirstFormError(registerFormRef.value)
    return
  }

  const userName = registerForm.userName.trim()
  registerLoading.value = true
  try {
    await userApi.register({
      userName,
      nickName: registerForm.nickName.trim(),
      email: registerForm.email.trim() || null,
      password: registerForm.password,
    })
    useDiscreteApi.message.success(t('Auth:RegisterSuccess'))
    formData.userName = userName
    formData.password = ''
    Object.assign(registerForm, {
      userName: '',
      nickName: '',
      email: '',
      password: '',
      confirmPassword: '',
    })
    await router.push({ name: 'Login', query: currentAuthQuery() })
  } catch (error) {
    useDiscreteApi.message.error(resolveRegisterErrorMessage(error))
  } finally {
    registerLoading.value = false
  }
}

watch(authMode, (mode) => {
  if (mode === 'register') {
    loginStep.value = 'credentials'
  }
})

watch([authMode, registrationEnabled], ([mode, enabled]) => {
  if (mode === 'register' && !enabled) {
    void router.replace({ name: authRouteName('login'), query: currentAuthQuery() })
  }
})

const navigateToDefaultLanding = () => {
  const configured = metaString('authLandingRoute')
  if (configured) {
    void router.push({ name: configured })
    return
  }
  void router.push(routeConfig.adminDefaultRedirect)
}

async function navigateAfterLogin() {
  const redirect = route.query.redirect as string
  if (isValidRedirect(redirect)) {
    await router.push(redirect)
  } else if (window.history.state.back) {
    router.back()
  } else {
    await navigateToDefaultLanding()
  }
}

async function loadExternalProviders() {
  try {
    const providers = await userApi.getExternalLoginProviders()
    externalProviders.value = providers.filter((provider) => !!provider.provider)
  } catch {
    // 外部登录属于可选能力，后端不可用时保持隐藏，不重复提示。
    externalProviders.value = []
  }
}

async function enterFactorStep(response: LoginResponse) {
  factorChallengeId.value = response.twoFactorChallengeId || ''
  factorMethods.value = (response.twoFactorMethods || []) as TwoFactorMethod[]
  factorMethod.value = factorMethods.value[0] || 'authenticator'
  loginStep.value = 'factor'
  if (factorMethod.value === 'email' || factorMethod.value === 'sms') {
    await sendFactorCode()
  }
}

/**
 * 外部身份的用户名预填：取邮箱 @ 前缀并截断到 20 个字符，
 * 避免把邮箱整串（或超出账号名长度上限）作为默认账号名。
 */
const suggestUserNameFromExternal = (email?: string | null, userName?: string | null) => {
  const source = (email || userName || '').trim()
  const localPart = source.includes('@') ? (source.split('@')[0] ?? '') : source
  return localPart.slice(0, 20)
}

async function applyPendingExternalAuthResult(response: LoginResponse) {
  if (response.requiresExternalLoginRegistration) {
    // 未绑定的第三方身份：先让用户在“关联已有账号”与“注册新账号”之间选择，
    // 不再直接落到注册表单，也避免登录页标题与当前任务不符。
    externalRegisterToken.value = response.externalLoginRegistrationToken || ''
    externalProviderName.value =
      response.externalLoginProviderDisplayName || response.externalLoginProvider || ''
    externalAccount.value = response.externalLoginEmail || response.externalLoginUserName || ''
    externalRegisterAllowed.value = response.externalLoginRegistrationAllowed !== false
    externalRegisterForm.value = {
      userName: suggestUserNameFromExternal(
        response.externalLoginEmail,
        response.externalLoginUserName,
      ),
      nickName: (response.externalLoginDisplayName || '').slice(0, 20),
      email: response.externalLoginEmail || '',
      phone: '',
      password: '',
      confirmPassword: '',
    }
    loginStep.value = 'external-choice'
    return
  }

  if (response.requiresTwoFactor && response.twoFactorChallengeId) {
    await enterFactorStep(response)
  }
}

onMounted(async () => {
  // 后台修改开放注册后，重新进入登录页即可生效，无需整页刷新。
  void refreshSiteConfiguration()

  const pendingExternalAuthNotice = consumePendingExternalAuthNotice()
  if (pendingExternalAuthNotice) {
    useDiscreteApi.message.error(pendingExternalAuthNotice)
  }

  const pendingExternalAuthResult = consumePendingExternalAuthResult()
  if (pendingExternalAuthResult) {
    await applyPendingExternalAuthResult(pendingExternalAuthResult)
  }

  await loadExternalProviders()
})

async function handleExternalLogin(provider: string) {
  if (externalLoginProcessing.value) return

  externalLoginProcessing.value = true
  try {
    const redirect = await userApi.getExternalLoginRedirect(provider)
    if (!redirect.redirectUrl) throw new Error(t('Auth:ExternalLoginFailed'))
    // 记录发起页，回调失败时回到登录页展示错误，而不是停留在回调中间页。
    markExternalAuthOrigin('login')
    window.location.assign(redirect.redirectUrl)
  } catch (error) {
    handleError(error, t('Auth:ExternalLoginFailed'))
    externalLoginProcessing.value = false
  }
}

/**
 * 校验 redirect 参数，仅允许站点内部的相对路径，防止开放重定向攻击。
 * 拒绝以 // 或 http(s):// 开头的绝对 URL。
 */
function isValidRedirect(target: string | undefined | null): target is string {
  if (!target) return false
  // 相对路径，不以 / 开头也合法（Vue Router 视为相对当前路由）
  if (target.startsWith('/') && !target.startsWith('//')) return true
  // 不以 / 开头但也不是协议开头的相对路径
  if (!target.startsWith('/') && !/^[a-zA-Z][a-zA-Z0-9+.-]*:\/\//.test(target)) return true
  return false
}

const resetForm = reactive({
  account: '',
  method: 'email' as Exclude<TwoFactorMethod, 'recovery'>,
  code: '',
  newPassword: '',
  confirmPassword: '',
})
const resetFormRef = ref<FormInst>()
const resetRules = computed<FormRules>(() => ({
  account: [{ required: true, message: t('Auth:AccountRequired'), trigger: 'blur' }],
  code: [{ required: true, message: t('Auth:ResetFieldsRequired'), trigger: 'blur' }],
  newPassword: [{ required: true, message: t('Auth:ResetFieldsRequired'), trigger: 'blur' }],
  confirmPassword: [
    { required: true, message: t('Auth:ResetFieldsRequired'), trigger: 'blur' },
    {
      validator: (_, value: string) =>
        value === resetForm.newPassword || new Error(t('Auth:PasswordMismatch')),
      trigger: ['input', 'blur'],
    },
  ],
}))
const recoveryMethodOptions = computed(() => [
  { label: t('Auth:EmailCode'), value: 'email' },
  { label: t('Auth:SmsCode'), value: 'sms' },
  { label: t('Auth:AuthenticatorCode'), value: 'authenticator' },
])

const rememberedUserName = getRememberedLoginUserName()
const formData = reactive({
  userName: rememberedUserName,
  password: '',
  remember: Boolean(rememberedUserName),
})

const syncRememberedLogin = () => {
  if (formData.remember) {
    saveRememberedLoginUserName(formData.userName)
    return
  }
  clearRememberedLoginUserName()
}

const externalRegisterForm = ref({
  userName: '',
  nickName: '',
  email: '',
  phone: '',
  password: '',
  confirmPassword: '',
})

const rules = computed<FormRules>(() => ({
  userName: [
    { required: true, message: t('Auth:UsernameRequired'), trigger: 'blur' },
    { min: 3, max: 20, message: t('Auth:UsernameLength'), trigger: 'blur' },
  ],
  password: [
    { required: true, message: t('Auth:PasswordRequired'), trigger: 'blur' },
    { min: 6, max: 20, message: t('Auth:PasswordLength'), trigger: 'blur' },
  ],
}))

const externalRegisterRules = computed<FormRules>(() => ({
  userName: [
    { required: true, message: t('Auth:UsernameRequired'), trigger: 'blur' },
    { min: 3, max: 20, message: t('Auth:UsernameLength'), trigger: 'blur' },
  ],
  password: [
    { required: true, message: t('Auth:PasswordRequired'), trigger: 'blur' },
    { min: 6, max: 20, message: t('Auth:PasswordLength'), trigger: 'blur' },
  ],
  nickName: [
    { required: true, message: t('Auth:NicknameRequired'), trigger: 'blur' },
    { max: 20, message: t('Auth:NicknameLength'), trigger: 'blur' },
  ],
  email: [optionalEmailRule],
  confirmPassword: [
    { required: true, message: t('Auth:ConfirmPasswordRequired'), trigger: 'blur' },
    {
      validator: (_, value: string) =>
        value === externalRegisterForm.value.password || new Error(t('Auth:PasswordMismatch')),
      trigger: ['input', 'blur'],
    },
  ],
}))

const externalLinkRules = computed<FormRules>(() => ({
  userName: [{ required: true, message: t('Auth:UsernameRequired'), trigger: 'blur' }],
  password: [{ required: true, message: t('Auth:PasswordRequired'), trigger: 'blur' }],
}))

const externalErrorCode = (error: unknown) =>
  (error as { response?: { data?: { error?: { code?: string } } } })?.response?.data?.error?.code

const chooseExternalLink = () => {
  externalLinkForm.value = { userName: '', password: '' }
  loginStep.value = 'external-link'
}

const chooseExternalRegister = () => {
  loginStep.value = 'external-register'
}

const backToExternalChoice = () => {
  loginStep.value = 'external-choice'
}

const backToExternalLogin = () => {
  externalRegisterToken.value = ''
  externalProviderName.value = ''
  externalAccount.value = ''
  externalRegisterAllowed.value = true
  externalLinkForm.value = { userName: '', password: '' }
  externalRegisterForm.value = {
    userName: '',
    nickName: '',
    email: '',
    phone: '',
    password: '',
    confirmPassword: '',
  }
  loginStep.value = 'credentials'
}

const resolveExternalRegisterErrorMessage = (error: unknown): string => {
  const code = externalErrorCode(error)
  if (code === 'Sim:System:UserNameAlreadyExists') return t('Auth:UsernameTaken')
  if (code === 'Sim:System:NickNameAlreadyExists') return t('Auth:NicknameTaken')
  if (code === 'Sim:System:EmailAlreadyExists') return t('Auth:EmailTaken')
  if (code === 'Sim:System:ExternalLoginRegistrationDisabled') {
    return t('Auth:ExternalRegistrationClosed')
  }
  return t('Auth:ExternalRegisterFailed')
}

const resolveExternalLinkErrorMessage = (error: unknown): string => {
  const code = externalErrorCode(error)
  if (code === 'Sim:System:InvalidCredentials' || code === 'Sim:System:LoginCredentialsRequired') {
    return t('Auth:SignInFailed')
  }
  return t('Auth:LinkFailed')
}

const handleExternalLink = async () => {
  if (!externalLinkFormRef.value) return

  try {
    await externalLinkFormRef.value.validate()
  } catch {
    void scrollToFirstFormError(externalLinkFormRef.value)
    return
  }

  externalLinkLoading.value = true
  try {
    const response = await userStore.linkWithExternalLogin({
      registrationToken: externalRegisterToken.value,
      userName: externalLinkForm.value.userName.trim(),
      password: externalLinkForm.value.password,
    })
    if (response.requiresTwoFactor && response.twoFactorChallengeId) {
      await enterFactorStep(response)
      return
    }
    useDiscreteApi.message.success(t('Auth:SignInSuccess'))
    await navigateAfterLogin()
  } catch (error) {
    if (Array.isArray(error)) return
    useDiscreteApi.message.error(resolveExternalLinkErrorMessage(error))
  } finally {
    externalLinkLoading.value = false
  }
}

const handleExternalRegister = async () => {
  if (!externalRegisterFormRef.value) return

  try {
    await externalRegisterFormRef.value.validate()

    externalRegisterLoading.value = true
    const response = await userStore.registerWithExternalLogin({
      registrationToken: externalRegisterToken.value,
      userName: externalRegisterForm.value.userName.trim(),
      password: externalRegisterForm.value.password,
      nickName: externalRegisterForm.value.nickName.trim() || undefined,
      email: externalRegisterForm.value.email.trim() || undefined,
      phone: externalRegisterForm.value.phone.trim() || undefined,
    })
    if (response.requiresTwoFactor && response.twoFactorChallengeId) {
      await enterFactorStep(response)
      return
    }
    useDiscreteApi.message.success(t('Auth:SignInSuccess'))
    await navigateAfterLogin()
  } catch (error) {
    if (Array.isArray(error)) {
      void scrollToFirstFormError(externalRegisterFormRef.value)
      return
    }
    useDiscreteApi.message.error(resolveExternalRegisterErrorMessage(error))
  } finally {
    externalRegisterLoading.value = false
  }
}

const handleLogin = async (): Promise<void> => {
  if (!formRef.value) return

  try {
    await formRef.value.validate()

    loading.value = true
    const response = await userStore.login(formData.userName, formData.password, formData.remember)
    if (response.requiresTwoFactor && response.twoFactorChallengeId) {
      await enterFactorStep(response)
      return
    }

    syncRememberedLogin()
    useDiscreteApi.message.success(t('Auth:SignInSuccess'))

    // 优先使用 redirect 参数；否则尝试返回上一页；最后回退到默认路由
    const redirect = route.query.redirect as string
    if (isValidRedirect(redirect)) {
      await router.push(redirect)
    } else if (window.history.state.back) {
      router.back()
    } else {
      await navigateToDefaultLanding()
    }
  } catch (error: unknown) {
    if (Array.isArray(error)) {
      // 表单验证错误，不显示消息
      void scrollToFirstFormError(formRef.value)
      return
    }

    handleError(error, t('Auth:SignInFailed'))
  } finally {
    loading.value = false
  }
}

const factorMethodLabel = (method: TwoFactorMethod) =>
  ({
    authenticator: t('Auth:AuthenticatorCode'),
    email: t('Auth:EmailCode'),
    sms: t('Auth:SmsCode'),
    recovery: t('Auth:RecoveryCode'),
  })[method]

const sendFactorCode = async () => {
  if (factorMethod.value !== 'email' && factorMethod.value !== 'sms') return
  sendingCode.value = true
  try {
    await userApi.sendTwoFactorCode(factorChallengeId.value, factorMethod.value)
    useDiscreteApi.message.success(t('Auth:CodeSent'))
  } catch (error) {
    handleError(error, t('Auth:CodeSendFailed'))
  } finally {
    sendingCode.value = false
  }
}

const handleFactorLogin = async () => {
  if (!factorCode.value.trim()) {
    useDiscreteApi.message.warning(t('Auth:VerificationCodeRequired'))
    return
  }
  loading.value = true
  try {
    await userStore.completeTwoFactorLogin(
      factorChallengeId.value,
      factorMethod.value,
      factorCode.value,
      formData.remember,
    )
    syncRememberedLogin()
    useDiscreteApi.message.success(t('Auth:SignInSuccess'))
    const redirect = route.query.redirect as string
    if (isValidRedirect(redirect)) {
      await router.push(redirect)
    } else if (window.history.state.back) {
      router.back()
    } else {
      await navigateToDefaultLanding()
    }
  } catch (error) {
    handleError(error, t('Auth:VerificationFailed'))
  } finally {
    loading.value = false
  }
}

const resetLoginStep = () => {
  loginStep.value = 'credentials'
  factorChallengeId.value = ''
  factorMethods.value = []
  factorCode.value = ''
}

const closeResetDialog = () => {
  showResetDialog.value = false
  resetStep.value = 'request'
  resetChallengeId.value = ''
  resetForm.code = ''
  resetForm.newPassword = ''
  resetForm.confirmPassword = ''
}

const handlePasswordReset = async () => {
  try {
    await resetFormRef.value?.validate()
  } catch {
    void scrollToFirstFormError(resetFormRef.value)
    return
  }

  resetLoading.value = true
  try {
    if (resetStep.value === 'request') {
      const result = await userApi.requestPasswordReset(resetForm.account, resetForm.method)
      resetChallengeId.value = result.challengeId
      resetStep.value = 'confirm'
      useDiscreteApi.message.success(t('Auth:ResetRequestAccepted'))
      return
    }
    await userApi.resetPassword(
      resetChallengeId.value,
      resetForm.code,
      resetForm.newPassword,
      resetForm.confirmPassword,
    )
    closeResetDialog()
    useDiscreteApi.message.success(t('Auth:PasswordResetSuccess'))
  } catch (error) {
    handleError(error, t('Auth:PasswordResetFailed'))
  } finally {
    resetLoading.value = false
  }
}
</script>

<style scoped>
/* 登录页：左品牌主色面板，右奶油纸面登录面板 */
.login-page {
  position: relative;
  display: grid;
  grid-template-columns: minmax(0, 1.05fr) minmax(0, 0.95fr);
  min-height: 100dvh;
  color: var(--po-ink);
  background: var(--po-bg);
}

/* 全局工具栏贴合页面右上角；窄屏时落在品牌带右侧，不再压住登录卡片 */
.login-toolbar {
  position: absolute;
  z-index: 2;
  top: 22px;
  right: 26px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.login-panel {
  position: relative;
  display: grid;
  min-height: 100dvh;
  place-items: center;
  padding: 96px clamp(24px, 5vw, 72px) 48px;
  background-color: var(--po-bg);
  background-image: radial-gradient(var(--po-dot) 1px, transparent 1px);
  background-size: 26px 26px;
}

.login-shell {
  display: grid;
  width: min(430px, 100%);
  gap: 24px;
}

.login-card {
  width: 100%;
  padding: clamp(24px, 4vw, 36px);
  overflow: hidden;
  /* 卡片只做裁剪、不形成滚动容器：叠放的隐藏卡片更高时，
     聚焦或 scrollIntoView 不会把当前步骤整体滚出可视区 */
  overflow: clip;
  border: var(--po-border);
  border-radius: var(--po-r-lg);
  background: var(--po-paper);
  box-shadow: var(--po-shadow-hard);
}

.login-header {
  margin-bottom: 24px;
}

.login-header h2 {
  margin: 0;
  color: var(--po-ink);
  font-family: var(--po-font-display);
  font-size: clamp(26px, 3.2vw, 32px);
  font-weight: var(--po-weight-display);
  line-height: 1.2;
}

.login-header p {
  margin: 8px 0 0;
  color: var(--po-ink-soft);
  font-size: 13px;
  line-height: 1.7;
}

.login-foot {
  margin: 0;
  color: var(--po-ink-faint);
  font-size: 11px;
  text-align: center;
}

:deep(.n-form-item-label) {
  color: var(--po-ink);
  font-size: 13px;
  font-weight: var(--po-weight-strong);
}

.form-options {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin: 2px 0 20px;
  color: var(--po-ink-soft);
  font-size: 12px;
}

.login-submit {
  height: 48px;
  font-size: 15px;
  font-weight: var(--po-weight-strong);
  letter-spacing: 0.02em;
}

/* 注册表单字段区：高度封顶并内部滚动，提交按钮始终停在表单底部 */
.auth-form-scroll {
  max-height: clamp(200px, 42vh, 400px);
  overflow-y: auto;
  overscroll-behavior: contain;
  scrollbar-gutter: stable;
  padding-right: 10px;
  padding-bottom: 4px;
  /* 抵消内边距与滚动条槽位，让输入框与提交按钮同宽 */
  margin-right: -20px;
  /* 与固定底部的提交按钮之间给出明确的滚动边界 */
  margin-bottom: 16px;
  border-bottom: 1.5px solid var(--po-line);
}

.external-login {
  display: grid;
  gap: 14px;
  margin-top: 22px;
}

.external-divider {
  display: flex;
  align-items: center;
  gap: 12px;
  color: var(--po-ink-faint);
  font-size: 11.5px;
}

.external-divider::before,
.external-divider::after {
  content: '';
  flex: 1;
  height: 1.5px;
  background: var(--po-line);
}

.external-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 10px;
}

.external-actions :deep(.n-button) {
  width: 42px;
  height: 42px;
}

.external-choice {
  display: grid;
  gap: 10px;
}

/* 表单字段提示自带负上边距，这里紧贴按钮下方，需要归零 */
.external-choice .auth-field-hint {
  margin: 0;
}

.factor-step {
  display: grid;
  gap: 18px;
}

.factor-heading .po-kicker {
  color: var(--po-ink-soft);
}

.factor-heading h3 {
  margin: 12px 0 8px;
  color: var(--po-ink);
  font-family: var(--po-font-display);
  font-size: 24px;
  font-weight: var(--po-weight-display);
}

.factor-heading p {
  margin: 0;
  color: var(--po-ink-soft);
  font-size: 13px;
  line-height: 1.7;
}

/* 外部登录表单步骤：标题与“返回”同排，标题略小于决策步骤 */
.step-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}

.step-heading h3 {
  margin: 0;
  color: var(--po-ink);
  font-family: var(--po-font-display);
  font-size: 20px;
  font-weight: var(--po-weight-display);
  line-height: 1.3;
}

.step-back {
  flex: 0 0 auto;
  font-size: 12px;
}

.step-back :deep(.n-icon) {
  font-size: 15px;
}

.step-hint {
  margin: -4px 0 16px;
  color: var(--po-ink-soft);
  font-size: 13px;
  line-height: 1.7;
}

.factor-methods {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.factor-actions,
.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.back-button {
  justify-self: start;
}

.auth-card-stack {
  position: relative;
  display: grid;
}

/* 两张卡片叠放在同一格：当前卡片占据高度，离场卡片绝对定位滑出，避免切换瞬间卡片塌陷。 */
.auth-card {
  grid-area: 1 / 1;
  display: block;
  background: var(--po-paper);
  transition:
    transform 0.18s cubic-bezier(0.22, 0.61, 0.36, 1),
    visibility 0s linear;
}

.auth-card.is-active {
  position: relative;
  z-index: 1;
  visibility: visible;
  opacity: 1;
  transform: none;
}

.auth-card.is-hidden {
  position: absolute;
  top: 0;
  left: 0;
  z-index: 0;
  width: 100%;
  visibility: hidden;
  opacity: 0;
  pointer-events: none;
  transition:
    opacity 0.16s ease,
    transform 0.18s cubic-bezier(0.22, 0.61, 0.36, 1),
    visibility 0s linear 0.18s;
}

.auth-card.is-hidden--left {
  transform: translateX(-12px);
}

.auth-card.is-hidden--right {
  transform: translateX(12px);
}

.auth-switch-hint {
  margin: 18px 0 0;
  color: var(--po-ink-soft);
  font-size: 12px;
  text-align: center;
}

.auth-switch-hint :deep(.n-button) {
  margin-left: 4px;
  font-size: 12px;
}

.auth-field-hint {
  margin: -12px 0 16px;
  color: var(--po-ink-faint);
  font-size: 11.5px;
  line-height: 1.6;
}

/* 博客前台登录：单列纸面布局 + 左上角博客品牌入口，登录卡片居中 */
.login-page.is-centered-auth {
  grid-template-columns: minmax(0, 1fr);
}

.is-centered-auth .login-panel {
  min-height: 100dvh;
  padding: 132px 24px 64px;
}

.is-centered-auth .login-shell {
  width: min(440px, 100%);
}

.auth-home-brand {
  position: absolute;
  top: 22px;
  left: 26px;
  z-index: 2;
  display: inline-flex;
  align-items: center;
  gap: 10px;
  max-width: min(52vw, 340px);
  color: var(--po-ink);
  text-decoration: none;
}

.auth-home-brand__mark {
  display: grid;
  flex-shrink: 0;
  width: 36px;
  height: 36px;
  place-items: center;
  color: var(--po-on-bright);
  background: var(--po-sun);
  border: var(--po-border);
  border-radius: 11px;
  box-shadow: var(--po-shadow-soft);
  transform: rotate(-4deg);
}

.auth-home-brand__text {
  display: grid;
  gap: 2px;
  min-width: 0;
  line-height: 1.15;
}

.auth-home-brand__text strong {
  overflow: hidden;
  font-family: var(--po-font-display);
  font-size: 15px;
  font-weight: var(--po-weight-display);
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (prefers-reduced-motion: reduce) {
  .auth-card,
  .auth-card.is-active,
  .auth-card.is-hidden {
    transition: none;
  }

  .auth-card.is-hidden--left,
  .auth-card.is-hidden--right {
    transform: none;
  }
}

.reset-dialog {
  width: min(520px, calc(100vw - 32px));
}

@media (max-width: 960px) {
  .login-page {
    grid-template-columns: minmax(0, 1fr);
  }

  .is-centered-auth .login-panel {
    min-height: 100dvh;
    padding: 112px 20px 48px;
  }

  .auth-home-brand {
    top: 28px;
    left: clamp(20px, 5vw, 40px);
    max-width: min(46vw, 260px);
  }

  .auth-form-scroll {
    max-height: clamp(180px, 34vh, 330px);
  }

  .login-panel {
    min-height: auto;
    padding: 44px 20px 48px;
  }

  /* 与品牌带 logo 行（padding 26px + 44px 标记）居中对齐 */
  .login-toolbar {
    top: 28px;
    right: clamp(20px, 5vw, 40px);
  }

  /* 为右上角工具栏预留横向空间，站点名过长时走省略号而不是被控件盖住 */
  .login-page :deep(.auth-brand) {
    padding-right: 172px;
  }

  /* 工具栏落在蓝色品牌带上，换成纸面底色保证两种主题下的对比度 */
  .login-toolbar :deep(.theme-toggle) {
    background: var(--po-paper);
  }
}

@media (max-width: 560px) {
  .login-panel {
    padding: 40px 16px 40px;
  }

  .is-centered-auth .login-panel {
    padding: 104px 16px 40px;
  }

  .auth-home-brand__text small {
    display: none;
  }

  .login-shell {
    gap: 20px;
  }

  .login-header {
    margin-bottom: 20px;
  }
}
</style>

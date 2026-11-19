<template>
  <main class="setup-page">
    <AuthBrandPanel
      :kicker="t('Setup:Kicker')"
      :headline="t('Setup:Headline')"
      :description="t('Setup:Description')"
      :highlights="brandHighlights"
      :show-progress="asideProgressVisible"
    >
      <template #progress>
        <div class="setup-flow">
          <Transition
            name="setup-flow-swap"
            mode="out-in"
            @after-enter="revealActiveStep"
          >
            <div :key="flowMode" class="setup-flow__inner">
              <div class="setup-flow__top">
                <span class="setup-flow__label">{{ flowTitle }}</span>
                <span class="setup-flow__percent">{{ asidePercent }}%</span>
              </div>

              <div
                class="setup-flow__rail"
                role="progressbar"
                :aria-label="t('Setup:FlowAria')"
                aria-valuemin="0"
                aria-valuemax="100"
                :aria-valuenow="asidePercent"
              >
                <span class="setup-flow__fill" :style="{ width: `${asidePercent}%` }"></span>
              </div>

              <ol ref="stepListRef" class="setup-flow__steps">
                <li
                  v-for="(step, index) in asideSteps"
                  :key="step.key"
                  class="setup-flow__step"
                  :class="`is-${step.state}`"
                >
                  <span class="setup-flow__dot" aria-hidden="true">
                    <span class="setup-flow__index">{{ index + 1 }}</span>
                  </span>
                  <span class="setup-flow__name">{{ t(step.label) }}</span>
                </li>
              </ol>

              <p v-if="flowHint" class="setup-flow__hint">{{ flowHint }}</p>
            </div>
          </Transition>
        </div>
      </template>
    </AuthBrandPanel>

    <section class="setup-panel">
      <div class="setup-toolbar">
        <ThemeToggle />
        <LocaleSwitcher />
      </div>

      <div v-if="loading" class="setup-loading">
        <n-spin size="large" />
      </div>

      <n-result
        v-else-if="loadFailed"
        class="setup-result"
        status="warning"
        :title="t('Setup:LoadFailed')"
      >
        <template #footer>
          <n-button type="primary" @click="loadStatus">{{ t('Setup:Retry') }}</n-button>
        </template>
      </n-result>

      <div v-else class="setup-shell">
        <div class="setup-card">
          <Transition name="setup-card-swap" mode="out-in">
            <div v-if="submitted" key="state" class="setup-state">
              <Transition name="setup-state-swap" mode="out-in">
                <div v-if="progress.completed" key="done" class="setup-state__item">
                  <span class="setup-state__badge" aria-hidden="true">
                    <Icon name="carbon:CheckmarkOutline" :size="28" />
                  </span>
                  <strong>{{ t('Setup:ProgressDoneTitle') }}</strong>
                  <p>{{ t('Setup:ProgressDoneHint') }}</p>
                </div>
                <div v-else key="running" class="setup-state__item">
                  <n-spin size="large" />
                  <strong>{{ t('Setup:DataInitializing') }}</strong>
                  <p class="setup-state__stage">{{ dataStageLabel }}</p>
                  <div class="setup-state__rail">
                    <span :style="{ width: `${progressPercent}%` }"></span>
                  </div>
                  <span class="setup-state__percent">{{ progressPercent }}%</span>
                </div>
              </Transition>
            </div>

            <div v-else key="form">
              <header class="setup-header">
                <div class="setup-header__top">
                  <span class="po-kicker">{{ t('Setup:Kicker') }}</span>
                  <span class="setup-step-pill">{{ stepCountLabel }}</span>
                </div>

                <Transition name="setup-head-swap" mode="out-in">
                  <div :key="currentStepDef.key" class="setup-header__copy">
                    <h2>{{ t(currentStepDef.title) }}</h2>
                    <p>{{ t(currentStepDef.hint) }}</p>
                  </div>
                </Transition>
              </header>

              <Transition
                :name="stepTransitionName"
                mode="out-in"
                @after-enter="stepBusy = false"
              >
                <n-form
                  :key="currentStepDef.key"
                  ref="formRef"
                  :model="form"
                  :rules="rules"
                  label-placement="top"
                  size="large"
                  @submit.prevent="handlePrimary"
                >
                  <section v-if="currentStep === 0" class="setup-step">
                    <n-form-item path="adminUserName" :label="t('Setup:AdminUserName')">
                      <n-input
                        v-model:value="form.adminUserName"
                        :maxlength="28"
                        :input-props="{ autocomplete: 'username' }"
                        clearable
                      />
                    </n-form-item>

                    <div class="setup-grid">
                      <n-form-item path="adminPassword" :label="t('Setup:AdminPassword')">
                        <n-input
                          v-model:value="form.adminPassword"
                          type="password"
                          show-password-on="click"
                          :input-props="{ autocomplete: 'new-password' }"
                          clearable
                        />
                      </n-form-item>
                      <n-form-item path="confirmPassword" :label="t('Setup:ConfirmPassword')">
                        <n-input
                          v-model:value="form.confirmPassword"
                          type="password"
                          show-password-on="click"
                          :input-props="{ autocomplete: 'new-password' }"
                          clearable
                        />
                      </n-form-item>
                    </div>

                    <p class="setup-hint">{{ t('Setup:PasswordHint') }}</p>

                    <n-form-item path="adminNickName" :label="t('Setup:AdminNickName')">
                      <n-input v-model:value="form.adminNickName" :maxlength="20" clearable />
                    </n-form-item>
                  </section>

                  <section v-else-if="currentStep === 1" class="setup-step">
                    <n-form-item path="siteName" :label="t('Setup:SiteName')">
                      <n-input
                        v-model:value="form.siteName"
                        :maxlength="64"
                        show-count
                        clearable
                      />
                    </n-form-item>

                    <div class="setup-grid">
                      <n-form-item path="defaultLanguage" :label="t('Setup:DefaultLanguage')">
                        <n-select
                          v-model:value="form.defaultLanguage"
                          :options="languageOptions"
                        />
                      </n-form-item>
                      <n-form-item path="defaultTheme" :label="t('Setup:DefaultTheme')">
                        <n-select v-model:value="form.defaultTheme" :options="themeOptions" />
                      </n-form-item>
                    </div>
                  </section>

                  <section v-else class="setup-step">
                    <div class="setup-switch">
                      <div class="setup-switch__copy">
                        <strong>{{ t('Setup:RegistrationEnabled') }}</strong>
                        <span>{{ t('Setup:RegistrationHint') }}</span>
                      </div>
                      <n-switch v-model:value="form.registrationEnabled">
                        <template #checked>{{ t('Common:Enabled') }}</template>
                        <template #unchecked>{{ t('Common:Disabled') }}</template>
                      </n-switch>
                    </div>

                    <div class="setup-grid">
                      <n-form-item
                        path="loginMaxFailedAttempts"
                        :label="t('Setup:MaxFailedAttempts')"
                      >
                        <n-input-number
                          v-model:value="form.loginMaxFailedAttempts"
                          :min="1"
                          :max="20"
                          :precision="0"
                          style="width: 100%"
                        />
                      </n-form-item>
                      <n-form-item
                        path="lockoutDurationMinutes"
                        :label="t('Setup:LockoutMinutes')"
                      >
                        <n-input-number
                          v-model:value="form.lockoutDurationMinutes"
                          :min="1"
                          :max="1440"
                          :precision="0"
                          style="width: 100%"
                        />
                      </n-form-item>
                    </div>
                    <p class="setup-hint">{{ t('Setup:MaxFailedAttemptsHint') }}</p>
                  </section>

                  <footer class="setup-actions">
                    <n-button
                      v-if="currentStep > 0"
                      class="setup-back"
                      secondary
                      size="large"
                      :disabled="locked || stepBusy"
                      @click="goBack"
                    >
                      {{ t('Setup:Previous') }}
                    </n-button>
                    <n-button
                      class="setup-next"
                      type="primary"
                      attr-type="submit"
                      size="large"
                      :disabled="locked || stepBusy || navigating"
                    >
                      {{ primaryLabel }}
                    </n-button>
                  </footer>
                </n-form>
              </Transition>
            </div>
          </Transition>
        </div>

        <p class="setup-foot">© {{ year }} {{ siteName }}</p>
      </div>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import type { FormInst, FormRules } from 'naive-ui'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { AuthBrandPanel } from '@/components/AuthBrandPanel'
import Icon from '@/components/Icon'
import { LocaleSwitcher } from '@/components/LocaleSwitcher'
import { ThemeToggle } from '@/components/ThemeToggle'
import { siteName } from '@/config/branding'
import { loadSetupStatus, resetSetupStatus } from '@/config/setup'
import { fetchSetupProgress, initializeSystem } from '@/api/setup'
import type { SetupProgress } from '@/api/setup'
import { formatErrorMessage } from '@/utils/errorHandler'
import { scrollToFirstFormError } from '@/utils/formErrors'

const router = useRouter()
const formRef = ref<FormInst>()
const loading = ref(true)
const loadFailed = ref(false)
const navigating = ref(false)
const stepBusy = ref(false)
const currentStep = ref(0)
const stepDirection = ref<'forward' | 'back'>('forward')
const setupDone = ref(false)
const submitted = ref(false)
const dataInitVisible = ref(false)
const progress = ref<SetupProgress>({
  running: false,
  stage: 'idle',
  percent: 0,
  completed: false,
})
const year = new Date().getFullYear()

const stepDefinitions = [
  { key: 'account', title: 'Setup:AccountSection', hint: 'Setup:AccountHint' },
  { key: 'site', title: 'Setup:SiteSection', hint: 'Setup:SiteHint' },
  { key: 'security', title: 'Setup:SecuritySection', hint: 'Setup:SecurityHint' },
] as const

const lastStepIndex = stepDefinitions.length - 1
const locked = computed(() => submitted.value || progress.value.completed)
const currentStepDef = computed(
  () => stepDefinitions[currentStep.value] ?? stepDefinitions[0],
)
const isLastStep = computed(() => currentStep.value === lastStepIndex)
const stepTransitionName = computed(() => `setup-step-${stepDirection.value}`)
const stepCountLabel = computed(() =>
  t('Setup:StepCount', { current: currentStep.value + 1, total: stepDefinitions.length }),
)

const progressStepLabels: Record<string, string> = {
  preparing: 'Setup:ProgressPreparing',
  administrator: 'Setup:ProgressAdministrator',
  'system-account': 'Setup:ProgressSystemAccount',
  tenant: 'Setup:ProgressTenant',
  'system-modules': 'Setup:ProgressSystemModules',
  roles: 'Setup:ProgressRoles',
  users: 'Setup:ProgressUsers',
  modules: 'Setup:ProgressModules',
  routes: 'Setup:ProgressRoutes',
  permissions: 'Setup:ProgressPermissions',
  configurations: 'Setup:ProgressConfigurations',
  'approval-numbering': 'Setup:ProgressApprovalNumbering',
  organization: 'Setup:ProgressOrganization',
  'admin-role': 'Setup:ProgressAdminRole',
  'basic-role': 'Setup:ProgressBasicRole',
  finalizing: 'Setup:ProgressFinalize',
}

/** 后端未下发步骤列表（旧版本）时的兜底。 */
const fallbackProgressSteps = [
  { key: 'preparing', label: 'Setup:ProgressPreparing' },
  { key: 'administrator', label: 'Setup:ProgressAdministrator' },
  { key: 'system-account', label: 'Setup:ProgressSystemAccount' },
  { key: 'seeding', label: 'Setup:ProgressSeed' },
  { key: 'finalizing', label: 'Setup:ProgressFinalize' },
]

/** 从进度接口缓存步骤顺序，初始化完成时即使没轮询到也能展示完整列表。 */
const progressStepKeys = ref<string[]>([])

const progressPercent = computed(() => Math.min(100, Math.max(0, progress.value.percent)))

const progressSteps = computed(() => {
  const backendSteps = progress.value.steps?.length
    ? progress.value.steps
    : progressStepKeys.value
  const definitions = backendSteps.length
    ? backendSteps.map((key) => ({ key, label: progressStepLabels[key] ?? key }))
    : fallbackProgressSteps
  const stage = progress.value.stage
  const currentIndex = definitions.findIndex((step) => step.key === stage)

  return definitions.map((step, index) => {
    let state: 'done' | 'active' | 'pending' = 'pending'
    if (progress.value.completed || index < currentIndex) {
      state = 'done'
    } else if (index === currentIndex) {
      state = 'active'
    }

    return { ...step, state }
  })
})

const wizardFlowSteps = computed(() =>
  stepDefinitions.map((step, index) => {
    let state: 'done' | 'active' | 'pending' = 'pending'
    if (setupDone.value || currentStep.value > index) {
      state = 'done'
    } else if (currentStep.value === index) {
      state = 'active'
    }

    return { key: step.key, label: step.title, state }
  }),
)

const asideProgressVisible = computed(() => submitted.value || currentStep.value > 0)
const flowMode = computed(() => (dataInitVisible.value ? 'init' : 'wizard'))
const asideSteps = computed(() =>
  dataInitVisible.value ? progressSteps.value : wizardFlowSteps.value,
)
const stepListRef = ref<HTMLElement | null>(null)
const activeStepKey = computed(
  () => asideSteps.value.find((step) => step.state === 'active')?.key ?? '',
)

watch(activeStepKey, () => {
  void nextTick(revealActiveStep)
})

/** 只滚动步骤容器：列表约 5-6 行高，进入下一步时把当前步骤滚入视野。 */
function revealActiveStep() {
  const list = stepListRef.value
  if (!list) return

  const active = list.querySelector<HTMLElement>('.setup-flow__step.is-active')
  if (!active) return

  const listRect = list.getBoundingClientRect()
  const activeRect = active.getBoundingClientRect()
  const padding = 8
  if (activeRect.bottom > listRect.bottom) {
    list.scrollTo({
      top: list.scrollTop + activeRect.bottom - listRect.bottom + padding,
      behavior: 'smooth',
    })
  } else if (activeRect.top < listRect.top) {
    list.scrollTo({
      top: list.scrollTop + activeRect.top - listRect.top - padding,
      behavior: 'smooth',
    })
  }
}

const wizardPercent = computed(() =>
  setupDone.value
    ? 100
    : Math.round((currentStep.value / stepDefinitions.length) * 100),
)
const asidePercent = computed(() =>
  dataInitVisible.value ? progressPercent.value : wizardPercent.value,
)
const flowTitle = computed(() => {
  if (!dataInitVisible.value) return t('Setup:FlowTitle')
  return progress.value.completed
    ? t('Setup:ProgressDoneTitle')
    : t('Setup:DataInitializing')
})
const flowHint = computed(() => {
  if (!dataInitVisible.value) return ''
  return progress.value.completed ? t('Setup:ProgressDoneHint') : t('Setup:ProgressHint')
})
const dataStageLabel = computed(() => {
  if (progress.value.completed) return t('Setup:ProgressDoneHint')
  const active = progressSteps.value.find((step) => step.state === 'active')
  return active ? t(active.label) : t('Setup:DataInitializing')
})
const primaryLabel = computed(() => (isLastStep.value ? t('Setup:Submit') : t('Setup:Next')))

const form = reactive({
  adminUserName: 'admin',
  adminPassword: '',
  confirmPassword: '',
  adminNickName: t('Setup:DefaultAdminNickName'),
  siteName: 'SimFramework',
  defaultLanguage: 'zh-Hans',
  defaultTheme: 'auto',
  registrationEnabled: false,
  loginMaxFailedAttempts: 5,
  lockoutDurationMinutes: 30,
})

const brandHighlights = computed(() => [
  t('Setup:BrandHighlightAccount'),
  t('Setup:BrandHighlightSite'),
  t('Setup:BrandHighlightTheme'),
  t('Setup:BrandHighlightSecurity'),
])

const languageOptions = computed(() => [
  { label: t('Setup:LanguageZh'), value: 'zh-Hans' },
  { label: t('Setup:LanguageEn'), value: 'en' },
])

const themeOptions = computed(() => [
  { label: t('Setup:ThemeAuto'), value: 'auto' },
  { label: t('Setup:ThemeLight'), value: 'light' },
  { label: t('Setup:ThemeDark'), value: 'dark' },
])

const rules = computed<FormRules>(() => ({
  adminUserName: [
    { required: true, message: t('Auth:UsernameRequired'), trigger: 'blur' },
    { min: 3, max: 28, message: t('Setup:UserNameLength'), trigger: 'blur' },
  ],
  adminNickName: [
    { required: true, message: t('Setup:AdminNickNameRequired'), trigger: 'blur' },
    { max: 20, message: t('Setup:AdminNickNameLength'), trigger: 'blur' },
  ],
  adminPassword: [
    { required: true, message: t('Auth:PasswordRequired'), trigger: 'blur' },
    { min: 8, max: 128, message: t('Setup:PasswordHint'), trigger: 'blur' },
  ],
  confirmPassword: [
    { required: true, message: t('Auth:ConfirmPasswordRequired'), trigger: 'blur' },
    {
      validator: (_, value: string) =>
        value === form.adminPassword || new Error(t('Setup:PasswordMismatch')),
      trigger: ['input', 'blur'],
    },
  ],
  siteName: [
    { required: true, message: t('Setup:SiteName'), trigger: 'blur' },
    { max: 64, message: t('Setup:SiteName'), trigger: 'blur' },
  ],
}))

async function loadStatus() {
  loading.value = true
  loadFailed.value = false

  const status = await loadSetupStatus()
  if (!status) {
    loading.value = false
    loadFailed.value = true
    return
  }

  if (status.initialized) {
    await router.replace({ name: 'Login' })
    return
  }

  void loadProgressSteps()

  form.adminUserName = status.defaultAdminUserName || 'admin'
  form.siteName = status.siteName || 'SimFramework'
  const configuredLanguage = (status.defaultLanguage ?? '').trim().toLowerCase()
  form.defaultLanguage =
    configuredLanguage === 'en' || configuredLanguage.startsWith('en-') ? 'en' : 'zh-Hans'
  form.defaultTheme = ['auto', 'light', 'dark'].includes(status.defaultTheme ?? '')
    ? (status.defaultTheme as string)
    : 'auto'
  form.registrationEnabled = status.registrationEnabled === true
  form.loginMaxFailedAttempts = status.loginMaxFailedAttempts ?? 5
  form.lockoutDurationMinutes = status.lockoutDurationMinutes ?? 30

  loading.value = false
}

const PROGRESS_POLL_INTERVAL = 600
const DATA_INIT_HOLD_MS = 600
let progressTimer: number | undefined
let dataInitTimer: number | undefined
let submittedAt = 0

function stopProgressPolling() {
  if (progressTimer !== undefined) {
    window.clearInterval(progressTimer)
    progressTimer = undefined
  }
}

function stopDataInitTimer() {
  if (dataInitTimer !== undefined) {
    window.clearTimeout(dataInitTimer)
    dataInitTimer = undefined
  }
}

/** 先让「设置进度」显示到 100%，再切到后端的数据初始化进度。 */
function scheduleDataInit() {
  if (dataInitVisible.value) return
  if (progress.value.stage === 'idle' && !progress.value.completed) return
  if (dataInitTimer !== undefined) return

  const remaining = submittedAt + DATA_INIT_HOLD_MS - Date.now()
  if (remaining <= 0) {
    dataInitVisible.value = true
    return
  }

  dataInitTimer = window.setTimeout(() => {
    dataInitTimer = undefined
    if (submitted.value) dataInitVisible.value = true
  }, remaining)
}

/** 打开页面时先读取后端下发的初始化步骤，保证首次展示就有完整步骤列表。 */
async function loadProgressSteps() {
  try {
    const current = await fetchSetupProgress()
    if (current.steps?.length) progressStepKeys.value = current.steps
  } catch {
    // 步骤列表只是展示辅助，失败时使用兜底列表。
  }
}

async function syncProgress() {
  try {
    const current = await fetchSetupProgress()
    if (!submitted.value) return
    if (current.steps?.length) progressStepKeys.value = current.steps
    // 初始化请求尚未在后端开始时可能读到旧的 idle 快照，不能把页面进度倒退回去。
    if (current.stage === 'idle' && !current.completed) return
    progress.value = current
    scheduleDataInit()
  } catch {
    // 初始化请求本身会给出最终错误；轮询失败时保留当前进度即可。
  }
}

function startProgressPolling() {
  stopProgressPolling()
  void syncProgress()
  progressTimer = window.setInterval(syncProgress, PROGRESS_POLL_INTERVAL)
}

function goToStep(next: number, direction: 'forward' | 'back') {
  if (next === currentStep.value) return
  stepDirection.value = direction
  stepBusy.value = true
  currentStep.value = next
}

async function validateCurrentStep(): Promise<boolean> {
  if (!formRef.value) return false
  try {
    await formRef.value.validate()
    return true
  } catch {
    void scrollToFirstFormError(formRef.value)
    return false
  }
}

async function handlePrimary() {
  if (locked.value || navigating.value || stepBusy.value) return

  navigating.value = true
  try {
    if (!(await validateCurrentStep())) return
    if (isLastStep.value) {
      await submit()
    } else {
      goToStep(currentStep.value + 1, 'forward')
    }
  } finally {
    navigating.value = false
  }
}

function goBack() {
  if (locked.value || stepBusy.value || currentStep.value === 0) return
  goToStep(currentStep.value - 1, 'back')
}

async function submit() {
  if (submitted.value) return

  submittedAt = Date.now()
  submitted.value = true
  setupDone.value = true
  dataInitVisible.value = false
  progress.value = {
    running: true,
    stage: 'idle',
    percent: 0,
    completed: false,
  }
  startProgressPolling()

  try {
    await initializeSystem({
      adminUserName: form.adminUserName.trim(),
      adminPassword: form.adminPassword,
      adminNickName: form.adminNickName.trim(),
      siteName: form.siteName.trim(),
      defaultLanguage: form.defaultLanguage,
      defaultTheme: form.defaultTheme,
      registrationEnabled: form.registrationEnabled,
      loginMaxFailedAttempts: form.loginMaxFailedAttempts,
      lockoutDurationMinutes: form.lockoutDurationMinutes,
    })

    stopProgressPolling()
    stopDataInitTimer()
    try {
      // 初始化可能很快，完成后补拉一次以拿到完整步骤列表。
      const final = await fetchSetupProgress()
      if (final.steps?.length) progressStepKeys.value = final.steps
    } catch {
      // 补拉失败时沿用轮询缓存或兜底步骤。
    }
    progress.value = {
      running: false,
      stage: 'completed',
      percent: 100,
      completed: true,
      steps: progressStepKeys.value,
    }
    dataInitVisible.value = true
    resetSetupStatus()
    useDiscreteApi.message.success(t('Setup:Success'))
    window.setTimeout(() => {
      window.location.assign('/login')
    }, 2400)
  } catch (error) {
    stopProgressPolling()
    stopDataInitTimer()
    submitted.value = false
    setupDone.value = false
    dataInitVisible.value = false
    progress.value = {
      running: false,
      stage: 'idle',
      percent: 0,
      completed: false,
    }
    useDiscreteApi.message.error(formatErrorMessage(error, t('Setup:SubmitFailed')))
  }
}

onMounted(loadStatus)
onBeforeUnmount(() => {
  stopProgressPolling()
  stopDataInitTimer()
})
</script>

<style scoped>
/* 初始化页：左品牌主色面板，右配置表单 */
.setup-page {
  display: grid;
  grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr);
  min-height: 100dvh;
  color: var(--po-ink);
  background: var(--po-bg);
}

.setup-panel {
  position: relative;
  display: grid;
  min-height: 100dvh;
  place-items: center;
  padding: 84px clamp(24px, 4vw, 64px) 44px;
  background-color: var(--po-bg);
  background-image: radial-gradient(var(--po-dot) 1px, transparent 1px);
  background-size: 26px 26px;
}

.setup-toolbar {
  position: absolute;
  top: 22px;
  right: 26px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.setup-loading {
  display: grid;
  place-items: center;
}

.setup-result {
  width: min(520px, 100%);
}

.setup-shell {
  display: grid;
  width: min(600px, 100%);
  gap: 20px;
}

.setup-card {
  width: 100%;
  padding: clamp(24px, 3vw, 36px);
  border: var(--po-border);
  border-radius: var(--po-r-lg);
  background: var(--po-paper);
  box-shadow: var(--po-shadow-hard);
}

.setup-header {
  margin-bottom: 8px;
}

.setup-header__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.setup-step-pill {
  padding: 4px 11px;
  color: var(--po-ink-soft);
  border: 1.5px solid var(--po-line-mid);
  border-radius: 999px;
  background: var(--po-bg-deep);
  font-size: 11px;
  font-weight: var(--po-weight-strong);
  letter-spacing: 0.02em;
  white-space: nowrap;
}

.setup-header__copy h2 {
  margin: 10px 0 0;
  font-family: var(--po-font-display);
  font-size: clamp(26px, 3vw, 32px);
  font-weight: var(--po-weight-display);
  line-height: 1.2;
}

.setup-header__copy p {
  margin: 8px 0 0;
  color: var(--po-ink-soft);
  font-size: 13px;
  line-height: 1.7;
}

.setup-step {
  display: grid;
  gap: 2px;
  align-content: start;
  padding-top: 18px;
  margin-top: 16px;
  border-top: 1.5px dashed var(--po-line-mid);
}

.setup-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 16px;
}

.setup-hint {
  margin: -6px 0 10px;
  color: var(--po-ink-faint);
  font-size: 11.5px;
  line-height: 1.6;
}

.setup-switch {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 14px;
  padding: 12px 14px;
  border: 1.5px solid var(--po-line);
  border-radius: var(--po-r);
  background: var(--po-bg-deep);
}

.setup-switch__copy {
  display: grid;
  gap: 2px;
}

.setup-switch__copy strong {
  font-size: 13px;
  font-weight: var(--po-weight-strong);
}

.setup-switch__copy span {
  color: var(--po-ink-soft);
  font-size: 11.5px;
  line-height: 1.6;
}

.setup-actions {
  display: flex;
  gap: 12px;
  margin-top: 22px;
  padding-top: 20px;
  border-top: 1.5px dashed var(--po-line);
}

.setup-back,
.setup-next {
  height: 46px;
  font-size: 14px;
  font-weight: var(--po-weight-strong);
}

.setup-next {
  flex: 1;
  font-size: 15px;
  letter-spacing: 0.02em;
}

.setup-foot {
  margin: 0;
  color: var(--po-ink-faint);
  font-size: 11px;
  text-align: center;
}

/* 宽屏左栏进度：替换品牌描述 */
.setup-flow {
  display: grid;
  width: 100%;
}

.setup-flow__inner {
  display: grid;
  gap: 14px;
}

.setup-flow__top {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}

.setup-flow__label {
  color: var(--bp-soft);
  font-size: 11px;
  font-weight: var(--po-weight-strong);
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.setup-flow__percent {
  color: var(--bp-ink);
  font-family: var(--po-font-display);
  font-size: 22px;
  font-weight: var(--po-weight-display);
  line-height: 1;
}

.setup-flow__rail {
  overflow: hidden;
  height: 7px;
  border: 1.5px solid var(--bp-line);
  border-radius: 999px;
  background: var(--bp-chip);
}

.setup-flow__fill {
  display: block;
  height: 100%;
  background: var(--po-coral);
  transition: width 420ms cubic-bezier(0.2, 0.8, 0.2, 1);
}

.setup-flow__steps {
  display: grid;
  gap: 2px;
  /* 约 6 行高度；步骤更多时在列表内滚动，避免撑高左栏 */
  max-height: 250px;
  margin: 0;
  padding: 0 4px 0 0;
  overflow-y: auto;
  overscroll-behavior: contain;
  list-style: none;
  scrollbar-color: var(--bp-soft) transparent;
  scrollbar-width: thin;
}

.setup-flow__steps::-webkit-scrollbar {
  width: 6px;
}

.setup-flow__steps::-webkit-scrollbar-track {
  background: transparent;
}

.setup-flow__steps::-webkit-scrollbar-thumb {
  border-radius: 999px;
  background: var(--bp-soft);
}

.setup-flow__step {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 9px 0;
  color: var(--bp-soft);
  font-size: 13.5px;
  font-weight: var(--po-weight-bold);
  transition: color 220ms ease;
}

.setup-flow__dot {
  position: relative;
  display: grid;
  flex: none;
  width: 22px;
  height: 22px;
  place-items: center;
  border: 1.5px solid var(--bp-line);
  border-radius: 999px;
  font-size: 11px;
  font-weight: var(--po-weight-strong);
  transition:
    background-color 220ms ease,
    border-color 220ms ease,
    box-shadow 220ms ease;
}

.setup-flow__index {
  transition: opacity 180ms ease;
}

.setup-flow__step.is-active {
  color: var(--bp-ink);
}

.setup-flow__step.is-active .setup-flow__dot {
  border-color: var(--po-coral);
  background: var(--po-coral);
  color: #14284b;
  box-shadow: 0 0 0 3px rgba(255, 255, 255, 0.22);
}

.setup-flow__step.is-done .setup-flow__dot {
  border-color: var(--bp-ink);
  background: var(--bp-ink);
}

.setup-flow__step.is-done .setup-flow__index {
  opacity: 0;
}

.setup-flow__step.is-done .setup-flow__dot::after {
  position: absolute;
  width: 8px;
  height: 4px;
  border-bottom: 2px solid var(--po-accent);
  border-left: 2px solid var(--po-accent);
  content: '';
  transform: translateY(-1px) rotate(-45deg);
}

.setup-flow__hint {
  margin: 2px 0 0;
  color: var(--bp-soft);
  font-size: 12px;
  line-height: 1.7;
}

/* 初始化执行状态：右侧表单替换为转圈 / 完成 */
.setup-state {
  display: grid;
  min-height: 582px;
  place-items: center;
}

.setup-state__item {
  display: grid;
  justify-items: center;
  gap: 12px;
  text-align: center;
}

.setup-state__item strong {
  font-family: var(--po-font-display);
  font-size: 20px;
  font-weight: var(--po-weight-display);
}

.setup-state__item p {
  margin: 0;
  color: var(--po-ink-soft);
  font-size: 12.5px;
  line-height: 1.7;
}

.setup-state__badge {
  display: grid;
  width: 64px;
  height: 64px;
  place-items: center;
  color: var(--po-accent);
  border: var(--po-border);
  border-radius: 20px;
  background: var(--po-accent-soft);
  box-shadow: var(--po-shadow-hard);
}

.setup-state__rail {
  overflow: hidden;
  width: min(240px, 70%);
  height: 6px;
  margin-top: 2px;
  border-radius: 999px;
  background: var(--po-line);
}

.setup-state__rail span {
  display: block;
  height: 100%;
  background: var(--po-coral);
  transition: width 420ms cubic-bezier(0.2, 0.8, 0.2, 1);
}

.setup-state__percent {
  margin-top: -4px;
  color: var(--po-ink);
  font-family: var(--po-font-display);
  font-size: 15px;
  font-weight: var(--po-weight-display);
}

/* 步骤切换动画 */
.setup-step-forward-enter-active,
.setup-step-forward-leave-active,
.setup-step-back-enter-active,
.setup-step-back-leave-active {
  transition:
    opacity 200ms ease,
    transform 200ms cubic-bezier(0.2, 0.8, 0.2, 1);
}

.setup-step-forward-enter-from {
  opacity: 0;
  transform: translateX(20px);
}

.setup-step-forward-leave-to {
  opacity: 0;
  transform: translateX(-20px);
}

.setup-step-back-enter-from {
  opacity: 0;
  transform: translateX(-20px);
}

.setup-step-back-leave-to {
  opacity: 0;
  transform: translateX(20px);
}

.setup-head-swap-enter-active,
.setup-head-swap-leave-active {
  transition:
    opacity 180ms ease,
    transform 180ms ease;
}

.setup-head-swap-enter-from {
  opacity: 0;
  transform: translateY(6px);
}

.setup-head-swap-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}

.setup-flow-swap-enter-active,
.setup-flow-swap-leave-active {
  transition:
    opacity 220ms ease,
    transform 220ms ease;
}

.setup-flow-swap-enter-from {
  opacity: 0;
  transform: translateY(8px);
}

.setup-flow-swap-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

.setup-card-swap-enter-active,
.setup-card-swap-leave-active {
  transition: opacity 200ms ease;
}

.setup-card-swap-enter-from,
.setup-card-swap-leave-to {
  opacity: 0;
}

.setup-state-swap-enter-active,
.setup-state-swap-leave-active {
  transition:
    opacity 220ms ease,
    transform 220ms ease;
}

.setup-state-swap-enter-from {
  opacity: 0;
  transform: translateY(8px);
}

.setup-state-swap-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

:deep(.n-form-item-label) {
  color: var(--po-ink);
  font-size: 13px;
  font-weight: var(--po-weight-strong);
}

@media (min-width: 961px) {
  .setup-header__copy {
    min-height: 76px;
  }

  /* 固定步骤内容高度，切换步骤时按钮位置不跳动 */
  .setup-step {
    min-height: 360px;
  }
}

@media (max-width: 960px) {
  .setup-page {
    grid-template-columns: minmax(0, 1fr);
  }

  .setup-panel {
    min-height: auto;
    padding: 70px 20px 44px;
  }

  .setup-toolbar {
    top: 18px;
    right: 18px;
  }

  .setup-state {
    min-height: 340px;
  }

  .setup-step {
    min-height: 0;
  }
}

@media (max-width: 620px) {
  .setup-panel {
    padding: 40px 16px 36px;
  }

  .setup-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .setup-actions {
    flex-wrap: wrap;
  }

  .setup-back {
    flex: 1;
  }
}

@media (prefers-reduced-motion: reduce) {
  .setup-step-forward-enter-active,
  .setup-step-forward-leave-active,
  .setup-step-back-enter-active,
  .setup-step-back-leave-active,
  .setup-head-swap-enter-active,
  .setup-head-swap-leave-active,
  .setup-flow-swap-enter-active,
  .setup-flow-swap-leave-active,
  .setup-card-swap-enter-active,
  .setup-card-swap-leave-active,
  .setup-state-swap-enter-active,
  .setup-state-swap-leave-active {
    transition-duration: 1ms;
  }
}
</style>

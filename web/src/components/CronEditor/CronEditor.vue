<template>
  <div class="cron-editor">
    <div class="cron-presets">
      <span class="cron-presets-label">{{ t('CronEditor:Presets') }}</span>
      <div class="cron-presets-row">
        <n-button
          v-for="preset in presets"
          :key="preset.key"
          size="tiny"
          :type="activePresetKey === preset.key ? 'primary' : 'default'"
          :disabled="disabled"
          @click="applyPreset(preset.cron)"
        >
          {{ t(preset.labelKey) }}
        </n-button>
        <n-button
          size="tiny"
          :type="isCustomActive ? 'primary' : 'default'"
          :disabled="disabled"
          @click="openCustomModal"
        >
          {{ t('CronEditor:Custom') }}
        </n-button>
      </div>
    </div>

    <p class="cron-description">{{ description }}</p>

    <div class="cron-advanced">
      <n-button
        text
        size="tiny"
        :disabled="disabled"
        @click="isAdvanced = !isAdvanced"
      >
        {{ t('CronEditor:Advanced') }}
      </n-button>
      <div v-if="isAdvanced" class="cron-advanced-body">
        <n-alert
          v-if="parseError"
          type="warning"
          :bordered="false"
          class="cron-alert"
        >
          {{ t('CronEditor:InvalidExpression') }}
        </n-alert>
        <n-input
          :value="model"
          size="small"
          :disabled="disabled"
          :placeholder="t('CronEditor:AdvancedHint')"
          @update:value="onRawInput"
        />
      </div>
    </div>

    <!-- Custom modal -->
    <n-modal v-model:show="showCustomModal" :mask-closable="!disabled">
      <n-card
        class="custom-modal-card"
        :title="t('CronEditor:CustomModal:Title')"
        closable
        role="dialog"
        aria-modal="true"
        @close="onCustomCancel"
      >
        <CronCustomModal
          ref="customModalRef"
          v-model="tempCron"
          :disabled="disabled"
        />
        <template #footer>
          <div class="modal-actions">
            <n-button @click="onCustomCancel">{{ t('Common:Cancel') }}</n-button>
            <n-button type="primary" @click="onCustomConfirm">{{ t('Common:Confirm') }}</n-button>
          </div>
        </template>
      </n-card>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { messageKey, t } from '@/i18n'
import CronCustomModal from './CronCustomModal.vue'

// --- Types ---

interface PresetDef {
  key: string
  labelKey: string
  cron: string
}

// --- Props / Model ---

interface Props {
  disabled?: boolean
}

withDefaults(defineProps<Props>(), { disabled: false })

const model = defineModel<string>({ default: '0 0/5 * * * ?' })

// --- Presets (6, down from 12) ---

const presets: PresetDef[] = [
  { key: 'everyMinute', labelKey: 'CronEditor:Preset:EveryMinute', cron: '0 * * * * ?' },
  { key: 'every5Min', labelKey: 'CronEditor:Preset:Every5Min', cron: '0 0/5 * * * ?' },
  { key: 'everyHour', labelKey: 'CronEditor:Preset:EveryHour', cron: '0 0 * * * ?' },
  { key: 'dailyMidnight', labelKey: 'CronEditor:Preset:DailyMidnight', cron: '0 0 0 * * ?' },
  { key: 'daily9am', labelKey: 'CronEditor:Preset:Daily9am', cron: '0 0 9 * * ?' },
  { key: 'weekdays9am', labelKey: 'CronEditor:Preset:Weekdays9am', cron: '0 0 9 ? * 1-5' },
]

// --- State ---

const isAdvanced = ref(false)
const parseError = ref(false)
const activePresetKey = ref<string | null>(null)
const showCustomModal = ref(false)
const tempCron = ref('0 0/5 * * * ?')
const customModalRef = ref<InstanceType<typeof CronCustomModal> | null>(null)

const isCustomActive = computed(() => showCustomModal.value || activePresetKey.value === null)

// --- Preset matching ---

function detectPreset(expression: string): string | null {
  const normalized = expression.trim()
  const found = presets.find((p) => p.cron === normalized)
  return found ? found.key : null
}

// --- Description ---

const MONTH_NAMES = [
  'january', 'february', 'march', 'april', 'may', 'june',
  'july', 'august', 'september', 'october', 'november', 'december',
] as const

const DAY_KEY_MAP: Record<string, string> = {
  '1': 'sunday', '2': 'monday', '3': 'tuesday', '4': 'wednesday',
  '5': 'thursday', '6': 'friday', '7': 'saturday',
}

function weekdayLabels(range: string): string {
  const m = range.match(/^(\d)-(\d)$/)
  if (m?.[1] && m?.[2]) {
    const start = DAY_KEY_MAP[m[1]]
    const end = DAY_KEY_MAP[m[2]]
    if (start && end) return `${t(messageKey('cronEditor', start))} ~ ${t(messageKey('cronEditor', end))}`
  }
  return range
}

const description = computed(() => {
  if (parseError.value) {
    return t('CronEditor:InvalidExpression')
  }

  const expr = model.value.trim()
  const parts = expr.split(/\s+/)
  if (parts.length !== 6) {
    return expr
  }

  const sec = parts[0]!
  const min = parts[1]!
  const hr = parts[2]!
  const dom = parts[3]!
  const month = parts[4]!
  const dow = parts[5]!

  const parts_desc: string[] = []

  // Time description
  if (sec === '0' && min === '*') {
    parts_desc.push(t('CronEditor:Desc:EveryNMinutes', { n: 1 }))
  } else if (sec === '0' && min.startsWith('0/')) {
    parts_desc.push(t('CronEditor:Desc:EveryNMinutes', { n: min.substring(2) }))
  } else if (sec === '0' && min.startsWith('*/')) {
    parts_desc.push(t('CronEditor:Desc:EveryNMinutes', { n: min.substring(2) }))
  } else {
    const h = hr === '*' ? '0' : hr
    const m = min === '*' ? '0' : (min.includes('/') ? '0' : min)
    const s = sec === '*' ? '0' : (sec.includes('/') ? '0' : sec)
    if (h !== '0' || m !== '0' || s !== '0') {
      parts_desc.push(
        t('CronEditor:Desc:AtTime', { h: h.padStart(2, '0'), m: m.padStart(2, '0'), s: s.padStart(2, '0') }),
      )
    }
  }

  // Day constraints
  if (dow !== '*' && dow !== '?') {
    parts_desc.push(t('CronEditor:Desc:OnDays', { days: weekdayLabels(dow) }))
  } else if (dom !== '*' && dom !== '?') {
    parts_desc.push(t('CronEditor:Desc:OnDayOfMonth', { n: dom }))
  }

  // Month constraint
  if (month !== '*') {
    const mIdx = parseInt(month, 10) - 1
    if (mIdx >= 0 && mIdx < 12) {
      parts_desc.push(t('CronEditor:Desc:InMonth', { month: t(messageKey('cronEditor', MONTH_NAMES[mIdx]!)) }))
    }
  }

  if (parts_desc.length === 0) {
    return expr
  }

  return parts_desc.join('')
})

// --- Validate expression ---

function validateExpression(expression: string): boolean {
  const parts = expression.trim().split(/\s+/)
  return parts.length === 6
}

// --- Handlers ---

function applyPreset(cron: string): void {
  model.value = cron
}

function openCustomModal(): void {
  tempCron.value = model.value
  showCustomModal.value = true
}

function onCustomConfirm(): void {
  const value = customModalRef.value?.getCronValue()
  if (value != null) {
    model.value = value
  }
  showCustomModal.value = false
}

function onCustomCancel(): void {
  showCustomModal.value = false
}

function onRawInput(value: string): void {
  model.value = value
}

// --- Watch model changes ---

watch(
  () => model.value,
  (newVal) => {
    parseError.value = !validateExpression(newVal)
    const matched = detectPreset(newVal)
    activePresetKey.value = matched
    if (matched) {
      isAdvanced.value = false
    }
  },
  { immediate: true },
)
</script>

<style scoped>
.cron-editor {
  display: grid;
  gap: 10px;
}
.cron-presets {
  display: flex;
  align-items: baseline;
  gap: 10px;
  flex-wrap: wrap;
}
.cron-presets-label {
  font-size: 12px;
  color: var(--theme-text-tertiary);
  white-space: nowrap;
  padding-top: 3px;
}
.cron-presets-row {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.cron-description {
  margin: 0;
  font-size: 13px;
  color: var(--theme-text-tertiary);
}
.cron-advanced-body {
  display: grid;
  gap: 8px;
  margin-top: 6px;
}
.cron-alert {
  margin: 0;
}
.custom-modal-card {
  width: min(620px, calc(100vw - 32px));
  max-height: calc(100vh - 32px);
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}
</style>

<template>
  <div class="cron-modal-body">
    <!-- Field rows -->
    <div class="field-rows">
      <div v-for="field in fields" :key="field.name" class="field-row">
        <label class="field-label">{{ t(field.labelKey) }}</label>

        <n-select
          v-model:value="fieldStates[field.name].mode"
          :options="modeOptions(field.name)"
          size="small"
          :consistent-menu-width="false"
          style="width: 80px; flex-shrink: 0"
          :disabled="disabled"
        />

        <!-- Every: no extra inputs -->
        <span v-if="fieldStates[field.name].mode === 'every'" class="field-hint">
          {{ field.name === 'dayOfWeek' && fieldStates.dayOfMonth.mode !== 'every' ? '?' : '*' }}
        </span>

        <!-- Interval: "每 [N] 秒/分/..." -->
        <template v-if="fieldStates[field.name].mode === 'interval'">
          <span class="field-hint">{{ t('CronEditor:FieldMode:Every') }}</span>
          <n-input-number
            v-model:value="fieldStates[field.name].interval"
            :min="1"
            :max="field.max"
            size="small"
            style="width: 80px"
            :disabled="disabled"
          />
          <span class="field-hint">{{ t(field.unitKey) }}</span>
        </template>

        <!-- Specific: comma values or checkboxes for DOW -->
        <template v-if="fieldStates[field.name].mode === 'specific'">
          <template v-if="field.name === 'dayOfWeek'">
            <n-checkbox-group v-model:value="fieldStates.dayOfWeek.specificValues" :disabled="disabled">
              <n-checkbox
                v-for="day in weekDayOptions"
                :key="day.value"
                :value="day.value"
                size="small"
              >
                {{ t(day.labelKey).substring(1) }}
              </n-checkbox>
            </n-checkbox-group>
          </template>
          <template v-else>
            <n-dynamic-tags
              v-model:value="fieldStates[field.name].specificTags"
              :disabled="disabled"
              :input-style="{ width: '100px' }"
            />
            <span class="field-hint field-hint-subtle">
              {{ field.min }}-{{ field.max }}
            </span>
          </template>
        </template>

        <!-- Range: "从 [X] 到 [Y] 步长 [S]" -->
        <template v-if="fieldStates[field.name].mode === 'range'">
          <span class="field-hint">{{ t('CronEditor:FieldMode:From') }}</span>
          <n-input-number
            v-model:value="fieldStates[field.name].rangeFrom"
            :min="field.min"
            :max="field.max"
            size="small"
            style="width: 80px"
            :disabled="disabled"
          />
          <span class="field-hint">{{ t('CronEditor:FieldMode:To') }}</span>
          <n-input-number
            v-model:value="fieldStates[field.name].rangeTo"
            :min="field.min"
            :max="field.max"
            size="small"
            style="width: 80px"
            :disabled="disabled"
          />
          <span class="field-hint">{{ t('CronEditor:FieldMode:Step') }}</span>
          <n-input-number
            v-model:value="fieldStates[field.name].rangeStep"
            :min="1"
            :max="field.max"
            size="small"
            style="width: 80px"
            :disabled="disabled"
            :placeholder="t('CronEditor:FieldMode:Optional')"
          />
        </template>
      </div>
    </div>

    <!-- Preview -->
    <div class="cron-preview">
      <div class="preview-row">
        <span class="preview-label">{{ t('CronEditor:CustomModal:Preview') }}</span>
        <code>{{ previewCron }}</code>
      </div>
      <div class="preview-row">
        <span class="preview-label">{{ t('Common:Description') }}</span>
        <span class="preview-desc">{{ previewDesc }}</span>
      </div>
    </div>

    <!-- Next runs -->
    <div class="cron-next-runs" v-if="nextRuns.times.length > 0 && !nextRuns.error">
      <div class="next-runs-label">{{ t('CronEditor:NextRuns') }}</div>
      <div class="next-runs-list">
        <div v-for="(time, i) in nextRuns.times" :key="i" class="next-run-item">
          <n-tag size="small" :bordered="false" type="info">{{ i + 1 }}</n-tag>
          <code>{{ formatRunTime(time) }}</code>
        </div>
      </div>
    </div>
    <div v-else-if="nextRuns.error" class="cron-next-runs cron-next-runs-error">
      <span class="preview-desc">{{ nextRuns.error }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { t } from '@/i18n'
import { getNextRuns } from '@/utils/cronNextRun'

// --- Types ---

type FieldMode = 'every' | 'interval' | 'specific' | 'range'
type FieldName = 'seconds' | 'minutes' | 'hours' | 'dayOfMonth' | 'month' | 'dayOfWeek'

interface FieldDef {
  name: FieldName
  labelKey: string
  unitKey: string
  min: number
  max: number
}

interface FieldState {
  mode: FieldMode
  /** Interval mode: every N */
  interval: number
  /** Specific mode: comma-separated tag values (for text-based fields) */
  specificTags: string[]
  /** Specific mode: selected day values (for DOW) */
  specificValues: number[]
  /** Range mode */
  rangeFrom: number
  rangeTo: number
  rangeStep: number | null
}

interface Props {
  modelValue: string
  disabled?: boolean
}

// --- Constants ---

const fields: FieldDef[] = [
  { name: 'seconds', labelKey: 'CronEditor:Field:Seconds', unitKey: 'CronEditor:Unit:Seconds', min: 0, max: 59 },
  { name: 'minutes', labelKey: 'CronEditor:Field:Minutes', unitKey: 'CronEditor:Unit:Minutes', min: 0, max: 59 },
  { name: 'hours', labelKey: 'CronEditor:Field:Hours', unitKey: 'CronEditor:Unit:Hours', min: 0, max: 23 },
  { name: 'dayOfMonth', labelKey: 'CronEditor:Field:DayOfMonth', unitKey: 'CronEditor:Unit:Days', min: 1, max: 31 },
  { name: 'month', labelKey: 'CronEditor:Field:Month', unitKey: 'CronEditor:Unit:Months', min: 1, max: 12 },
  { name: 'dayOfWeek', labelKey: 'CronEditor:Field:DayOfWeek', unitKey: 'CronEditor:Unit:Weekdays', min: 1, max: 7 },
]

const weekDayOptions = [
  { value: 2, labelKey: 'CronEditor:Monday' },
  { value: 3, labelKey: 'CronEditor:Tuesday' },
  { value: 4, labelKey: 'CronEditor:Wednesday' },
  { value: 5, labelKey: 'CronEditor:Thursday' },
  { value: 6, labelKey: 'CronEditor:Friday' },
  { value: 7, labelKey: 'CronEditor:Saturday' },
  { value: 1, labelKey: 'CronEditor:Sunday' },
]

const WEEKDAY_KEYS: Record<number, string> = {
  1: 'CronEditor:Sunday',
  2: 'CronEditor:Monday',
  3: 'CronEditor:Tuesday',
  4: 'CronEditor:Wednesday',
  5: 'CronEditor:Thursday',
  6: 'CronEditor:Friday',
  7: 'CronEditor:Saturday',
}

const MONTH_KEYS: Record<number, string> = {
  1: 'CronEditor:January', 2: 'CronEditor:February', 3: 'CronEditor:March',
  4: 'CronEditor:April', 5: 'CronEditor:May', 6: 'CronEditor:June',
  7: 'CronEditor:July', 8: 'CronEditor:August', 9: 'CronEditor:September',
  10: 'CronEditor:October', 11: 'CronEditor:November', 12: 'CronEditor:December',
}

// --- Props ---

const props = withDefaults(defineProps<Props>(), { disabled: false })

// --- State ---

const fieldStates = reactive<Record<FieldName, FieldState>>({
  // Default to 'specific 0' so cron fires ONCE per interval (e.g., "every 7 min" = at :00 second, not every second)
  seconds: { mode: 'specific', interval: 1, specificTags: ['0'], specificValues: [], rangeFrom: 0, rangeTo: 59, rangeStep: null },
  minutes: { mode: 'interval', interval: 5, specificTags: [], specificValues: [], rangeFrom: 0, rangeTo: 59, rangeStep: null },
  hours: { mode: 'every', interval: 1, specificTags: [], specificValues: [], rangeFrom: 0, rangeTo: 23, rangeStep: null },
  dayOfMonth: { mode: 'every', interval: 1, specificTags: [], specificValues: [], rangeFrom: 1, rangeTo: 31, rangeStep: null },
  month: { mode: 'every', interval: 1, specificTags: [], specificValues: [], rangeFrom: 1, rangeTo: 12, rangeStep: null },
  dayOfWeek: { mode: 'every', interval: 1, specificTags: [], specificValues: [2, 3, 4, 5, 6], rangeFrom: 1, rangeTo: 7, rangeStep: null },
})

// When minutes switches to interval mode N>1, auto-correct seconds to '0' (specific) so cron fires once per interval
watch(
  () => [fieldStates.minutes.mode, fieldStates.minutes.interval] as const,
  ([mode, interval]) => {
    if (mode === 'interval' && interval > 1 && fieldStates.seconds.mode === 'every') {
      fieldStates.seconds.mode = 'specific'
      fieldStates.seconds.specificTags = ['0']
    }
  },
)

// --- Mode options ---

function modeOptions(fieldName: FieldName) {
  const options: Array<{ label: string; value: FieldMode }> = [
    { label: t('CronEditor:FieldMode:Every'), value: 'every' },
    { label: t('CronEditor:FieldMode:Interval'), value: 'interval' },
    { label: t('CronEditor:FieldMode:Specific'), value: 'specific' },
    { label: t('CronEditor:FieldMode:Range'), value: 'range' },
  ]
  void fieldName // placeholder; behavior is identical for DOW vs others
  return options
}

// --- Build cron expression from field states ---

function buildFieldCron(state: FieldState, fieldName: FieldName): string {
  switch (state.mode) {
    case 'every':
      // DOM/DOW auto-handling
      if (fieldName === 'dayOfMonth' && fieldStates.dayOfWeek.mode !== 'every') return '?'
      if (fieldName === 'dayOfWeek' && fieldStates.dayOfMonth.mode !== 'every') return '?'
      return '*'

    case 'interval': {
      const iv = state.interval || 1
      return iv === 1 ? '*' : `*/${iv}`
    }

    case 'specific': {
      if (fieldName === 'dayOfWeek') {
        const vals = [...state.specificValues].sort((a, b) => a - b)
        return vals.length > 0 ? vals.join(',') : '*'
      }
      // Parse tags to numbers
      const nums = state.specificTags
        .map((t) => parseInt(t, 10))
        .filter((n) => !isNaN(n))
        .sort((a, b) => a - b)
      if (nums.length === 0) return '*'
      return nums.join(',')
    }

    case 'range': {
      const from = state.rangeFrom
      const to = state.rangeTo
      const step = state.rangeStep
      const base = `${from}-${to}`
      return step && step > 1 ? `${base}/${step}` : base
    }
  }
}

const previewCron = computed<string>(() => {
  return fields.map((f) => buildFieldCron(fieldStates[f.name], f.name)).join(' ')
})

// --- Human-readable description ---

const previewDesc = computed<string>(() => {
  const parts: string[] = []

  // Helper: is this field effectively "every"?
  const isEvery = (mode: FieldMode) => mode === 'every'
  const isEveryDayOfMonth = isEvery(fieldStates.dayOfMonth.mode)
  const isEveryMonth = isEvery(fieldStates.month.mode)
  const isEveryDayOfWeek = isEvery(fieldStates.dayOfWeek.mode)

  // "Recurring day" = cron fires every day (no month/day restriction); only hour/min/sec matter
  const isRecurring = isEveryDayOfMonth && isEveryDayOfWeek && isEveryMonth
  // "Annual" = month is constrained → cron fires once per year (specific month/day combination)
  const isAnnual = !isEveryMonth
  // Whether DOM is constrained but month is not → monthly pattern
  const monthConstrained = !isEveryMonth

  // --- Frequency (first) ---
  if (isAnnual) {
    parts.push(t('CronEditor:Desc:EveryYear'))
  }

  // --- Date clauses (middle) ---
  // Day-of-week constraint
  if (fieldStates.dayOfWeek.mode === 'specific' && fieldStates.dayOfWeek.specificValues.length > 0) {
    const dayLabels = [...fieldStates.dayOfWeek.specificValues]
      .sort((a, b) => a - b)
      .map((d) => t(WEEKDAY_KEYS[d] ?? 'CronEditor:Monday'))
    parts.push(t('CronEditor:Desc:OnDays', { days: dayLabels.join('、') }))
  } else if (fieldStates.dayOfWeek.mode === 'range') {
    const dw = fieldStates.dayOfWeek
    const start = WEEKDAY_KEYS[dw.rangeFrom]
    const end = WEEKDAY_KEYS[dw.rangeTo]
    parts.push(t('CronEditor:Desc:OnDays', { days: start && end ? `${t(start)} ~ ${t(end)}` : `${dw.rangeFrom}-${dw.rangeTo}` }))
  }

  // Combine month + day-of-month when both are constrained and specific (e.g., "六月 15 日")
  const monthHasSpecific =
    fieldStates.month.mode === 'specific' && fieldStates.month.specificTags.length > 0
  const domHasSpecific =
    fieldStates.dayOfMonth.mode === 'specific' && fieldStates.dayOfMonth.specificTags.length > 0
  const domHasRange = fieldStates.dayOfMonth.mode === 'range'
  const monthHasRange = fieldStates.month.mode === 'range'

  if (monthHasSpecific && domHasSpecific) {
    // Combined: "六月 15 日"
    const monthLabels = fieldStates.month.specificTags
      .map((tag) => {
        const n = parseInt(tag, 10)
        if (isNaN(n)) return tag
        return MONTH_KEYS[n] ? t(MONTH_KEYS[n]!) : tag
      })
    parts.push(t('CronEditor:Desc:MonthDay', {
      month: monthLabels.join('、'),
      day: fieldStates.dayOfMonth.specificTags.join('、'),
    }))
  } else {
    // Month constraint alone
    if (monthHasSpecific) {
      const monthLabels = fieldStates.month.specificTags
        .map((tag) => {
          const n = parseInt(tag, 10)
          if (isNaN(n)) return tag
          return MONTH_KEYS[n] ? t(MONTH_KEYS[n]!) : tag
        })
      parts.push(t('CronEditor:Desc:InMonths', { months: monthLabels.join('、') }))
    } else if (monthHasRange) {
      const ms2 = fieldStates.month
      const start = MONTH_KEYS[ms2.rangeFrom]
      const end = MONTH_KEYS[ms2.rangeTo]
      parts.push(t('CronEditor:Desc:InMonths', {
        months: start && end ? `${t(start)} ~ ${t(end)}` : `${ms2.rangeFrom}-${ms2.rangeTo}`,
      }))
    }

    // Day-of-month constraint alone
    if (domHasSpecific) {
      const dayKey = monthConstrained
        ? 'CronEditor:Desc:DayOfMonth'
        : 'CronEditor:Desc:OnDayOfMonth'
      parts.push(t(dayKey, { n: fieldStates.dayOfMonth.specificTags.join(',') }))
    } else if (domHasRange) {
      const dm = fieldStates.dayOfMonth
      parts.push(t('CronEditor:Desc:DayOfMonthRange', { from: dm.rangeFrom, to: dm.rangeTo }))
    }
  }

  // --- Time-of-day (last) ---
  const hs = fieldStates.hours
  const ms = fieldStates.minutes

  const showSingleTime =
    ms.mode === 'specific' && ms.specificTags.length === 1 &&
    hs.mode === 'specific' && hs.specificTags.length === 1

  const showMultipleTimes =
    ms.mode === 'specific' && ms.specificTags.length > 0 &&
    hs.mode === 'specific' && hs.specificTags.length > 0 &&
    !(ms.specificTags.length === 1 && hs.specificTags.length === 1)

  if (showSingleTime) {
    const timeKey = isRecurring
      ? 'CronEditor:Desc:AtTimeEveryDay'
      : 'CronEditor:Desc:AtTime'
    parts.push(t(timeKey, {
      h: hs.specificTags[0]!.padStart(2, '0'),
      m: ms.specificTags[0]!.padStart(2, '0'),
    }))
  } else if (showMultipleTimes) {
    const hourList = hs.specificTags.join(',')
    const minList = ms.specificTags.join(',')
    parts.push(t('CronEditor:Desc:AtTime', { h: hourList, m: minList }))
  } else if (ms.mode === 'interval' && ms.interval > 1) {
    parts.push(t('CronEditor:Desc:EveryNMinutes', { n: ms.interval }))
  } else if (hs.mode === 'interval' && hs.interval > 1) {
    parts.push(t('CronEditor:Desc:EveryNHours', { n: hs.interval }))
  }

  // --- Seconds — only show when non-default (not 'specific 0') ---
  const ss = fieldStates.seconds
  const isDefaultSeconds =
    ss.mode === 'specific' &&
    ss.specificTags.length === 1 &&
    ss.specificTags[0] === '0'
  if (!isDefaultSeconds) {
    if (ss.mode === 'interval' && ss.interval > 1) {
      parts.push(t('CronEditor:Desc:EveryNSeconds', { n: ss.interval }))
    } else if (ss.mode === 'specific' && ss.specificTags.length > 0) {
      parts.push(t('CronEditor:Desc:AtSeconds', { s: ss.specificTags.join(',') }))
    } else if (ss.mode === 'range') {
      const step = ss.rangeStep && ss.rangeStep > 1 ? `/${ss.rangeStep}` : ''
      parts.push(t('CronEditor:Desc:SecondsRange', { from: ss.rangeFrom, to: ss.rangeTo, step }))
    }
  }

  if (parts.length === 0) {
    return t('CronEditor:Desc:Default')
  }

  // Join with separator; only the first part should not have a leading separator
  const separator = t('CronEditor:Desc:Separator')
  return parts.reduce((acc, part, idx) => idx === 0 ? part : acc + separator + part, '')
})

// --- Next runs ---

const nextRuns = ref(getNextRuns(previewCron.value, 3))

watch(previewCron, (cron) => {
  nextRuns.value = getNextRuns(cron, 3)
})

function formatRunTime(iso: string): string {
  const d = new Date(iso)
  const y = d.getFullYear()
  const mo = String(d.getMonth() + 1).padStart(2, '0')
  const da = String(d.getDate()).padStart(2, '0')
  const h = String(d.getHours()).padStart(2, '0')
  const mi = String(d.getMinutes()).padStart(2, '0')
  const s = String(d.getSeconds()).padStart(2, '0')
  return `${y}-${mo}-${da} ${h}:${mi}:${s}`
}

// --- Tab detection from cron expression (auto-fill field states) ---

function detectFromCron(expression: string): void {
  const parts = expression.trim().split(/\s+/)
  if (parts.length !== 6) return

  for (let i = 0; i < 6; i++) {
    const field = fields[i]!
    const state = fieldStates[field.name]
    const part = parts[i]!

    if (part === '*' || part === '?') {
      state.mode = 'every'
      continue
    }

    // Range with or without step: X-Y or X-Y/S
    const rangeWithStepMatch = part.match(/^(\d+)-(\d+)\/(\d+)$/)
    if (rangeWithStepMatch) {
      state.mode = 'range'
      state.rangeFrom = parseInt(rangeWithStepMatch[1]!, 10)
      state.rangeTo = parseInt(rangeWithStepMatch[2]!, 10)
      state.rangeStep = parseInt(rangeWithStepMatch[3]!, 10)
      continue
    }

    // Plain range: X-Y
    const rangeMatch = part.match(/^(\d+)-(\d+)$/)
    if (rangeMatch) {
      state.mode = 'range'
      state.rangeFrom = parseInt(rangeMatch[1]!, 10)
      state.rangeTo = parseInt(rangeMatch[2]!, 10)
      state.rangeStep = null
      continue
    }

    // Interval: */N
    const intervalMatch = part.match(/^\*\/(\d+)$/)
    if (intervalMatch) {
      state.mode = 'interval'
      state.interval = parseInt(intervalMatch[1]!, 10)
      continue
    }

    // Comma-separated list
    if (part.includes(',')) {
      state.mode = 'specific'
      if (field.name === 'dayOfWeek') {
        state.specificValues = part.split(',').map((s) => parseInt(s, 10)).filter((n) => !isNaN(n))
      } else {
        state.specificTags = part.split(',')
      }
      continue
    }

    // Single specific value
    if (/^\d+$/.test(part)) {
      state.mode = 'specific'
      const n = parseInt(part, 10)
      if (field.name === 'dayOfWeek') {
        state.specificValues = [n]
      } else {
        state.specificTags = [part]
      }
      continue
    }

    // Fallback: treat as-is (every)
    state.mode = 'every'
  }

  // Normalize: if hours+minute are specific (e.g., "every hour at minute 20") but seconds is '*' (= every second),
  // that's almost always a mistake — auto-set seconds to '0' so it fires once per minute, not 60 times per minute.
  // Exception: user explicitly chose seconds=every AND minutes is interval N>1 with every-second bursts.
  normalizeSecondsIfTopOfMinute()
}

/** If the cron expresses "at minute M every hour" and seconds is '*', set seconds to 0 */
function normalizeSecondsIfTopOfMinute(): void {
  const ss = fieldStates.seconds
  if (ss.mode !== 'every') return

  // If seconds is '*' but the user has any other field at a non-every/non-every-interval value that
  // makes the cron less than per-second, auto-correct seconds to '0'.
  // Specifically: if hours is 'every' AND minutes is anything specific/range OR interval>1,
  // OR if any other field is non-every → safe to assume seconds should be 0.
  const minutes = fieldStates.minutes
  const hasMinutesSpec =
    minutes.mode === 'specific' ||
    minutes.mode === 'range' ||
    (minutes.mode === 'interval' && minutes.interval > 1)

  if (hasMinutesSpec) {
    ss.mode = 'specific'
    ss.specificTags = ['0']
    return
  }

  // hours is specific/range → also safe
  const hours = fieldStates.hours
  if (hours.mode === 'specific' || hours.mode === 'range') {
    ss.mode = 'specific'
    ss.specificTags = ['0']
  }
}

// --- Watch external modelValue ---

watch(
  () => props.modelValue,
  (val) => detectFromCron(val),
  { immediate: true },
)

// --- Expose ---

defineExpose({ getCronValue: () => previewCron.value })
</script>

<style scoped>
.cron-modal-body {
  display: grid;
  gap: 16px;
}

.field-rows {
  display: grid;
  gap: 10px;
}

.field-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.field-label {
  width: 28px;
  font-size: 13px;
  font-weight: 600;
  color: var(--theme-text-primary);
  flex-shrink: 0;
  text-align: right;
}

.field-hint {
  font-size: 13px;
  color: var(--theme-text-secondary);
  white-space: nowrap;
}

.field-hint-subtle {
  font-size: 11px;
  color: var(--theme-text-tertiary);
}

.cron-preview {
  padding: 14px 18px;
  border-radius: 10px;
  background: var(--admin-surface-muted);
  display: grid;
  gap: 8px;
}

.preview-row {
  display: flex;
  align-items: baseline;
  gap: 10px;
}

.preview-label {
  font-size: 12px;
  color: var(--theme-text-tertiary);
  min-width: 44px;
  flex-shrink: 0;
}

.preview-row code {
  font-size: 14px;
  font-weight: 600;
  color: var(--primary-color);
  font-family: var(--po-font-mono);
}

.preview-desc {
  font-size: 13px;
  color: var(--theme-text-secondary);
}

.cron-next-runs {
  padding: 14px 18px;
  border-radius: 10px;
  background: var(--admin-surface-muted);
}

.cron-next-runs-error {
  opacity: 0.7;
}

.next-runs-label {
  font-size: 12px;
  color: var(--theme-text-tertiary);
  margin-bottom: 8px;
}

.next-runs-list {
  display: grid;
  gap: 6px;
}

.next-run-item {
  display: flex;
  align-items: center;
  gap: 10px;
}

.next-run-item code {
  font-size: 13px;
  font-family: var(--po-font-mono);
  color: var(--theme-text-primary);
}
</style>

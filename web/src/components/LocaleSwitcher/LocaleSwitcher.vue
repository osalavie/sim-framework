<template>
  <button
    v-if="compact"
    class="locale-switcher__compact po-icon-btn"
    type="button"
    :aria-label="t('Locale:Language')"
    v-tooltip="currentLabel"
    :lang="locale"
    @click="cycleLocale"
  >
    {{ currentShort }}
  </button>
  <SegmentedSwitch
    v-else
    :model-value="locale"
    :options="options"
    :aria-label="t('Locale:Language')"
    @update:model-value="handleSelect"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { locale, localeOptions, setLocale, t, type AppLocale } from '@/i18n'

withDefaults(defineProps<{ compact?: boolean }>(), { compact: false })

const shortLabels: Record<AppLocale, string> = {
  'zh-CN': '简',
  'en-US': 'EN',
}

const options = computed(() =>
  localeOptions.value.map((option) => ({
    ...option,
    short: shortLabels[option.value],
  })),
)

const currentLabel = computed(
  () => localeOptions.value.find((option) => option.value === locale.value)?.label ?? '',
)
const currentShort = computed(() => shortLabels[locale.value])

function handleSelect(value: AppLocale) {
  if (value !== locale.value) void setLocale(value)
}

/** 窄屏只保留一个圆形入口：点击按选项顺序切到下一种语言 */
function cycleLocale() {
  const values = localeOptions.value
  const index = values.findIndex((option) => option.value === locale.value)
  const next = values[(index + 1) % values.length]
  if (next) handleSelect(next.value)
}
</script>

<style scoped>
.locale-switcher__compact {
  color: var(--po-ink-soft);
  font-size: 12px;
  font-weight: var(--po-weight-bold);
  line-height: 1;
  letter-spacing: 0.02em;
}

.locale-switcher__compact:hover {
  color: var(--po-ink);
}
</style>

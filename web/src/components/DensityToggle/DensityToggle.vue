<template>
  <SegmentedSwitch
    class="density-toggle"
    :model-value="density"
    :options="options"
    :aria-label="t('Density:Label')"
    @update:model-value="handleSelect"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { locale, t } from '@/i18n'
import { useDensityStore, type ContentDensity } from '@/stores/density'

const densityStore = useDensityStore()
const density = computed(() => densityStore.density)

// 短标签用「小 / 中 / 大」，气泡与无障碍名称保留完整档位描述
const options = computed(() => [
  {
    label: t('Density:Compact'),
    value: 'compact' as const,
    short: locale.value === 'zh-CN' ? '小' : 'S',
  },
  {
    label: t('Density:Standard'),
    value: 'standard' as const,
    short: locale.value === 'zh-CN' ? '中' : 'M',
  },
  {
    label: t('Density:Roomy'),
    value: 'roomy' as const,
    short: locale.value === 'zh-CN' ? '大' : 'L',
  },
])

function handleSelect(value: ContentDensity) {
  if (value !== density.value) densityStore.setDensity(value)
}
</script>

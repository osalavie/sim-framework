<template>
  <div class="theme-picker">
    <div>
      <p class="theme-picker__label">{{ t('Theme:Style') }}</p>
      <div class="theme-picker__grid" role="radiogroup" :aria-label="t('Theme:Style')"
        @keydown="handleGridKeydown">
        <button v-for="style in themeStyles" :key="style.id" ref="cardRefs" type="button"
          class="theme-card" :class="{ 'is-active': style.id === activeStyleId }" role="radio"
          :aria-checked="style.id === activeStyleId" :tabindex="style.id === activeStyleId ? 0 : -1"
          @click="selectStyle(style.id)">
          <span class="theme-card__preview" aria-hidden="true">
            <span v-for="(color, band) in previewOf(style)" :key="band" :style="{ background: color }" />
            <span v-if="style.id === activeStyleId" class="theme-card__check">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"
                stroke-linejoin="round">
                <polyline points="20 6 9 17 4 12" />
              </svg>
            </span>
          </span>
          <span class="theme-card__copy">
            <span class="theme-card__name">{{ t(style.labelKey) }}</span>
            <span class="theme-card__desc">{{ t(style.descriptionKey) }}</span>
          </span>
        </button>
      </div>
    </div>

    <div class="theme-picker__mode">
      <span class="theme-picker__label">{{ t('Theme:Mode') }}</span>
      <SegmentedSwitch :model-value="mode" :options="modeOptions" :aria-label="t('Theme:Mode')"
        @update:model-value="selectMode" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { t } from '@/i18n'
import { themeStyles, type ThemeMode, type ThemeStyleDefinition } from '@/config/themes'
import { useThemeStore } from '@/stores/theme'

const themeStore = useThemeStore()
const activeStyleId = computed(() => themeStore.styleDefinition.id)
const mode = computed<ThemeMode>(() => themeStore.mode)
const modeOptions = computed(() => [
  { label: t('Theme:LightMode'), value: 'light' as const, short: t('Theme:LightModeShort') },
  { label: t('Theme:DarkMode'), value: 'dark' as const, short: t('Theme:DarkModeShort') },
  { label: t('Theme:FollowSystem'), value: 'system' as const, short: t('Theme:FollowSystemShort') },
])
const cardRefs = ref<HTMLButtonElement[]>([])

/** 预览跟随当前解析后的明暗：深色（含系统深色）取同一风格的夜航色。 */
const previewOf = (style: ThemeStyleDefinition) =>
  themeStore.isDark
    ? [style.dark.bg, style.dark.primary, style.dark.sun]
    : style.preview

const selectStyle = (id: string) => themeStore.setThemeStyle(id)

const selectMode = (value: ThemeMode) => themeStore.setThemeMode(value)

/** 单选组键盘行为：方向键移动选中项，Home / End 跳到首尾。 */
const handleGridKeydown = (event: KeyboardEvent) => {
  const handled = ['ArrowRight', 'ArrowDown', 'ArrowLeft', 'ArrowUp', 'Home', 'End']
  if (!handled.includes(event.key)) return
  event.preventDefault()

  const current = themeStyles.findIndex((style) => style.id === activeStyleId.value)
  const last = themeStyles.length - 1
  let next = current
  if (event.key === 'Home') next = 0
  else if (event.key === 'End') next = last
  else if (event.key === 'ArrowRight' || event.key === 'ArrowDown') next = current >= last ? 0 : current + 1
  else next = current <= 0 ? last : current - 1

  themeStore.setThemeStyle(themeStyles[next]!.id)
  void nextTick(() => cardRefs.value[next]?.focus())
}
</script>

<style scoped>
.theme-picker {
  display: grid;
  gap: 18px;
}

.theme-picker__label {
  margin: 0 0 8px;
  color: var(--po-ink-soft);
  font-size: 12.5px;
  font-weight: var(--po-weight-bold);
}

.theme-picker__grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.theme-card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
  padding: 8px;
  border: var(--po-border);
  border-radius: var(--po-r);
  background: var(--po-paper);
  box-shadow: var(--po-shadow-card);
  color: var(--po-ink);
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition:
    border-color 160ms ease,
    box-shadow 160ms ease;
}

.theme-card:hover {
  box-shadow: var(--po-shadow-hover);
}

.theme-card.is-active {
  border-color: var(--po-accent);
}

.theme-card:focus-visible {
  outline: 3px solid var(--po-accent);
  outline-offset: 3px;
}

.theme-card__preview {
  position: relative;
  display: flex;
  height: 46px;
  overflow: hidden;
  border: 1px solid var(--po-line);
  border-radius: var(--po-r-sm);
}

.theme-card__preview > span:not(.theme-card__check) {
  flex: 1;
}

.theme-card__check {
  position: absolute;
  top: 6px;
  right: 6px;
  display: grid;
  place-items: center;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: var(--po-accent);
  color: var(--po-on-primary);
  box-shadow: 0 0 0 1px var(--po-paper);
}

.theme-card__check svg {
  width: 12px;
  height: 12px;
}

.theme-card__copy {
  display: grid;
  gap: 1px;
  min-width: 0;
}

.theme-card__name {
  overflow: hidden;
  font-size: 12.5px;
  font-weight: var(--po-weight-bold);
  line-height: 1.4;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.theme-card__desc {
  overflow: hidden;
  color: var(--po-ink-faint);
  font-size: 11.5px;
  line-height: 1.4;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.theme-picker__mode {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding-top: 14px;
  border-top: 1px dashed var(--po-line);
}

.theme-picker__mode .theme-picker__label {
  margin: 0;
}
</style>

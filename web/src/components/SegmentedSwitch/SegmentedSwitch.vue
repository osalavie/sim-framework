<template>
  <div
    class="segmented-switch"
    role="group"
    :aria-label="ariaLabel"
    :style="{ '--at': highlightIndex, '--count': options.length }"
    @mouseleave="handleLeave"
  >
    <button
      v-for="(option, index) in options"
      :key="option.value"
      type="button"
      class="segmented-switch__item"
      :class="{ 'is-highlighted': index === highlightIndex }"
      :aria-pressed="option.value === modelValue"
      :aria-label="option.label"
      v-tooltip="option.label"
      :lang="option.lang"
      @click="handleSelect(option.value)"
      @mouseenter="handleHover(index)"
      @focus="handleHover(index)"
      @blur="handleLeave"
    >
      {{ option.short ?? option.label }}
    </button>
  </div>
</template>

<script setup lang="ts" generic="T extends string | number">
import { computed, ref } from 'vue'

const props = withDefaults(
  defineProps<{
    modelValue: T
    options: Array<{ label: string; value: T; short?: string; lang?: string }>
    ariaLabel?: string
    hoverEffect?: boolean
  }>(),
  { ariaLabel: undefined, hoverEffect: true },
)

const emit = defineEmits<{ 'update:modelValue': [value: T] }>()

const hoverIndex = ref(-1)
const activeIndex = computed(() => Math.max(props.options.findIndex((o) => o.value === props.modelValue), 0))
const highlightIndex = computed(() =>
  props.hoverEffect && hoverIndex.value >= 0 ? hoverIndex.value : activeIndex.value,
)

function handleSelect(value: T) {
  if (value !== props.modelValue) emit('update:modelValue', value)
}

function handleHover(index: number) {
  if (props.hoverEffect) hoverIndex.value = index
}

function handleLeave() {
  hoverIndex.value = -1
}
</script>

<style scoped>
.segmented-switch {
  --at: 0;
  --count: 2;

  position: relative;
  isolation: isolate;
  display: grid;
  width: max-content;
  grid-template-columns: repeat(var(--count), minmax(36px, 1fr));
  gap: 2px;
  padding: 3px;
  border: var(--po-border);
  border-radius: 999px;
  background: var(--po-bg-deep);
  box-shadow: var(--po-shadow-card);
}

/* 滑块宽度按选项数量均分，位置由 --at 驱动；悬停时移动到悬停项，离开后回到选中项 */
.segmented-switch::before {
  content: '';
  position: absolute;
  z-index: -1;
  top: 3px;
  bottom: 3px;
  left: 3px;
  width: calc((100% - 6px - (var(--count) - 1) * 2px) / var(--count));
  border-radius: 999px;
  background: var(--po-ink);
  transform: translateX(calc(var(--at) * (100% + 2px)));
  transition: transform 0.35s cubic-bezier(0.5, 1.6, 0.4, 1);
}

.segmented-switch__item {
  position: relative;
  min-width: 36px;
  padding: 2px 10px;
  border: 0;
  border-radius: 999px;
  background: transparent;
  color: var(--po-ink-soft);
  font-size: 13px;
  font-weight: var(--po-weight-bold);
  line-height: 1.5;
  text-align: center;
  cursor: pointer;
  transition: color 0.3s ease;
}

.segmented-switch__item.is-highlighted {
  color: var(--po-paper);
}

@media (prefers-reduced-motion: reduce) {
  .segmented-switch::before {
    transition: none;
  }
}
</style>

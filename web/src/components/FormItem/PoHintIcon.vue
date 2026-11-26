<template>
  <n-popover
    :show="visible"
    trigger="manual"
    :placement="placement"
    :show-arrow="true"
    :style="popoverStyle"
  >
    <template #trigger>
      <button
        ref="triggerRef"
        type="button"
        class="po-hint-icon"
        :aria-label="ariaLabel"
        :aria-expanded="visible"
        @mouseenter="handleEnter"
        @mouseleave="handleLeave"
        @click.stop.prevent="handleToggle"
        @focus="handleFocus"
        @blur="handleBlur"
        @keydown.esc="close"
      >
        <Icon name="carbon:Information" :size="14" />
      </button>
    </template>
    <span class="po-hint-icon__content">{{ text }}</span>
  </n-popover>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import Icon from '@/components/Icon/Icon.vue'
import { useMediaQuery } from '@/hooks/useResponsive'
import { t } from '@/i18n'

const props = withDefaults(defineProps<{ text: string; ariaLabel?: string }>(), {
  ariaLabel: undefined,
})

const triggerRef = ref<HTMLButtonElement | null>(null)
const visible = ref(false)
const pinned = ref(false)
const ariaLabel = computed(() => props.ariaLabel ?? t('Common:FieldHint'))
const isNarrow = useMediaQuery('(max-width: 640px)')
const placement = ref<'top' | 'top-start' | 'top-end'>('top')
const popoverWidth = ref(320)
// 宽度不够时先收窄让文字换行，仍放不下就由 CSS 多行截断；锚点一侧空间不足时改用 start/end 对齐
function measurePopover() {
  const trigger = triggerRef.value
  if (!trigger || typeof window === 'undefined') return
  const rect = trigger.getBoundingClientRect()
  const base = isNarrow.value ? 224 : 320
  const leftSpace = rect.right
  const rightSpace = window.innerWidth - rect.left
  const alignStart = rightSpace >= leftSpace
  const available = Math.max(160, (alignStart ? rightSpace : leftSpace) - 12)
  popoverWidth.value = Math.round(Math.min(base, available))
  placement.value = alignStart ? 'top-start' : 'top-end'
}

const popoverStyle = computed(() => ({
  maxWidth: `${popoverWidth.value}px`,
  width: 'max-content',
}))

function open() {
  measurePopover()
  visible.value = true
}

function handleEnter() {
  open()
}

function handleLeave() {
  if (!pinned.value) visible.value = false
}

function handleToggle() {
  pinned.value = !pinned.value
  if (pinned.value) {
    open()
  } else {
    visible.value = false
  }
}

function handleFocus() {
  measurePopover()
  visible.value = true
}

function handleBlur() {
  if (!pinned.value) visible.value = false
}

function close() {
  pinned.value = false
  visible.value = false
}

function handleDocumentPointerDown(event: PointerEvent) {
  if (!pinned.value) return
  const target = event.target as Node | null
  if (target && triggerRef.value?.contains(target)) return
  close()
}

function handleViewportChange() {
  if (visible.value) measurePopover()
}

onMounted(() => {
  document.addEventListener('pointerdown', handleDocumentPointerDown, true)
  window.addEventListener('resize', handleViewportChange)
})
onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', handleDocumentPointerDown, true)
  window.removeEventListener('resize', handleViewportChange)
})

defineExpose({ close })
</script>

<style scoped>
.po-hint-icon {
  display: inline-grid;
  place-items: center;
  width: 18px;
  height: 18px;
  padding: 0;
  border: 1.5px solid var(--po-line-mid);
  border-radius: 999px;
  background: var(--po-paper);
  color: var(--po-ink-soft);
  cursor: help;
  vertical-align: middle;
  transition:
    border-color 0.12s ease,
    background-color 0.12s ease,
    color 0.12s ease;
}

.po-hint-icon:hover,
.po-hint-icon:focus-visible {
  border-color: var(--po-ink);
  background: var(--po-accent-soft);
  color: var(--po-ink);
  outline: none;
}

.po-hint-icon:focus-visible {
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--po-accent) 40%, transparent);
}

.po-hint-icon__content {
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 6;
  font-size: 13px;
  line-height: 1.6;
  color: var(--po-ink-soft);
  overflow-wrap: anywhere;
}
</style>

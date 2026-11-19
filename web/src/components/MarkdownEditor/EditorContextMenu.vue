<template>
  <Teleport to="body">
    <div
      v-if="show"
      ref="menuRef"
      class="editor-context-menu"
      role="menu"
      :aria-label="t('Editor:FormatMenu')"
      :style="{ left: `${position.left}px`, top: `${position.top}px` }"
      @pointerdown.stop
      @contextmenu.prevent
    >
      <template v-for="(group, groupIndex) in groups" :key="groupIndex">
        <div v-if="groupIndex > 0" class="editor-context-menu__divider" role="separator"></div>
        <button
          v-for="command in group"
          :key="command.id"
          type="button"
          role="menuitem"
          class="editor-context-menu__item"
          :class="{ 'is-active': command.active }"
          :disabled="command.disabled"
          @click="emit('select', command.id)"
        >
          {{ t(command.labelKey) }}
        </button>
      </template>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { nextTick, reactive, ref, watch } from 'vue'
import { t } from '@/i18n'
import type { ResolvedEditorCommand } from './editorCommands'

const props = defineProps<{
  show: boolean
  x: number
  y: number
  groups: ResolvedEditorCommand[][]
}>()

const emit = defineEmits<{
  (event: 'select', id: string): void
  (event: 'close'): void
}>()

const menuRef = ref<HTMLElement | null>(null)
const position = reactive({ left: 0, top: 0 })

const clamp = (value: number, min: number, max: number) =>
  Math.max(min, Math.min(value, Math.max(min, max)))

const measure = () => {
  const element = menuRef.value
  if (!element) return
  const { width, height } = element.getBoundingClientRect()
  const margin = 8
  position.left = clamp(props.x, margin, window.innerWidth - width - margin)
  position.top = clamp(props.y, margin, window.innerHeight - height - margin)
}

const handlePointerDown = (event: PointerEvent) => {
  if (menuRef.value?.contains(event.target as Node)) return
  emit('close')
}

const handleKeydown = (event: KeyboardEvent) => {
  if (event.key !== 'Escape') return
  event.preventDefault()
  event.stopPropagation()
  emit('close')
}

watch(
  () => props.show,
  async (show) => {
    if (!show) {
      window.removeEventListener('pointerdown', handlePointerDown, true)
      window.removeEventListener('keydown', handleKeydown, true)
      window.removeEventListener('resize', measure)
      window.removeEventListener('scroll', measure, true)
      return
    }

    await nextTick()
    measure()
    menuRef.value?.querySelector<HTMLButtonElement>('button:not([disabled])')?.focus()
    window.addEventListener('pointerdown', handlePointerDown, true)
    window.addEventListener('keydown', handleKeydown, true)
    window.addEventListener('resize', measure)
    // 页面滚动会让固定坐标的菜单脱锚，直接收起更可靠
    window.addEventListener('scroll', handleScroll, true)
  },
)

function handleScroll(event: Event) {
  if (event.target instanceof Node && menuRef.value?.contains(event.target)) return
  emit('close')
}
</script>

<style scoped>
.editor-context-menu {
  position: fixed;
  z-index: 3000;
  display: flex;
  width: 216px;
  max-height: min(70vh, 480px);
  padding: 6px;
  flex-direction: column;
  overflow: auto;
  border: var(--po-border);
  border-radius: 10px;
  background: var(--po-paper);
  box-shadow: var(--po-shadow-pop);
}

.editor-context-menu__item {
  display: flex;
  width: 100%;
  min-height: 32px;
  padding: 6px 10px;
  align-items: center;
  color: var(--po-ink);
  border: 0;
  border-radius: 6px;
  background: transparent;
  font: inherit;
  font-size: 13px;
  text-align: left;
  cursor: pointer;
}

.editor-context-menu__item:hover:not(:disabled),
.editor-context-menu__item:focus-visible {
  background: var(--po-accent-soft);
  outline: none;
}

.editor-context-menu__item.is-active {
  color: var(--po-accent);
  font-weight: var(--po-weight-bold);
}

.editor-context-menu__item:disabled {
  color: var(--po-disabled);
  cursor: not-allowed;
}

.editor-context-menu__divider {
  height: 0;
  margin: 6px 4px;
  border-top: var(--po-border-thin);
}

@media (max-width: 768px) {
  .editor-context-menu__item {
    min-height: 44px;
    font-size: 15px;
  }
}
</style>

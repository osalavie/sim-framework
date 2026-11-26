<template>
  <div class="edge-toolbar" role="toolbar" :aria-label="t('Editor:Canvas:Edge:Toolbar')" @pointerdown.stop>
    <div class="edge-toolbar-group" role="group" :aria-label="t('Editor:Canvas:Edge:Type')">
      <button
        v-for="option in TYPE_OPTIONS"
        :key="option.value"
        type="button"
        :class="{ 'is-active': edge.data.type === option.value }"
        v-tooltip="t(option.labelKey)"
        :aria-pressed="edge.data.type === option.value"
        @click="emit('update-style', edge.id, { type: option.value })"
      >
        <svg viewBox="0 0 24 12" aria-hidden="true">
          <path v-if="option.value === 'smoothstep'" d="M2 10V6h10V2h10" />
          <path v-else-if="option.value === 'straight'" d="M2 10L22 2" />
          <path v-else d="M2 10C8 10 8 2 14 2h8" />
        </svg>
      </button>
    </div>
    <div class="edge-toolbar-group" role="group" :aria-label="t('Editor:Canvas:Edge:Arrow')">
      <button
        v-for="option in ARROW_OPTIONS"
        :key="option.value"
        type="button"
        :class="{ 'is-active': edge.data.arrow === option.value }"
        v-tooltip="t(option.labelKey)"
        :aria-pressed="edge.data.arrow === option.value"
        @click="emit('update-style', edge.id, { arrow: option.value })"
      >
        <svg viewBox="0 0 24 12" aria-hidden="true">
          <path d="M2 6h14" />
          <path v-if="option.value === 'arrow'" d="M16 2l6 4-6 4z" fill="currentColor" stroke="none" />
          <path v-else-if="option.value === 'open'" d="M16 2l6 4-6 4" />
        </svg>
      </button>
    </div>
    <button
      type="button"
      :class="{ 'is-active': edge.data.line === 'dashed' }"
      v-tooltip="t('Editor:Canvas:Edge:Dashed')"
      :aria-pressed="edge.data.line === 'dashed'"
      @click="emit('update-style', edge.id, { line: edge.data.line === 'dashed' ? 'solid' : 'dashed' })"
    >
      <svg viewBox="0 0 24 12" aria-hidden="true">
        <path d="M2 6h20" stroke-dasharray="4 3" />
      </svg>
    </button>
    <button type="button" v-tooltip="t('Editor:Canvas:Edge:EditLabel')" @click="emit('edit-label', edge.id)">
      <svg viewBox="0 0 24 12" aria-hidden="true">
        <path d="M3 6h18" stroke="none" />
        <text x="12" y="9" text-anchor="middle" font-size="9" fill="currentColor" stroke="none">T</text>
      </svg>
    </button>
    <button type="button" class="edge-delete" v-tooltip="t('Editor:Canvas:Edge:Delete')" @click="emit('delete', edge.id)">
      <svg viewBox="0 0 24 12" aria-hidden="true">
        <path d="M6 2l12 8M18 2L6 10" />
      </svg>
    </button>
  </div>
</template>

<script setup lang="ts">
import { t } from '@/i18n'
import type { CanvasArrowHead, CanvasEdgeType, CanvasLineStyle } from './canvasSyntax'

interface EdgeStylePatch {
  type?: CanvasEdgeType
  line?: CanvasLineStyle
  arrow?: CanvasArrowHead
}

defineProps<{
  edge: {
    id: string
    data: { type: CanvasEdgeType; line: CanvasLineStyle; arrow: CanvasArrowHead; label: string }
  }
}>()
const emit = defineEmits<{
  (event: 'update-style', id: string, patch: EdgeStylePatch): void
  (event: 'edit-label', id: string): void
  (event: 'delete', id: string): void
}>()

const TYPE_OPTIONS: ReadonlyArray<{ value: CanvasEdgeType; labelKey: string }> = [
  { value: 'smoothstep', labelKey: 'Editor:Canvas:Edge:TypeSmoothstep' },
  { value: 'straight', labelKey: 'Editor:Canvas:Edge:TypeStraight' },
  { value: 'bezier', labelKey: 'Editor:Canvas:Edge:TypeBezier' },
]

const ARROW_OPTIONS: ReadonlyArray<{ value: CanvasArrowHead; labelKey: string }> = [
  { value: 'none', labelKey: 'Editor:Canvas:Edge:ArrowNone' },
  { value: 'arrow', labelKey: 'Editor:Canvas:Edge:ArrowClosed' },
  { value: 'open', labelKey: 'Editor:Canvas:Edge:ArrowOpen' },
]
</script>

<style scoped>
.edge-toolbar {
  position: absolute;
  z-index: 15;
  bottom: 12px;
  left: 50%;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 6px;
  border: 1px solid var(--canvas-border, var(--theme-border));
  border-radius: 9px;
  background: var(--canvas-surface, #ffffff);
  box-shadow: 0 8px 24px rgb(0 0 0 / 14%);
  transform: translateX(-50%);
}

.edge-toolbar-group {
  display: flex;
  gap: 2px;
  padding-right: 4px;
  border-right: 1px solid var(--canvas-border, var(--theme-border));
}

.edge-toolbar button {
  display: flex;
  width: 34px;
  height: 30px;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid transparent;
  border-radius: 6px;
  background: transparent;
  color: var(--canvas-text, var(--theme-text-primary));
  cursor: pointer;
}

.edge-toolbar button:hover {
  border-color: var(--canvas-accent, #2f7d6d);
}

.edge-toolbar button.is-active {
  border-color: var(--canvas-accent, #2f7d6d);
  background: color-mix(in srgb, var(--canvas-accent, #2f7d6d) 12%, transparent);
  color: var(--canvas-accent, #2f7d6d);
}

.edge-toolbar button.edge-delete {
  color: var(--theme-error, #b42318);
}

.edge-toolbar svg {
  width: 22px;
  height: 12px;
  fill: none;
  stroke: currentColor;
  stroke-linecap: round;
  stroke-linejoin: round;
  stroke-width: 1.6;
}
</style>

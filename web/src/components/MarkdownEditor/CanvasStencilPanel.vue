<template>
  <aside class="stencil-panel" :aria-label="t('Editor:Canvas:Panel:Title')">
    <details
      v-for="group in CANVAS_SHAPE_GROUPS"
      :key="group"
      class="stencil-group"
      open
    >
      <summary>{{ t(`Editor:Canvas:Group:${capitalize(group)}`) }}</summary>
      <div class="stencil-items">
        <button
          v-for="definition in canvasShapesByGroup(group)"
          :key="definition.shape"
          type="button"
          class="stencil-item"
          draggable="true"
          v-tooltip="t(definition.labelKey)"
          @click="emit('add', definition.shape)"
          @dragstart="handleDragStart($event, definition.shape)"
        >
          <svg
            :viewBox="`0 0 ${previewSize(definition).width} ${previewSize(definition).height}`"
            aria-hidden="true"
            class="stencil-icon"
          >
            <path
              v-for="(outline, index) in previewPaths(definition)"
              :key="index"
              :d="outline.d"
              :fill="index === 0 && definition.fill ? 'var(--canvas-surface, #ffffff)' : 'none'"
              stroke="currentColor"
              stroke-width="1.6"
              :stroke-dasharray="outline.dashed ? '4 3' : undefined"
            />
          </svg>
          <span>{{ t(definition.labelKey) }}</span>
        </button>
      </div>
    </details>
  </aside>
</template>

<script setup lang="ts">
import {
  CANVAS_SHAPE_DRAG_MIME,
  CANVAS_SHAPE_GROUPS,
  canvasShapesByGroup,
  type CanvasShapeDefinition,
  type CanvasShapeGroup,
  type CanvasShapePath,
} from './canvasShapes'
import { t } from '@/i18n'

const PREVIEW_WIDTH = 26
const PREVIEW_MIN_HEIGHT = 12
const PREVIEW_MAX_HEIGHT = 24

const emit = defineEmits<{
  (event: 'add', shape: string): void
}>()

const capitalize = (value: CanvasShapeGroup) => value.charAt(0).toUpperCase() + value.slice(1)

/** 预览按形状默认宽高比缩放，避免数据库等高形状被压扁到 26×18 后几何走样。 */
const previewSize = (definition: CanvasShapeDefinition) => {
  const ratio = definition.defaultSize.height / definition.defaultSize.width
  const height = Math.round(
    Math.min(Math.max(PREVIEW_WIDTH * ratio, PREVIEW_MIN_HEIGHT), PREVIEW_MAX_HEIGHT),
  )
  return { width: PREVIEW_WIDTH, height }
}

const previewPaths = (definition: CanvasShapeDefinition): CanvasShapePath[] => {
  const paths = definition.previewPaths ?? definition.paths
  const size = previewSize(definition)
  if (definition.shape === 'junction') {
    return paths(size.height, size.height)
  }
  return paths(size.width, size.height)
}

const handleDragStart = (event: DragEvent, shape: string) => {
  if (!event.dataTransfer) return
  event.dataTransfer.setData(CANVAS_SHAPE_DRAG_MIME, shape)
  event.dataTransfer.effectAllowed = 'move'
}
</script>

<style scoped>
.stencil-panel {
  display: flex;
  flex-direction: column;
  flex: 0 0 168px;
  gap: 4px;
  min-height: 0;
  overflow-y: auto;
  padding: 8px;
  border-right: 1px solid var(--canvas-border, var(--theme-border));
  background: var(--canvas-surface, #ffffff);
}

.stencil-group {
  border: 0;
}

.stencil-group summary {
  padding: 4px 6px;
  border-radius: 6px;
  color: var(--canvas-muted, var(--theme-text-secondary));
  cursor: pointer;
  font-size: 12px;
  user-select: none;
}

.stencil-group summary:hover {
  background: color-mix(in srgb, var(--canvas-accent, #2f7d6d) 8%, transparent);
}

.stencil-items {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  padding: 4px 0 8px;
}

.stencil-item {
  display: flex;
  min-width: 0;
  min-height: 52px;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 3px;
  padding: 6px 4px;
  border: 1px solid transparent;
  border-radius: 7px;
  background: transparent;
  color: var(--canvas-text, var(--theme-text-primary));
  cursor: grab;
  font-size: 11px;
}

.stencil-item:hover,
.stencil-item:focus-visible {
  border-color: var(--canvas-accent, #2f7d6d);
  color: var(--canvas-accent, #2f7d6d);
  outline: 0;
}

.stencil-item span {
  overflow: hidden;
  max-width: 100%;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.stencil-icon {
  width: 26px;
  height: auto;
  max-height: 24px;
  overflow: visible;
}

@media (max-width: 640px) {
  .stencil-panel {
    flex-basis: 120px;
  }

  .stencil-items {
    grid-template-columns: 1fr;
  }

  .stencil-item {
    min-height: 44px;
  }
}
</style>

<template>
  <div
    class="canvas-shape"
    :class="{
      'is-readonly': readonly,
      'is-borderless': !definition.fill,
      'is-anchor': isAnchor,
      'is-revealed': revealed,
      'is-selected': selected,
    }"
    :style="shapeStyle"
    @dblclick="startEditing"
  >
    <Handle
      v-for="position in HANDLE_POSITIONS"
      :id="position"
      :key="position"
      type="source"
      :position="position"
      :connectable="readonly ? false : connectable"
      class="shape-handle"
    />
    <svg
      v-if="outlinePaths.length > 0"
      class="shape-outline"
      :viewBox="`0 0 ${width} ${height}`"
      preserveAspectRatio="none"
      aria-hidden="true"
    >
      <path
        v-for="(outline, index) in outlinePaths"
        :key="index"
        :d="outline.d"
        :fill="isAnchor ? 'var(--shape-accent)' : index === 0 && definition.fill ? 'var(--canvas-surface, #ffffff)' : 'none'"
        :stroke="isAnchor ? 'none' : 'var(--shape-accent)'"
        :stroke-width="isAnchor ? 0 : 2"
        :stroke-dasharray="outline.dashed ? '6 4' : undefined"
      />
    </svg>
    <span v-if="!editing && !isAnchor" class="shape-label" :style="labelAreaStyle">{{ data.label }}</span>
    <textarea
      v-else-if="editing"
      ref="editorRef"
      v-model="draft"
      class="shape-editor nodrag nowheel"
      :style="labelAreaStyle"
      :aria-label="t('Editor:Canvas:ShapeLabel')"
      rows="1"
      @blur="commit"
      @input="autogrowEditor"
      @keydown="handleEditorKeydown"
      @pointerdown.stop
    />
    <NodeResizer
      v-if="!readonly"
      :is-visible="selected"
      :min-width="definition.minSize.width"
      :min-height="definition.minSize.height"
      :max-width="definition.maxSize.width"
      :max-height="definition.maxSize.height"
      @resize="handleResize"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { Handle, Position, type HandleConnectable } from '@vue-flow/core'
import { NodeResizer, type OnResize } from '@vue-flow/node-resizer'
import { getCanvasShape } from './canvasShapes'
import { t } from '@/i18n'
import '@vue-flow/node-resizer/dist/style.css'

interface ShapeData {
  shape: string
  label: string
  color: string
  width: number
  height: number
}

const HANDLE_POSITIONS = [Position.Top, Position.Right, Position.Bottom, Position.Left]

const props = defineProps<{
  id: string
  data: ShapeData
  connectable: HandleConnectable
  readonly: boolean
  selected?: boolean
  /** 端点锚点浮现开关：悬停/选中线段或端点改接时由画布标记 */
  revealed?: boolean
}>()
const emit = defineEmits<{
  (event: 'update-label', id: string, value: string): void
  (event: 'resize', id: string, size: { width: number; height: number }): void
}>()

const definition = computed(() => getCanvasShape(props.data.shape))
const isAnchor = computed(() => props.data.shape === 'anchor')
const width = computed(() => props.data.width)
const height = computed(() => props.data.height)
const outlinePaths = computed(() => definition.value.paths(width.value, height.value))
const shapeStyle = computed(() => ({
  '--shape-accent': props.data.color,
  '--shape-label-inset': `${definition.value.labelInset}px`,
}))
/** 文本区最小高度：13px 字号 × 1.45 行高，保证文本区最小也能裹住一行字。 */
const LABEL_MIN_HEIGHT = 19

const labelAreaStyle = computed(() => {
  const box = definition.value.labelBox?.(width.value, height.value)
  if (box) {
    return {
      left: `${box.x + box.width / 2}px`,
      top: `${box.y + box.height / 2}px`,
      width: `${box.width}px`,
      minHeight: `${Math.max(box.height, LABEL_MIN_HEIGHT)}px`,
    }
  }
  return undefined
})

const handleResize = ({ params }: OnResize) => {
  emit('resize', props.id, { width: params.width, height: params.height })
}

const editing = ref(false)
const draft = ref('')
const editorRef = ref<HTMLTextAreaElement>()

const startEditing = () => {
  if (props.readonly || editing.value || isAnchor.value) return
  draft.value = props.data.label
  editing.value = true
  nextTick(() => {
    autogrowEditor()
    editorRef.value?.focus()
    editorRef.value?.select()
  })
}

/** 编辑框随内容自动撑高，不滚动；高度不受形状约束，可超出形状显示。 */
const autogrowEditor = () => {
  const el = editorRef.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = `${el.scrollHeight}px`
}

const commit = () => {
  if (!editing.value) return
  editing.value = false
  const value = draft.value.trim()
  if (value !== props.data.label) emit('update-label', props.id, value)
}

const handleEditorKeydown = (event: KeyboardEvent) => {
  event.stopPropagation()
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    commit()
  }
  if (event.key === 'Escape') {
    event.preventDefault()
    editing.value = false
  }
}
</script>

<style scoped>
.canvas-shape {
  position: relative;
  box-sizing: border-box;
  width: 100%;
  height: 100%;
}

.shape-outline {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
}

.shape-label {
  position: absolute;
  top: 50%;
  left: 50%;
  display: flex;
  width: calc(100% - 2 * var(--shape-label-inset, 10px));
  min-height: max(calc(100% - 2 * var(--shape-label-inset, 10px)), 1.45em);
  align-items: center;
  justify-content: center;
  overflow: visible;
  color: var(--canvas-text, var(--theme-text-primary));
  font-size: 13px;
  line-height: 1.45;
  text-align: center;
  transform: translate(-50%, -50%);
  white-space: pre-wrap;
  word-break: break-word;
  pointer-events: none;
}

.shape-editor {
  position: absolute;
  z-index: 2;
  top: 50%;
  left: 50%;
  box-sizing: border-box;
  width: calc(100% - 2 * var(--shape-label-inset, 10px));
  min-height: max(calc(100% - 2 * var(--shape-label-inset, 10px)), 1.45em);
  margin: 0;
  padding: 0;
  border: 0;
  outline: 1px dashed var(--shape-accent);
  background: var(--canvas-surface, #ffffff);
  color: var(--canvas-text, var(--theme-text-primary));
  font: inherit;
  font-size: 13px;
  line-height: 1.45;
  overflow: hidden;
  text-align: center;
  transform: translate(-50%, -50%);
  resize: none;
}

.canvas-shape.is-borderless .shape-label {
  color: var(--canvas-text, var(--theme-text-primary));
}

/* 端点锚点：平时完全隐藏，悬停锚点/线段（revealed 由画布下发）或拖动时才浮现 */
.canvas-shape.is-anchor .shape-outline {
  opacity: 0;
  transition: opacity 0.12s ease;
}

.canvas-shape.is-anchor:hover .shape-outline,
.canvas-shape.is-anchor.is-revealed .shape-outline,
.vue-flow__node.dragging .canvas-shape.is-anchor .shape-outline {
  opacity: 1;
}

.shape-handle {
  z-index: 2;
  width: 11px;
  height: 11px;
  border: 2px solid var(--canvas-surface, #ffffff);
  background: var(--shape-accent);
  opacity: 0;
  transition: opacity 0.15s ease;
}

.canvas-shape:hover .shape-handle.connectable,
.canvas-shape.is-selected .shape-handle.connectable,
.shape-handle.vue-flow__handle-connecting {
  opacity: 1;
}

/* 触屏没有悬停：连接点向节点外侧扩大命中区，内侧只留 2px，避免抢走节点拖动 */
@media (pointer: coarse) {
  .shape-handle::before {
    position: absolute;
    border-radius: 50%;
    content: '';
  }

  .shape-handle.vue-flow__handle-top::before {
    inset: -14px -8px -2px;
  }

  .shape-handle.vue-flow__handle-right::before {
    inset: -8px -14px -8px -2px;
  }

  .shape-handle.vue-flow__handle-bottom::before {
    inset: -2px -8px -14px;
  }

  .shape-handle.vue-flow__handle-left::before {
    inset: -8px -2px -8px -14px;
  }

  .canvas-shape.is-selected .shape-handle.connectable {
    width: 13px;
    height: 13px;
  }
}

/* 拖动悬空锚点时的吸附候选（画布按半径命中最邻近 handle 后下发） */
.shape-handle.is-snap-target {
  opacity: 1;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--shape-accent) 35%, transparent);
}

.canvas-shape.is-readonly .shape-handle {
  opacity: 0;
  pointer-events: none;
}

/* 端点锚点只呈现小圆点：不展示四向连接把手（保留布局盒，连线仍可吸附到端点） */
.canvas-shape.is-anchor .shape-handle,
.canvas-shape.is-anchor:hover .shape-handle.connectable,
.canvas-shape.is-anchor .shape-handle.vue-flow__handle-connecting {
  opacity: 0;
  pointer-events: none;
}
</style>

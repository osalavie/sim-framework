<template>
  <g :class="{ 'is-selected-glow': selected }">
    <BaseEdge :id="id" :path="edgePath" :style="edgeStyle" />
    <polygon
      v-if="arrowhead"
      class="edge-arrowhead"
      :class="{ 'is-open': data?.arrow === 'open' }"
      :points="arrowhead.points"
      :style="arrowhead.style"
    />
  </g>
  <path
    v-if="!readonly"
    :d="edgePath"
    class="edge-interaction"
    fill="none"
    stroke="transparent"
    stroke-width="16"
    @pointerdown="startEdgeDrag"
    @dblclick="startEditing"
  />
  <EdgeLabelRenderer>
    <div
      v-if="label || labelEditing"
      class="edge-label nodrag nopan"
      :class="{ 'is-editing': labelEditing }"
      :style="{ transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)` }"
      @dblclick.stop="startEditing"
      @pointerdown.stop
    >
      <span v-if="!labelEditing">{{ label }}</span>
      <input
        v-else
        ref="editorRef"
        v-model="draft"
        class="edge-label-editor nowheel"
        :aria-label="t('Editor:Canvas:Edge:Label')"
        @blur="commit"
        @keydown="handleEditorKeydown"
      />
    </div>

    <template v-if="selected && !readonly && supportsWaypoints">
      <button
        v-for="midpoint in waypointMidpoints"
        :key="`add-${midpoint.insertAfter}-${midpoint.x}-${midpoint.y}`"
        type="button"
        class="edge-vertex-add nodrag nopan"
        v-tooltip="t('Editor:Canvas:Edge:AddWaypoint')"
        :style="{ transform: `translate(-50%, -50%) translate(${midpoint.x}px, ${midpoint.y}px)` }"
        @pointerdown.stop
        @click.stop="emit('add-waypoint', id, midpoint.insertAfter, midpoint.x, midpoint.y)"
      >
        +
      </button>
      <button
        v-for="(point, index) in waypointList"
        :key="`wp-${index}`"
        type="button"
        class="edge-vertex nodrag nopan"
        :class="{ 'is-dragging': draggingIndex === index }"
        v-tooltip="t('Editor:Canvas:Edge:WaypointHint')"
        :style="{ transform: `translate(-50%, -50%) translate(${point.x}px, ${point.y}px)` }"
        @pointerdown.stop="startWaypointDrag($event, index)"
        @dblclick.stop="emit('remove-waypoint', id, index)"
      />
    </template>
  </EdgeLabelRenderer>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch, type CSSProperties } from 'vue'
import {
  BaseEdge,
  EdgeLabelRenderer,
  Position,
  getBezierPath,
  getSmoothStepPath,
  type EdgeProps,
} from '@vue-flow/core'
import {
  adjustCanvasEdgePoint,
  buildCatmullRomPath,
  buildOrthogonalRoute,
  type CanvasGeometryPoint,
  type CanvasSegmentMidpoint,
} from './canvasEdgeGeometry'
import { t } from '@/i18n'

interface CanvasEdgeData {
  type: 'smoothstep' | 'straight' | 'bezier'
  line: 'solid' | 'dashed'
  arrow: 'none' | 'arrow' | 'open'
  label: string
  waypoints?: CanvasGeometryPoint[]
}

const props = defineProps<EdgeProps & { readonly?: boolean; editing?: boolean }>()
const emit = defineEmits<{
  (event: 'update-label', id: string, value: string): void
  (event: 'edit-done'): void
  (event: 'add-waypoint', id: string, insertAfter: number, x: number, y: number): void
  (event: 'move-waypoint', id: string, index: number, screenX: number, screenY: number): void
  (event: 'remove-waypoint', id: string, index: number): void
  (event: 'edge-drag-start', id: string, screenX: number, screenY: number): void
  (event: 'edge-drag-move', id: string, screenX: number, screenY: number): void
  (event: 'edge-drag-end', id: string): void
}>()

const data = computed(() => props.data as CanvasEdgeData | undefined)
const label = computed(() => data.value?.label ?? '')
const waypointList = computed(() => data.value?.waypoints ?? [])
const supportsWaypoints = computed(() => (data.value?.type ?? 'smoothstep') !== 'straight')

const adjustedSource = computed(() =>
  adjustCanvasEdgePoint(props.sourceX, props.sourceY, props.sourcePosition),
)
const adjustedTarget = computed(() =>
  adjustCanvasEdgePoint(props.targetX, props.targetY, props.targetPosition),
)

interface RoutedPath {
  path: string
  labelX: number
  labelY: number
}

const legacyPath = computed<RoutedPath>(() => {
  const type = data.value?.type ?? 'smoothstep'
  const source = adjustedSource.value
  const target = adjustedTarget.value
  const params = {
    ...props,
    sourceX: source.x,
    sourceY: source.y,
    targetX: target.x,
    targetY: target.y,
  }
  if (type === 'straight') {
    return {
      path: `M${source.x} ${source.y}L${target.x} ${target.y}`,
      labelX: (source.x + target.x) / 2,
      labelY: (source.y + target.y) / 2,
    }
  }
  if (type === 'bezier') {
    const [path, x, y] = getBezierPath(params)
    return { path, labelX: x, labelY: y }
  }
  const [path, x, y] = getSmoothStepPath({ ...params, borderRadius: 8 })
  return { path, labelX: x, labelY: y }
})

const anchors = computed<CanvasGeometryPoint[]>(() => [
  adjustedSource.value,
  ...waypointList.value.map((point) => ({ x: point.x, y: point.y })),
  adjustedTarget.value,
])

const midpointOfAnchors = (points: CanvasGeometryPoint[]): CanvasSegmentMidpoint[] => {
  const midpoints: CanvasSegmentMidpoint[] = []
  for (let i = 0; i < points.length - 1; i++) {
    const a = points[i] as CanvasGeometryPoint
    const b = points[i + 1] as CanvasGeometryPoint
    midpoints.push({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2, insertAfter: i })
  }
  return midpoints
}

const routedPath = computed<RoutedPath>(() => {
  const type = data.value?.type ?? 'smoothstep'
  if (!supportsWaypoints.value || waypointList.value.length === 0) return legacyPath.value
  const points = anchors.value
  const middle = Math.floor((points.length - 1) / 2)
  const a = points[middle] as CanvasGeometryPoint
  const b = points[Math.min(middle + 1, points.length - 1)] as CanvasGeometryPoint
  const labelPosition = { labelX: (a.x + b.x) / 2, labelY: (a.y + b.y) / 2 }
  if (type === 'bezier') {
    return { path: buildCatmullRomPath(points), ...labelPosition }
  }
  return { path: buildOrthogonalRoute(points).path, ...labelPosition }
})

const waypointMidpoints = computed<CanvasSegmentMidpoint[]>(() => {
  if (!supportsWaypoints.value) return []
  const points = anchors.value
  if (points.length < 2) return []
  if ((data.value?.type ?? 'smoothstep') === 'bezier') return midpointOfAnchors(points)
  return buildOrthogonalRoute(points).midpoints
})

const edgePath = computed(() => routedPath.value.path)
const labelX = computed(() => routedPath.value.labelX)
const labelY = computed(() => routedPath.value.labelY)
const EDGE_STROKE = 'var(--canvas-muted, #8a8f98)'

/**
 * 内联填充/描边不能只靠 scoped CSS：html-to-image 会原样克隆 SVG 子树，
 * 导出时若路径没有 fill:none，弯折路径会按默认黑色填充成巨大色块。
 */
const edgeStyle = computed(() => ({
  fill: 'none',
  stroke: EDGE_STROKE,
  ...(data.value?.line === 'dashed' ? { strokeDasharray: '6 4' } : {}),
}))

/**
 * 箭头自绘：顶角 30°、底角 75° 的等腰三角。无折点时 smoothstep/bezier 沿目标边
 * 法线进入节点、straight 取两端点连线方向；有折点时沿末段折线方向进入。
 * 尖端压在锚点上，不会被目标形状边框遮住。
 */
const ARROW_LENGTH = 14
const ARROW_HALF_WIDTH = ARROW_LENGTH * Math.tan((15 * Math.PI) / 180)
const TARGET_INWARD_ANGLES: Record<Position, number> = {
  [Position.Top]: 90,
  [Position.Right]: 180,
  [Position.Bottom]: -90,
  [Position.Left]: 0,
}

/**
 * 折线到达方向：正交线末段可能经插入的直角拐角（水平/垂直），
 * 箭头方向必须取路由脊线末段而不是折点到目标的对角连线。
 */
const approachPoint = computed<CanvasGeometryPoint | undefined>(() => {
  const points = anchors.value
  if (waypointList.value.length === 0) return undefined
  if ((data.value?.type ?? 'smoothstep') === 'bezier') {
    return points[Math.max(points.length - 2, 0)] as CanvasGeometryPoint
  }
  const spine = buildOrthogonalRoute(points).points
  return (spine[spine.length - 2] ?? points[points.length - 2]) as CanvasGeometryPoint
})

const arrowhead = computed(() => {
  const kind = data.value?.arrow ?? 'arrow'
  if (kind === 'none') return undefined
  const target = adjustedTarget.value
  const type = data.value?.type ?? 'smoothstep'
  let angle = TARGET_INWARD_ANGLES[props.targetPosition ?? Position.Left]
  const approach = approachPoint.value
  if (approach) {
    angle = (Math.atan2(target.y - approach.y, target.x - approach.x) * 180) / Math.PI
  } else if (type === 'straight') {
    const source = adjustedSource.value
    angle = (Math.atan2(target.y - source.y, target.x - source.x) * 180) / Math.PI
  }
  // 沿来向回退 1px：避开形状边框 2px 描边的外半幅，尖端恰好贴上边框而不被覆盖。
  const rad = (angle * Math.PI) / 180
  const tipX = target.x - Math.cos(rad)
  const tipY = target.y - Math.sin(rad)
  const baseX = tipX - Math.cos(rad) * ARROW_LENGTH
  const baseY = tipY - Math.sin(rad) * ARROW_LENGTH
  // 直接生成绝对坐标，避免导出克隆时复制 SVG transform 导致箭头重复变换。
  const format = (value: number) => Number(value.toFixed(2))
  const points = [
    [tipX, tipY],
    [
      baseX - Math.sin(rad) * ARROW_HALF_WIDTH,
      baseY + Math.cos(rad) * ARROW_HALF_WIDTH,
    ],
    [
      baseX + Math.sin(rad) * ARROW_HALF_WIDTH,
      baseY - Math.cos(rad) * ARROW_HALF_WIDTH,
    ],
  ]
    .map(([x, y]) => `${format(x as number)},${format(y as number)}`)
    .join(' ')
  const color = props.selected
    ? 'var(--canvas-accent, #2f7d6d)'
    : 'var(--canvas-muted, #8a8f98)'
  const style: CSSProperties =
    kind === 'open'
      ? {
          fill: 'none',
          stroke: color,
          strokeLinecap: 'round',
          strokeLinejoin: 'round',
          strokeWidth: '1.7',
        }
      : { fill: color, stroke: 'none' }
  return { points, style }
})

const labelEditing = ref(false)
const draggingIndex = ref<number | null>(null)
const draft = ref('')
const editorRef = ref<HTMLInputElement>()

const startEditing = () => {
  if (props.readonly || labelEditing.value) return
  draft.value = label.value
  labelEditing.value = true
  nextTick(() => {
    editorRef.value?.focus()
    editorRef.value?.select()
  })
}

const finishEditing = (commitValue: boolean) => {
  if (!labelEditing.value) return
  labelEditing.value = false
  const value = draft.value.trim()
  if (commitValue && value !== label.value) emit('update-label', props.id, value)
  emit('edit-done')
}

const commit = () => finishEditing(true)

const handleEditorKeydown = (event: KeyboardEvent) => {
  event.stopPropagation()
  if (event.key === 'Enter') {
    event.preventDefault()
    finishEditing(true)
  }
  if (event.key === 'Escape') {
    event.preventDefault()
    finishEditing(false)
  }
}

const startWaypointDrag = (event: PointerEvent, index: number) => {
  if (props.readonly) return
  draggingIndex.value = index
  const onMove = (moveEvent: PointerEvent) => {
    emit('move-waypoint', props.id, index, moveEvent.clientX, moveEvent.clientY)
  }
  const onUp = () => {
    draggingIndex.value = null
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
  }
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

/** 整线拖动：线段中部按住拖动整体平移（3px 阈值区分点击选中），
 *  两端改接把手（edgeupdater）与折点/标签等自带手势的区域不拦截。 */
const startEdgeDrag = (event: PointerEvent) => {
  if (props.readonly || event.button !== 0) return
  const target = event.target as Element | null
  if (target?.closest('.vue-flow__edgeupdater, .edge-vertex, .edge-vertex-add, .edge-label')) return
  const startX = event.clientX
  const startY = event.clientY
  let dragging = false
  const onMove = (moveEvent: PointerEvent) => {
    if (!dragging) {
      if (Math.hypot(moveEvent.clientX - startX, moveEvent.clientY - startY) < 3) return
      dragging = true
      // 以按下点为基准，越过阈值后线段恰好跟随指针，不丢失前几像素
      emit('edge-drag-start', props.id, startX, startY)
    }
    moveEvent.preventDefault()
    emit('edge-drag-move', props.id, moveEvent.clientX, moveEvent.clientY)
  }
  const onUp = () => {
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    if (dragging) emit('edge-drag-end', props.id)
  }
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

watch(
  () => props.editing,
  (requested) => {
    if (requested) startEditing()
  },
)
</script>

<style scoped>
.edge-interaction {
  cursor: move;
  pointer-events: stroke;
}

/* 选中态：线条与自绘箭头整体泛主题色光晕（drop-shadow 会跟随形状轮廓） */
.is-selected-glow {
  filter: drop-shadow(0 0 3px var(--canvas-accent, #2f7d6d))
    drop-shadow(0 0 8px color-mix(in srgb, var(--canvas-accent, #2f7d6d) 55%, transparent));
}

.edge-arrowhead {
  fill: var(--canvas-muted, #8a8f98);
  stroke: none;
}

.edge-arrowhead.is-open {
  fill: none;
  stroke: var(--canvas-muted, #8a8f98);
  stroke-linecap: round;
  stroke-linejoin: round;
  stroke-width: 1.7;
}

.vue-flow__edge.selected .edge-arrowhead {
  fill: var(--canvas-accent, #2f7d6d);
  stroke: none;
}

.vue-flow__edge.selected .edge-arrowhead.is-open {
  fill: none;
  stroke: var(--canvas-accent, #2f7d6d);
}

.edge-label {
  position: absolute;
  z-index: 5;
  max-width: 220px;
  padding: 2px 8px;
  border: 1px solid var(--canvas-border, var(--theme-border));
  border-radius: 6px;
  background: var(--canvas-surface, #ffffff);
  color: var(--canvas-text, var(--theme-text-primary));
  font-size: 12px;
  line-height: 1.5;
  text-align: center;
  white-space: pre-wrap;
  word-break: break-word;
  pointer-events: all;
}

.edge-label.is-editing {
  padding: 2px;
}

.edge-label-editor {
  width: 160px;
  border: 0;
  outline: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  text-align: center;
}

.edge-vertex,
.edge-vertex-add {
  position: absolute;
  z-index: 6;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid var(--canvas-accent, #2f7d6d);
  background: var(--canvas-surface, #ffffff);
  cursor: grab;
  pointer-events: all;
}

.edge-vertex {
  width: 9px;
  height: 9px;
  border-radius: 2px;
}

.edge-vertex.is-dragging {
  cursor: grabbing;
}

.edge-vertex-add {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  color: var(--canvas-accent, #2f7d6d);
  font-size: 12px;
  line-height: 1;
  cursor: copy;
  opacity: 0.85;
}

.edge-vertex-add:hover {
  opacity: 1;
}
</style>

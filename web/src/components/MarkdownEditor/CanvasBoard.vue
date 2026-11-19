<template>
  <section
    ref="rootRef"
    class="canvas-block"
    :class="{ 'is-readonly': readonly, 'is-fullscreen': isFullscreen, 'is-library-open': libraryOpen }"
    :style="fullscreenStyle"
    :aria-label="t('Editor:Canvas:Label')"
    @touchstart.stop
    @contextmenu.stop
    @keydown.esc="handleCanvasEscape"
  >
    <header v-if="!readonly" class="canvas-header">
      <div>
        <strong>Canvas</strong>
        <span>{{ t('Editor:Canvas:Hint') }}</span>
      </div>
      <div class="canvas-actions">
        <button
          ref="libraryToggleRef"
          type="button"
          class="canvas-library-toggle"
          :aria-expanded="libraryOpen"
          :aria-controls="`${flowId}-library`"
          :aria-label="t('Editor:Canvas:Panel:Title')"
          @click="libraryOpen = !libraryOpen"
        >
          {{ t('Editor:Canvas:Panel:Title') }}
        </button>
        <button
          type="button"
          :disabled="!hasSelection"
          @click="removeSelection"
        >
          {{ t('Editor:Canvas:DeleteSelection') }}
        </button>
        <button type="button" class="canvas-delete" @click="emit('remove')">
          {{ t('Editor:Canvas:Delete') }}
        </button>
      </div>
    </header>

    <div
      class="canvas-body"
      :style="isFullscreen ? undefined : { height: `${canvasDocument.height}px` }"
    >
      <div v-if="libraryOpen && !readonly" class="canvas-library-scrim" @click="closeLibrary" />
      <CanvasStencilPanel
        v-if="!readonly && packReady"
        :id="`${flowId}-library`"
        @add="addShapeFromPanel"
      />

      <div
        ref="stageRef"
        class="stage"
        :class="{ 'is-pan-shift': shiftPressed && !readonly, 'is-pan-drag': panOnDrag }"
        @dragover.prevent
        @drop="handleDrop"
        @mousedown="blockMiddleClickAutoscroll"
        @pointerdown.capture="handleStagePointerDown"
      >
        <VueFlow
          :id="flowId"
          ref="flowRef"
          v-model:nodes="nodes"
          v-model:edges="edges"
          :nodes-draggable="!readonly"
          :nodes-connectable="!readonly"
          :elements-selectable="!readonly"
          :edges-updatable="!readonly"
          :delete-key-code="readonly ? null : ['Backspace', 'Delete']"
          :connection-mode="ConnectionMode.Loose"
          :connection-radius="48"
          :pan-on-drag="panOnDrag"
          :pan-activation-key-code="readonly ? 'Space' : 'Shift'"
          :selection-key-code="readonly ? 'Shift' : null"
          :zoom-on-scroll="false"
          zoom-activation-key-code="Control"
          :zoom-on-double-click="false"
          :min-zoom="0.35"
          :max-zoom="2"
          :default-edge-options="{ type: 'canvas' }"
          @connect="connect"
          @connect-start="handleConnectStart"
          @connect-end="handleConnectEnd"
          @edge-update="updateConnection"
          @edge-update-start="handleEdgeUpdateStart"
          @edge-update-end="handleEdgeUpdateEnd"
          @edge-click="selectEdge"
          @edge-mouse-enter="handleEdgeMouseEnter"
          @edge-mouse-leave="handleEdgeMouseLeave"
          @node-drag="handleNodeDrag"
          @node-drag-stop="handleNodeDragStop"
          @pane-click="closeMenu"
          @pane-context-menu="openMenu"
        >
          <Background pattern-color="var(--canvas-border)" :gap="GRID_SIZE" :size="1" />
          <MiniMap v-if="nodes.length > 0" pannable zoomable class="canvas-minimap" />
          <Controls v-if="!coarsePointer" :show-interactive="!readonly" position="bottom-left" />

          <template #node-shape="nodeProps">
            <CanvasShapeNode
              v-bind="nodeProps"
              :readonly="readonly"
              :revealed="revealedAnchorIds.has(nodeProps.id)"
              @update-label="updateNodeLabel"
              @resize="updateNodeSize"
            />
          </template>
          <template #edge-canvas="edgeProps">
            <CanvasEdge
              v-bind="edgeProps"
              :readonly="readonly"
              :editing="editingEdgeId === edgeProps.id"
              @update-label="updateEdgeLabel"
              @edit-done="editingEdgeId = null"
              @add-waypoint="addEdgeWaypoint"
              @move-waypoint="moveEdgeWaypoint"
              @remove-waypoint="removeEdgeWaypoint"
              @edge-drag-start="startEdgeDrag"
              @edge-drag-move="moveEdgeDrag"
              @edge-drag-end="endEdgeDrag"
            />
          </template>
        </VueFlow>

        <div
          v-if="guides.vertical.length > 0 || guides.horizontal.length > 0"
          class="canvas-guides"
          aria-hidden="true"
        >
          <i
            v-for="line in guides.vertical"
            :key="`v-${line}`"
            class="guide is-vertical"
            :style="{ left: `${toScreenX(line)}px` }"
          />
          <i
            v-for="line in guides.horizontal"
            :key="`h-${line}`"
            class="guide is-horizontal"
            :style="{ top: `${toScreenY(line)}px` }"
          />
        </div>

        <p v-if="nodes.length === 0" class="empty">
          {{ readonly ? t('Editor:Canvas:EmptyReadonly') : t('Editor:Canvas:Empty') }}
        </p>

        <svg v-if="lineDraft" class="canvas-line-preview" aria-hidden="true">
          <line
            :x1="toScreenX(lineDraft.start.x)"
            :y1="toScreenY(lineDraft.start.y)"
            :x2="toScreenX(lineDraft.current.x)"
            :y2="toScreenY(lineDraft.current.y)"
          />
        </svg>

        <div
          v-if="menu.visible && !readonly && packReady"
          class="canvas-menu"
          role="menu"
          :aria-label="t('Editor:Canvas:Menu')"
          :style="{ left: `${menu.x}px`, top: `${menu.y}px` }"
          @pointerdown.stop
        >
          <div v-for="group in CANVAS_SHAPE_GROUPS" :key="group" class="canvas-menu-group">
            <span>{{ t(`Editor:Canvas:Group:${capitalize(group)}`) }}</span>
            <button
              v-for="definition in canvasShapesByGroup(group)"
              :key="definition.shape"
              type="button"
              role="menuitem"
              @click="addShapeFromMenu(definition.shape)"
            >
              {{ t(definition.labelKey) }}
            </button>
          </div>
          <div class="canvas-menu-group">
            <span>{{ t('Editor:Canvas:Group:General') }}</span>
            <button type="button" role="menuitem" @click="addFreeLine">
              {{ t('Editor:Canvas:Menu:Line') }}
            </button>
          </div>
        </div>

        <CanvasEdgeToolbar
          v-if="selectedEdge && !readonly && packReady"
          :edge="selectedEdge"
          @update-style="updateEdgeStyle"
          @edit-label="startEdgeLabelEdit"
          @delete="removeEdge"
        />

        <div v-if="packReady" class="canvas-export" @pointerdown.stop>
          <button
            type="button"
            class="canvas-export-toggle"
            :disabled="nodes.length === 0"
            :aria-expanded="exportMenu.visible"
            :aria-label="t('Editor:Canvas:Export:Title')"
            v-tooltip="t('Editor:Canvas:Export:Title')"
            @click="exportMenu.visible = !exportMenu.visible"
          >
            <svg viewBox="0 0 16 16" aria-hidden="true">
              <path
                d="M8 2v7M5 6.5 8 9.5l3-3M3 12.5h10"
                fill="none"
                stroke="currentColor"
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="1.6"
              />
            </svg>
          </button>
          <div v-if="exportMenu.visible" class="canvas-export-menu" role="menu">
            <button type="button" role="menuitem" @click="exportCanvas('png')">PNG</button>
            <button type="button" role="menuitem" @click="exportCanvas('jpeg')">JPG</button>
          </div>
        </div>

        <button
          v-if="packReady"
          type="button"
          class="canvas-fullscreen-toggle"
          :aria-pressed="isFullscreen"
          :aria-label="isFullscreen ? t('Editor:Canvas:ExitFullscreen') : t('Editor:Canvas:Fullscreen')"
          v-tooltip="isFullscreen ? t('Editor:Canvas:ExitFullscreen') : t('Editor:Canvas:Fullscreen')"
          @click="toggleFullscreen"
          @pointerdown.stop
        >
          <svg viewBox="0 0 16 16" aria-hidden="true">
            <path
              v-if="isFullscreen"
              d="M6 2v4H2M10 14v-4h4M2 14l4-4M14 2l-4 4"
              fill="none"
              stroke="currentColor"
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="1.6"
            />
            <path
              v-else
              d="M2 6V2h4M14 10v4h-4M6 2L2 6M10 14l4-4"
              fill="none"
              stroke="currentColor"
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="1.6"
            />
          </svg>
        </button>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  capitalize,
  computed,
  inject,
  onBeforeMount,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
  watch,
} from 'vue'
import {
  ConnectionMode,
  VueFlow,
  useVueFlow,
  type Connection,
  type Edge,
  type EdgeMouseEvent,
  type EdgeUpdateEvent,
  type GraphNode,
  type Node as FlowNode,
  type NodeDragEvent,
  type OnConnectStartParams,
  type XYPosition,
} from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import CanvasEdge from './CanvasEdge.vue'
import CanvasEdgeToolbar from './CanvasEdgeToolbar.vue'
import CanvasShapeNode from './CanvasShapeNode.vue'
import CanvasStencilPanel from './CanvasStencilPanel.vue'
import {
  buildClipboardPayload,
  remapClipboardForPaste,
  type CanvasClipboardPayload,
} from './canvasClipboard'
import {
  CANVAS_EXPORT_ROOT_SELECTOR,
  canvasExportFileName,
  exportCanvasImage,
  measureCanvasContentBounds,
  planCanvasExport,
  renderCanvasImage,
  resolveCanvasBackground,
  resolveCanvasThemeProperties,
  withCanvasThemeProperties,
  type CanvasImageFormat,
} from './canvasExport'
import { CANVAS_CAPTURE_KEY } from './canvasCapture'
import { computeDragSnap, EMPTY_GUIDES, type CanvasGuideLines, type GuideRect } from './canvasGuides'
import { pickNearestHandle, type CanvasHandleCandidate } from './canvasEdgeGeometry'
import {
  CANVAS_SHAPE_DRAG_MIME,
  CANVAS_SHAPE_GROUPS,
  canvasShapesByGroup,
  getCanvasShape,
  isCanvasShape,
} from './canvasShapes'
import {
  cloneCanvas,
  type CanvasArrowHead,
  type CanvasDocument,
  type CanvasEdgeSpec,
  type CanvasEdgeType,
  type CanvasLineStyle,
  type CanvasNodeSpec,
  type CanvasPoint,
  type CanvasPort,
} from './canvasSyntax'
import { ensureMessagePacks } from '@/i18n/lazyPacks'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'

interface ShapeNodeData {
  shape: string
  label: string
  color: string
  width: number
  height: number
}

interface CanvasEdgeData {
  type: CanvasEdgeType
  line: CanvasLineStyle
  arrow: CanvasArrowHead
  label: string
  waypoints?: CanvasPoint[]
}

const GRID_SIZE = 20
const GUIDE_THRESHOLD = 6
const DEFAULT_NODE_COLOR = '#2f7d6d'
const DEFAULT_EDGE_DATA = (): CanvasEdgeData => ({
  type: 'smoothstep',
  line: 'solid',
  arrow: 'arrow',
  label: '',
})

const props = withDefaults(
  defineProps<{
    modelValue: CanvasDocument
    readonly?: boolean
    /** 导出文件名片段：文章标题、各级标题、画布序号；导出时求值以取到最新内容。 */
    resolveExportSegments?: () => string[]
  }>(),
  { readonly: false },
)
const emit = defineEmits<{
  (event: 'update:modelValue', value: CanvasDocument): void
  (event: 'remove'): void
}>()

const flowId = `canvas-${crypto.randomUUID()}`
const flow = useVueFlow(flowId)

/**
 * 编辑类变更统一经 store 查找实体再改：VueFlow 的 v-model 同步舞蹈下，
 * 本地 nodes/edges 数组可能短暂持有与 store 分离的副本，直接改副本不会影响渲染与持久化。
 * store 实体才是组件渲染与序列化回写的权威对象。
 */
const findStoreNode = (id: string) => flow.findNode(id)
const findStoreEdge = (id: string) => flow.findEdge(id)
const flowRef = ref<InstanceType<typeof VueFlow>>()
const rootRef = ref<HTMLElement>()
const stageRef = ref<HTMLElement>()
const libraryToggleRef = ref<HTMLButtonElement>()
const canvasDocument = ref(cloneCanvas(props.modelValue))
const nodes = ref<FlowNode<ShapeNodeData>[]>([])
const edges = ref<Edge<CanvasEdgeData>[]>([])
const guides = ref<CanvasGuideLines>(EMPTY_GUIDES)
const editingEdgeId = ref<string | null>(null)
const packReady = ref(false)
const menu = reactive({
  visible: false,
  x: 0,
  y: 0,
  position: { x: 0, y: 0 } as XYPosition,
})
const exportMenu = reactive({ visible: false })
const isFullscreen = ref(false)
const libraryOpen = ref(false)
/** 触屏设备直接用拖动平移画布，桌面端仍以 Shift + 左键平移、空白拖动画线。 */
const coarsePointer = ref(false)
let coarsePointerQuery: MediaQueryList | null = null

const panOnDrag = computed(() => props.readonly || coarsePointer.value)

/** 全屏脱离文档流：固定定位铺满视口；不能用 Teleport 到 body——组件挂在 tiptap 节点视图里，根 Teleport 会引发递归更新。 */
const fullscreenStyle = computed(() =>
  isFullscreen.value
    ? { position: 'fixed' as const, inset: '0', zIndex: 9999 }
    : undefined,
)

const exitFullscreenOnEscape = (event: KeyboardEvent) => {
  if (event.key === 'Escape') isFullscreen.value = false
}

const handleCanvasEscape = (event: KeyboardEvent) => {
  if (!libraryOpen.value && !isFullscreen.value) return
  event.preventDefault()
  event.stopPropagation()
  if (libraryOpen.value) closeLibrary()
  else isFullscreen.value = false
}

const closeLibrary = () => {
  libraryOpen.value = false
  libraryToggleRef.value?.focus()
}

/** 画板文档里的元素包围盒；初始定位不依赖节点首帧测量，避免过渡布局量到错误尺寸。 */
const contentBounds = (): { x: number; y: number; width: number; height: number } | null => {
  if (nodes.value.length === 0) return null
  let minX = Number.POSITIVE_INFINITY
  let minY = Number.POSITIVE_INFINITY
  let maxX = Number.NEGATIVE_INFINITY
  let maxY = Number.NEGATIVE_INFINITY
  for (const node of nodes.value) {
    const width = node.data?.width ?? 0
    const height = node.data?.height ?? 0
    minX = Math.min(minX, node.position.x)
    minY = Math.min(minY, node.position.y)
    maxX = Math.max(maxX, node.position.x + width)
    maxY = Math.max(maxY, node.position.y + height)
  }
  return { x: minX, y: minY, width: maxX - minX, height: maxY - minY }
}

/** 打开文章时默认把元素定位到可视区域；空画布回到左上角原点。 */
const fitCanvasContent = (duration = 0) => {
  const stage = flow.dimensions.value
  if (stage.width <= 0 || stage.height <= 0) return
  const bounds = contentBounds()
  if (bounds) void flow.fitBounds(bounds, { padding: 0.2, duration })
  else void flow.setViewport({ x: 0, y: 0, zoom: 1 }, { duration })
}

/** 用户尚未主动平移/缩放前，布局变化（图形库挂载、窗口尺寸）后继续保持元素可见。 */
let viewportTouched = false

const tryInitialFit = () => {
  if (viewportTouched || !packReady.value) return
  const stage = flow.dimensions.value
  if (stage.width <= 0 || stage.height <= 0) return
  fitCanvasContent()
}

/** 全屏改变舞台尺寸：等 ResizeObserver 回报新尺寸后再定位，避免用旧视口尺寸计算。 */
let refitOnStageResize = false

const handleStageResize = () => {
  scheduleInternalsRefresh()
  if (refitOnStageResize) {
    refitOnStageResize = false
    fitCanvasContent(200)
    return
  }
  tryInitialFit()
}

const toggleFullscreen = () => {
  isFullscreen.value = !isFullscreen.value
  refitOnStageResize = true
}

watch(isFullscreen, (value) => {
  if (value) window.addEventListener('keydown', exitFullscreenOnEscape)
  else window.removeEventListener('keydown', exitFullscreenOnEscape)
})

onBeforeUnmount(() => window.removeEventListener('keydown', exitFullscreenOnEscape))
/** 最近一次与外部文档互相同步的 JSON，用于识别自身回写的回声，避免重建回环。 */
let lastSyncedJson = ''
let syncingFromProps = false
let refreshTimers: number[] = []
/** 补测退避序列：弹窗/路由入场动画通常 <300ms，末尾两档兜住慢动画与漏发事件。 */
const REFRESH_BACKOFF_MS = [0, 80, 200, 400, 800, 1600]

/** 祖先 transform: scale()（弹窗入场）或 CSS zoom 会让 getBoundingClientRect 失真，
 *  此时量出的 handle 偏移会被缩放污染，且尺寸不变后不再触发任何重测。 */
const isLayoutSettled = () => {
  const root = rootRef.value
  if (!root || !root.isConnected || root.offsetWidth === 0) return false
  const layoutWidth = root.offsetWidth
  // 至少 1px 容差吸收 offsetWidth 取整；比例容差兜住大画布的轻微缩放。
  return Math.abs(root.getBoundingClientRect().width - layoutWidth) <= Math.max(1, layoutWidth * 0.002)
}

/** 强制重测节点内部尺寸（dimensions/handleBounds）。Vue Flow 只靠挂载时的 ResizeObserver
 *  首次回调测量，页面未渲染（后台标签页、弹窗/过渡中）时回调不送达或量到带祖先 transform
 *  的坐标，节点会停留在 0 尺寸、连线退化到节点原点且不再自愈。这里用 setTimeout 而非 rAF：
 *  未渲染环境没有渲染帧，定时器仍会执行；测量本身会强制读取布局，不依赖回调时机。
 *  退避补测覆盖动画结束才稳定的场景；最后一档无视缩放兜底，避免永久缩放容器下永不测量。 */
const scheduleInternalsRefresh = () => {
  refreshTimers.forEach((timer) => window.clearTimeout(timer))
  refreshTimers = REFRESH_BACKOFF_MS.map((delay, index) =>
    window.setTimeout(() => {
      if (index === REFRESH_BACKOFF_MS.length - 1 || isLayoutSettled()) {
        flow.updateNodeInternals()
        tryInitialFit()
      }
    }, delay),
  )
}

/** 后台标签页的定时器会被节流，恢复可见时再兜底补测一次，保证用户切回即可看到正确连线。 */
const handleVisibilityChange = () => {
  if (document.visibilityState === 'visible') scheduleInternalsRefresh()
}

/** 触控能力变化（外接鼠标/键盘）时同步平移策略，不写入画板文档。 */
const handleCoarsePointerChange = (event: MediaQueryListEvent) => {
  coarsePointer.value = event.matches
}

/** 前进/后退从 bfcache 恢复时布局重新生效，补测一次。 */
const handlePageShow = () => scheduleInternalsRefresh()

onBeforeMount(() => {
  void ensureMessagePacks(['canvas']).then(() => {
    packReady.value = true
  })
})

const createFlowNode = (node: CanvasNodeSpec): FlowNode<ShapeNodeData> => ({
  id: node.id,
  type: 'shape',
  position: { x: node.x, y: node.y },
  width: node.width,
  height: node.height,
  data: {
    shape: node.shape,
    label: node.label,
    color: node.color,
    width: node.width,
    height: node.height,
  },
  ariaLabel: node.label || t(getCanvasShape(node.shape).labelKey),
  // 悬空端点锚点不参与选中/框选/快捷键删除，随线段生命周期回收
  ...(node.shape === 'anchor' ? { selectable: false } : {}),
})

const createFlowEdge = (edge: CanvasEdgeSpec): Edge<CanvasEdgeData> => ({
  id: edge.id,
  source: edge.source,
  target: edge.target,
  sourceHandle: edge.sourcePort ?? undefined,
  targetHandle: edge.targetPort ?? undefined,
  type: 'canvas',
  data: {
    type: edge.type,
    line: edge.line,
    arrow: edge.arrow,
    label: edge.label,
    ...(edge.waypoints ? { waypoints: edge.waypoints.map((point) => ({ ...point })) } : {}),
  },
})

const toFlowNodes = (value: CanvasDocument): FlowNode<ShapeNodeData>[] =>
  value.nodes.map(createFlowNode)

const toFlowEdges = (value: CanvasDocument): Edge<CanvasEdgeData>[] =>
  value.edges.map(createFlowEdge)

const load = (value: CanvasDocument) => {
  syncingFromProps = true
  canvasDocument.value = cloneCanvas(value)
  nodes.value = toFlowNodes(value)
  edges.value = toFlowEdges(value)
  lastSyncedJson = JSON.stringify(value)
  queueMicrotask(() => {
    syncingFromProps = false
  })
  // 重建后的节点对象必须再测一次，不依赖 ResizeObserver 首帧回调是否送达（含挂载场景）
  scheduleInternalsRefresh()
}

const serialize = (): CanvasDocument => ({
  version: 2,
  height: canvasDocument.value.height,
  // 以 store 为权威序列化：本地数组在 v-model 同步舞蹈下可能短暂持有分离副本
  nodes: flow.nodes.value.map((node) => ({
    id: node.id,
    shape: (node.data as ShapeNodeData | undefined)?.shape ?? 'rect',
    x: node.position.x,
    y: node.position.y,
    width: (node.data as ShapeNodeData | undefined)?.width ?? 160,
    height: (node.data as ShapeNodeData | undefined)?.height ?? 80,
    label: (node.data as ShapeNodeData | undefined)?.label ?? '',
    color: (node.data as ShapeNodeData | undefined)?.color ?? DEFAULT_NODE_COLOR,
  })),
  edges: flow.edges.value.map((edge) => ({
    id: edge.id,
    source: edge.source,
    target: edge.target,
    ...(edge.sourceHandle ? { sourcePort: edge.sourceHandle as CanvasPort } : {}),
    ...(edge.targetHandle ? { targetPort: edge.targetHandle as CanvasPort } : {}),
    type: edge.data?.type ?? 'smoothstep',
    line: edge.data?.line ?? 'solid',
    arrow: edge.data?.arrow ?? 'arrow',
    label: edge.data?.label ?? '',
    ...(edge.data?.waypoints?.length
      ? {
          waypoints: (edge.data.waypoints as CanvasPoint[]).map((point) => ({
            x: point.x,
            y: point.y,
          })),
        }
      : {}),
  })),
})

const publish = () => {
  if (props.readonly || syncingFromProps) return
  scheduleAnchorPrune()
  const value = serialize()
  canvasDocument.value = cloneCanvas(value)
  const json = JSON.stringify(value)
  // 仅尺寸测量/视口等内部变化时序列化结果不变，不回写以打断 更新→重建 回环
  if (json === lastSyncedJson) return
  lastSyncedJson = json
  emit('update:modelValue', value)
}

watch(
  () => props.modelValue,
  (value) => {
    // 自身 emit 引发的外部回声直接跳过，不重建节点（重建会导致测量错位、连线扭曲）
    if (JSON.stringify(value) === lastSyncedJson) return
    load(value)
  },
  { deep: true },
)
/** 监听 store 深变化（而非本地数组）：store 才是渲染与编辑变更的落点，
 *  本地数组在同步舞蹈下可能持有分离副本，漏掉变更会导致文档回写不及时。 */
watch([flow.nodes, flow.edges], publish, { deep: true, flush: 'post' })

load(props.modelValue)

const hasSelection = computed(
  () => flow.getSelectedNodes.value.length > 0 || flow.getSelectedEdges.value.length > 0,
)

interface SelectedCanvasEdge {
  id: string
  data: CanvasEdgeData
}

const selectedEdgeId = ref<string | null>(null)

const selectedEdge = computed<SelectedCanvasEdge | undefined>(() => {
  if (!selectedEdgeId.value) return undefined
  // Edge 泛型联盟在 find 上展开会触发 TS2589，先降为结构类型再查找
  const list = edges.value as unknown as Array<{ id: string; data?: CanvasEdgeData }>
  const edge = list.find((candidate) => candidate.id === selectedEdgeId.value)
  return edge?.data ? { id: edge.id, data: edge.data } : undefined
})

const selectEdge = (event: EdgeMouseEvent) => {
  selectedEdgeId.value = event.edge.id
}

/** —— 端点锚点浮现：悬停线段、选中线段、端点改接或整线拖动时临时可见 —— */

const activeEdgeIds = ref<Set<string>>(new Set())

const setEdgeActive = (id: string, active: boolean) => {
  const next = new Set(activeEdgeIds.value)
  if (active) next.add(id)
  else next.delete(id)
  activeEdgeIds.value = next
}

/** —— 整线拖动：平移两端悬空锚点与折点（吸附元素的端不跟随） —— */

interface EdgeDragState {
  edgeId: string
  pointerX: number
  pointerY: number
  movableEnds: Array<{ id: string; origin: CanvasPoint }>
  waypoints: CanvasPoint[] | null
}

let edgeDragState: EdgeDragState | null = null
/** 端点改接（updater 拖拽）进行中：Vue Flow 对该手势也会发 connect-end，需与悬空端点生成区分 */
let edgeUpdaterActive = false

/** 端点改接手势详情：记录被拖的是哪一端、按下点与是否发生有效改接（未命中 handle 的释放=脱离成悬空端点） */
interface EdgeUpdaterGesture {
  edgeId: string
  end: 'source' | 'target'
  origin: { x: number; y: number }
  updated: boolean
}
let edgeUpdaterGesture: EdgeUpdaterGesture | null = null

const revealedAnchorIds = computed(() => {
  const relevant = new Set(activeEdgeIds.value)
  if (selectedEdgeId.value) relevant.add(selectedEdgeId.value)
  const ids = new Set<string>()
  for (const edge of edges.value) {
    if (!relevant.has(edge.id)) continue
    ids.add(edge.source)
    ids.add(edge.target)
  }
  return ids
})

const handleEdgeMouseEnter = (event: EdgeMouseEvent) => {
  setEdgeActive(event.edge.id, true)
}

const handleEdgeMouseLeave = (event: EdgeMouseEvent) => {
  if (edgeDragState?.edgeId === event.edge.id) return
  setEdgeActive(event.edge.id, false)
}

const handleEdgeUpdateStart = (event: EdgeMouseEvent) => {
  edgeUpdaterActive = true
  setEdgeActive(event.edge.id, true)
  // updater 把手为端点外侧的透明圆（EdgeAnchor 渲染 vue-flow__edgeupdater-{source|target}），
  // class 后缀即被拖的端；落在空白释放时据此决定脱离的是哪一端
  const native = event.event as MouseEvent
  const updater = (native?.target as Element | null)?.closest('.vue-flow__edgeupdater')
  edgeUpdaterGesture = {
    edgeId: event.edge.id,
    end: updater?.classList.contains('vue-flow__edgeupdater-target') ? 'target' : 'source',
    origin: { x: native?.clientX ?? 0, y: native?.clientY ?? 0 },
    updated: false,
  }
}

const handleEdgeUpdateEnd = (event: EdgeMouseEvent) => {
  edgeUpdaterActive = false
  const gesture = edgeUpdaterGesture
  edgeUpdaterGesture = null
  if (gesture && !gesture.updated) detachEdgeEndToAnchor(gesture, event.event as MouseEvent)
  if (edgeDragState?.edgeId === event.edge.id) return
  setEdgeActive(event.edge.id, false)
}

const startEdgeDrag = (id: string, pointerX: number, pointerY: number) => {
  const edge = edges.value.find((candidate) => candidate.id === id)
  if (!edge?.data) return
  const movableEnds: EdgeDragState['movableEnds'] = []
  for (const nodeId of [edge.source, edge.target]) {
    const node = nodes.value.find((candidate) => candidate.id === nodeId)
    if (node?.data?.shape === 'anchor') {
      movableEnds.push({ id: node.id, origin: { x: node.position.x, y: node.position.y } })
    }
  }
  const waypoints = edge.data.waypoints?.map((point) => ({ x: point.x, y: point.y })) ?? null
  if (movableEnds.length === 0 && !waypoints?.length) return
  edgeDragState = { edgeId: id, pointerX, pointerY, movableEnds, waypoints }
  setEdgeActive(id, true)
}

const moveEdgeDrag = (id: string, pointerX: number, pointerY: number) => {
  if (!edgeDragState || edgeDragState.edgeId !== id) return
  const zoom = Math.max(viewport.value.zoom, 0.01)
  const dx = (pointerX - edgeDragState.pointerX) / zoom
  const dy = (pointerY - edgeDragState.pointerY) / zoom
  const round = (value: number) => Math.round(value * 100) / 100
  for (const end of edgeDragState.movableEnds) {
    const node = findStoreNode(end.id)
    if (!node) continue
    node.position = { x: round(end.origin.x + dx), y: round(end.origin.y + dy) }
  }
  if (edgeDragState.waypoints?.length) {
    const edge = findStoreEdge(id)
    const origins = edgeDragState.waypoints
    if (edge?.data) {
      edge.data.waypoints = origins.map((point) => ({ x: round(point.x + dx), y: round(point.y + dy) }))
    }
  }
}

const endEdgeDrag = (id: string) => {
  if (!edgeDragState || edgeDragState.edgeId !== id) return
  edgeDragState = null
  setEdgeActive(id, false)
}

const viewport = flow.viewport
const toScreenX = (x: number) => x * viewport.value.zoom + viewport.value.x
const toScreenY = (y: number) => y * viewport.value.zoom + viewport.value.y

interface RectSource {
  id: string
  position: XYPosition
  data?: ShapeNodeData
  dimensions?: { width: number; height: number }
}

const rectOf = (node: RectSource): GuideRect => ({
  id: node.id,
  x: node.position.x,
  y: node.position.y,
  width: node.dimensions?.width || node.data?.width || 0,
  height: node.dimensions?.height || node.data?.height || 0,
})

const handleNodeDrag = (event: NodeDragEvent) => {
  if (props.readonly) return
  const dragged: GraphNode[] = event.nodes
  const draggedIds = new Set(dragged.map((node) => node.id))
  const moving = dragged.map((node) => rectOf(node))
  const others = nodes.value
    .filter((node) => !draggedIds.has(node.id))
    .map((node) => rectOf(node))
  const zoom = Math.max(viewport.value.zoom, 0.01)
  const result = computeDragSnap(moving, others, GUIDE_THRESHOLD / zoom, GRID_SIZE, rectOf(event.node))
  if (result.offsetX !== 0 || result.offsetY !== 0) {
    dragged.forEach((node) => {
      node.position = { x: node.position.x + result.offsetX, y: node.position.y + result.offsetY }
    })
  }
  guides.value = result.guides
  // 拖动悬空锚点：高亮半径内最近的元素 handle，提示松手即吸附
  if ((event.node.data as ShapeNodeData | undefined)?.shape === 'anchor') {
    const native = event.event as MouseEvent
    const hit =
      native && typeof native.clientX === 'number'
        ? resolveHandleAt(native.clientX, native.clientY, { excludeAnchors: true })
        : null
    setSnapCandidate(hit?.element ?? null)
  }
}

const clearGuides = () => {
  guides.value = EMPTY_GUIDES
}

/** 拖动悬空锚点时的吸附候选高亮 */
let snapCandidateElement: HTMLElement | null = null

const setSnapCandidate = (element: HTMLElement | null) => {
  if (snapCandidateElement === element) return
  snapCandidateElement?.classList.remove('is-snap-target')
  snapCandidateElement = element
  snapCandidateElement?.classList.add('is-snap-target')
}

const handleNodeDragStop = (event: NodeDragEvent) => {
  clearGuides()
  setSnapCandidate(null)
  if (props.readonly) return
  if ((event.node.data as ShapeNodeData | undefined)?.shape !== 'anchor') return
  const native = event.event as MouseEvent
  if (!native || typeof native.clientX !== 'number') return
  const hit = resolveHandleAt(native.clientX, native.clientY, { excludeAnchors: true })
  if (hit) snapAnchorToHandle(event.node.id, hit)
}

/**
 * 悬空锚点拖到元素 handle 附近松手：依附该锚点的所有端点改接元素并回收锚点。
 * 放宏任务执行：节点拖拽结束的 store→models 回写暂停窗口可能仍开着，
 * 紧随的同步替换会被吞掉（组件/文档已变而 store 与 DOM 保留旧状态）。
 */
const snapAnchorToHandle = (anchorId: string, hit: HandleHit) => {
  window.setTimeout(() => {
    let changed = false
    const next = [...edges.value]
    for (let index = 0; index < next.length; index++) {
      const edge = next[index]
      if (!edge) continue
      if (edge.source === anchorId) {
        next[index] = { ...edge, source: hit.nodeId, sourceHandle: hit.handleId ?? undefined }
        changed = true
      } else if (edge.target === anchorId) {
        next[index] = { ...edge, target: hit.nodeId, targetHandle: hit.handleId ?? undefined }
        changed = true
      }
    }
    if (changed) edges.value = next
    const kept = nodes.value.filter((node) => node.id !== anchorId)
    if (kept.length !== nodes.value.length) nodes.value = kept
    scheduleAnchorPrune()
  }, 0)
}

const closeMenu = () => {
  menu.visible = false
  exportMenu.visible = false
  selectedEdgeId.value = null
}

/**
 * 导出完整元素区域：捕获根取 Vue Flow 承载画布变换的层（节点/连线/箭头/边标签），
 * 包围盒按元素屏幕矩形换算回流坐标，因此当前平移缩放、连线绕行、标签越界都不影响出图范围。
 * 导出前把画布主题令牌内联到捕获根，SVG 属性里的 var() 在克隆文档中也能解析为当前主题色。
 */
const exportCanvas = async (format: CanvasImageFormat) => {
  exportMenu.visible = false
  const stage = stageRef.value
  const rootEl = stage?.querySelector<HTMLElement>(CANVAS_EXPORT_ROOT_SELECTOR)
  if (!stage || !rootEl || nodes.value.length === 0) return
  const bounds = measureCanvasContentBounds(rootEl, (point) =>
    flow.screenToFlowCoordinate(point),
  )
  if (!bounds) return
  const plan = planCanvasExport(bounds, getComputedStyle(stage).backgroundColor)
  const fileName = canvasExportFileName(format, props.resolveExportSegments?.() ?? [])
  try {
    await withCanvasThemeProperties(rootEl, resolveCanvasThemeProperties(stage), () =>
      exportCanvasImage(rootEl, plan, format, fileName),
    )
  } catch {
    useDiscreteApi.message.error(t('Editor:Canvas:Export:Failed'))
  }
}

/**
 * 文章导出用：复用与下载导出完全相同的捕获流程，返回 PNG dataURL 而不触发下载。
 * 空画布或尚未完成挂载时返回 null，由导出管线跳过该块。
 */
const captureForExport = async (): Promise<string | null> => {
  const stage = stageRef.value
  const rootEl = stage?.querySelector<HTMLElement>(CANVAS_EXPORT_ROOT_SELECTOR)
  if (!stage || !rootEl || nodes.value.length === 0) return null
  const bounds = measureCanvasContentBounds(rootEl, (point) =>
    flow.screenToFlowCoordinate(point),
  )
  if (!bounds) return null
  const plan = planCanvasExport(bounds, resolveCanvasBackground(getComputedStyle(stage).backgroundColor))
  return withCanvasThemeProperties(rootEl, resolveCanvasThemeProperties(stage), () =>
    renderCanvasImage(rootEl, plan, 'png'),
  )
}

const captureRegistry = inject(CANVAS_CAPTURE_KEY, null)
let unregisterCapture: (() => void) | undefined

const showMenu = (event: MouseEvent) => {
  if (props.readonly) return
  const stage = stageRef.value
  if (!stage) return
  const bounds = stage.getBoundingClientRect()
  menu.x = Math.max(8, Math.min(stage.clientWidth - 200, event.clientX - bounds.left))
  menu.y = Math.max(8, Math.min(canvasDocument.value.height - 56, event.clientY - bounds.top))
  menu.position =
    flowRef.value?.screenToFlowCoordinate({ x: event.clientX, y: event.clientY }) ?? {
      x: event.clientX - bounds.left,
      y: event.clientY - bounds.top,
    }
  menu.visible = true
}

const openMenu = (event: MouseEvent) => {
  event.preventDefault()
  showMenu(event)
}

const createNode = (shape: string, center: XYPosition) => {
  const definition = getCanvasShape(shape)
  const { width, height } = definition.defaultSize
  nodes.value.push({
    id: crypto.randomUUID(),
    type: 'shape',
    position: {
      x: Math.max(0, center.x - width / 2),
      y: Math.max(0, center.y - height / 2),
    },
    width,
    height,
    data: {
      shape: definition.shape,
      label: t(definition.labelKey),
      color: DEFAULT_NODE_COLOR,
      width,
      height,
    },
  })
}

const addShapeFromMenu = (shape: string) => {
  createNode(shape, menu.position)
  closeMenu()
}

const addShapeAtStageCenter = (shape: string) => {
  const stage = stageRef.value
  if (!stage) return
  const bounds = stage.getBoundingClientRect()
  const center =
    flowRef.value?.screenToFlowCoordinate({
      x: bounds.left + bounds.width / 2,
      y: bounds.top + bounds.height / 2,
    }) ?? { x: bounds.width / 2, y: bounds.height / 2 }
  createNode(shape, center)
}

const addShapeFromPanel = (shape: string) => {
  addShapeAtStageCenter(shape)
  if (libraryOpen.value) closeLibrary()
}

const handleDrop = (event: DragEvent) => {
  if (props.readonly) return
  const shape = event.dataTransfer?.getData(CANVAS_SHAPE_DRAG_MIME)
  if (!shape || !isCanvasShape(shape)) return
  event.preventDefault()
  const point = flowRef.value?.screenToFlowCoordinate({ x: event.clientX, y: event.clientY })
  if (point) createNode(shape, point)
}

const updateNodeLabel = (id: string, label: string) => {
  const node = findStoreNode(id)
  if (node?.data) node.data.label = label
}

const updateNodeSize = (id: string, size: { width: number; height: number }) => {
  const node = findStoreNode(id)
  if (!node?.data) return
  node.width = size.width
  node.height = size.height
  node.data.width = size.width
  node.data.height = size.height
}

const updateEdgeLabel = (id: string, label: string) => {
  const edge = findStoreEdge(id)
  if (edge?.data) edge.data.label = label
}

const startEdgeLabelEdit = (id: string) => {
  editingEdgeId.value = null
  requestAnimationFrame(() => {
    editingEdgeId.value = id
  })
}

const updateEdgeStyle = (
  id: string,
  patch: Partial<Pick<CanvasEdgeData, 'type' | 'line' | 'arrow'>>,
) => {
  const edge = findStoreEdge(id)
  if (!edge) return
  edge.data = { ...DEFAULT_EDGE_DATA(), ...edge.data, ...patch }
}

const removeEdge = (id: string) => {
  edges.value = edges.value.filter((edge) => edge.id !== id)
  if (selectedEdgeId.value === id) selectedEdgeId.value = null
  // 端点锚点随线段一起消失（仅剩无依附锚点时回收）
  scheduleAnchorPrune()
}

// 画布内剪贴板：模块级共享，支持跨画布实例粘贴
let canvasClipboard: CanvasClipboardPayload | null = null

const isEditingText = (event: KeyboardEvent) =>
  (event.target as HTMLElement | null)?.closest('textarea, input') !== null

const copySelection = () => {
  const selectedIds = new Set(flow.getSelectedNodes.value.map((node) => node.id))
  if (selectedIds.size === 0) return
  const current = serialize()
  canvasClipboard = buildClipboardPayload(selectedIds, current.nodes, current.edges)
}

const pasteClipboard = () => {
  if (!canvasClipboard) return
  const payload = remapClipboardForPaste(canvasClipboard, GRID_SIZE, () => crypto.randomUUID())
  const nodeList = nodes.value as unknown as Array<{ selected?: boolean }>
  const edgeList = edges.value as unknown as Array<{ selected?: boolean }>
  nodeList.forEach((node) => {
    node.selected = false
  })
  edgeList.forEach((edge) => {
    edge.selected = false
  })
  nodes.value.push(
    ...payload.nodes.map((node) => ({ ...createFlowNode(node), selected: true })),
  )
  edges.value.push(...payload.edges.map(createFlowEdge))
}

const NUDGE_DIRECTIONS: Readonly<Record<string, XYPosition>> = {
  ArrowUp: { x: 0, y: -1 },
  ArrowDown: { x: 0, y: 1 },
  ArrowLeft: { x: -1, y: 0 },
  ArrowRight: { x: 1, y: 0 },
}

const nudgeSelection = (event: KeyboardEvent) => {
  const direction = NUDGE_DIRECTIONS[event.key]
  if (!direction) return
  const selected = flow.getSelectedNodes.value
  if (selected.length === 0) return
  event.preventDefault()
  const step = event.shiftKey ? GRID_SIZE : 1
  selected.forEach((node) => {
    const storeNode = findStoreNode(node.id)
    if (!storeNode) return
    storeNode.position = {
      x: storeNode.position.x + direction.x * step,
      y: storeNode.position.y + direction.y * step,
    }
  })
}

const clearSelection = () => {
  const nodeList = nodes.value as unknown as Array<{ selected?: boolean }>
  const edgeList = edges.value as unknown as Array<{ selected?: boolean }>
  nodeList.forEach((node) => {
    node.selected = false
  })
  edgeList.forEach((edge) => {
    edge.selected = false
  })
  selectedEdgeId.value = null
}

/** 用户最近一次指针交互是否落在本画布内，决定编辑器焦点下快捷键的归属。 */
let pointerInsideCanvas = false

const focusedWithinCanvas = (target: EventTarget | null): boolean => {
  const root = rootRef.value
  if (!root || !(target instanceof Node)) return false
  // 焦点在画布内，或落在包住本画布块的 ProseMirror 编辑器根上
  return root.contains(target) || (target instanceof HTMLElement && target.contains(root))
}

const handleDocumentKeydown = (event: KeyboardEvent) => {
  if (props.readonly || event.defaultPrevented) return
  const root = rootRef.value
  const target = event.target as Node | null
  if (!root || !target) return
  const inside = root.contains(target)
  const owned = inside || (pointerInsideCanvas && focusedWithinCanvas(target))
  const isDelete = event.key === 'Delete' || event.key === 'Backspace'

  if (isDelete && !isEditingText(event)) {
    if (!owned) return
    // Delete/Backspace 只作用于画布内选中元素，并阻止冒泡避免 tiptap 删除整个画布节点
    event.preventDefault()
    event.stopPropagation()
    if (hasSelection.value || selectedEdgeId.value) removeSelection()
    return
  }

  if (!owned || isEditingText(event)) return
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'c') {
    event.preventDefault()
    event.stopPropagation()
    copySelection()
    return
  }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'v') {
    event.preventDefault()
    event.stopPropagation()
    pasteClipboard()
    return
  }
  nudgeSelection(event)
}

const handleDocumentPointerDown = (event: Event) => {
  const root = rootRef.value
  pointerInsideCanvas = !!root && event.target instanceof Node && root.contains(event.target)
}

const handleDocumentFocusIn = (event: FocusEvent) => {
  if (props.readonly) return
  const root = rootRef.value
  const target = event.target as Node | null
  if (!root || !target || focusedWithinCanvas(target)) return
  // 焦点移出画布后清空画布选中态，避免残留选中导致快捷键误删元素
  if (hasSelection.value || selectedEdgeId.value) clearSelection()
}

let resizeObserver: ResizeObserver | null = null

/** 弹窗/全屏过渡会临时给祖先加 transform，动画结束后补测，避免动画期坐标残留。 */
const handleLayoutAnimationEnd = (event: Event) => {
  const root = rootRef.value
  const target = event.target as Node | null
  if (!root || !target) return
  if (root.contains(target) || (target instanceof HTMLElement && target.contains(root))) {
    scheduleInternalsRefresh()
  }
}

onMounted(() => {
  coarsePointerQuery = window.matchMedia?.('(pointer: coarse)') ?? null
  coarsePointer.value = !!coarsePointerQuery?.matches
  coarsePointerQuery?.addEventListener?.('change', handleCoarsePointerChange)
  document.addEventListener('keydown', handleDocumentKeydown, true)
  document.addEventListener('pointerdown', handleDocumentPointerDown, true)
  document.addEventListener('focusin', handleDocumentFocusIn, true)
  document.addEventListener('visibilitychange', handleVisibilityChange)
  document.addEventListener('keydown', handleShiftDown, true)
  document.addEventListener('keyup', handleShiftUp, true)
  window.addEventListener('blur', handleWindowBlur)
  window.addEventListener('pageshow', handlePageShow)
  window.addEventListener('transitionend', handleLayoutAnimationEnd, true)
  window.addEventListener('transitioncancel', handleLayoutAnimationEnd, true)
  window.addEventListener('animationend', handleLayoutAnimationEnd, true)
  const stage = stageRef.value
  if (stage && typeof ResizeObserver !== 'undefined') {
    // 容器尺寸变化（弹窗↔全屏、侧栏收起）后刷新节点内部尺寸，
    // 避免连线仍锚在旧测量位置导致扭曲
    resizeObserver = new ResizeObserver(handleStageResize)
    resizeObserver.observe(stage)
  }
  if (captureRegistry && rootRef.value) {
    unregisterCapture = captureRegistry.register({
      element: rootRef.value,
      capture: captureForExport,
    })
  }
})

onBeforeUnmount(() => {
  unregisterCapture?.()
  unregisterCapture = undefined
  coarsePointerQuery?.removeEventListener?.('change', handleCoarsePointerChange)
  coarsePointerQuery = null
  document.removeEventListener('keydown', handleDocumentKeydown, true)
  document.removeEventListener('pointerdown', handleDocumentPointerDown, true)
  document.removeEventListener('focusin', handleDocumentFocusIn, true)
  document.removeEventListener('visibilitychange', handleVisibilityChange)
  document.removeEventListener('keydown', handleShiftDown, true)
  document.removeEventListener('keyup', handleShiftUp, true)
  window.removeEventListener('blur', handleWindowBlur)
  window.removeEventListener('pageshow', handlePageShow)
  window.removeEventListener('transitionend', handleLayoutAnimationEnd, true)
  window.removeEventListener('transitioncancel', handleLayoutAnimationEnd, true)
  window.removeEventListener('animationend', handleLayoutAnimationEnd, true)
  resizeObserver?.disconnect()
  refreshTimers.forEach((timer) => window.clearTimeout(timer))
  refreshTimers = []
})

const connect = (connection: Connection) => {
  connectionCompleted = true
  if (props.readonly || !connection.source || !connection.target) return
  const duplicate = edges.value.some(
    (edge) =>
      edge.source === connection.source &&
      edge.target === connection.target &&
      edge.sourceHandle === connection.sourceHandle &&
      edge.targetHandle === connection.targetHandle,
  )
  if (duplicate) return
  edges.value.push({
    id: crypto.randomUUID(),
    source: connection.source,
    target: connection.target,
    sourceHandle: connection.sourceHandle,
    targetHandle: connection.targetHandle,
    type: 'canvas',
    data: DEFAULT_EDGE_DATA(),
  })
}

const updateConnection = ({ edge, connection }: EdgeUpdateEvent) => {
  // edge-update 仅在落到有效 handle 时触发：标记本次手势为"改接"，不再按脱离处理
  if (edgeUpdaterGesture) edgeUpdaterGesture.updated = true
  if (props.readonly || !connection.source || !connection.target) return
  const index = edges.value.findIndex((candidate) => candidate.id === edge.id)
  if (index < 0) return
  const current = edges.value[index]
  if (!current) return
  // 必须替换数组引用：VueFlow 的 v-model 同步只监听数组引用与长度，
  // 原位替换元素不会传到 store，改接结果会被丢弃（DOM 保留旧连接）
  const next = [...edges.value]
  next[index] = {
    ...current,
    source: connection.source,
    target: connection.target,
    sourceHandle: connection.sourceHandle,
    targetHandle: connection.targetHandle,
  }
  edges.value = next
  scheduleAnchorPrune()
}

const removeSelection = () => {
  if (props.readonly) return
  const selectedNodeIds = new Set(flow.getSelectedNodes.value.map((node) => node.id))
  const selectedEdgeIds = new Set(flow.getSelectedEdges.value.map((edge) => edge.id))
  nodes.value = nodes.value.filter((node) => !selectedNodeIds.has(node.id))
  edges.value = edges.value.filter(
    (edge) =>
      !selectedEdgeIds.has(edge.id) &&
      !selectedNodeIds.has(edge.source) &&
      !selectedNodeIds.has(edge.target),
  )
  scheduleAnchorPrune()
}

/** —— 自由连线：端点可悬空（自由锚点），也支持整条独立线段 —— */

let connectionCompleted = false
const connectionStart = ref<OnConnectStartParams | null>(null)

const createAnchor = (x: number, y: number): CanvasNodeSpec => ({
  id: crypto.randomUUID(),
  shape: 'anchor',
  x: Math.max(0, x - 5),
  y: Math.max(0, y - 5),
  width: 10,
  height: 10,
  label: '',
  color: DEFAULT_NODE_COLOR,
})

const handleConnectStart = (params: OnConnectStartParams) => {
  connectionCompleted = false
  connectionStart.value = params
}

const handleConnectEnd = (event?: MouseEvent | TouchEvent) => {
  const start = connectionStart.value
  connectionStart.value = null
  if (props.readonly || connectionCompleted || !start) return
  // 端点改接（updater 拖拽）同样会触发 connect-end，其连接语义由 edge-update 承担，
  // 此时不再按"拖 Handle 释放空白"生成悬空端点，避免遗留多余锚点与线段
  if (edgeUpdaterActive) return
  const native = event as MouseEvent | undefined
  if (!native || typeof native.clientX !== 'number') return
  // 仅在画布空白处释放时生成悬空端点；释放到控件/面板等区域忽略
  const target = native.target as HTMLElement | null
  if (!target?.closest('.vue-flow__pane')) return
  const point = flowRef.value?.screenToFlowCoordinate({ x: native.clientX, y: native.clientY })
  if (!point) return
  const anchor = createAnchor(point.x, point.y)
  const anchorId = anchor.id
  const startedFromTarget = start.handleType === 'target'
  nodes.value.push(createFlowNode(anchor))
  edges.value.push({
    id: crypto.randomUUID(),
    source: startedFromTarget ? anchorId : (start.nodeId as string),
    target: startedFromTarget ? (start.nodeId as string) : anchorId,
    sourceHandle: startedFromTarget ? undefined : (start.handleId ?? undefined),
    targetHandle: startedFromTarget ? (start.handleId ?? undefined) : undefined,
    type: 'canvas',
    data: DEFAULT_EDGE_DATA(),
  })
}

/** 改接把手拖到空白处释放的最小位移：低于该值视为点击/抖动，端点保持原连接 */
const DETACH_MIN_DRAG = 8

/**
 * 端点改接把手释放到空白处：该端脱离元素/锚点，在释放点生成悬空锚点，
 * 使元素连线端点与自由连线端点完全同质（可随时再拖回元素边缘吸附）。
 */
const detachEdgeEndToAnchor = (gesture: EdgeUpdaterGesture, native: MouseEvent | undefined) => {
  if (props.readonly || !native || typeof native.clientX !== 'number') return
  const moved = Math.hypot(native.clientX - gesture.origin.x, native.clientY - gesture.origin.y)
  if (moved < DETACH_MIN_DRAG) return
  if (!(native.target as HTMLElement | null)?.closest('.vue-flow__pane')) return
  const point = flowRef.value?.screenToFlowCoordinate({ x: native.clientX, y: native.clientY })
  if (!point) return
  const index = edges.value.findIndex((candidate) => candidate.id === gesture.edgeId)
  if (index < 0) return
  const current = edges.value[index]
  if (!current) return
  const storeEdge = findStoreEdge(gesture.edgeId)
  const otherX = gesture.end === 'source' ? storeEdge?.targetX : storeEdge?.sourceX
  const otherY = gesture.end === 'source' ? storeEdge?.targetY : storeEdge?.sourceY
  const handle = sideFor((otherX ?? point.x) - point.x, (otherY ?? point.y) - point.y)
  const anchor = createAnchor(point.x, point.y)
  nodes.value.push(createFlowNode(anchor))
  // 必须替换数组引用：原位替换元素不会同步到 store（v-model 监听只覆盖引用与长度）
  const next = [...edges.value]
  next[index] = {
    ...current,
    ...(gesture.end === 'source'
      ? { source: anchor.id, sourceHandle: handle }
      : { target: anchor.id, targetHandle: handle }),
  }
  edges.value = next
  scheduleAnchorPrune()
}

const addFreeLine = () => {
  const start = menu.position
  const from = createAnchor(start.x, start.y)
  const to = createAnchor(start.x + 160, start.y)
  nodes.value.push(createFlowNode(from), createFlowNode(to))
  edges.value.push({
    id: crypto.randomUUID(),
    source: from.id,
    target: to.id,
    sourceHandle: 'right',
    targetHandle: 'left',
    type: 'canvas',
    data: DEFAULT_EDGE_DATA(),
  })
  closeMenu()
}

/** —— 自由划线：空白处按住拖动即画线（无需模式），起止点可悬空或吸附到 Handle —— */

const lineDraft = ref<{ start: CanvasPoint; current: CanvasPoint } | null>(null)
let drawingLine = false
/** 编辑模式按住 Shift 时左键拖拽为平移（panActivationKeyCode），pane 光标同步切成抓手 */
const shiftPressed = ref(false)

const roundPoint2 = (point: CanvasPoint): CanvasPoint => ({
  x: Math.round(point.x * 100) / 100,
  y: Math.round(point.y * 100) / 100,
})

const handleShiftDown = (event: KeyboardEvent) => {
  if (event.key === 'Shift') shiftPressed.value = true
}

const handleShiftUp = (event: KeyboardEvent) => {
  if (event.key === 'Shift') shiftPressed.value = false
}

const handleWindowBlur = () => {
  shiftPressed.value = false
}

const sideFor = (dx: number, dy: number): string =>
  Math.abs(dx) >= Math.abs(dy) ? (dx >= 0 ? 'right' : 'left') : dy >= 0 ? 'bottom' : 'top'

const HANDLE_SNAP_RADIUS = 48

interface HandleHit extends CanvasHandleCandidate {
  element: HTMLElement
}

const collectHandleHits = (excludeAnchors: boolean): HandleHit[] => {
  const hits: HandleHit[] = []
  const handles = rootRef.value?.querySelectorAll<HTMLElement>('.vue-flow__handle')
  handles?.forEach((handle) => {
    const rect = handle.getBoundingClientRect()
    if (rect.width === 0 && rect.height === 0) return
    const nodeEl = handle.closest('.vue-flow__node') as HTMLElement | null
    const nodeId = nodeEl?.getAttribute('data-id')
    if (!nodeId) return
    if (excludeAnchors && findStoreNode(nodeId)?.data?.shape === 'anchor') return
    hits.push({
      nodeId,
      handleId: handle.getAttribute('data-handleid'),
      element: handle,
      x: rect.x + rect.width / 2,
      y: rect.y + rect.height / 2,
    })
  })
  return hits
}

/**
 * 半径内最近 Handle 命中（与 VueFlow connectionRadius 一致）：
 * elementFromPoint 会被文本编辑框/覆盖物挡住，改用坐标距离扫描。
 * excludeAnchors 用于拖动悬空端点时的吸附：锚点自身的把手不参与命中。
 */
const resolveHandleAt = (
  clientX: number,
  clientY: number,
  options?: { excludeAnchors?: boolean },
): HandleHit | null =>
  pickNearestHandle(collectHandleHits(!!options?.excludeAnchors), clientX, clientY, HANDLE_SNAP_RADIUS)

/** 中键不再平移，但仍拦下浏览器默认的中键自动滚动（内容超出视口时会被触发）。 */
const blockMiddleClickAutoscroll = (event: MouseEvent) => {
  if (event.button === 1) event.preventDefault()
}

const handleStagePointerDown = (event: PointerEvent) => {
  const target = event.target as HTMLElement | null
  if (target?.closest('.vue-flow__pane')) viewportTouched = true
  // 空白处左键拖拽 = 自由划线；Shift+拖拽为平移（panActivationKeyCode），框选已让位给平移
  if (props.readonly || event.pointerType === 'touch' || event.button !== 0 || event.shiftKey) return
  if (!target?.closest('.vue-flow__pane')) return
  if (target.closest('.vue-flow__node, .vue-flow__edge, .edge-label, .edge-vertex, .edge-vertex-add')) {
    return
  }
  const start = flowRef.value?.screenToFlowCoordinate({ x: event.clientX, y: event.clientY })
  if (!start) return
  event.preventDefault()
  event.stopPropagation()
  drawingLine = true
  lineDraft.value = { start: roundPoint2(start), current: roundPoint2(start) }

  const onMove = (moveEvent: PointerEvent) => {
    if (!drawingLine || !lineDraft.value) return
    const point = flowRef.value?.screenToFlowCoordinate({
      x: moveEvent.clientX,
      y: moveEvent.clientY,
    })
    if (point) lineDraft.value.current = roundPoint2(point)
  }
  const onUp = (upEvent: PointerEvent) => {
    drawingLine = false
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    finishLineDraft(upEvent)
  }
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

const finishLineDraft = (event: PointerEvent) => {
  const draft = lineDraft.value
  lineDraft.value = null
  if (!draft) return
  const start = draft.start
  const upPoint = flowRef.value?.screenToFlowCoordinate({ x: event.clientX, y: event.clientY })
  // 最小拖动距离对吸附分支同样生效：空白处单击（未拖动）只做取消选中，
  // 即使落点在已有端点的吸附半径内也不得生成线段与悬空锚点
  if (!upPoint || Math.hypot(upPoint.x - start.x, upPoint.y - start.y) < 8) return
  const endHit = resolveHandleAt(event.clientX, event.clientY)
  const end = endHit ? undefined : upPoint
  const targetPoint = end ? roundPoint2(end) : start

  const dx = end ? end.x - start.x : 0
  const dy = end ? end.y - start.y : 0
  const fromNode = createAnchor(start.x, start.y)
  nodes.value.push(createFlowNode(fromNode))
  let targetId = ''
  let targetHandle: string | undefined
  if (endHit) {
    targetId = endHit.nodeId
    targetHandle = endHit.handleId ?? undefined
  } else {
    const toNode = createAnchor(targetPoint.x, targetPoint.y)
    nodes.value.push(createFlowNode(toNode))
    targetId = toNode.id
    targetHandle = sideFor(-dx, -dy)
  }
  edges.value.push({
    id: crypto.randomUUID(),
    source: fromNode.id,
    target: targetId,
    sourceHandle: end ? sideFor(dx, dy) : 'right',
    targetHandle,
    type: 'canvas',
    data: DEFAULT_EDGE_DATA(),
  })
}

let anchorPruneScheduled = false

/** 删除连线/端点后回收没有连线依附的自由锚点（创建为同步入列，微任务清理安全）。 */
const scheduleAnchorPrune = () => {
  if (anchorPruneScheduled || props.readonly) return
  anchorPruneScheduled = true
  // 必须放宏任务：Vue Flow 在 store→props 回写（pauseStore 观察 store 变化后写回 models）期间
  // 会暂停 props→store 的监听（pauseModel.pause），紧跟微任务替换 nodes 数组会被暂停窗口吞掉，
  // 表现为组件/文档已删锚点而 store 与 DOM 保留旧节点；宏任务在所有暂停恢复后执行，不再丢失。
  window.setTimeout(() => {
    anchorPruneScheduled = false
    const connected = new Set<string>()
    edges.value.forEach((edge) => {
      connected.add(edge.source)
      connected.add(edge.target)
    })
    const kept = nodes.value.filter(
      (node) => node.data?.shape !== 'anchor' || connected.has(node.id),
    )
    if (kept.length !== nodes.value.length) nodes.value = kept
  }, 0)
}

/** —— 折点：贝塞尔/正交线的拐角增删移 —— */

const updateEdgeWaypoints = (
  id: string,
  mutate: (list: CanvasPoint[]) => CanvasPoint[],
) => {
  const edge = findStoreEdge(id)
  if (!edge?.data) return
  const data = edge.data as CanvasEdgeData
  data.waypoints = mutate((data.waypoints ?? []).map((point) => ({ ...point })))
}

const addEdgeWaypoint = (id: string, insertAfter: number, x: number, y: number) => {
  updateEdgeWaypoints(id, (list) => {
    list.splice(Math.max(0, Math.min(insertAfter, list.length)), 0, {
      x: Math.round(x * 100) / 100,
      y: Math.round(y * 100) / 100,
    })
    return list
  })
}

const moveEdgeWaypoint = (id: string, index: number, screenX: number, screenY: number) => {
  const point = flowRef.value?.screenToFlowCoordinate({ x: screenX, y: screenY })
  if (!point) return
  updateEdgeWaypoints(id, (list) => {
    if (!list[index]) return list
    list[index] = { x: Math.round(point.x * 10) / 10, y: Math.round(point.y * 10) / 10 }
    return list
  })
}

const removeEdgeWaypoint = (id: string, index: number) => {
  updateEdgeWaypoints(id, (list) => {
    list.splice(index, 1)
    return list
  })
}
</script>

<style scoped>
.canvas-block {
  --canvas-surface: var(--theme-bg-base);
  --canvas-stage: var(--admin-canvas);
  --canvas-border: var(--theme-border);
  --canvas-text: var(--theme-text-primary);
  --canvas-muted: var(--theme-text-secondary);
  --canvas-accent: var(--theme-primary);
  overflow: hidden;
  border: 1px solid var(--canvas-border);
  border-radius: 12px;
  background: var(--canvas-surface);
  container-type: inline-size;
}

.canvas-block.is-readonly {
  border-color: var(--canvas-border);
}

.canvas-block.is-fullscreen {
  display: flex;
  border-radius: 0;
  flex-direction: column;
}

.canvas-block.is-fullscreen .canvas-body {
  flex: 1;
  min-height: 0;
}

.canvas-fullscreen-toggle {
  position: absolute;
  z-index: 12;
  top: 10px;
  right: 10px;
  display: grid;
  width: 36px;
  min-width: 0;
  height: 36px;
  min-height: 36px;
  padding: 0;
  border-radius: 8px;
  place-items: center;
  box-shadow: 0 3px 10px rgb(0 0 0 / 8%);
}

.canvas-fullscreen-toggle svg {
  width: 15px;
  height: 15px;
}

.canvas-header {
  display: flex;
  min-height: 48px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 6px 10px 6px 14px;
  border-bottom: 1px solid var(--canvas-border);
  background: var(--canvas-surface);
}

.canvas-header > div:first-child {
  display: flex;
  min-width: 0;
  align-items: baseline;
  gap: 12px;
}

.canvas-header strong {
  color: var(--canvas-text);
}

.canvas-header span {
  overflow: hidden;
  color: var(--canvas-muted);
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.canvas-actions {
  display: flex;
  flex: 0 0 auto;
  gap: 8px;
}

.canvas-library-toggle,
.canvas-library-scrim {
  display: none;
}

@media (pointer: coarse) {
  /* 触屏嵌入态封顶可视高度：文档高度语义不变，超出部分靠拖动画布查看，全屏仍铺满 */
  .canvas-block:not(.is-fullscreen) .canvas-body {
    max-height: min(45vh, 360px);
    max-height: min(45dvh, 360px);
  }
}

button {
  min-height: 36px;
  padding: 0 12px;
  border: 1px solid var(--canvas-border);
  border-radius: 7px;
  background: var(--canvas-surface);
  color: var(--canvas-text);
  cursor: pointer;
}

button:hover:not(:disabled) {
  border-color: var(--canvas-accent);
  color: var(--canvas-accent);
}

button:focus-visible {
  outline: 2px solid var(--canvas-accent);
  outline-offset: 2px;
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}

.canvas-delete {
  color: var(--theme-error);
}

.canvas-actions button.is-active {
  border-color: var(--canvas-accent);
  background: color-mix(in srgb, var(--canvas-accent) 10%, var(--canvas-surface));
  color: var(--canvas-accent);
}

.stage :deep(.vue-flow__pane) {
  cursor: crosshair;
}

/* 编辑模式 Shift+左键平移：按住 Shift 即提示抓手，按下时握紧 */
.stage.is-pan-shift :deep(.vue-flow__pane) {
  cursor: grab;
}

.stage.is-pan-shift :deep(.vue-flow__pane:active) {
  cursor: grabbing;
}

/* 触屏直接拖动平移（无按键可用），同样给出抓手提示 */
.stage.is-pan-drag :deep(.vue-flow__pane) {
  cursor: grab;
}

.stage.is-pan-drag :deep(.vue-flow__pane:active) {
  cursor: grabbing;
}

/* 阅读模式没有绘制操作，平移是主要交互，光标用抓手（按下时握紧） */
.canvas-block.is-readonly .stage :deep(.vue-flow__pane) {
  cursor: grab;
}

.canvas-block.is-readonly .stage :deep(.vue-flow__pane:active) {
  cursor: grabbing;
}

.canvas-line-preview {
  position: absolute;
  z-index: 14;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}

.canvas-line-preview line {
  stroke: var(--canvas-accent);
  stroke-width: 1.5;
  stroke-dasharray: 5 4;
}

.canvas-body {
  position: relative;
  display: flex;
  align-items: stretch;
}

.stage {
  position: relative;
  flex: 1;
  height: 100%;
  min-width: 0;
  overflow: hidden;
  background: var(--canvas-stage);
}

.stage :deep(.vue-flow) {
  width: 100%;
  height: 100%;
}

.stage :deep(.vue-flow__node-shape) {
  border: 0;
  background: transparent;
}

/* 选中/悬停态对齐 Vue Flow 官方主题：0.5px 紧贴细环 + 柔和抬升阴影，不做偏移描边 */
.stage :deep(.vue-flow__node.selectable:hover .canvas-shape) {
  box-shadow: 0 1px 4px 1px rgb(0 0 0 / 8%);
}

.stage :deep(.vue-flow__node.selected .canvas-shape),
.stage :deep(.vue-flow__node.selected .canvas-shape:hover) {
  outline: none;
  box-shadow: 0 0 0 0.5px var(--shape-accent, var(--canvas-accent));
}

.stage :deep(.vue-flow__resize-control.line) {
  border-color: transparent;
}

.stage :deep(.vue-flow__resize-control.handle) {
  width: 8px;
  height: 8px;
  border: 1px solid var(--shape-accent, var(--canvas-accent));
  border-radius: 2px;
  background: var(--canvas-surface);
}

/* 选中态不加粗不改色，仅由 CanvasEdge 的主题色光晕表达 */

.stage :deep(.vue-flow__edge-path) {
  stroke: var(--canvas-muted);
}

/* 端点改接把手（端点外侧的透明圆）：拖出即改接/脱离，给出可拖拽光标 */
.stage :deep(.vue-flow__edgeupdater) {
  cursor: grab;
}

.stage :deep(.vue-flow__controls) {
  overflow: hidden;
  border: 1px solid var(--canvas-border);
  border-radius: 8px;
  box-shadow: 0 5px 16px rgb(0 0 0 / 10%);
}

.stage :deep(.vue-flow__controls-button) {
  min-height: 32px;
  border: 0;
  border-bottom: 1px solid var(--canvas-border);
  border-radius: 0;
  background: var(--canvas-surface);
}

.stage :deep(.vue-flow__minimap) {
  border: 1px solid var(--canvas-border);
  border-radius: 8px;
  background: var(--canvas-surface);
}

.canvas-guides {
  position: absolute;
  z-index: 12;
  inset: 0;
  pointer-events: none;
}

.guide {
  position: absolute;
  background: var(--canvas-accent);
}

.guide.is-vertical {
  top: 0;
  bottom: 0;
  width: 1px;
}

.guide.is-horizontal {
  left: 0;
  right: 0;
  height: 1px;
}

.empty {
  position: absolute;
  z-index: 2;
  inset: 50% auto auto 50%;
  width: min(440px, calc(100% - 48px));
  margin: 0;
  transform: translate(-50%, -50%);
  color: var(--canvas-muted);
  text-align: center;
  pointer-events: none;
}

.canvas-menu {
  position: absolute;
  z-index: 20;
  width: 192px;
  max-height: 320px;
  overflow-y: auto;
  padding: 6px;
  border: 1px solid var(--canvas-border);
  border-radius: 9px;
  background: var(--canvas-surface);
  box-shadow: 0 12px 34px rgb(0 0 0 / 18%);
}

.canvas-menu-group > span {
  display: block;
  padding: 6px 10px 2px;
  color: var(--canvas-muted);
  font-size: 11px;
}

.canvas-menu-group button {
  display: flex;
  width: 100%;
  min-height: 34px;
  align-items: center;
  padding: 0 10px;
  border-color: transparent;
  text-align: left;
}

.canvas-menu-group button:hover {
  background: color-mix(in srgb, var(--canvas-accent) 10%, var(--canvas-surface));
}

.canvas-export {
  position: absolute;
  z-index: 15;
  top: 10px;
  right: 54px;
}

.canvas-export > button {
  display: grid;
  width: 36px;
  min-width: 0;
  height: 36px;
  min-height: 36px;
  padding: 0;
  border-radius: 8px;
  place-items: center;
  box-shadow: 0 3px 10px rgb(0 0 0 / 8%);
}

.canvas-export-toggle svg {
  width: 15px;
  height: 15px;
}

.canvas-export-menu {
  position: absolute;
  top: calc(100% + 4px);
  right: 0;
  min-width: 96px;
  padding: 4px;
  border: 1px solid var(--canvas-border);
  border-radius: 8px;
  background: var(--canvas-surface);
  box-shadow: 0 12px 34px rgb(0 0 0 / 18%);
}

.canvas-export-menu button {
  display: flex;
  width: 100%;
  min-height: 32px;
  align-items: center;
  justify-content: center;
  border-color: transparent;
}

.canvas-export-menu button:hover {
  background: color-mix(in srgb, var(--canvas-accent) 10%, var(--canvas-surface));
}

@container (max-width: 799px) {
  .canvas-library-toggle {
    display: inline-flex;
    align-items: center;
  }

  .canvas-body :deep(.stencil-panel) {
    position: absolute;
    z-index: 18;
    inset: 0 auto 0 0;
    display: none;
    width: min(240px, 80%);
    border-right: 1px solid var(--canvas-border);
  }

  .canvas-library-scrim {
    position: absolute;
    z-index: 17;
    inset: 0;
    display: block;
    background: rgb(0 0 0 / 15%);
  }

  .canvas-block.is-library-open .canvas-body :deep(.stencil-panel) {
    display: flex;
  }

  .canvas-header {
    flex-wrap: wrap;
    gap: 6px;
  }

  .canvas-header span,
  .canvas-minimap {
    display: none;
  }

  .canvas-actions {
    flex-wrap: wrap;
  }

  .canvas-actions button {
    min-height: 44px;
  }

  .canvas-body :deep(.stencil-items) {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@container (max-width: 479px) {
  .canvas-body :deep(.stencil-panel) {
    inset: auto 0 0 0;
    width: 100%;
    max-height: min(65%, 360px);
    border-right: 0;
    border-top: 1px solid var(--canvas-border);
  }
}

@media (max-width: 640px) {
  button {
    min-height: 44px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .stage :deep(*) {
    scroll-behavior: auto !important;
    transition-duration: 0.01ms !important;
  }
}
</style>

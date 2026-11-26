<template>
  <div class="designer-page">
    <header class="designer-header">
      <div class="header-main">
        <n-button quaternary class="back-button" @click="backToList">←</n-button>
        <div class="header-copy">
          <div class="title-row">
            <h2>{{ pageTitle }}</h2>
            <ApprovalStatusTag v-if="definition" kind="flow" :status="definition.status" />
            <n-tag v-if="definition" size="small" :bordered="false">v{{ definition.version ?? 1 }}</n-tag>
          </div>
          <p>{{ definition?.code }}<span v-if="form.businessType"> · {{ form.businessType }}</span></p>
        </div>
      </div>
      <div class="header-actions">
        <n-button secondary @click="showSettings = !showSettings">
          {{ t('Approval:Designer:FlowSettings') }}
        </n-button>
        <n-button v-if="isEditable" secondary :loading="validating" @click="validateDraft">
          {{ t('Approval:Designer:Validate') }}
        </n-button>
        <n-button v-if="isEditable" secondary :loading="saving" @click="save(false)">
          {{ t('Approval:Definition:SaveDraft') }}
        </n-button>
        <n-button v-if="isEditable" type="primary" :loading="saving" @click="save(true)">
          {{ t('Approval:Definition:SaveAndPublish') }}
        </n-button>
      </div>
    </header>

    <n-empty v-if="!definitionId" :description="t('Approval:Definition:MissingId')">
      <template #extra>
        <n-button type="primary" @click="backToList">
          {{ t('Approval:Definition:BackToDefinition') }}
        </n-button>
      </template>
    </n-empty>

    <n-spin v-else :show="loading" class="designer-spin">
      <n-alert v-if="!isEditable && definition" class="status-alert" type="info" :bordered="false">
        {{ t('Approval:Definition:ReadOnlyHint') }}
      </n-alert>

      <n-collapse-transition :show="showSettings">
      <section class="settings-panel">
        <div class="panel-heading">
          <div>
            <h3>{{ t('Approval:Definition:BasicSection') }}</h3>
            <p>{{ t('Approval:Designer:SettingsHint') }}</p>
          </div>
          <n-button quaternary size="small" @click="showSettings = false">×</n-button>
        </div>
        <n-grid :cols="24" :x-gap="12" item-responsive responsive="screen">
          <PoFormItem gi :span="24" :sm="8" :label="t('Approval:Field:Code')">
            <n-input :value="definition?.code ?? ''" disabled />
          </PoFormItem>
          <PoFormItem gi :span="24" :sm="8" :label="t('Approval:Field:Version')">
            <n-input :value="`v${definition?.version ?? 1}`" disabled />
          </PoFormItem>
          <PoFormItem gi :span="24" :sm="8" :label="t('Approval:Field:Status')">
            <ApprovalStatusTag kind="flow" :status="definition?.status" />
          </PoFormItem>
          <PoFormItem gi :span="24" :sm="8" :label="t('Approval:Field:Name')">
            <n-input v-model:value="form.name" :disabled="!isEditable" />
          </PoFormItem>
          <PoFormItem gi :span="24" :sm="8" :label="t('Approval:Field:BusinessType')">
            <n-select
              v-model:value="form.businessType"
              clearable
              :disabled="!isEditable"
              :options="businessTypeOptions"
            />
          </PoFormItem>
          <PoFormItem gi :span="24" :sm="8" :label="t('Approval:Field:FallbackApprovers')">
            <ApprovalUserSelect
              v-model:value="form.fallbackApproverUserIds"
              multiple
              :disabled="!isEditable"
            />
          </PoFormItem>
          <PoFormItem
            gi
            :span="24"
            :sm="8"
            :label="t('Approval:Field:WithdrawWindowHours')"
            :description="t('Approval:Definition:WithdrawWindowPlaceholder')"
          >
            <n-input-number
              v-model:value="form.withdrawWindowHours"
              clearable
              :min="1"
              :max="8760"
              :disabled="!isEditable || !form.allowWithdraw"
            />
          </PoFormItem>
          <PoFormItem gi :span="24" :sm="16" :label="t('Approval:Field:Description')">
            <n-input
              v-model:value="form.description"
              type="textarea"
              :autosize="{ minRows: 2, maxRows: 4 }"
              :disabled="!isEditable"
            />
          </PoFormItem>
        </n-grid>
        <div class="switch-row">
          <n-checkbox v-model:checked="form.allowWithdraw" :disabled="!isEditable">
            {{ t('Approval:Field:AllowWithdraw') }}
          </n-checkbox>
          <n-checkbox v-model:checked="form.autoPassWhenInitiator" :disabled="!isEditable">
            {{ t('Approval:Field:AutoPassWhenInitiator') }}
          </n-checkbox>
        </div>
      </section>
      </n-collapse-transition>

      <main class="designer-workspace">
        <aside class="palette-panel">
          <div class="panel-heading compact">
            <div>
              <h3>{{ t('Approval:Designer:Palette') }}</h3>
              <p>{{ t('Approval:Designer:PaletteHint') }}</p>
            </div>
          </div>
          <div class="palette-list">
          <button
            v-for="item in PALETTE_NODE_TYPES"
            :key="item"
            type="button"
            class="palette-item"
            :class="`is-${item}`"
            :disabled="!isEditable"
            draggable="true"
            @dragstart="startDrag($event, item)"
            @click="appendNode(item)"
          >
            <span class="palette-icon" :class="`is-${item}`">{{ nodeTypeGlyph(item) }}</span>
            <span class="palette-copy">
              <strong>{{ t(nodeTypeLabelKey(item)) }}</strong>
              <small>{{ t(messageKey('approval', 'designer', 'nodeHint', item)) }}</small>
            </span>
            <span class="palette-add">＋</span>
          </button>
          </div>
        </aside>

        <section class="canvas-panel" @dragover.prevent @drop="dropOnCanvas">
          <div class="canvas-toolbar">
            <div class="canvas-title">
              <strong>{{ t('Approval:Designer:Canvas') }}</strong>
              <span>{{ t('Approval:Designer:CanvasSummary', { nodes: nodes.length, edges: edges.length }) }}</span>
            </div>
            <div class="canvas-actions">
              <n-button size="small" secondary @click="fitCanvas">{{ t('Approval:Designer:FitView') }}</n-button>
              <n-button v-if="isEditable" size="small" secondary @click="arrangeCanvas">
                {{ t('Approval:Designer:AutoLayout') }}
              </n-button>
            </div>
          </div>
          <div class="stage">
            <VueFlow
              :id="flowId"
              ref="flowRef"
              v-model:nodes="nodes"
              v-model:edges="edges"
              :pan-on-drag="[0, 1]"
              :nodes-draggable="isEditable"
              :nodes-connectable="isEditable"
              :connection-mode="ConnectionMode.Loose"
              :connection-radius="72"
              :edges-updatable="isEditable"
              :delete-key-code="isEditable ? ['Backspace', 'Delete'] : null"
              :zoom-on-scroll="false"
              zoom-activation-key-code="Control"
              :zoom-on-double-click="false"
              :min-zoom="0.35"
              :max-zoom="2"
              :default-edge-options="{ type: 'floating', updatable: isEditable }"
              :fit-view-on-init="true"
              @connect="connect"
              @edge-update="updateConnection"
              @node-click="selectNode"
              @edge-click="selectEdge"
              @pane-click="clearSelection"
            >
              <Background pattern-color="var(--canvas-border)" :gap="20" :size="1" />
              <MiniMap v-if="nodes.length > 0" pannable zoomable class="canvas-minimap" />
              <Controls :show-interactive="isEditable" position="bottom-left" />
              <template #node-flowNode="nodeProps">
                <ApprovalFlowCanvasNode v-bind="nodeProps" />
              </template>
              <template #edge-floating="edgeProps">
                <ApprovalFlowFloatingEdge v-bind="edgeProps" />
              </template>
            </VueFlow>
            <div v-if="!nodes.length" class="empty-state">
              <span>⌁</span>
              <strong>{{ t('Approval:Designer:Empty') }}</strong>
              <p>{{ t('Approval:Designer:EmptyHint') }}</p>
            </div>
          </div>
        </section>

        <aside class="inspector-panel">
          <div class="panel-heading compact inspector-heading">
            <div>
            <h3>
              {{ selectedNode || selectedEdge ? inspectorTitle : t('Approval:Designer:Inspector') }}
            </h3>
            <p>{{ selectedSummary }}</p>
            </div>
            <n-button
              v-if="selectedNode || selectedEdge"
              size="small"
              type="error"
              secondary
              :disabled="!isEditable"
              @click="removeSelected"
            >
              {{ t('Common:Delete') }}
            </n-button>
          </div>
          <ApprovalFlowNodeInspector
            v-if="selectedNode"
            v-model:node="nodeDraft"
            :fields="conditionFields"
          />
          <ApprovalFlowEdgeInspector
            v-else-if="selectedEdge"
            v-model:draft="edgeDraft"
            :from-name="nodeName(selectedEdge.source)"
            :to-name="nodeName(selectedEdge.target)"
            :fields="conditionFields"
            :locked="selectedEdgeParallel"
          />
          <div v-else class="inspector-empty">
            <span>↖</span>
            <strong>{{ t('Approval:Designer:InspectorEmptyTitle') }}</strong>
            <p>{{ t('Approval:Designer:InspectorEmpty') }}</p>
          </div>
        </aside>
      </main>

      <section class="issues-panel" :class="{ 'has-issues': issues.length || serverProblems.length }">
        <div class="issues-heading">
          <div class="issues-title">
          <span class="issues-indicator">{{ issues.length || serverProblems.length ? '!' : '✓' }}</span>
          <div>
          <h3>{{ t('Approval:Designer:Issues') }}</h3>
          <p>{{ issues.length ? t('Approval:Designer:IssueCount', { count: issues.length }) : t('Approval:Designer:IssuesNone') }}</p>
          </div>
          </div>
          <n-tag v-if="issues.length" size="small" type="warning" :bordered="false">
            {{ t('Approval:Designer:IssueCount', { count: issues.length }) }}
          </n-tag>
        </div>
        <ul v-if="issues.length" class="issue-list">
          <li v-for="(issue, index) in issues" :key="`${issue.key}-${index}`">
            <button
              type="button"
              :disabled="!issue.nodeKeys?.length"
              @click="openNode(issue.nodeKeys?.[0] ?? '')"
            >
              {{ t(issue.key, issue.params) }}
            </button>
          </li>
        </ul>
        <template v-if="serverProblems.length">
          <h4 class="section-subtitle">{{ t('Approval:Designer:ServerIssues') }}</h4>
          <ul class="issue-list is-server">
            <li v-for="(problem, index) in serverProblems" :key="index">{{ problem }}</li>
          </ul>
        </template>
      </section>
    </n-spin>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  VueFlow,
  ConnectionMode,
  Position,
  type Connection,
  type DefaultEdge,
  type EdgeMouseEvent,
  type EdgeUpdateEvent,
  type Node,
  type NodeMouseEvent,
  type XYPosition,
} from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { messageKey, t } from '@/i18n'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import { approvalDefinitionApi, type ApprovalFlowDefinitionDto } from '../../api'
import { isFieldConditionComparable, nodeTypeLabelKey } from '../../api/approvalModels'
import ApprovalFlowCanvasNode from '../../components/ApprovalFlowCanvasNode.vue'
import ApprovalFlowEdgeInspector from '../../components/ApprovalFlowEdgeInspector.vue'
import ApprovalFlowFloatingEdge from '../../components/ApprovalFlowFloatingEdge.vue'
import ApprovalFlowNodeInspector from '../../components/ApprovalFlowNodeInspector.vue'
import ApprovalStatusTag from '../../components/ApprovalStatusTag.vue'
import ApprovalUserSelect from '../../components/ApprovalUserSelect.vue'
import { useBusinessTypes } from '../../composables/useApprovalCatalogs'
import {
  buildFlowCanvas,
  canConnectNodes,
  conditionFieldKeys,
  createFlowNodeDraft,
  flowEdgeId,
  flowEdgesToSaveDtos,
  flowNodesToSaveDtos,
  formatFlowLayout,
  maxOutgoingEdges,
  nextNodeKey,
  nextNodePosition,
  validateFlowCanvas,
  type FlowEdgeDraft,
  type FlowIssue,
  type FlowNodePosition,
} from '../../utils/flowGraph'
import { createConditionDraft, type ApprovalFlowNodeDraft } from '../../utils'

interface EdgeData {
  draft: FlowEdgeDraft
}

type FlowCanvasNode = Node<ApprovalFlowNodeDraft>
type FlowCanvasEdge = DefaultEdge<EdgeData>

/** 属性面板只读取节点/连线的最小结构，避免 Vue Flow 深层泛型拖慢类型推导。 */
interface SelectedNode {
  id: string
  data: ApprovalFlowNodeDraft
  position: { x: number; y: number }
}

interface SelectedEdge {
  id: string
  source: string
  target: string
  label?: string
  sourceHandle?: string | null
  targetHandle?: string | null
  data?: EdgeData
}

/** 面板节点类型顺序：常用审批/抄送在前，边界节点在后。 */
const PALETTE_NODE_TYPES = [
  'Approval',
  'Cc',
  'ExclusiveGateway',
  'ParallelGateway',
  'Start',
  'End',
] as const

const DRAG_MIME = 'application/x-approval-node-type'

const route = useRoute()
const router = useRouter()

const definitionId = computed(() => (route.params.definitionId as string | undefined) || '')

const loading = ref(false)
const saving = ref(false)
const validating = ref(false)
const definition = ref<ApprovalFlowDefinitionDto | null>(null)

const form = reactive({
  name: '',
  description: '',
  businessType: null as string | null,
  allowWithdraw: true,
  withdrawWindowHours: null as number | null,
  autoPassWhenInitiator: false,
  fallbackApproverUserIds: [] as string[],
})

const flowId = `approval-flow-${crypto.randomUUID()}`
const flowRef = ref<InstanceType<typeof VueFlow>>()

const nodes = ref<FlowCanvasNode[]>([])
const edges = ref<FlowCanvasEdge[]>([])

const nodeList = () => nodes.value as unknown as SelectedNode[]
const edgeList = () => edges.value as unknown as SelectedEdge[]

const selectedNodeId = ref<string | null>(null)
const selectedEdgeId = ref<string | null>(null)
const serverProblems = ref<string[]>([])
const showSettings = ref(false)

const { items: businessTypeItems, options: businessTypeOptions, ensureLoaded } = useBusinessTypes()

const isEditable = computed(() => !definition.value || definition.value.status === 'Draft')

const pageTitle = computed(() =>
  definition.value?.name
    ? t('Approval:Definition:EditTitle', { name: definition.value.name })
    : t('Approval:Definition:EditTitleNew'),
)

/** 当前业务类型声明的可条件字段，透传给节点与连线的条件编辑器。 */
const conditionFields = computed(() =>
  (
    businessTypeItems.value.find((item) => item.businessType === form.businessType)?.fields ?? []
  ).filter((field) => isFieldConditionComparable(field.type)),
)

function findNode(id: string | null): SelectedNode | null {
  if (!id) return null
  for (const node of nodeList()) {
    if (node.id === id) return node
  }
  return null
}

function findEdge(id: string | null): SelectedEdge | null {
  if (!id) return null
  for (const edge of edgeList()) {
    if (edge.id === id) return edge
  }
  return null
}

const selectedNode = computed(() => findNode(selectedNodeId.value))
const selectedEdge = computed(() => findEdge(selectedEdgeId.value))

/** 属性面板以 v-model 回写选中的节点 / 连线草稿：对象引用不变，只改字段。 */
const nodeDraft = computed<ApprovalFlowNodeDraft>({
  get: () => selectedNode.value?.data as ApprovalFlowNodeDraft,
  set: (value) => {
    if (selectedNode.value) selectedNode.value.data = value
  },
})

const edgeDraft = computed<FlowEdgeDraft>({
  get: () => selectedEdge.value?.data?.draft as FlowEdgeDraft,
  set: (value) => {
    if (selectedEdge.value?.data) selectedEdge.value.data.draft = value
  },
})

/** 并行网关的出线不接受条件，属性面板只读（后端同样拒绝）。 */
const selectedEdgeParallel = computed(() => {
  const edge = selectedEdge.value
  if (!edge) return false
  return findNode(edge.source)?.data?.nodeType === 'ParallelGateway'
})

const inspectorTitle = computed(() =>
  selectedNode.value ? t('Approval:Designer:NodeInspector') : t('Approval:Designer:EdgeInspector'),
)

const selectedSummary = computed(() => {
  if (selectedNode.value?.data) return selectedNode.value.data.name || selectedNode.value.id
  if (selectedEdge.value) {
    return `${nodeName(selectedEdge.value.source)} → ${nodeName(selectedEdge.value.target)}`
  }
  return t('Approval:Designer:InspectorHint')
})

const NODE_GLYPHS: Record<string, string> = {
  Approval: '✓',
  Cc: '@',
  ExclusiveGateway: '◇',
  ParallelGateway: '＋',
  Start: '▶',
  End: '■',
}

const nodeTypeGlyph = (nodeType: string) => NODE_GLYPHS[nodeType] ?? '•'

const drafts = () =>
  nodeList().flatMap((node) => (node.data ? [node.data as ApprovalFlowNodeDraft] : []))

const edgeDrafts = () =>
  edgeList().flatMap((edge) => (edge.data?.draft ? [edge.data.draft] : []))

const positionedDrafts = () =>
  nodeList().flatMap((node) =>
    node.data
      ? [
          {
            ...(node.data as ApprovalFlowNodeDraft),
            position: { x: node.position.x, y: node.position.y } as FlowNodePosition,
          },
        ]
      : [],
  )

/** 画布即时校验：与后端共用同一套规则，问题在保存前即可见。 */
const issues = computed<FlowIssue[]>(() =>
  validateFlowCanvas(
    drafts(),
    edgeDrafts(),
    conditionFields.value.flatMap((field) => (field.key ? [field.key] : [])),
  ),
)

const nodeName = (nodeKey: string) => {
  const node = findNode(nodeKey)
  return node?.data?.name?.trim() || nodeKey
}

/** 画布节点类型查询：连线方向与出线数量校验都依赖节点类型。 */
const nodeTypeOf = (nodeKey: string) => findNode(nodeKey)?.data?.nodeType ?? ''

/** 统计某节点的出线条数，`exceptEdgeId` 用于改接时排除被编辑的连线自身。 */
const outgoingCount = (nodeKey: string, exceptEdgeId?: string) =>
  edgeList().filter((edge) => edge.source === nodeKey && edge.id !== exceptEdgeId).length

/** 取消选中：画布空白处点击后属性面板回到空态。 */
const clearSelection = () => {
  selectedNodeId.value = null
  selectedEdgeId.value = null
}

const openNode = (nodeId: string) => {
  if (!findNode(nodeId)) return
  selectedNodeId.value = nodeId
  selectedEdgeId.value = null
}

const openEdge = (edgeId: string) => {
  if (!findEdge(edgeId)) return
  selectedEdgeId.value = edgeId
  selectedNodeId.value = null
}

const selectNode = ({ node }: NodeMouseEvent) => openNode(node.id)

const selectEdge = ({ edge }: EdgeMouseEvent) => openEdge(edge.id)

/** 新建连线：连接点记录用户实际吸附的边缘方位，载入历史数据时回退为下出上进。 */
const createEdge = (
  from: string,
  to: string,
  draft?: FlowEdgeDraft,
  handles?: { sourceHandle?: string | null; targetHandle?: string | null },
): FlowCanvasEdge => ({
  id: flowEdgeId(from, to),
  source: from,
  target: to,
  sourceHandle: handles?.sourceHandle ?? Position.Bottom,
  targetHandle: handles?.targetHandle ?? Position.Top,
  type: 'floating',
  updatable: isEditable.value,
  data: {
    draft: draft ?? {
      fromNodeKey: from,
      toNodeKey: to,
      label: '',
      condition: createConditionDraft(),
    },
  },
})

/** 连线显示文案：分支名称 + 条件条数，供画布上直接判读分支语义。 */
const edgeLabel = (draft: FlowEdgeDraft): string | undefined => {
  const parts: string[] = []
  const label = draft.label.trim()
  if (label) parts.push(label)
  const rules = conditionFieldKeys(draft.condition).length
  if (rules) parts.push(t('Approval:Designer:EdgeRuleCount', { count: rules }))
  return parts.length ? parts.join(' · ') : undefined
}

/** 连线标签由草稿派生，属性面板改动后同步回 Vue Flow 的 label。 */
watch(
  () => edgeList().map((edge) => edgeLabel(edge.data!.draft)),
  (labels) => {
    edgeList().forEach((edge, index) => {
      edge.label = labels[index]
    })
  },
  { immediate: true },
)

const appendNodeAt = (nodeType: string, position: FlowNodePosition) => {
  if (!isEditable.value) return
  if (nodeType === 'Start' && nodeList().some((node) => node.data?.nodeType === 'Start')) {
    useDiscreteApi.message.warning(t('Approval:Designer:StartExists'))
    return
  }

  const draft = createFlowNodeDraft(nodeType, nextNodeKey(nodeType, drafts()))
  clearServerProblems()
  nodes.value.push({
    id: draft.nodeKey,
    type: 'flowNode',
    position,
    data: draft,
    ariaLabel: draft.name || draft.nodeKey,
  })
}

const appendNode = (nodeType: string) => {
  appendNodeAt(nodeType, nextNodePosition(nodeList().map((node) => ({ position: node.position }))))
}

const fitCanvas = () => {
  void nextTick(() => flowRef.value?.fitView({ padding: 0.22, duration: 280 }))
}

/** 一键恢复清晰的自上而下布局；仅调整视觉坐标，不改变任何流转关系。 */
const arrangeCanvas = () => {
  if (!isEditable.value) return
  const ordered = [...nodeList()].sort((left, right) =>
    left.position.y === right.position.y
      ? left.position.x - right.position.x
      : left.position.y - right.position.y,
  )
  ordered.forEach((node, index) => {
    node.position = { x: 280, y: 56 + index * 150 }
  })
  clearServerProblems()
  fitCanvas()
}

const startDrag = (event: DragEvent, nodeType: string) => {
  if (!isEditable.value || !event.dataTransfer) return
  event.dataTransfer.setData(DRAG_MIME, nodeType)
  event.dataTransfer.effectAllowed = 'copy'
}

const dropOnCanvas = (event: DragEvent) => {
  const nodeType = event.dataTransfer?.getData(DRAG_MIME)
  if (!nodeType) return
  event.preventDefault()
  const position: XYPosition | undefined = flowRef.value?.screenToFlowCoordinate({
    x: event.clientX,
    y: event.clientY,
  })
  appendNodeAt(nodeType, position ?? nextNodePosition(nodeList().map((node) => ({ position: node.position }))))
}

const connect = (connection: Connection) => {
  if (!isEditable.value) return
  const from = connection.source
  const to = connection.target
  if (!from || !to) return

  if (from === to) {
    useDiscreteApi.message.warning(
      t('Approval:Designer:Issue:SelfLoop', { name: nodeName(from) }),
    )
    return
  }

  if (!canConnectNodes(nodeTypeOf(from), nodeTypeOf(to))) {
    useDiscreteApi.message.warning(t('Approval:Designer:ConnectDirection'))
    return
  }

  if (edgeList().some((edge) => edge.source === from && edge.target === to)) return

  if (outgoingCount(from) >= maxOutgoingEdges(nodeTypeOf(from))) {
    useDiscreteApi.message.warning(
      t('Approval:Designer:ConnectTooMany', { name: nodeName(from) }),
    )
    return
  }

  clearServerProblems()
  edges.value.push(createEdge(from, to, undefined, connection))
}

/** 改接连线端点：同步 Vue Flow 的 source/target 与草稿两端标识。 */
const updateConnection = ({ edge, connection }: EdgeUpdateEvent) => {
  if (!isEditable.value) return
  const from = connection.source
  const to = connection.target
  if (!from || !to || from === to) return

  if (!canConnectNodes(nodeTypeOf(from), nodeTypeOf(to))) {
    useDiscreteApi.message.warning(t('Approval:Designer:ConnectDirection'))
    return
  }

  const index = edgeList().findIndex((item) => item.id === edge.id)
  const current = edgeList()[index]
  if (index < 0 || !current?.data) return
  if (
    edgeList().some(
      (item) => item.id !== edge.id && item.source === from && item.target === to,
    )
  ) {
    return
  }

  if (outgoingCount(from, edge.id) >= maxOutgoingEdges(nodeTypeOf(from))) {
    useDiscreteApi.message.warning(
      t('Approval:Designer:ConnectTooMany', { name: nodeName(from) }),
    )
    return
  }

  clearServerProblems()
  edges.value[index] = {
    ...current,
    source: from,
    target: to,
    sourceHandle: connection.sourceHandle ?? current.sourceHandle ?? Position.Bottom,
    targetHandle: connection.targetHandle ?? current.targetHandle ?? Position.Top,
    data: { draft: { ...current.data.draft, fromNodeKey: from, toNodeKey: to } },
  } as unknown as FlowCanvasEdge
}

const removeSelected = () => {
  if (!isEditable.value) return

  const nodeKey = selectedNodeId.value
  const edgeId = selectedEdgeId.value
  if (nodeKey) {
    nodes.value = nodeList().filter((node) => node.id !== nodeKey) as unknown as FlowCanvasNode[]
    edges.value = edgeList().filter(
      (edge) => edge.source !== nodeKey && edge.target !== nodeKey,
    ) as unknown as FlowCanvasEdge[]
  } else if (edgeId) {
    edges.value = edgeList().filter((edge) => edge.id !== edgeId) as unknown as FlowCanvasEdge[]
  }

  clearServerProblems()
  clearSelection()
}

const clearServerProblems = () => {
  serverProblems.value = []
}

const backToList = () => {
  void router.push({ name: 'ApprovalDefinition' })
}

const load = async () => {
  if (!definitionId.value) return

  loading.value = true
  try {
    const detail = await approvalDefinitionApi.get(definitionId.value)
    definition.value = detail
    form.name = detail.name ?? ''
    form.description = detail.description ?? ''
    form.businessType = detail.businessType ?? null
    form.allowWithdraw = detail.allowWithdraw ?? true
    form.withdrawWindowHours = detail.withdrawWindowHours ?? null
    form.autoPassWhenInitiator = Boolean(detail.autoPassWhenInitiator)
    form.fallbackApproverUserIds = detail.fallbackApproverUserIds ?? []

    const canvas = buildFlowCanvas(detail)
    nodes.value = canvas.nodes.map((draft) => ({
      id: draft.nodeKey,
      type: 'flowNode',
      position: canvas.layout[draft.nodeKey] ?? { x: 0, y: 0 },
      data: draft,
      ariaLabel: draft.name || draft.nodeKey,
    }))
    edges.value = canvas.edges.map((draft) =>
      createEdge(draft.fromNodeKey, draft.toNodeKey, draft),
    )

    clearServerProblems()
    clearSelection()
    fitCanvas()
  } catch {
    useDiscreteApi.message.error(t('Approval:Definition:LoadDetailFailed'))
  } finally {
    loading.value = false
  }
}

const persist = () =>
  approvalDefinitionApi.update(definitionId.value, {
    name: form.name.trim(),
    description: form.description.trim() || null,
    businessType: form.businessType,
    allowWithdraw: form.allowWithdraw,
    withdrawWindowHours: form.allowWithdraw ? (form.withdrawWindowHours ?? null) : null,
    autoPassWhenInitiator: form.autoPassWhenInitiator,
    fallbackApproverUserIds: form.fallbackApproverUserIds,
    nodes: flowNodesToSaveDtos(positionedDrafts()),
    edges: flowEdgesToSaveDtos(edgeDrafts()),
    layoutJson: formatFlowLayout(positionedDrafts()),
  })

/** 保存校验：流程名称与画布问题都通过后才落库。 */
const canPersist = () => {
  if (!form.name.trim()) {
    useDiscreteApi.message.warning(t('Approval:Definition:Error:Name'))
    return false
  }

  if (issues.value.length) {
    useDiscreteApi.message.warning(t('Approval:Designer:SaveBlocked'))
    return false
  }

  return true
}

/** 保存并校验：先落库再用服务端规则复核，问题清单以后端判定为准。 */
const validateDraft = async () => {
  if (!definitionId.value || !isEditable.value || !canPersist()) return

  validating.value = true
  clearServerProblems()
  try {
    await persist()
    const result = await approvalDefinitionApi.validate(definitionId.value)
    serverProblems.value = result.problems ?? []
    if (result.valid) {
      useDiscreteApi.message.success(t('Approval:Designer:Validated'))
    } else {
      useDiscreteApi.message.warning(t('Approval:Designer:ValidateFailed'))
    }
  } catch {
    useDiscreteApi.message.error(t('Approval:Designer:ValidateRequestFailed'))
  } finally {
    validating.value = false
  }
}

const save = async (publishAfterSave: boolean) => {
  if (!definitionId.value || !isEditable.value || !canPersist()) return

  saving.value = true
  clearServerProblems()
  try {
    await persist()

    if (!publishAfterSave) {
      useDiscreteApi.message.success(t('Approval:Definition:Saved'))
      await load()
      return
    }

    const result = await approvalDefinitionApi.validate(definitionId.value)
    if (!result.valid) {
      serverProblems.value = result.problems ?? []
      useDiscreteApi.message.warning(t('Approval:Definition:PublishFailed'))
      return
    }

    await approvalDefinitionApi.publish(definitionId.value)
    useDiscreteApi.message.success(t('Approval:Definition:SavedAndPublished'))
    await load()
  } catch {
    useDiscreteApi.message.error(
      t(publishAfterSave ? 'Approval:Definition:PublishFailed' : 'Approval:Definition:SaveFailed'),
    )
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  void ensureLoaded()
  void load()
})
</script>

<style scoped>
.designer-page { min-height: 100%; background: var(--theme-bg); }
.designer-header { position: sticky; z-index: 12; top: 0; display: flex; min-height: 70px; align-items: center; justify-content: space-between; gap: 20px; padding: 10px 22px; border-bottom: var(--po-border); background: var(--po-paper); }
.header-main, .header-actions, .title-row, .switch-row, .canvas-actions, .issues-title { display: flex; align-items: center; }
.header-main { min-width: 0; gap: 10px; }
.back-button { width: 40px; height: 40px; font-size: 20px; }
.header-copy { min-width: 0; }
.title-row { gap: 8px; }
.title-row h2 { overflow: hidden; margin: 0; color: var(--po-ink); font-size: 18px; font-weight: 650; text-overflow: ellipsis; white-space: nowrap; }
.header-copy p { margin: 4px 0 0; color: var(--po-ink-faint); font-size: 12px; }
.header-actions { flex-wrap: wrap; justify-content: flex-end; gap: 8px; }
.designer-spin { display: block; }
.status-alert { margin: 14px 20px 0; }
.settings-panel { margin: 14px 20px 0; padding: 18px 20px; border: 2px solid var(--po-line-mid); border-radius: var(--po-r); background: var(--po-paper); box-shadow: var(--po-shadow-card); }
.panel-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; margin-bottom: 16px; }
.panel-heading.compact { margin-bottom: 12px; }
.panel-heading h3, .issues-heading h3 { margin: 0; color: var(--po-ink); font-size: 14px; font-weight: 650; }
.panel-heading p, .issues-heading p { margin: 4px 0 0; color: var(--po-ink-faint); font-size: 12px; line-height: 1.45; }
.switch-row { flex-wrap: wrap; gap: 22px; margin-top: 4px; }
.designer-workspace { display: grid; min-height: calc(100vh - 190px); grid-template-columns: 224px minmax(420px, 1fr) 340px; margin: 14px 20px 0; overflow: hidden; border: var(--po-border); border-radius: var(--po-r-lg); background: var(--po-paper); box-shadow: var(--po-shadow-card); }
.palette-panel, .inspector-panel { min-width: 0; padding: 18px 14px; background: color-mix(in srgb, var(--po-paper) 96%, var(--theme-bg)); }
.palette-panel { border-right: 1px solid var(--po-line); }
.inspector-panel { overflow: auto; border-left: 1px solid var(--po-line); }
.palette-list { display: grid; gap: 8px; }
.palette-item { display: grid; width: 100%; min-height: 58px; grid-template-columns: 34px minmax(0, 1fr) 20px; align-items: center; gap: 9px; padding: 9px; border: 1px solid transparent; border-radius: 11px; background: transparent; color: var(--po-ink); text-align: left; cursor: grab; transition: background .18s ease, border-color .18s ease, transform .18s ease; }
.palette-item:hover:not(:disabled) { border-color: color-mix(in srgb, var(--po-accent) 28%, var(--po-line)); background: color-mix(in srgb, var(--po-accent) 7%, var(--po-paper)); transform: translateY(-1px); }
.palette-item:disabled { cursor: not-allowed; opacity: .5; }
.palette-icon { display: grid; width: 34px; height: 34px; place-items: center; border-radius: 10px; background: color-mix(in srgb, var(--po-accent) 10%, var(--po-paper)); color: var(--po-accent); font-weight: var(--po-weight-bold); }
.palette-icon.is-Cc { color: var(--theme-info, #2080f0); }
.palette-icon.is-ExclusiveGateway, .palette-icon.is-ParallelGateway { color: var(--theme-warning, #f0a020); }
.palette-icon.is-Start, .palette-icon.is-End { color: var(--theme-success, #18a058); }
.palette-copy { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
.palette-copy strong { font-size: 13px; font-weight: 600; }
.palette-copy small { overflow: hidden; color: var(--po-ink-faint); font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
.palette-add { color: var(--po-ink-faint); font-size: 17px; }
.canvas-panel { display: flex; min-width: 0; flex-direction: column; background: color-mix(in srgb, var(--theme-bg) 72%, var(--po-paper)); }
.canvas-toolbar { display: flex; min-height: 55px; align-items: center; justify-content: space-between; gap: 14px; padding: 9px 14px; border-bottom: 1px solid var(--po-line); background: var(--po-paper); }
.canvas-title { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
.canvas-title strong { color: var(--po-ink); font-size: 13px; }
.canvas-title span { color: var(--po-ink-faint); font-size: 11px; }
.canvas-actions { gap: 7px; }
.stage { position: relative; min-height: 620px; flex: 1; overflow: hidden; }
.stage :deep(.vue-flow__edge.selected .vue-flow__edge-path) { stroke: var(--po-accent); stroke-width: 2.5; }
.stage :deep(.vue-flow__edge-path) { stroke: color-mix(in srgb, var(--po-ink-faint) 58%, transparent); stroke-width: 1.7; }
.stage :deep(.vue-flow__handle) { width: 12px; height: 12px; border: 2px solid var(--po-paper); background: var(--po-accent); opacity: 0; transition: opacity .15s ease; }
.stage :deep(.vue-flow__node:hover .vue-flow__handle.connectable),
.stage :deep(.vue-flow__handle.vue-flow__handle-connecting) { opacity: 1; }
.canvas-minimap { right: 12px; bottom: 12px; overflow: hidden; border: 1px solid var(--po-line); border-radius: 10px; }
.empty-state, .inspector-empty { display: flex; align-items: center; justify-content: center; flex-direction: column; text-align: center; }
.empty-state { position: absolute; z-index: 2; top: 50%; left: 50%; max-width: 280px; pointer-events: none; transform: translate(-50%, -50%); }
.empty-state span, .inspector-empty span { display: grid; width: 48px; height: 48px; margin-bottom: 12px; place-items: center; border: 1px dashed var(--po-line); border-radius: 14px; color: var(--po-ink-faint); font-size: 24px; }
.empty-state strong, .inspector-empty strong { color: var(--po-ink); font-size: 14px; }
.empty-state p, .inspector-empty p { margin: 6px 0 0; color: var(--po-ink-faint); font-size: 12px; line-height: 1.6; }
.inspector-heading { min-height: 45px; border-bottom: 1px solid var(--po-line); }
.inspector-empty { min-height: 340px; padding: 24px; }
.issues-panel { margin: 14px 20px 20px; padding: 14px 16px; border: 1px solid color-mix(in srgb, var(--theme-success, #18a058) 35%, var(--po-line)); border-radius: 14px; background: var(--po-paper); }
.issues-panel.has-issues { border-color: color-mix(in srgb, var(--theme-warning, #f0a020) 55%, var(--po-line)); }
.issues-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.issues-title { gap: 10px; }
.issues-indicator { display: grid; width: 30px; height: 30px; place-items: center; border-radius: 9px; background: color-mix(in srgb, var(--theme-success, #18a058) 12%, transparent); color: var(--theme-success, #18a058); font-weight: var(--po-weight-bold); }
.has-issues .issues-indicator { background: color-mix(in srgb, var(--theme-warning, #f0a020) 14%, transparent); color: var(--theme-warning, #f0a020); }
.section-subtitle { margin: 14px 0 8px; color: var(--po-ink-soft); font-size: 13px; }
.issue-list { margin: 12px 0 0; padding: 12px 12px 6px 34px; border-top: 1px solid var(--po-line); color: var(--po-ink-soft); font-size: 13px; }
.issue-list li { margin-bottom: 7px; }
.issue-list button { padding: 0; border: none; background: none; color: var(--po-accent); cursor: pointer; font-size: 13px; text-align: left; }
.issue-list button:disabled { color: var(--po-ink-soft); cursor: default; }
.issue-list.is-server { color: var(--theme-warning, #f0a020); }
@media (max-width: 1360px) {
  .designer-workspace { grid-template-columns: 206px minmax(400px, 1fr) 300px; }
}
@media (max-width: 1100px) {
  .designer-workspace { grid-template-columns: 190px minmax(0, 1fr); }
  .inspector-panel { grid-column: 1 / -1; border-top: 1px solid var(--po-line); border-left: 0; }
  .inspector-empty { min-height: 180px; }
}
@media (max-width: 760px) {
  .designer-header { position: static; align-items: stretch; flex-direction: column; }
  .header-actions :deep(.n-button) { flex: 1; }
  .designer-workspace { display: flex; min-height: auto; flex-direction: column; margin-inline: 10px; }
  .palette-panel { border-right: 0; border-bottom: 1px solid var(--po-line); }
  .palette-list { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .stage { min-height: 520px; }
  .settings-panel, .issues-panel { margin-inline: 10px; }
}
</style>

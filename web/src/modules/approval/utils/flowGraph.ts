import { messageKey, t } from '@/i18n'
import type {
  ApprovalFlowDefinitionDto,
  ApprovalFlowEdgeDto,
  SaveApprovalFlowEdgeDto,
  SaveApprovalFlowNodeDto,
} from '../api'
import {
  createConditionDraft,
  createNodeDraft,
  formatCondition,
  nodeDraftFromDto,
  nodeDraftToSaveDto,
  parseCondition,
  validateNodeDraft,
  type ApprovalFlowNodeDraft,
  type ConditionDraft,
} from './index'

/** 合成开始/结束节点的标识，与后端 `ApprovalConsts.SynthesizedStartNodeKey` / `SynthesizedEndNodeKey` 一致。 */
export const SYNTHESIZED_START_NODE_KEY = '__start'
export const SYNTHESIZED_END_NODE_KEY = '__end'

/** 画布节点纵横向距（默认布局用，落库到定义级 layout）。 */
const NODE_COLUMN_X = 260
const NODE_ROW_GAP = 132
const NODE_TOP_Y = 40

/** 节点类型分组。 */
export const BOUNDARY_NODE_TYPES = ['Start', 'End'] as const
export const GATEWAY_NODE_TYPES = ['ExclusiveGateway', 'ParallelGateway'] as const

export const isBoundaryNodeType = (nodeType: string) =>
  (BOUNDARY_NODE_TYPES as readonly string[]).includes(nodeType)

export const isGatewayNodeType = (nodeType: string) =>
  (GATEWAY_NODE_TYPES as readonly string[]).includes(nodeType)

/**
 * 节点出边数量上限：`End` 不能引出连线，`Start` / `Cc` 只允许一条；
 * `Approval` 可带条件分支，网关用于 fork / 条件分支，均允许多条出线。
 */
export function maxOutgoingEdges(nodeType: string): number {
  if (nodeType === 'End') return 0
  if (nodeType === 'Approval' || isGatewayNodeType(nodeType)) return Number.POSITIVE_INFINITY
  return 1
}

/** 连线方向：必须从节点出口（source）引出、从入口（target）引入——`End` 无出口，`Start` 无入口。 */
export function canConnectNodes(fromNodeType: string, toNodeType: string): boolean {
  return fromNodeType !== 'End' && toNodeType !== 'Start'
}

/** 画布节点坐标（定义级 layout，引擎不读取）。 */
export interface FlowNodePosition {
  x: number
  y: number
}

/** 定义级布局：节点标识 → 坐标。 */
export type FlowLayout = Record<string, FlowNodePosition>

/** 画布连线草稿（可编辑部分：标签与条件；两端标识由画布连线的 source/target 承载）。 */
export interface FlowEdgeDraft {
  fromNodeKey: string
  toNodeKey: string
  label: string
  condition: ConditionDraft
}

/** 画布载入结果：节点草稿 + 连线草稿（历史线性定义已合成开始/结束与串联连线）。 */
export interface FlowCanvas {
  nodes: ApprovalFlowNodeDraft[]
  edges: FlowEdgeDraft[]
  layout: FlowLayout
}

/** 校验问题：i18n 键 + 参数 + 关联节点标识（用于画布定位）。 */
export interface FlowIssue {
  key: string
  params?: Record<string, string | number>
  nodeKeys?: string[]
}

/** 连线标识由两端节点标识派生，重命名节点不需要额外的连线重写。 */
export const flowEdgeId = (fromNodeKey: string, toNodeKey: string) =>
  `${fromNodeKey}->${toNodeKey}`

/** 解析布局 JSON；结构非法时返回空布局，避免设计器因历史脏数据崩溃。 */
export function parseFlowLayout(layoutJson?: string | null): FlowLayout {
  if (!layoutJson?.trim()) return {}

  try {
    const parsed = JSON.parse(layoutJson) as Record<string, { x?: unknown; y?: unknown } | null>
    const layout: FlowLayout = {}
    for (const [nodeKey, position] of Object.entries(parsed ?? {})) {
      if (!position || typeof position.x !== 'number' || typeof position.y !== 'number') continue
      if (!Number.isFinite(position.x) || !Number.isFinite(position.y)) continue
      layout[nodeKey] = { x: position.x, y: position.y }
    }
    return layout
  } catch {
    return {}
  }
}

/** 序列化布局；无节点时返回 null（引擎不读该字段，仅设计器还原用）。 */
export function formatFlowLayout(
  nodes: readonly { nodeKey: string; position: FlowNodePosition }[],
): string | null {
  if (!nodes.length) return null

  const layout: FlowLayout = {}
  for (const node of nodes) {
    layout[node.nodeKey] = { x: Math.round(node.position.x), y: Math.round(node.position.y) }
  }

  return JSON.stringify(layout)
}

/** 默认纵向布局：按数组顺序自上而下排布，缺失坐标时回填。 */
export function defaultFlowLayout(nodeKeys: readonly string[]): FlowLayout {
  const layout: FlowLayout = {}
  nodeKeys.forEach((nodeKey, index) => {
    layout[nodeKey] = { x: NODE_COLUMN_X, y: NODE_TOP_Y + index * NODE_ROW_GAP }
  })
  return layout
}

/** 新建节点草稿：名称预填节点类型标签，标识由调用方按 `nextNodeKey` 生成。 */
export function createFlowNodeDraft(nodeType: string, nodeKey: string): ApprovalFlowNodeDraft {
  return {
    ...createNodeDraft(1),
    nodeKey,
    name: t(messageKey('approval', 'nodeType', nodeType)),
    nodeType,
  }
}

/** 节点标识前缀：按节点类型命名，便于画布判读与问题定位（如 `approval1` / `exclusiveGateway2`）。 */
export function nodeKeyPrefix(nodeType: string): string {
  const normalized = nodeType.trim()
  if (!normalized) return 'node'
  return normalized.charAt(0).toLowerCase() + normalized.slice(1)
}

/** 生成不与现有节点冲突的类型化标识：`<类型前缀><序号>`，如 `approval1` / `cc1` / `parallelGateway1`。 */
export function nextNodeKey(nodeType: string, nodes: readonly ApprovalFlowNodeDraft[]): string {
  const used = new Set(nodes.map((node) => node.nodeKey))
  const prefix = nodeKeyPrefix(nodeType)
  let suffix = 1
  while (used.has(`${prefix}${suffix}`)) suffix += 1
  return `${prefix}${suffix}`
}

/** 新增节点的落点：排在现有节点下方，避免与已有节点重叠。 */
export function nextNodePosition(
  nodes: readonly { position: FlowNodePosition }[],
): FlowNodePosition {
  const maxY = nodes.reduce(
    (bottom, node) => Math.max(bottom, node.position.y),
    NODE_TOP_Y - NODE_ROW_GAP,
  )
  return { x: NODE_COLUMN_X, y: maxY + NODE_ROW_GAP }
}

/**
 * 新建流程的初始图：开始 → 审批（发起人本人） → 结束。
 * 服务端保存期要求至少一个审批节点，空图无法建草稿，因此新建时即给出可运行的最小图；
 * 发起人本人是唯一无需额外配置即合法的审批人来源。
 */
export function createInitialFlowCanvas(): FlowCanvas {
  const nodes: ApprovalFlowNodeDraft[] = [
    createFlowNodeDraft('Start', SYNTHESIZED_START_NODE_KEY),
  ]
  nodes.push({
    ...createFlowNodeDraft('Approval', nextNodeKey('Approval', nodes)),
    approverType: 'Initiator',
  })
  nodes.push(createFlowNodeDraft('End', SYNTHESIZED_END_NODE_KEY))
  const nodeKeys = nodes.map((node) => node.nodeKey)
  return { nodes, edges: linearEdges(nodeKeys), layout: defaultFlowLayout(nodeKeys) }
}

/** 画布节点 → 带坐标的草稿；保存入参与 layoutJson 都由它派生，缺失坐标回填默认位置。 */
export function positionedFlowNodes(
  canvas: FlowCanvas,
): (ApprovalFlowNodeDraft & { position: FlowNodePosition })[] {
  return canvas.nodes.map((node) => ({
    ...node,
    position: canvas.layout[node.nodeKey] ?? { x: NODE_COLUMN_X, y: NODE_TOP_Y },
  }))
}

function edgeDraftFromDto(dto: ApprovalFlowEdgeDto): FlowEdgeDraft {
  return {
    fromNodeKey: dto.fromNodeKey ?? '',
    toNodeKey: dto.toNodeKey ?? '',
    label: dto.label ?? '',
    condition: parseCondition(dto.conditionJson),
  }
}

/**
 * 定义 → 画布。第一阶段与上线的线性定义（只有节点没有连线）在此合成
 * `Start → n1 → … → nN → End`，使历史草稿仍可打开与编辑（保存时落为真实连线）。
 */
export function buildFlowCanvas(definition: ApprovalFlowDefinitionDto | null | undefined): FlowCanvas {
  const nodes = (definition?.nodes ?? []).map(nodeDraftFromDto)
  if (!nodes.length) {
    return { nodes: [], edges: [], layout: {} }
  }

  const storedLayout = parseFlowLayout(definition?.layoutJson)
  const storedEdges = (definition?.edges ?? [])
    .filter((edge) => edge.fromNodeKey && edge.toNodeKey)
    .map(edgeDraftFromDto)

  const needsSynthesis =
    !definition?.edges?.length && !nodes.some((node) => isBoundaryNodeType(node.nodeType))

  const graphNodes = needsSynthesis ? withSynthesizedBoundaries(nodes) : nodes
  const graphEdges = needsSynthesis
    ? linearEdges(graphNodes.map((node) => node.nodeKey))
    : storedEdges
  const layout = mergeLayout(graphNodes.map((node) => node.nodeKey), storedLayout)

  return { nodes: graphNodes, edges: graphEdges, layout }
}

/** 在首个节点前插入开始节点、末尾追加结束节点。 */
function withSynthesizedBoundaries(nodes: ApprovalFlowNodeDraft[]): ApprovalFlowNodeDraft[] {
  return [
    createFlowNodeDraft('Start', SYNTHESIZED_START_NODE_KEY),
    ...nodes,
    createFlowNodeDraft('End', SYNTHESIZED_END_NODE_KEY),
  ]
}

/** 按数组顺序串联相邻节点（线性语义）。 */
function linearEdges(nodeKeys: readonly string[]): FlowEdgeDraft[] {
  const edges: FlowEdgeDraft[] = []
  for (let index = 0; index < nodeKeys.length - 1; index += 1) {
    edges.push({
      fromNodeKey: nodeKeys[index] as string,
      toNodeKey: nodeKeys[index + 1] as string,
      label: '',
      condition: createConditionDraft(),
    })
  }
  return edges
}

/** 存储布局优先，缺失坐标按数组顺序回填默认位置。 */
function mergeLayout(nodeKeys: readonly string[], stored: FlowLayout): FlowLayout {
  const fallback = defaultFlowLayout(nodeKeys)
  const layout: FlowLayout = {}
  for (const nodeKey of nodeKeys) {
    layout[nodeKey] = stored[nodeKey] ?? fallback[nodeKey] ?? { x: NODE_COLUMN_X, y: NODE_TOP_Y }
  }
  return layout
}

/** 画布节点 → 保存入参；节点顺序按画布自上而下、自左向右落定 SortOrder 与实例快照顺序。 */
export function flowNodesToSaveDtos(
  nodes: readonly (ApprovalFlowNodeDraft & { position: FlowNodePosition })[],
): SaveApprovalFlowNodeDto[] {
  return [...nodes]
    .sort((left, right) =>
      left.position.y === right.position.y
        ? left.position.x - right.position.x
        : left.position.y - right.position.y,
    )
    .map((node) => nodeDraftToSaveDto(node))
}

/** 画布连线 → 保存入参；`sortOrder` 由服务端按起点自动编排。 */
export function flowEdgesToSaveDtos(edges: readonly FlowEdgeDraft[]): SaveApprovalFlowEdgeDto[] {
  return edges.map((edge) => ({
    fromNodeKey: edge.fromNodeKey,
    toNodeKey: edge.toNodeKey,
    conditionJson: formatCondition(edge.condition),
    label: edge.label.trim() || null,
  }))
}

/** 条件引用的业务字段（忽略空字段）。 */
export function conditionFieldKeys(condition: ConditionDraft): string[] {
  return condition.rules.map((rule) => rule.field.trim()).filter(Boolean)
}

/**
 * 画布校验：与后端共用同一套规则。结构规则对应 `ApprovalFlowGraphValidator`（发布期），
 * 节点配置与连线完整性对应 `FlowDefinitionAppService`（保存期），使问题在画布上即时可见。
 */
export function validateFlowCanvas(
  nodes: readonly ApprovalFlowNodeDraft[],
  edges: readonly FlowEdgeDraft[],
  declaredFields: readonly string[] = [],
): FlowIssue[] {
  const issues: FlowIssue[] = []

  const keyCounts = new Map<string, number>()
  for (const node of nodes) {
    const nodeKey = node.nodeKey.trim()
    if (!nodeKey) {
      issues.push({ key: 'Approval:Definition:NodeError:NodeKey', nodeKeys: [node.nodeKey] })
      continue
    }
    keyCounts.set(nodeKey, (keyCounts.get(nodeKey) ?? 0) + 1)
  }
  for (const [nodeKey, count] of keyCounts) {
    if (count > 1) {
      issues.push({ key: 'Approval:Definition:Error:DuplicatedNodeKey', params: { key: nodeKey } })
    }
  }

  const nameOf = (nodeKey: string) =>
    nodes.find((node) => node.nodeKey === nodeKey)?.name.trim() || nodeKey

  const seenEdges = new Set<string>()
  for (const edge of edges) {
    const known = keyCounts.has(edge.fromNodeKey) && keyCounts.has(edge.toNodeKey)
    if (!known) {
      issues.push({
        key: 'Approval:Designer:Issue:DanglingEdge',
        params: { from: edge.fromNodeKey, to: edge.toNodeKey },
      })
      continue
    }

    if (edge.fromNodeKey === edge.toNodeKey) {
      issues.push({
        key: 'Approval:Designer:Issue:SelfLoop',
        params: { name: nameOf(edge.fromNodeKey) },
        nodeKeys: [edge.fromNodeKey],
      })
      continue
    }

    const pair = flowEdgeId(edge.fromNodeKey, edge.toNodeKey)
    if (seenEdges.has(pair)) {
      issues.push({
        key: 'Approval:Designer:Issue:DuplicatedEdge',
        params: { from: edge.fromNodeKey, to: edge.toNodeKey },
      })
    }
    seenEdges.add(pair)
  }

  for (const node of nodes) {
    const errorKey = validateNodeDraft(node)
    if (errorKey) {
      issues.push({ key: errorKey, nodeKeys: [node.nodeKey] })
    }
  }

  issues.push(...validateDeclaredConditionFields(nodes, edges, declaredFields))
  issues.push(...validateGraphStructure(nodes, edges))

  return issues
}

/** 规则 6：条件字段必须来自业务类型声明的字段（未声明字段清单时放行，与后端一致）。 */
function validateDeclaredConditionFields(
  nodes: readonly ApprovalFlowNodeDraft[],
  edges: readonly FlowEdgeDraft[],
  declaredFields: readonly string[],
): FlowIssue[] {
  if (!declaredFields.length) return []

  const declared = new Set(declaredFields)
  const issues: FlowIssue[] = []
  const collect = (condition: ConditionDraft, nodeKeys?: string[]) => {
    for (const field of conditionFieldKeys(condition)) {
      if (declared.has(field)) continue
      issues.push({
        key: 'Approval:Designer:Issue:ConditionField',
        params: { field },
        nodeKeys,
      })
    }
  }

  for (const node of nodes) collect(node.condition, [node.nodeKey])
  for (const edge of edges) {
    collect(edge.condition, [edge.fromNodeKey, edge.toNodeKey])
  }

  return issues
}

/** 规则 1/2/3/7/8/9：边界不合法时只报边界问题，避免可达性与环检测产生重复噪声。 */
function validateGraphStructure(
  nodes: readonly ApprovalFlowNodeDraft[],
  edges: readonly FlowEdgeDraft[],
): FlowIssue[] {
  const boundaryIssues: FlowIssue[] = []
  const startNodes = nodes.filter((node) => node.nodeType === 'Start')
  if (startNodes.length !== 1) {
    boundaryIssues.push({
      key: 'Approval:Designer:Issue:StartCount',
      params: { count: startNodes.length },
    })
  }

  if (!nodes.some((node) => node.nodeType === 'End')) {
    boundaryIssues.push({ key: 'Approval:Designer:Issue:NoEnd' })
  }

  if (boundaryIssues.length) return boundaryIssues

  const issues: FlowIssue[] = []
  const outgoing = new Map<string, FlowEdgeDraft[]>()
  for (const edge of edges) {
    const list = outgoing.get(edge.fromNodeKey)
    if (list) list.push(edge)
    else outgoing.set(edge.fromNodeKey, [edge])
  }
  const outgoingOf = (nodeKey: string) => outgoing.get(nodeKey) ?? []

  const start = startNodes[0] as ApprovalFlowNodeDraft
  const reachable = new Set<string>([start.nodeKey])
  const queue: string[] = [start.nodeKey]
  while (queue.length) {
    const current = queue.shift() as string
    for (const edge of outgoingOf(current)) {
      if (reachable.has(edge.toNodeKey)) continue
      reachable.add(edge.toNodeKey)
      queue.push(edge.toNodeKey)
    }
  }

  const unreachable = nodes.filter((node) => !reachable.has(node.nodeKey))
  if (unreachable.length) {
    issues.push({
      key: 'Approval:Designer:Issue:Unreachable',
      params: { names: unreachable.map((node) => node.name.trim() || node.nodeKey).join('、') },
      nodeKeys: unreachable.map((node) => node.nodeKey),
    })
  }

  for (const node of nodes) {
    const count = outgoingOf(node.nodeKey).length
    const name = node.name.trim() || node.nodeKey

    if (node.nodeType === 'Start') {
      if (count !== 1) {
        issues.push({
          key: 'Approval:Designer:Issue:StartOutgoing',
          params: { name, count },
          nodeKeys: [node.nodeKey],
        })
      }
      continue
    }

    if (count === 0) {
      if (node.nodeType !== 'End') {
        issues.push({
          key: 'Approval:Designer:Issue:NoOutgoing',
          params: { name },
          nodeKeys: [node.nodeKey],
        })
      }
      continue
    }

    if (count > maxOutgoingEdges(node.nodeType)) {
      issues.push({
        key:
          node.nodeType === 'End'
            ? 'Approval:Designer:Issue:EndOutgoing'
            : 'Approval:Designer:Issue:MultipleOutgoing',
        params: { name, count },
        nodeKeys: [node.nodeKey],
      })
    }
  }

  for (const node of nodes) {
    const list = outgoingOf(node.nodeKey)
    if (list.length <= 1) continue

    if (node.nodeType === 'ParallelGateway' && list.some((edge) => conditionFieldKeys(edge.condition).length)) {
      issues.push({
        key: 'Approval:Designer:Issue:ParallelCondition',
        params: { name: node.name.trim() || node.nodeKey },
        nodeKeys: [node.nodeKey],
      })
    }

    if (node.nodeType === 'ExclusiveGateway') {
      const defaults = list.filter((edge) => conditionFieldKeys(edge.condition).length === 0).length
      if (defaults > 1) {
        issues.push({
          key: 'Approval:Designer:Issue:ExclusiveDefault',
          params: { name: node.name.trim() || node.nodeKey, count: defaults },
          nodeKeys: [node.nodeKey],
        })
      }
    }
  }

  const cycle = findCycleWithoutGateway(nodes, edges)
  if (cycle) {
    issues.push({
      key: 'Approval:Designer:Issue:Cycle',
      params: { name: cycle.name.trim() || cycle.nodeKey },
      nodeKeys: [cycle.nodeKey],
    })
  }

  if (!nodes.some((node) => node.nodeType === 'Approval')) {
    issues.push({ key: 'Approval:Definition:Error:NoApprovalNode' })
  }

  return issues
}

/** 去掉网关后仍有环，说明该环不经过任何网关（与后端 `ValidateCycles` 一致）。 */
function findCycleWithoutGateway(
  nodes: readonly ApprovalFlowNodeDraft[],
  edges: readonly FlowEdgeDraft[],
): ApprovalFlowNodeDraft | null {
  const gatewayKeys = new Set(
    nodes.filter((node) => isGatewayNodeType(node.nodeType)).map((node) => node.nodeKey),
  )
  const outgoing = new Map<string, string[]>()
  for (const edge of edges) {
    if (gatewayKeys.has(edge.toNodeKey)) continue
    const list = outgoing.get(edge.fromNodeKey)
    if (list) list.push(edge.toNodeKey)
    else outgoing.set(edge.fromNodeKey, [edge.toNodeKey])
  }

  const state = new Map<string, 1 | 2>()
  const visit = (nodeKey: string): boolean => {
    const current = state.get(nodeKey)
    if (current === 1) return true
    if (current === 2) return false

    state.set(nodeKey, 1)
    for (const target of outgoing.get(nodeKey) ?? []) {
      if (visit(target)) return true
    }
    state.set(nodeKey, 2)
    return false
  }

  for (const node of nodes) {
    if (gatewayKeys.has(node.nodeKey) || state.has(node.nodeKey)) continue
    if (visit(node.nodeKey)) return node
  }

  return null
}

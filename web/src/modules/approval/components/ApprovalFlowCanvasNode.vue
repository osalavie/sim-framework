<template>
  <div class="flow-node" :class="[`is-${data.nodeType}`, { 'is-selected': selected }]">
    <Handle
      v-for="position in HANDLE_POSITIONS"
      :id="position"
      :key="position"
      type="source"
      :position="position"
      :connectable-start="hasSource"
      :connectable-end="hasTarget"
    />
    <span class="node-accent" />
    <div class="node-header">
      <span class="node-icon">{{ glyph }}</span>
      <span class="node-kind">{{ t(nodeTypeLabelKey(data.nodeType)) }}</span>
      <span class="node-key">{{ data.nodeKey }}</span>
    </div>
    <strong class="node-name">{{ data.name || data.nodeKey }}</strong>
    <span v-if="summary" class="node-summary">{{ summary }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { Handle, Position } from '@vue-flow/core'
import { t } from '@/i18n'
import {
  approverTypeLabelKey,
  joinPolicyLabelKey,
  multiApproveModeLabelKey,
  nodeTypeLabelKey,
} from '../api/approvalModels'
import { isGatewayNodeType } from '../utils/flowGraph'
import type { ApprovalFlowNodeDraft } from '../utils'

/** 四边各一个连接点：连线接近任意边缘时吸附到该边，配合 Loose 连接模式互为起终点。 */
const HANDLE_POSITIONS = [Position.Top, Position.Right, Position.Bottom, Position.Left]

const props = defineProps<{
  data: ApprovalFlowNodeDraft
  selected?: boolean
}>()

const hasTarget = computed(() => props.data.nodeType !== 'Start')
const hasSource = computed(() => props.data.nodeType !== 'End')
const glyph = computed(() => ({
  Approval: '✓',
  Cc: '@',
  ExclusiveGateway: '◇',
  ParallelGateway: '＋',
  Start: '▶',
  End: '■',
}[props.data.nodeType] ?? '•'))

/** 节点摘要：审批人来源 / 抄送对象 / 网关汇聚策略。 */
const summary = computed(() => {
  const node = props.data

  if (isGatewayNodeType(node.nodeType)) {
    return t(joinPolicyLabelKey(node.joinPolicy))
  }

  if (node.nodeType === 'Approval') {
    const approver = t(approverTypeLabelKey(node.approverType))
    const mode = t(multiApproveModeLabelKey(node.multiApproveMode))
    return `${approver} · ${mode}`
  }

  if (node.nodeType === 'Cc') {
    const { userIds, roles, initiator } = node.ccConfig
    const parts: string[] = []
    if (userIds.filter(Boolean).length) {
      parts.push(t('Approval:Designer:CcUserCount', { count: userIds.filter(Boolean).length }))
    }
    if (roles.filter(Boolean).length) {
      parts.push(t('Approval:Designer:CcRoleCount', { count: roles.filter(Boolean).length }))
    }
    if (initiator) parts.push(t('Approval:Field:CcInitiator'))
    return parts.length ? parts.join(' · ') : t('Approval:Designer:CcEmpty')
  }

  return ''
})
</script>

<style scoped>
.flow-node {
  position: relative;
  display: flex;
  width: 220px;
  flex-direction: column;
  gap: 5px;
  padding: 13px 14px 14px;
  border: 2px solid var(--po-line-mid);
  border-radius: 14px;
  background: var(--po-paper);
  box-shadow: var(--po-shadow-card);
  text-align: left;
  transition: border-color .18s ease, box-shadow .18s ease, transform .18s ease;
}

.flow-node:hover {
  border-color: color-mix(in srgb, var(--po-accent) 35%, var(--po-line));
  box-shadow: var(--po-shadow-hover);
}

.flow-node.is-selected {
  border-color: var(--po-accent);
  box-shadow:
    0 0 0 3px color-mix(in srgb, var(--po-accent) 22%, transparent), var(--po-shadow-pop);
}

.flow-node.is-Start,
.flow-node.is-End {
  width: 156px;
  align-items: center;
  border-radius: 999px;
  background: var(--po-accent-soft);
  text-align: center;
}

.flow-node.is-Start .node-key,
.flow-node.is-End .node-key,
.flow-node.is-Start .node-summary,
.flow-node.is-End .node-summary { display: none; }

.flow-node.is-Cc {
  border-style: dashed;
}

.flow-node.is-ExclusiveGateway,
.flow-node.is-ParallelGateway {
  border-color: color-mix(in srgb, var(--po-warn, #f0a020) 55%, var(--po-line));
  background: color-mix(in srgb, var(--po-warn, #f0a020) 10%, var(--po-paper));
}

.node-accent { position: absolute; top: 12px; bottom: 12px; left: 0; width: 3px; border-radius: 0 3px 3px 0; background: var(--po-accent); }
.is-Cc .node-accent { background: var(--po-accent, #2080f0); }
.is-ExclusiveGateway .node-accent, .is-ParallelGateway .node-accent { background: var(--po-warn, #f0a020); }
.is-Start .node-accent, .is-End .node-accent { display: none; }
.node-header { display: flex; min-width: 0; align-items: center; gap: 6px; }
.node-icon { display: grid; width: 22px; height: 22px; place-items: center; border-radius: 7px; background: color-mix(in srgb, var(--po-accent) 10%, transparent); color: var(--po-accent); font-size: 12px; font-weight: var(--po-weight-bold); }
.node-key { overflow: hidden; margin-left: auto; color: var(--po-ink-faint); font-family: var(--po-font-mono); font-size: 10px; text-overflow: ellipsis; white-space: nowrap; }

.node-kind {
  color: var(--po-ink-faint);
  font-size: 10px;
  letter-spacing: 0.04em;
}

.node-name {
  overflow: hidden;
  color: var(--po-ink);
  font-size: 14px;
  line-height: 1.45;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.node-summary {
  overflow: hidden;
  color: var(--po-ink-soft);
  font-size: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>

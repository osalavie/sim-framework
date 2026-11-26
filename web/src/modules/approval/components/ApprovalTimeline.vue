<template>
  <div class="po-grid po-grid--2 approval-timeline">
    <section class="po-card">
      <header class="po-card__head">
        <div>
          <h2>{{ t('Approval:Detail:NodeProgress') }}</h2>
        </div>
      </header>
      <n-empty v-if="!nodes?.length" :description="t('Approval:Detail:NodesEmpty')" />
      <div v-else class="po-timeline">
        <article
          v-for="node in nodes"
          :key="node.nodeKey ?? node.name ?? ''"
          class="po-timeline__item node-item"
          :class="{
            'is-accent': node.status === 'Active' || node.status === 'Rejected',
            'is-done': node.status === 'Approved',
          }"
        >
          <div class="node-head">
            <strong class="node-name">{{ node.name ?? '' }}</strong>
            <span class="po-pill">{{ t(nodeTypeLabelKey(node.nodeType)) }}</span>
            <ApprovalStatusTag kind="node" :status="node.status" />
            <span v-if="node.activated === false" class="node-hint">
              {{ t('Approval:Detail:NotActivated') }}
            </span>
          </div>
          <ul v-if="node.tasks?.length" class="po-list task-list">
            <li v-for="task in node.tasks" :key="task.id" class="po-list__item task-item">
              <span class="po-avatar po-avatar--sky task-avatar" aria-hidden="true">
                {{ initial(label(task.assigneeUserId)) }}
              </span>
              <div class="po-list__main">
                <div class="task-head">
                  <strong class="assignee">{{ label(task.assigneeUserId) }}</strong>
                  <span v-if="taskTypeLabelKey(task.taskType)" class="po-pill po-pill--lilac">
                    {{ t(taskTypeLabelKey(task.taskType)) }}
                  </span>
                  <ApprovalStatusTag kind="task" :status="task.status" />
                  <span class="task-time">
                    {{ formatDate(task.handledTime || task.creationTime || '') }}
                  </span>
                </div>
                <p v-if="task.comment" class="comment">{{ task.comment }}</p>
              </div>
            </li>
          </ul>
        </article>
      </div>
    </section>

    <section class="po-card">
      <header class="po-card__head">
        <div>
          <h2>{{ t('Approval:Detail:Records') }}</h2>
        </div>
      </header>
      <n-empty v-if="!records?.length" :description="t('Approval:Detail:RecordsEmpty')" />
      <div v-else class="po-timeline">
        <article
          v-for="record in records"
          :key="record.id"
          class="po-timeline__item record-item"
          :class="{ 'is-accent': record.action === 'Reject' || record.action === 'Terminate' }"
        >
          <div class="record-head">
            <strong>{{ t(actionLabelKey(record.action)) }}</strong>
            <span v-if="record.nodeName" class="po-pill">{{ record.nodeName }}</span>
            <span class="record-time">{{ formatDate(record.creationTime || '') }}</span>
          </div>
          <div class="record-body">
            <span v-if="record.operatorUserId">
              {{ t('Approval:Detail:Operator') }}：{{ label(record.operatorUserId) }}
            </span>
            <span v-if="record.targetUserId">
              {{ t('Approval:Detail:TargetUser') }}：{{ label(record.targetUserId) }}
            </span>
          </div>
          <p v-if="record.comment" class="comment">{{ record.comment }}</p>
        </article>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { t } from '@/i18n'
import { formatDate } from '@/utils/date'
import type { ApprovalNodeProgressDto, ApprovalRecordDto } from '../api'
import {
  actionLabelKey,
  nodeTypeLabelKey,
  taskTypeLabelKey,
} from '../api/approvalModels'
import { useUserDirectory } from '../composables/useUserDirectory'
import ApprovalStatusTag from './ApprovalStatusTag.vue'

withDefaults(
  defineProps<{
    nodes?: ApprovalNodeProgressDto[] | null
    records?: ApprovalRecordDto[] | null
  }>(),
  { nodes: null, records: null },
)

const { label, ensureLoaded } = useUserDirectory()
void ensureLoaded()

const initial = (value: string) => value.trim().charAt(0) || '·'
</script>

<style scoped>
.task-list {
  margin-top: 10px;
}

.task-item {
  align-items: flex-start;
}

.task-avatar {
  flex: none;
  font-size: 12px;
  font-weight: var(--po-weight-strong);
}

.node-head,
.task-head,
.record-head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 7px;
}

.node-name {
  color: var(--po-ink);
  font-size: 14px;
}

.node-hint {
  color: var(--po-ink-faint);
  font-size: 12px;
}

.assignee {
  color: var(--po-ink);
  font-size: 13px;
}

.task-time,
.record-time {
  margin-left: auto;
  color: var(--po-ink-faint);
  font-size: 11.5px;
}

.comment {
  margin: 7px 0 0;
  color: var(--po-ink-soft);
  font-size: 13px;
  overflow-wrap: anywhere;
}

.record-body {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  margin-top: 6px;
  color: var(--po-ink-soft);
  font-size: 13px;
}

@media (max-width: 640px) {
  .task-time,
  .record-time {
    margin-left: 0;
  }
}
</style>

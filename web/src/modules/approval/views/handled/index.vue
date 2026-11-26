<template>
  <div class="po-page po-page--fill">
    <section class="po-toolbar">
      <n-input
        v-model:value="filter.keyword"
        class="filter-field"
        clearable
        :placeholder="t('Approval:Filter:Keyword')"
        @keyup.enter="search"
      />
      <n-input
        v-model:value="filter.definitionCode"
        class="filter-field"
        clearable
        :placeholder="t('Approval:Filter:DefinitionCode')"
        @keyup.enter="search"
      />
      <div class="po-toolbar__spacer" />
      <div class="filter-actions">
        <n-button type="primary" :loading="loading" @click="search">{{ t('Common:Search') }}</n-button>
        <n-button secondary @click="reset">{{ t('Approval:Filter:Reset') }}</n-button>
        <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
      </div>
    </section>

    <DataTable
      class="task-table"
      remote
      framed
      fill
      flex-height
      auto-view-mode
      card-pagination
      :columns="columns"
      :data="items"
      :loading="loading"
      :row-key="rowKey"
      :pagination="pagination"
      @update:page="handlePageChange"
      @update:page-size="handlePageSizeChange"
    >
      <template #card>
        <n-spin :show="loading">
          <div class="po-stack">
            <article v-for="task in items" :key="task.id" class="po-card">
              <div class="card-meta">
                <span class="po-pill">{{ task.instanceNumber }}</span>
                <ApprovalStatusTag kind="task" :status="task.status" />
                <ApprovalStatusTag kind="instance" :status="task.instanceStatus" />
              </div>
              <h3>{{ task.instanceTitle }}</h3>
              <p class="card-line">
                {{ businessTypeLabel(task.businessType) }} · {{ task.nodeName }} ·
                {{ formatDate(task.handledTime || task.creationTime || '') }}
              </p>
              <p v-if="task.comment" class="card-line">{{ t('Approval:Field:Comment') }}：{{ task.comment }}</p>
              <div class="card-actions">
                <n-button size="small" secondary @click="openDetail(task)">{{ t('Common:View') }}</n-button>
              </div>
            </article>
          </div>
          <n-empty v-if="!items.length && !loading" :description="t('Approval:Handled:Empty')" />
        </n-spin>
      </template>
    </DataTable>
  </div>
</template>

<script setup lang="ts">
import { computed, h, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { NButton, type DataTableColumns } from 'naive-ui'
import { DataTable } from '@/components/Table'
import { t } from '@/i18n'
import { formatDate } from '@/utils/date'
import { approvalTaskApi, type ApprovalTaskDto } from '../../api'
import { useBusinessTypes } from '../../composables/useApprovalCatalogs'
import ApprovalStatusTag from '../../components/ApprovalStatusTag.vue'
import { useApprovalList } from '../../composables/useApprovalList'
import { buildTaskQuery, createTaskFilter } from '../../utils'

const router = useRouter()
const { label: businessTypeLabel, ensureLoaded: ensureBusinessTypes } = useBusinessTypes()

const { items, loading, filter, pagination, load, search, reset, handlePageChange, handlePageSizeChange } =
  useApprovalList<ApprovalTaskDto, ReturnType<typeof createTaskFilter>, ReturnType<typeof buildTaskQuery>>({
    createFilter: createTaskFilter,
    buildQuery: buildTaskQuery,
    fetch: (query) => approvalTaskApi.myHandled(query),
    errorMessage: t('Approval:Handled:LoadFailed'),
  })

const rowKey = (row: ApprovalTaskDto) => row.id ?? ''

const columns = computed<DataTableColumns<ApprovalTaskDto>>(() => [
  {
    title: t('Approval:Field:Title'),
    key: 'instanceTitle',
    minWidth: 240,
    resizable: true,
    render: (row) =>
      h('div', { class: 'title-cell' }, [
        h('strong', row.instanceTitle ?? ''),
        h('code', row.instanceNumber ?? ''),
      ]),
  },
  {
    title: t('Approval:Field:BusinessType'),
    key: 'businessType',
    minWidth: 150,
    resizable: true,
    render: (row) => businessTypeLabel(row.businessType),
  },
  {
    title: t('Approval:Field:Node'),
    key: 'nodeName',
    minWidth: 150,
    resizable: true,
    render: (row) => row.nodeName ?? '',
  },
  {
    title: t('Approval:Handled:MyResult'),
    key: 'status',
    width: 110,
    resizable: true,
    render: (row) => h(ApprovalStatusTag, { kind: 'task', status: row.status }),
  },
  {
    title: t('Approval:Field:HandledAt'),
    key: 'handledTime',
    width: 180,
    resizable: true,
    render: (row) => formatDate(row.handledTime || row.creationTime || ''),
  },
  {
    title: t('Approval:Field:Comment'),
    key: 'comment',
    minWidth: 200,
    resizable: true,
    ellipsis: { tooltip: true },
    render: (row) => row.comment || t('Common:None'),
  },
  {
    title: t('Common:Actions'),
    key: 'actions',
    width: 120,
    fixed: 'right',
    render: (row) =>
      h('div', { class: 'po-cell-actions' }, [
        h(
          NButton,
          { size: 'small', secondary: true, onClick: () => openDetail(row) },
          { default: () => t('Common:View') },
        ),
      ]),
  },
])

const openDetail = (task: ApprovalTaskDto) => {
  if (!task.instanceId) return
  void router.push({
    name: 'ApprovalInstanceDetail',
    params: { instanceId: task.instanceId },
  })
}

onMounted(() => {
  void load()
  void ensureBusinessTypes()
})
</script>

<style scoped>
.filter-field {
  width: 240px;
}

.filter-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.task-table :deep(.title-cell) {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.task-table :deep(.title-cell strong) {
  color: var(--po-ink);
  font-weight: var(--po-weight-bold);
}

.task-table :deep(.title-cell code) {
  color: var(--po-ink-faint);
  font-family: var(--po-font-mono);
  font-size: 11px;
}

.task-table :deep(.node-cell),
.task-table :deep(.due-cell) {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}

.card-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 7px;
}

.card-line { margin: 8px 0 0; color: var(--po-ink-soft); font-size: 13px; overflow-wrap: anywhere; }

.card-detail {
  margin: 12px 0 0;
  grid-template-columns: auto minmax(0, 1fr);
}

.card-detail dt {
  color: var(--po-ink-faint);
  font-size: 12px;
}

.card-detail dd {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin: 0;
  color: var(--po-ink);
  font-size: 13px;
}

.card-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 9px;
  margin-top: 14px;
}

.po-stack h3 {
  margin: 8px 0 0;
  font-size: 16px;
}

@media (max-width: 640px) {
  .filter-field {
    width: 100%;
  }

  .filter-actions {
    display: grid;
    width: 100%;
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .card-actions :deep(.n-button) {
    flex: 1;
  }
}
</style>

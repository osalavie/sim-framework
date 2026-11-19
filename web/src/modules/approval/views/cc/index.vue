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
            <article v-for="record in items" :key="record.id" class="po-card">
              <div class="card-meta">
                <span class="po-pill">{{ record.instanceNumber }}</span>
                <ApprovalStatusTag kind="instance" :status="record.instanceStatus" />
                <span class="po-pill" :class="record.readAt ? 'po-pill--mint' : 'po-pill--sun'">
                  {{ t(record.readAt ? 'Approval:Cc:Read' : 'Approval:Cc:Unread') }}
                </span>
              </div>
              <h3>{{ record.instanceTitle }}</h3>
              <p class="card-line">{{ formatDate(record.creationTime || '') }}</p>
              <div class="card-actions">
                <n-button v-if="!record.readAt" size="small" secondary @click="markRead(record)">
                  {{ t('Approval:Cc:MarkRead') }}
                </n-button>
                <n-button size="small" secondary @click="openDetail(record.instanceId)">
                  {{ t('Common:View') }}
                </n-button>
              </div>
            </article>
          </div>
          <n-empty v-if="!items.length && !loading" :description="t('Approval:Cc:Empty')" />
        </n-spin>
      </template>
    </DataTable>
  </div>
</template>

<script setup lang="ts">
import { computed, h, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { NButton, NTag, type DataTableColumns } from 'naive-ui'
import { DataTable } from '@/components/Table'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { formatDate } from '@/utils/date'
import { approvalTaskApi, type ApprovalCcDto } from '../../api'
import ApprovalStatusTag from '../../components/ApprovalStatusTag.vue'
import { useApprovalList } from '../../composables/useApprovalList'
import { buildTaskQuery, createTaskFilter } from '../../utils'

const router = useRouter()

const { items, loading, filter, pagination, load, search, reset, handlePageChange, handlePageSizeChange } =
  useApprovalList<ApprovalCcDto, ReturnType<typeof createTaskFilter>, ReturnType<typeof buildTaskQuery>>({
    createFilter: createTaskFilter,
    buildQuery: buildTaskQuery,
    fetch: (query) => approvalTaskApi.myCc(query),
    errorMessage: t('Approval:Cc:LoadFailed'),
  })

const rowKey = (row: ApprovalCcDto) => row.id ?? ''

const columns = computed<DataTableColumns<ApprovalCcDto>>(() => [
  {
    title: t('Approval:Field:Number'),
    key: 'instanceNumber',
    width: 180,
    resizable: true,
    render: (row) => h('code', row.instanceNumber ?? ''),
  },
  {
    title: t('Approval:Field:Title'),
    key: 'instanceTitle',
    minWidth: 220,
    resizable: true,
    ellipsis: { tooltip: true },
    render: (row) => row.instanceTitle ?? '',
  },
  {
    title: t('Common:Status'),
    key: 'instanceStatus',
    width: 110,
    resizable: true,
    render: (row) => h(ApprovalStatusTag, { kind: 'instance', status: row.instanceStatus }),
  },
  {
    title: t('Approval:Cc:ReceivedAt'),
    key: 'creationTime',
    width: 180,
    resizable: true,
    render: (row) => formatDate(row.creationTime || ''),
  },
  {
    title: t('Approval:Cc:ReadState'),
    key: 'readAt',
    width: 140,
    resizable: true,
    render: (row) =>
      h(
        NTag,
        { size: 'small', bordered: false, type: row.readAt ? 'default' : 'warning' },
        { default: () => t(row.readAt ? 'Approval:Cc:Read' : 'Approval:Cc:Unread') },
      ),
  },
  {
    title: t('Common:Actions'),
    key: 'actions',
    width: 190,
    fixed: 'right',
    render: (row) =>
      h('div', { class: 'po-cell-actions' }, [
        row.readAt
          ? null
          : h(
              NButton,
              { size: 'small', secondary: true, onClick: () => void markRead(row) },
              { default: () => t('Approval:Cc:MarkRead') },
            ),
        h(
          NButton,
          { size: 'small', secondary: true, onClick: () => openDetail(row.instanceId) },
          { default: () => t('Common:View') },
        ),
      ]),
  },
])

const markRead = async (record: ApprovalCcDto) => {
  if (!record.id) return
  try {
    await approvalTaskApi.markCcRead(record.id)
    items.value = items.value.map((item) =>
      item.id === record.id ? { ...item, readAt: new Date().toISOString() } : item,
    )
    useDiscreteApi.message.success(t('Approval:Cc:MarkReadSucceeded'))
  } catch {
    useDiscreteApi.message.error(t('Approval:Cc:MarkReadFailed'))
  }
}

const openDetail = (instanceId?: string) => {
  if (!instanceId) return
  void router.push({
    name: 'ApprovalInstanceDetail',
    params: { instanceId },
  })
}

onMounted(() => void load())
</script>

<style scoped>
.filter-field {
  width: 240px;
}

.filter-field-narrow {
  width: 175px;
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

.card-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 7px;
}

.card-line {
  margin: 8px 0 0;
  color: var(--po-ink-soft);
  font-size: 13px;
  overflow-wrap: anywhere;
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
  .filter-field,
  .filter-field-narrow {
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

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
      <n-select
        v-model:value="filter.status"
        class="filter-field filter-field-narrow"
        :options="statusOptions"
      />
      <n-select
        v-model:value="filter.businessType"
        class="filter-field filter-field-narrow"
        clearable
        :options="businessTypeOptions"
        :placeholder="t('Approval:Filter:BusinessType')"
      />
      <ApprovalUserSelect
        v-model:value="filter.initiatorUserId"
        class="filter-field"
        :placeholder="t('Approval:Filter:Initiator')"
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
      class="instance-table"
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
            <article v-for="instance in items" :key="instance.id" class="po-card">
              <div class="card-meta">
                <span class="po-pill">{{ instance.number }}</span>
                <ApprovalStatusTag kind="instance" :status="instance.status" />
              </div>
              <h3>{{ instance.title }}</h3>
              <p class="card-line">
                {{ instance.definitionName }} · {{ businessLabel(instance.businessType) }} ·
                {{ userLabel(instance.initiatorUserId) }}
              </p>
              <p class="card-line">
                {{ instance.currentNodeName || t('Common:None') }} ·
                {{ formatDate(instance.creationTime || '') }}
              </p>
              <div class="card-actions">
                <n-button size="small" secondary @click="openDetail(instance.id)">
                  {{ t('Common:View') }}
                </n-button>
                <n-button
                  v-if="canTerminate && (instance.status === 'Running' || instance.status === 'Returned')"
                  size="small"
                  type="error"
                  secondary
                  @click="openTerminate(instance)"
                >
                  {{ t('Approval:Monitor:Terminate') }}
                </n-button>
              </div>
            </article>
          </div>
          <n-empty v-if="!items.length && !loading" :description="t('Approval:Monitor:Empty')" />
        </n-spin>
      </template>
    </DataTable>

    <ApprovalInstanceActionModal
      v-model:show="showTerminate"
      mode="terminate"
      :instance="activeInstance"
      @done="load"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, h, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { NButton, type DataTableColumns } from 'naive-ui'
import { DataTable } from '@/components/Table'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { formatDate } from '@/utils/date'
import { useAuditNames } from '@/components/AuditInfo/useAuditNames'
import { createAuditColumns } from '@/utils/auditColumns'
import { useUserStore } from '@/stores/user'
import {
  approvalMonitorApi,
  type ApprovalInstanceDetailDto,
  type ApprovalInstanceListDto,
} from '../../api'
import { INSTANCE_STATUSES, instanceStatusLabelKey } from '../../api/approvalModels'
import ApprovalStatusTag from '../../components/ApprovalStatusTag.vue'
import ApprovalInstanceActionModal from '../../components/ApprovalInstanceActionModal.vue'
import ApprovalUserSelect from '../../components/ApprovalUserSelect.vue'
import { useApprovalList } from '../../composables/useApprovalList'
import { useBusinessTypes } from '../../composables/useApprovalCatalogs'
import { useUserDirectory } from '../../composables/useUserDirectory'
import {
  APPROVAL_TERMINATE_PERMISSION,
  buildInstanceQuery,
  createInstanceFilter,
  enumOptions,
} from '../../utils'

const router = useRouter()
const userStore = useUserStore()

const canTerminate = computed(() => userStore.hasPermission(APPROVAL_TERMINATE_PERMISSION))

const showTerminate = ref(false)
const activeInstance = ref<ApprovalInstanceDetailDto | null>(null)

const { items, loading, filter, pagination, load, search, reset, handlePageChange, handlePageSizeChange } =
  useApprovalList<
    ApprovalInstanceListDto,
    ReturnType<typeof createInstanceFilter>,
    ReturnType<typeof buildInstanceQuery>
  >({
    createFilter: createInstanceFilter,
    buildQuery: buildInstanceQuery,
    fetch: (query) => approvalMonitorApi.getList(query),
    errorMessage: t('Approval:Monitor:LoadFailed'),
  })

const { options: businessTypeOptions, label: businessLabel, ensureLoaded: ensureBusinessTypes } =
  useBusinessTypes()
const { label: userLabel, ensureLoaded: ensureUsers } = useUserDirectory()
const { resolveName, resolveAccount } = useAuditNames(() =>
  items.value.flatMap((row) => [row.creatorId, row.lastModifierId]),
)

const statusOptions = computed(() => [
  { value: 'all', label: t('Approval:Filter:StatusAll') },
  ...enumOptions(INSTANCE_STATUSES, instanceStatusLabelKey),
])

const rowKey = (row: ApprovalInstanceListDto) => row.id ?? ''

const openDetail = (instanceId?: string) => {
  if (!instanceId) return
  void router.push({
    name: 'ApprovalInstanceDetail',
    params: { instanceId },
    query: { from: 'monitor' },
  })
}

/** 终止操作需要实例明细（含上下文），先取详情再开弹窗。 */
const openTerminate = async (instance: ApprovalInstanceListDto) => {
  if (!instance.id) return

  try {
    activeInstance.value = await approvalMonitorApi.detail(instance.id)
    showTerminate.value = true
  } catch {
    useDiscreteApi.message.error(t('Approval:Monitor:LoadFailed'))
  }
}

const columns = computed<DataTableColumns<ApprovalInstanceListDto>>(() => [
  {
    title: t('Approval:Field:Number'),
    key: 'number',
    width: 180,
    resizable: true,
    render: (row) => h('code', row.number ?? ''),
  },
  {
    title: t('Approval:Field:Title'),
    key: 'title',
    minWidth: 200,
    resizable: true,
    ellipsis: { tooltip: true },
    render: (row) => row.title ?? '',
  },
  {
    title: t('Approval:Field:Definition'),
    key: 'definitionName',
    minWidth: 160,
    resizable: true,
    render: (row) => row.definitionName ?? '',
  },
  {
    title: t('Approval:Field:BusinessType'),
    key: 'businessType',
    width: 140,
    resizable: true,
    render: (row) => businessLabel(row.businessType) || t('Common:None'),
  },
  {
    title: t('Approval:Field:Initiator'),
    key: 'initiatorUserId',
    width: 150,
    resizable: true,
    render: (row) => userLabel(row.initiatorUserId),
  },
  {
    title: t('Approval:Field:Node'),
    key: 'currentNodeName',
    minWidth: 140,
    resizable: true,
    render: (row) => row.currentNodeName || t('Common:None'),
  },
  {
    title: t('Common:Status'),
    key: 'status',
    width: 110,
    resizable: true,
    render: (row) => h(ApprovalStatusTag, { kind: 'instance', status: row.status }),
  },
  ...createAuditColumns<ApprovalInstanceListDto>({ resolveName, resolveAccount }),
  {
    title: t('Common:Actions'),
    key: 'actions',
    width: 170,
    fixed: 'right',
    render: (row) =>
      h('div', { class: 'po-cell-actions' }, [
        h(
          NButton,
          { size: 'small', secondary: true, onClick: () => openDetail(row.id) },
          { default: () => t('Common:View') },
        ),
        ...(canTerminate.value && (row.status === 'Running' || row.status === 'Returned')
          ? [
              h(
                NButton,
                {
                  size: 'small',
                  type: 'error',
                  secondary: true,
                  onClick: () => openTerminate(row),
                },
                { default: () => t('Approval:Monitor:Terminate') },
              ),
            ]
          : []),
      ]),
  },
])

onMounted(() => {
  void load()
  void ensureBusinessTypes()
  void ensureUsers()
})
</script>

<style scoped>
.filter-field {
  width: 210px;
}

.filter-field-narrow {
  width: 165px;
}

.filter-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.instance-table :deep(.title-cell) {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.instance-table :deep(.title-cell strong) {
  color: var(--po-ink);
  font-weight: var(--po-weight-bold);
}

.instance-table :deep(.title-cell code) {
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

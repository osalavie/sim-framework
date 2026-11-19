<template>
  <div class="po-page">
    <header class="po-page-head">
      <div class="po-page-head__stats">
        <div v-for="stat in stats" :key="stat.label" class="po-page-head__stat">
          <span class="po-page-head__stat-label">{{ stat.label }}</span>
          <strong class="po-page-head__stat-value">{{ stat.value }}</strong>
          <span class="po-page-head__stat-foot">{{ stat.foot }}</span>
        </div>
      </div>
    </header>

    <section class="po-card po-card--flush log-filters" :aria-label="t('Openplatform:Logs:FilterLabel')">
      <div class="log-filters__fields">
        <n-input
          v-model:value="filter.appId"
          class="filter-field"
          clearable
          :placeholder="t('Openplatform:Logs:AppIdPlaceholder')"
          @keyup.enter="search"
        />
        <n-select
          v-model:value="filter.succeeded"
          class="filter-field filter-field-narrow"
          :options="statusOptions"
        />
        <n-input
          v-model:value="filter.clientIp"
          class="filter-field"
          clearable
          :placeholder="t('Openplatform:Logs:ClientIpPlaceholder')"
          @keyup.enter="search"
        />
        <n-date-picker
          v-model:formatted-value="filter.startTime"
          class="filter-field"
          type="datetime"
          value-format="yyyy-MM-dd'T'HH:mm:ss"
          clearable
          :placeholder="t('Openplatform:Logs:StartTime')"
        />
        <n-date-picker
          v-model:formatted-value="filter.endTime"
          class="filter-field"
          type="datetime"
          value-format="yyyy-MM-dd'T'HH:mm:ss"
          clearable
          :placeholder="t('Openplatform:Logs:EndTime')"
        />
        <div class="log-filters__actions">
          <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
          <n-button type="primary" :loading="loading" @click="search">{{ t('Common:Search') }}</n-button>
          <n-button secondary @click="reset">{{ t('Openplatform:Logs:Reset') }}</n-button>
        </div>
      </div>
    </section>

    <DataTable
      :columns="columns"
      :data="logs"
      :loading="loading"
      :row-key="rowKey"
      :pagination="pagination"
      framed
      auto-view-mode
      card-pagination
      remote
      @update:page="handlePageChange"
      @update:page-size="handlePageSizeChange"
    >
      <template #card>
        <n-spin :show="loading">
          <div class="log-cards">
            <article v-for="log in logs" :key="log.id" class="po-card log-card">
              <div class="log-card__meta">
                <span
                  class="po-pill"
                  :class="log.succeeded ? 'po-pill--mint po-pill--dot' : 'po-pill--peach po-pill--dot'"
                >
                  {{
                    t(
                      log.succeeded
                        ? 'Openplatform:Logs:StatusSucceeded'
                        : 'Openplatform:Logs:StatusFailed',
                    )
                  }}
                </span>
                <span class="po-tiny po-muted">{{ formatDate(log.creationTime) }}</span>
              </div>
              <code class="po-mono">{{ log.appId }}</code>
              <dl class="po-kv log-card__detail">
                <dt>{{ t('Openplatform:Logs:ClientIp') }}</dt>
                <dd>{{ log.clientIp || t('Common:None') }}</dd>
                <template v-if="!log.succeeded">
                  <dt>{{ t('Openplatform:Logs:Reason') }}</dt>
                  <dd>{{ log.reason || t('Common:None') }}</dd>
                </template>
              </dl>
            </article>
          </div>
          <n-empty v-if="!logs.length && !loading" :description="t('Openplatform:Logs:Empty')" />
        </n-spin>
      </template>
    </DataTable>
  </div>
</template>

<script setup lang="ts">
import { computed, h, onMounted, ref } from 'vue'
import { NTag, type DataTableColumns } from 'naive-ui'
import { DataTable } from '@/components/Table'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { formatDate } from '@/utils/date'
import { openPlatformAccessLogApi, type OpenPlatformAccessLog } from '../api'
import { buildAccessLogQuery, createAccessLogFilter } from '../utils'

const logs = ref<OpenPlatformAccessLog[]>([])
const loading = ref(false)
const filter = ref(createAccessLogFilter())
const pagination = ref({
  page: 1,
  pageSize: 20,
  itemCount: 0,
})

// 成功/失败需要按当前筛选范围统计全量记录，当前页数量没有治理意义
const logStats = ref({ succeeded: 0, failed: 0 })

const stats = computed(() => [
  {
    label: t('Openplatform:Logs:StatsSucceeded'),
    value: logStats.value.succeeded,
    foot: t('Openplatform:Logs:StatsSucceededFoot'),
  },
  {
    label: t('Openplatform:Logs:StatsFailed'),
    value: logStats.value.failed,
    foot: t('Openplatform:Logs:StatsFailedFoot'),
  },
])

const loadStats = async () => {
  // 统计同时展示成功与失败，因此不叠加结果筛选
  const base = buildAccessLogQuery(
    { ...filter.value, succeeded: 'all' },
    { page: 1, pageSize: 1 },
  )
  try {
    const [succeeded, failed] = await Promise.all([
      openPlatformAccessLogApi.getList({ ...base, succeeded: true }),
      openPlatformAccessLogApi.getList({ ...base, succeeded: false }),
    ])
    logStats.value = {
      succeeded: succeeded.totalCount,
      failed: failed.totalCount,
    }
  } catch {
    // 统计失败不阻塞日志列表
  }
}

const statusOptions = computed(() => [
  { label: t('Openplatform:Logs:StatusAll'), value: 'all' },
  { label: t('Openplatform:Logs:StatusSucceeded'), value: 'succeeded' },
  { label: t('Openplatform:Logs:StatusFailed'), value: 'failed' },
])

const rowKey = (row: OpenPlatformAccessLog) => row.id

const columns = computed<DataTableColumns<OpenPlatformAccessLog>>(() => [
  {
    title: t('Openplatform:Logs:Time'),
    key: 'creationTime',
    width: 180,
    render: (row) => formatDate(row.creationTime),
  },
  {
    title: t('Openplatform:Applications:AppId'),
    key: 'appId',
    minWidth: 200,
    render: (row) => h('code', row.appId),
  },
  {
    title: t('Openplatform:Logs:Status'),
    key: 'succeeded',
    width: 110,
    render: (row) =>
      h(
        NTag,
        { size: 'small', bordered: false, type: row.succeeded ? 'success' : 'error' },
        {
          default: () =>
            t(row.succeeded ? 'Openplatform:Logs:StatusSucceeded' : 'Openplatform:Logs:StatusFailed'),
        },
      ),
  },
  {
    title: t('Openplatform:Logs:ClientIp'),
    key: 'clientIp',
    minWidth: 160,
    render: (row) => row.clientIp || t('Common:None'),
  },
  {
    title: t('Openplatform:Logs:Reason'),
    key: 'reason',
    minWidth: 220,
    ellipsis: { tooltip: true },
    render: (row) => (row.succeeded ? '' : row.reason || t('Common:None')),
  },
])

const load = async () => {
  loading.value = true
  try {
    const response = await openPlatformAccessLogApi.getList(
      buildAccessLogQuery(filter.value, {
        page: pagination.value.page,
        pageSize: pagination.value.pageSize,
      }),
    )
    logs.value = response.items
    pagination.value.itemCount = response.totalCount
  } catch {
    useDiscreteApi.message.error(t('Openplatform:Logs:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const search = () => {
  pagination.value.page = 1
  void Promise.all([load(), loadStats()])
}

const reset = () => {
  filter.value = createAccessLogFilter()
  search()
}

const handlePageChange = (page: number) => {
  pagination.value.page = page
  void load()
}

const handlePageSizeChange = (pageSize: number) => {
  pagination.value.page = 1
  pagination.value.pageSize = pageSize
  void load()
}

onMounted(() => void Promise.all([load(), loadStats()]))
</script>

<style scoped>
.log-filters__fields {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  padding: var(--po-density-toolbar-pad-y) 16px;
}

.log-filters__actions {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-left: auto;
}

.filter-field {
  width: 220px;
}

.filter-field-narrow {
  width: 140px;
}

.log-cards {
  display: grid;
  gap: 14px;
}

.log-card {
  display: grid;
  gap: 9px;
}

.log-card__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}

.log-card code {
  overflow-wrap: anywhere;
  color: var(--po-ink-soft);
  font-size: 12.5px;
}

.log-card__detail {
  margin: 0;
}

@media (max-width: 1024px) {
  .log-filters__fields {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
  }

  .filter-field,
  .filter-field-narrow {
    width: 100%;
  }

  .log-filters__actions {
    grid-column: 1 / -1;
    justify-content: flex-end;
  }
}

@media (max-width: 640px) {
  .log-filters__actions {
    width: 100%;
  }

  .log-filters__actions :deep(.n-button) {
    flex: 1 1 auto;
  }
}
</style>

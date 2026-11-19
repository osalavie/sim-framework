<template>
  <DataTable
    v-model:checked-row-keys="checkedRowKeys"
    :columns="columns"
    :data="treeRoutes"
    :loading="loading"
    :pagination="pagination"
    :row-key="(row: Route) => row.id"
    :view-mode="viewMode"
    selectable
    :selection-options="{ fixed: 'left' }"
    framed
    fill
    flex-height
    :default-expand-all="false"
    @update:page="$emit('pageChange', $event)"
    @update:page-size="$emit('pageSizeChange', $event)"
  >
    <template #card>
      <slot name="card" />
    </template>
  </DataTable>
</template>

<script setup lang="ts">
import { h, computed } from 'vue'
import { NButton, NIcon, type DataTableColumns } from 'naive-ui'
import { GitBranchOutline } from '@vicons/ionicons5'
import { DataTable } from '@/components/Table'
import type { Route } from '@/modules/authorization/api/route/routeModels'
import { useAuditNames } from '@/components/AuditInfo/useAuditNames'
import { createAuditColumns } from '@/utils/auditColumns'
import { createActionColumn } from '@/utils/tableColumns'
import { useUserStore } from '@/stores/user'
import { t } from '@/i18n'

interface Props {
  treeRoutes: Route[]
  loading: boolean
  pagination:
    | false
    | {
        page: number
        pageSize: number
        pageCount: number
        itemCount: number
      }
  selectedRowKeys: Array<string | number>
  viewMode?: 'table' | 'card'
}

interface Emits {
  (e: 'view', route: Route): void
  (e: 'edit', route: Route): void
  (e: 'delete', id: string): void
  (e: 'update:selectedRowKeys', value: Array<string | number>): void
  (e: 'pageChange', page: number): void
  (e: 'pageSizeChange', pageSize: number): void
}

const props = defineProps<Props>()

const { resolveName, resolveAccount } = useAuditNames(() =>
  props.treeRoutes.flatMap(function collect(row): Array<string | null | undefined> {
    return [row.creatorId, row.lastModifierId, ...(row.children ?? []).flatMap(collect)]
  }),
)
const emit = defineEmits<Emits>()

const userStore = useUserStore()

const checkedRowKeys = computed<Array<string | number>>({
  get: () => props.selectedRowKeys,
  set: (value) => emit('update:selectedRowKeys', value),
})

const renderBooleanPill = (value: boolean | null | undefined) =>
  h(
    'span',
    { class: ['po-pill', value ? 'po-pill--mint' : 'po-pill--outline'] },
    value ? t('Common:Yes') : t('Common:No'),
  )

// 表格列定义（后端已排序，不需要前端排序）
const columns = computed<DataTableColumns<Route>>(() => [
  {
    title: t('Common:Name'),
    key: 'name',
    resizable: true,
    width: 260,
    // 限制拖拽下限：树形首列需要容纳展开触发器/缩进（level × 16px + 24px）加内边距，
    // 过窄时 ellipsis 即使有全局 max-width 修正（见 style.css）也会因剩余宽度不足而折行。
    minWidth: 120,
    ellipsis: { tooltip: true },
    render: (row) => {
      // 有子路由时在名称前展示分支图标。
      // 注意：此处必须使用内联元素而非 NSpace（flex 容器默认 flex-wrap: wrap），
      // 否则列宽缩窄时图标与名称会折成两行、撑高行高并破坏对齐；内联结构可让
      // 外层 ellipsis 统一保持单行截断并正常触发 tooltip。
      if (row.children && row.children.length > 0) {
        return [
          h(NIcon, {
            component: GitBranchOutline,
            size: 16,
            style: 'margin-right: 4px; vertical-align: -0.18em;',
          }),
          row.name,
        ]
      }
      return row.name
    }
  },
  {
    title: t('Field:Path'),
    key: 'path',
    resizable: true,
    width: 300,
    ellipsis: { tooltip: true },
    render: (row) => h('span', { class: 'po-mono' }, row.path),
  },
  {
    title: t('Field:Component'),
    key: 'component',
    resizable: true,
    width: 200,
    ellipsis: { tooltip: true },
    render: (row) => row.component || '—',
  },
  {
    title: t('Entity:Module'),
    key: 'moduleName',
    resizable: true,
    width: 240,
    ellipsis: { tooltip: true },
    render: (row) =>
      row.moduleName
        ? h('span', { class: 'po-pill po-pill--sky' }, row.moduleName)
        : '—',
  },
  {
    title: t('Field:Menu'),
    key: 'meta.menu',
    width: 110,
    render: (row) => renderBooleanPill(row.meta?.menu),
  },
  {
    title: t('Field:Tab'),
    key: 'meta.tab',
    width: 110,
    render: (row) => renderBooleanPill(row.meta?.tab),
  },
  ...createAuditColumns<Route>({ resolveName, resolveAccount }),
  createActionColumn<Route>((row) => {
    const actions = [
      h(NButton, { size: 'small', secondary: true, onClick: () => emit('view', row) }, { default: () => t('Common:View') }),
    ]

    if (userStore.hasPermission('system:route:update')) {
      actions.push(
        h(NButton, { size: 'small', secondary: true, onClick: () => emit('edit', row) }, { default: () => t('Common:Edit') }),
      )
    }

    if (userStore.hasPermission('system:route:delete')) {
      actions.push(
        h(
          NButton,
          { size: 'small', type: 'error', ghost: true, onClick: () => emit('delete', row.id) },
          { default: () => t('Common:Delete') },
        ),
      )
    }

    return actions
  }),
])
</script>

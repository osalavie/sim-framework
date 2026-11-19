<template>
  <DataTable
    v-model:checked-row-keys="checkedRowKeys"
    :columns="columns"
    :data="permissions"
    :loading="loading"
    :pagination="pagination"
    :row-key="(row: Permission) => row.id"
    :view-mode="viewMode"
    selectable
    :selection-options="{ fixed: 'left' }"
    framed
    fill
    flex-height
    @update:page="$emit('pageChange', $event)"
    @update:page-size="$emit('pageSizeChange', $event)"
    @update:sorter="handleSorterChange"
  >
    <template #card>
      <slot name="card" />
    </template>
  </DataTable>
</template>

<script setup lang="ts">
import { h, computed } from 'vue'
import { NButton, type DataTableColumns } from 'naive-ui'
import { DataTable } from '@/components/Table'
import type { Permission } from '@/modules/authorization/api/permission/permissionModels'
import { useAuditNames } from '@/components/AuditInfo/useAuditNames'
import { createAuditColumns } from '@/utils/auditColumns'
import { createActionColumn } from '@/utils/tableColumns'
import { useUserStore } from '@/stores/user'
import { t } from '@/i18n'

interface SortInfo {
  field: string
  order: 'ascend' | 'descend'
}

interface Props {
  permissions: Permission[]
  loading: boolean
  pagination: {
    page: number
    pageSize: number
    pageCount: number
    itemCount: number
  }
  selectedRowKeys: Array<string | number>
  sortFields?: SortInfo[]
  viewMode?: 'table' | 'card'
}

interface Emits {
  (e: 'view', permission: Permission): void
  (e: 'edit', permission: Permission): void
  (e: 'delete', id: string): void
  (e: 'update:selectedRowKeys', value: Array<string | number>): void
  (e: 'pageChange', page: number): void
  (e: 'pageSizeChange', pageSize: number): void
  (e: 'sortChange', sortFields: SortInfo[]): void
}

const props = defineProps<Props>()

const { resolveName, resolveAccount } = useAuditNames(() =>
  props.permissions.flatMap((row) => [row.creatorId, row.lastModifierId]),
)
const emit = defineEmits<Emits>()

const userStore = useUserStore()

const checkedRowKeys = computed<Array<string | number>>({
  get: () => props.selectedRowKeys,
  set: (value) => emit('update:selectedRowKeys', value),
})

// 排序器类型定义
interface SorterInfo {
  columnKey: string
  order: 'ascend' | 'descend' | false
}

// 处理排序变化 - 支持多列排序（增量式）
const handleSorterChange = (sorter: SorterInfo | SorterInfo[] | null) => {
  if (!sorter) {
    emit('sortChange', [])
    return
  }

  // Naive UI 在多列排序模式下，每次只返回当前点击的列
  // 我们需要基于现有的 sortFields 进行增量更新
  const currentSorter = Array.isArray(sorter) ? sorter[0] : sorter

  if (!currentSorter) {
    emit('sortChange', [])
    return
  }

  // 复制现有的排序字段
  const newSortFields = [...(props.sortFields || [])]

  // 查找当前列是否已经在排序列表中
  const existingIndex = newSortFields.findIndex(s => s.field === currentSorter.columnKey)

  if (currentSorter.order === false) {
    // 如果 order 为 false，表示取消该列的排序
    if (existingIndex !== -1) {
      newSortFields.splice(existingIndex, 1)
    }
  } else {
    // 如果该列已存在，更新其排序方向
    if (existingIndex !== -1) {
      newSortFields[existingIndex] = {
        field: currentSorter.columnKey,
        order: currentSorter.order
      }
    } else {
      // 如果该列不存在，添加到排序列表
      newSortFields.push({
        field: currentSorter.columnKey,
        order: currentSorter.order
      })
    }
  }

  emit('sortChange', newSortFields)
}

// 获取列的排序状态
const getColumnSortOrder = (columnKey: string) => {
  if (!props.sortFields || props.sortFields.length === 0) {
    return false
  }
  const sortInfo = props.sortFields.find(s => s.field === columnKey)
  return sortInfo ? sortInfo.order : false
}

// 头像色彩轮换
const AVATAR_TONES = ['', 'po-avatar--peach', 'po-avatar--mint', 'po-avatar--lilac', 'po-avatar--sun']
const avatarTone = (seed: string) => {
  let hash = 0
  for (let i = 0; i < seed.length; i += 1) {
    hash = (hash + seed.charCodeAt(i)) % AVATAR_TONES.length
  }
  return AVATAR_TONES[hash]
}

// 表格列定义 - 使用 computed 使其响应式
const columns = computed<DataTableColumns<Permission>>(() => [
  {
    title: t('Field:PermissionName'),
    key: 'name',
    resizable: true,
    width: 280,
    sorter: 'default',
    sortOrder: getColumnSortOrder('name'),
    render: (row) =>
      h('div', { class: 'po-table__person' }, [
        h('span', { class: ['po-avatar', avatarTone(row.name || '')] }, (row.name || '?').charAt(0).toUpperCase()),
        h('div', { style: 'display:grid;gap:2px;min-width:0' }, [
          h('b', null, row.name),
          h('span', null, row.code),
        ]),
      ]),
  },
  {
    title: t('Field:PermissionCode'),
    key: 'code',
    resizable: true,
    width: 200,
    ellipsis: { tooltip: true },
    sorter: 'default',
    sortOrder: getColumnSortOrder('code'),
    render: (row) => h('span', { class: 'po-mono' }, row.code),
  },
  {
    title: t('Common:Description'),
    key: 'description',
    resizable: true,
    width: 250,
    ellipsis: { tooltip: true },
    render: (row) => row.description || '-'
  },
  ...createAuditColumns<Permission>({ resolveName, resolveAccount, getSortOrder: getColumnSortOrder }),
  createActionColumn<Permission>((row) => {
    const actions = [
      h(NButton, { size: 'small', secondary: true, onClick: () => emit('view', row) }, { default: () => t('Common:View') }),
    ]

    if (userStore.hasPermission('system:permission:manage')) {
      actions.push(
        h(NButton, { size: 'small', secondary: true, onClick: () => emit('edit', row) }, { default: () => t('Common:Edit') }),
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

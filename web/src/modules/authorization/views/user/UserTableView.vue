<template>
  <DataTable
    v-model:checked-row-keys="checkedRowKeys"
    :columns="columns"
    :data="users"
    :loading="loading"
    :pagination="pagination"
    :row-key="(row: User) => row.id"
    :view-mode="viewMode"
    selectable
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
import type { User } from '@/modules/authorization/api/user/userModels'
import { useAuditNames } from '@/components/AuditInfo/useAuditNames'
import { createAuditColumns } from '@/utils/auditColumns'
import { ACTIONS_COLUMN_WIDTH, createActionColumn } from '@/utils/tableColumns'
import { useUserStore } from '@/stores/user'
import { t } from '@/i18n'

interface SortInfo {
  field: string
  order: 'ascend' | 'descend'
}

interface Props {
  users: User[]
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
  (e: 'view', user: User): void
  (e: 'edit', user: User): void
  (e: 'resetPassword', user: User): void
  (e: 'delete', id: string): void
  (e: 'update:selectedRowKeys', value: Array<string | number>): void
  (e: 'pageChange', page: number): void
  (e: 'pageSizeChange', pageSize: number): void
  (e: 'sortChange', sortFields: SortInfo[]): void
}

const props = defineProps<Props>()

const { resolveName, resolveAccount } = useAuditNames(() =>
  props.users.flatMap((row) => [row.creatorId, row.lastModifierId]),
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

const getInitial = (user: User) =>
  (user.nickName || user.name || user.userName || '?').charAt(0).toUpperCase()

// 性别显示
const getSexLabel = (sex?: number) => {
  if (sex === 1) return t('Authz:SexMale')
  if (sex === 2) return t('Authz:SexFemale')
  return t('Authz:SexUnknown')
}

const getSexTone = (sex?: number) => {
  if (sex === 1) return 'po-pill--sky'
  if (sex === 2) return 'po-pill--lilac'
  return 'po-pill--outline'
}

// 表格列定义 - 使用 computed 使其响应式
const columns = computed<DataTableColumns<User>>(() => [
  {
    title: t('Field:Username'),
    key: 'userName',
    resizable: true,
    width: 240,
    sorter: 'default',
    sortOrder: getColumnSortOrder('userName'),
    render: (row) =>
      h('div', { class: 'po-table__person' }, [
        h('span', { class: ['po-avatar', avatarTone(row.userName || '')] }, getInitial(row)),
        h('div', { style: 'display:grid;gap:2px;min-width:0' }, [
          h('b', null, row.userName),
          h('span', null, row.email || t('Authz:CardNoEmail')),
        ]),
      ]),
  },
  {
    title: t('Field:Nickname'),
    key: 'nickName',
    resizable: true,
    width: 150,
    ellipsis: { tooltip: true },
  },
  {
    title: t('Field:FullName'),
    key: 'name',
    resizable: true,
    width: 120,
    ellipsis: { tooltip: true },
  },
  {
    title: t('Field:Phone'),
    key: 'phone',
    resizable: true,
    width: 150,
    ellipsis: { tooltip: true },
  },
  {
    title: t('Field:Gender'),
    key: 'sex',
    width: 96,
    render: (row) => h('span', { class: ['po-pill', getSexTone(row.sex)] }, getSexLabel(row.sex)),
  },
  ...createAuditColumns<User>({ resolveName, resolveAccount, getSortOrder: getColumnSortOrder }),
  createActionColumn<User>((row) => {
    const actions = [
      h(NButton, { size: 'small', secondary: true, onClick: () => emit('view', row) }, { default: () => t('Common:View') }),
    ]

    if (userStore.hasPermission('system:user:update')) {
      actions.push(
        h(NButton, { size: 'small', secondary: true, onClick: () => emit('edit', row) }, { default: () => t('Common:Edit') }),
      )
    }

    if (!row.isBuiltIn && userStore.hasPermission('system:user:reset-password')) {
      actions.push(
        h(NButton, { size: 'small', secondary: true, onClick: () => emit('resetPassword', row) }, { default: () => t('Authz:ResetPassword') }),
      )
    }

    if (!row.isBuiltIn && userStore.hasPermission('system:user:delete')) {
      actions.push(
        h(
          NButton,
          { size: 'small', type: 'error', ghost: true, onClick: () => emit('delete', row.id) },
          { default: () => t('Common:Delete') },
        ),
      )
    }

    return actions
  }, { width: ACTIONS_COLUMN_WIDTH.quad }),
])
</script>

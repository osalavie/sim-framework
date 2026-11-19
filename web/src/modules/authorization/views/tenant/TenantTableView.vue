<template>
  <DataTable
    v-model:checked-row-keys="checkedRowKeys"
    :columns="columns"
    :data="filteredTenants"
    :loading="loading"
    :row-key="(row: Tenant) => row.id"
    selectable
    :selection-options="{ fixed: 'left', disabled: isProtectedTenant }"
    framed
    fill
    flex-height
  />
</template>

<script setup lang="ts">
import { h, computed } from 'vue'
import { NButton, type DataTableColumns } from 'naive-ui'
import { DataTable } from '@/components/Table'
import type { Tenant } from '@/modules/authorization/api/tenant/tenantModels'
import { useAuditNames } from '@/components/AuditInfo/useAuditNames'
import { createAuditColumns } from '@/utils/auditColumns'
import { createActionColumn, ACTIONS_COLUMN_WIDTH } from '@/utils/tableColumns'
import { t } from '@/i18n'

interface Props {
  tenants: Tenant[]
  loading: boolean
  searchKeyword: string
  selectedRowKeys: Array<string | number>
}

interface Emits {
  (e: 'view', tenant: Tenant): void
  (e: 'edit', tenant: Tenant): void
  (e: 'delete', id: string): void
  (e: 'enable', id: string): void
  (e: 'disable', id: string): void
  (e: 'update:selectedRowKeys', value: Array<string | number>): void
}

const props = defineProps<Props>()

const { resolveName, resolveAccount } = useAuditNames(() =>
  props.tenants.flatMap((row) => [row.creatorId, row.lastModifierId]),
)
const emit = defineEmits<Emits>()

const checkedRowKeys = computed<Array<string | number>>({
  get: () => props.selectedRowKeys,
  set: (value) => emit('update:selectedRowKeys', value),
})

// 前端过滤搜索
const filteredTenants = computed(() => {
  if (!props.searchKeyword) return props.tenants
  const kw = props.searchKeyword.toLowerCase()
  return props.tenants.filter(
    (t) =>
      t.name.toLowerCase().includes(kw) ||
      t.code.toLowerCase().includes(kw) ||
      (t.adminEmail ?? '').toLowerCase().includes(kw),
  )
})

// 头像色彩轮换
const AVATAR_TONES = ['', 'po-avatar--peach', 'po-avatar--mint', 'po-avatar--lilac', 'po-avatar--sun']
const avatarTone = (seed: string) => {
  let hash = 0
  for (let i = 0; i < seed.length; i += 1) {
    hash = (hash + seed.charCodeAt(i)) % AVATAR_TONES.length
  }
  return AVATAR_TONES[hash]
}

const isProtectedTenant = (tenant: Tenant) => tenant.code === 'host' || tenant.code === 'default'

// 表格列定义
const columns = computed<DataTableColumns<Tenant>>(() => [
  {
    title: t('Tenant:Name'),
    key: 'name',
    resizable: true,
    width: 260,
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
    title: t('Common:Status'),
    key: 'isActive',
    width: 120,
    render: (row) =>
      h(
        'span',
        { class: ['po-pill', row.isActive ? 'po-pill--mint' : 'po-pill--coral'] },
        row.isActive ? t('Tenant:Active') : t('Tenant:Disabled'),
      ),
  },
  {
    title: t('Tenant:AdminEmail'),
    key: 'adminEmail',
    resizable: true,
    width: 220,
    ellipsis: { tooltip: true },
    render: (row) => row.adminEmail || '-',
  },
  ...createAuditColumns<Tenant>({ resolveName, resolveAccount }),
  createActionColumn<Tenant>(
    (row) => [
      h(NButton, { size: 'small', secondary: true, onClick: () => emit('view', row) }, { default: () => t('Common:View') }),
      h(NButton, { size: 'small', secondary: true, onClick: () => emit('edit', row) }, { default: () => t('Common:Edit') }),
      row.isActive
        ? h(
            NButton,
            { size: 'small', secondary: true, type: 'warning', onClick: () => emit('disable', row.id) },
            { default: () => t('Tenant:Disable') },
          )
        : h(
            NButton,
            { size: 'small', secondary: true, type: 'success', onClick: () => emit('enable', row.id) },
            { default: () => t('Tenant:Enable') },
          ),
      h(
        NButton,
        {
          size: 'small',
          type: 'error',
          ghost: true,
          disabled: isProtectedTenant(row),
          onClick: () => emit('delete', row.id),
        },
        { default: () => t('Common:Delete') },
      ),
    ],
    { width: ACTIONS_COLUMN_WIDTH.quad },
  ),
])
</script>

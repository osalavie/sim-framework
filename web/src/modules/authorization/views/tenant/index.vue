<template>
  <div class="po-page po-page--fill">
    <header class="po-page-head">
      <div class="po-page-head__stats">
        <div v-for="stat in stats" :key="stat.label" class="po-page-head__stat">
          <span class="po-page-head__stat-label">{{ stat.label }}</span>
          <strong class="po-page-head__stat-value">{{ stat.value }}</strong>
          <span class="po-page-head__stat-foot">{{ stat.foot }}</span>
        </div>
      </div>
    </header>

    <section class="po-toolbar">
      <label class="po-search-box">
        <n-icon :component="SearchOutline" />
        <input
          v-model="searchKeyword"
          type="search"
          :placeholder="t('Authz:SearchTenants')"
          :aria-label="t('Authz:SearchTenants')"
        />
      </label>
      <span class="po-toolbar__spacer"></span>
      <p class="po-small po-muted authz-toolbar-note">{{ t('Authz:TenantsLocalFilter') }}</p>
      <div class="po-toolbar__actions">
        <n-button
          type="error"
          ghost
          :disabled="selectedRowKeys.length === 0"
          @click="handleBatchDelete"
        >
          {{ t('Common:BatchDelete') }} ({{ selectedRowKeys.length }})
        </n-button>
        <n-button type="primary" @click="handleCreate">
          {{ t('Page:CreateEntity', { entity: t('Entity:Tenant') }) }}
        </n-button>
      </div>
    </section>

    <TenantTableView
      :tenants="tenants"
      :loading="loading"
      v-model:search-keyword="searchKeyword"
      v-model:selected-row-keys="selectedRowKeys"
      @view="handleView"
      @edit="handleEdit"
      @delete="handleDelete"
      @enable="handleEnable"
      @disable="handleDisable"
    />

    <!-- 创建/编辑租户弹窗 -->
    <TenantEditModal
      v-model:show="showEditModal"
      :editing-tenant="editingTenant"
      :submitting="submitting"
      :templates="templates"
      @submit="handleSubmit"
    />

    <!-- 查看租户详情弹窗 -->
    <TenantDetailModal v-model:show="showDetailModal" :tenant="viewingTenant" />

    <!-- 创建成功结果弹窗 -->
    <n-modal
      v-model:show="showResultModal"
      preset="card"
      :title="t('Tenant:CreateResultTitle')"
      :style="{ width: '500px' }"
      :segmented="{ content: 'soft' }"
    >
      <div v-if="createResult" class="po-stack">
        <p class="po-note">{{ t('Tenant:CreateResultDesc', { name: createResult.tenantName }) }}</p>
        <section class="po-card po-card--cream authz-modal-block">
          <dl class="po-kv">
            <dt>{{ t('Tenant:TenantCode') }}</dt>
            <dd>{{ createResult.tenantCode }}</dd>
            <dt>{{ t('Tenant:AdminUserName') }}</dt>
            <dd>{{ createResult.adminUserName }}</dd>
            <dt>{{ t('Tenant:InitialPassword') }}</dt>
            <dd><code class="po-mono">{{ createResult.initialPassword }}</code></dd>
            <dt>{{ t('Tenant:AssignedCounts') }}</dt>
            <dd>
              {{ t('Tenant:CountSummary', {
                modules: createResult.assignedModuleCount,
                routes: createResult.assignedRouteCount,
                permissions: createResult.assignedPermissionCount
              }) }}
            </dd>
          </dl>
        </section>
        <p class="po-small po-muted">{{ t('Tenant:CredentialsWarning') }}</p>
      </div>

      <template #footer>
        <n-space justify="end">
          <n-button secondary @click="showResultModal = false">{{ t('Common:Close') }}</n-button>
        </n-space>
      </template>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { SearchOutline } from '@vicons/ionicons5'
import type { Tenant, TenantFormData, CreateTenantResult } from '@/modules/authorization/api/tenant/tenantModels'
import TenantTableView from './TenantTableView.vue'
import TenantEditModal from './TenantEditModal.vue'
import TenantDetailModal from './TenantDetailModal.vue'
import { useTenantData } from './useTenantData'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'

const {
  loading,
  submitting,
  tenants,
  templates,
  createTenant,
  updateTenant,
  deleteTenant,
  deleteTenants,
  enableTenant,
  disableTenant,
} = useTenantData()

// 搜索关键词（租户列表当前为前端过滤）
const searchKeyword = ref('')

// 概览统计 - 复用列表数据，不新增接口
const stats = computed(() => [
  {
    label: t('Authz:StatTenantsActive'),
    value: tenants.value.filter((tenant) => tenant.isActive).length,
    foot: t('Authz:StatTenantsActiveFoot'),
  },
  {
    label: t('Authz:StatTenantsDisabled'),
    value: tenants.value.filter((tenant) => !tenant.isActive).length,
    foot: t('Authz:StatTenantsDisabledFoot'),
  },
])

// 弹窗状态
const showEditModal = ref(false)
const showDetailModal = ref(false)
const showResultModal = ref(false)
const editingTenant = ref<Tenant | null>(null)
const viewingTenant = ref<Tenant | null>(null)
const createResult = ref<CreateTenantResult | null>(null)
const selectedRowKeys = ref<Array<string | number>>([])

// 新建
const handleCreate = () => {
  editingTenant.value = null
  showEditModal.value = true
}

// 查看
const handleView = (tenant: Tenant) => {
  viewingTenant.value = tenant
  showDetailModal.value = true
}

// 编辑
const handleEdit = (tenant: Tenant) => {
  editingTenant.value = tenant
  showEditModal.value = true
}

// 提交表单
const handleSubmit = async (data: TenantFormData) => {
  if (editingTenant.value) {
    // 编辑模式：只更新基本信息
    const success = await updateTenant(editingTenant.value.id, {
      name: data.name,
      adminEmail: data.adminEmail,
      connectionString: data.connectionString,
    })
    if (success) {
      showEditModal.value = false
    }
  } else {
    // 创建模式
    const result = await createTenant(data)
    if (result) {
      showEditModal.value = false
      createResult.value = result
      showResultModal.value = true
    }
  }
}

// 删除
const handleDelete = (id: string) => {
  useDiscreteApi.dialog.warning({
    title: t('Common:Confirm'),
    content: t('Tenant:DeleteConfirm'),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      await deleteTenant(id)
    },
  })
}

const handleBatchDelete = () => {
  const selectedIds = new Set(selectedRowKeys.value.map(String))
  const deletableIds = tenants.value
    .filter((tenant) => selectedIds.has(tenant.id) && tenant.code !== 'host' && tenant.code !== 'default')
    .map((tenant) => tenant.id)

  if (!deletableIds.length) {
    useDiscreteApi.message.warning(t('Authz:DeleteTenantProtected'))
    return
  }

  const dialog = useDiscreteApi.dialog.warning({
    title: t('Common:Confirm'),
    content: t('Feedback:BatchDeleteConfirm', { count: deletableIds.length, entity: t('Entity:Tenant') }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      const success = await deleteTenants(deletableIds)
      if (success) {
        selectedRowKeys.value = []
      }
    },
  })
}

// 启用
const handleEnable = (id: string) => {
  enableTenant(id)
}

// 禁用
const handleDisable = (id: string) => {
  useDiscreteApi.dialog.warning({
    title: t('Common:Confirm'),
    content: t('Tenant:DisableConfirm'),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      await disableTenant(id)
    },
  })
}
</script>

<style scoped>
.authz-toolbar-note {
  margin: 0;
}

.authz-modal-block {
  padding: 16px;
}

</style>

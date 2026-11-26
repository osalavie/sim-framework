<template>
  <div class="po-page po-page--fill">
    <section class="po-toolbar">
      <label class="po-search-box">
        <n-icon :component="SearchOutline" />
        <input
          v-model="searchKeyword"
          type="search"
          :placeholder="t('Authz:SearchRoles')"
          :aria-label="t('Authz:SearchRoles')"
          @keyup.enter="handleSearch"
        />
      </label>
      <span class="po-toolbar__spacer"></span>
      <SegmentedSwitch
        v-if="!isNarrowScreen"
        :model-value="viewMode"
        :options="viewModeOptions"
        :aria-label="t('Table:ViewModeLabel')"
        @update:model-value="handleViewModeChange"
      />
      <div class="po-toolbar__actions">
        <n-button
          v-if="viewMode === 'table'"
          v-permission="'system:role:manage'"
          type="error"
          ghost
          :disabled="selectedRowKeys.length === 0"
          @click="handleBatchDelete"
        >
          {{ t('Common:BatchDelete') }} ({{ selectedRowKeys.length }})
        </n-button>
        <n-button v-permission="'system:role:manage'" type="primary" @click="handleCreate">
          {{ t('Page:CreateEntity', { entity: t('Entity:Role') }) }}
        </n-button>
      </div>
    </section>

    <div class="authz-list-region">
      <RoleTableView
        :view-mode="viewMode"
        :roles="roles"
        :loading="loading"
        :selected-row-keys="selectedRowKeys"
        :pagination="pagination"
        :sort-fields="sortFields"
        @view="handleView"
        @edit="handleEdit"
        @delete="handleDelete"
        @page-change="handlePageChange"
        @page-size-change="handlePageSizeChange"
        @sort-change="handleSortChange"
        @update:selected-row-keys="selectedRowKeys = $event"
      >
        <template #card>
          <RoleCardView
            :roles="roles"
            :loading="loading"
            :has-more="hasMore"
            :loading-more="loadingMore"
            v-model:search-keyword="searchKeyword"
            @search="handleSearch"
            @view="handleView"
            @edit="handleEdit"
            @delete="handleDelete"
            @load-more="loadMore"
          />
        </template>
      </RoleTableView>
    </div>

    <!-- 创建/编辑角色弹窗 -->
    <RoleEditModal
      v-model:show="showEditModal"
      :editing-role="editingRole"
      :submitting="submitting"
      :modules="modules"
      :role-modules="editingRoleModules"
      :permissions="permissions"
      :role-permissions="editingRolePermissions"
      @submit="handleSubmit"
    />

    <!-- 查看角色详情弹窗 -->
    <RoleDetailModal v-model:show="showDetailModal" :role="viewingRole" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { SearchOutline } from '@vicons/ionicons5'
import { isBuiltInRole, type Role, type RoleFormData } from '@/modules/authorization/api/role/roleModels'
import RoleTableView from './RoleTableView.vue'
import RoleCardView from './RoleCardView.vue'
import RoleEditModal from './RoleEditModal.vue'
import RoleDetailModal from './RoleDetailModal.vue'
import { useRoleData } from './useRoleData'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { useResponsive } from '@/hooks/useResponsive'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'

// 使用数据管理 composable
const {
  loading,
  loadingMore,
  submitting,
  roles,
  modules,
  permissions,
  searchKeyword,
  sortFields,
  pagination,
  hasMore,
  loadRoles,
  loadMore,
  handleSearch,
  handlePageChange,
  handlePageSizeChange,
  handleSortChange,
  createRole,
  updateRole,
  deleteRole,
  deleteRoles,
  getRoleModules,
  getRolePermissions,
} = useRoleData()

// 响应式屏幕尺寸
const { isNarrowScreen } = useResponsive()

// 视图模式 - 根据屏幕尺寸自动切换，宽屏可通过工具栏切换
const viewMode = ref<'table' | 'card'>('table')

const viewModeOptions = computed(() => [
  { label: t('Table:ViewTable'), value: 'table' as const },
  { label: t('Table:ViewCard'), value: 'card' as const },
])

const handleViewModeChange = (mode: 'table' | 'card') => {
  if (mode === viewMode.value) return
  viewMode.value = mode
  pagination.value.page = 1
  void loadRoles()
}

// 监听屏幕尺寸变化，自动切换视图模式
watch(
  isNarrowScreen,
  (narrow) => {
    handleViewModeChange(narrow ? 'card' : 'table')
  },
  { immediate: true },
)

// 概览统计 - 复用列表数据，不新增接口
// 弹窗状态
const showEditModal = ref(false)
const showDetailModal = ref(false)
const editingRole = ref<Role | null>(null)
const viewingRole = ref<Role | null>(null)
const editingRoleModules = ref<string[]>([])
const editingRolePermissions = ref<string[]>([])
const selectedRowKeys = ref<Array<string | number>>([])

// 新建
const handleCreate = () => {
  editingRole.value = null
  editingRoleModules.value = []
  editingRolePermissions.value = []
  showEditModal.value = true
}

// 查看
const handleView = (role: Role) => {
  viewingRole.value = role
  showDetailModal.value = true
}

// 编辑
const handleEdit = async (role: Role) => {
  if (!isBuiltInRole(role)) {
    const [roleModules, rolePermissions] = await Promise.all([
      getRoleModules(role.id),
      getRolePermissions(role.id),
    ])

    if (!roleModules || !rolePermissions) {
      useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Role') }))
      return
    }

    editingRoleModules.value = roleModules
    editingRolePermissions.value = rolePermissions
  } else {
    editingRoleModules.value = []
    editingRolePermissions.value = []
  }

  editingRole.value = role
  showEditModal.value = true
}

// 提交表单
const handleSubmit = async (data: RoleFormData) => {
  const success = editingRole.value
    ? await updateRole(editingRole.value.id, data)
    : await createRole(data)

  if (success) {
    showEditModal.value = false
  }
}

// 删除
const handleDelete = async (id: string) => {
  const dialog = useDiscreteApi.dialog.warning({
    title: t('Authz:DeleteRoleTitle'),
    content: t('Authz:DeleteRoleContent'),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      await deleteRole(id)
    },
  })
}

const handleBatchDelete = () => {
  const ids = selectedRowKeys.value.map(String)
  if (!ids.length) return

  const builtInRoleIds = new Set(
    roles.value.filter(isBuiltInRole).map((role) => role.id),
  )
  const deletableIds = ids.filter((id) => !builtInRoleIds.has(id))
  if (deletableIds.length !== ids.length) {
    useDiscreteApi.message.warning(t('Authz:AdminRoleLocked'))
    if (deletableIds.length === 0) return
  }

  const dialog = useDiscreteApi.dialog.warning({
    title: t('Common:Confirm'),
    content: t('Feedback:BatchDeleteConfirm', { count: deletableIds.length, entity: t('Entity:Role') }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      const success = await deleteRoles(deletableIds)
      if (success) {
        selectedRowKeys.value = []
      }
    },
  })
}
</script>

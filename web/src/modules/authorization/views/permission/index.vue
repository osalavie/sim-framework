<template>
  <div class="po-page po-page--fill">
    <section class="po-toolbar">
      <label class="po-search-box">
        <n-icon :component="SearchOutline" />
        <input
          v-model="searchKeyword"
          type="search"
          :placeholder="t('Authz:SearchPermissions')"
          :aria-label="t('Authz:SearchPermissions')"
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
          v-permission="'system:permission:manage'"
          type="error"
          ghost
          :disabled="selectedRowKeys.length === 0"
          @click="handleBatchDelete"
        >
          {{ t('Common:BatchDelete') }} ({{ selectedRowKeys.length }})
        </n-button>
        <n-button v-permission="'system:permission:manage'" type="primary" @click="handleCreate">
          {{ t('Page:CreateEntity', { entity: t('Entity:Permission') }) }}
        </n-button>
      </div>
    </section>

    <div class="authz-list-region">
      <PermissionTableView
        :view-mode="viewMode"
        :permissions="permissions"
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
          <PermissionCardView
            :permissions="permissions"
            :loading="loading"
            v-model:search-keyword="searchKeyword"
            :has-more="hasMore"
            :loading-more="loadingMore"
            @search="handleSearch"
            @view="handleView"
            @edit="handleEdit"
            @delete="handleDelete"
            @load-more="loadMore"
          />
        </template>
      </PermissionTableView>
    </div>

    <!-- 创建/编辑权限弹窗 -->
    <PermissionEditModal
      v-model:show="showEditModal"
      :editing-permission="editingPermission"
      :submitting="submitting"
      @submit="handleSubmit"
    />

    <!-- 查看权限详情弹窗 -->
    <PermissionDetailModal v-model:show="showDetailModal" :permission="viewingPermission" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { SearchOutline } from '@vicons/ionicons5'
import type { Permission, PermissionFormData } from '@/modules/authorization/api/permission/permissionModels'
import PermissionTableView from './PermissionTableView.vue'
import PermissionCardView from './PermissionCardView.vue'
import PermissionEditModal from './PermissionEditModal.vue'
import PermissionDetailModal from './PermissionDetailModal.vue'
import { usePermissionData } from './usePermissionData'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { useResponsive } from '@/hooks/useResponsive'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'

// 使用数据管理 composable
const {
  loading,
  loadingMore,
  submitting,
  permissions,
  searchKeyword,
  sortFields,
  hasMore,
  loadPermissions,
  loadMore,
  pagination,
  handleSearch,
  handlePageChange,
  handlePageSizeChange,
  handleSortChange,
  createPermission,
  updatePermission,
  deletePermission,
  deletePermissions,
} = usePermissionData()

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
  void loadPermissions()
}

// 监听屏幕尺寸变化，自动切换视图模式
watch(
  isNarrowScreen,
  (narrow) => {
    handleViewModeChange(narrow ? 'card' : 'table')
  },
  { immediate: true },
)

// 弹窗状态
const showEditModal = ref(false)
const showDetailModal = ref(false)
const editingPermission = ref<Permission | null>(null)
const viewingPermission = ref<Permission | null>(null)
const selectedRowKeys = ref<Array<string | number>>([])

// 新建
const handleCreate = () => {
  editingPermission.value = null
  showEditModal.value = true
}

// 查看
const handleView = (permission: Permission) => {
  viewingPermission.value = permission
  showDetailModal.value = true
}

// 编辑
const handleEdit = (permission: Permission) => {
  editingPermission.value = permission
  showEditModal.value = true
}

// 提交表单
const handleSubmit = async (data: PermissionFormData) => {
  const success = editingPermission.value
    ? await updatePermission(editingPermission.value.id, data)
    : await createPermission(data)

  if (success) {
    showEditModal.value = false
  }
}

// 删除
const handleDelete = async (id: string) => {
  const dialog = useDiscreteApi.dialog.warning({
    title: t('Authz:DeletePermissionTitle'),
    content: t('Authz:DeletePermissionContent'),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      await deletePermission(id)
    },
  })
}

const handleBatchDelete = () => {
  const ids = selectedRowKeys.value.map(String)
  if (!ids.length) return

  const dialog = useDiscreteApi.dialog.warning({
    title: t('Common:Confirm'),
    content: t('Feedback:BatchDeleteConfirm', { count: ids.length, entity: t('Entity:Permission') }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      const success = await deletePermissions(ids)
      if (success) {
        selectedRowKeys.value = []
      }
    },
  })
}
</script>

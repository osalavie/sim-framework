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
          :placeholder="t('Authz:SearchModules')"
          :aria-label="t('Authz:SearchModules')"
          @keyup.enter="handleSearch"
        />
      </label>
      <n-button
        v-if="isNarrowScreen"
        class="module-create-fab"
        type="primary"
        circle
        :aria-label="t('Page:CreateEntity', { entity: t('Entity:Module') })"
        @click="handleCreate"
      >
        <template #icon><n-icon :component="AddOutline" /></template>
      </n-button>
      <span class="po-toolbar__spacer"></span>
      <SegmentedSwitch
        v-if="!isNarrowScreen"
        :model-value="viewMode"
        :options="viewModeOptions"
        :aria-label="t('Table:ViewModeLabel')"
        @update:model-value="handleViewModeChange"
      />
      <div v-if="!isNarrowScreen" class="po-toolbar__actions">
        <n-button
          v-if="viewMode === 'table'"
          v-permission="'system:module:delete'"
          type="error"
          ghost
          :disabled="selectedRowKeys.length === 0"
          @click="handleBatchDelete"
        >
          {{ t('Common:BatchDelete') }} ({{ selectedRowKeys.length }})
        </n-button>
        <n-button v-permission="'system:module:create'" type="primary" @click="handleCreate">
          {{ t('Page:CreateEntity', { entity: t('Entity:Module') }) }}
        </n-button>
      </div>
    </section>

    <div class="authz-list-region">
      <ModuleTableView
        :view-mode="viewMode"
        :modules="modules"
        :loading="loading"
        :selected-row-keys="selectedRowKeys"
        :pagination="pagination"
        :sort-fields="sortFields"
        @view="handleView"
        @edit="handleEdit"
        @delete="handleDeleteWithCheck"
        @page-change="handlePageChange"
        @page-size-change="handlePageSizeChange"
        @sort-change="handleSortChange"
        @update:selected-row-keys="selectedRowKeys = $event"
      >
        <template #card>
          <ModuleCardView
            :modules="modules"
            :loading="loading"
            :has-more="hasMore"
            :loading-more="loadingMore"
            v-model:search-keyword="searchKeyword"
            @search="handleSearch"
            @view="handleView"
            @edit="handleEdit"
            @delete="handleDeleteWithCheck"
            @load-more="loadMore"
          />
        </template>
      </ModuleTableView>
    </div>

    <!-- 创建/编辑模块弹窗 -->
    <ModuleEditModal
      v-model:show="showEditModal"
      :editing-module="editingModule"
      :submitting="submitting"
      :routes="routes"
      @submit="handleSubmit"
    />

    <!-- 查看模块详情弹窗 -->
    <ModuleDetailModal v-model:show="showDetailModal" :module="viewingModule" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AddOutline, SearchOutline } from '@vicons/ionicons5'
import type { Module, ModuleFormData } from '@/modules/authorization/api/module/moduleModels'
import ModuleTableView from './ModuleTableView.vue'
import ModuleCardView from './ModuleCardView.vue'
import ModuleEditModal from './ModuleEditModal.vue'
import ModuleDetailModal from './ModuleDetailModal.vue'
import { useModuleData } from './useModuleData'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { useResponsive } from '@/hooks/useResponsive'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'

// 使用数据管理 composable
const {
  loading,
  loadingMore,
  submitting,
  modules,
  routes,
  statsModules,
  searchKeyword,
  sortFields,
  pagination,
  hasMore,
  loadModules,
  loadMore,
  handleSearch,
  handlePageChange,
  handlePageSizeChange,
  handleSortChange,
  createModule,
  updateModule,
  deleteModule,
  deleteModules,
  checkModuleAssociations,
} = useModuleData()

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
  void loadModules()
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
// 只统计有治理意义的启用/停用分布，且使用全量模块而非当前页
const stats = computed(() => [
  {
    label: t('Authz:StatModulesEnabled'),
    value: statsModules.value.filter((item) => item.enabled).length,
    foot: t('Authz:StatModulesEnabledFoot'),
  },
  {
    label: t('Authz:StatModulesDisabled'),
    value: statsModules.value.filter((item) => !item.enabled).length,
    foot: t('Authz:StatModulesDisabledFoot'),
  },
])

// 弹窗状态
const showEditModal = ref(false)
const showDetailModal = ref(false)
const editingModule = ref<Module | null>(null)
const viewingModule = ref<Module | null>(null)
const selectedRowKeys = ref<Array<string | number>>([])

// 新建
const handleCreate = () => {
  editingModule.value = null
  showEditModal.value = true
}

// 查看
const handleView = (module: Module) => {
  viewingModule.value = module
  showDetailModal.value = true
}

// 编辑
const handleEdit = (module: Module) => {
  editingModule.value = module
  showEditModal.value = true
}

// 提交表单
const handleSubmit = async (data: ModuleFormData) => {
  const success = editingModule.value
    ? await updateModule(editingModule.value.id, data)
    : await createModule(data)

  if (success) {
    showEditModal.value = false
  }
}

// 删除前检查关联数据
const handleDeleteWithCheck = async (id: string) => {
  // 先检查是否有关联数据
  const associations = await checkModuleAssociations(id)

  let warningContent = t('Authz:DeleteModuleContent')

  // 删除模块只解除路由关联，路由本身保留在全局目录
  if (associations.hasRoutes) {
    warningContent = t('Authz:DeleteModuleWithRoutes', {
      count: associations.routeCount,
    })
  }

  const dialog = useDiscreteApi.dialog.warning({
    title: t('Authz:DeleteModuleTitle'),
    content: warningContent,
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      await deleteModule(id)
    },
  })
}

const handleBatchDelete = () => {
  const ids = selectedRowKeys.value.map(String)
  if (!ids.length) return

  const dialog = useDiscreteApi.dialog.warning({
    title: t('Common:Confirm'),
    content: t('Feedback:BatchDeleteConfirm', { count: ids.length, entity: t('Entity:Module') }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      const success = await deleteModules(ids)
      if (success) {
        selectedRowKeys.value = []
      }
    },
  })
}
</script>

<style scoped>
.po-toolbar .module-create-fab {
  flex: none;
  width: var(--po-density-toolbar-control-h);
  height: var(--po-density-toolbar-control-h);
  min-width: var(--po-density-toolbar-control-h);
  padding: 0;
}
</style>

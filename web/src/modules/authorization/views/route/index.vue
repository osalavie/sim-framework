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
          :placeholder="t('Authz:SearchRoutes')"
          :aria-label="t('Authz:SearchRoutes')"
          @keyup.enter="handleSearchResetParent"
        />
      </label>
      <n-select
        class="authz-filter"
        :value="selectedModuleId"
        :options="moduleOptions"
        :placeholder="t('Table:FilterModule')"
        clearable
        @update:value="handleModuleFilterResetParent"
      />
      <button
        v-if="activeParentRoute"
        type="button"
        class="po-pill po-pill--accent authz-parent-filter"
        v-tooltip="t('Authz:ClearParentFilter')"
        :aria-label="t('Authz:ClearParentFilter')"
        @click="clearParentFilter"
      >
        <n-icon :component="GitBranchOutline" size="14" />
        <span class="authz-parent-filter__name">
          {{ t('Authz:FilterByParent', { name: activeParentRoute.name }) }}
        </span>
        <n-icon :component="CloseOutline" size="14" />
      </button>
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
          v-permission="'system:route:delete'"
          type="error"
          ghost
          :disabled="selectedRowKeys.length === 0"
          @click="handleBatchDelete"
        >
          {{ t('Common:BatchDelete') }} ({{ selectedRowKeys.length }})
        </n-button>
        <n-button v-permission="'system:route:create'" type="primary" @click="handleCreate">
          {{ t('Page:CreateEntity', { entity: t('Entity:Route') }) }}
        </n-button>
      </div>
    </section>

    <div class="authz-list-region">
      <RouteTableView
        :view-mode="viewMode"
        :tree-routes="visibleRoutes"
        :loading="loading"
        :pagination="listPagination"
        :selected-row-keys="selectedRowKeys"
        @view="handleView"
        @edit="handleEdit"
        @delete="handleDelete"
        @page-change="handlePageChange"
        @page-size-change="handlePageSizeChange"
        @update:selected-row-keys="selectedRowKeys = $event"
      >
        <template #card>
          <RouteCardView
            :tree-routes="visibleRoutes"
            :loading="loading"
            :search-keyword="searchKeyword"
            :has-more="listHasMore"
            :loading-more="loadingMore"
            @view="handleView"
            @edit="handleEdit"
            @delete="handleDelete"
            @filter-children="handleChildFilter"
            @load-more="loadMore"
          />
        </template>
      </RouteTableView>
    </div>

    <!-- 创建/编辑路由弹窗 -->
    <RouteEditModal
      v-model:show="showEditModal"
      :editing-route="editingRoute"
      :routes="treeRoutes"
      :permissions="permissions"
      :submitting="submitting"
      @submit="handleSubmit"
    />

    <!-- 查看路由详情弹窗 -->
    <RouteDetailModal v-model:show="showDetailModal" :route="viewingRoute" :routes="treeRoutes" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { CloseOutline, GitBranchOutline, SearchOutline } from '@vicons/ionicons5'
import type { Route, RouteFormData } from '@/modules/authorization/api/route/routeModels'
import RouteTableView from './RouteTableView.vue'
import RouteCardView from './RouteCardView.vue'
import RouteEditModal from './RouteEditModal.vue'
import RouteDetailModal from './RouteDetailModal.vue'
import { useRouteData } from './useRouteData'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { useResponsive } from '@/hooks/useResponsive'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'

// 使用数据管理 composable
const {
  loading,
  loadingMore,
  submitting,
  treeRoutes,
  statsRoutes,
  modules,
  permissions,
  searchKeyword,
  selectedModuleId,
  pagination,
  hasMore,
  loadRoutes,
  loadMore,
  handleSearch,
  handleModuleFilter,
  handlePageChange,
  handlePageSizeChange,
  createRoute,
  updateRoute,
  deleteRoute,
  deleteRoutes,
} = useRouteData()

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
  void loadRoutes()
}

// 监听屏幕尺寸变化，自动切换视图模式
watch(
  isNarrowScreen,
  (narrow) => {
    handleViewModeChange(narrow ? 'card' : 'table')
  },
  { immediate: true },
)

// 模块过滤选项
const moduleOptions = computed(() =>
  modules.value.map((module) => ({
    label: module.name,
    value: module.id,
  })),
)

// 父路由筛选：点击卡片上的「子路由」后，以该父路由为条件展示其子路由
const activeParentId = ref<string | null>(null)

const findRouteById = (routes: Route[], id: string): Route | undefined => {
  for (const route of routes) {
    if (route.id === id) return route
    const matched = route.children?.length ? findRouteById(route.children, id) : undefined
    if (matched) return matched
  }
  return undefined
}

// 从当前树中按 id 重新解析，编辑/删除后子路由数据仍与列表保持同步
const activeParentRoute = computed(() => {
  if (!activeParentId.value) return undefined
  const parent = findRouteById(treeRoutes.value, activeParentId.value)
  return parent?.children?.length ? parent : undefined
})

const visibleRoutes = computed(() => activeParentRoute.value?.children ?? treeRoutes.value)
const listPagination = computed(() => (activeParentRoute.value ? false : pagination.value))
const listHasMore = computed(() => !activeParentRoute.value && hasMore.value)

const handleChildFilter = (route: Route) => {
  activeParentId.value = route.id
  selectedRowKeys.value = []
}

const clearParentFilter = () => {
  activeParentId.value = null
  selectedRowKeys.value = []
}

// 父路由被删除或子路由清空后，自动退出筛选状态
watch(activeParentRoute, (parent) => {
  if (activeParentId.value && !parent) {
    clearParentFilter()
  }
})

const handleSearchResetParent = () => {
  clearParentFilter()
  handleSearch()
}

const handleModuleFilterResetParent = (moduleId?: string) => {
  clearParentFilter()
  handleModuleFilter(moduleId)
}

// 只保留需要治理的特殊状态；统计基于全量路由树，避免翻页时指标跳动
const flattenRoutes = (routes: Route[]): Route[] =>
  routes.flatMap((route) => [route, ...flattenRoutes(route.children ?? [])])

const stats = computed(() => {
  const allRoutes = flattenRoutes(statsRoutes.value)
  const scopedRoutes = selectedModuleId.value
    ? allRoutes.filter((route) => route.moduleIds?.includes(selectedModuleId.value!))
    : allRoutes
  return [
    {
      label: t('Authz:StatRoutesDisabled'),
      value: scopedRoutes.filter((route) => route.meta?.disabled).length,
      foot: t('Authz:StatRoutesDisabledFoot'),
    },
    {
      label: t('Authz:StatRoutesAnonymous'),
      value: scopedRoutes.filter((route) => route.meta?.anonymous).length,
      foot: t('Authz:StatRoutesAnonymousFoot'),
    },
  ]
})

// 弹窗状态
const showEditModal = ref(false)
const showDetailModal = ref(false)
const editingRoute = ref<Route | null>(null)
const viewingRoute = ref<Route | null>(null)
const selectedRowKeys = ref<Array<string | number>>([])

// 新建
const handleCreate = () => {
  editingRoute.value = null
  showEditModal.value = true
}

// 查看
const handleView = (route: Route) => {
  viewingRoute.value = route
  showDetailModal.value = true
}

// 编辑
const handleEdit = (route: Route) => {
  editingRoute.value = route
  showEditModal.value = true
}

// 提交表单
const handleSubmit = async (data: RouteFormData) => {
  const success = editingRoute.value
    ? await updateRoute(editingRoute.value.id, { id: editingRoute.value.id, ...data })
    : await createRoute(data)

  if (success) {
    showEditModal.value = false
  }
}

// 删除
const handleDelete = async (id: string) => {
  const dialog = useDiscreteApi.dialog.warning({
    title: t('Authz:DeleteRouteTitle'),
    content: t('Authz:DeleteRouteContent'),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      await deleteRoute(id)
    },
  })
}

const handleBatchDelete = () => {
  const ids = selectedRowKeys.value.map(String)
  if (!ids.length) return

  const dialog = useDiscreteApi.dialog.warning({
    title: t('Common:Confirm'),
    content: t('Feedback:BatchDeleteConfirm', { count: ids.length, entity: t('Entity:Route') }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      const success = await deleteRoutes(ids)
      if (success) {
        selectedRowKeys.value = []
      }
    },
  })
}
</script>

<style scoped>
.authz-filter {
  width: 200px;
}

.authz-parent-filter {
  max-width: 280px;
  cursor: pointer;
  font-family: inherit;
  transition:
    background-color 0.12s ease,
    border-color 0.12s ease;
}

.authz-parent-filter:hover,
.authz-parent-filter:focus-visible {
  background: color-mix(in oklab, var(--po-accent-soft) 70%, var(--po-paper));
}

.authz-parent-filter__name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>

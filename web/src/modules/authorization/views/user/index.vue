<template>
  <div class="po-page po-page--fill">
    <section class="po-toolbar">
      <label class="po-search-box">
        <n-icon :component="SearchOutline" />
        <input
          v-model="searchKeyword"
          type="search"
          :placeholder="t('Authz:SearchUsers')"
          :aria-label="t('Authz:SearchUsers')"
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
          v-permission="'system:user:delete'"
          type="error"
          ghost
          :disabled="selectedRowKeys.length === 0"
          @click="handleBatchDelete"
        >
          {{ t('Common:BatchDelete') }} ({{ selectedRowKeys.length }})
        </n-button>
        <n-button v-permission="'system:user:create'" type="primary" @click="handleCreate">
          {{ t('Page:CreateEntity', { entity: t('Entity:User') }) }}
        </n-button>
      </div>
    </section>

    <div class="authz-list-region">
      <UserTableView
        :view-mode="viewMode"
        :users="users"
        :loading="loading"
        :selected-row-keys="selectedRowKeys"
        :pagination="pagination"
        :sort-fields="sortFields"
        @view="handleView"
        @edit="handleEdit"
        @reset-password="handleResetPassword"
        @delete="handleDelete"
        @page-change="handlePageChange"
        @page-size-change="handlePageSizeChange"
        @sort-change="handleSortChange"
        @update:selected-row-keys="selectedRowKeys = $event"
      >
        <template #card>
          <UserCardView
            :users="users"
            :loading="loading"
            :has-more="hasMore"
            :loading-more="loadingMore"
            v-model:search-keyword="searchKeyword"
            @search="handleSearch"
            @view="handleView"
            @edit="handleEdit"
            @reset-password="handleResetPassword"
            @delete="handleDelete"
            @load-more="loadMore"
          />
        </template>
      </UserTableView>
    </div>

    <!-- 创建/编辑用户弹窗 -->
    <UserEditModal
      v-model:show="showEditModal"
      :editing-user="editingUser"
      :submitting="submitting"
      :roles="roles"
      @submit="handleSubmit"
    />

    <!-- 查看用户详情弹窗 -->
    <UserDetailModal v-model:show="showDetailModal" :user="viewingUser" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { SearchOutline } from '@vicons/ionicons5'
import type { User, UserFormData } from '@/modules/authorization/api/user/userModels'
import UserTableView from './UserTableView.vue'
import UserCardView from './UserCardView.vue'
import UserEditModal from './UserEditModal.vue'
import UserDetailModal from './UserDetailModal.vue'
import { useUserData } from './useUserData'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { useResponsive } from '@/hooks/useResponsive'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'

// 使用数据管理 composable
const {
  loading,
  loadingMore,
  submitting,
  users,
  roles,
  searchKeyword,
  sortFields,
  pagination,
  hasMore,
  loadUsers,
  loadMore,
  handleSearch,
  handlePageChange,
  handlePageSizeChange,
  handleSortChange,
  createUser,
  updateUser,
  resetUserPassword,
  deleteUser,
  deleteUsers,
} = useUserData()

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
  void loadUsers()
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
const editingUser = ref<User | null>(null)
const viewingUser = ref<User | null>(null)
const selectedRowKeys = ref<Array<string | number>>([])

// 新建
const handleCreate = () => {
  editingUser.value = null
  showEditModal.value = true
}

// 查看
const handleView = (user: User) => {
  viewingUser.value = user
  showDetailModal.value = true
}

// 编辑
const handleEdit = (user: User) => {
  editingUser.value = user
  showEditModal.value = true
}

// 提交表单
const handleSubmit = async (data: UserFormData) => {
  const success = editingUser.value
    ? await updateUser(editingUser.value.id, data)
    : await createUser(data)

  if (success) {
    showEditModal.value = false
  }
}

// 重置密码
const handleResetPassword = (user: User) => {
  const dialog = useDiscreteApi.dialog.warning({
    title: t('Authz:ResetPasswordTitle'),
    content: t('Authz:ResetPasswordContent', { user: user.nickName || user.userName }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      await resetUserPassword(user.id)
    },
  })
}

// 删除
const handleDelete = async (id: string) => {
  if (users.value.find((user) => user.id === id)?.isBuiltIn) {
    useDiscreteApi.message.warning(t('Authz:BuiltInUserUndeletable'))
    return
  }

  const dialog = useDiscreteApi.dialog.warning({
    title: t('Authz:DeleteUserTitle'),
    content: t('Authz:DeleteUserContent'),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      await deleteUser(id)
    },
  })
}

const handleBatchDelete = () => {
  const ids = selectedRowKeys.value.map(String)
  if (!ids.length) return

  const builtInIds = new Set(
    users.value.filter((user) => user.isBuiltIn).map((user) => user.id),
  )
  const deletableIds = ids.filter((id) => !builtInIds.has(id))
  if (deletableIds.length !== ids.length) {
    useDiscreteApi.message.warning(t('Authz:BuiltInUserUndeletable'))
    if (deletableIds.length === 0) return
  }

  const dialog = useDiscreteApi.dialog.warning({
    title: t('Common:Confirm'),
    content: t('Feedback:BatchDeleteConfirm', { count: deletableIds.length, entity: t('Entity:User') }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      dialog.loading = true
      const success = await deleteUsers(deletableIds)
      if (success) {
        selectedRowKeys.value = []
      }
    },
  })
}
</script>

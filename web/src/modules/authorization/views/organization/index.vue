<template>
  <div class="po-page">
    <n-result v-if="!canManage" status="403" :title="t('Organization:NoPermission')" />

    <div v-else class="po-grid authz-org-grid">
      <aside class="po-card authz-tree-panel">
        <label class="po-search-box authz-tree-search">
          <n-icon :component="SearchOutline" />
          <input
            v-model="keyword"
            type="search"
            :placeholder="t('Organization:SearchPlaceholder')"
            :aria-label="t('Organization:SearchPlaceholder')"
          />
        </label>
        <div class="authz-tree-actions">
          <n-button size="small" secondary :loading="loading" @click="loadTree()">
            {{ t('Common:Refresh') }}
          </n-button>
          <n-button size="small" type="primary" @click="openCreate(null)">
            {{ t('Organization:CreateRoot') }}
          </n-button>
        </div>
        <n-spin :show="loading">
          <n-tree
            class="authz-tree"
            block-line
            show-line
            :data="treeData"
            :pattern="keyword"
            :theme-overrides="treeThemeOverrides"
            :selected-keys="selectedKeys"
            :expanded-keys="expandedKeys"
            :expand-on-click="false"
            :node-props="treeNodeProps"
            @update:selected-keys="handleSelect"
            @update:expanded-keys="handleExpandedKeysChange"
          />
          <p v-if="!treeData.length && !loading" class="po-small po-muted">
            {{ t('Organization:Empty') }}
          </p>
        </n-spin>
      </aside>

      <section class="po-card authz-detail-panel">
        <div v-if="!selectedUnit" class="authz-detail-empty">
          <span class="po-sticker">{{ t('Organization:Title') }}</span>
          <p class="po-small po-muted">{{ t('Organization:SelectHint') }}</p>
        </div>

        <template v-else>
          <header class="po-card__head authz-detail-head">
            <div>
              <h3>{{ selectedUnit.name }}</h3>
              <p class="authz-detail-path">
                <template v-if="unitTrail.length > 1">
                  <template v-for="(unit, index) in unitTrail" :key="unit.id">
                    <span v-if="index > 0" class="authz-detail-path__sep" aria-hidden="true">/</span>
                    <span
                      :class="{ 'authz-detail-path__current': index === unitTrail.length - 1 }"
                    >
                      {{ unit.name }}
                    </span>
                  </template>
                </template>
                <span v-else class="po-pill po-pill--outline">
                  {{ t('Organization:RootUnit') }}
                </span>
              </p>
            </div>
            <div class="po-cell-actions">
              <n-button size="small" secondary @click="openCreate(selectedUnit)">
                {{ t('Organization:CreateChild') }}
              </n-button>
              <n-button size="small" secondary @click="startEdit">{{ t('Common:Edit') }}</n-button>
              <n-button size="small" secondary type="primary" @click="openAddMember">
                {{ t('Organization:AddMember') }}
              </n-button>
              <n-button size="small" type="error" ghost @click="removeUnit">
                {{ t('Common:Delete') }}
              </n-button>
            </div>
          </header>

          <dl class="po-kv authz-detail-kv">
            <dt>{{ t('Organization:Field:Code') }}</dt>
            <dd>{{ selectedUnit.code || t('Common:None') }}</dd>
            <dt>{{ t('Organization:Field:Manager') }}</dt>
            <dd>{{ userLabel(selectedUnit.managerUserId) || t('Common:None') }}</dd>
            <dt>{{ t('Organization:Field:Order') }}</dt>
            <dd>{{ selectedUnit.sortOrder ?? 0 }}</dd>
            <dt>{{ t('Common:Status') }}</dt>
            <dd>
              <span class="po-pill" :class="selectedUnit.isActive ? 'po-pill--mint' : 'po-pill--outline'">
                {{ selectedUnit.isActive ? t('Common:Enabled') : t('Common:Disabled') }}
              </span>
            </dd>
          </dl>

          <h4 class="authz-member-title">{{ t('Organization:Members') }}</h4>
          <DataTable
            size="small"
            :columns="memberColumns"
            :data="members"
            :loading="memberLoading"
            :row-key="memberRowKey"
          />
        </template>
      </section>
    </div>

    <n-modal
      v-model:show="editorVisible"
      preset="card"
      :title="editorTitle"
      :style="editorModalStyle"
      :segmented="{ content: 'soft' }"
      :mask-closable="false"
    >
      <n-form ref="unitFormRef" :model="form" :rules="unitRules" label-placement="top">
        <div class="po-form-grid">
          <n-form-item :label="t('Organization:Field:Name')" path="name">
            <n-input v-model:value="form.name" :placeholder="t('Organization:NamePlaceholder')" />
          </n-form-item>
          <n-form-item v-if="!editingId" :label="t('Organization:Field:Code')" path="code">
            <n-input v-model:value="form.code" :placeholder="t('Organization:CodePlaceholder')" />
          </n-form-item>
          <n-form-item :label="t('Organization:Field:Manager')">
            <n-select
              v-model:value="form.managerUserId"
              clearable
              filterable
              :options="userOptions"
              :loading="userLoading"
              :placeholder="t('Organization:ManagerPlaceholder')"
              @update:show="handleUserSelectShow"
            />
          </n-form-item>
          <n-form-item :label="t('Organization:Field:Order')">
            <n-input-number v-model:value="form.sortOrder" :min="0" style="width: 100%" />
          </n-form-item>
          <n-form-item v-if="editingId" :label="t('Organization:Field:Active')">
            <n-switch v-model:value="form.isActive">
              <template #checked>{{ t('Common:Enabled') }}</template>
              <template #unchecked>{{ t('Common:Disabled') }}</template>
            </n-switch>
          </n-form-item>
        </div>
      </n-form>
      <template #footer>
        <n-space justify="end">
          <n-button secondary @click="editorVisible = false">{{ t('Common:Cancel') }}</n-button>
          <n-button type="primary" :loading="submitting" @click="submitEditor">
            {{ t('Common:Confirm') }}
          </n-button>
        </n-space>
      </template>
    </n-modal>

    <n-modal
      v-model:show="addMemberVisible"
      preset="card"
      :title="t('Organization:AddMember')"
      :style="editorModalStyle"
      :segmented="{ content: 'soft' }"
      :mask-closable="false"
    >
      <n-form ref="memberFormRef" :model="memberForm" :rules="memberRules" label-placement="top">
        <div class="po-form-grid po-form-grid--full">
          <n-form-item :label="t('Organization:Field:Member')" path="userId">
            <n-select
              v-model:value="memberForm.userId"
              filterable
              :options="userOptions"
              :loading="userLoading"
              :placeholder="t('Organization:MemberPlaceholder')"
              @update:show="handleUserSelectShow"
            />
          </n-form-item>
          <n-form-item :label="t('Organization:Field:PositionName')">
            <n-input
              v-model:value="memberForm.positionName"
              :placeholder="t('Organization:PositionPlaceholder')"
            />
          </n-form-item>
          <n-form-item :label="t('Organization:Field:IsPrimary')">
            <n-switch v-model:value="memberForm.isPrimary">
              <template #checked>{{ t('Common:Yes') }}</template>
              <template #unchecked>{{ t('Common:No') }}</template>
            </n-switch>
          </n-form-item>
        </div>
      </n-form>
      <template #footer>
        <n-space justify="end">
          <n-button secondary @click="addMemberVisible = false">{{ t('Common:Cancel') }}</n-button>
          <n-button type="primary" :loading="submitting" @click="submitMember">
            {{ t('Common:Confirm') }}
          </n-button>
        </n-space>
      </template>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, h, nextTick, onMounted, reactive, ref } from 'vue'
import { NIcon, NButton, type DataTableColumns, type FormInst, type FormRules, type TreeOption } from 'naive-ui'
import { BusinessOutline, FolderOutline, SearchOutline } from '@vicons/ionicons5'
import { getSystemUser } from '@/api/system'
import { DataTable } from '@/components/Table'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { useUserStore } from '@/stores/user'
import { organizationApi } from '../../api/organization'
import type { OrganizationMemberDto, OrganizationUnitDto } from '../../api/organization'
import { buildTree } from '../../utils'
import { collectAllPages } from '@/utils/pagination'
import { scrollToFirstFormError } from '@/utils/formErrors'
import type { PageData } from '@/types/api'

const userStore = useUserStore()

const canManage = computed(() => userStore.hasPermission('system:organization:manage'))

const units = ref<OrganizationUnitDto[]>([])
const loading = ref(false)
const memberLoading = ref(false)
const submitting = ref(false)
const keyword = ref('')

const selectedKeys = ref<string[]>([])
const expandedKeys = ref<string[]>([])
let expansionInitialized = false
let selectionInitialized = false
const selectedUnit = ref<OrganizationUnitDto | null>(null)
const members = ref<OrganizationMemberDto[]>([])

const editorVisible = ref(false)
const addMemberVisible = ref(false)
const editingId = ref('')
const parentId = ref<string | null>(null)
const form = reactive({
  name: '',
  code: '',
  managerUserId: null as string | null,
  sortOrder: 0,
  isActive: true,
})
const memberForm = reactive({ userId: null as string | null, positionName: '', isPrimary: false })
const unitFormRef = ref<FormInst | null>(null)
const memberFormRef = ref<FormInst | null>(null)
const unitRules = computed<FormRules>(() => ({
  name: [{ required: true, message: t('Organization:NameRequired'), trigger: 'blur' }],
  ...(editingId.value
    ? {}
    : { code: [{ required: true, message: t('Organization:CodeRequired'), trigger: 'blur' }] }),
}))
const memberRules: FormRules = {
  userId: [{ required: true, message: t('Organization:MemberRequired'), trigger: 'blur' }],
}

const userOptions = ref<{ label: string; value: string }[]>([])
const userLoading = ref(false)

// 弹窗样式
const editorModalStyle = { width: 'min(520px, 94vw)', maxHeight: '88vh' }

// 与树形列表保持一致的节点高度，避免组织树每项过于紧凑
const treeThemeOverrides = { nodeHeight: '40px' }

interface TreeOptionWithUnit extends TreeOption {
  unit: OrganizationUnitDto
}

/** 扁平组织列表按 parentId 组装为 n-tree 数据（复用授权模块的 buildTree）。 */
const treeData = computed<TreeOptionWithUnit[]>(() => {
  const map = (
    nodes: (OrganizationUnitDto & { children?: OrganizationUnitDto[] })[],
  ): TreeOptionWithUnit[] =>
    nodes.map((node) => ({
      key: node.id ?? '',
      label: node.name ?? '',
      unit: node,
      prefix: () =>
        h(NIcon, {
          component: node.children?.length ? FolderOutline : BusinessOutline,
          size: 16,
          class: 'authz-tree-icon',
        }),
      children: node.children?.length ? map(node.children) : undefined,
    }))

  const normalized = units.value.map((unit) => ({
    ...unit,
    id: unit.id ?? '',
    parentId: unit.parentId ?? null,
  }))

  return map(buildTree(normalized))
})

/** 按 parentId 回溯出从根到当前组织的名称层级，用于详情标题下的面包屑。 */
const unitTrail = computed<OrganizationUnitDto[]>(() => {
  const unit = selectedUnit.value
  if (!unit) return []

  const byId = new Map(units.value.map((item) => [item.id ?? '', item]))
  const trail: OrganizationUnitDto[] = []
  let current: OrganizationUnitDto | undefined = unit
  while (current) {
    trail.unshift(current)
    current = current.parentId ? byId.get(current.parentId) : undefined
  }
  return trail
})

const userLabel = (userId?: string | null) =>
  userId
    ? (userOptions.value.find((option) => option.value === userId)?.label ?? userId.slice(0, 8))
    : ''

const memberRowKey = (row: OrganizationMemberDto) => row.id ?? ''

const memberColumns = computed<DataTableColumns<OrganizationMemberDto>>(() => [
  { title: t('Organization:Field:Member'), key: 'userId', render: (row) => userLabel(row.userId) },
  {
    title: t('Organization:Field:PositionName'),
    key: 'positionName',
    render: (row) => row.positionName || t('Common:None'),
  },
  {
    title: t('Organization:Field:IsPrimary'),
    key: 'isPrimary',
    width: 110,
    render: (row) =>
      h(
        'span',
        { class: ['po-pill', row.isPrimary ? 'po-pill--accent' : 'po-pill--outline'] },
        row.isPrimary ? t('Common:Yes') : t('Common:No'),
      ),
  },
  {
    title: t('Common:Actions'),
    key: 'actions',
    width: 100,
    render: (row) =>
      h(
        NButton,
        { size: 'small', secondary: true, type: 'error', onClick: () => removeMember(row) },
        { default: () => t('Common:Delete') },
      ),
  },
])

const editorTitle = computed(() =>
  editingId.value ? t('Organization:EditTitle') : t('Organization:CreateTitle'),
)

type UserOptionSource = {
  id?: string
  nickName?: string
  name?: string
  userName?: string
}

const loadUsers = async (force = false) => {
  if (!force && userOptions.value.length) return
  if (userLoading.value) return

  userLoading.value = true
  try {
    const users = await collectAllPages<UserOptionSource>(async (skipCount, maxResultCount) => {
      const response = await getSystemUser({
        query: { SkipCount: skipCount, MaxResultCount: maxResultCount },
        throwOnError: true,
      })
      return (response.data ?? { items: [], totalCount: 0 }) as PageData<UserOptionSource>
    })
    userOptions.value = users
      .filter((user) => Boolean(user.id))
      .map((user) => ({
        value: user.id as string,
        label: user.nickName || user.name || user.userName || (user.id as string).slice(0, 8),
      }))
  } catch {
    // 刷新失败时保留上一次成功加载的选项，避免下拉框瞬间清空
  } finally {
    userLoading.value = false
  }
}

/** 用户下拉框每次展开时重新拉取用户列表，避免选项过期。 */
const handleUserSelectShow = (show: boolean) => {
  if (show) void loadUsers(true)
}

const selectUnit = (unit: OrganizationUnitDto) => {
  const unitId = unit.id
  if (!unitId) return
  selectedKeys.value = [unitId]
  selectedUnit.value = unit
  members.value = []
  void loadMembers(unitId)
}

const loadTree = async (keepSelection = true) => {
  loading.value = true
  try {
    units.value = await organizationApi.getTree()

    const expandableKeys = collectExpandableKeys(treeData.value)
    if (!expansionInitialized) {
      expandedKeys.value = expandableKeys
      expansionInitialized = true
    } else {
      const available = new Set(expandableKeys)
      expandedKeys.value = expandedKeys.value.filter((key) => available.has(key))
    }

    const currentId = keepSelection ? selectedUnit.value?.id : ''
    if (!currentId) {
      if (!selectionInitialized) {
        selectionInitialized = true
        if (canManage.value) {
          const firstRootId = treeData.value[0]?.key
          const firstRoot = units.value.find((unit) => unit.id === firstRootId)
          if (firstRoot) selectUnit(firstRoot)
        }
      }
      return
    }

    const refreshed = units.value.find((unit) => unit.id === currentId) ?? null
    selectedUnit.value = refreshed
    if (!refreshed) {
      selectedKeys.value = []
      members.value = []
    }
  } catch {
    useDiscreteApi.message.error(t('Organization:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const loadMembers = async (organizationUnitId: string) => {
  memberLoading.value = true
  try {
    members.value = await organizationApi.members(organizationUnitId)
  } catch {
    useDiscreteApi.message.error(t('Organization:MemberLoadFailed'))
  } finally {
    memberLoading.value = false
  }
}

const handleSelect = (keys: Array<string | number>) => {
  const key = keys.length ? String(keys[0]) : ''
  if (!key) return
  selectedKeys.value = [key]
  selectedUnit.value = units.value.find((unit) => unit.id === key) ?? null
  members.value = []

  void loadMembers(key)
}

const collectExpandableKeys = (nodes: TreeOptionWithUnit[]): string[] =>
  nodes.flatMap((node) => {
    const children = (node.children ?? []) as TreeOptionWithUnit[]
    return children.length ? [String(node.key), ...collectExpandableKeys(children)] : []
  })

const handleExpandedKeysChange = (keys: Array<string | number>) => {
  expandedKeys.value = keys.map(String)
}

const toggleExpanded = (key: string) => {
  expandedKeys.value = expandedKeys.value.includes(key)
    ? expandedKeys.value.filter((item) => item !== key)
    : [...expandedKeys.value, key]
}

const treeNodeProps = ({ option }: { option: TreeOption }) => ({
  class: selectedKeys.value.includes(String(option.key ?? '')) ? 'authz-tree-node--active' : '',
  onDblclick: (event: MouseEvent) => {
    if ((event.target as HTMLElement | null)?.closest('.n-tree-node-switcher')) return
    const treeOption = option as TreeOptionWithUnit
    if (!treeOption.children?.length) return
    toggleExpanded(String(option.key ?? ''))
  },
})

const openCreate = (parent: OrganizationUnitDto | null) => {
  editingId.value = ''
  parentId.value = parent?.id ?? null
  form.name = ''
  form.code = ''
  form.managerUserId = null
  form.sortOrder = 0
  form.isActive = true
  editorVisible.value = true
  void nextTick(() => unitFormRef.value?.restoreValidation())
  void loadUsers()
}

const startEdit = () => {
  if (!selectedUnit.value) return

  editingId.value = selectedUnit.value.id ?? ''
  parentId.value = selectedUnit.value.parentId ?? null
  form.name = selectedUnit.value.name ?? ''
  form.code = selectedUnit.value.code ?? ''
  form.managerUserId = selectedUnit.value.managerUserId ?? null
  form.sortOrder = selectedUnit.value.sortOrder ?? 0
  form.isActive = selectedUnit.value.isActive ?? true
  editorVisible.value = true
  void nextTick(() => unitFormRef.value?.restoreValidation())
  void loadUsers()
}

const submitEditor = async () => {
  try {
    await unitFormRef.value?.validate()
  } catch {
    void scrollToFirstFormError(unitFormRef.value)
    return
  }

  submitting.value = true
  try {
    if (editingId.value) {
      await organizationApi.update(editingId.value, {
        name: form.name.trim(),
        managerUserId: form.managerUserId,
        sortOrder: form.sortOrder,
        isActive: form.isActive,
      })
    } else {
      await organizationApi.create({
        parentId: parentId.value,
        code: form.code.trim(),
        name: form.name.trim(),
        managerUserId: form.managerUserId,
        sortOrder: form.sortOrder,
      })
    }

    editorVisible.value = false
    useDiscreteApi.message.success(t('Organization:Saved'))
    await loadTree()
  } catch {
    useDiscreteApi.message.error(t('Organization:SaveFailed'))
  } finally {
    submitting.value = false
  }
}

const openAddMember = () => {
  memberForm.userId = null
  memberForm.positionName = ''
  memberForm.isPrimary = false
  addMemberVisible.value = true
  void nextTick(() => memberFormRef.value?.restoreValidation())
  void loadUsers()
}

const submitMember = async () => {
  const unitId = selectedUnit.value?.id
  if (!unitId) {
    return
  }
  try {
    await memberFormRef.value?.validate()
  } catch {
    void scrollToFirstFormError(memberFormRef.value)
    return
  }

  submitting.value = true
  try {
    await organizationApi.addMember({
      organizationUnitId: unitId,
      userId: memberForm.userId as string,
      positionName: memberForm.positionName.trim() || null,
      isPrimary: memberForm.isPrimary,
    })
    addMemberVisible.value = false
    useDiscreteApi.message.success(t('Organization:Saved'))
    await loadMembers(unitId)
  } catch {
    useDiscreteApi.message.error(t('Organization:MemberSaveFailed'))
  } finally {
    submitting.value = false
  }
}

const removeMember = (row: OrganizationMemberDto) => {
  const membershipId = row.id
  const unitId = selectedUnit.value?.id
  if (!membershipId || !unitId) return

  useDiscreteApi.dialog.warning({
    title: t('Organization:RemoveMember'),
    content: t('Organization:RemoveMemberConfirm', { name: userLabel(row.userId) }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      try {
        await organizationApi.removeMember(membershipId)
        useDiscreteApi.message.success(t('Organization:Removed'))
        await loadMembers(unitId)
      } catch {
        useDiscreteApi.message.error(t('Organization:RemoveFailed'))
      }
    },
  })
}

const removeUnit = () => {
  const unitId = selectedUnit.value?.id
  if (!unitId) return

  useDiscreteApi.dialog.error({
    title: t('Common:Delete'),
    content: t('Organization:RemoveUnitConfirm', { name: selectedUnit.value?.name ?? '' }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      try {
        await organizationApi.remove(unitId)
        selectedUnit.value = null
        selectedKeys.value = []
        members.value = []
        useDiscreteApi.message.success(t('Organization:Removed'))
        await loadTree(false)
      } catch {
        useDiscreteApi.message.error(t('Organization:RemoveFailed'))
      }
    },
  })
}

onMounted(() => {
  void loadTree(false)
  void loadUsers()
})
</script>

<style scoped>
.authz-org-grid {
  grid-template-columns: minmax(260px, 340px) minmax(0, 1fr);
  align-items: start;
}

.authz-tree-panel,
.authz-detail-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
}

.authz-tree-search {
  width: 100%;
  max-width: none;
  min-height: var(--po-density-toolbar-control-sm-h);
  flex: 0 0 auto;
}

.authz-tree-search input {
  min-height: calc(var(--po-density-toolbar-control-sm-h) - 4px);
}

.authz-tree-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.authz-tree {
  --n-line-color: var(--po-line);

  flex: 1 1 auto;
  min-height: 0;
  max-height: 62vh;
  overflow: auto;
}

.authz-tree :deep(.authz-tree-icon) {
  margin-right: 4px;
  color: var(--po-ink-soft);
}

.authz-tree :deep(.authz-tree-node--active) {
  background: var(--po-accent-soft);
  border-radius: 8px;
  box-shadow: inset 3px 0 0 var(--po-accent);
}

.authz-detail-empty {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 10px;
  padding: 20px 4px;
}

.authz-detail-head {
  align-items: flex-start;
}

.authz-detail-head h3 {
  margin: 0 0 4px;
  font-size: 18px;
  font-weight: var(--po-weight-display);
}

.authz-detail-path {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  color: var(--po-ink-soft);
  font-size: 12.5px;
}

.authz-detail-path__sep {
  color: var(--po-ink-faint);
}

.authz-detail-path__current {
  color: var(--po-ink);
  font-weight: var(--po-weight-strong);
}

.authz-detail-kv {
  padding: 12px 14px;
  border: 1px dashed var(--po-line);
  border-radius: var(--po-r-md, 10px);
}

.authz-member-title {
  margin: 4px 0 0;
  font-size: 14px;
  font-weight: var(--po-weight-strong);
}

@media (max-width: 1080px) {
  .authz-org-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .authz-tree {
    max-height: 40vh;
  }
}
</style>

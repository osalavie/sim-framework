<template>
  <div class="po-page po-page--fill">
    <section class="po-toolbar">
      <label class="po-search-box">
        <n-icon :component="SearchOutline" />
        <input
          v-model="searchKeyword"
          type="search"
          :placeholder="t('Approval:Definition:SearchPlaceholder')"
          :aria-label="t('Approval:Definition:SearchPlaceholder')"
        />
      </label>
      <span class="po-toolbar__spacer"></span>
      <div class="po-toolbar__actions">
        <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
        <n-button v-if="canManage" type="primary" @click="openCreate">
          {{ t('Approval:Definition:Create') }}
        </n-button>
      </div>
    </section>

    <DataTable
      class="definition-table"
      framed
      fill
      flex-height
      auto-view-mode
      :columns="columns"
      :data="filteredItems"
      :loading="loading"
      :row-key="rowKey"
    >
      <template #card>
        <n-spin :show="loading">
          <div class="po-stack">
            <article v-for="definition in filteredItems" :key="definition.id" class="po-card">
              <div class="card-meta">
                <ApprovalStatusTag kind="flow" :status="definition.status" />
                <span class="po-pill">v{{ definition.version ?? 1 }}</span>
                <span class="po-pill po-pill--sky">{{ definition.code }}</span>
              </div>
              <h3>{{ definition.name }}</h3>
              <p class="card-line">{{ definition.description || t('Common:None') }}</p>
              <p class="card-line">
                {{ businessLabel(definition.businessType) || t('Common:None') }} ·
                {{ t('Approval:Field:NodeCount', { count: definition.nodeCount ?? 0 }) }} ·
                {{ formatDate(definition.creationTime || '') }}
              </p>
              <div class="card-actions">
                <n-button
                  v-for="action in rowActions(definition)"
                  :key="action.key"
                  size="small"
                  :type="action.type"
                  :secondary="action.type !== 'error'"
                  @click="action.run"
                >
                  {{ action.label }}
                </n-button>
              </div>
            </article>
          </div>
          <n-empty v-if="!filteredItems.length && !loading" :description="t('Approval:Definition:Empty')" />
        </n-spin>
      </template>
    </DataTable>

    <n-modal
      v-model:show="showCreate"
      preset="card"
      style="width: min(520px, 92vw)"
      :title="t('Approval:Definition:CreateTitle')"
      :mask-closable="false"
    >
      <n-form ref="createFormRef" :model="form" :rules="createRules" label-placement="top">
        <n-form-item :label="t('Approval:Field:Code')" path="code">
          <n-input
            v-model:value="form.code"
            :placeholder="t('Approval:Definition:CodePlaceholder')"
          />
        </n-form-item>
        <n-form-item :label="t('Approval:Field:Name')" path="name">
          <n-input v-model:value="form.name" :placeholder="t('Approval:Definition:NamePlaceholder')" />
        </n-form-item>
        <n-form-item :label="t('Approval:Field:BusinessType')">
          <n-select
            v-model:value="form.businessType"
            clearable
            :options="businessTypeOptions"
            :placeholder="t('Approval:Definition:BusinessTypePlaceholder')"
          />
        </n-form-item>
        <n-form-item :label="t('Approval:Field:Description')">
          <n-input
            v-model:value="form.description"
            type="textarea"
            :autosize="{ minRows: 2, maxRows: 4 }"
          />
        </n-form-item>
      </n-form>
      <template #footer>
        <div class="modal-footer">
          <n-button secondary @click="showCreate = false">{{ t('Common:Cancel') }}</n-button>
          <n-button type="primary" :loading="submitting" @click="submitCreate">
            {{ t('Common:Confirm') }}
          </n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, h, nextTick, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { NButton, NTag, type DataTableColumns, type FormInst, type FormRules } from 'naive-ui'
import { SearchOutline } from '@vicons/ionicons5'
import { DataTable } from '@/components/Table'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { formatDate } from '@/utils/date'
import { useAuditNames } from '@/components/AuditInfo/useAuditNames'
import { createAuditColumns } from '@/utils/auditColumns'
import { scrollToFirstFormError } from '@/utils/formErrors'
import { useUserStore } from '@/stores/user'
import { approvalDefinitionApi, type ApprovalFlowDefinitionListDto } from '../../api'
import ApprovalStatusTag from '../../components/ApprovalStatusTag.vue'
import { useBusinessTypes } from '../../composables/useApprovalCatalogs'
import {
  createInitialFlowCanvas,
  flowEdgesToSaveDtos,
  flowNodesToSaveDtos,
  formatFlowLayout,
  positionedFlowNodes,
} from '../../utils/flowGraph'

const router = useRouter()
const userStore = useUserStore()

const canManage = computed(() => userStore.hasPermission('approval:definition:manage'))

const items = ref<ApprovalFlowDefinitionListDto[]>([])
const { resolveName, resolveAccount } = useAuditNames(() =>
  items.value.flatMap((row) => [row.creatorId, row.lastModifierId]),
)
const loading = ref(false)
const searchKeyword = ref('')
const showCreate = ref(false)
const submitting = ref(false)
const form = reactive({ code: '', name: '', businessType: null as string | null, description: '' })
const createFormRef = ref<FormInst | null>(null)
const createRules: FormRules = {
  code: [{ required: true, message: t('Approval:Definition:RequiredHint'), trigger: 'blur' }],
  name: [{ required: true, message: t('Approval:Definition:RequiredHint'), trigger: 'blur' }],
}

const filteredItems = computed(() => {
  const keyword = searchKeyword.value.trim().toLowerCase()
  if (!keyword) return items.value
  return items.value.filter(
    (item) =>
      item.name?.toLowerCase().includes(keyword) ||
      item.code?.toLowerCase().includes(keyword) ||
      item.description?.toLowerCase().includes(keyword),
  )
})

const { options: businessTypeOptions, label: businessLabel, ensureLoaded } = useBusinessTypes()

const load = async () => {
  loading.value = true
  try {
    items.value = await approvalDefinitionApi.getList()
  } catch {
    useDiscreteApi.message.error(t('Approval:Definition:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  form.code = ''
  form.name = ''
  form.businessType = null
  form.description = ''
  showCreate.value = true
  void nextTick(() => createFormRef.value?.restoreValidation())
  void ensureLoaded()
}

const submitCreate = async () => {
  try {
    await createFormRef.value?.validate()
  } catch {
    void scrollToFirstFormError(createFormRef.value)
    return
  }
  const code = form.code.trim()
  const name = form.name.trim()

  submitting.value = true
  try {
    // 服务端保存期要求至少一个审批节点，空图无法建草稿，因此新建时即给出可运行的最小图。
    const canvas = createInitialFlowCanvas()
    const positioned = positionedFlowNodes(canvas)
    const created = await approvalDefinitionApi.create({
      code,
      name,
      businessType: form.businessType,
      description: form.description.trim() || null,
      nodes: flowNodesToSaveDtos(positioned),
      edges: flowEdgesToSaveDtos(canvas.edges),
      layoutJson: formatFlowLayout(positioned),
    })
    showCreate.value = false
    useDiscreteApi.message.success(t('Approval:Definition:Created'))
    if (created.id) {
      void router.push({
        name: 'ApprovalDefinitionEdit',
        params: { definitionId: created.id },
      })
      return
    }
    await load()
  } catch {
    useDiscreteApi.message.error(t('Approval:Definition:CreateFailed'))
  } finally {
    submitting.value = false
  }
}

const openEditor = (definition: ApprovalFlowDefinitionListDto) => {
  if (!definition.id) return
  void router.push({
    name: 'ApprovalDefinitionEdit',
    params: { definitionId: definition.id },
  })
}

const publish = (definition: ApprovalFlowDefinitionListDto) => {
  useDiscreteApi.dialog.warning({
    title: t('Approval:Definition:Publish'),
    content: t('Approval:Definition:PublishConfirm', { name: definition.name ?? '' }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      if (!definition.id) return
      try {
        await approvalDefinitionApi.publish(definition.id)
        useDiscreteApi.message.success(t('Approval:Definition:Published'))
        await load()
      } catch {
        useDiscreteApi.message.error(t('Approval:Definition:PublishFailed'))
      }
    },
  })
}

const disable = (definition: ApprovalFlowDefinitionListDto) => {
  useDiscreteApi.dialog.warning({
    title: t('Approval:Definition:Disable'),
    content: t('Approval:Definition:DisableConfirm', { name: definition.name ?? '' }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      if (!definition.id) return
      try {
        await approvalDefinitionApi.disable(definition.id)
        useDiscreteApi.message.success(t('Approval:Definition:Disabled'))
        await load()
      } catch {
        useDiscreteApi.message.error(t('Approval:Definition:DisableFailed'))
      }
    },
  })
}

const newVersion = (definition: ApprovalFlowDefinitionListDto) => {
  useDiscreteApi.dialog.warning({
    title: t('Approval:Definition:NewVersion'),
    content: t('Approval:Definition:NewVersionConfirm', { name: definition.name ?? '' }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      if (!definition.id) return
      try {
        const draft = await approvalDefinitionApi.newVersion(definition.id)
        useDiscreteApi.message.success(t('Approval:Definition:VersionCreated'))
        if (draft.id) {
          openEditor(draft)
          return
        }
        await load()
      } catch {
        useDiscreteApi.message.error(t('Approval:Definition:NewVersionFailed'))
      }
    },
  })
}

const remove = (definition: ApprovalFlowDefinitionListDto) => {
  useDiscreteApi.dialog.error({
    title: t('Approval:Definition:Remove'),
    content: t('Approval:Definition:RemoveConfirm', { name: definition.name ?? '' }),
    positiveText: t('Common:Confirm'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      if (!definition.id) return
      try {
        await approvalDefinitionApi.remove(definition.id)
        useDiscreteApi.message.success(t('Approval:Definition:Removed'))
        await load()
      } catch {
        useDiscreteApi.message.error(t('Approval:Definition:RemoveFailed'))
      }
    },
  })
}

interface RowAction {
  key: string
  label: string
  type?: 'primary' | 'error'
  run: () => void
}

/** 行内操作按状态收敛，与后端仅允许草稿修改 / 发布 / 删除的规则保持一致。 */
const rowActions = (definition: ApprovalFlowDefinitionListDto): RowAction[] => {
  if (!canManage.value) return []

  const actions: RowAction[] = []
  if (definition.status === 'Draft') {
    actions.push({ key: 'edit', label: t('Common:Edit'), run: () => openEditor(definition) })
    actions.push({ key: 'publish', label: t('Approval:Definition:Publish'), run: () => publish(definition) })
    actions.push({ key: 'remove', label: t('Common:Delete'), type: 'error', run: () => remove(definition) })
    return actions
  }

  if (definition.status === 'Published') {
    actions.push({ key: 'disable', label: t('Approval:Definition:Disable'), run: () => disable(definition) })
  }
  actions.push({ key: 'version', label: t('Approval:Definition:NewVersion'), run: () => newVersion(definition) })
  return actions
}

const rowKey = (row: ApprovalFlowDefinitionListDto) => row.id ?? ''

const columns = computed<DataTableColumns<ApprovalFlowDefinitionListDto>>(() => [
  {
    title: t('Approval:Field:Name'),
    key: 'name',
    minWidth: 180,
    resizable: true,
    render: (row) => row.name ?? '',
  },
  {
    title: t('Approval:Field:Code'),
    key: 'code',
    width: 190,
    resizable: true,
    render: (row) => h('code', row.code ?? ''),
  },
  {
    title: t('Approval:Field:BusinessType'),
    key: 'businessType',
    width: 150,
    resizable: true,
    render: (row) => businessLabel(row.businessType) || t('Common:None'),
  },
  {
    title: t('Approval:Field:Version'),
    key: 'version',
    width: 90,
    resizable: true,
    render: (row) => h(NTag, { size: 'small', bordered: false }, { default: () => `v${row.version ?? 1}` }),
  },
  {
    title: t('Common:Status'),
    key: 'status',
    width: 110,
    resizable: true,
    render: (row) => h(ApprovalStatusTag, { kind: 'flow', status: row.status }),
  },
  {
    title: t('Approval:Field:NodeCountLabel'),
    key: 'nodeCount',
    width: 100,
    resizable: true,
    render: (row) => String(row.nodeCount ?? 0),
  },
  ...createAuditColumns<ApprovalFlowDefinitionListDto>({ resolveName, resolveAccount }),
  {
    title: t('Common:Actions'),
    key: 'actions',
    width: 240,
    fixed: 'right',
    render: (row) =>
      h(
        'div',
        { class: 'po-cell-actions' },
        rowActions(row).map((action) =>
          h(
            NButton,
            {
              size: 'small',
              type: action.type,
              secondary: action.type !== 'error',
              onClick: action.run,
            },
            { default: () => action.label },
          ),
        ),
      ),
  },
])

onMounted(() => {
  void load()
  void ensureLoaded()
})
</script>

<style scoped>
.definition-table :deep(.title-cell) {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.definition-table :deep(.title-cell strong) {
  color: var(--po-ink);
  font-weight: var(--po-weight-bold);
}

.definition-table :deep(.title-cell code) {
  color: var(--po-ink-faint);
  font-family: var(--po-font-mono);
  font-size: 11px;
}

.card-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 7px;
}

.card-line {
  margin: 8px 0 0;
  color: var(--po-ink-soft);
  font-size: 13px;
  overflow-wrap: anywhere;
}

.card-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 9px;
  margin-top: 14px;
}

.po-stack h3 {
  margin: 8px 0 0;
  font-size: 16px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

@media (max-width: 640px) {
  .card-actions :deep(.n-button) {
    flex: 1;
  }
}
</style>

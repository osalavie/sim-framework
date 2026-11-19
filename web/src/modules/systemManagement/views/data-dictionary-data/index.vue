<template>
  <div class="po-page">
    <header class="po-page-head po-page-head--record">
      <div class="po-page-head__main">
        <h1>{{ dictionary?.displayName ?? t('DataDictionary:DataTitle') }}</h1>
      </div>
      <div class="po-page-head__actions">
        <n-button secondary @click="backToList">{{ t('DataDictionary:BackToList') }}</n-button>
      </div>
    </header>

    <div v-if="missingDictionary" class="po-note po-note--danger">
      <span>{{ t('DataDictionary:NotFound') }}</span>
    </div>

    <div class="po-toolbar">
      <n-input
        v-model:value="keyword"
        class="item-search"
        clearable
        :placeholder="t('DataDictionary:DataSearchPlaceholder')"
      />
      <span class="po-pill po-pill--outline">
        {{ t('DataDictionary:ItemCount', { count: filteredItems.length }) }}
      </span>
      <div class="po-toolbar__spacer" />
      <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
      <n-button type="primary" :disabled="!dictionary" @click="openCreate">
        {{ t('DataDictionary:CreateItem') }}
      </n-button>
    </div>

    <DataTable
      framed
      auto-view-mode
      :columns="columns"
      :data="filteredItems"
      :loading="loading"
      :row-key="rowKey"
    >
      <template #card>
        <n-spin :show="loading">
          <div class="item-cards">
            <article v-for="item in filteredItems" :key="item.id" class="po-card item-card">
              <div class="card-meta">
                <span class="po-pill po-pill--outline">{{ item.itemKey }}</span>
                <span
                  class="po-pill"
                  :class="item.enabled ? 'po-pill--mint po-pill--dot' : 'po-pill--outline'"
                >
                  {{ t(item.enabled ? 'Common:Enabled' : 'Common:Disabled') }}
                </span>
              </div>
              <h3>{{ item.itemName }}</h3>
              <code class="po-mono">{{ item.value }}</code>
              <p v-if="item.description">{{ item.description }}</p>
              <div class="card-actions">
                <n-button size="small" secondary @click="openEdit(item)">{{ t('Common:Edit') }}</n-button>
                <n-popconfirm @positive-click="removeItem(item.id)">
                  <template #trigger>
                    <n-button size="small" quaternary type="error">{{ t('Common:Delete') }}</n-button>
                  </template>
                  {{ t('DataDictionary:DeleteItemConfirm', { name: item.itemName }) }}
                </n-popconfirm>
              </div>
            </article>
          </div>
          <n-empty v-if="!filteredItems.length && !loading" :description="emptyDescription" />
        </n-spin>
      </template>
      <template #empty>
        <n-empty :description="emptyDescription" />
      </template>
    </DataTable>

    <n-modal v-model:show="showEditor" :mask-closable="!saving">
      <n-card
        class="editor-dialog"
        :title="editing ? t('DataDictionary:EditItem') : t('DataDictionary:CreateItem')"
        closable
        role="dialog"
        aria-modal="true"
        @close="showEditor = false"
      >
        <n-form ref="formRef" :model="form" :rules="rules" label-placement="top">
          <n-grid responsive="screen" cols="1 m:2" :x-gap="18">
            <n-form-item-gi :label="t('DataDictionary:ItemKey')" path="itemKey" required>
              <n-input
                v-model:value="form.itemKey"
                :disabled="Boolean(editing)"
                :maxlength="128"
                show-count
              />
            </n-form-item-gi>
            <n-form-item-gi :label="t('DataDictionary:ItemName')" path="itemName" required>
              <n-input v-model:value="form.itemName" :maxlength="256" show-count />
            </n-form-item-gi>
            <n-form-item-gi :label="t('DataDictionary:Value')" path="value" required :span="2">
              <n-input
                v-if="dictionary?.valueType === 0"
                v-model:value="form.value"
                :maxlength="1024"
                show-count
              >
                <template v-if="dictionary.valuePrefix" #prefix>{{ dictionary.valuePrefix }}</template>
                <template v-if="dictionary.valueSuffix" #suffix>{{ dictionary.valueSuffix }}</template>
              </n-input>
              <n-input-number v-else-if="dictionary?.valueType === 1" v-model:value="numberValue" class="value-number">
                <template v-if="dictionary.valuePrefix" #prefix>{{ dictionary.valuePrefix }}</template>
                <template v-if="dictionary.valueSuffix" #suffix>{{ dictionary.valueSuffix }}</template>
              </n-input-number>
              <n-date-picker
                v-else
                class="value-date"
                type="date"
                :formatted-value="form.value || null"
                value-format="yyyy-MM-dd"
                @update:formatted-value="form.value = $event || ''"
              />
            </n-form-item-gi>
            <n-form-item-gi :label="t('DataDictionary:Sort')">
              <n-input-number v-model:value="form.sort" :min="0" :precision="0" />
            </n-form-item-gi>
            <n-form-item-gi :label="t('DataDictionary:Enabled')">
              <n-switch v-model:value="form.enabled">
                <template #checked>{{ t('Common:Enabled') }}</template>
                <template #unchecked>{{ t('Common:Disabled') }}</template>
              </n-switch>
            </n-form-item-gi>
            <n-form-item-gi :label="t('DataDictionary:DescriptionField')" :span="2">
              <n-input v-model:value="form.description" type="textarea" :rows="3" :maxlength="1024" show-count />
            </n-form-item-gi>
          </n-grid>
        </n-form>
        <template #footer>
          <div class="dialog-actions">
            <n-button @click="showEditor = false">{{ t('Common:Cancel') }}</n-button>
            <n-button type="primary" :loading="saving" @click="save">{{ t('Common:Save') }}</n-button>
          </div>
        </template>
      </n-card>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, h, nextTick, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NButton, NTag, type DataTableColumns, type FormInst, type FormRules } from 'naive-ui'
import { DataTable } from '@/components/Table'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { scrollToFirstFormError } from '@/utils/formErrors'
import {
  dataDictionaryApi,
  dataDictionaryItemApi,
  type CreateDataDictionaryItem,
  type DataDictionary,
  type DataDictionaryItem,
  type UpdateDataDictionaryItem,
} from '@/modules/systemManagement/api/dataDictionary'

const newItemForm = (dictionaryId: string): CreateDataDictionaryItem => ({
  dictionaryId,
  itemKey: '',
  itemName: '',
  value: '',
  description: '',
  sort: 0,
  enabled: true,
})

const route = useRoute()
const router = useRouter()
const dictionary = ref<DataDictionary | null>(null)
const items = ref<DataDictionaryItem[]>([])
const loading = ref(false)
const saving = ref(false)
const showEditor = ref(false)
const editing = ref<DataDictionaryItem | null>(null)
const form = ref<CreateDataDictionaryItem>(newItemForm(''))
const formRef = ref<FormInst | null>(null)
const rules: FormRules = {
  itemKey: [{ required: true, message: t('DataDictionary:ItemRequired'), trigger: 'blur' }],
  itemName: [{ required: true, message: t('DataDictionary:ItemRequired'), trigger: 'blur' }],
  value: [{ required: true, message: t('DataDictionary:ItemRequired'), trigger: ['blur', 'change'] }],
}
const keyword = ref('')

const dictionaryId = computed(() => String(route.params.dictionaryId ?? ''))
const missingDictionary = computed(() => !loading.value && !dictionary.value)
const emptyDescription = computed(() =>
  keyword.value ? t('DataDictionary:NoMatchingItems') : t('DataDictionary:ItemEmpty'),
)
const filteredItems = computed(() => {
  const term = keyword.value.trim().toLowerCase()
  if (!term) return items.value
  return items.value.filter((item) =>
    [item.itemKey, item.itemName, item.value, item.description].some((value) =>
      (value ?? '').toLowerCase().includes(term),
    ),
  )
})
const numberValue = computed({
  get: () => (form.value.value === '' ? null : Number(form.value.value)),
  set: (value) => {
    form.value.value = value === null ? '' : String(value)
  },
})

const rowKey = (row: DataDictionaryItem) => row.id
const columns = computed<DataTableColumns<DataDictionaryItem>>(() => [
  { title: t('DataDictionary:ItemKey'), key: 'itemKey', minWidth: 160, ellipsis: { tooltip: true } },
  { title: t('DataDictionary:ItemName'), key: 'itemName', minWidth: 170, ellipsis: { tooltip: true } },
  { title: t('DataDictionary:Value'), key: 'value', minWidth: 170, ellipsis: { tooltip: true } },
  { title: t('DataDictionary:Sort'), key: 'sort', width: 84 },
  { title: t('DataDictionary:DescriptionField'), key: 'description', minWidth: 200, ellipsis: { tooltip: true } },
  {
    title: t('DataDictionary:Enabled'),
    key: 'enabled',
    width: 104,
    render: (row) => h(
      NTag,
      { size: 'small', bordered: false, type: row.enabled ? 'success' : 'default' },
      { default: () => t(row.enabled ? 'Common:Enabled' : 'Common:Disabled') },
    ),
  },
  {
    title: t('Common:Actions'),
    key: 'actions',
    width: 160,
    render: (row) => h('div', { class: 'table-actions' }, [
      h(NButton, {
        size: 'small',
        secondary: true,
        onClick: () => openEdit(row),
      }, { default: () => t('Common:Edit') }),
      h(NButton, {
        size: 'small',
        quaternary: true,
        type: 'error',
        onClick: () => removeItem(row.id),
      }, { default: () => t('Common:Delete') }),
    ]),
  },
])

const backToList = () => {
  void router.push({ name: 'DataDictionaryManagement' })
}

const load = async () => {
  if (!dictionaryId.value) return
  loading.value = true
  try {
    const dictionaries = await dataDictionaryApi.getList()
    dictionary.value = dictionaries.find((item) => item.id === dictionaryId.value) ?? null
    items.value = await dataDictionaryItemApi.getList(dictionaryId.value)
  } catch {
    useDiscreteApi.message.error(t('DataDictionary:ItemLoadFailed'))
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  if (!dictionary.value) return
  editing.value = null
  form.value = newItemForm(dictionary.value.id)
  showEditor.value = true
  void nextTick(() => formRef.value?.restoreValidation())
}

const openEdit = (item: DataDictionaryItem) => {
  editing.value = item
  form.value = { ...item }
  showEditor.value = true
  void nextTick(() => formRef.value?.restoreValidation())
}

const save = async () => {
  try {
    await formRef.value?.validate()
  } catch {
    void scrollToFirstFormError(formRef.value)
    return
  }

  saving.value = true
  try {
    if (editing.value) {
      const input: UpdateDataDictionaryItem = {
        itemName: form.value.itemName,
        value: form.value.value,
        description: form.value.description,
        sort: form.value.sort,
        enabled: form.value.enabled,
      }
      const updated = await dataDictionaryItemApi.update(editing.value.id, input)
      items.value = items.value.map((item) => (item.id === updated.id ? updated : item))
    } else {
      const created = await dataDictionaryItemApi.create(form.value)
      items.value = [...items.value, created]
    }
    showEditor.value = false
    useDiscreteApi.message.success(t('DataDictionary:ItemSaved'))
  } catch {
    useDiscreteApi.message.error(t('DataDictionary:ItemSaveFailed'))
  } finally {
    saving.value = false
  }
}

const removeItem = async (id: string) => {
  try {
    await dataDictionaryItemApi.delete(id)
    items.value = items.value.filter((item) => item.id !== id)
    useDiscreteApi.message.success(t('DataDictionary:ItemDeleted'))
  } catch {
    useDiscreteApi.message.error(t('DataDictionary:ItemDeleteFailed'))
  }
}

watch(dictionaryId, () => void load(), { immediate: true })
</script>

<style scoped>
.item-search {
  width: min(320px, 100%);
}

:deep(.table-actions) {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
  flex-wrap: nowrap;
}

.item-cards {
  display: grid;
  gap: 14px;
}

.item-card h3 {
  margin: 2px 0 0;
  font-size: 17px;
}

.item-card code {
  overflow-wrap: anywhere;
  color: var(--po-ink-soft);
  font-size: 12px;
}

.item-card p {
  margin: 0;
  color: var(--po-ink-soft);
  font-size: 13px;
}

.card-meta,
.card-actions,
.dialog-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.card-actions {
  margin-top: 4px;
}

.dialog-actions {
  justify-content: flex-end;
}

.value-number,
.value-date {
  width: 100%;
}

.editor-dialog {
  width: min(860px, calc(100vw - 32px));
}

@media (max-width: 640px) {
  .po-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .item-search {
    width: 100%;
  }

  .po-toolbar :deep(.n-button) {
    width: 100%;
  }

  .po-page-head__actions :deep(.n-button),
  .card-actions :deep(.n-button) {
    flex: 1 1 auto;
  }
}
</style>

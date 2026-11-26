<template>
  <div class="po-page">
    <div class="po-toolbar">
      <n-input
        v-model:value="keyword"
        class="dictionary-search"
        clearable
        :placeholder="t('DataDictionary:SearchPlaceholder')"
      />
      <span class="po-pill po-pill--outline">
        {{ t('DataDictionary:FilteredCount', { count: filteredDictionaries.length }) }}
      </span>
      <div class="po-toolbar__spacer" />
      <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
      <n-button type="primary" @click="openCreate">{{ t('DataDictionary:Create') }}</n-button>
    </div>

    <DataTable
      framed
      auto-view-mode
      :columns="columns"
      :data="filteredDictionaries"
      :loading="loading"
      :row-key="rowKey"
    >
      <template #card>
        <n-spin :show="loading">
          <div class="dictionary-cards">
            <article
              v-for="dictionary in filteredDictionaries"
              :key="dictionary.id"
              class="po-card dictionary-card"
            >
              <div class="card-meta">
                <span class="po-pill po-pill--outline">{{ valueTypeLabel(dictionary.valueType) }}</span>
                <span
                  class="po-pill"
                  :class="dictionary.enabled ? 'po-pill--mint po-pill--dot' : 'po-pill--outline'"
                >
                  {{ t(dictionary.enabled ? 'Common:Enabled' : 'Common:Disabled') }}
                </span>
              </div>
              <h3>{{ dictionary.displayName }}</h3>
              <code class="po-mono">{{ dictionary.dictionaryKey }}</code>
              <p v-if="dictionary.description">{{ dictionary.description }}</p>
              <div class="card-actions">
                <n-button size="small" secondary @click="openData(dictionary)">
                  {{ t('DataDictionary:ViewData') }}
                </n-button>
                <n-button size="small" secondary @click="openEdit(dictionary)">
                  {{ t('Common:Edit') }}
                </n-button>
                <n-popconfirm @positive-click="removeDictionary(dictionary.id)">
                  <template #trigger>
                    <n-button size="small" quaternary type="error">{{ t('Common:Delete') }}</n-button>
                  </template>
                  {{ t('DataDictionary:DeleteConfirm', { name: dictionary.displayName }) }}
                </n-popconfirm>
              </div>
            </article>
          </div>
          <n-empty v-if="!filteredDictionaries.length && !loading" :description="emptyDescription" />
        </n-spin>
      </template>
      <template #empty>
        <n-empty :description="emptyDescription" />
      </template>
    </DataTable>

    <n-modal v-model:show="showEditor" :mask-closable="!saving">
      <n-card
        class="editor-dialog"
        :title="editing ? t('DataDictionary:Edit') : t('DataDictionary:Create')"
        closable
        role="dialog"
        aria-modal="true"
        @close="showEditor = false"
      >
        <n-form ref="formRef" :model="form" :rules="rules" label-placement="top">
          <n-grid responsive="screen" cols="1 m:2" :x-gap="18">
            <n-form-item-gi :label="t('DataDictionary:DictionaryKey')" path="dictionaryKey" required>
              <n-input
                v-model:value="form.dictionaryKey"
                :disabled="Boolean(editing)"
                :maxlength="128"
                show-count
              />
            </n-form-item-gi>
            <n-form-item-gi :label="t('DataDictionary:DisplayName')" path="displayName" required>
              <n-input v-model:value="form.displayName" :maxlength="256" show-count />
            </n-form-item-gi>
            <n-form-item-gi :label="t('DataDictionary:ValueType')">
              <n-select v-model:value="form.valueType" :options="valueTypeOptions" :disabled="valueTypeLocked" />
            </n-form-item-gi>
            <n-form-item-gi :label="t('DataDictionary:Enabled')">
              <n-switch v-model:value="form.enabled">
                <template #checked>{{ t('Common:Enabled') }}</template>
                <template #unchecked>{{ t('Common:Disabled') }}</template>
              </n-switch>
            </n-form-item-gi>
            <template v-if="form.valueType !== 2">
              <n-form-item-gi :label="t('DataDictionary:ValuePrefix')">
                <n-input v-model:value="form.valuePrefix" :maxlength="32" />
              </n-form-item-gi>
              <n-form-item-gi :label="t('DataDictionary:ValueSuffix')">
                <n-input v-model:value="form.valueSuffix" :maxlength="32" />
              </n-form-item-gi>
            </template>
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
import { computed, h, nextTick, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { NButton, NTag, type DataTableColumns, type FormInst, type FormRules } from 'naive-ui'
import { DataTable } from '@/components/Table'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { scrollToFirstFormError } from '@/utils/formErrors'
import {
  dataDictionaryApi,
  type CreateDataDictionary,
  type DataDictionary,
  type DataDictionaryValueType,
  type UpdateDataDictionary,
} from '@/modules/systemManagement/api/dataDictionary'

const newForm = (): CreateDataDictionary => ({
  dictionaryKey: '',
  displayName: '',
  description: '',
  valueType: 0,
  valuePrefix: '',
  valueSuffix: '',
  enabled: true,
})

const router = useRouter()
const dictionaries = ref<DataDictionary[]>([])
const loading = ref(false)
const saving = ref(false)
const showEditor = ref(false)
const editing = ref<DataDictionary | null>(null)
const form = ref<CreateDataDictionary>(newForm())
const formRef = ref<FormInst | null>(null)
const rules: FormRules = {
  dictionaryKey: [{ required: true, message: t('DataDictionary:Required'), trigger: 'blur' }],
  displayName: [{ required: true, message: t('DataDictionary:Required'), trigger: 'blur' }],
}
const keyword = ref('')

const valueTypeLabels: Record<DataDictionaryValueType, string> = {
  0: 'DataDictionary:ValueType:String',
  1: 'DataDictionary:ValueType:Number',
  2: 'DataDictionary:ValueType:Date',
}

const valueTypeOptions = computed(() => [
  { label: t('DataDictionary:ValueType:String'), value: 0 },
  { label: t('DataDictionary:ValueType:Number'), value: 1 },
  { label: t('DataDictionary:ValueType:Date'), value: 2 },
])

const emptyDescription = computed(() =>
  keyword.value ? t('DataDictionary:NoMatches') : t('DataDictionary:Empty'),
)

const filteredDictionaries = computed(() => {
  const term = keyword.value.trim().toLowerCase()
  if (!term) return dictionaries.value
  return dictionaries.value.filter((dictionary) =>
    [dictionary.dictionaryKey, dictionary.displayName, dictionary.description].some((value) =>
      (value ?? '').toLowerCase().includes(term),
    ),
  )
})

const valueTypeLabel = (valueType: DataDictionaryValueType) => t(valueTypeLabels[valueType])
const valueTypeLocked = computed(() => Boolean(editing.value) && (editing.value?.itemCount ?? 0) > 0)
const rowKey = (row: DataDictionary) => row.id

const columns = computed<DataTableColumns<DataDictionary>>(() => [
  { title: t('DataDictionary:DictionaryKey'), key: 'dictionaryKey', minWidth: 170, ellipsis: { tooltip: true } },
  { title: t('DataDictionary:DisplayName'), key: 'displayName', minWidth: 170, ellipsis: { tooltip: true } },
  {
    title: t('DataDictionary:ValueType'),
    key: 'valueType',
    width: 110,
    render: (row) => h(NTag, { size: 'small', bordered: false }, { default: () => valueTypeLabel(row.valueType) }),
  },
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
    width: 220,
    render: (row) => h('div', { class: 'table-actions' }, [
      h(NButton, {
        size: 'small',
        secondary: true,
        onClick: () => openData(row),
      }, { default: () => t('DataDictionary:ViewData') }),
      h(NButton, {
        size: 'small',
        secondary: true,
        onClick: () => openEdit(row),
      }, { default: () => t('Common:Edit') }),
      h(NButton, {
        size: 'small',
        quaternary: true,
        type: 'error',
        onClick: () => removeDictionary(row.id),
      }, { default: () => t('Common:Delete') }),
    ]),
  },
])

const load = async () => {
  loading.value = true
  try {
    dictionaries.value = await dataDictionaryApi.getList()
  } catch {
    useDiscreteApi.message.error(t('DataDictionary:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const openData = (dictionary: DataDictionary) => {
  void router.push({
    name: 'DataDictionaryDataManagement',
    params: { dictionaryId: dictionary.id },
  })
}

const openCreate = () => {
  editing.value = null
  form.value = newForm()
  showEditor.value = true
  void nextTick(() => formRef.value?.restoreValidation())
}

const openEdit = (dictionary: DataDictionary) => {
  editing.value = dictionary
  form.value = { ...dictionary }
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
      const input: UpdateDataDictionary = {
        displayName: form.value.displayName,
        description: form.value.description,
        valueType: form.value.valueType ?? 0,
        valuePrefix: form.value.valuePrefix,
        valueSuffix: form.value.valueSuffix,
        enabled: form.value.enabled,
      }
      const updated = await dataDictionaryApi.update(editing.value.id, input)
      dictionaries.value = dictionaries.value.map((item) => (item.id === updated.id ? updated : item))
    } else {
      const created = await dataDictionaryApi.create(form.value)
      dictionaries.value = [...dictionaries.value, created]
    }
    showEditor.value = false
    useDiscreteApi.message.success(t('DataDictionary:Saved'))
  } catch {
    useDiscreteApi.message.error(t('DataDictionary:SaveFailed'))
  } finally {
    saving.value = false
  }
}

const removeDictionary = async (id: string) => {
  try {
    await dataDictionaryApi.delete(id)
    dictionaries.value = dictionaries.value.filter((dictionary) => dictionary.id !== id)
    useDiscreteApi.message.success(t('DataDictionary:Deleted'))
  } catch {
    useDiscreteApi.message.error(t('DataDictionary:DeleteFailed'))
  }
}

onMounted(() => void load())
</script>

<style scoped>
.dictionary-search {
  width: min(320px, 100%);
}

:deep(.table-actions) {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
  flex-wrap: nowrap;
}

.dictionary-cards {
  display: grid;
  gap: 14px;
}

.dictionary-card h3 {
  margin: 2px 0 0;
  font-size: 17px;
}

.dictionary-card code {
  overflow-wrap: anywhere;
  color: var(--po-ink-soft);
  font-size: 12px;
}

.dictionary-card p {
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

.editor-dialog {
  width: min(860px, calc(100vw - 32px));
}

@media (max-width: 640px) {
  .po-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .dictionary-search {
    width: 100%;
  }

  .po-toolbar :deep(.n-button) {
    width: 100%;
  }

  .card-actions :deep(.n-button) {
    flex: 1 1 auto;
  }
}
</style>

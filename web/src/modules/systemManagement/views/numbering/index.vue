<template>
  <div class="po-page">
    <div class="po-toolbar">
      <span class="po-pill po-pill--outline">
        {{ t('Numbering:Summary', { count: definitions.length }) }}
      </span>
      <span class="po-pill po-pill--mint po-pill--dot">
        {{ t('Numbering:EnabledSummary', { count: enabledCount }) }}
      </span>
      <div class="po-toolbar__spacer" />
      <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
      <n-button type="primary" @click="openCreate">{{ t('Numbering:Create') }}</n-button>
    </div>

    <n-spin :show="loading">
      <div v-if="definitions.length" class="definition-list">
        <article v-for="definition in definitions" :key="definition.id" class="po-card definition-card">
          <header class="definition-card__head">
            <div class="definition-card__main">
              <div class="card-meta">
                <span
                  class="po-pill"
                  :class="definition.enabled ? 'po-pill--mint po-pill--dot' : 'po-pill--outline'"
                >
                  {{ t(definition.enabled ? 'Common:Enabled' : 'Common:Disabled') }}
                </span>
                <code class="po-pill po-pill--sky po-mono">{{ definition.businessKey }}</code>
              </div>
              <h2>{{ definition.displayName }}</h2>
              <p>{{ definition.description || t('Numbering:NoDescription') }}</p>
            </div>
            <n-button secondary size="small" @click="openEdit(definition)">
              {{ t('Common:Edit') }}
            </n-button>
          </header>
          <dl class="po-kv definition-card__meta">
            <dt>{{ t('Numbering:Format') }}</dt>
            <dd><code class="po-mono">{{ definition.format }}</code></dd>
            <dt>{{ t('Numbering:InitialSequence') }}</dt>
            <dd class="po-num">{{ definition.initialSequence }}</dd>
          </dl>
        </article>
      </div>
      <n-empty v-else-if="!loading" :description="t('Numbering:Empty')" />
    </n-spin>

    <n-modal v-model:show="showEditor" :mask-closable="!saving">
      <n-card
        class="editor-dialog"
        :title="editing ? t('Numbering:Edit') : t('Numbering:Create')"
        closable
        role="dialog"
        aria-modal="true"
        @close="showEditor = false"
      >
        <n-form ref="formRef" :model="form" :rules="rules" label-placement="top">
          <n-grid responsive="screen" cols="1 m:2" :x-gap="18">
            <PoFormItem gi :label="t('Numbering:BusinessKey')" path="businessKey" required>
              <n-input
                v-model:value="form.businessKey"
                :disabled="Boolean(editing)"
                :maxlength="128"
                :placeholder="t('Numbering:BusinessKeyPlaceholder')"
                show-count
              />
            </PoFormItem>
            <PoFormItem gi :label="t('Numbering:DisplayName')" path="displayName" required>
              <n-input v-model:value="form.displayName" :maxlength="256" show-count />
            </PoFormItem>
            <PoFormItem
              gi
              :label="t('Numbering:Format')"
              :description="t('Numbering:FormatHelp')"
              path="format"
              required
              :span="2"
            >
              <n-input v-model:value="form.format" :maxlength="512" />
            </PoFormItem>
            <PoFormItem gi :label="t('Numbering:InitialSequence')">
              <n-input-number v-model:value="form.initialSequence" :min="1" :precision="0" />
            </PoFormItem>
            <PoFormItem gi :label="t('Numbering:Enabled')">
              <n-switch v-model:value="form.enabled">
                <template #checked>{{ t('Common:Enabled') }}</template>
                <template #unchecked>{{ t('Common:Disabled') }}</template>
              </n-switch>
            </PoFormItem>
            <PoFormItem gi :label="t('Numbering:DescriptionField')" :span="2">
              <n-input v-model:value="form.description" type="textarea" :rows="3" :maxlength="1024" show-count />
            </PoFormItem>
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
import { computed, nextTick, onMounted, ref } from 'vue'
import type { FormInst, FormRules } from 'naive-ui'
import { t } from '@/i18n'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { scrollToFirstFormError } from '@/utils/formErrors'
import {
  businessNumberDefinitionApi,
  type BusinessNumberDefinition,
  type CreateBusinessNumberDefinition,
} from '@/modules/systemManagement/api/numbering'

const newForm = (): CreateBusinessNumberDefinition => ({
  businessKey: '',
  displayName: '',
  description: '',
  format: '{business}-{date:yyyyMMdd}-{sequence:D6}',
  initialSequence: 1,
  enabled: true,
})

const definitions = ref<BusinessNumberDefinition[]>([])
const loading = ref(false)
const saving = ref(false)
const showEditor = ref(false)
const editing = ref<BusinessNumberDefinition | null>(null)
const form = ref<CreateBusinessNumberDefinition>(newForm())
const formRef = ref<FormInst | null>(null)
const rules: FormRules = {
  businessKey: [{ required: true, message: t('Numbering:Required'), trigger: 'blur' }],
  displayName: [{ required: true, message: t('Numbering:Required'), trigger: 'blur' }],
  format: [{ required: true, message: t('Numbering:Required'), trigger: 'blur' }],
}

const enabledCount = computed(() => definitions.value.filter((definition) => definition.enabled).length)

const load = async () => {
  loading.value = true
  try {
    definitions.value = await businessNumberDefinitionApi.getList()
  } catch {
    useDiscreteApi.message.error(t('Numbering:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  editing.value = null
  form.value = newForm()
  showEditor.value = true
  void nextTick(() => formRef.value?.restoreValidation())
}

const openEdit = (definition: BusinessNumberDefinition) => {
  editing.value = definition
  form.value = { ...definition }
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
      const { businessKey: _businessKey, ...input } = form.value
      void _businessKey // businessKey 创建后不可修改，更新请求需剔除
      const updated = await businessNumberDefinitionApi.update(editing.value.id, input)
      definitions.value = definitions.value.map((item) => (item.id === updated.id ? updated : item))
    } else {
      const created = await businessNumberDefinitionApi.create(form.value)
      definitions.value = [...definitions.value, created].sort((left, right) =>
        left.displayName.localeCompare(right.displayName),
      )
    }
    showEditor.value = false
    useDiscreteApi.message.success(t('Numbering:Saved'))
  } catch {
    useDiscreteApi.message.error(t('Numbering:SaveFailed'))
  } finally {
    saving.value = false
  }
}

onMounted(() => void load())
</script>

<style scoped>
.definition-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: 16px;
}

.definition-card {
  align-content: start;
}

.definition-card__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 14px;
}

.definition-card__main {
  display: grid;
  gap: 6px;
  min-width: 0;
}

.definition-card__main h2 {
  font-size: 18px;
}

.definition-card__main p {
  color: var(--po-ink-soft);
  font-size: 12.5px;
}

.card-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 7px;
}

.card-meta code {
  font-size: 11px;
}

.definition-card__meta {
  margin: 0;
  padding-top: 12px;
  border-top: 1px dashed var(--po-line);
}

.definition-card__meta code {
  overflow-wrap: anywhere;
}

.dialog-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
}

.editor-dialog {
  width: min(860px, calc(100vw - 32px));
}

@media (max-width: 640px) {
  .po-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .po-toolbar :deep(.n-button) {
    width: 100%;
  }

  .definition-card__head {
    flex-direction: column;
    align-items: stretch;
  }

  .definition-card__head :deep(.n-button) {
    width: 100%;
  }
}
</style>

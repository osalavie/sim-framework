<template>
  <div class="po-page">
    <div class="po-toolbar">
      <div class="po-toolbar__spacer" />
      <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
      <n-button type="primary" @click="openCreate">{{ t('Configuration:Create') }}</n-button>
    </div>

    <n-spin :show="loading">
      <div v-if="groups.length" class="configuration-layout">
        <nav class="po-stack configuration-nav" :aria-label="t('Configuration:GroupsNav')">
          <button
            v-for="group in groups"
            :key="group.groupKey"
            type="button"
            class="po-card config-group-card"
            :class="{ 'is-active': group.groupKey === activeGroup?.groupKey }"
            :aria-pressed="group.groupKey === activeGroup?.groupKey"
            @click="activeGroupKey = group.groupKey"
          >
            <span class="config-group-card__code po-mono">{{ group.groupKey }}</span>
            <strong class="config-group-card__name">{{ group.groupName }}</strong>
            <span class="config-group-card__meta">
              <span class="po-pill po-pill--outline">
                {{ t('Configuration:ItemCount', { count: group.items.length }) }}
              </span>
              <span class="po-pill po-pill--mint po-pill--dot">
                {{ t('Configuration:EnabledCount', { count: groupEnabledCount(group) }) }}
              </span>
            </span>
          </button>
          <n-empty v-if="!groups.length" :description="t('Configuration:Empty')" />
        </nav>

        <section v-if="activeGroup" class="po-card configuration-panel">
          <div class="po-card__head">
            <div>
              <h2>{{ activeGroup.groupName }}</h2>
              <p>
                <span class="po-mono">{{ activeGroup.groupKey }}</span>
                ·
                {{ t('Configuration:ItemCount', { count: activeGroup.items.length }) }}
              </p>
            </div>
            <div class="po-card__side">
              <span v-if="dirtyCount" class="po-pill po-pill--sun">
                {{ t('Configuration:UnsavedCount', { count: dirtyCount }) }}
              </span>
              <span v-else class="po-pill po-pill--mint po-pill--dot">
                {{ t('Configuration:AllSaved') }}
              </span>
            </div>
          </div>

          <template v-if="activeGroup.items.length">
            <div class="po-form-grid configuration-form">
              <div v-for="item in activeGroup.items" :key="item.id" class="po-field config-field">
                <label class="po-field__label">
                  <PoFieldLabel :text="item.configName" :description="configFieldHint(item)" />
                </label>
                <n-select
                  v-if="item.configKey === registrationRoleConfigKey && canPickRoles && roleOptions.length > 0"
                  multiple
                  filterable
                  :options="roleOptions"
                  :value="registrationRoleCodes(drafts[item.id] ?? '')"
                  :placeholder="t('Configuration:RegistrationRolesPlaceholder')"
                  @update:value="updateRegistrationRoleCodes(item.id)"
                />
                <n-input
                  v-else
                  v-model:value="drafts[item.id]"
                  :maxlength="1024"
                  :placeholder="t('Configuration:ValuePlaceholder')"
                />
                <div class="config-field__foot">
                  <n-switch v-model:value="enabledDrafts[item.id]">
                    <template #checked>{{ t('Common:Enabled') }}</template>
                    <template #unchecked>{{ t('Common:Disabled') }}</template>
                  </n-switch>
                  <div class="config-field__actions">
                    <n-button size="small" quaternary @click="openEdit(item)">
                      {{ t('Common:Edit') }}
                    </n-button>
                    <n-popconfirm @positive-click="removeItem(item.id)">
                      <template #trigger>
                        <n-button size="small" quaternary type="error">
                          {{ t('Common:Delete') }}
                        </n-button>
                      </template>
                      {{ t('Configuration:DeleteConfirm', { name: item.configName }) }}
                    </n-popconfirm>
                  </div>
                </div>
              </div>
            </div>
          </template>
          <n-empty v-else :description="t('Configuration:EmptyGroup')" />
        </section>
      </div>
      <n-empty v-else-if="!loading" :description="t('Configuration:Empty')" />
    </n-spin>

    <div v-if="dirtyCount" class="po-save-bar">
      <div class="po-card po-card--hard po-save-bar__inner">
        <div class="po-save-bar__text">
          <span class="po-pill po-pill--sun">
            {{ t('Configuration:UnsavedCount', { count: dirtyCount }) }}
          </span>
        </div>
        <div class="po-save-bar__actions">
          <n-button secondary :disabled="saving" @click="discardChanges">
            {{ t('Configuration:Discard') }}
          </n-button>
          <n-button type="primary" :loading="saving" @click="saveChanges">
            {{ t('Configuration:SaveAll') }}
          </n-button>
        </div>
      </div>
    </div>

    <n-modal v-model:show="showEditor" :mask-closable="!saving">
      <n-card
        class="editor-dialog"
        :title="editing ? t('Configuration:Edit') : t('Configuration:Create')"
        closable
        role="dialog"
        aria-modal="true"
        @close="showEditor = false"
      >
        <n-form ref="formRef" :model="form" :rules="rules" label-placement="top">
          <n-grid responsive="screen" cols="1 m:2" :x-gap="18">
            <n-form-item-gi :label="t('Configuration:GroupKey')" path="groupKey" required>
              <n-input v-model:value="form.groupKey" :disabled="Boolean(editing)" :maxlength="64" show-count />
            </n-form-item-gi>
            <n-form-item-gi :label="t('Configuration:GroupName')" path="groupName" required>
              <n-input v-model:value="form.groupName" :maxlength="256" show-count />
            </n-form-item-gi>
            <n-form-item-gi :label="t('Configuration:ConfigKey')" path="configKey" required>
              <n-input v-model:value="form.configKey" :disabled="Boolean(editing)" :maxlength="128" show-count />
            </n-form-item-gi>
            <n-form-item-gi :label="t('Configuration:ConfigName')" path="configName" required>
              <n-input v-model:value="form.configName" :maxlength="256" show-count />
            </n-form-item-gi>
            <n-form-item-gi :label="t('Configuration:Value')" :span="2">
              <n-input v-model:value="form.value" :maxlength="1024" show-count />
            </n-form-item-gi>
            <n-form-item-gi :label="t('Configuration:Sort')">
              <n-input-number v-model:value="form.sort" :min="0" :precision="0" />
            </n-form-item-gi>
            <n-form-item-gi :label="t('Configuration:Enabled')">
              <n-switch v-model:value="form.enabled">
                <template #checked>{{ t('Common:Enabled') }}</template>
                <template #unchecked>{{ t('Common:Disabled') }}</template>
              </n-switch>
            </n-form-item-gi>
            <n-form-item-gi :label="t('Configuration:DescriptionField')" :span="2">
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
import { computed, nextTick, onMounted, ref } from 'vue'
import type { FormInst, FormRules } from 'naive-ui'
import { t } from '@/i18n'
import PoFieldLabel from '@/components/FormItem/PoFieldLabel.vue'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { useUserStore } from '@/stores/user'
import { roleApi } from '@/modules/authorization/api/role'
import { collectAllPages } from '@/utils/pagination'
import { scrollToFirstFormError } from '@/utils/formErrors'
import {
  systemConfigurationApi,
  type CreateSystemConfigurationItem,
  type SystemConfigurationItem,
} from '@/modules/systemManagement/api/configuration'

interface ConfigurationGroup {
  groupKey: string
  groupName: string
  items: SystemConfigurationItem[]
}

const newForm = (): CreateSystemConfigurationItem => ({
  groupKey: '',
  groupName: '',
  configKey: '',
  configName: '',
  value: '',
  description: '',
  sort: 0,
  enabled: true,
})

const items = ref<SystemConfigurationItem[]>([])
const loading = ref(false)
const saving = ref(false)
const showEditor = ref(false)
const editing = ref<SystemConfigurationItem | null>(null)
const form = ref<CreateSystemConfigurationItem>(newForm())
const formRef = ref<FormInst | null>(null)
const rules: FormRules = {
  groupKey: [{ required: true, message: t('Configuration:Required'), trigger: 'blur' }],
  groupName: [{ required: true, message: t('Configuration:Required'), trigger: 'blur' }],
  configKey: [{ required: true, message: t('Configuration:Required'), trigger: 'blur' }],
  configName: [{ required: true, message: t('Configuration:Required'), trigger: 'blur' }],
}
const drafts = ref<Record<string, string>>({})
const enabledDrafts = ref<Record<string, boolean>>({})
const activeGroupKey = ref('')

const registrationRoleConfigKey = 'security.registration-role-codes'
const userStore = useUserStore()
const roleOptions = ref<{ label: string; value: string }[]>([])
const canPickRoles = computed(() => userStore.hasPermission('system:role:manage'))
const configFieldHint = (item: SystemConfigurationItem) => {
  const parts = [item.configKey, item.description]
  if (item.configKey === registrationRoleConfigKey) {
    parts.push(t('Configuration:RegistrationRolesHint'))
  }
  return parts.filter(Boolean).join(' · ')
}
const registrationRoleCodes = (value: string) =>
  value
    .split(',')
    .map((code) => code.trim())
    .filter(Boolean)
const updateRegistrationRoleCodes = (itemId: string) => (value: unknown) => {
  const codes = Array.isArray(value) ? value.map(String) : []
  drafts.value[itemId] = codes.join(',')
}
const loadRoleOptions = async () => {
  if (!canPickRoles.value) return

  try {
    const roles = await collectAllPages((skipCount, maxResultCount) =>
      roleApi.getList({ skipCount, maxResultCount }),
    )
    roleOptions.value = roles
      .filter((role) => !role.isBuiltIn)
      .map((role) => ({ label: `${role.name} (${role.code})`, value: role.code }))
  } catch {
    roleOptions.value = []
  }
}

const groups = computed<ConfigurationGroup[]>(() => {
  const grouped = new Map<string, ConfigurationGroup>()
  for (const item of items.value) {
    const group = grouped.get(item.groupKey) ?? {
      groupKey: item.groupKey,
      groupName: item.groupName,
      items: [],
    }
    group.groupName = item.groupName
    group.items.push(item)
    grouped.set(item.groupKey, group)
  }
  return [...grouped.values()]
})

const activeGroup = computed<ConfigurationGroup | undefined>(
  () =>
    groups.value.find((group) => group.groupKey === activeGroupKey.value) ?? groups.value[0],
)

const groupEnabledCount = (group: ConfigurationGroup) =>
  group.items.filter((item) => enabledDrafts.value[item.id] ?? item.enabled).length

const resetDrafts = () => {
  const values: Record<string, string> = {}
  const enabled: Record<string, boolean> = {}
  for (const item of items.value) {
    values[item.id] = item.value ?? ''
    enabled[item.id] = item.enabled
  }
  drafts.value = values
  enabledDrafts.value = enabled
}

const dirtyItems = computed(() =>
  items.value.filter(
    (item) =>
      (drafts.value[item.id] ?? item.value) !== (item.value ?? '') ||
      (enabledDrafts.value[item.id] ?? item.enabled) !== item.enabled,
  ),
)
const dirtyCount = computed(() => dirtyItems.value.length)

const syncActiveGroup = () => {
  if (!groups.value.some((group) => group.groupKey === activeGroupKey.value)) {
    activeGroupKey.value = groups.value[0]?.groupKey ?? ''
  }
}

const load = async () => {
  loading.value = true
  try {
    items.value = await systemConfigurationApi.getList()
    resetDrafts()
    syncActiveGroup()
  } catch {
    useDiscreteApi.message.error(t('Configuration:LoadFailed'))
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

const openEdit = (item: SystemConfigurationItem) => {
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
      const input = {
        groupName: form.value.groupName,
        configName: form.value.configName,
        value: form.value.value,
        description: form.value.description,
        sort: form.value.sort,
        enabled: form.value.enabled,
      }
      const updated = await systemConfigurationApi.update(editing.value.id, input)
      items.value = items.value.map((item) => (item.id === updated.id ? updated : item))
    } else {
      const created = await systemConfigurationApi.create(form.value)
      items.value = [...items.value, created]
    }
    resetDrafts()
    syncActiveGroup()
    showEditor.value = false
    useDiscreteApi.message.success(t('Configuration:Saved'))
  } catch {
    useDiscreteApi.message.error(t('Configuration:SaveFailed'))
  } finally {
    saving.value = false
  }
}

const discardChanges = () => {
  resetDrafts()
}

const saveChanges = async () => {
  if (!dirtyCount.value) return
  saving.value = true
  try {
    const updated = new Map<string, SystemConfigurationItem>()
    for (const item of dirtyItems.value) {
      const result = await systemConfigurationApi.update(item.id, {
        groupName: item.groupName,
        configName: item.configName,
        value: drafts.value[item.id] ?? item.value,
        description: item.description,
        sort: item.sort,
        enabled: enabledDrafts.value[item.id] ?? item.enabled,
      })
      updated.set(item.id, result)
    }
    items.value = items.value.map((item) => updated.get(item.id) ?? item)
    resetDrafts()
    useDiscreteApi.message.success(t('Configuration:Saved'))
  } catch {
    useDiscreteApi.message.error(t('Configuration:SaveFailed'))
  } finally {
    saving.value = false
  }
}

const removeItem = async (id: string) => {
  try {
    await systemConfigurationApi.delete(id)
    items.value = items.value.filter((item) => item.id !== id)
    resetDrafts()
    syncActiveGroup()
    useDiscreteApi.message.success(t('Configuration:Deleted'))
  } catch {
    useDiscreteApi.message.error(t('Configuration:DeleteFailed'))
  }
}

onMounted(() => {
  void load()
  void loadRoleOptions()
})
</script>

<style scoped>
.configuration-layout {
  display: grid;
  grid-template-columns: minmax(240px, 300px) minmax(0, 1fr);
  gap: 18px;
  align-items: start;
}

.configuration-nav {
  position: sticky;
  top: 16px;
}

.config-group-card {
  display: grid;
  gap: 8px;
  width: 100%;
  padding: 15px 16px;
  color: var(--po-ink);
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition:
    transform 0.12s ease,
    box-shadow 0.12s ease;
}

.config-group-card:hover {
  transform: translate(-1px, -1px);
  box-shadow: var(--po-shadow-hover);
}

.config-group-card.is-active {
  background: var(--po-accent-soft);
  box-shadow: var(--po-shadow-hard);
}

.config-group-card.is-active::before {
  content: '';
  position: absolute;
  top: 18%;
  bottom: 18%;
  left: 6px;
  width: 4px;
  border-radius: 999px;
  background: var(--po-accent);
}

.config-group-card__code,
.config-group-card__name {
  overflow-wrap: anywhere;
}

.config-group-card__code {
  color: var(--po-ink-faint);
  font-size: 11px;
  font-weight: var(--po-weight-bold);
  letter-spacing: 0.04em;
}

.config-group-card__name {
  font-family: var(--po-font-display);
  font-size: 15px;
  font-weight: var(--po-weight-display);
}

.config-group-card__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.configuration-panel {
  min-width: 0;
}

.configuration-form {
  padding-top: 2px;
}

.config-field {
  padding: 13px 14px;
  border: 2px solid var(--po-line);
  border-radius: var(--po-r);
  background: var(--po-bg-deep);
}

.config-field__foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  flex-wrap: wrap;
}

.config-field__actions {
  display: flex;
  align-items: center;
  gap: 4px;
}

.po-save-bar {
  position: sticky;
  bottom: 16px;
  z-index: 12;
}

.po-save-bar__inner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  flex-wrap: wrap;
  padding: 12px 18px;
}

.po-save-bar__text,
.po-save-bar__actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
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

@media (max-width: 1024px) {
  .configuration-layout {
    grid-template-columns: minmax(0, 1fr);
  }

  .configuration-nav {
    position: static;
    grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
    display: grid;
  }
}

@media (max-width: 640px) {
  .po-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .po-toolbar :deep(.n-button) {
    width: 100%;
  }

  .po-save-bar__inner,
  .po-save-bar__text,
  .po-save-bar__actions {
    flex-direction: column;
    align-items: stretch;
  }

  .po-save-bar__actions :deep(.n-button) {
    flex: 1 1 auto;
  }

  .config-field__actions :deep(.n-button) {
    flex: 1 1 auto;
  }
}
</style>

<template>
  <div class="po-page">
    <header class="po-page-head">
      <div class="po-page-head__stats">
        <div v-for="stat in stats" :key="stat.label" class="po-page-head__stat">
          <span class="po-page-head__stat-label">{{ stat.label }}</span>
          <strong class="po-page-head__stat-value">{{ stat.value }}</strong>
          <span class="po-page-head__stat-foot">{{ stat.foot }}</span>
        </div>
      </div>
    </header>

    <div class="po-note">
      <span>{{ t('Openplatform:Applications:SecretOnceHint') }}</span>
    </div>

    <section class="po-toolbar">
      <label class="po-search-box">
        <n-icon :component="SearchOutline" />
        <input
          v-model="keyword"
          type="search"
          :placeholder="t('Openplatform:Applications:SearchPlaceholder')"
          :aria-label="t('Openplatform:Applications:SearchPlaceholder')"
        />
      </label>
      <span class="po-pill po-pill--outline">
        {{ t('Openplatform:Applications:FilteredCount', { count: filteredApplications.length }) }}
      </span>
      <span class="po-toolbar__spacer"></span>
      <span class="po-tiny po-muted">{{ t('Openplatform:Applications:SecretMaskedHint') }}</span>
      <div class="po-toolbar__actions">
        <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
        <n-button type="primary" @click="openCreate">{{ t('Openplatform:Applications:Create') }}</n-button>
      </div>
    </section>

    <n-spin :show="loading">
      <div v-if="filteredApplications.length" class="application-grid">
        <article
          v-for="(application, index) in filteredApplications"
          :key="application.id"
          class="po-card application-card"
        >
          <header class="application-card__head">
            <span class="po-avatar application-card__avatar" :class="avatarTone(index)" aria-hidden="true">
              {{ avatarInitial(application.name) }}
            </span>
            <div class="application-card__title">
              <div class="application-card__name">
                <h2>{{ application.name }}</h2>
                <span
                  class="po-pill"
                  :class="application.isActive ? 'po-pill--mint po-pill--dot' : 'po-pill--outline'"
                >
                  {{ t(application.isActive ? 'Common:Enabled' : 'Common:Disabled') }}
                </span>
              </div>
              <p>{{ application.description || t('Openplatform:Applications:NoDescription') }}</p>
            </div>
          </header>

          <div class="application-card__key">
            <code class="po-mono">{{ application.appId }}</code>
            <button
              type="button"
              class="po-icon-btn po-icon-btn--sm"
              :aria-label="t('Openplatform:Applications:CopyAppId')"
              v-tooltip="t('Openplatform:Applications:CopyAppId')"
              @click="copy(application.appId)"
            >
              <n-icon :component="CopyOutline" />
            </button>
          </div>

          <dl class="po-kv application-card__meta">
            <dt>{{ t('Openplatform:Applications:Scopes') }}</dt>
            <dd>{{ scopeSummary(application) }}</dd>
            <dt>{{ t('Openplatform:Applications:AllowedIps') }}</dt>
            <dd>{{ ipSummary(application) }}</dd>
            <dt>{{ t('Common:CreatedAt') }}</dt>
            <dd>{{ formatDate(application.creationTime) }}</dd>
            <dt>{{ t('Audit:Creator') }}</dt>
            <dd v-tooltip="resolveAccount(application.creatorId)">{{ resolveName(application.creatorId) }}</dd>
            <dt>{{ t('Common:UpdatedAt') }}</dt>
            <dd>
              {{ application.lastModificationTime ? formatDate(application.lastModificationTime) : '-' }}
            </dd>
            <dt>{{ t('Audit:LastModifier') }}</dt>
            <dd v-tooltip="resolveAccount(application.lastModifierId)">
              {{ resolveName(application.lastModifierId) }}
            </dd>
          </dl>

          <footer class="application-card__actions">
            <n-button size="small" secondary @click="openEdit(application)">{{ t('Common:Edit') }}</n-button>
            <n-button size="small" secondary @click="toggleActive(application)">
              {{ t(application.isActive ? 'Openplatform:Applications:Disable' : 'Openplatform:Applications:Enable') }}
            </n-button>
            <n-popconfirm @positive-click="resetSecret(application)">
              <template #trigger>
                <n-button size="small" secondary>
                  {{ t('Openplatform:Applications:ResetSecret') }}
                </n-button>
              </template>
              {{ t('Openplatform:Applications:ResetSecretConfirm') }}
            </n-popconfirm>
            <n-popconfirm @positive-click="removeApplication(application)">
              <template #trigger>
                <n-button size="small" quaternary type="error">{{ t('Common:Delete') }}</n-button>
              </template>
              {{ t('Openplatform:Applications:DeleteConfirm', { name: application.name }) }}
            </n-popconfirm>
          </footer>
        </article>
      </div>
      <n-empty
        v-else-if="!loading"
        :description="keyword ? t('Openplatform:Applications:NoMatches') : t('Openplatform:Applications:Empty')"
      />
    </n-spin>

    <n-modal v-model:show="showEditor" :mask-closable="!saving">
      <n-card
        class="editor-dialog"
        :title="editing ? t('Openplatform:Applications:Edit') : t('Openplatform:Applications:Create')"
        closable
        role="dialog"
        aria-modal="true"
        @close="showEditor = false"
      >
        <n-form ref="formRef" :model="form" :rules="rules" label-placement="top">
          <n-grid responsive="screen" cols="1 m:2" :x-gap="18">
            <PoFormItem gi :label="t('Openplatform:Applications:Name')" path="name" required :span="2">
              <n-input
                v-model:value="form.name"
                :maxlength="128"
                show-count
                :placeholder="t('Openplatform:Applications:NamePlaceholder')"
              />
            </PoFormItem>
            <PoFormItem gi :label="t('Openplatform:Applications:DescriptionField')" :span="2">
              <n-input v-model:value="form.description" type="textarea" :rows="2" :maxlength="512" show-count />
            </PoFormItem>
            <PoFormItem
              gi
              :label="t('Openplatform:Applications:Scopes')"
              :description="t('Openplatform:Applications:ScopesHint')"
            >
              <n-select
                v-model:value="scopes"
                multiple
                filterable
                clearable
                :max-tag-count="'responsive'"
                :options="scopeOptions"
                :loading="scopesLoading"
                :placeholder="t('Openplatform:Applications:ScopesPlaceholder')"
              />
            </PoFormItem>
            <PoFormItem
              gi
              :label="t('Openplatform:Applications:AllowedIps')"
              :description="t('Openplatform:Applications:AllowedIpsHint')"
            >
              <n-input v-model:value="allowedIpsText" type="textarea" :rows="5" />
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

    <n-modal v-model:show="showSecret">
      <n-card
        class="editor-dialog secret-dialog"
        :title="t('Openplatform:Applications:SecretTitle')"
        closable
        role="dialog"
        aria-modal="true"
        @close="closeSecret"
      >
        <n-alert type="warning" class="secret-alert">
          {{
            t(
              secretIsReset
                ? 'Openplatform:Applications:SecretResetHint'
                : 'Openplatform:Applications:SecretCreatedHint',
            )
          }}
        </n-alert>
        <div class="secret-fields">
          <div class="po-field secret-field">
            <span class="po-field__label">{{ t('Openplatform:Applications:AppId') }}</span>
            <div class="secret-field__row">
              <code class="secret-value po-mono">{{ secret.appId }}</code>
              <n-button size="small" secondary @click="copy(secret.appId)">
                {{ t('Openplatform:Applications:CopyAppId') }}
              </n-button>
            </div>
          </div>
          <div class="po-field secret-field">
            <span class="po-field__label">{{ t('Openplatform:Applications:Secret') }}</span>
            <div class="secret-field__row">
              <code class="secret-value po-mono">{{ secret.appSecret }}</code>
              <n-button size="small" secondary @click="copy(secret.appSecret)">
                {{ t('Openplatform:Applications:CopySecret') }}
              </n-button>
            </div>
            <span class="po-field__hint">{{ t('Openplatform:Applications:SecretMaskedHint') }}</span>
          </div>
        </div>
        <template #footer>
          <div class="dialog-actions">
            <n-button type="primary" @click="closeSecret">{{ t('Common:Close') }}</n-button>
          </div>
        </template>
      </n-card>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import type { FormInst, FormRules } from 'naive-ui'
import { CopyOutline, SearchOutline } from '@vicons/ionicons5'
import { messageKey, t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { useAuditNames } from '@/components/AuditInfo/useAuditNames'
import { formatDate } from '@/utils/date'
import { scrollToFirstFormError } from '@/utils/formErrors'
import { openPlatformApplicationApi, openPlatformScopeApi, type OpenPlatformApplication } from '../api'
import { buildScopeGroups, formatListInput, parseListInput, type ScopeGroup } from '../utils'

const applications = ref<OpenPlatformApplication[]>([])
const { resolveName, resolveAccount } = useAuditNames(() =>
  applications.value.flatMap((row) => [row.creatorId, row.lastModifierId]),
)
const loading = ref(false)
const saving = ref(false)
const showEditor = ref(false)
const editing = ref<OpenPlatformApplication | null>(null)
const form = ref({ name: '', description: '' })
const formRef = ref<FormInst | null>(null)
const rules: FormRules = {
  name: [
    { required: true, message: t('Openplatform:Applications:RequiredName'), trigger: 'blur' },
    { max: 128, message: t('Openplatform:Applications:RequiredName'), trigger: 'blur' },
  ],
}
const scopes = ref<string[]>([])
const scopeGroups = ref<ScopeGroup[]>([])
const scopesLoading = ref(false)
const allowedIpsText = ref('')
const keyword = ref('')

/** 权限码模块段 → 分组标题；未知模块段直接展示模块段本身。 */
const scopeGroupLabel = (module: string): string => {
  const key = messageKey('openplatform', 'applications', 'scopeGroup', module)
  const label = t(key)
  return label === key ? module : label
}

const scopeOptions = computed(() =>
  scopeGroups.value.map((group) => ({
    type: 'group' as const,
    key: group.module,
    label: scopeGroupLabel(group.module),
    children: group.options,
  })),
)

const showSecret = ref(false)
const secretIsReset = ref(false)
const secret = ref({ appId: '', appSecret: '' })

const filteredApplications = computed(() => {
  const term = keyword.value.trim().toLowerCase()
  if (!term) return applications.value
  return applications.value.filter((application) =>
    [application.name, application.appId, application.description].some((value) =>
      (value ?? '').toLowerCase().includes(term),
    ),
  )
})

const stats = computed(() => {
  const active = applications.value.filter((application) => application.isActive).length
  const restricted = applications.value.filter((application) => application.allowedIps.length > 0).length
  return [
    {
      label: t('Openplatform:Applications:StatsActive'),
      value: active,
      foot: t('Openplatform:Applications:StatsActiveFoot'),
    },
    {
      label: t('Openplatform:Applications:StatsInactive'),
      value: applications.value.length - active,
      foot: t('Openplatform:Applications:StatsInactiveFoot'),
    },
    {
      label: t('Openplatform:Applications:StatsIps'),
      value: restricted,
      foot: t('Openplatform:Applications:StatsIpsFoot', { count: restricted }),
    },
  ]
})

const avatarTones = ['po-avatar--mint', 'po-avatar--sun', 'po-avatar--peach', 'po-avatar--lilac']
const avatarTone = (index: number) => avatarTones[index % avatarTones.length] ?? 'po-avatar--sky'
const avatarInitial = (name: string) => (name.trim() || '?').slice(0, 1).toUpperCase()

const scopeSummary = (application: OpenPlatformApplication) =>
  application.scopes.length
    ? t('Openplatform:Applications:ScopeCount', { count: application.scopes.length })
    : t('Openplatform:Applications:ScopeNone')

const ipSummary = (application: OpenPlatformApplication) =>
  application.allowedIps.length
    ? t('Openplatform:Applications:IpRestricted', { count: application.allowedIps.length })
    : t('Openplatform:Applications:IpUnrestricted')

/** 拉取权限清单；失败时保留已选权限码，仅提示错误。 */
const loadScopes = async () => {
  scopesLoading.value = true
  try {
    scopeGroups.value = buildScopeGroups(await openPlatformScopeApi.getList())
  } catch {
    useDiscreteApi.message.error(t('Openplatform:Applications:ScopesLoadFailed'))
  } finally {
    scopesLoading.value = false
  }
}

const load = async () => {
  loading.value = true
  try {
    applications.value = await openPlatformApplicationApi.getList()
  } catch {
    useDiscreteApi.message.error(t('Openplatform:Applications:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  editing.value = null
  form.value = { name: '', description: '' }
  scopes.value = []
  allowedIpsText.value = ''
  void loadScopes()
  showEditor.value = true
  void nextTick(() => formRef.value?.restoreValidation())
}

const openEdit = (application: OpenPlatformApplication) => {
  editing.value = application
  form.value = { name: application.name, description: application.description ?? '' }
  scopes.value = [...application.scopes]
  allowedIpsText.value = formatListInput(application.allowedIps)
  void loadScopes()
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
  const name = form.value.name.trim()

  const payload = {
    name,
    description: form.value.description.trim(),
    scopes: scopes.value,
    allowedIps: parseListInput(allowedIpsText.value),
  }

  saving.value = true
  try {
    if (editing.value) {
      const updated = await openPlatformApplicationApi.update(editing.value.id, payload)
      applications.value = applications.value.map((item) => (item.id === updated.id ? updated : item))
      showEditor.value = false
    } else {
      const created = await openPlatformApplicationApi.create(payload)
      showEditor.value = false
      secretIsReset.value = false
      secret.value = { appId: created.appId, appSecret: created.appSecret }
      showSecret.value = true
      await load()
    }
    useDiscreteApi.message.success(t('Openplatform:Applications:Saved'))
  } catch {
    useDiscreteApi.message.error(t('Openplatform:Applications:SaveFailed'))
  } finally {
    saving.value = false
  }
}

const toggleActive = async (application: OpenPlatformApplication) => {
  try {
    if (application.isActive) {
      await openPlatformApplicationApi.disable(application.id)
    } else {
      await openPlatformApplicationApi.enable(application.id)
    }
    applications.value = applications.value.map((item) =>
      item.id === application.id ? { ...item, isActive: !item.isActive } : item,
    )
    useDiscreteApi.message.success(t('Openplatform:Applications:StatusUpdated'))
  } catch {
    useDiscreteApi.message.error(t('Openplatform:Applications:StatusUpdateFailed'))
  }
}

const resetSecret = async (application: OpenPlatformApplication) => {
  try {
    const result = await openPlatformApplicationApi.resetSecret(application.id)
    secretIsReset.value = true
    secret.value = { appId: application.appId, appSecret: result.appSecret }
    showSecret.value = true
  } catch {
    useDiscreteApi.message.error(t('Openplatform:Applications:ResetSecretFailed'))
  }
}

const removeApplication = async (application: OpenPlatformApplication) => {
  try {
    await openPlatformApplicationApi.delete(application.id)
    applications.value = applications.value.filter((item) => item.id !== application.id)
    useDiscreteApi.message.success(t('Openplatform:Applications:Deleted'))
  } catch {
    useDiscreteApi.message.error(t('Openplatform:Applications:DeleteFailed'))
  }
}

const copy = async (value: string) => {
  try {
    await navigator.clipboard.writeText(value)
    useDiscreteApi.message.success(t('Common:Copied'))
  } catch {
    useDiscreteApi.message.error(t('Openplatform:Applications:CopyFailed'))
  }
}

const closeSecret = () => {
  showSecret.value = false
  secret.value = { appId: '', appSecret: '' }
}

onMounted(() => void load())
</script>

<style scoped>
.application-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: 16px;
}

.application-card {
  align-content: start;
}

.application-card__head {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.application-card__avatar {
  width: 42px;
  height: 42px;
  flex: none;
  font-size: 15px;
}

.application-card__title {
  display: grid;
  gap: 5px;
  min-width: 0;
}

.application-card__name {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.application-card__name h2 {
  font-size: 17px;
  overflow-wrap: anywhere;
}

.application-card__title p {
  color: var(--po-ink-soft);
  font-size: 12px;
}

.application-card__key {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border: 2px solid var(--po-line);
  border-radius: var(--po-r-sm);
  background: var(--po-bg-deep);
}

.application-card__key code {
  flex: 1;
  min-width: 0;
  overflow-wrap: anywhere;
  color: var(--po-ink);
  font-size: 12.5px;
}

.application-card__meta {
  margin: 0;
}

.application-card__actions {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
  padding-top: 10px;
  border-top: 1px dashed var(--po-line);
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

.secret-dialog {
  width: min(720px, calc(100vw - 32px));
}

.secret-alert {
  margin-bottom: 16px;
}

.secret-fields {
  display: grid;
  gap: 14px;
}

.secret-field__row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.secret-value {
  flex: 1;
  min-width: 200px;
  overflow-wrap: anywhere;
  padding: 8px 12px;
  border: 2px solid var(--po-line);
  border-radius: var(--po-r-sm);
  background: var(--po-bg-deep);
  color: var(--po-ink);
}

@media (max-width: 640px) {
  .application-card__actions :deep(.n-button) {
    flex: 1 1 auto;
  }

  .application-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

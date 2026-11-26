<template>
  <div class="po-page">
    <n-spin :show="loading">
      <n-form label-placement="top" class="po-stack storage-form">
        <section class="po-card po-card--flush storage-overview">
          <div class="po-card__head storage-overview__head">
            <div>
              <h2>{{ t('Storage:Overview') }}</h2>
            </div>
            <div class="po-card__side storage-overview__actions">
              <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
              <n-button type="primary" :loading="saving" @click="save">{{ t('Common:Save') }}</n-button>
            </div>
          </div>
          <div class="po-form-grid storage-overview__fields">
            <PoFormItem :label="t('Storage:DefaultProvider')">
              <n-select v-model:value="form.defaultProvider" :options="providerOptions" />
            </PoFormItem>
            <PoFormItem :label="t('Storage:MaxUploadMb')">
              <n-input-number v-model:value="maxUploadMb" :min="1" :max="5120" />
            </PoFormItem>
          </div>
        </section>

        <div class="storage-workspace">
          <nav class="po-card po-card--flush provider-nav" :aria-label="t('Storage:ProviderNav')">
            <button
              v-for="provider in form.providers"
              :key="provider.provider"
              type="button"
              class="provider-nav__item"
              :class="{ 'is-active': activeProvider === provider.provider }"
              :aria-pressed="activeProvider === provider.provider"
              @click="activeProvider = provider.provider"
            >
              <span class="provider-status" :class="{ enabled: provider.enabled }" aria-hidden="true" />
              <span class="provider-nav__text">
                <strong>{{ providerLabel(provider.provider) }}</strong>
                <small>{{ t(messageKey('storage', 'providerHint', provider.provider)) }}</small>
              </span>
              <span class="provider-nav__pills">
                <span
                  class="po-pill"
                  :class="provider.enabled ? 'po-pill--mint po-pill--dot' : 'po-pill--outline'"
                >
                  {{ t(provider.enabled ? 'Common:Enabled' : 'Common:Disabled') }}
                </span>
                <span v-if="provider.provider === form.defaultProvider" class="po-pill po-pill--sun">
                  {{ t('Storage:DefaultBadge') }}
                </span>
              </span>
            </button>
          </nav>

          <section v-if="currentProvider" class="po-card provider-editor">
            <header class="provider-editor__head">
              <div>
                <h2>{{ providerLabel(currentProvider.provider) }}</h2>
              </div>
              <div class="provider-editor__actions">
                <n-button size="small" :loading="testing" :disabled="saving" @click="testConnection">
                  {{ t('Storage:TestConnection') }}
                </n-button>
                <n-switch
                  v-model:value="currentProvider.enabled"
                  :disabled="currentProvider.provider === form.defaultProvider"
                >
                  <template #checked>{{ t('Common:Enabled') }}</template>
                  <template #unchecked>{{ t('Common:Disabled') }}</template>
                </n-switch>
              </div>
            </header>

            <div class="po-form-grid provider-fields" :class="{ muted: !currentProvider.enabled }">
              <PoFormItem
                v-if="currentProvider.provider === 'local'"
                :label="t('Storage:RootPath')"
                :required="currentProvider.enabled"
                class="field-wide"
              >
                <n-input v-model:value="currentProvider.rootPath" />
              </PoFormItem>

              <template v-else-if="isObjectProvider(currentProvider.provider)">
                <PoFormItem :label="t('Storage:Endpoint')" class="field-wide">
                  <n-input v-model:value="currentProvider.endpoint" />
                </PoFormItem>
                <PoFormItem :label="t('Field:Region')" :required="currentProvider.enabled">
                  <n-input v-model:value="currentProvider.region" />
                </PoFormItem>
                <PoFormItem :label="t('Storage:Bucket')" :required="currentProvider.enabled">
                  <n-input v-model:value="currentProvider.bucket" />
                </PoFormItem>
                <PoFormItem :label="t('Storage:AccessKeyId')" :required="currentProvider.enabled">
                  <n-input v-model:value="currentProvider.accessKeyId" />
                </PoFormItem>
                <PoFormItem
                  :label="secretLabel(currentProvider.hasAccessKeySecret)"
                  :description="t('Storage:SecretDesc')"
                  :required="currentProvider.enabled && !currentProvider.hasAccessKeySecret"
                >
                  <n-input
                    v-model:value="currentProvider.accessKeySecret"
                    type="password"
                    show-password-on="click"
                  />
                </PoFormItem>
                <PoFormItem
                  :label="tokenLabel(currentProvider.hasSessionToken)"
                  :description="t('Storage:TokenDesc')"
                  class="field-wide"
                >
                  <n-input
                    v-model:value="currentProvider.sessionToken"
                    type="password"
                    show-password-on="click"
                  />
                </PoFormItem>
                <n-checkbox v-model:checked="currentProvider.forcePathStyle" class="field-wide">
                  {{ t('Storage:ForcePathStyle') }}
                </n-checkbox>
              </template>

            </div>
          </section>
        </div>
      </n-form>
    </n-spin>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { messageKey, t } from '@/i18n'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import { useDiscreteApi } from '@/hooks/discreteApi'
import {
  fileStorageConfigurationApi,
  type UpdateFileStorageConfiguration,
  type UpdateFileStorageProviderConfiguration,
} from '@/modules/systemManagement/api/storage'

const loading = ref(false)
const saving = ref(false)
const testing = ref(false)
const activeProvider = ref('local')
const form = ref<UpdateFileStorageConfiguration>({
  defaultProvider: 'local',
  maxUploadBytes: 50 * 1024 * 1024,
  providers: [],
})
const currentProvider = computed(() =>
  form.value.providers.find((provider) => provider.provider === activeProvider.value),
)
const maxUploadMb = computed({
  get: () => Math.max(1, Math.round(form.value.maxUploadBytes / 1024 / 1024)),
  set: (value: number | null) => {
    form.value.maxUploadBytes = (value || 1) * 1024 * 1024
  },
})
const providerOptions = computed(() =>
  form.value.providers
    .filter((provider) => provider.enabled)
    .map((provider) => ({ label: providerLabel(provider.provider), value: provider.provider })),
)

const providerLabel = (provider: string) => t(messageKey('storage', 'provider', provider))
const isObjectProvider = (provider: string) =>
  ['s3', 'tencent-cos', 'aliyun-oss'].includes(provider)
const secretLabel = (configured?: boolean) =>
  `${t('Storage:AccessKeySecret')} · ${t(configured ? 'Storage:Configured' : 'Storage:NotConfigured')}`
const tokenLabel = (configured?: boolean) =>
  `${t('Storage:SessionToken')} · ${t(configured ? 'Storage:Configured' : 'Storage:Optional')}`

const applyValue = (value: UpdateFileStorageConfiguration) => {
  form.value = {
    ...value,
    providers: value.providers.map((provider) => ({ ...provider })),
  }
  activeProvider.value = value.defaultProvider || value.providers[0]?.provider || 'local'
}

const load = async () => {
  loading.value = true
  try {
    applyValue(await fileStorageConfigurationApi.get())
  } catch {
    useDiscreteApi.message.error(t('Storage:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const validateProvider = (provider: UpdateFileStorageProviderConfiguration) => {
  if (!provider.enabled) return true
  if (provider.provider === 'local') {
    return Boolean(provider.rootPath?.trim())
  }
  if (isObjectProvider(provider.provider)) {
    return Boolean(
      provider.region?.trim() &&
        provider.bucket?.trim() &&
        provider.accessKeyId?.trim() &&
        (provider.hasAccessKeySecret || provider.accessKeySecret?.trim()),
    )
  }
  return true
}

const validateStorage = () => {
  const invalid = form.value.providers.filter((provider) => !validateProvider(provider))
  if (invalid.length === 0) return true
  useDiscreteApi.message.warning(t('Storage:RequiredFields'))
  return false
}

const save = async () => {
  if (!validateStorage()) return
  saving.value = true
  try {
    const value = await fileStorageConfigurationApi.update({
      ...form.value,
      providers: form.value.providers.map((provider: UpdateFileStorageProviderConfiguration) => ({
        ...provider,
        accessKeySecret: provider.accessKeySecret || undefined,
        sessionToken: provider.sessionToken || undefined,
      })),
    })
    applyValue(value)
    useDiscreteApi.message.success(t('Storage:Saved'))
  } catch {
    useDiscreteApi.message.error(t('Storage:SaveFailed'))
  } finally {
    saving.value = false
  }
}

const testConnection = async () => {
  const provider = currentProvider.value
  if (provider && !validateProvider(provider)) {
    useDiscreteApi.message.warning(t('Storage:RequiredFields'))
    return
  }
  if (!provider) return
  testing.value = true
  try {
    const result = await fileStorageConfigurationApi.testConnection({
      provider: provider.provider,
      rootPath: provider.rootPath,
      endpoint: provider.endpoint,
      region: provider.region,
      bucket: provider.bucket,
      accessKeyId: provider.accessKeyId,
      accessKeySecret: provider.accessKeySecret || undefined,
      sessionToken: provider.sessionToken || undefined,
      forcePathStyle: provider.forcePathStyle,
    })
    if (result.success) {
      useDiscreteApi.message.success(t('Storage:TestSucceeded'))
      return
    }
    useDiscreteApi.dialog.error({
      title: t('Storage:TestFailed'),
      content: result.message || t('Storage:TestFailedFallback'),
      positiveText: t('Common:Confirm'),
    })
  } catch {
    useDiscreteApi.message.error(t('Storage:TestFailed'))
  } finally {
    testing.value = false
  }
}

onMounted(() => void load())
</script>

<style scoped>
.storage-form {
  gap: 18px;
}

.storage-overview__head {
  padding: 18px 20px 0;
}

.storage-overview__fields {
  padding: 6px 20px 20px;
}

.storage-workspace {
  display: grid;
  grid-template-columns: minmax(240px, 280px) minmax(0, 1fr);
  gap: 18px;
  align-items: start;
}

.provider-nav {
  display: grid;
  gap: 6px;
  padding: 10px;
}

.provider-nav__item {
  position: relative;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: start;
  gap: 10px;
  padding: 12px;
  border: 2px solid transparent;
  border-radius: var(--po-r);
  color: var(--po-ink);
  font: inherit;
  text-align: left;
  background: transparent;
  cursor: pointer;
  transition:
    border-color 0.12s ease,
    background-color 0.12s ease,
    box-shadow 0.12s ease;
}

.provider-nav__item:hover {
  border-color: var(--po-line-mid);
  background: var(--po-bg-deep);
}

.provider-nav__item.is-active {
  border-color: var(--po-ink);
  background: var(--po-accent-soft);
  box-shadow: var(--po-shadow-soft);
}

.provider-nav__text {
  display: grid;
  gap: 3px;
  min-width: 0;
}

.provider-nav__text strong {
  font-family: var(--po-font-display);
  font-size: 14px;
  font-weight: var(--po-weight-display);
}

.provider-nav__text small {
  color: var(--po-ink-soft);
  font-size: 11.5px;
  line-height: 1.45;
}

.provider-nav__pills {
  grid-column: 1 / -1;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 4px;
}

.provider-status {
  width: 9px;
  height: 9px;
  margin-top: 5px;
  border: var(--po-border-thin);
  border-radius: 50%;
  background: var(--po-line-mid);
}

.provider-status.enabled {
  background: var(--po-good);
}

.provider-editor {
  min-width: 0;
}

.provider-editor__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 14px;
  flex-wrap: wrap;
}

.provider-editor__head h2 {
  font-size: 18px;
}

.provider-editor__head p {
  color: var(--po-ink-soft);
  font-size: 12px;
}

.provider-editor__actions {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.provider-fields {
  transition: opacity 160ms ease;
}

.provider-fields.muted {
  opacity: 0.55;
}

.field-wide {
  grid-column: 1 / -1;
}

@media (max-width: 1024px) {
  .storage-workspace {
    grid-template-columns: minmax(0, 1fr);
  }

  .provider-nav {
    display: flex;
    overflow-x: auto;
    padding: 10px;
  }

  .provider-nav__item {
    min-width: 220px;
    flex: 1 0 220px;
  }
}

@media (max-width: 640px) {
  .storage-overview__actions {
    width: 100%;
  }

  .storage-overview__actions :deep(.n-button) {
    flex: 1 1 auto;
  }

  .provider-editor__actions {
    width: 100%;
  }

  .provider-editor__actions :deep(.n-button) {
    flex: 1 1 auto;
  }

  .provider-fields {
    grid-template-columns: minmax(0, 1fr);
  }

  .field-wide {
    grid-column: auto;
  }
}
</style>

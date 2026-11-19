<template>
  <div class="po-page">
    <section class="po-grid po-grid--3" :aria-label="t('Notifications:Overview')">
      <article v-for="metric in metrics" :key="metric.label" class="po-stat">
        <span class="po-stat__label">{{ metric.label }}</span>
        <strong class="po-stat__value">{{ metric.value }}</strong>
        <span class="po-stat__foot">{{ metric.hint }}</span>
      </article>
    </section>

    <n-spin :show="loading">
      <n-tabs v-model:value="activeTab" type="line" animated>
        <n-tab-pane name="providers" :tab="t('Notifications:Providers')">
          <div class="po-toolbar">
            <p class="po-small po-muted toolbar-hint">{{ t('Notifications:ProvidersHint') }}</p>
            <span class="po-pill po-pill--outline">
              {{ t('Notifications:ProviderCount', { count: snapshot.providers.length }) }}
            </span>
            <div class="po-toolbar__spacer" />
            <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
            <n-button type="primary" @click="openProvider()">
              {{ t('Notifications:AddProvider') }}
            </n-button>
          </div>
          <div class="provider-grid">
            <button
              v-for="item in snapshot.providers"
              :key="item.id"
              type="button"
              class="po-card provider-card"
              :aria-label="`${item.displayName} · ${item.channel} · ${item.provider} · ${t(
                item.enabled ? 'Common:Enabled' : 'Common:Disabled',
              )}`"
              @click="openProvider(item)"
            >
              <span
                class="po-avatar provider-card__avatar"
                :class="channelTone(item.channel)"
                aria-hidden="true"
              >
                {{ avatarInitial(item.displayName, item.channel) }}
              </span>
              <span class="provider-card__main">
                <strong>{{ item.displayName }}</strong>
                <small class="po-mono">{{ item.channel }} · {{ item.provider }}</small>
              </span>
              <span class="provider-card__pills">
                <span
                  class="po-pill"
                  :class="item.enabled ? 'po-pill--mint po-pill--dot' : 'po-pill--outline'"
                >
                  {{ t(item.enabled ? 'Common:Enabled' : 'Common:Disabled') }}
                </span>
                <span class="po-pill" :class="item.hasSecret ? 'po-pill--sky' : 'po-pill--outline'">
                  {{ t(item.hasSecret ? 'Notifications:SecretConfigured' : 'Notifications:NoSecret') }}
                </span>
              </span>
            </button>
            <n-empty v-if="!snapshot.providers.length" :description="t('Notifications:Empty')" />
          </div>
        </n-tab-pane>

        <n-tab-pane name="templates" :tab="t('Notifications:Templates')">
          <div class="po-toolbar">
            <p class="po-small po-muted toolbar-hint">{{ t('Notifications:TemplatesHint') }}</p>
            <span class="po-pill po-pill--outline">
              {{ t('Notifications:TemplateCount', { count: snapshot.templates.length }) }}
            </span>
            <div class="po-toolbar__spacer" />
            <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
            <n-button type="primary" @click="openTemplate()">
              {{ t('Notifications:AddTemplate') }}
            </n-button>
          </div>
          <DataTable framed :columns="templateColumns" :data="snapshot.templates" :row-key="rowKey" />
        </n-tab-pane>

        <n-tab-pane name="rules" :tab="t('Notifications:Rules')">
          <div class="po-toolbar">
            <p class="po-small po-muted toolbar-hint">{{ t('Notifications:RulesHint') }}</p>
            <span class="po-pill po-pill--outline">
              {{ t('Notifications:RuleCount', { count: snapshot.rules.length }) }}
            </span>
            <div class="po-toolbar__spacer" />
            <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
            <n-button type="primary" @click="openRule()">
              {{ t('Notifications:AddRule') }}
            </n-button>
          </div>
          <DataTable framed :columns="ruleColumns" :data="snapshot.rules" :row-key="rowKey" />
        </n-tab-pane>

        <n-tab-pane name="webhooks" :tab="t('Notifications:Webhooks')">
          <div class="po-toolbar">
            <p class="po-small po-muted toolbar-hint">{{ t('Notifications:WebhooksHint') }}</p>
            <span class="po-pill po-pill--outline">
              {{ t('Notifications:WebhookCount', { count: snapshot.webhooks.length }) }}
            </span>
            <div class="po-toolbar__spacer" />
            <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
            <n-button type="primary" @click="openWebhook()">
              {{ t('Notifications:AddWebhook') }}
            </n-button>
          </div>
          <DataTable framed :columns="webhookColumns" :data="snapshot.webhooks" :row-key="rowKey" />
        </n-tab-pane>

        <n-tab-pane name="deliveries" :tab="t('Notifications:Deliveries')">
          <div class="po-toolbar">
            <p class="po-small po-muted toolbar-hint">{{ t('Notifications:DeliveriesHint') }}</p>
            <span class="po-pill po-pill--outline">
              {{ t('Notifications:DeliveryCount', { count: deliveries.length }) }}
            </span>
            <div class="po-toolbar__spacer" />
            <n-button secondary :loading="deliveryLoading" @click="loadDeliveries">
              {{ t('Common:Refresh') }}
            </n-button>
          </div>
          <section v-if="deliveries.length" class="po-card po-card--flush delivery-card">
            <div class="po-card__head delivery-card__head">
              <div>
                <h2>{{ t('Notifications:Deliveries') }}</h2>
                <p>{{ t('Notifications:MaskedDestinationHint') }}</p>
              </div>
              <span class="po-pill po-pill--outline">
                {{ t('Notifications:DeliveryCount', { count: deliveries.length }) }}
              </span>
            </div>
            <div class="po-list delivery-list">
              <div v-for="delivery in deliveries" :key="delivery.id" class="delivery-item">
                <span
                  class="delivery-dot"
                  :class="`is-${deliveryStatusKind(delivery.status)}`"
                  aria-hidden="true"
                />
                <div class="delivery-item__main">
                  <strong>{{ delivery.eventName }}</strong>
                  <span class="po-small po-muted">
                    {{ delivery.channel }} · {{ delivery.provider }} · {{ delivery.destinationMasked }}
                  </span>
                </div>
                <div class="delivery-item__meta">
                  <span class="po-pill" :class="deliveryPillClass(delivery.status)">
                    {{ deliveryStatusLabel(delivery.status) }}
                  </span>
                  <span class="po-tiny po-muted">{{ formatDate(delivery.creationTime) }}</span>
                  <span class="po-tiny po-muted">
                    {{ t('Notifications:Attempts') }} · {{ delivery.attempts }}
                  </span>
                </div>
              </div>
            </div>
          </section>
          <n-empty v-else :description="t('Notifications:DeliveriesEmpty')" />
        </n-tab-pane>
      </n-tabs>
    </n-spin>

    <n-modal
      v-model:show="editorVisible"
      preset="card"
      class="editor-modal"
      :title="editorTitle"
      :mask-closable="false"
    >
      <n-form label-placement="top" @submit.prevent="saveEditor">
        <template v-if="editorKind === 'provider'">
          <div class="form-grid">
            <section class="provider-basics wide">
              <div class="provider-basics-grid">
                <PoFormItem
                  class="provider-name-field"
                  :label="t('Notifications:DisplayName')"
                  required
                >
                  <n-input v-model:value="providerForm.displayName" maxlength="128" />
                </PoFormItem>
                <PoFormItem :label="t('Notifications:Channel')" required>
                  <n-select
                    v-model:value="providerForm.channel"
                    :options="channelOptions"
                    :disabled="Boolean(providerForm.id)"
                  />
                </PoFormItem>
                <PoFormItem :label="t('Notifications:Provider')" required>
                  <n-select
                    v-model:value="providerForm.provider"
                    :options="providerEditorOptions"
                    filterable
                    :disabled="Boolean(providerForm.id)"
                  />
                </PoFormItem>
                <PoFormItem :label="t('Notifications:Priority')">
                  <n-input-number v-model:value="providerForm.priority" :min="0" :max="1000" />
                </PoFormItem>
                <PoFormItem :label="t('Common:Status')">
                  <n-switch v-model:value="providerForm.enabled">
                    <template #checked>{{ t('Common:Enabled') }}</template>
                    <template #unchecked>{{ t('Common:Disabled') }}</template>
                  </n-switch>
                </PoFormItem>
              </div>
            </section>

            <n-alert
              v-if="activeProviderSchema.groups.length === 0"
              class="wide"
              type="info"
              :bordered="false"
            >
              {{ t('Notifications:NoProviderSettings') }}
            </n-alert>

            <section
              v-for="group in activeProviderSchema.groups"
              :key="group.titleKey"
              class="config-section wide"
            >
              <header>
                <h3>
                  {{ t(group.titleKey) }}
                  <PoHintIcon
                    v-if="group.descriptionKey"
                    :text="t(group.descriptionKey)"
                  />
                </h3>
              </header>
              <div class="config-fields">
                <PoFormItem
                  v-for="field in group.fields"
                  :key="field.key"
                  :label="t(field.labelKey)"
                  :description="field.hintKey ? t(field.hintKey) : undefined"
                  :required="field.required"
                >
                  <n-select
                    v-if="field.type === 'select'"
                    v-model:value="providerSettings[field.key]"
                    :options="providerFieldOptions(field)"
                  />
                  <n-input
                    v-else-if="field.secret"
                    v-model:value="providerSecrets[field.key]"
                    type="password"
                    show-password-on="click"
                    :disabled="providerClearSecrets"
                    :placeholder="providerFieldPlaceholder(field)"
                    autocomplete="new-password"
                  />
                  <n-input
                    v-else
                    v-model:value="providerSettings[field.key]"
                    :type="field.type === 'textarea' ? 'textarea' : 'text'"
                    :placeholder="providerFieldPlaceholder(field)"
                    autocomplete="off"
                  />
                </PoFormItem>
              </div>
            </section>

            <div v-if="providerHasSecret" class="secret-policy wide">
              <span class="section-title">
                <strong>{{ t('Notifications:SecretRetentionTitle') }}</strong>
                <PoHintIcon :text="t('Notifications:SecretRetentionHint')" />
              </span>
              <n-checkbox v-model:checked="providerClearSecrets">
                {{ t('Notifications:ClearAllSecrets') }}
              </n-checkbox>
            </div>

            <n-collapse class="advanced-config wide">
              <n-collapse-item name="advanced">
                <template #header>
                  <span class="section-title">
                    {{ t('Notifications:AdvancedConfiguration') }}
                    <PoHintIcon :text="t('Notifications:AdvancedConfigurationHint')" />
                  </span>
                </template>
                <div class="advanced-grid">
                  <PoFormItem :label="t('Notifications:SettingsJson')">
                    <n-input
                      v-model:value="providerSettingsJson"
                      type="textarea"
                      :rows="7"
                      spellcheck="false"
                    />
                  </PoFormItem>
                  <PoFormItem
                    :label="t('Notifications:SecretsJson')"
                    :description="t('Notifications:SecretPlaceholder')"
                  >
                    <n-input
                      v-model:value="providerSecretsJson"
                      type="textarea"
                      :rows="7"
                      spellcheck="false"
                      :disabled="providerClearSecrets"
                    />
                  </PoFormItem>
                </div>
              </n-collapse-item>
            </n-collapse>

            <section v-if="providerForm.id" class="provider-test wide">
              <div class="provider-test-heading">
                <span class="section-title">
                  <strong>{{ t('Notifications:TestProviderTitle') }}</strong>
                  <PoHintIcon :text="t('Notifications:TestProviderHint')" />
                </span>
                <SegmentedSwitch
                  v-if="providerSupportsAccountTarget"
                  v-model="providerTestTargetMode"
                  :options="providerTestTargetModeOptions"
                  :aria-label="t('Notifications:TestProviderTitle')"
                />
              </div>
              <div class="provider-test-action">
                <n-select
                  v-if="providerTestTargetMode === 'account'"
                  v-model:value="providerTestAccountId"
                  :options="providerTestAccountOptions"
                  :loading="providerTestAccountsLoading"
                  :placeholder="t('Notifications:TestAccountPlaceholder')"
                  filterable
                  remote
                  clearable
                  @search="searchProviderTestAccounts"
                />
                <n-input
                  v-else
                  v-model:value="providerTestDestination"
                  :placeholder="t('Notifications:TestDestinationPlaceholder')"
                />
                <n-button
                  :loading="providerTesting"
                  :disabled="!providerTestCanSend"
                  @click="testCurrentProvider"
                >
                  {{ t('Notifications:SendTest') }}
                </n-button>
              </div>
            </section>
          </div>
        </template>

        <template v-else-if="editorKind === 'template'">
          <div class="form-grid">
            <PoFormItem :label="t('Notifications:TemplateKey')" required>
              <n-input v-model:value="templateForm.key" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:Channel')" required>
              <n-select v-model:value="templateForm.channel" :options="channelOptions" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:Locale')">
              <n-input v-model:value="templateForm.locale" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:ProviderTemplateCode')">
              <n-input v-model:value="templateForm.providerTemplateCode" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:Subject')" class="wide">
              <n-input v-model:value="templateForm.subject" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:Body')" class="wide" required>
              <n-input v-model:value="templateForm.body" type="textarea" :rows="8" />
            </PoFormItem>
          </div>
        </template>

        <template v-else-if="editorKind === 'rule'">
          <div class="form-grid">
            <PoFormItem :label="t('Notifications:EventName')" class="wide" required>
              <n-input v-model:value="ruleForm.eventName" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:Channel')" required>
              <n-select v-model:value="ruleForm.channel" :options="channelOptions" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:Provider')">
              <n-select
                v-model:value="ruleForm.provider"
                clearable
                :options="ruleProviderOptions"
              />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:TemplateKey')" class="wide" required>
              <n-input v-model:value="ruleForm.templateKey" />
            </PoFormItem>
          </div>
        </template>

        <template v-else>
          <div class="form-grid">
            <PoFormItem :label="t('Notifications:Site')" required>
              <n-input v-model:value="webhookForm.site" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:DisplayName')" required>
              <n-input v-model:value="webhookForm.name" />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:Field:Endpoint')" class="wide" required>
              <n-input
                v-model:value="webhookForm.url"
                placeholder="https://example.com/hooks/sim"
              />
            </PoFormItem>
            <PoFormItem :label="t('Notifications:EventNames')" class="wide">
              <n-select
                v-model:value="webhookForm.eventNames"
                multiple
                filterable
                tag
                :options="[]"
              />
            </PoFormItem>
            <PoFormItem
              :label="t('Notifications:SigningSecret')"
              :description="t('Notifications:SecretPlaceholder')"
              class="wide"
            >
              <n-input
                v-model:value="webhookForm.signingSecret"
                type="password"
                show-password-on="click"
              />
            </PoFormItem>
          </div>
        </template>

        <footer class="modal-actions">
          <div
            v-if="editorKind === 'provider' && Boolean(providerForm.id) && providerSupportsSecrets"
            class="provider-credential-status"
          >
            <n-tag size="small" :type="providerHasSecret ? 'success' : 'default'" :bordered="false">
              {{
                t(providerHasSecret ? 'Notifications:SecretConfigured' : 'Notifications:NoSecret')
              }}
            </n-tag>
            <PoHintIcon :text="t('Notifications:SecretStatusHint')" />
          </div>
          <div class="modal-action-buttons">
            <n-button @click="editorVisible = false">{{ t('Common:Cancel') }}</n-button>
            <n-button attr-type="submit" type="primary" :loading="saving">
              {{ t('Common:Save') }}
            </n-button>
          </div>
        </footer>
      </n-form>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, h, onMounted, reactive, ref, watch } from 'vue'
import { NButton, NTag, type DataTableColumns } from 'naive-ui'
import { DataTable } from '@/components/Table'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import PoHintIcon from '@/components/FormItem/PoHintIcon.vue'
import { messageKey, t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { formatDate } from '@/utils/date'
import { ACTIONS_COLUMN_WIDTH } from '@/utils/tableColumns'
import {
  notificationAdministrationApi,
  type NotificationAccount,
  type NotificationAdministrationSnapshot,
  type NotificationDelivery,
  type NotificationProviderConfiguration,
  type NotificationRule,
  type NotificationTemplate,
  type WebhookEndpoint,
} from '@/modules/systemManagement/api/notifications'
import {
  createProviderSettings,
  getProviderFields,
  providerNames,
  providerSchemas,
  type ProviderField,
  type ProviderSchema,
} from './providerSchemas'

type EditorKind = 'provider' | 'template' | 'rule' | 'webhook'
const emptySnapshot = (): NotificationAdministrationSnapshot => ({
  providers: [],
  templates: [],
  rules: [],
  webhooks: [],
})
const loading = ref(false)
const saving = ref(false)
const deliveryLoading = ref(false)
const activeTab = ref('providers')
const snapshot = ref(emptySnapshot())
const deliveries = ref<NotificationDelivery[]>([])
const editorVisible = ref(false)
const editorKind = ref<EditorKind>('provider')
const providerSettingsJson = ref('{}')
const providerSecretsJson = ref('')
const providerSettings = ref<Record<string, string>>({})
const providerSecrets = ref<Record<string, string>>({})
const providerHasSecret = ref(false)
const providerClearSecrets = ref(false)
const providerTestDestination = ref('')
const providerTestTargetMode = ref<'account' | 'direct'>('account')
const providerTestAccountId = ref<string | null>(null)
const providerTestAccounts = ref<NotificationAccount[]>([])
const providerTestAccountsLoading = ref(false)
const providerTesting = ref(false)
let providerTestAccountSearchSequence = 0
const providerForm = reactive({
  id: undefined as string | undefined,
  channel: 'email',
  provider: 'smtp',
  displayName: '',
  enabled: true,
  priority: 100,
})
const templateForm = reactive({
  id: undefined as string | undefined,
  key: '',
  channel: 'email',
  locale: 'zh-CN',
  subject: '',
  body: '',
  providerTemplateCode: '',
  enabled: true,
})
const ruleForm = reactive({
  id: undefined as string | undefined,
  eventName: '',
  channel: 'inbox',
  provider: undefined as string | undefined,
  templateKey: '',
  enabled: true,
})
const webhookForm = reactive({
  id: undefined as string | undefined,
  site: '',
  name: '',
  url: '',
  enabled: true,
  eventNames: [] as string[],
  signingSecret: '',
})

const channelNames = [
  'inbox',
  'browser',
  'email',
  'sms',
  'webhook',
  'feishu',
  'wecom',
  'dingtalk',
  'voice',
]
const providerChannels = Object.fromEntries(
  Object.entries(providerSchemas).map(([provider, schema]) => [provider, schema.channel]),
)
const channelOptions = channelNames.map((value) => ({ label: value, value }))
const providerEditorOptions = computed(() =>
  providerNames
    .filter((value) => providerChannels[value] === providerForm.channel)
    .map((value) => ({
      label: t(providerSchemas[value]?.labelKey ?? value),
      value,
    })),
)
const ruleProviderOptions = computed(() =>
  providerNames
    .filter((value) => providerChannels[value] === ruleForm.channel)
    .map((value) => ({
      label: t(providerSchemas[value]?.labelKey ?? value),
      value,
    })),
)
const fallbackProviderSchema = providerSchemas.inbox as ProviderSchema
const activeProviderSchema = computed<ProviderSchema>(
  () => providerSchemas[providerForm.provider] ?? fallbackProviderSchema,
)
const providerSupportsSecrets = computed(() =>
  getProviderFields(providerForm.provider).some((field) => field.secret),
)
const providerSupportsAccountTarget = computed(() =>
  ['inbox', 'browser', 'email', 'sms', 'voice'].includes(providerForm.channel),
)
const providerTestTargetModeOptions = computed(() => [
  { label: t('Notifications:TestTargetAccount'), value: 'account' as const },
  { label: t('Notifications:TestTargetDirect'), value: 'direct' as const },
])
const providerTestAccountOptions = computed(() =>
  providerTestAccounts.value.map((item) => {
    const identity =
      item.displayName && item.displayName !== item.account
        ? `${item.displayName} (${item.account})`
        : item.account
    return {
      label: `${identity} · ${item.destinationMasked}`,
      value: item.userId,
    }
  }),
)
const providerTestCanSend = computed(() =>
  providerTestTargetMode.value === 'account'
    ? Boolean(providerTestAccountId.value)
    : Boolean(providerTestDestination.value.trim()),
)
const providerFieldOptions = (field: ProviderField) =>
  (field.options ?? []).map((option) => ({
    label: t(option.labelKey),
    value: option.value,
  }))
const providerFieldPlaceholder = (field: ProviderField) => {
  if (field.secret && providerHasSecret.value) {
    return t('Notifications:SecretFieldRetainPlaceholder')
  }
  return field.required
    ? t('Notifications:RequiredFieldPlaceholder')
    : t('Notifications:OptionalFieldPlaceholder')
}
watch(
  () => providerForm.channel,
  (channel) => {
    if (providerChannels[providerForm.provider] !== channel) {
      providerForm.provider =
        providerNames.find((value) => providerChannels[value] === channel) ?? ''
    }
  },
)
watch(
  () => providerForm.provider,
  (provider, previous) => {
    if (!providerForm.id && provider !== previous) {
      providerSettings.value = createProviderSettings(provider)
      providerSecrets.value = {}
      providerSettingsJson.value = '{}'
      providerSecretsJson.value = ''
      providerHasSecret.value = false
      providerClearSecrets.value = false
    }
  },
)
watch(
  () => ruleForm.channel,
  (channel) => {
    if (ruleForm.provider && providerChannels[ruleForm.provider] !== channel) {
      ruleForm.provider = undefined
    }
  },
)
const rowKey = (row: { id: string }) => row.id
const editorTitle = computed(() => t(messageKey('notifications', 'editor', editorKind.value)))
const metrics = computed(() => [
  {
    label: t('Notifications:EnabledProviders'),
    value: snapshot.value.providers.filter((item) => item.enabled).length,
    hint: `${snapshot.value.providers.length} ${t('Notifications:Configured')}`,
  },
  {
    label: t('Notifications:Templates'),
    value: snapshot.value.templates.length,
    hint: t('Notifications:ChannelTemplates'),
  },
  {
    label: t('Notifications:WebhookSites'),
    value: snapshot.value.webhooks.filter((item) => item.enabled).length,
    hint: t('Notifications:IndependentDelivery'),
  },
])

const channelTone = (channel: string) => {
  const tones: Record<string, string> = {
    inbox: 'po-avatar--sun',
    browser: 'po-avatar--lilac',
    email: 'po-avatar--peach',
    sms: 'po-avatar--mint',
    webhook: 'po-avatar--lilac',
    feishu: 'po-avatar--sun',
    wecom: 'po-avatar--mint',
    dingtalk: 'po-avatar--peach',
    voice: 'po-avatar--sun',
  }
  return tones[channel] ?? 'po-avatar--mint'
}
const avatarInitial = (displayName: string, channel: string) =>
  (displayName.trim() || channel).slice(0, 1).toUpperCase()
type DeliveryStatusKind = 'succeeded' | 'failed' | 'pending' | 'unknown'
const deliveryStatusKind = (status: string): DeliveryStatusKind => {
  const value = (status ?? '').toLowerCase()
  if (['success', 'succeeded', 'sent', 'delivered', 'ok'].some((token) => value.includes(token))) {
    return 'succeeded'
  }
  if (['fail', 'error', 'dead', 'reject'].some((token) => value.includes(token))) return 'failed'
  if (['pending', 'retry', 'queue', 'process'].some((token) => value.includes(token))) return 'pending'
  return 'unknown'
}
const deliveryStatusLabel = (status: string) => {
  const kind = deliveryStatusKind(status)
  if (kind === 'succeeded') return t('Notifications:StatusSucceeded')
  if (kind === 'failed') return t('Notifications:StatusFailed')
  if (kind === 'pending') return t('Notifications:StatusPending')
  return status || t('Notifications:StatusUnknown')
}
const deliveryPillClass = (status: string) => {
  const kind = deliveryStatusKind(status)
  if (kind === 'succeeded') return 'po-pill--mint po-pill--dot'
  if (kind === 'failed') return 'po-pill--peach po-pill--dot'
  if (kind === 'pending') return 'po-pill--sun po-pill--dot'
  return 'po-pill--outline'
}
const editButton = (handler: () => void) =>
  h(NButton, { size: 'small', quaternary: true, onClick: handler }, () => t('Common:Edit'))
const statusTag = (enabled: boolean) =>
  h(NTag, { size: 'small', type: enabled ? 'success' : 'default' }, () =>
    t(enabled ? 'Common:Enabled' : 'Common:Disabled'),
  )
const templateColumns: DataTableColumns<NotificationTemplate> = [
  { title: () => t('Notifications:TemplateKey'), key: 'key' },
  { title: () => t('Notifications:Channel'), key: 'channel' },
  { title: () => t('Notifications:Locale'), key: 'locale' },
  { title: () => t('Common:Status'), key: 'enabled', render: (row) => statusTag(row.enabled) },
  {
    title: '',
    key: 'actions',
    width: ACTIONS_COLUMN_WIDTH.single,
    render: (row) => editButton(() => openTemplate(row)),
  },
]
const ruleColumns: DataTableColumns<NotificationRule> = [
  { title: () => t('Notifications:EventName'), key: 'eventName' },
  { title: () => t('Notifications:Channel'), key: 'channel' },
  { title: () => t('Notifications:TemplateKey'), key: 'templateKey' },
  { title: () => t('Common:Status'), key: 'enabled', render: (row) => statusTag(row.enabled) },
  {
    title: '',
    key: 'actions',
    width: ACTIONS_COLUMN_WIDTH.single,
    render: (row) => editButton(() => openRule(row)),
  },
]
const webhookColumns: DataTableColumns<WebhookEndpoint> = [
  { title: () => t('Notifications:Site'), key: 'site' },
  { title: () => t('Notifications:DisplayName'), key: 'name' },
  { title: () => t('Notifications:Field:Endpoint'), key: 'url', ellipsis: { tooltip: true } },
  { title: () => t('Common:Status'), key: 'enabled', render: (row) => statusTag(row.enabled) },
  {
    title: '',
    key: 'actions',
    width: ACTIONS_COLUMN_WIDTH.single,
    render: (row) => editButton(() => openWebhook(row)),
  },
]

const load = async () => {
  loading.value = true
  try {
    snapshot.value = await notificationAdministrationApi.get()
  } catch {
    useDiscreteApi.message.error(t('Notifications:LoadFailed'))
  } finally {
    loading.value = false
  }
}
const loadDeliveries = async () => {
  deliveryLoading.value = true
  try {
    deliveries.value = await notificationAdministrationApi.getDeliveries()
  } catch {
    useDiscreteApi.message.error(t('Notifications:LoadFailed'))
  } finally {
    deliveryLoading.value = false
  }
}
const openProvider = (item?: NotificationProviderConfiguration) => {
  editorKind.value = 'provider'
  Object.assign(
    providerForm,
    item ?? {
      id: undefined,
      channel: 'email',
      provider: 'smtp',
      displayName: '',
      enabled: true,
      priority: 100,
    },
  )
  const settings = item?.settings ?? {}
  providerSettings.value = createProviderSettings(providerForm.provider, settings)
  providerSecrets.value = {}
  providerHasSecret.value = item?.hasSecret ?? false
  providerClearSecrets.value = false
  providerTestDestination.value = ''
  providerTestTargetMode.value = providerSupportsAccountTarget.value ? 'account' : 'direct'
  providerTestAccountId.value = null
  providerTestAccounts.value = []
  providerSettingsJson.value = JSON.stringify(settings, null, 2)
  providerSecretsJson.value = ''
  editorVisible.value = true
  if (item && providerSupportsAccountTarget.value) {
    void searchProviderTestAccounts('')
  }
}
const openTemplate = (item?: NotificationTemplate) => {
  editorKind.value = 'template'
  Object.assign(
    templateForm,
    item ?? {
      id: undefined,
      key: '',
      channel: 'email',
      locale: 'zh-CN',
      subject: '',
      body: '',
      providerTemplateCode: '',
      enabled: true,
    },
  )
  editorVisible.value = true
}
const openRule = (item?: NotificationRule) => {
  editorKind.value = 'rule'
  Object.assign(
    ruleForm,
    item ?? {
      id: undefined,
      eventName: '',
      channel: 'inbox',
      provider: undefined,
      templateKey: '',
      enabled: true,
    },
  )
  editorVisible.value = true
}
const openWebhook = (item?: WebhookEndpoint) => {
  editorKind.value = 'webhook'
  Object.assign(
    webhookForm,
    item ?? {
      id: undefined,
      site: '',
      name: '',
      url: '',
      enabled: true,
      eventNames: [],
      signingSecret: '',
    },
  )
  webhookForm.signingSecret = ''
  editorVisible.value = true
}
const parseJsonRecord = (value: string, label: string) => {
  if (!value.trim()) return {}
  const parsed: unknown = JSON.parse(value)
  if (!parsed || Array.isArray(parsed) || typeof parsed !== 'object') {
    throw new Error(label)
  }
  const values: Record<string, string> = {}
  for (const [key, entry] of Object.entries(parsed)) {
    if (typeof entry !== 'string') {
      throw new Error(label)
    }
    values[key] = entry
  }
  return values
}
const buildProviderConfiguration = () => {
  const settings = parseJsonRecord(providerSettingsJson.value, 'settings')
  const secrets = parseJsonRecord(providerSecretsJson.value, 'secrets')
  for (const field of getProviderFields(providerForm.provider)) {
    if (field.secret) {
      const value = providerSecrets.value[field.key]?.trim()
      if (value) secrets[field.key] = value
      continue
    }
    settings[field.key] = providerSettings.value[field.key]?.trim() ?? ''
  }
  return { settings, secrets }
}
const validateProviderConfiguration = () => {
  const missing = getProviderFields(providerForm.provider).find((field) => {
    if (!field.required) return false
    if (field.secret) {
      return (
        (!providerSecrets.value[field.key]?.trim() && !providerHasSecret.value) ||
        providerClearSecrets.value
      )
    }
    return !providerSettings.value[field.key]?.trim()
  })
  if (missing) {
    useDiscreteApi.message.error(
      `${t('Notifications:RequiredConfigurationMissing')}：${t(missing.labelKey)}`,
    )
    return false
  }
  return true
}
const searchProviderTestAccounts = async (keyword: string) => {
  if (!providerSupportsAccountTarget.value) return
  const sequence = ++providerTestAccountSearchSequence
  providerTestAccountsLoading.value = true
  try {
    const accounts = await notificationAdministrationApi.getAccounts(
      providerForm.channel,
      keyword.trim() || undefined,
    )
    if (sequence === providerTestAccountSearchSequence) {
      providerTestAccounts.value = accounts
    }
  } catch {
    if (sequence === providerTestAccountSearchSequence) {
      providerTestAccounts.value = []
    }
  } finally {
    if (sequence === providerTestAccountSearchSequence) {
      providerTestAccountsLoading.value = false
    }
  }
}
const testCurrentProvider = async () => {
  if (!providerForm.id || !providerTestCanSend.value) return
  providerTesting.value = true
  try {
    const target =
      providerTestTargetMode.value === 'account'
        ? { userId: providerTestAccountId.value ?? undefined }
        : { destination: providerTestDestination.value.trim() }
    const result = await notificationAdministrationApi.testProvider(providerForm.id, target)
    if (result.succeeded) {
      useDiscreteApi.message.success(t('Notifications:TestSucceeded'))
    } else {
      const error =
        result.errorCode === 'Sim:System:NotificationDestinationUnavailable'
          ? t('Notifications:TestDestinationUnavailable')
          : result.errorCode
      useDiscreteApi.message.error(
        error ? `${t('Notifications:TestFailed')}：${error}` : t('Notifications:TestFailed'),
      )
    }
  } catch {
    useDiscreteApi.message.error(t('Notifications:TestFailed'))
  } finally {
    providerTesting.value = false
  }
}
const isHttpsUrl = (value: string) => {
  try {
    const url = new URL(value)
    return url.protocol === 'https:'
  } catch {
    return false
  }
}

const validateEditorFields = () => {
  let requiredMissing = false
  if (editorKind.value === 'provider') {
    requiredMissing = !providerForm.displayName.trim() || !providerForm.channel || !providerForm.provider
  } else if (editorKind.value === 'template') {
    requiredMissing = !templateForm.key.trim() || !templateForm.channel || !templateForm.body.trim()
  } else if (editorKind.value === 'rule') {
    requiredMissing = !ruleForm.eventName.trim() || !ruleForm.channel || !ruleForm.templateKey.trim()
  } else {
    requiredMissing = !webhookForm.site.trim() || !webhookForm.name.trim() || !webhookForm.url.trim()
  }
  if (requiredMissing) {
    useDiscreteApi.message.warning(t('Notifications:RequiredFieldsMissing'))
    return false
  }
  if (editorKind.value === 'webhook' && !isHttpsUrl(webhookForm.url.trim())) {
    useDiscreteApi.message.warning(t('Notifications:Field:EndpointHint'))
    return false
  }
  return true
}

const saveEditor = async () => {
  if (!validateEditorFields()) return
  saving.value = true
  try {
    if (editorKind.value === 'provider') {
      if (!validateProviderConfiguration()) return
      const configuration = buildProviderConfiguration()
      await notificationAdministrationApi.saveProvider({
        ...providerForm,
        ...configuration,
        clearSecrets: providerClearSecrets.value,
      })
    } else if (editorKind.value === 'template') {
      await notificationAdministrationApi.saveTemplate({ ...templateForm })
    } else if (editorKind.value === 'rule') {
      await notificationAdministrationApi.saveRule({ ...ruleForm })
    } else {
      await notificationAdministrationApi.saveWebhook({ ...webhookForm })
    }
    editorVisible.value = false
    useDiscreteApi.message.success(t('Notifications:Saved'))
    await load()
  } catch {
    useDiscreteApi.message.error(t('Notifications:SaveFailed'))
  } finally {
    saving.value = false
  }
}

onMounted(() => void load())
</script>

<style scoped>
.toolbar-hint {
  max-width: 680px;
  min-width: 220px;
}

.po-toolbar__spacer {
  margin-left: auto;
}

/* 标签页内容不在 .po-page 的 flex 间距体系内，单独补操作栏与内容区之间的间隔 */
.po-toolbar {
  margin-bottom: var(--po-density-page-gap);
}

/* n-tabs-pane-wrapper 为动画裁切了溢出内容；留出投影空间，
   避免最右列 / 最底行卡片贴合裁切边界后看起来像少了右边框 */
:deep(.n-tab-pane > .po-data-table > .po-data-table__frame),
:deep(.n-tab-pane > .delivery-card) {
  margin: 0 3px 3px 0;
}

.provider-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 14px;
  padding: 0 3px 3px 0;
}

.provider-card {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 12px;
  padding: 16px;
  color: var(--po-ink);
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition:
    transform 0.12s ease,
    box-shadow 0.12s ease;
}

.provider-card:hover {
  transform: translate(-1px, -1px);
  box-shadow: var(--po-shadow-hover);
}

.provider-card__avatar {
  width: 42px;
  height: 42px;
  font-size: 15px;
}

.provider-card__main {
  display: grid;
  gap: 3px;
  min-width: 0;
}

.provider-card__main strong {
  font-family: var(--po-font-display);
  font-size: 15px;
  font-weight: var(--po-weight-display);
  overflow-wrap: anywhere;
}

.provider-card__main small {
  color: var(--po-ink-soft);
  font-size: 11.5px;
  overflow-wrap: anywhere;
}

.provider-card__pills {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 6px;
}

.delivery-card__head {
  padding: 16px 20px 12px;
  border-bottom: 2px solid var(--po-line);
}

.delivery-list {
  display: grid;
}

.delivery-item {
  display: grid;
  grid-template-columns: 10px minmax(0, 1fr) auto;
  align-items: center;
  gap: 12px;
  padding: 13px 20px;
}

.delivery-item + .delivery-item {
  border-top: 1px solid var(--po-line);
}

.delivery-dot {
  width: 9px;
  height: 9px;
  border: var(--po-border-thin);
  border-radius: 50%;
  background: var(--po-line-mid);
}

.delivery-dot.is-succeeded {
  background: var(--po-good);
}

.delivery-dot.is-failed {
  background: var(--po-danger);
}

.delivery-dot.is-pending {
  background: var(--po-warn);
}

.delivery-item__main {
  display: grid;
  gap: 3px;
  min-width: 0;
}

.delivery-item__main strong {
  font-family: var(--po-font-display);
  font-size: 14px;
  font-weight: var(--po-weight-strong);
  overflow-wrap: anywhere;
}

.delivery-item__main span {
  overflow-wrap: anywhere;
}

.delivery-item__meta {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  flex-wrap: wrap;
}

/* ---- 编辑弹窗（沿用全局 Naive 换肤，仅补布局） ---- */
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 18px;
}

.wide {
  grid-column: 1 / -1;
}

.provider-basics {
  margin-bottom: 8px;
}

.provider-basics-grid {
  display: grid;
  grid-template-columns:
    minmax(220px, 2fr) minmax(138px, 1fr) minmax(150px, 1fr)
    minmax(100px, 0.6fr) auto;
  gap: 0 16px;
  align-items: start;
}

.config-section {
  margin-top: 12px;
  padding: 18px 2px 0;
  border-top: 1px solid var(--po-line);
}

.config-section header {
  margin-bottom: 14px;
}

.config-section h3 {
  display: flex;
  align-items: center;
  gap: 5px;
  margin: 0;
  font-size: 14px;
  font-weight: var(--po-weight-strong);
}

.config-fields {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
  gap: 0 20px;
}

.section-title {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.secret-policy,
.provider-test {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  margin-top: 12px;
  margin-bottom: 12px;
  padding: 12px 2px;
  border-top: 1px solid var(--po-line);
  border-bottom: 1px solid var(--po-line);
}

.advanced-config {
  margin-bottom: 12px;
  padding: 0 2px;
}

.advanced-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  padding-top: 14px;
}

.provider-test-heading {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}

.provider-test-action {
  width: min(520px, 62%);
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 10px;
}

.modal-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 18px;
  padding-top: 16px;
}

.provider-credential-status,
.modal-action-buttons {
  display: flex;
  align-items: center;
  gap: 8px;
}

.modal-action-buttons {
  margin-left: auto;
}

:global(.editor-modal) {
  width: min(920px, calc(100vw - 32px));
}

@media (max-width: 900px) {
  .provider-basics-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .provider-name-field {
    grid-column: 1 / -1;
  }
}

@media (max-width: 1024px) {
  .provider-grid {
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  }
}

@media (max-width: 700px) {
  .po-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .po-toolbar :deep(.n-button) {
    width: 100%;
  }

  .provider-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .provider-card {
    grid-template-columns: auto minmax(0, 1fr);
  }

  .provider-card__pills {
    grid-column: 1 / -1;
    flex-direction: row;
    align-items: center;
    justify-content: flex-start;
    flex-wrap: wrap;
  }

  .delivery-item {
    grid-template-columns: 10px minmax(0, 1fr);
  }

  .delivery-item__meta {
    grid-column: 1 / -1;
    justify-content: flex-start;
  }

  .form-grid,
  .provider-basics-grid,
  .config-fields,
  .advanced-grid {
    grid-template-columns: 1fr;
  }

  .wide {
    grid-column: auto;
  }

  .secret-policy,
  .provider-test {
    align-items: stretch;
    flex-direction: column;
  }

  .provider-test-action {
    width: 100%;
    grid-template-columns: 1fr;
  }
}
</style>

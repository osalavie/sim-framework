<template>
  <div class="po-page">
    <header class="po-page-head po-page-head--record">
      <div class="po-page-head__main">
        <p class="po-kicker">{{ detail?.number }}</p>
        <h1>{{ detail?.title }}</h1>
        <p>{{ definitionText }}</p>
      </div>
      <div class="po-page-head__actions">
        <ApprovalStatusTag kind="instance" :status="detail?.status" />
        <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
      </div>
    </header>

    <n-spin :show="loading">
      <div class="po-grid po-grid--sidebar">
        <section class="po-card">
          <dl class="summary-grid">
            <div class="po-kv">
              <dt>{{ t('Approval:Detail:Initiator') }}</dt>
              <dd>{{ label(detail?.initiatorUserId) || t('Common:None') }}</dd>
            </div>
            <div class="po-kv">
              <dt>{{ t('Approval:Field:CurrentNode') }}</dt>
              <dd>{{ detail?.currentNodeName || t('Common:None') }}</dd>
            </div>
            <div class="po-kv">
              <dt>{{ t('Common:CreatedAt') }}</dt>
              <dd>{{ formatDate(detail?.creationTime || '') }}</dd>
            </div>
            <div class="po-kv">
              <dt>{{ t('Approval:Field:CompletedAt') }}</dt>
              <dd>{{ detail?.completedTime ? formatDate(detail.completedTime) : t('Common:None') }}</dd>
            </div>
            <div class="po-kv">
              <dt>{{ t('Approval:Field:BusinessType') }}</dt>
              <dd>{{ businessTypeLabel(detail?.businessType) }}</dd>
            </div>
            <div class="po-kv">
              <dt>{{ t('Approval:Submit:BusinessId') }}</dt>
              <dd>{{ detail?.businessId || t('Common:None') }}</dd>
            </div>
          </dl>

          <template v-if="contextList.length">
            <h3 class="section-title">{{ t('Approval:Detail:Context') }}</h3>
            <dl class="summary-grid">
              <div v-for="entry in contextList" :key="entry.key" class="po-kv">
                <dt>{{ entry.displayName }}</dt>
                <dd>{{ entry.value || t('Common:None') }}</dd>
              </div>
            </dl>
          </template>

          <h3 class="section-title">{{ t('Approval:Detail:Attachments') }}</h3>
          <div v-if="detail?.canUploadAttachment" class="attachment-actions">
            <input ref="attachmentInput" type="file" hidden @change="onAttachmentSelected" />
            <n-button size="small" secondary :loading="uploading" @click="attachmentInput?.click()">
              {{ t('Approval:Detail:AttachmentUpload') }}
            </n-button>
          </div>
          <n-empty v-if="!attachments.length" :description="t('Approval:Detail:AttachmentsEmpty')" />
          <ul v-else class="po-list attachment-list">
            <li v-for="attachment in attachments" :key="attachment.id" class="po-list__item attachment-item">
              <span class="attachment-name">{{ attachment.fileName }}</span>
              <span v-if="attachment.taskId" class="po-pill">{{ t('Approval:Detail:AttachmentTaskScoped') }}</span>
              <span class="attachment-meta">
                {{ label(attachment.uploaderUserId) }} · {{ formatDate(attachment.creationTime || '') }}
              </span>
              <n-button size="small" quaternary type="primary" @click="downloadAttachment(attachment)">
                {{ t('Approval:Detail:AttachmentDownload') }}
              </n-button>
            </li>
          </ul>
        </section>

        <aside class="po-stack">
          <section v-if="hasActions" class="po-card">
            <h2 class="section-title action-title">{{ t('Approval:Detail:Title') }}</h2>
            <div class="action-bar">
              <n-button v-if="myTask" type="primary" @click="showTaskAction = true">
                {{ t('Approval:Todo:Handle') }}
              </n-button>
              <n-button v-if="detail?.canWithdraw" secondary @click="openInstanceAction('withdraw')">
                {{ t('Approval:InstanceAction:Withdraw') }}
              </n-button>
              <n-button v-if="detail?.canResubmit" secondary @click="openInstanceAction('resubmit')">
                {{ t('Approval:InstanceAction:Resubmit') }}
              </n-button>
              <n-popconfirm v-if="canUrge" @positive-click="urge">
                <template #trigger>
                  <n-button secondary>{{ t('Approval:InstanceAction:Urge') }}</n-button>
                </template>
                {{ t('Approval:InstanceAction:UrgeConfirm') }}
              </n-popconfirm>
            </div>
            <template v-if="canTerminate">
              <p class="po-note po-note--danger">
                {{ t('Approval:InstanceAction:TerminateHint') }}
              </p>
              <n-button type="error" secondary block @click="openInstanceAction('terminate')">
                {{ t('Approval:InstanceAction:Terminate') }}
              </n-button>
            </template>
          </section>

          <section class="po-card">
            <h2 class="section-title action-title">{{ t('Approval:Detail:CcRecipients') }}</h2>
            <n-empty v-if="!detail?.ccRecipients?.length" :description="t('Approval:Detail:CcEmpty')" />
            <div v-else class="cc-tags">
              <span
                v-for="cc in detail?.ccRecipients"
                :key="cc.id"
                class="po-pill"
                :class="cc.readAt ? '' : 'po-pill--sun'"
              >
                {{ label(cc.userId) }}
                <em v-if="!cc.readAt">{{ t('Approval:Cc:Unread') }}</em>
              </span>
            </div>
          </section>
        </aside>
      </div>

      <ApprovalTimeline :nodes="detail?.nodes" :records="detail?.records" />
    </n-spin>

    <ApprovalTaskActionModal v-model:show="showTaskAction" :task="myTask ?? null" @handled="load" />
    <ApprovalInstanceActionModal
      v-model:show="showInstanceAction"
      :mode="instanceActionMode"
      :instance="detail"
      @done="load"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { formatDate } from '@/utils/date'
import { saveBlob } from '@/api/fileManagement'
import { useUserStore } from '@/stores/user'
import {
  approvalInstanceApi,
  approvalMonitorApi,
  type ApprovalAttachmentDto,
  type ApprovalInstanceDetailDto,
} from '../../api'
import { useBusinessTypes } from '../../composables/useApprovalCatalogs'
import { useUserDirectory } from '../../composables/useUserDirectory'
import ApprovalStatusTag from '../../components/ApprovalStatusTag.vue'
import ApprovalTimeline from '../../components/ApprovalTimeline.vue'
import ApprovalTaskActionModal from '../../components/ApprovalTaskActionModal.vue'
import ApprovalInstanceActionModal from '../../components/ApprovalInstanceActionModal.vue'
import { contextFields, type InstanceActionMode } from '../../utils'

const TERMINATE_PERMISSION = 'approval:instance:terminate'

const route = useRoute()
const userStore = useUserStore()
const { label, ensureLoaded: ensureUsers } = useUserDirectory()
const { label: businessTypeLabel, ensureLoaded: ensureBusinessTypes } = useBusinessTypes()

const detail = ref<ApprovalInstanceDetailDto | null>(null)
const loading = ref(false)
const showTaskAction = ref(false)
const showInstanceAction = ref(false)
const instanceActionMode = ref<InstanceActionMode | null>(null)

const instanceId = computed(() => String(route.params.instanceId ?? ''))
const fromMonitor = computed(() => route.query.from === 'monitor')
const currentUserId = computed(() => userStore.user?.id ?? '')

const definitionText = computed(() =>
  detail.value
    ? `${detail.value.definitionName ?? ''} v${detail.value.definitionVersion ?? 1}`
    : '',
)

const contextList = computed(() => contextFields(detail.value?.context))

const attachments = computed(() => detail.value?.attachments ?? [])

const attachmentInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)

/** 当前用户在该实例上的待办任务；存在时详情页直接提供审批入口。 */
const myTask = computed(() =>
  detail.value?.tasks?.find(
    (task) => task.status === 'Pending' && task.assigneeUserId === currentUserId.value,
  ),
)

const canUrge = computed(
  () =>
    detail.value?.status === 'Running' &&
    Boolean(currentUserId.value) &&
    detail.value?.initiatorUserId === currentUserId.value,
)

const canTerminate = computed(
  () =>
    fromMonitor.value &&
    detail.value?.status === 'Running' &&
    userStore.hasPermission(TERMINATE_PERMISSION),
)

const hasActions = computed(
  () =>
    Boolean(myTask.value) ||
    Boolean(detail.value?.canWithdraw) ||
    Boolean(detail.value?.canResubmit) ||
    canUrge.value ||
    canTerminate.value,
)

const load = async () => {
  if (!instanceId.value) return
  loading.value = true
  try {
    detail.value = fromMonitor.value
      ? await approvalMonitorApi.detail(instanceId.value)
      : await approvalInstanceApi.detail(instanceId.value)
  } catch {
    detail.value = null
    useDiscreteApi.message.error(t('Approval:Detail:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const openInstanceAction = (mode: InstanceActionMode) => {
  instanceActionMode.value = mode
  showInstanceAction.value = true
}

const urge = async () => {
  try {
    await approvalInstanceApi.urge(instanceId.value)
    useDiscreteApi.message.success(t('Approval:InstanceAction:UrgeSucceeded'))
  } catch {
    useDiscreteApi.message.error(t('Approval:InstanceAction:UrgeFailed'))
  }
}

const onAttachmentSelected = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file || !instanceId.value) return

  uploading.value = true
  try {
    await approvalInstanceApi.uploadAttachment(instanceId.value, file)
    useDiscreteApi.message.success(t('Approval:Detail:AttachmentUploadSucceeded'))
    await load()
  } catch {
    useDiscreteApi.message.error(t('Approval:Detail:AttachmentUploadFailed'))
  } finally {
    uploading.value = false
  }
}

const downloadAttachment = async (attachment: ApprovalAttachmentDto) => {
  if (!attachment.id) return

  try {
    const blob = await approvalInstanceApi.downloadAttachment(attachment.id)
    saveBlob(blob, attachment.fileName || 'attachment')
  } catch {
    useDiscreteApi.message.error(t('Approval:Detail:AttachmentDownloadFailed'))
  }
}

onMounted(() => {
  void load()
  void ensureUsers()
  void ensureBusinessTypes()
})
</script>

<style scoped>
.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
  gap: 10px;
  margin: 0;
}

.section-title {
  margin: 18px 0 10px;
  color: var(--po-ink);
  font-size: 14px;
  font-weight: var(--po-weight-strong);
}

.action-title {
  margin-top: 0;
}

.attachment-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
}

.attachment-item {
  flex-wrap: wrap;
  align-items: center;
}

.attachment-name {
  color: var(--po-ink);
  font-weight: 600;
  overflow-wrap: anywhere;
}

.attachment-meta {
  color: var(--po-ink-faint);
  font-size: 12px;
}

.action-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.action-bar :deep(.n-button) {
  flex: 1 1 auto;
}

.po-note {
  margin: 14px 0 10px;
}

.cc-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.cc-tags em {
  margin-left: 4px;
  font-style: normal;
  font-size: 10.5px;
  opacity: 0.75;
}

@media (max-width: 640px) {
  .summary-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

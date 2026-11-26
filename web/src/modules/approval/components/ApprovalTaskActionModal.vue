<template>
  <n-modal v-model:show="visible" :mask-closable="!submitting">
    <n-card
      class="action-dialog"
      :title="t('Approval:Action:Title')"
      closable
      role="dialog"
      aria-modal="true"
      @close="visible = false"
    >
      <div v-if="task" class="task-summary">
        <h4>{{ task.instanceTitle }}</h4>
        <div class="task-meta">
          <span class="po-pill">{{ task.instanceNumber }}</span>
          <span class="po-pill po-pill--sky">{{ task.nodeName }}</span>
          <ApprovalStatusTag kind="instance" :status="task.instanceStatus" />
        </div>
      </div>

      <n-form ref="actionFormRef" :model="actionModel" :rules="actionRules" label-placement="top">
        <PoFormItem :label="t('Approval:Action:Type')">
          <n-radio-group v-model:value="action">
            <n-radio-button v-for="option in actionOptions" :key="option.value" :value="option.value">
              {{ option.label }}
            </n-radio-button>
          </n-radio-group>
        </PoFormItem>

        <PoFormItem v-if="needsTargetUser" :label="targetLabel" path="targetUserId" required>
          <ApprovalUserSelect v-model:value="targetUserId" :placeholder="targetPlaceholder" />
        </PoFormItem>

        <PoFormItem v-if="action === 'AddSign'" :label="t('Approval:Action:SignPosition')">
          <n-radio-group v-model:value="position">
            <n-radio-button v-for="option in positionOptions" :key="option.value" :value="option.value">
              {{ option.label }}
            </n-radio-button>
          </n-radio-group>
        </PoFormItem>

        <PoFormItem
          :label="t('Approval:Action:Comment')"
          :description="t('Approval:Action:CommentDesc')"
          path="comment"
          :required="commentRequired"
        >
          <n-input
            v-model:value="comment"
            type="textarea"
            :rows="3"
            :maxlength="1000"
            show-count
          />
        </PoFormItem>

        <PoFormItem :label="t('Approval:Detail:Attachments')">
          <input ref="attachmentInput" type="file" hidden @change="onAttachmentSelected" />
          <n-button size="small" secondary :loading="uploading" @click="attachmentInput?.click()">
            {{ t('Approval:Detail:AttachmentUpload') }}
          </n-button>
        </PoFormItem>
      </n-form>

      <template #footer>
        <div class="dialog-actions">
          <n-button @click="visible = false">{{ t('Common:Cancel') }}</n-button>
          <n-button type="primary" :loading="submitting" @click="submit">
            {{ t('Common:Confirm') }}
          </n-button>
        </div>
      </template>
    </n-card>
  </n-modal>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import type { FormInst, FormRules } from 'naive-ui'
import { t } from '@/i18n'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { scrollToFirstFormError } from '@/utils/formErrors'
import { approvalInstanceApi, type ApprovalTaskDto } from '../api'
import ApprovalStatusTag from './ApprovalStatusTag.vue'
import ApprovalUserSelect from './ApprovalUserSelect.vue'

type ActionKind =
  | 'Approve'
  | 'Reject'
  | 'ReturnToPrevious'
  | 'ReturnToInitiator'
  | 'Transfer'
  | 'AddSign'

const props = defineProps<{ show: boolean; task: ApprovalTaskDto | null }>()
const emit = defineEmits<{ 'update:show': [value: boolean]; handled: [] }>()

const submitting = ref(false)
const action = ref<ActionKind>('Approve')
const comment = ref('')
const targetUserId = ref<string | null>(null)
const actionFormRef = ref<FormInst | null>(null)
const actionModel = computed(() => ({
  targetUserId: targetUserId.value,
  comment: comment.value,
}))
const position = ref('Before')
const attachmentInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)

const visible = computed({
  get: () => props.show,
  set: (value: boolean) => emit('update:show', value),
})

/** 可用动作由节点配置与流程开关决定，接口未返回权限时仅开放同意/拒绝。 */
const actionOptions = computed(() => {
  const permissions = props.task?.permissions
  const options: { value: ActionKind; label: string }[] = [
    { value: 'Approve', label: t('Approval:Action:Approve') },
    { value: 'Reject', label: t('Approval:Action:Reject') },
  ]

  if (permissions?.canReturnToPrevious) {
    options.push({ value: 'ReturnToPrevious', label: t('Approval:Action:ReturnToPrevious') })
  }
  if (permissions?.canReturnToInitiator) {
    options.push({ value: 'ReturnToInitiator', label: t('Approval:Action:ReturnToInitiator') })
  }
  if (permissions?.canTransfer) {
    options.push({ value: 'Transfer', label: t('Approval:Action:Transfer') })
  }
  if (permissions?.canAddSign) {
    options.push({ value: 'AddSign', label: t('Approval:Action:AddSign') })
  }

  return options
})

const positionOptions = computed(() => [
  { value: 'Before', label: t('Approval:Action:PositionBefore') },
  { value: 'After', label: t('Approval:Action:PositionAfter') },
])

const needsTargetUser = computed(() => action.value === 'Transfer' || action.value === 'AddSign')

const commentRequired = computed(() =>
  ['Reject', 'ReturnToPrevious', 'ReturnToInitiator'].includes(action.value),
)

const actionRules = computed<FormRules>(() => ({
  ...(needsTargetUser.value
    ? {
        targetUserId: [
          { required: true, message: t('Approval:Action:TargetRequired'), trigger: 'blur' },
        ],
      }
    : {}),
  ...(commentRequired.value
    ? {
        comment: [
          { required: true, message: t('Approval:Action:CommentRequired'), trigger: 'blur' },
        ],
      }
    : {}),
}))

watch(action, () => {
  void nextTick(() => actionFormRef.value?.restoreValidation())
})

const targetLabel = computed(() =>
  t(action.value === 'Transfer' ? 'Approval:Action:TargetUser' : 'Approval:Action:SignUser'),
)

const targetPlaceholder = computed(() =>
  t(
    action.value === 'Transfer'
      ? 'Approval:Action:TargetPlaceholder'
      : 'Approval:Action:SignPlaceholder',
  ),
)

const runAction = (taskId: string) => {
  const value = comment.value.trim()

  switch (action.value) {
    case 'Approve':
      return approvalInstanceApi.approve(taskId, value)
    case 'Reject':
      return approvalInstanceApi.reject(taskId, value)
    case 'ReturnToPrevious':
      return approvalInstanceApi.returnToPrevious(taskId, value)
    case 'ReturnToInitiator':
      return approvalInstanceApi.returnToInitiator(taskId, value)
    case 'Transfer':
      return approvalInstanceApi.transfer(taskId, targetUserId.value as string, value)
    case 'AddSign':
      return approvalInstanceApi.addSign(taskId, targetUserId.value as string, position.value, value)
  }
}

/** 审批人上传佐证材料：挂到当前任务，与后续审批动作相互独立。 */
const onAttachmentSelected = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  const taskId = props.task?.id
  if (!file || !taskId) return

  uploading.value = true
  try {
    await approvalInstanceApi.uploadTaskAttachment(taskId, file)
    useDiscreteApi.message.success(t('Approval:Detail:AttachmentUploadSucceeded'))
    emit('handled')
  } catch {
    useDiscreteApi.message.error(t('Approval:Detail:AttachmentUploadFailed'))
  } finally {
    uploading.value = false
  }
}

const submit = async () => {
  const taskId = props.task?.id
  if (!taskId) return

  try {
    await actionFormRef.value?.validate()
  } catch {
    void scrollToFirstFormError(actionFormRef.value)
    return
  }

  submitting.value = true
  try {
    await runAction(taskId)
    useDiscreteApi.message.success(t('Approval:Action:Succeeded'))
    visible.value = false
    emit('handled')
  } catch {
    useDiscreteApi.message.error(t('Approval:Action:Failed'))
  } finally {
    submitting.value = false
  }
}

watch(
  () => props.show,
  (show) => {
    if (!show) return
    comment.value = ''
    targetUserId.value = null
    position.value = 'Before'
    action.value = actionOptions.value[0]?.value ?? 'Approve'
  },
)
</script>

<style scoped>
.action-dialog { width: min(560px, calc(100vw - 32px)); }
.task-summary { margin-bottom: 16px; }
.task-summary h4 { margin: 0 0 8px; color: var(--po-ink); font-family: var(--po-font-display); font-size: 16px; font-weight: var(--po-weight-display); }
.task-meta { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.dialog-actions { display: flex; align-items: center; justify-content: flex-end; gap: 10px; }
</style>

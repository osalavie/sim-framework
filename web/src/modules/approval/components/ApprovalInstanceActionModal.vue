<template>
  <n-modal v-model:show="visible" :mask-closable="!submitting">
    <n-card
      class="instance-dialog"
      :title="title"
      closable
      role="dialog"
      aria-modal="true"
      @close="visible = false"
    >
      <n-form
        v-if="mode === 'resubmit'"
        ref="actionFormRef"
        :model="actionModel"
        :rules="actionRules"
        label-placement="top"
      >
        <PoFormItem :label="t('Common:Title')" path="title" required>
          <n-input v-model:value="titleText" :maxlength="256" show-count />
        </PoFormItem>
        <PoFormItem :label="t('Approval:Submit:Context')" :description="t('Approval:Submit:ContextHint')">
          <n-input
            v-model:value="contextText"
            type="textarea"
            :rows="3"
          />
        </PoFormItem>
      </n-form>

      <n-form v-else ref="actionFormRef" :model="actionModel" :rules="actionRules" label-placement="top">
        <PoFormItem
          :label="t('Approval:Action:Comment')"
          :description="t('Approval:Action:CommentDesc')"
          path="comment"
          :required="mode === 'terminate'"
        >
          <n-input
            v-model:value="comment"
            type="textarea"
            :rows="3"
            :maxlength="1000"
            show-count
          />
        </PoFormItem>
      </n-form>

      <p v-if="hint" class="po-note po-note--danger">{{ hint }}</p>

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
import { messageKey, t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { scrollToFirstFormError } from '@/utils/formErrors'
import { approvalInstanceApi, approvalMonitorApi, type ApprovalInstanceDetailDto } from '../api'
import { formatContextInput, parseContextInput, type InstanceActionMode } from '../utils'

const props = defineProps<{
  show: boolean
  mode: InstanceActionMode | null
  instance: ApprovalInstanceDetailDto | null
}>()

const emit = defineEmits<{ 'update:show': [value: boolean]; done: [] }>()

const submitting = ref(false)
const comment = ref('')
const titleText = ref('')
const contextText = ref('')
const actionFormRef = ref<FormInst | null>(null)
const actionModel = computed(() => ({ title: titleText.value, comment: comment.value }))
const actionRules = computed<FormRules>(() => {
  const rules: FormRules = {}
  if (props.mode === 'resubmit') {
    rules.title = [{ required: true, message: t('Approval:Submit:TitleRequired'), trigger: 'blur' }]
  } else if (props.mode === 'terminate') {
    rules.comment = [
      { required: true, message: t('Approval:InstanceAction:ReasonRequired'), trigger: 'blur' },
    ]
  }
  return rules
})

const visible = computed({
  get: () => props.show,
  set: (value: boolean) => emit('update:show', value),
})

const title = computed(() => t(messageKey('approval', 'instanceAction', props.mode ?? 'withdraw')))

const hint = computed(() =>
  props.mode === 'terminate' ? t('Approval:InstanceAction:TerminateHint') : '',
)

const submit = async () => {
  const instanceId = props.instance?.id
  if (!instanceId || !props.mode) return

  try {
    await actionFormRef.value?.validate()
  } catch {
    void scrollToFirstFormError(actionFormRef.value)
    return
  }

  submitting.value = true
  try {
    if (props.mode === 'withdraw') {
      await approvalInstanceApi.withdraw(instanceId, comment.value.trim())
    } else if (props.mode === 'terminate') {
      await approvalMonitorApi.terminate(instanceId, comment.value.trim())
    } else {
      await approvalInstanceApi.resubmit(instanceId, {
        businessType: props.instance?.businessType ?? '',
        businessId: props.instance?.businessId ?? '',
        title: titleText.value.trim(),
        fields: parseContextInput(contextText.value),
      })
    }

    useDiscreteApi.message.success(t('Approval:InstanceAction:Succeeded'))
    visible.value = false
    emit('done')
  } catch {
    useDiscreteApi.message.error(t('Approval:InstanceAction:Failed'))
  } finally {
    submitting.value = false
  }
}

watch(
  () => props.show,
  (show) => {
    if (!show) return
    comment.value = ''
    titleText.value = props.instance?.title ?? ''
    contextText.value = formatContextInput(props.instance?.context)
    void nextTick(() => actionFormRef.value?.restoreValidation())
  },
)
</script>

<style scoped>
.po-note { margin: 12px 0 0; }
.instance-dialog { width: min(560px, calc(100vw - 32px)); }
.dialog-actions { display: flex; align-items: center; justify-content: flex-end; gap: 10px; }
</style>

<template>
  <n-form class="edge-inspector" label-placement="top" :show-feedback="false">
    <n-form-item :label="t('Approval:Designer:EdgeEndpoints')">
      <n-input :value="`${fromName} → ${toName}`" disabled />
    </n-form-item>
    <n-form-item :label="t('Approval:Designer:EdgeLabel')">
      <n-input
        v-model:value="draft.label"
        :maxlength="64"
        :placeholder="t('Approval:Designer:EdgeLabelPlaceholder')"
      />
    </n-form-item>
    <ApprovalConditionEditor
      v-model:condition="draft.condition"
      :fields="fields"
      :disabled="locked"
    />
  </n-form>
</template>

<script setup lang="ts">
import { t } from '@/i18n'
import type { ApprovalFieldDto } from '../api'
import ApprovalConditionEditor from './ApprovalConditionEditor.vue'
import type { FlowEdgeDraft } from '../utils/flowGraph'

/** 连线草稿由画布持有，属性面板直接改写标签与出边条件。 */
const draft = defineModel<FlowEdgeDraft>('draft', { required: true })

defineProps<{
  fromName: string
  toName: string
  /** 业务类型声明的可条件字段（已过滤附件字段）。 */
  fields?: ApprovalFieldDto[]
  /** 并行网关的出边不接受条件（后端同样拒绝），此处只读提示。 */
  locked?: boolean
}>()
</script>

<style scoped>
.edge-inspector {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
</style>

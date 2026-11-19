<template>
  <n-form class="node-inspector" label-placement="top" :show-feedback="false">
    <PoFormItem :label="t('Approval:Field:Name')">
      <n-input v-model:value="node.name" :placeholder="t('Approval:Definition:NodeNamePlaceholder')" />
    </PoFormItem>
    <PoFormItem :label="t('Approval:Field:NodeType')">
      <n-select v-model:value="node.nodeType" :options="nodeTypeOptions" />
    </PoFormItem>
    <PoFormItem :label="t('Approval:Field:NodeKey')" :description="t('Approval:Designer:NodeKeyHint')">
      <n-input :value="node.nodeKey" disabled />
    </PoFormItem>

    <template v-if="node.nodeType === 'Approval'">
      <PoFormItem :label="t('Approval:Field:ApproverType')">
        <n-select v-model:value="node.approverType" :options="approverTypeOptions" />
      </PoFormItem>
      <PoFormItem v-if="node.approverType === 'SpecifiedUsers'" :label="t('Approval:Field:Approvers')">
        <ApprovalUserSelect v-model:value="node.approverUserIds" multiple />
      </PoFormItem>
      <PoFormItem v-else-if="node.approverType === 'Role'" :label="t('Approval:Field:ApproverRole')">
        <n-select
          v-model:value="node.approverRoleName"
          filterable
          :options="roleOptions"
          :placeholder="t('Approval:Definition:RolePlaceholder')"
        />
      </PoFormItem>
      <PoFormItem
        v-else-if="isOrganizationApproverType(node.approverType)"
        :label="t('Approval:Field:ApproverOrganization')"
      >
        <n-select
          v-model:value="node.approverOrganizationId"
          :options="organizationOptions"
          filterable
          clearable
          :placeholder="t('Approval:Definition:OrganizationPlaceholder')"
        />
      </PoFormItem>
      <PoFormItem :label="t('Approval:Field:MultiApproveMode')">
        <n-select v-model:value="node.multiApproveMode" :options="multiApproveModeOptions" />
      </PoFormItem>
      <PoFormItem
        :label="t('Approval:Field:TimeoutHours')"
        :description="t('Approval:Definition:TimeoutPlaceholder')"
      >
        <n-input-number
          v-model:value="node.timeoutHours"
          clearable
          :min="1"
          :max="8760"
        />
      </PoFormItem>
    </template>

    <template v-else-if="node.nodeType === 'Cc'">
      <PoFormItem :label="t('Approval:Field:CcUsers')">
        <ApprovalUserSelect v-model:value="node.ccConfig.userIds" multiple />
      </PoFormItem>
      <PoFormItem :label="t('Approval:Field:CcRoles')">
        <n-select
          v-model:value="node.ccConfig.roles"
          multiple
          filterable
          :options="roleOptions"
          :placeholder="t('Approval:Definition:RolePlaceholder')"
        />
      </PoFormItem>
      <PoFormItem :label="t('Approval:Field:CcInitiator')">
        <n-switch v-model:value="node.ccConfig.initiator" />
      </PoFormItem>
    </template>

    <PoFormItem
      v-else-if="isGateway"
      :label="t('Approval:Designer:JoinPolicy')"
      :description="t('Approval:Designer:JoinPolicyHint')"
    >
      <n-select v-model:value="node.joinPolicy" :options="joinPolicyOptions" />
    </PoFormItem>

    <ApprovalConditionEditor
      v-if="node.nodeType !== 'Start' && node.nodeType !== 'End'"
      v-model:condition="node.condition"
      :fields="fields"
    />

    <div v-if="node.nodeType === 'Approval'" class="switch-column">
      <n-checkbox v-model:checked="node.allowTransfer">{{ t('Approval:Field:AllowTransfer') }}</n-checkbox>
      <n-checkbox v-model:checked="node.allowAddSign">{{ t('Approval:Field:AllowAddSign') }}</n-checkbox>
      <n-checkbox v-model:checked="node.allowReturnToInitiator">
        {{ t('Approval:Field:AllowReturnToInitiator') }}
      </n-checkbox>
    </div>
  </n-form>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { t } from '@/i18n'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import type { ApprovalFieldDto } from '../api'
import {
  APPROVER_TYPES,
  JOIN_POLICIES,
  MULTI_APPROVE_MODES,
  NODE_TYPES,
  approverTypeLabelKey,
  joinPolicyLabelKey,
  multiApproveModeLabelKey,
  nodeTypeLabelKey,
} from '../api/approvalModels'
import ApprovalConditionEditor from './ApprovalConditionEditor.vue'
import ApprovalUserSelect from './ApprovalUserSelect.vue'
import { useOrganizationOptions, useRoleOptions } from '../composables/useApprovalCatalogs'
import { enumOptions, isOrganizationApproverType, type ApprovalFlowNodeDraft } from '../utils'
import { isGatewayNodeType } from '../utils/flowGraph'

/** 节点草稿由画布持有，属性面板直接改写其中的字段。 */
const node = defineModel<ApprovalFlowNodeDraft>('node', { required: true })

const props = defineProps<{
  /** 业务类型声明的可条件字段（已过滤附件字段）。 */
  fields?: ApprovalFieldDto[]
}>()
void props

const { options: roleOptions, ensureLoaded } = useRoleOptions()
void ensureLoaded()

const { options: organizationOptions, ensureLoaded: ensureOrganizationsLoaded } =
  useOrganizationOptions()
void ensureOrganizationsLoaded()

const isGateway = computed(() => isGatewayNodeType(node.value.nodeType))

const nodeTypeOptions = enumOptions(NODE_TYPES, nodeTypeLabelKey)
const approverTypeOptions = enumOptions(APPROVER_TYPES, approverTypeLabelKey)
const multiApproveModeOptions = enumOptions(MULTI_APPROVE_MODES, multiApproveModeLabelKey)
const joinPolicyOptions = enumOptions(JOIN_POLICIES, joinPolicyLabelKey)
</script>

<style scoped>
.node-inspector {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.switch-column {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-top: 12px;
  border-top: 1px dashed var(--theme-divider);
}
</style>

<template>
  <section class="condition-block">
    <div class="condition-head">
      <span class="block-label">{{ t('Approval:Field:Condition') }}</span>
      <n-select
        v-model:value="condition.match"
        class="condition-match"
        :disabled="disabled"
        :options="matchOptions"
      />
      <n-button size="small" secondary :disabled="disabled" @click="addRule">
        {{ t('Approval:Condition:AddRule') }}
      </n-button>
    </div>
    <p v-if="disabled" class="condition-hint">{{ t('Approval:Designer:EdgeConditionLocked') }}</p>
    <p v-else-if="!condition.rules.length" class="condition-hint">
      {{ t('Approval:Condition:EmptyHint') }}
    </p>
    <div v-for="(rule, index) in condition.rules" :key="index" class="condition-row">
      <n-select
        v-model:value="rule.field"
        class="condition-field"
        filterable
        tag
        :disabled="disabled"
        :options="fieldOptions"
        :placeholder="t('Approval:Condition:FieldPlaceholder')"
      />
      <n-select
        v-model:value="rule.operator"
        class="condition-operator"
        :disabled="disabled"
        :options="operatorOptions"
      />
      <n-input
        v-if="conditionOperatorNeedsValue(rule.operator)"
        v-model:value="rule.value"
        class="condition-value"
        :disabled="disabled"
        :placeholder="t('Approval:Condition:ValuePlaceholder')"
      />
      <n-button size="small" quaternary :disabled="disabled" @click="removeRule(index)">
        {{ t('Common:Delete') }}
      </n-button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { t } from '@/i18n'
import type { ApprovalFieldDto } from '../api'
import {
  CONDITION_MATCHES,
  CONDITION_OPERATORS,
  conditionMatchLabelKey,
  conditionOperatorLabelKey,
  conditionOperatorNeedsValue,
  enumOptions,
  type ConditionDraft,
} from '../utils'

/** 条件规则直接写在父级传入的草稿对象上（画布节点 / 连线的属性）。 */
const condition = defineModel<ConditionDraft>('condition', { required: true })

const props = defineProps<{
  /** 业务类型声明的可条件字段（已过滤附件字段）；为空时允许自由输入字段名。 */
  fields?: ApprovalFieldDto[]
  disabled?: boolean
}>()

const matchOptions = enumOptions(CONDITION_MATCHES, conditionMatchLabelKey)
const operatorOptions = enumOptions(CONDITION_OPERATORS, conditionOperatorLabelKey)

const fieldOptions = computed(() =>
  (props.fields ?? []).map((field) => ({
    value: field.key,
    label:
      field.displayName && field.displayName !== field.key
        ? `${field.displayName}（${field.key}）`
        : field.key,
  })),
)

const addRule = () => {
  condition.value.rules.push({ field: '', operator: 'equals', value: '' })
}

const removeRule = (index: number) => {
  condition.value.rules.splice(index, 1)
}
</script>

<style scoped>
.condition-block {
  padding-top: 12px;
  border-top: 1px dashed var(--theme-divider);
}
.condition-head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}
.block-label {
  color: var(--theme-text-secondary);
  font-size: 13px;
}
.condition-match {
  width: 150px;
}
.condition-hint {
  margin: 10px 0 0;
  color: var(--theme-text-tertiary);
  font-size: 13px;
}
.condition-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-top: 10px;
}
.condition-field {
  width: 200px;
}
.condition-operator {
  width: 170px;
}
.condition-value {
  width: 180px;
}
@media (max-width: 640px) {
  .condition-match,
  .condition-field,
  .condition-operator,
  .condition-value {
    width: 100%;
  }
}
</style>

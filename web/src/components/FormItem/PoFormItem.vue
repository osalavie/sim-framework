<template>
  <component
    :is="gi ? NFormItemGi : NFormItem"
    v-if="hasLabel"
    ref="itemRef"
    v-bind="$attrs"
    :label="label"
  >
    <template #label>
      <PoFieldLabel :text="label" :description="description">
        <slot name="label">{{ label }}</slot>
      </PoFieldLabel>
    </template>
    <slot />
    <template v-if="$slots.feedback" #feedback><slot name="feedback" /></template>
  </component>
  <component :is="gi ? NFormItemGi : NFormItem" v-else ref="itemRef" v-bind="$attrs">
    <slot />
    <template v-if="$slots.feedback" #feedback><slot name="feedback" /></template>
  </component>
</template>

<script setup lang="ts">
import { computed, ref, useSlots } from 'vue'
import { NFormItem, NFormItemGi, type FormItemInst } from 'naive-ui'
import PoFieldLabel from './PoFieldLabel.vue'

// n-grid 只渲染带 __GRID_ITEM__ 标记的直接子节点，包装组件需要透传该标记
defineOptions({ inheritAttrs: false, __GRID_ITEM__: true })

const props = defineProps<{
  label?: string
  /** 标题右侧 info 气泡的简短说明；为空时不显示图标 */
  description?: string
  /** 使用 n-form-item-gi（配合 n-grid 布局） */
  gi?: boolean
}>()

const slots = useSlots()
const itemRef = ref<FormItemInst | null>(null)
const hasLabel = computed(() => Boolean(props.label) || Boolean(slots.label))

defineExpose({
  validate: (trigger?: Parameters<FormItemInst['validate']>[0]) => itemRef.value?.validate(trigger),
  restoreValidation: () => itemRef.value?.restoreValidation(),
})
</script>

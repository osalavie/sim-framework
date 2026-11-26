<template>
  <n-select
    :value="value"
    :options="mergedOptions"
    :multiple="multiple"
    :loading="loading"
    :disabled="disabled"
    filterable
    remote
    clearable
    :clear-filter-after-select="false"
    :max-tag-count="multiple ? 'responsive' : undefined"
    :placeholder="placeholderText"
    @update:value="(next: string | string[] | null) => emit('update:value', next)"
    @focus="void ensureLoaded()"
    @search="search"
  />
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { t } from '@/i18n'
import { useUserDirectory } from '../composables/useUserDirectory'

const props = withDefaults(
  defineProps<{
    value?: string | string[] | null
    multiple?: boolean
    disabled?: boolean
    placeholder?: string
  }>(),
  { value: null, multiple: false, disabled: false, placeholder: '' },
)

const emit = defineEmits<{ 'update:value': [value: string | string[] | null] }>()

const { options, loading, ensureLoaded, search, label } = useUserDirectory()

const placeholderText = computed(() => props.placeholder || t('Approval:User:Placeholder'))

/** 目录未覆盖的已选用户补一个占位选项，避免选择框显示为空。 */
const mergedOptions = computed(() => {
  const known = new Set(options.value.map((item) => item.value))
  const selected = props.multiple
    ? ((props.value as string[] | null) ?? [])
    : [props.value as string | null]
  const extras = selected
    .filter((id): id is string => Boolean(id) && !known.has(id as string))
    .map((id) => ({ label: label(id), value: id }))

  return [...options.value, ...extras]
})

onMounted(() => void ensureLoaded())
</script>

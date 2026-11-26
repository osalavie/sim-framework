<template>
  <n-icon v-bind="$attrs">
    <component :is="iconComponent" />
  </n-icon>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { createIconComponent } from '@/utils/iconLoader'

interface Props {
  /** 图标名称，支持格式：
   * - "HomeOutline" - 使用默认库（ionicons5）
   * - "ionicons5:HomeOutline" - 明确指定库
   * - "carbon:Home" - 使用 Carbon 库
   * - "home-outline" - kebab-case（自动转换）
   */
  name: string
}

const props = defineProps<Props>()

// 创建图标组件
const iconComponent = computed(() => {
  if (!props.name) {
    return null
  }
  return createIconComponent(props.name)
})
</script>

<style scoped></style>

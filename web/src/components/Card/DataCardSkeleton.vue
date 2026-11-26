<template>
  <n-card class="data-card-skeleton" :bordered="true">
    <n-space vertical :size="12">
      <!-- 标题骨架 -->
      <n-skeleton :width="'60%'" :height="24" />

      <!-- 描述骨架 -->
      <n-skeleton :width="'80%'" :height="16" />

      <!-- 标签骨架 -->
      <n-space :size="8">
        <n-skeleton :width="60" :height="24" :sharp="false" />
        <n-skeleton :width="80" :height="24" :sharp="false" />
      </n-space>

      <!-- 元数据骨架 -->
      <n-space vertical :size="8">
        <n-skeleton :width="'100%'" :height="16" />
        <n-skeleton :width="'90%'" :height="16" />
      </n-space>

      <!-- 操作按钮骨架 -->
      <n-space :size="8">
        <n-skeleton :width="60" :height="32" :sharp="false" />
        <n-skeleton :width="60" :height="32" :sharp="false" />
        <n-skeleton :width="60" :height="32" :sharp="false" />
      </n-space>
    </n-space>
  </n-card>
</template>

<script setup lang="ts">
// 卡片骨架屏组件
</script>

<style scoped lang="scss">
.data-card-skeleton {
  height: 100%;
  min-height: 280px;
}
</style>

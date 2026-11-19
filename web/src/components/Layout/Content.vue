<template>
  <n-layout class="admin-content-layout">
    <n-layout-content class="admin-content">
      <Tabs>
        <slot></slot>
      </Tabs>
    </n-layout-content>
  </n-layout>
</template>
<script setup lang="ts">
import { Tabs } from '@/components/Tabs'
</script>

<style scoped>
.admin-content-layout,
.admin-content {
  height: 100%;
  min-height: 0;
  background: transparent !important;
}
</style>
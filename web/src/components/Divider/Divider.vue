<template>
  <div class="divider-container">
    <div class="divider-line"></div>
  </div>
</template>

<script setup lang="ts">
//TODO: 根据属性动态调整样式
</script>

<style scoped lang="scss">
.divider-container {
  width: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
}

.divider-line {
  width: 100%;
  height: 1px;
  background-color: var(--theme-divider);
}
</style>

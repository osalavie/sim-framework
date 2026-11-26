<template>
  <span class="po-field-label">
    <slot>{{ text }}</slot>
    <PoHintIcon v-if="description" :text="description" />
  </span>
</template>

<script setup lang="ts">
import PoHintIcon from './PoHintIcon.vue'

defineProps<{ text?: string; description?: string }>()
</script>

<style scoped>
.po-field-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  max-width: 100%;
  min-width: 0;
}
</style>

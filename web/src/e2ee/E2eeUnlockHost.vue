<template>
  <E2eeUnlockPanel v-if="gate.visible" />
</template>

<script setup lang="ts">
import { defineAsyncComponent } from 'vue'
import { useE2eeUnlockGateState } from './unlockGate'

// 面板包含国密实现与 PBKDF2，按需加载，不进入首屏包。
const E2eeUnlockPanel = defineAsyncComponent(() => import('./E2eeUnlockPanel.vue'))
const gate = useE2eeUnlockGateState()
</script>

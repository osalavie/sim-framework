<template>
  <details>
    <summary @click="report = getPasskeyDiagnosticReport()">{{ t('E2ee:PasskeyDiagnostics') }}</summary>
    <p class="po-small po-muted">{{ t('E2ee:PasskeyDiagnosticsHint') }}</p>
    <n-input :value="report" type="textarea" :rows="8" readonly :aria-label="t('E2ee:PasskeyDiagnostics')" />
  </details>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { t } from '@/i18n'
import { getPasskeyDiagnosticReport } from './passkeyDiagnostics'
const report = ref('')
</script>

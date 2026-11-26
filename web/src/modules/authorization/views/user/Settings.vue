<template>
  <div class="po-page">
    <section class="po-card po-card--cream authz-settings-note">
      <span class="po-sticker">{{ t('Authz:SettingsSticker') }}</span>
      <h3>{{ t('Authz:SettingsNoteTitle') }}</h3>
      <p class="po-small po-muted">{{ t('Authz:SettingsNoteContent') }}</p>
      <div class="po-chip-row">
        <span class="po-pill po-pill--outline">{{ t('Authz:SettingsProfileLabel') }}</span>
        <span class="po-pill po-pill--outline">{{ t('Authz:SettingsSecurityLabel') }}</span>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { t } from '@/i18n'
</script>

<style scoped>
.authz-settings-note {
  align-content: start;
  max-width: 760px;
}

.authz-settings-note h3 {
  margin: 0;
  font-size: 18px;
  font-weight: var(--po-weight-display);
}

.authz-settings-note p {
  margin: 0;
}
</style>

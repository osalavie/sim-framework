<template>
  <footer class="admin-footer">
    <span>{{ appTitle }} · {{ t('Common:AdminWorkspace') }}</span>
    <span v-if="icpLicense">{{ icpLicense }}</span>
  </footer>
</template>
<script setup lang="ts">
import { t } from '@/i18n'
import { siteName } from '@/config/branding'

// AdminLayout 已按原型移除页脚，本组件仅保留给自定义布局/独立页面复用。
const appTitle = siteName
const icpLicense = import.meta.env.VITE_ICP_LICENSE
</script>
<style scoped>
.admin-footer {
  display: flex;
  min-height: 44px;
  flex: 0 0 auto;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 0 20px;
  color: var(--po-ink-soft);
  background: var(--po-paper);
  border-top: var(--po-border);
  font-size: 10px;
  letter-spacing: 0.06em;
}

@media (max-width: 560px) {
  .admin-footer {
    justify-content: center;
    padding: 0 10px;
  }

  .admin-footer span:last-child:not(:first-child) {
    display: none;
  }
}
</style>
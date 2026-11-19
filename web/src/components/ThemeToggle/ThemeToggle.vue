<template>
  <button
    class="po-icon-btn theme-toggle"
    type="button"
    :aria-label="isDark ? t('Theme:Light') : t('Theme:Dark')"
    v-tooltip="isDark ? t('Theme:Light') : t('Theme:Dark')"
    @click="toggleTheme"
  >
    <n-icon :component="isDark ? SunnyOutline : MoonOutline" />
  </button>
</template>

<script setup lang="ts">
import { SunnyOutline, MoonOutline } from '@vicons/ionicons5'
import { useTheme } from '@/hooks/useTheme'
import { t } from '@/i18n'

const { isDark, toggleTheme } = useTheme()
</script>

<style scoped>
.theme-toggle {
  color: var(--po-ink-soft);
  line-height: 1;
}

.theme-toggle:hover {
  color: var(--po-ink);
}
</style>
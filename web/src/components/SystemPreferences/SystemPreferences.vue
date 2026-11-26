<template>
  <n-drawer v-if="!isNarrowScreen" :show="show" placement="right" :width="440" @update:show="updateShow"
    @after-leave="emit('closed')">
    <n-drawer-content :title="t('Theme:SystemPreferences')" closable class="system-preferences-drawer"
      body-content-style="padding: 18px 20px 24px">
      <ThemeStylePicker />
    </n-drawer-content>
  </n-drawer>

  <n-modal v-else :show="show" preset="card" :title="t('Theme:SystemPreferences')"
    :style="{ width: 'min(92vw, 460px)' }" :segmented="{ content: 'soft' }" closable @update:show="updateShow"
    @after-leave="emit('closed')">
    <ThemeStylePicker />
  </n-modal>
</template>

<script setup lang="ts">
import { ThemeStylePicker } from '@/components/ThemeStylePicker'
import { useResponsive } from '@/hooks/useResponsive'
import { t } from '@/i18n'

const show = defineModel<boolean>('show', { default: false })

const emit = defineEmits<{ closed: [] }>()

const { isNarrowScreen } = useResponsive()

function updateShow(value: boolean) {
  show.value = value
}
</script>

<style scoped>
:global(.system-preferences-drawer) {
  border-left: var(--po-border);
  background: var(--po-paper);
  box-shadow: none;
}

:global(.system-preferences-drawer .n-drawer-header) {
  padding: 18px 20px;
  border-bottom: var(--po-border-hair);
}

:global(.system-preferences-drawer .n-drawer-header__main) {
  color: var(--po-ink);
  font-family: var(--po-font-display);
  font-weight: var(--po-weight-display);
}
</style>

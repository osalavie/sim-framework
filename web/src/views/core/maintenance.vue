<template>
  <main class="maintenance-page ink-canvas">
    <section class="maintenance-card">
      <span class="maintenance-mark" aria-hidden="true">S</span>
      <p class="po-kicker">{{ appTitle }}</p>
      <h1>{{ t('Maintenance:Title') }}</h1>
      <p class="maintenance-desc">{{ maintenanceMessage || t('Maintenance:Description') }}</p>
      <footer>{{ appTitle }}</footer>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { t } from '@/i18n'
import { siteName } from '@/config/branding'
import { routeConfig } from '@/config/routeConfig'
import { useSiteStatusStore } from '@/stores/siteStatus'

const router = useRouter()
const siteStatusStore = useSiteStatusStore()

const appTitle = computed(() => siteName.value)
const maintenanceMessage = computed(() => siteStatusStore.maintenanceMode.message)

// 维护开关通过 SignalR 实时下发；关闭后无需用户手动刷新即可回到站点。
watch(
  () => siteStatusStore.maintenanceMode.enabled,
  (enabled) => {
    if (!enabled) {
      void router.replace(routeConfig.customRedirect)
    }
  },
)
</script>

<style scoped>
.maintenance-page {
  display: grid;
  min-height: 100dvh;
  place-items: center;
  padding: 28px 22px;
}

.maintenance-card {
  position: relative;
  display: grid;
  width: min(560px, 100%);
  justify-items: center;
  gap: 12px;
  padding: 48px 38px 40px;
  border: var(--po-border);
  border-radius: var(--po-r-xl);
  background: var(--po-paper);
  box-shadow: var(--po-shadow-hard);
  text-align: center;
}

.maintenance-card .po-kicker {
  justify-self: center;
  color: var(--po-ink-soft);
}

.maintenance-mark {
  display: grid;
  width: 48px;
  height: 48px;
  place-items: center;
  color: var(--po-ink);
  border: var(--po-border);
  border-radius: 14px;
  background: var(--po-coral);
  box-shadow: var(--po-shadow-card);
  transform: rotate(-4deg);
  font-family: var(--po-font-display);
  font-size: 22px;
  font-weight: var(--po-weight-display);
}

.maintenance-card h1 {
  color: var(--po-ink);
  font-size: clamp(24px, 6vw, 32px);
}

.maintenance-desc {
  min-height: 24px;
  color: var(--po-ink-soft);
  font-size: 13.5px;
  line-height: 1.8;
  white-space: pre-line;
}

.maintenance-card footer {
  width: 100%;
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1.5px dashed var(--po-line-mid);
  color: var(--po-ink-faint);
  font-size: 11.5px;
  letter-spacing: 0.06em;
}

@media (max-width: 560px) {
  .maintenance-card {
    padding: 36px 22px 30px;
  }
}
</style>

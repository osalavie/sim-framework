<template>
  <main class="forbidden-page ink-canvas">
    <div class="forbidden-toolbar">
      <ThemeToggle />
      <LocaleSwitcher />
    </div>

    <section class="forbidden-card">
      <p class="po-kicker">{{ t('Route:Forbidden') }}</p>
      <div class="forbidden-code" aria-hidden="true">403</div>
      <h1>{{ t('Route:Forbidden') }}</h1>
      <p class="forbidden-desc">{{ t('Error:ForbiddenHint') }}</p>
      <div class="forbidden-actions">
        <button class="po-btn po-btn--primary" type="button" @click="goHome">
          {{ t('Error:BackHome') }}
        </button>
        <button class="po-btn po-btn--ghost" type="button" @click="goBack">
          {{ t('Error:BackPrevious') }}
        </button>
      </div>
    </section>
  </main>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { routeConfig } from '@/config/routeConfig'
import { t } from '@/i18n'
import { ThemeToggle } from '@/components/ThemeToggle'
import { LocaleSwitcher } from '@/components/LocaleSwitcher'

const router = useRouter()

function goHome() {
  void router.push(routeConfig.adminDefaultRedirect)
}

function goBack() {
  if (window.history.state?.back) {
    router.back()
  } else {
    goHome()
  }
}
</script>

<style scoped>
.forbidden-page {
  position: relative;
  display: grid;
  min-height: 100dvh;
  place-items: center;
  padding: 72px 22px 48px;
}

.forbidden-toolbar {
  position: absolute;
  top: 22px;
  right: 26px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.forbidden-card {
  display: grid;
  width: min(560px, 100%);
  justify-items: center;
  gap: 10px;
  padding: 44px 36px 38px;
  border: var(--po-border);
  border-radius: var(--po-r-xl);
  background: var(--po-paper);
  box-shadow: var(--po-shadow-hard);
  text-align: center;
}

.forbidden-card .po-kicker {
  justify-self: center;
}

.forbidden-code {
  color: var(--po-coral);
  font-family: var(--po-font-display);
  font-size: clamp(76px, 15vw, 118px);
  font-weight: var(--po-weight-display);
  letter-spacing: 0.02em;
  line-height: 0.95;
  -webkit-text-stroke: 2px var(--po-ink);
}

.forbidden-card h1 {
  margin-top: 2px;
  color: var(--po-ink);
  font-size: clamp(22px, 3vw, 28px);
}

.forbidden-desc {
  max-width: 40ch;
  color: var(--po-ink-soft);
  font-size: 13.5px;
  line-height: 1.8;
}

.forbidden-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 10px;
  margin-top: 8px;
}

@media (max-width: 560px) {
  .forbidden-card {
    padding: 32px 20px;
  }

  .forbidden-actions .po-btn {
    flex: 1 1 auto;
  }
}
</style>

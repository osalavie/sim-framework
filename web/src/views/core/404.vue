<template>
  <main
    class="not-found-page"
    :class="{ 'ink-canvas': !embedded, 'not-found-page--embedded': embedded }"
  >
    <div v-if="!embedded" class="not-found-toolbar">
      <ThemeToggle />
      <LocaleSwitcher />
    </div>

    <section class="not-found-card">
      <p class="po-kicker">{{ t('Route:NotFound') }}</p>
      <div class="not-found-code" aria-hidden="true">404</div>
      <h1>{{ t('Route:NotFound') }}</h1>
      <p class="not-found-desc">{{ t('Error:NotFoundHint') }}</p>
      <div class="not-found-actions">
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

interface Props {
  /** 后台兜底路由使用：只占内容区，不自带画布与主题 / 语言切换 */
  embedded?: boolean
}

withDefaults(defineProps<Props>(), { embedded: false })

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
.not-found-page {
  position: relative;
  display: grid;
  min-height: 100dvh;
  place-items: center;
  padding: 72px 22px 48px;
}

/* 后台兜底：只占内容区，跟随页面密度内边距，保留布局框架 */
.not-found-page--embedded {
  min-height: 100%;
  padding: var(--po-density-page-pad-y) clamp(18px, 2.4vw, 30px);
}

.not-found-toolbar {
  position: absolute;
  top: 22px;
  right: 26px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.not-found-card {
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

.not-found-card .po-kicker {
  justify-self: center;
}

.not-found-code {
  color: var(--po-coral);
  font-family: var(--po-font-display);
  font-size: clamp(76px, 15vw, 118px);
  font-weight: var(--po-weight-display);
  letter-spacing: 0.02em;
  line-height: 0.95;
  -webkit-text-stroke: 2px var(--po-ink);
}

.not-found-card h1 {
  margin-top: 2px;
  color: var(--po-ink);
  font-size: clamp(22px, 3vw, 28px);
}

.not-found-desc {
  max-width: 40ch;
  color: var(--po-ink-soft);
  font-size: 13.5px;
  line-height: 1.8;
}

.not-found-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 10px;
  margin-top: 8px;
}

@media (max-width: 560px) {
  .not-found-card {
    padding: 32px 20px;
  }

  .not-found-actions .po-btn {
    flex: 1 1 auto;
  }
}
</style>

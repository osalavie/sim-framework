<template>
  <aside class="auth-brand" :aria-label="siteName">
    <div class="auth-brand__lockup">
      <span class="auth-brand__mark" aria-hidden="true">
        <BrandLogo :size="24" />
      </span>
      <span class="auth-brand__names">
        <strong>{{ siteName }}</strong>
        <span>{{ t('Common:AdminWorkspace') }}</span>
      </span>
    </div>

    <div class="auth-brand__story">
      <Transition name="brand-swap" mode="out-in">
        <div v-if="showProgress && $slots.progress" key="progress" class="auth-brand__progress">
          <slot name="progress" />
        </div>
        <div v-else key="intro" class="auth-brand__intro">
          <span v-if="kicker" class="auth-brand__kicker">{{ kicker }}</span>
          <h1>{{ headline }}</h1>
          <p v-if="description">{{ description }}</p>

          <ul v-if="highlights.length" class="auth-brand__tags">
            <li v-for="item in highlights" :key="item">{{ item }}</li>
          </ul>
        </div>
      </Transition>
    </div>

    <div class="auth-brand__meta">
      <span>© {{ year }} {{ siteName }}</span>
      <span>{{ t('Common:AdminWorkspace') }}</span>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { siteName } from '@/config/branding'
import { BrandLogo } from '@/components/BrandLogo'
import { t } from '@/i18n'

withDefaults(
  defineProps<{
    kicker?: string
    headline: string
    description?: string
    highlights?: string[]
    showProgress?: boolean
  }>(),
  { kicker: '', description: '', highlights: () => [], showProgress: false },
)

const year = new Date().getFullYear()
</script>

<style scoped>
.auth-brand {
  --bp-ink: #ffffff;
  --bp-soft: rgba(255, 255, 255, 0.82);
  --bp-line: rgba(255, 255, 255, 0.3);
  --bp-chip: rgba(255, 255, 255, 0.16);
  position: relative;
  display: flex;
  overflow: hidden;
  flex-direction: column;
  padding: 36px clamp(28px, 4vw, 60px) 30px;
  color: var(--bp-ink);
  background: var(--po-accent);
}

.auth-brand::before {
  position: absolute;
  top: -180px;
  right: -160px;
  width: 460px;
  height: 460px;
  border: 2px solid var(--bp-line);
  border-radius: 50%;
  content: '';
  pointer-events: none;
}

.auth-brand::after {
  position: absolute;
  inset: 0;
  background-image: radial-gradient(rgba(255, 255, 255, 0.22) 1px, transparent 1px);
  background-size: 24px 24px;
  content: '';
  opacity: 0.5;
  pointer-events: none;
}

:root[data-theme='dark'] .auth-brand {
  --bp-ink: #14284b;
  --bp-soft: rgba(20, 40, 75, 0.76);
  --bp-line: rgba(20, 40, 75, 0.26);
  --bp-chip: rgba(20, 40, 75, 0.1);
}

:root[data-theme='dark'] .auth-brand::after {
  background-image: radial-gradient(rgba(20, 40, 75, 0.18) 1px, transparent 1px);
}

.auth-brand__lockup,
.auth-brand__story,
.auth-brand__meta {
  position: relative;
  z-index: 1;
}

.auth-brand__lockup {
  display: flex;
  align-items: center;
  gap: 12px;
}

.auth-brand__mark {
  display: grid;
  width: 44px;
  height: 44px;
  place-items: center;
  flex: none;
  color: var(--po-on-coral);
  border: var(--po-border);
  border-radius: 14px;
  background: var(--po-coral);
  box-shadow: var(--po-shadow-hard);
  transform: rotate(-4deg);
}

.auth-brand__names {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.auth-brand__names strong {
  overflow: hidden;
  font-family: var(--po-font-display);
  font-size: 17px;
  font-weight: var(--po-weight-display);
  line-height: 1.2;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.auth-brand__names span {
  color: var(--bp-soft);
  font-size: 10.5px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.auth-brand__story {
  display: grid;
  max-width: 520px;
  align-content: center;
  flex: 1;
  gap: 16px;
  padding: 48px 0 40px;
}

.auth-brand__kicker {
  width: fit-content;
  padding: 6px 12px;
  border: 1.5px solid var(--bp-line);
  border-radius: 999px;
  background: var(--bp-chip);
  font-size: 11px;
  font-weight: var(--po-weight-bold);
  letter-spacing: 0.08em;
}

.auth-brand__story h1 {
  max-width: 470px;
  color: currentColor;
  font-family: var(--po-font-display);
  font-size: clamp(30px, 3vw, 44px);
  font-weight: var(--po-weight-display);
  line-height: 1.3;
}

.auth-brand__story p {
  max-width: 44ch;
  color: var(--bp-soft);
  font-size: 14px;
  line-height: 1.9;
}

.auth-brand__intro,
.auth-brand__progress {
  display: grid;
  justify-items: start;
  gap: 16px;
}

.auth-brand__progress {
  width: 100%;
  max-width: 430px;
}

.brand-swap-enter-active,
.brand-swap-leave-active {
  transition:
    opacity 260ms ease,
    transform 260ms cubic-bezier(0.2, 0.8, 0.2, 1);
}

.brand-swap-enter-from {
  opacity: 0;
  transform: translateY(14px);
}

.brand-swap-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

.auth-brand__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 9px;
  margin: 6px 0 0;
  padding: 0;
  list-style: none;
}

.auth-brand__tags li {
  padding: 6px 12px;
  border: 1.5px solid var(--bp-line);
  border-radius: 999px;
  background: var(--bp-chip);
  font-size: 12px;
  font-weight: var(--po-weight-bold);
}

.auth-brand__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  color: var(--bp-soft);
  font-size: 11px;
}

@media (max-width: 960px) {
  .auth-brand {
    padding: 26px clamp(20px, 5vw, 40px) 22px;
  }

  .auth-brand__story {
    display: none;
  }

  .auth-brand__meta {
    display: none;
  }
}
</style>

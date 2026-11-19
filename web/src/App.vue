<template>
  <n-config-provider
    :theme="themeStore.currentTheme"
    :theme-overrides="themeStore.currentThemeOverrides"
    :locale="currentLocale"
    :date-locale="currentDateLocale"
  >
    <n-global-style />
    <router-view v-slot="{ Component }">
      <transition name="layout-fade" mode="out-in">
        <component :is="Component" />
      </transition>
    </router-view>
    <ProgressBar :show="showLoading" />
    <E2eeUnlockHost />
    <transition name="layout-overlay">
      <div v-if="showLoading" class="layout-loading-overlay">
        <div class="layout-loading-spinner"></div>
      </div>
    </transition>
  </n-config-provider>
</template>
<script setup lang="ts">
import { computed, defineAsyncComponent, onBeforeUnmount, onMounted, ref, watch, watchEffect } from 'vue'
import { dateEnUS, dateZhCN, enUS, NGlobalStyle, zhCN } from 'naive-ui'
import { useThemeStore } from './stores/theme'
import { useDensityStore } from './stores/density'
import { useSiteStatusStore } from './stores/siteStatus'
import { useUserStore } from './stores/user'
import { useUserPreferencesStore } from './stores/userPreferences'
import { siteDefaults } from './config/branding'
import { locale } from './i18n'
import { useRouter } from 'vue-router'
import ProgressBar from '@/components/Loading/ProgressBar.vue'

// E2EE 解锁宿主只在需要时加载，避免国密实现进入首屏包。
const E2eeUnlockHost = defineAsyncComponent(() => import('@/e2ee/E2eeUnlockHost.vue'))
import { useNavigationLoading, stopNavigationLoading } from '@/hooks/useNavigationLoading'

const themeStore = useThemeStore()
const densityStore = useDensityStore()
const siteStatusStore = useSiteStatusStore()
const userStore = useUserStore()
const userPreferencesStore = useUserPreferencesStore()

// 维护开关由服务端通过 SignalR 推送，全站共享同一份 store 状态。
onMounted(() => siteStatusStore.startRealtime())
onBeforeUnmount(() => void siteStatusStore.stopRealtime())

watch(
  siteDefaults.defaultTheme,
  (configuredTheme) => {
    if (!configuredTheme) return
    themeStore.applyConfiguredTheme(configuredTheme)
  },
  { immediate: true },
)

// 登录后加载一次服务端偏好（刷新时由路由守卫提前加载）；
// 切换账号时重新加载，退出登录后停止自动保存。
watch(
  () => userStore.user?.id,
  (userId) => {
    if (userId) {
      void userPreferencesStore.load()
    } else {
      userPreferencesStore.reset()
    }
  },
)

// 用户在前端切换语言 / 明暗 / 主题色后自动同步；sync 保证应用服务端偏好时
// 能看到 applyingRemote 标记，不会把刚拉取的偏好原样回写。
watch(
  [() => themeStore.mode, () => themeStore.themeStyle, () => locale.value],
  () => userPreferencesStore.scheduleSave(),
  { flush: 'sync' },
)
const currentLocale = computed(() => (locale.value === 'zh-CN' ? zhCN : enUS))
const currentDateLocale = computed(() => (locale.value === 'zh-CN' ? dateZhCN : dateEnUS))
const router = useRouter()
const { isNavigating } = useNavigationLoading()

// Track initial app readiness — show loading overlay until the very first route resolves
const appReady = ref(false)
const showLoading = computed(() => isNavigating.value || !appReady.value)

router.isReady().then(() => {
  appReady.value = true
  stopNavigationLoading()
})

// Safety: if any navigation fails, ensure loading state is cleared
router.onError(() => {
  stopNavigationLoading()
})

// 注入 CSS 变量到根元素，让所有组件都能使用
watchEffect(() => {
  const colors = themeStore.themeColors
  const root = document.documentElement

  for (const [name, value] of Object.entries(themeStore.cssTokens)) {
    root.style.setProperty(name, value)
  }

  root.style.setProperty('--theme-primary', colors.primary)
  root.style.setProperty('--theme-primary-hover', colors.primaryHover)
  root.style.setProperty('--theme-primary-pressed', colors.primaryPressed)
  root.style.setProperty('--theme-info', colors.info)
  root.style.setProperty('--theme-success', colors.success)
  root.style.setProperty('--theme-warning', colors.warning)
  root.style.setProperty('--theme-error', colors.error)
  root.style.setProperty('--theme-text-base', colors.textBase)
  root.style.setProperty('--theme-text-secondary', colors.textSecondary)
  root.style.setProperty('--theme-text-tertiary', colors.textTertiary)
  root.style.setProperty('--theme-text-disabled', colors.textDisabled)
  root.style.setProperty('--theme-bg-base', colors.bgBase)
  root.style.setProperty('--theme-bg-secondary', colors.bgSecondary)
  root.style.setProperty('--theme-bg-tertiary', colors.bgTertiary)
  root.style.setProperty('--theme-border', colors.border)
  root.style.setProperty('--theme-border-hover', colors.borderHover)
  root.style.setProperty('--theme-divider', colors.divider)
  root.dataset.theme = themeStore.isDark ? 'dark' : 'light'
  root.dataset.style = themeStore.styleDefinition.id
  root.dataset.skin = themeStore.skin
  root.dataset.density = densityStore.density
})
</script>

<style>
.layout-fade-enter-active,
.layout-fade-leave-active {
  transition: opacity 200ms ease;
}

.layout-fade-enter-from,
.layout-fade-leave-to {
  opacity: 0;
}

.layout-loading-overlay {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: grid;
  place-items: center;
  background: var(--po-bg, #fff9ee);
  background-image: radial-gradient(var(--po-dot, rgba(20, 40, 75, 0.09)) 1px, transparent 1px);
  background-size: 26px 26px;
}

.layout-loading-spinner {
  width: 38px;
  height: 38px;
  border: 3px solid var(--po-line, #d9e2f1);
  border-top-color: var(--po-coral, #ff7a6b);
  border-radius: 50%;
  animation: layout-spin 0.7s linear infinite;
}

@keyframes layout-spin {
  to {
    transform: rotate(360deg);
  }
}

.layout-overlay-enter-active,
.layout-overlay-leave-active {
  transition: opacity 250ms ease;
}

.layout-overlay-enter-from,
.layout-overlay-leave-to {
  opacity: 0;
}
</style>

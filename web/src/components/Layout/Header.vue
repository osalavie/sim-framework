<template>
  <header class="admin-header">
    <div class="header-container">
      <div class="header-main">
        <button
          class="po-icon-btn header-nav-toggle"
          type="button"
          :aria-label="t('Header:ToggleNavigation')"
          :aria-expanded="!collapsed"
          @click="toggleSidebar"
        >
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.9"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M4 7h16" />
            <path d="M4 12h16" />
            <path d="M4 17h10" />
          </svg>
        </button>

        <div class="topbar-heading">
          <h1 class="topbar-title">{{ currentRouteTitle }}</h1>
        </div>
      </div>

      <div class="user-info-area">
        <DensityToggle />

        <LocaleSwitcher :compact="compactLocale" />

        <ThemeToggle />

        <n-popover
          trigger="click"
          placement="bottom-end"
          @update:show="handleNotificationPanelVisibility"
        >
          <template #trigger>
            <button
              class="po-icon-btn notification-action"
              type="button"
              :aria-label="t('Header:Notifications')"
            >
              <n-icon :component="NotificationsIcon" />
              <span
                v-if="notifications.unreadCount"
                class="notification-dot"
                aria-hidden="true"
              ></span>
            </button>
          </template>
          <section class="notification-panel" :aria-label="t('Header:Notifications')">
            <button
              v-for="item in notifications.items"
              :key="item.id"
              type="button"
              class="notification-item"
              :class="{ unread: !item.isRead }"
              :aria-label="`${item.title}，${t(item.isRead ? 'Header:Read' : 'Header:Unread')}`"
              @click="openNotification(item)"
            >
              <span class="notification-item-heading">
                <strong>{{ item.title }}</strong>
                <small class="notification-state" :class="{ unread: !item.isRead }">
                  <i aria-hidden="true"></i>
                  {{ t(item.isRead ? 'Header:Read' : 'Header:Unread') }}
                </small>
              </span>
              <span>{{ item.body }}</span>
            </button>
            <p v-if="!notifications.items.length">{{ t('Header:NoNotifications') }}</p>
            <n-button
              v-if="notifications.unreadCount"
              class="browser-notification-button"
              secondary
              @click="markAllRead"
            >
              {{ t('Header:MarkAllRead') }}
            </n-button>
            <n-button
              v-if="canEnableBrowserNotifications"
              class="browser-notification-button"
              secondary
              @click="enableBrowserNotifications"
            >
              {{ t('Header:EnableBrowserNotifications') }}
            </n-button>
          </section>
        </n-popover>

        <span class="header-divider" aria-hidden="true"></span>

        <n-popover
          v-model:show="accountMenuShow"
          trigger="click"
          placement="bottom-end"
          :style="{ padding: '6px' }"
        >
          <template #trigger>
            <button
              class="account-chip"
              type="button"
              :aria-label="t('Header:OpenAccountMenu')"
            >
              <LazyImage
                v-if="userStore.user?.avatar"
                class="user-avatar user-avatar--image"
                :src="userStore.user.avatar"
                :alt="userInitial"
              />
              <span v-else class="user-avatar user-avatar--fallback">{{ userInitial }}</span>
              <span class="account-copy">
                <n-tooltip trigger="hover" :disabled="nameTooltipDisabled">
                  <template #trigger>
                    <strong ref="nameEl" @mouseenter="checkNameTooltip">{{ displayName }}</strong>
                  </template>
                  {{ displayName }}
                </n-tooltip>
                <small>{{ accountName }}</small>
              </span>
              <n-icon class="account-chevron" :component="ChevronDownOutline" />
            </button>
          </template>
          <n-menu :indent="12" :options="personalOptions" @update:value="handleMenuSelect" />
        </n-popover>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { NIcon } from 'naive-ui'
import { computed, h, onBeforeUnmount, onMounted, ref, type Component } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import {
  ChevronDownOutline,
  NotificationsOutline as NotificationsIcon,
} from '@vicons/ionicons5'
import { useUserStore } from '@/stores/user'
import LazyImage from '@/components/LazyImage.vue'
import { useTruncatedTooltip } from '@/hooks'
import { Logout, Security as SecurityIcon, UserProfile } from '@vicons/carbon'
import { DensityToggle } from '@/components/DensityToggle'
import { LocaleSwitcher } from '@/components/LocaleSwitcher'
import { ThemeToggle } from '@/components/ThemeToggle'
import { useAdminShell } from '@/components/Layout/useAdminShell'
import { t } from '@/i18n'
import {
  userNotificationApi,
  type UserNotification,
  type UserNotificationSummary,
} from '@/modules/notifications/api'
import {
  createUserNotificationRealtimeConnection,
  type UserNotificationRealtimeConnection,
} from '@/modules/notifications/realtime'
import { resolveNotificationPresentationTarget } from '@/modules/notifications/presentation'
import { useDiscreteApi } from '@/hooks'
import { useHeaderTitleOverride } from '@/hooks/headerTitleOverride'
import { useMediaQuery } from '@/hooks/useResponsive'

const compactLocale = useMediaQuery('(max-width: 768px)')

const { collapsed, toggleSidebar } = useAdminShell()
const userStore = useUserStore()
const router = useRouter()
const route = useRoute()
const { headerTitleOverride } = useHeaderTitleOverride()
const currentRouteTitle = computed(() => {
  if (headerTitleOverride.value) return headerTitleOverride.value
  const title = route.meta.title
  return typeof title === 'string' && title.trim() ? t(title) : t('Header:Overview')
})
const displayName = computed(() => userStore.user?.nickName || t('Header:Account'))
// 昵称气泡仅在 CSS 截断（超出 120px 显示省略号）时启用，短昵称不弹气泡
const {
  targetEl: nameEl,
  disabled: nameTooltipDisabled,
  check: checkNameTooltip,
} = useTruncatedTooltip()
const accountName = computed(() => userStore.user?.userName || t('Header:Account'))
const userInitial = computed(() => displayName.value.trim().charAt(0).toUpperCase() || 'S')
const notifications = ref<UserNotificationSummary>({ unreadCount: 0, items: [] })
const browserPermission = ref<NotificationPermission | 'unsupported'>(
  typeof Notification === 'undefined' ? 'unsupported' : Notification.permission,
)
// 仅当浏览器通知能力可用且权限尚未确定时，引导用户授权。
// permission === 'granted' / 'denied' / 'unsupported' 时不再展示提示按钮。
const canEnableBrowserNotifications = computed(
  () => typeof Notification !== 'undefined' && browserPermission.value === 'default',
)
let notificationLoadPromise: Promise<void> | undefined
let notificationRealtime: UserNotificationRealtimeConnection | undefined
let notificationsInitialized = false

function presentNotification(item: UserNotification) {
  const target = resolveNotificationPresentationTarget({
    initialized: notificationsInitialized,
    pageVisible: document.visibilityState === 'visible',
    pageFocused: document.hasFocus(),
    browserPermission: browserPermission.value,
  })
  if (target === 'in-app') {
    useDiscreteApi.notification.info({
      title: item.title,
      content: item.body,
      duration: 5_000,
      keepAliveOnHover: true,
    })
    return
  }
  if (target === 'browser') {
    const notification = new Notification(item.title, {
      body: item.body,
      tag: item.id,
    })
    notification.onclick = () => {
      window.focus()
      notification.close()
      void openNotification(item)
    }
  }
}

function loadNotifications() {
  if (notificationLoadPromise) return notificationLoadPromise
  notificationLoadPromise = (async () => {
    try {
      const previousIds = new Set(notifications.value.items.map((item) => item.id))
      const value = await userNotificationApi.get()
      notifications.value = value
      const latest = value.items.find((item) => !item.isRead && !previousIds.has(item.id))
      if (latest) presentNotification(latest)
      notificationsInitialized = true
    } catch {
      // Header notifications degrade quietly when the realtime channel/API is unavailable.
    } finally {
      notificationLoadPromise = undefined
    }
  })()
  return notificationLoadPromise
}

async function markAllRead() {
  notifications.value = {
    unreadCount: 0,
    items: notifications.value.items.map((item) => ({ ...item, isRead: true })),
  }
  try {
    await userNotificationApi.markAllRead()
  } catch {
    void loadNotifications()
  }
}

async function openNotification(item: UserNotification) {
  if (!item.isRead) {
    item.isRead = true
    notifications.value.unreadCount = Math.max(0, notifications.value.unreadCount - 1)
    try {
      await userNotificationApi.markRead(item.id)
    } catch {
      void loadNotifications()
    }
  }
  if (item.link) await router.push(item.link)
}

function handleNotificationPanelVisibility(show: boolean) {
  if (show) void loadNotifications()
}

async function enableBrowserNotifications() {
  if (typeof Notification !== 'undefined') {
    browserPermission.value = await Notification.requestPermission()
  }
}

// 账号菜单：仅点击头像胶囊展开，选中菜单项后显式关闭
const accountMenuShow = ref(false)

const personalOptions = [
  {
    label: () =>
      h(
        RouterLink,
        {
          to: { name: 'UserProfile' },
        },
        { default: () => t('Header:Profile') },
      ),
    key: 'profile',
    icon: renderIcon(UserProfile),
  },
  {
    label: () =>
      h(
        RouterLink,
        {
          to: { name: 'UserSecurity' },
        },
        { default: () => t('Header:Security') },
      ),
    key: 'security',
    icon: renderIcon(SecurityIcon),
  },
  {
    label: () => t('Header:SignOut'),
    key: 'logout',
    icon: renderIcon(Logout),
  },
]

function renderIcon(icon: Component) {
  return () => h(NIcon, null, { default: () => h(icon) })
}

async function handleMenuSelect(key: string) {
  accountMenuShow.value = false
  if (key === 'logout') {
    await userStore.logout()
    await router.push({ name: 'Login' })
  }
}

onMounted(() => {
  notificationRealtime = createUserNotificationRealtimeConnection(loadNotifications)
  void notificationRealtime.start()
  // 权限异步初始化的兜底：极少数浏览器在脚本初次读取时还未返回权限，
  // 在挂载后再读一次，确保刷新页面后状态与浏览器真实授权情况一致。
  if (typeof Notification !== 'undefined') {
    browserPermission.value = Notification.permission
  }
})
onBeforeUnmount(() => {
  void notificationRealtime?.stop()
})
</script>
<style scoped>
/* 顶栏：纸底实色 + 海军蓝 2px 底线（无模糊 / 无玻璃拟态） */
.admin-header {
  height: var(--po-header-h, 76px);
  flex: 0 0 var(--po-header-h, 76px);
  background: var(--po-paper);
  border-bottom: var(--po-border);
}

.header-container {
  display: flex;
  height: var(--po-header-h, 76px);
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  padding: 0 22px;
}

.header-main {
  display: flex;
  min-width: 0;
  flex: 1 1 auto;
  align-items: center;
  gap: 10px;
  overflow: hidden;
}

.header-nav-toggle {
  color: var(--po-ink);
}

.topbar-heading {
  display: grid;
  min-width: 0;
  overflow: hidden;
  gap: 1px;
}

.topbar-title {
  overflow: hidden;
  margin: 0;
  color: var(--po-ink);
  font-family: var(--po-font-display);
  font-size: 19px;
  font-weight: var(--po-weight-display);
  line-height: 1.2;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-info-area {
  display: flex;
  min-width: 0;
  flex: 0 0 auto;
  align-items: center;
  gap: 6px;
}

.notification-action {
  position: relative;
}

.notification-dot {
  position: absolute;
  top: 7px;
  right: 7px;
  width: 9px;
  height: 9px;
  border: var(--po-border-thin);
  border-radius: 50%;
  background: var(--po-coral);
}

.notification-panel {
  /* 右侧还有主题 / 账号按钮，按铃铛左侧可用宽度收窄，避免窄屏向左出屏 */
  width: min(360px, calc(100vw - 108px));
  padding: 6px 4px;
}

.notification-item-heading {
  display: flex;
  min-width: 0;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.notification-panel > p {
  margin: 14px 0;
  color: var(--po-ink-faint);
  text-align: center;
}

/* 窄屏面板收窄后，英文按钮文案允许折行而不是被裁切 */
.notification-panel :deep(.n-button) {
  max-width: 100%;
  height: auto;
  min-height: var(--n-height);
  white-space: normal;
}

.notification-panel :deep(.n-button__content) {
  padding-block: calc((var(--n-height) - 1.4em) / 2);
  line-height: 1.4;
  white-space: normal;
  overflow-wrap: anywhere;
}

.notification-item {
  display: grid;
  width: 100%;
  gap: 4px;
  padding: 11px 12px;
  color: inherit;
  border: 0;
  border-radius: 10px;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.notification-item:hover,
.notification-item:focus-visible {
  background: var(--po-accent-soft);
}

.notification-item:focus-visible {
  outline: 3px solid var(--po-accent);
  outline-offset: 1px;
}

.notification-item > span:not(.notification-item-heading) {
  overflow: hidden;
  color: var(--po-ink-soft);
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notification-item-heading strong {
  overflow: hidden;
  min-width: 0;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notification-state {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  flex: 0 0 auto;
  color: var(--po-ink-faint);
  font-size: 11px;
  font-weight: 500;
}

.notification-state i {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--po-ink-faint);
}

.notification-state.unread {
  color: var(--po-accent);
  font-weight: var(--po-weight-bold);
}

.notification-state.unread i {
  background: var(--po-accent);
}

.browser-notification-button {
  width: 100%;
  min-height: 38px;
  margin-top: 8px;
}

.header-divider {
  width: 1.5px;
  height: 28px;
  margin: 0 6px;
  background: var(--po-line-mid);
}

/* 账号胶囊（对齐原型 .user-chip） */
.account-chip {
  display: flex;
  height: 44px;
  min-width: 0;
  align-items: center;
  gap: 9px;
  padding: 0 12px 0 5px;
  color: var(--po-ink);
  border: 2px solid var(--po-line-mid);
  border-radius: 999px;
  background: var(--po-paper);
  cursor: pointer;
  transition:
    border-color 0.12s ease,
    box-shadow 0.12s ease;
}

.account-chip:hover,
.account-chip:focus-visible {
  border-color: var(--po-ink);
  box-shadow: 2px 2px 0 var(--po-line);
}

.account-chip:focus-visible {
  outline: 3px solid var(--po-accent);
  outline-offset: 2px;
}

.user-avatar {
  display: grid;
  width: 32px;
  height: 32px;
  flex: 0 0 auto;
  place-items: center;
  overflow: hidden;
  border-radius: 50%;
  color: var(--po-on-bright);
  border: var(--po-border);
  background: var(--po-sun);
  font-family: var(--po-font-display);
  font-size: 12.5px;
  font-weight: var(--po-weight-display);
}

.user-avatar--image {
  display: block;
  object-fit: cover;
}

.account-copy {
  display: grid;
  min-width: 82px;
  text-align: left;
}

.account-copy strong {
  overflow: hidden;
  max-width: 120px;
  color: var(--po-ink);
  font-size: 12.5px;
  font-weight: var(--po-weight-bold);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.account-copy small {
  color: var(--po-ink-faint);
  font-size: 10.5px;
}

.account-chevron {
  color: var(--po-ink-faint);
  font-size: 13px;
}

@media (max-width: 900px) {
  .account-copy,
  .account-chevron {
    display: none;
  }
}

@media (max-width: 680px) {
  .admin-header {
    --po-header-h: 64px;
  }

  .header-container {
    gap: 10px;
    padding: 0 12px;
  }

  .topbar-title {
    font-size: 17px;
  }

  .header-divider {
    display: none;
  }

  /* 窄屏优先保留语言 / 主题 / 账号入口，密度开关收起 */
  .density-toggle {
    display: none;
  }

  .account-chip {
    padding: 0 4px;
  }
}

@media (max-width: 380px) {
  .header-container {
    gap: 8px;
    padding: 0 10px;
  }

  .topbar-title {
    font-size: 16px;
  }

  .user-info-area {
    gap: 4px;
  }
}
</style>
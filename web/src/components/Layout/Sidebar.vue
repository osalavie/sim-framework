<template>
  <n-layout-sider
    bordered
    :native-scrollbar="false"
    collapse-mode="width"
    :collapsed-width="76"
    :width="252"
    :collapsed="collapsed"
    :show-trigger="false"
    content-class="sidebar-scroll-content"
    class="admin-sidebar"
    @update:collapsed="setCollapsed"
  >
    <div class="sidebar-inner">
      <div class="sidebar-brand">
        <span class="sidebar-brand__mark" aria-hidden="true">
          <BrandLogo :size="24" />
        </span>
        <span v-if="!collapsed" class="sidebar-brand__text">
          <strong>{{ siteTitle }}</strong>
          <span>{{ t('Common:AdminWorkspace') }}</span>
        </span>
        <button
          v-if="!collapsed"
          class="sidebar-close"
          type="button"
          :aria-label="t('Common:Close')"
          @click="setCollapsed(true)"
        >
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            aria-hidden="true"
          >
            <path d="M6 6l12 12" />
            <path d="M18 6L6 18" />
          </svg>
        </button>
      </div>
      <div class="sidebar-menu">
        <Menu :collapsed="collapsed" @select="handleMenuSelect" />
      </div>
      <div class="sidebar-preferences-wrap" :class="{ 'is-collapsed': collapsed }">
        <button type="button" class="sidebar-preferences" :class="{ 'is-collapsed': collapsed }"
          :aria-label="t('Theme:OpenPreferences')" aria-haspopup="dialog"
          v-tooltip="collapsed ? t('Theme:OpenPreferences') : undefined" @click="openPreferences">
          <n-icon class="sidebar-preferences__icon" :component="Settings" :size="20" />
          <span v-if="!collapsed">{{ t('Theme:OpenPreferences') }}</span>
        </button>
      </div>
      <SystemPreferences v-model:show="showPreferences" @closed="handlePreferencesClosed" />
    </div>
  </n-layout-sider>
</template>
<script setup lang="ts">
import { computed, ref } from 'vue'
import { Settings } from '@vicons/carbon'
import { BrandLogo } from '@/components/BrandLogo'
import { Menu } from '@/components/Menu'
import { SystemPreferences } from '@/components/SystemPreferences'
import { useAdminShell } from '@/components/Layout/useAdminShell'
import { t } from '@/i18n'
import { siteName } from '@/config/branding'

const { collapsed, isNarrowScreen, setCollapsed } = useAdminShell()
const showPreferences = ref(false)

function openPreferences() {
  showPreferences.value = true
  if (isNarrowScreen.value) setCollapsed(true)
}

// 窄屏入口随侧栏一起收起，关闭弹窗后把焦点交给顶栏汉堡按钮以便重新打开导航
function handlePreferencesClosed() {
  if (!isNarrowScreen.value) return
  document.querySelector<HTMLElement>('.header-nav-toggle')?.focus()
}

// 窄屏点选菜单后立即收起抽屉（含再次点击当前路由的情况）
function handleMenuSelect() {
  if (isNarrowScreen.value) setCollapsed(true)
}
// 品牌块只显示产品名；环境后缀（如 “(Development)”）在窄侧栏里会挤掉副标题
const siteTitle = computed(() => {
  const raw = siteName.value
  return raw.replace(/\s*\([^)]*\)\s*$/, '').trim() || raw
})
</script>
<style scoped>
.admin-sidebar {
  background: var(--po-paper) !important;
  border-right: var(--po-border) !important;
  box-shadow: none;
}

/* 侧栏内容锁定为视口高度，只有菜单区滚动，页脚不再随内容滚走 */
.admin-sidebar :deep(.sidebar-scroll-content) {
  display: flex;
  height: 100%;
  flex-direction: column;
}

.sidebar-inner {
  display: flex;
  flex: 1;
  min-height: 0;
  flex-direction: column;
  gap: 12px;
}

/* 菜单区独立滚动，系统设置始终停靠侧栏底边；使用原生滚动条占位，浮层滚动条会压住菜单项右侧描边 */
.sidebar-menu {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overscroll-behavior: contain;
}

/* 品牌块：珊瑚底品牌 Logo 贴纸 + 展示字体（对齐原型 .brand / .brand__mark） */
.sidebar-brand {
  display: flex;
  min-height: 60px;
  align-items: center;
  flex: none;
  gap: 11px;
  padding: 8px 4px;
  border-bottom: 1.5px dashed var(--po-line-mid);
}

.sidebar-brand__mark {
  display: grid;
  width: 44px;
  height: 44px;
  place-items: center;
  flex: none;
  color: var(--po-on-coral);
  border: var(--po-border);
  border-radius: 14px;
  background: var(--po-coral);
  box-shadow: var(--po-shadow-card);
  transform: rotate(-4deg);
}

.sidebar-brand__text {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.sidebar-brand__text strong {
  overflow: hidden;
  color: var(--po-ink);
  font-family: var(--po-font-display);
  font-size: 17px;
  font-weight: var(--po-weight-display);
  line-height: 1.2;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sidebar-brand__text span {
  overflow: hidden;
  color: var(--po-ink-soft);
  font-size: 10.5px;
  letter-spacing: 0.03em;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.admin-sidebar.n-layout-sider--collapsed .sidebar-brand {
  justify-content: center;
  padding-right: 0;
  padding-left: 0;
}

.admin-sidebar.n-layout-sider--collapsed .sidebar-brand__mark {
  width: 40px;
  height: 40px;
}

:deep(.n-menu-item-content-header) {
  font-weight: var(--po-weight-bold);
}

/* 菜单项超长文字截断 + hover 显示完整名（完整名由 menus.ts 按需设置 title） */
:deep(.menu-item-label) {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

:deep(.n-menu-item-group-title) {
  color: var(--po-ink-faint);
  font-size: 11px;
  letter-spacing: 0.12em;
}

/* 子菜单容器为折叠动画保留 overflow: hidden；选中项的下投影会伸出末项 2px，
   留出底部空间，避免分组最后一项的下边框被裁切 */
:deep(.n-menu .n-submenu .n-submenu-children) {
  padding-bottom: 3px;
}

/* 系统设置作为侧栏页脚：纯文字入口，融入纸面，不使用按钮底板 */
.sidebar-preferences-wrap {
  flex: none;
  padding: 10px 0 12px;
  border-top: 1.5px dashed var(--po-line-mid);
}

.sidebar-preferences-wrap.is-collapsed {
  display: flex;
  justify-content: center;
}

.sidebar-preferences {
  display: flex;
  width: 100%;
  height: 42px;
  align-items: center;
  gap: 10px;
  padding: 0 18px 0 32px;
  color: var(--po-ink-soft);
  border: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
  font: inherit;
  font-size: 14px;
  font-weight: 600;
  line-height: 1.2;
  text-align: left;
  cursor: pointer;
  transition:
    color 160ms ease,
    background-color 160ms ease;
}

.sidebar-preferences__icon {
  transition: transform 520ms cubic-bezier(0.22, 0.61, 0.36, 1);
}

/* 进入时逆时针转一圈，离开时按原路径顺时针转回 */
.sidebar-preferences:hover .sidebar-preferences__icon {
  transform: rotate(-360deg);
}

.sidebar-preferences:hover {
  color: var(--po-ink);
  background: var(--po-accent-soft);
}

.sidebar-preferences:focus-visible {
  outline: 3px solid var(--po-coral);
  outline-offset: -3px;
}

.sidebar-preferences.is-collapsed {
  width: 44px;
  justify-content: center;
  padding: 0;
}

/* ≤768px：折叠即完全隐藏；展开时以覆盖式抽屉呈现，不挤压内容 */
@media (max-width: 768px) {
  .admin-sidebar.n-layout-sider--collapsed {
    width: 0 !important;
    min-width: 0 !important;
    max-width: 0 !important;
    border-right: 0 !important;
    overflow: hidden;
    /* 收起时不进入 Tab 顺序，也不交给读屏器朗读 */
    visibility: hidden;
  }

  .admin-sidebar:not(.n-layout-sider--collapsed) {
    position: fixed;
    z-index: 60;
    top: 0;
    bottom: 0;
    left: 0;
    width: min(252px, 78vw) !important;
    min-width: 0 !important;
    max-width: none !important;
    height: 100dvh;
    border-right: var(--po-border) !important;
    box-shadow: 6px 0 0 rgba(20, 40, 75, 0.14);
  }

  .sidebar-preferences-wrap {
    padding-bottom: calc(12px + env(safe-area-inset-bottom));
  }
}

/* 移动端抽屉内置关闭入口（抽屉会覆盖顶栏汉堡按钮） */
.sidebar-close {
  display: none;
  width: 36px;
  height: 36px;
  flex: none;
  margin-left: auto;
  place-items: center;
  padding: 0;
  color: var(--po-ink);
  border: 2px solid var(--po-line-mid);
  border-radius: 10px;
  background: var(--po-paper);
  cursor: pointer;
}

.sidebar-close svg {
  width: 18px;
  height: 18px;
}

.sidebar-close:hover,
.sidebar-close:focus-visible {
  border-color: var(--po-ink);
  box-shadow: 2px 2px 0 var(--po-line);
}

@media (max-width: 768px) {
  .sidebar-close {
    display: grid;
  }
}
</style>
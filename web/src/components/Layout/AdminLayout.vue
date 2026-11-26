<template>
  <div class="admin-shell ink-canvas" :class="{ 'is-focus-mode': focusMode }">
    <n-layout class="admin-layout" has-sider>
      <div
        v-if="showSidebarBackdrop"
        class="sidebar-backdrop"
        aria-hidden="true"
        @click="setCollapsed(true)"
      ></div>
      <Sidebar />
      <n-layout class="admin-main">
        <Header />
        <Content>
          <main id="admin-main-content" class="router-view-wrapper">
            <router-view v-slot="{ Component, route }">
              <transition name="fade" mode="out-in">
                <component
                  :is="Component"
                  v-if="shouldDelegateKeepAlive(route)"
                  :key="getAdminRouteViewKey(route)"
                />
                <keep-alive v-else-if="route.meta.keepAlive" :max="10">
                  <component :is="Component" :key="route.fullPath" />
                </keep-alive>
                <component :is="Component" v-else />
              </transition>
            </router-view>
          </main>
        </Content>
      </n-layout>
    </n-layout>
  </div>
</template>
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { Content, Header, Sidebar } from '@/components/Layout'
import { provideAdminShell } from '@/components/Layout/useAdminShell'
import { useAdminFocusMode } from '@/hooks/adminFocusMode'
import { getAdminRouteViewKey, shouldDelegateKeepAlive } from '@/utils/routeViewCache'
import { initInkRipple } from '@/utils/inkRipple'

// 壳层折叠状态（顶栏汉堡按钮与侧栏共享）。用 provide 的返回值，
// 因为 inject() 不会读取当前组件自身 provide 的内容。
const { collapsed, isNarrowScreen, setCollapsed } = provideAdminShell()
const { focusMode } = useAdminFocusMode()
const route = useRoute()

// 窄屏下侧栏是覆盖式抽屉，带遮罩；宽屏下不渲染遮罩
const showSidebarBackdrop = computed(() => isNarrowScreen.value && !collapsed.value)

// 抽屉打开时点击导航即收起，避免移动端切页后仍停在菜单上
watch(
  () => route.fullPath,
  () => {
    if (isNarrowScreen.value && !collapsed.value) setCollapsed(true)
  },
)

function handleEscape(event: KeyboardEvent) {
  if (event.key === 'Escape' && showSidebarBackdrop.value) setCollapsed(true)
}

// 电光蓝涟漪（导航与自定义按钮；旧工具函数名保留）
let disposeInkRipple: (() => void) | undefined
onMounted(() => {
  disposeInkRipple = initInkRipple()
  window.addEventListener('keydown', handleEscape)
})
onBeforeUnmount(() => {
  disposeInkRipple?.()
  window.removeEventListener('keydown', handleEscape)
})
</script>
<style scoped>
.admin-shell {
  width: 100%;
  height: 100dvh;
  overflow: hidden;
}

/* 画布点阵由 .ink-canvas 提供，n-layout 一律透明，让纸底点阵透出 */
.admin-layout {
  width: 100%;
  height: 100%;
  background: transparent;
}

/* 右侧主列：顶栏 + 标签页 + 内容画布，纵向撑满 */
.admin-main {
  display: flex;
  height: 100%;
  min-width: 0;
  flex-direction: column;
  background: transparent;
}

.admin-main :deep(.n-layout-scroll-container) {
  display: flex;
  min-height: 0;
  flex: 1;
  flex-direction: column;
}

.router-view-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
}

/* 专注模式：文档编辑页独占画布，隐藏导航壳；页面仍保留自身的顶栏与底栏 */
.admin-shell.is-focus-mode :deep(.admin-header),
.admin-shell.is-focus-mode :deep(.admin-sidebar),
.admin-shell.is-focus-mode :deep(.tabs-header) {
  display: none;
}

.admin-shell.is-focus-mode .admin-main :deep(.tabs-content) {
  padding: 0;
}

/* 窄屏抽屉遮罩：点按空白处收起导航 */
.sidebar-backdrop {
  position: fixed;
  z-index: 55;
  inset: 0;
  background: rgba(20, 40, 75, 0.38);
  backdrop-filter: blur(1.5px);
}

.fade-enter-active {
  transition:
    opacity 180ms ease,
    transform 180ms ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0.01;
}

.fade-enter-from {
  transform: translateY(4px);
}

.fade-enter-to,
.fade-leave-from {
  opacity: 1;
}

.route-container {
  width: 100%;
  height: 100%;
}
</style>
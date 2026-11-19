<template>
  <div class="tabs-container">
    <div ref="tabsHeaderRef" class="tabs-header">
      <n-tabs
        ref="tabsInstRef"
        type="bar"
        size="small"
        :value="currentTab"
        @update:value="handleUpdate"
      >
        <n-tab
          v-for="tab in tabs"
          :key="tab.path"
          :name="tab.name"
          @contextmenu.prevent="openContextMenu($event, tab.name)"
          @keydown="handleTabKeydown($event, tab.name)"
        >
          <span class="tab-label">{{ tabLabel(tab) }}</span>
          <button
            v-if="!tab.pinned"
            class="tab-close"
            type="button"
            :aria-label="`${t('Common:Close')} ${tabLabel(tab)}`"
            @click.stop="handleClose(tab.name)"
          >
            ×
          </button>
        </n-tab>
      </n-tabs>
      <n-dropdown
        placement="bottom-start"
        trigger="manual"
        :show="contextMenuVisible"
        :x="contextMenuX"
        :y="contextMenuY"
        :options="contextMenuOptions"
        :on-clickoutside="closeContextMenu"
        @select="handleContextMenuSelect"
      />
    </div>
    <div class="tabs-content">
      <slot></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { DropdownOption } from 'naive-ui'
import { getTabCloseAvailability, useTabs } from '@/hooks/tabs'
import type { TabCloseScope } from '@/hooks/tabs'
import type { TabOption } from '@/hooks/tabOption'
import { t } from '@/i18n'

type TabsInst = { syncBarPosition?: () => void }
const tabsInstRef = ref<TabsInst | null>(null)

const emits = defineEmits<{
  (e: 'close', name: string): void
  (e: 'update', name: string): void
}>()

const { tabs, currentTab, setActiveTab, removeTab, closeTabs } = useTabs()
const tabsHeaderRef = ref<HTMLElement | null>(null)
const contextMenuVisible = ref(false)
const contextMenuX = ref(0)
const contextMenuY = ref(0)
const contextTabName = ref('')

/** 业务标题等用户内容直接展示，不经过 i18n 查表 */
const tabLabel = (tab: TabOption) => (tab.rawTitle ? tab.title : t(tab.title))

const contextMenuOptions = computed<DropdownOption[]>(() => {
  const availability = getTabCloseAvailability(contextTabName.value)
  return [
    { label: t('Tabs:CloseAll'), key: 'all', disabled: !availability.all },
    { label: t('Tabs:CloseLeft'), key: 'left', disabled: !availability.left },
    { label: t('Tabs:CloseRight'), key: 'right', disabled: !availability.right },
  ]
})

function handleUpdate(name: string) {
  setActiveTab(name)
}

function handleClose(name: string) {
  emits('close', name)
  void removeTab(name)
}

function showContextMenu(name: string, x: number, y: number) {
  contextMenuVisible.value = false
  contextTabName.value = name
  contextMenuX.value = x
  contextMenuY.value = y
  void nextTick(() => {
    contextMenuVisible.value = true
  })
}

function openContextMenu(event: MouseEvent, name: string) {
  showContextMenu(name, event.clientX, event.clientY)
}

function handleTabKeydown(event: KeyboardEvent, name: string) {
  if (event.key !== 'ContextMenu' && !(event.shiftKey && event.key === 'F10')) return

  event.preventDefault()
  const target = event.currentTarget as HTMLElement
  const rect = target.getBoundingClientRect()
  showContextMenu(name, rect.left + Math.min(24, rect.width / 2), rect.bottom)
}

function closeContextMenu() {
  contextMenuVisible.value = false
}

function isTabCloseScope(value: string | number): value is TabCloseScope {
  return value === 'all' || value === 'left' || value === 'right'
}

function handleContextMenuSelect(key: string | number) {
  const anchorName = contextTabName.value
  closeContextMenu()
  if (!anchorName || !isTabCloseScope(key)) return
  void closeTabs(anchorName, key)
}

watch(currentTab, closeContextMenu)

/**
 * 标签过多时 Naive 的标签条可横向滚动，但激活项可能落在可视区外。
 * 这里直接操作 Naive 内部的横向滚动容器，保证激活标签始终可见。
 */
function scrollActiveTabIntoView() {
  const root = tabsHeaderRef.value
  if (!root) return

  const scroller = root.querySelector<HTMLElement>('.v-x-scroll')
  const activeTab = root.querySelector<HTMLElement>('.n-tabs-tab--active')
  if (!scroller || !activeTab) return

  const scrollerRect = scroller.getBoundingClientRect()
  const activeRect = activeTab.getBoundingClientRect()
  const padding = 24
  const tabLeft = activeRect.left - scrollerRect.left + scroller.scrollLeft
  const tabRight = tabLeft + activeRect.width
  const viewLeft = scroller.scrollLeft
  const viewRight = viewLeft + scroller.clientWidth

  let target: number | undefined
  if (tabLeft < viewLeft + padding) {
    target = Math.max(0, tabLeft - padding)
  } else if (tabRight > viewRight - padding) {
    target = tabRight - scroller.clientWidth + padding
  }
  if (target === undefined) return

  const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches ?? false
  scroller.scrollTo({ left: target, behavior: reduceMotion ? 'auto' : 'smooth' })
}

let scrollCorrectionTimer: number | undefined

function scheduleScrollActiveTab() {
  void nextTick(() => {
    requestAnimationFrame(() => {
      scrollActiveTabIntoView()
      // Naive 在切换标签后可能用自身逻辑再修正一次 scrollLeft，
      // 平滑滚动结束后补一次校正，确保激活标签完全进入可视区。
      window.clearTimeout(scrollCorrectionTimer)
      scrollCorrectionTimer = window.setTimeout(scrollActiveTabIntoView, 350)
    })
  })
}

watch(currentTab, scheduleScrollActiveTab)

// 当新增标签页时，naive-ui 在 route 切换同 tick 内尝试测量 bar 时
// 新的 n-tab DOM 尚未挂载，导致激活指示条消失。
// 在 DOM 更新后调用 syncBarPosition 强制重新测量，使指示条正确滑动到新标签页。
watch(
  () => tabs.value.length,
  () => {
    void nextTick(() => {
      tabsInstRef.value?.syncBarPosition?.()
      requestAnimationFrame(scrollActiveTabIntoView)
    })
  },
)

// Naive 只在激活项或标签数量变化时测量指示条宽度。打开详情时页签先以占位标题挂载、
// 标题异步回填后标签变宽，指示条会停在旧宽度；这里跟随标签文案变化重新测量。
const tabLabelsSignature = computed(() =>
  tabs.value.map((tab) => `${tab.name}\u0000${tabLabel(tab)}`).join('\u0001'),
)

watch(tabLabelsSignature, () => {
  void nextTick(() => {
    requestAnimationFrame(() => {
      tabsInstRef.value?.syncBarPosition?.()
      scrollActiveTabIntoView()
    })
  })
})

onMounted(() => {
  scheduleScrollActiveTab()
  window.addEventListener('resize', scheduleScrollActiveTab, { passive: true })
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', scheduleScrollActiveTab)
  window.clearTimeout(scrollCorrectionTimer)
})
</script>

<style scoped>
.tabs-container {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

/* 原型：标签条直接落在点阵画布上 = 主题背景 + 海军蓝 2px 底线 + 珊瑚色活动指示条。
   这里必须保持透明：顶栏是 --po-paper 纸面，标签条若也用纸面会在顶栏与内容画布之间
   多出一条白色隔断，与主题背景色不一致。 */
.tabs-header {
  height: 48px;
  flex-shrink: 0;
  padding: 0 14px;
  background: transparent;
}

.tabs-header :deep(.n-tabs-nav) {
  height: 48px;
}

/* 标签条使用 bar 类型，Naive 不会自动画底线；这里补回贯穿整条的 2px 海军蓝底线，
   同时让活动指示条落在底线上，避免指示条悬空或溢出标签条容器。 */
.tabs-header :deep(.n-tabs-nav-scroll-content) {
  border-bottom: var(--po-border) !important;
}

.tabs-header :deep(.n-tabs-tab) {
  height: 46px;
  padding: 0 14px;
  color: var(--po-ink-soft);
  font-size: 13px;
  font-weight: var(--po-weight-bold);
}

.tabs-header :deep(.n-tabs-tab:hover) {
  color: var(--po-ink);
}

.tabs-header :deep(.n-tabs-tab.n-tabs-tab--active) {
  color: var(--po-ink) !important;
  font-weight: var(--po-weight-strong);
}

.tabs-header :deep(.n-tabs .n-tabs-bar) {
  height: 4px !important;
  border: var(--po-border);
  border-bottom: 0;
  border-radius: 4px 4px 0 0;
  background: var(--po-coral);
}

.tab-label {
  overflow: hidden;
  max-width: 180px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tab-close {
  display: inline-grid;
  width: 20px;
  height: 20px;
  margin-left: 7px;
  place-items: center;
  color: currentColor;
  border: 0;
  border-radius: 6px;
  background: transparent;
  font: inherit;
  line-height: 1;
  cursor: pointer;
}

.tab-close:hover,
.tab-close:focus-visible {
  background: var(--po-accent-soft);
  outline: none;
}

.tabs-content {
  flex: 1;
  min-height: 0;
  height: 100%;
  overflow: auto;
  background: transparent;
}

@media (max-width: 560px) {
  .tabs-header {
    padding: 0 8px;
  }

  .tabs-header :deep(.n-tabs-tab) {
    padding: 0 10px;
  }

  /* 触屏下放大的关闭热区，避免 20px 小按钮难以点中 */
  .tab-close {
    width: 24px;
    height: 24px;
    margin-left: 5px;
  }
}
</style>
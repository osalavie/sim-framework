import { computed, ref } from 'vue'
import type { TabOption } from './tabOption'
import { useRoute, useRouter } from 'vue-router'
import type { Router } from 'vue-router'
import { routeConfig } from '@/config/routeConfig'

const tabs = ref<TabOption[]>([])

export type TabCloseScope = 'all' | 'left' | 'right'

export interface TabCloseAvailability {
  all: boolean
  left: boolean
  right: boolean
}

/**
 * 添加标签页（不依赖 router，可在路由守卫中使用）
 */
export function addTab(tab: TabOption) {
  if (tabs.value.some((item) => item.name === tab.name)) return

  if (!tab.pinned) {
    tabs.value.push(tab)
    return
  }

  // 固定标签始终占据左侧区域，并保持彼此的打开顺序。
  const firstClosableIndex = tabs.value.findIndex((item) => !item.pinned)
  if (firstClosableIndex < 0) tabs.value.push(tab)
  else tabs.value.splice(firstClosableIndex, 0, tab)
}

export function clearTabs() {
  tabs.value = []
}

interface RouteLocationLike {
  name?: unknown
  path: string
  fullPath: string
}

/**
 * 找到当前路由对应的标签页名称。
 * 先按 fullPath 匹配（详情编辑页每个单据一个 path），再退回 path（带查询参数的列表页），
 * 最后退回路由名（常规命名路由与标签同名）。
 */
export function findActiveTabName(route: RouteLocationLike): string | undefined {
  const exact = tabs.value.find((tab) => tab.path === route.fullPath)
  if (exact) return exact.name
  const byPath = tabs.value.find((tab) => tab.path === route.path)
  if (byPath) return byPath.name
  return typeof route.name === 'string' ? route.name : undefined
}

/** 新增或更新标签页（同名标签只保留一个，路径与标题以最新为准） */
export function upsertTab(tab: TabOption) {
  const index = tabs.value.findIndex((item) => item.name === tab.name)
  if (index >= 0) {
    tabs.value[index] = { ...tabs.value[index], ...tab }
    return
  }
  addTab(tab)
}

/** 只更新标题（编辑页随标题输入实时更新页签） */
export function setTabTitle(name: string, title: string) {
  const tab = tabs.value.find((item) => item.name === name)
  if (tab && tab.title !== title) tab.title = title
}

/** 静默移除标签页（不触发导航），用于清理路由守卫自动添加的重复页签 */
export function removeTabSilently(name: string) {
  const index = tabs.value.findIndex((item) => item.name === name)
  if (index >= 0) tabs.value.splice(index, 1)
}

/** 重命名标签页（新建单据保存后草稿页签切换为正式 ID 页签） */
export function renameTab(oldName: string, next: TabOption) {
  const index = tabs.value.findIndex((item) => item.name === oldName)
  if (index < 0) {
    upsertTab(next)
    return
  }
  const duplicate = tabs.value.find(
    (item) => item.name === next.name && item.name !== oldName,
  )
  if (duplicate) {
    duplicate.path = next.path
    duplicate.title = next.title
    duplicate.rawTitle = next.rawTitle
    tabs.value.splice(index, 1)
    return
  }
  tabs.value[index] = { ...tabs.value[index], ...next }
}

export function getTabs() {
  return [...tabs.value]
}

export function getTabCloseAvailability(anchorName: string): TabCloseAvailability {
  const anchorIndex = tabs.value.findIndex((tab) => tab.name === anchorName)
  if (anchorIndex < 0) return { all: false, left: false, right: false }

  return {
    all: tabs.value.some((tab) => !tab.pinned),
    left: tabs.value.slice(0, anchorIndex).some((tab) => !tab.pinned),
    right: tabs.value.slice(anchorIndex + 1).some((tab) => !tab.pinned),
  }
}

/**
 * 获取标签页名称
 */
export function getTabName(path: string) {
  const tab = tabs.value.find((tab) => tab.path === path)
  return tab?.name
}

/**
 * 移除标签页（需要 router 实例）
 */
export async function removeTabWithRouter(name: string, router: Router) {
  const index = tabs.value.findIndex((t) => t.name === name)
  if (index < 0 || tabs.value[index]?.pinned) return

  const isActiveTab = findActiveTabName(router.currentRoute.value) === name
  tabs.value.splice(index, 1)

  // 关闭后台标签不应打断用户当前正在查看的页面。
  if (!isActiveTab) return

  // 如果还有 tab，跳转到相邻的 tab
  if (tabs.value.length > 0) {
    if (tabs.value.length > index) {
      // 跳转到右侧的 tab
      const target = tabs.value[index]?.path ?? '/'
      await router.push(target)
    } else {
      // 跳转到左侧的 tab
      const target = tabs.value[index - 1]?.path ?? '/'
      await router.push(target)
    }
  } else {
    // 所有 tab 都关闭了，跳转到 admin 根路径
    await router.push(routeConfig.adminRootPath)
  }
}

/**
 * 以指定标签为锚点批量关闭标签。固定标签始终保留。
 */
export async function closeTabsWithRouter(
  anchorName: string,
  scope: TabCloseScope,
  router: Router,
) {
  const originalTabs = [...tabs.value]
  const anchorIndex = originalTabs.findIndex((tab) => tab.name === anchorName)
  if (anchorIndex < 0) return

  const removableNames = new Set(
    originalTabs
      .filter((tab, index) => {
        if (tab.pinned) return false
        if (scope === 'left') return index < anchorIndex
        if (scope === 'right') return index > anchorIndex
        return true
      })
      .map((tab) => tab.name),
  )
  if (removableNames.size === 0) return

  const activeName = findActiveTabName(router.currentRoute.value)
  const activeIndex = originalTabs.findIndex((tab) => tab.name === activeName)
  const activeTabWasClosed = Boolean(activeName) && removableNames.has(activeName as string)

  tabs.value = originalTabs.filter((tab) => !removableNames.has(tab.name))
  if (!activeTabWasClosed) return

  const preferredTab =
    scope === 'all' ? undefined : tabs.value.find((tab) => tab.name === anchorName)
  const survivingNames = new Set(tabs.value.map((tab) => tab.name))
  const rightNeighbour = originalTabs
    .slice(activeIndex + 1)
    .find((tab) => survivingNames.has(tab.name))
  const leftNeighbour = originalTabs
    .slice(0, Math.max(activeIndex, 0))
    .reverse()
    .find((tab) => survivingNames.has(tab.name))
  const target = preferredTab ?? rightNeighbour ?? leftNeighbour

  await router.push(target?.path ?? routeConfig.adminRootPath)
}

/**
 * 设置活动标签页（需要 router 实例）
 */
export function setActiveTabWithRouter(name: string, router: Router) {
  const target = tabs.value.find((tab) => tab.name === name)?.path ?? '/'
  router.push(target)
}

/**
 * 在组件中使用的 tabs composable
 * 只能在组件 setup 中调用
 */
export function useTabs() {
  const route = useRoute()
  const router = useRouter()

  const currentTab = computed(() => {
    return findActiveTabName(route)
  })

  function setActiveTab(name: string) {
    setActiveTabWithRouter(name, router)
  }

  function removeTab(name: string) {
    return removeTabWithRouter(name, router)
  }

  function closeTabs(anchorName: string, scope: TabCloseScope) {
    return closeTabsWithRouter(anchorName, scope, router)
  }

  return {
    tabs,
    currentTab,
    addTab,
    removeTab,
    closeTabs,
    setActiveTab,
    getTabName,
  }
}

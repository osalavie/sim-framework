import { inject, provide, readonly, ref, watch, type InjectionKey, type Ref } from 'vue'
import { useResponsive } from '@/hooks/useResponsive'

/**
 * AdminLayout 壳层共享状态。
 *
 * 侧栏折叠按钮已从 n-layout-sider 内置触发器移到顶栏（Paper Ocean 原型：
 * 顶栏左侧汉堡按钮控制导航），因此折叠状态由 AdminLayout 统一持有，
 * 通过 provide / inject 同时提供给 Header 与 Sidebar。
 */
export interface AdminShellContext {
  /** 侧栏是否折叠（窄屏下折叠即关闭抽屉） */
  collapsed: Readonly<Ref<boolean>>
  /** 是否为窄屏（侧栏以抽屉形式覆盖内容） */
  isNarrowScreen: Readonly<Ref<boolean>>
  /** 显式设置折叠状态 */
  setCollapsed: (value: boolean) => void
  /** 切换折叠状态 */
  toggleSidebar: () => void
}

const ADMIN_SHELL_KEY: InjectionKey<AdminShellContext> = Symbol('admin-shell')

export function provideAdminShell(): AdminShellContext {
  const { isNarrowScreen } = useResponsive()
  const collapsed = ref(false)
  // 仅记录宽屏下用户的折叠偏好；窄屏的展开态是临时抽屉，不参与偏好
  const widePreferredCollapsed = ref<boolean | null>(null)

  watch(
    isNarrowScreen,
    (narrow) => {
      collapsed.value = narrow ? true : (widePreferredCollapsed.value ?? false)
    },
    { immediate: true },
  )

  const setCollapsed = (value: boolean) => {
    collapsed.value = value
    if (!isNarrowScreen.value) widePreferredCollapsed.value = value
  }

  const toggleSidebar = () => setCollapsed(!collapsed.value)

  const context: AdminShellContext = {
    collapsed: readonly(collapsed),
    isNarrowScreen,
    setCollapsed,
    toggleSidebar,
  }

  provide(ADMIN_SHELL_KEY, context)
  return context
}

export function useAdminShell(): AdminShellContext {
  const context = inject(ADMIN_SHELL_KEY)
  if (!context) {
    throw new Error('useAdminShell() 必须在 AdminLayout 内使用')
  }
  return context
}

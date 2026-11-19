import type {
  RouteLocationNormalized,
  RouteLocationRaw,
  RouteRecordNormalized,
  Router,
} from 'vue-router'
import { addTab, clearTabs } from '@/hooks/tabs'
import type { MenuConfig } from '@/types/module'
import { useUserStore } from '@/stores/user'
import { useUserPreferencesStore } from '@/stores/userPreferences'
import type { DynamicRouteLoader } from '@/utils/dynamicRouteLoader'
import { resolveAuthNavigation, shouldRestoreSession } from './authNavigation'
import { initializeBackendLocalization, t } from '@/i18n'
import { useSiteStatusStore } from '@/stores/siteStatus'
import { refreshSetupStatus } from '@/config/setup'
import { routeConfig } from '@/config/routeConfig'
import { splitPermissionCodes } from '@/utils/permissionCodes'
import { siteDefaults, siteName } from '@/config/branding'
import { startNavigationLoading, stopNavigationLoading } from '@/hooks/useNavigationLoading'

interface NavigationGuardOptions {
  adminDefaultRedirect: string
  appTitle: string
  menuConfig: MenuConfig
}

export function installNavigationGuards(
  router: Router,
  dynamicRouteLoader: DynamicRouteLoader,
  options: NavigationGuardOptions,
) {
  let defaultTabsInitialized = false

  router.beforeEach(async (to, from) => {
    // 维护状态由站点状态 store 共享：刷新后兜底读取一次，之后由 SignalR 推送更新。
    const siteStatusStore = useSiteStatusStore()
    await siteStatusStore.load()

    if (siteStatusStore.maintenanceMode.enabled) {
      if (to.name === 'Maintenance') {
        document.title = `${siteName.value} - ${t('Maintenance:Title')}`
        return
      }

      return { name: 'Maintenance', replace: true }
    }

    if (to.name === 'Maintenance') {
      return { path: routeConfig.customRedirect, replace: true }
    }

    await initializeBackendLocalization()

    const initialized = await refreshSetupStatus()
    if (!initialized) {
      if (to.name === 'Setup') {
        updateDocumentTitle(to, options.appTitle)
        return
      }
      return { name: 'Setup', replace: true }
    }

    if (to.name === 'Setup') {
      return { name: 'Login', replace: true }
    }

    if (to.name === 'Register' && !siteDefaults.registrationEnabled.value) {
      return { name: 'Login', replace: true }
    }

    const isAuthEntryRoute = to.name === 'Login' || to.name === 'Register'

    // Determine if this is an initial load into the admin layout
    const toLayout = 'admin'
    const fromLayout = 'admin'
    const isCrossLayout = toLayout !== fromLayout && from.path !== '/'
    const isInitialLoad = from.path === '/' && to.path !== '/'

    if (isCrossLayout || isInitialLoad) {
      startNavigationLoading(toLayout)
    }

    // 先兜底设为主标题，避免路由解析前闪现 URL 路径或 "NotFound"
    document.title = options.appTitle

    const userStore = useUserStore()
    if (
      shouldRestoreSession({
        isLoginRoute: isAuthEntryRoute,
        isAnonymous: to.meta.anonymous === true,
        hasPersistedIdentity: !!userStore.user?.id,
      })
    ) {
      await userStore.restoreSession()
    }

    // 刷新/首次进入站点时先加载一次用户偏好，应用完成后再放行首次导航，
    // 避免加载遮罩消失后主题或语言再跳变；同一用户后续导航直接命中本地状态。
    if (userStore.isLoggedIn()) {
      await useUserPreferencesStore().load()
    }

    const decision = resolveAuthNavigation({
      isLoginRoute: isAuthEntryRoute,
      isAnonymous: to.meta.anonymous === true,
      isLoggedIn: userStore.isLoggedIn(),
      targetPath: to.fullPath,
    })

    if (decision.type === 'redirect-default') {
      return { path: options.adminDefaultRedirect }
    }

    if (decision.type === 'redirect-login') {
      return { name: 'Login', query: { redirect: decision.redirect } }
    }

    if (isAuthEntryRoute && !userStore.isLoggedIn()) {
      dynamicRouteLoader.clear()
      clearTabs()
      defaultTabsInitialized = false
    }

    if (isAuthEntryRoute || to.meta.anonymous === true) {
      updateDocumentTitle(to, options.appTitle)
      return
    }

    if (!dynamicRouteLoader.isLoaded()) {
      try {
        await dynamicRouteLoader.loadRoutes()

        const targetRoute = router.getRoutes().find((route) => route.path === to.path)
        const redirect = resolveRelativeRedirect(targetRoute)
        if (redirect) return redirect

        return { path: to.fullPath, replace: true }
      } catch (error) {
        console.error('[Router] Failed to load dynamic routes:', error)
      }
    }

    updateDocumentTitle(to, options.appTitle)

    if (to.matched.length === 0) {
      return { name: 'NotFound' }
    }

    const permissionCodes = splitPermissionCodes(to.meta.permissionCode as string | undefined)
    const requireAll = Boolean(to.meta.permissionRequireAll)
    const permitted =
      permissionCodes.length === 0 ||
      (requireAll ? userStore.hasAllPermissions(permissionCodes) : userStore.hasAnyPermission(permissionCodes))
    if (!permitted) {
      return { name: 'Forbidden', replace: true }
    }

    const relativeRedirect = resolveRelativeRedirect(to.matched[to.matched.length - 1])
    if (relativeRedirect) return relativeRedirect

    if (!defaultTabsInitialized && from.path === '/' && userStore.isLoggedIn()) {
      defaultTabsInitialized = true
      initializeDefaultTabs(router, options.menuConfig)
    }
  })

  router.afterEach((to) => {
    stopNavigationLoading()

    if (!to.meta.tab || !useUserStore().isLoggedIn()) return

    addTab({
      name: to.name as string,
      path: to.path,
      title: (to.meta.title as string) ?? 'New Tab',
      pinned: !!to.meta.pinned,
    })
  })
}

function updateDocumentTitle(to: RouteLocationNormalized, appTitle: string) {
  const title = typeof to.meta.title === 'string' ? t(to.meta.title) : ''
  const currentSiteName = siteName.value || appTitle
  document.title = title ? `${currentSiteName} - ${title}` : currentSiteName
}

function resolveRelativeRedirect(
  route: RouteRecordNormalized | undefined,
): RouteLocationRaw | undefined {
  if (!route) return

  const redirect = route.redirect
  if (typeof redirect !== 'string' || redirect.startsWith('/')) return

  const basePath = route.path.endsWith('/') ? route.path : `${route.path}/`
  return { path: `${basePath}${redirect}`, replace: true }
}

function initializeDefaultTabs(router: Router, menuConfig: MenuConfig) {
  const allRoutes = router.getRoutes()

  menuConfig.defaultOpen.forEach((path) => {
    const route = allRoutes.find((candidate) => candidate.path === path)
    if (!route?.meta.tab) return

    addTab({
      name: route.name as string,
      path: route.path,
      title: (route.meta.title as string) ?? 'New Tab',
      pinned: !!route.meta.pinned,
    })
  })
}

/**
 * Dynamic Route Loader Module
 *
 * This module handles loading route configurations from a backend API and dynamically
 * registering them with Vue Router. It implements fault-tolerant loading where individual
 * module failures don't prevent other modules from loading successfully.
 *
 * Key Features:
 * - Fault-tolerant module loading (one failure doesn't affect others)
 * - Multiple component loading strategies (auto, registry, map)
 * - Component path aliasing for flexibility
 * - Detailed error logging and statistics
 * - Support for both modular and flat route structures
 * - Automatic module tagging for traceability
 *
 * Component Loading Strategies:
 * - 'auto'/'registry': Uses component registry with auto-discovery (recommended)
 * - 'map': Uses manually defined component map
 *
 * @module dynamicRouteLoader
 * @see {@link DynamicRouteLoader} for main loader class
 * @see {@link componentRegistry} for component auto-discovery
 */
import type { RouteRecordRaw, Router } from 'vue-router'
import type { BackendRoute, ModuleInfo } from '@/types/dynamicRoute'
import type { Component } from 'vue'
import { routeApi } from '@/api/route'
import { routeConfig } from '@/config/routeConfig'
import {
  COMPONENT_LOAD_STRATEGY,
  SHOW_404_ON_COMPONENT_ERROR,
  ENABLE_LOAD_LOGGING,
  COMPONENT_ALIASES,
} from '@/config/dynamicRoute'
import { componentRegistry } from '@/utils/componentRegistry'
import { ensureMessagePacks, messagePacksForComponent } from '@/i18n/lazyPacks'

/**
 * Component map for manual component registration
 *
 * Used only when COMPONENT_LOAD_STRATEGY is set to 'map'.
 * Maps backend component paths to actual Vue component loaders.
 *
 * @example
 * ```typescript
 * componentMap['dashboard/index.vue'] = () => import('@/views/dynamic/dashboard/index.vue')
 * ```
 */
const componentMap: Record<string, () => Promise<Component>> = {
  // Example: Manually registered components
  // 'dashboard/index.vue': () => import('@/views/dynamic/dashboard/index.vue'),
}

/**
 * Resolve component path with alias support
 *
 * Checks if the provided path has an alias defined in COMPONENT_ALIASES
 * and returns the aliased path if found, otherwise returns the original path.
 *
 * This allows backend to use simplified paths that map to actual component locations.
 *
 * @param path - Component path from backend
 * @returns Resolved component path (aliased or original)
 *
 * @example
 * ```typescript
 * // If COMPONENT_ALIASES = { 'Dashboard': 'views/dashboard/index.vue' }
 * resolveComponentPath('Dashboard') // Returns 'views/dashboard/index.vue'
 * resolveComponentPath('other/path') // Returns 'other/path'
 * ```
 */
export function resolveComponentPath(path: string): string {
  return COMPONENT_ALIASES[path] || path
}

/**
 * Load component dynamically based on configured strategy
 *
 * Attempts to load a component using the configured loading strategy:
 * 1. 'auto'/'registry': Uses component registry (auto-discovered components)
 * 2. 'map': Uses manually defined component map
 *
 * Special handling:
 * - 'Layout' or 'layout' paths load the generic Layout component
 * - Missing components can optionally return 404 component
 * - Component loading can be logged for debugging
 *
 * @param componentPath - Path to component from backend
 * @returns Component loader function, or null if component not found
 *
 * @example
 * ```typescript
 * const loader = loadComponent('dashboard/index.vue')
 * if (loader) {
 *   const component = await loader()
 * } else {
 *   console.warn('Component not found')
 * }
 * ```
 *
 * Requirements: 5.4, 9.1, 9.4 - Handle invalid component paths gracefully
 */
function loadComponent(componentPath: string): (() => Promise<Component>) | null {
  // Resolve any aliases first
  const resolvedPath = resolveComponentPath(componentPath)

  // Special case: Layout component
  // Backend can use 'Layout' or 'layout' as a shorthand for the generic layout component
  if (resolvedPath === 'Layout' || resolvedPath === 'layout') {
    return () => import('@/components/Layout/Layout.vue')
  }

  // Strategy 1: Component Registry (Recommended)
  // Uses auto-discovered components from Vite glob imports
  // This is the most flexible approach as it automatically finds all components
  if (COMPONENT_LOAD_STRATEGY === 'auto' || COMPONENT_LOAD_STRATEGY === 'registry') {
    const component = componentRegistry.getComponent(resolvedPath)

    if (component) {
      return wrapWithMessagePacks(resolvedPath, component)
    }

    // Component not found in registry
    if (ENABLE_LOAD_LOGGING) {
      console.warn(`[Component Load] ✗ Component not found in registry: ${resolvedPath}`)
    }

    // Optionally return 404 component instead of null
    // This prevents route registration failures but shows 404 when accessed
    if (SHOW_404_ON_COMPONENT_ERROR) {
      return () => import('@/views/core/404.vue')
    }

    return null
  }

  // Strategy 2: Component Map
  // Uses manually registered components from componentMap
  // Requires explicit registration of each component
  if (COMPONENT_LOAD_STRATEGY === 'map') {
    const component = componentMap[resolvedPath]
    if (!component && ENABLE_LOAD_LOGGING) {
      console.warn(`[Component Load] ✗ Component not found in map: ${resolvedPath}`)
    }
    return component || null
  }

  return null
}

/**
 * 为功能模块组件挂上语言包加载：组件模块导入前先注册对应语言包，
 * 使模块文案不进入入口包，同时保证组件渲染时文案已就绪。
 */
function wrapWithMessagePacks(
  componentPath: string,
  loader: () => Promise<Component>,
): () => Promise<Component> {
  const packs = messagePacksForComponent(componentPath)
  if (!packs.length) return loader

  return () => ensureMessagePacks(packs).then(() => loader())
}

/**
 * Transform backend route configuration to Vue Router format
 *
 * Converts a backend route object into a Vue Router RouteRecordRaw object.
 * This includes:
 * - Loading the component dynamically
 * - Preserving route metadata
 * - Tagging route with module ID for traceability
 * - Recursively processing child routes
 * - Handling redirects
 *
 * Error Handling:
 * - Returns null if component cannot be loaded (logs warning)
 * - Returns null if transformation fails (logs error)
 * - Filters out null child routes from recursive processing
 *
 * This implements fault-tolerant route processing where one route's failure
 * doesn't prevent other routes from being processed.
 *
 * @param backendRoute - Route configuration from backend API
 * @param moduleId - Optional module ID to tag the route with
 * @returns Vue Router route configuration, or null if transformation fails
 *
 * @example
 * ```typescript
 * const backendRoute = {
 *   path: '/dashboard',
 *   name: 'Dashboard',
 *   component: 'views/dashboard/index.vue',
 *   meta: { title: 'Dashboard', menu: true }
 * }
 * const vueRoute = transformBackendRoute(backendRoute, 'dashboard-module')
 * // Returns RouteRecordRaw with component loader and module tag
 * ```
 *
 * Requirements: 5.4, 9.1, 9.4, 9.5 - Graceful component loading error handling
 */
function transformBackendRoute(
  backendRoute: BackendRoute,
  moduleId?: string,
  resolveModuleId?: (route: BackendRoute) => string | undefined,
): RouteRecordRaw | null {
  try {
    // Load component dynamically
    // Requirement: 5.4 - Handle invalid component paths gracefully
    const component = loadComponent(backendRoute.component)
    if (!component) {
      // Requirement: 9.1, 9.4 - Log warnings for missing component paths
      console.warn(
        `[Dynamic Routes] ✗ Component not found for route: ${backendRoute.path} (component: ${backendRoute.component})`,
      )
      return null
    }

    // 跨模块路由树中，每个节点优先使用自身关联的可见模块做标记。
    const ownModuleId = resolveModuleId?.(backendRoute) ?? moduleId

    // Create Vue Router route configuration
    const route: RouteRecordRaw = {
      path: backendRoute.path,
      name: backendRoute.name,
      component,
      meta: {
        // Preserve all backend metadata (including permissionCode)
        ...(backendRoute.meta || {}),
        // Tag route with module ID for traceability
        // Requirement: 10.1 - Route module tagging
        module: ownModuleId || backendRoute.meta?.module,
      },
    }

    // Handle route redirects
    // Redirects allow routes to automatically navigate to another path
    if (backendRoute.redirect) {
      ;(route as RouteRecordRaw & { redirect?: string }).redirect = backendRoute.redirect
    }

    // Recursively process child routes
    // This maintains the route hierarchy from the backend configuration
    // Failed child routes are filtered out (fault-tolerant processing)
    if (backendRoute.children && backendRoute.children.length > 0) {
      ;(route as RouteRecordRaw & { children?: RouteRecordRaw[] }).children = backendRoute.children
        .map((child) => transformBackendRoute(child, ownModuleId, resolveModuleId))
        .filter((r): r is RouteRecordRaw => r !== null)
    }

    return route
  } catch (error) {
    // Requirement: 9.5 - Component errors don't affect other routes
    // Catch any unexpected errors during transformation
    // Log the error and return null to skip this route
    const errorMessage = error instanceof Error ? error.message : String(error)
    console.error(`[Dynamic Routes] ✗ Error transforming route ${backendRoute.path}:`, errorMessage)
    return null
  }
}

/**
 * Dynamic Route Loader
 *
 * Loads routes from a backend API and registers them with the Vue Router.
 * Implements fault-tolerant loading where individual module failures don't
 * prevent other modules from loading successfully.
 *
 * @example
 * ```typescript
 * const loader = new DynamicRouteLoader(router)
 *
 * // Load routes from API
 * await loader.loadRoutes()
 *
 * // Check if loaded successfully
 * if (loader.isLoaded()) {
 *   loader.getLoadedModules()
 * }
 * ```
 */
export class DynamicRouteLoader {
  private router: Router
  private loaded = false
  private modules: ModuleInfo[] = []
  private loadingPromise: Promise<void> | null = null
  private loadGeneration = 0
  private routeRemovers: Array<() => void> = []

  /**
   * Creates a new DynamicRouteLoader instance
   *
   * @param router - Vue Router instance to register routes with
   */
  constructor(router: Router) {
    this.router = router
  }

  /**
   * Load dynamic routes from backend API
   *
   * The backend returns accessible modules and one global route tree in a single
   * request. Cross-module parent/child relationships are already resolved by the
   * backend, so the loader only resolves the layout of each root route and registers it.
   *
   * It implements fault-tolerant loading where:
   * - Missing components only skip their own route
   * - Static routes remain accessible even if dynamic loading fails
   *
   * The method is idempotent - calling it multiple times will return immediately.
   *
   * @returns Promise that resolves when all routes are loaded
   * @throws Error if the API request fails or AdminLayout route is not found
   *
   * @example
   * ```typescript
   * try {
   *   await loader.loadRoutes()
   *   // Routes loaded successfully.
   * } catch (error) {
   *   console.error('Failed to load routes:', error)
   * }
   * ```
   *
   * Requirements: 5.1, 5.2, 5.3, 5.4, 1.5, 11.6
   */
  async loadRoutes(): Promise<void> {
    if (this.loaded) {
      return
    }

    if (this.loadingPromise) {
      return this.loadingPromise
    }

    const generation = this.loadGeneration
    const loadingPromise = this.loadRoutesInternal(generation)
    this.loadingPromise = loadingPromise

    try {
      await loadingPromise
    } finally {
      if (this.loadingPromise === loadingPromise) {
        this.loadingPromise = null
      }
    }
  }

  private async loadRoutesInternal(generation: number): Promise<void> {
    try {
      // 后端一次性返回可访问模块和跨模块统一组装的全局路由树。
      const userMenu = await routeApi.getUserMenu()
      if (generation !== this.loadGeneration) return

      this.modules = (userMenu.modules ?? []).filter((module) => module.enabled)

      if (this.modules.length === 0 || !userMenu.routes?.length) {
        this.loaded = true
        return
      }

      const moduleById = new Map(this.modules.map((module) => [module.id, module]))

      // 顶层路由可能是多个模块共享的父级；收集子树中的可见模块，按模块顺序决定布局。
      const collectModuleIds = (route: BackendRoute): string[] => {
        const ids = new Set<string>(route.moduleIds ?? [])
        route.children?.forEach((child) => {
          collectModuleIds(child).forEach((id) => ids.add(id))
        })
        return [...ids]
      }

      const pickModule = (ids: Iterable<string>): ModuleInfo | undefined =>
        [...ids]
          .map((id) => moduleById.get(id))
          .filter((module): module is ModuleInfo => Boolean(module))
          .sort(
            (left, right) =>
              (left.order ?? 0) - (right.order ?? 0) || left.id.localeCompare(right.id),
          )[0]

      // 顶层路由优先用自身关联的可见模块；仅当它只是跨模块祖先时回退到子树模块。
      const resolveModule = (route: BackendRoute): ModuleInfo | undefined =>
        pickModule(route.moduleIds ?? []) ?? pickModule(collectModuleIds(route))

      const applyEmptyRedirect = (route: BackendRoute): void => {
        if (route.redirect) return
        const module = resolveModule(route)
        if (module?.emptyRedirect) {
          ;(route as BackendRoute & { redirect?: string }).redirect = module.emptyRedirect
        }
      }

      // Find AdminLayout route for registering default layout routes
      const adminLayoutRoute = this.router.getRoutes().find((r) => r.name === 'AdminLayout')
      if (!adminLayoutRoute) {
        console.error('[Dynamic Routes] ✗ AdminLayout route not found, cannot add dynamic routes')
        this.loaded = true
        return
      }

      const customLayoutRoute = this.router
        .getRoutes()
        .find((r) => r.path === routeConfig.customRootPath)

      for (const backendRoute of userMenu.routes) {
        applyEmptyRedirect(backendRoute)

        const module = resolveModule(backendRoute)
        const layout = module?.layout || 'admin'
        // 每个节点优先用自身关联的可见模块打标；共享父级沿用最近祖先的模块。
        const transformed = transformBackendRoute(backendRoute, module?.id, (route) =>
          resolveModule(route)?.id,
        )
        if (!transformed) {
          continue
        }

        if (layout === 'none') {
          this.registerRoute(transformed)
        } else if (layout === 'custom') {
          if (customLayoutRoute?.name) {
            this.registerRoute(transformed, customLayoutRoute.name)
          } else {
            this.registerRoute(transformed)
          }
        } else if (adminLayoutRoute.name) {
          this.registerRoute(transformed, adminLayoutRoute.name)
        } else {
          this.registerRoute(transformed)
        }
      }

      if (generation === this.loadGeneration) {
        this.loaded = true
      }
    } catch (error) {
      if (generation !== this.loadGeneration) return

      // Static routes remain accessible even if dynamic loading fails.
      console.error('[Dynamic Routes] ✗ Failed to load dynamic routes:', error)
      this.loaded = true // Prevent retry loops
      throw error
    }
  }

  /**
   * Reload dynamic routes
   *
   * Clears the loaded state and reloads routes from the backend API.
   * Useful for scenarios like permission changes or manual refresh.
   *
   * Note: Vue Router doesn't provide a direct API to remove routes.
   * This method simply reloads and may result in duplicate routes if
   * the router instance isn't recreated.
   *
   * @returns Promise that resolves when routes are reloaded
   *
   * @example
   * ```typescript
   * // User permissions changed, reload routes
   * await loader.reloadRoutes()
   * ```
   */
  async reloadRoutes(): Promise<void> {
    this.clear()
    return this.loadRoutes()
  }

  /**
   * Check if routes have been loaded
   *
   * @returns true if routes have been loaded (successfully or with errors), false otherwise
   *
   * @example
   * ```typescript
   * if (loader.isLoaded()) {
   *   // Routes are ready.
   * } else {
   *   await loader.loadRoutes()
   * }
   * ```
   */
  isLoaded(): boolean {
    return this.loaded
  }

  /**
   * Get list of successfully loaded module IDs
   *
   * @returns Array of module IDs that were successfully loaded
   *
   * @example
   * ```typescript
   * const loadedModules = loader.getLoadedModules()
   * loadedModules.includes('rbac')
   * ```
   */
  getLoadedModules(): string[] {
    return this.modules.map((m) => m.id)
  }

  /**
   * Clear loaded routes and reset state
   *
   * This allows routes to be reloaded on the next call to loadRoutes().
   * Note: This does not remove routes from the router.
   *
   * @example
   * ```typescript
   * loader.clear()
   * await loader.loadRoutes() // Will fetch fresh routes from API
   * ```
   */
  clear(): void {
    this.loadGeneration += 1
    this.loadingPromise = null
    this.removeRegisteredRoutes()
    this.loaded = false
    this.modules = []
  }

  private registerRoute(route: RouteRecordRaw, parentName?: string | symbol): void {
    // 非 admin 布局也做一次兜底去重，避免共享路由重复注册
    if (
      route.name &&
      typeof this.router.hasRoute === 'function' &&
      this.router.hasRoute(route.name)
    ) {
      return
    }
    const removeRoute = parentName
      ? this.router.addRoute(parentName, route)
      : this.router.addRoute(route)
    this.routeRemovers.push(removeRoute)
  }

  private removeRegisteredRoutes(): void {
    for (const removeRoute of this.routeRemovers.reverse()) {
      removeRoute()
    }
    this.routeRemovers = []
  }
}

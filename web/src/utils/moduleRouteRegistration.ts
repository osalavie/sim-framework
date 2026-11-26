/**
 * Module Route Registration Utilities
 *
 * This module handles the registration of module routes based on their layout configuration.
 * It processes module configurations and organizes routes into the appropriate router hierarchy.
 *
 * The system supports three layout types:
 * 1. 'admin': Routes become children of AdminLayout (standard admin pages)
 * 2. 'custom': Routes get a custom layout wrapper at top level
 * 3. 'none': Routes are registered at top level without any wrapper
 *
 * Key features:
 * - Fault-tolerant processing (one module's failure doesn't affect others)
 * - Automatic route tagging with module IDs for traceability
 * - Module root route redirect creation
 * - /admin/ prefix removal for default layout routes
 * - Comprehensive validation before processing
 *
 * @module moduleRouteRegistration
 * @see {@link ModuleConfig} for module configuration structure
 * @see {@link loadModuleRoutes} for main entry point
 */
import type { RouteRecordRaw } from 'vue-router'
import type { ModuleConfig } from '@/types/module'
import { validateModuleConfig } from './moduleValidation'

const STATIC_ADMIN_MODULE_ORDER = 0

/**
 * Result of module route processing
 *
 * Contains organized routes and processing statistics:
 * - adminLayoutRoutes: Routes to register under AdminLayout
 * - noneLayoutRoutes: Routes to register at router root
 * - processedModules: IDs of successfully processed modules
 * - failedModules: Failed modules with error details
 *
 */
export interface ModuleRouteResult {
  /** Routes to be added as AdminLayout children (default layout modules) */
  adminLayoutRoutes: RouteRecordRaw[]
  /** Routes to be added as top-level routes (custom/none layout modules) */
  customLayoutRoutes: RouteRecordRaw[]
  /** Routes to be added as top-level routes (custom/none layout modules) */
  noneLayoutRoutes: RouteRecordRaw[]
  /** IDs of modules that were successfully processed */
  processedModules: string[]
  /** Modules that failed processing with error details */
  failedModules: Array<{ id: string; error: string }>
}

/**
 * Add module ID to route meta
 *
 * Tags a route with its source module ID for traceability and debugging.
 * This allows routes to be traced back to their originating module at runtime.
 *
 * Preserves existing meta.module values to avoid overwriting manually set values.
 * This is important for routes that may have been pre-tagged or have special
 * module associations.
 *
 * @param route - Route to tag with module ID
 * @param moduleId - ID of the module this route belongs to
 * @returns New route object with module ID in meta
 *
 * @example
 * ```typescript
 * const route = { path: '/dashboard', component: Dashboard }
 * const tagged = tagRouteWithModule(route, 'dashboard-module')
 * // tagged.meta.module === 'dashboard-module'
 *
 * // Preserves existing module tag
 * const preTagged = { path: '/special', meta: { module: 'custom' } }
 * const result = tagRouteWithModule(preTagged, 'dashboard-module')
 * // result.meta.module === 'custom' (preserved)
 * ```
 *
 * Requirements: 10.1 - Route module tagging
 * Requirements: 10.2 - Existing module tag preservation
 */
function tagRouteWithModule(route: RouteRecordRaw, moduleId: string): RouteRecordRaw {
  // Check if route already has a module tag
  // Preserve existing meta.module value if it exists (Requirement 10.2)
  const existingModule = route.meta?.module

  return {
    ...route,
    meta: {
      ...route.meta,
      // Use existing module tag if present, otherwise use provided moduleId
      module: existingModule !== undefined ? existingModule : moduleId,
    },
  }
}

/**
 * Remove /admin/ prefix from route path
 * Requirements: 2.6
 */
function removeAdminPrefix(path: string): string {
  if (path.startsWith('/admin/')) {
    return path.replace('/admin/', '/')
  }
  return path
}

/**
 * Process module routes for admin layout (AdminLayout)
 * Requirements: 2.1, 2.6, 12.1
 */
function processAdminLayoutModule(module: ModuleConfig): RouteRecordRaw[] {
  const routes: RouteRecordRaw[] = []

  // Tag all module routes with module ID, remove /admin/ prefix, and add them
  // Requirements: 2.6 - Remove /admin/ prefix for admin layout routes
  const taggedRoutes = module.routes.map((route) => {
    const tagged = tagRouteWithModule(route, module.id)
    return {
      ...tagged,
      path: removeAdminPrefix(tagged.path),
      meta: {
        ...tagged.meta,
        // Static admin modules share the first navigation priority. Child
        // routes keep their own order inside the module group.
        order: STATIC_ADMIN_MODULE_ORDER,
      },
    }
  })
  routes.push(...taggedRoutes)

  return routes
}

/**
 * Process module routes for layout (custom Layout)
 * Requirements: 2.2, 12.3
 */
function processLayoutModule(module: ModuleConfig): RouteRecordRaw[] {
  // Tag all module routes with module ID
  const taggedRoutes = module.routes.map((route) => tagRouteWithModule(route, module.id))

  return taggedRoutes
}

/**
 * Process module routes for no layout (top-level routes)
 * Requirements: 2.3, 12.2
 */
function processNoLayoutModule(module: ModuleConfig): RouteRecordRaw[] {
  // Tag all module routes with module ID
  const taggedRoutes = module.routes.map((route) => tagRouteWithModule(route, module.id))

  return taggedRoutes
}

/**
 * Process a single module and return its routes
 * Requirements: 2.1, 2.2, 2.3, 2.4, 7.1
 */
function processModule(module: ModuleConfig): {
  adminLayoutRoutes: RouteRecordRaw[]
  customLayoutRoutes: RouteRecordRaw[]
  noneLayoutRoutes: RouteRecordRaw[]
} {
  const layout = module.layout || 'admin'

  const result = {
    adminLayoutRoutes: [] as RouteRecordRaw[],
    customLayoutRoutes: [] as RouteRecordRaw[],
    noneLayoutRoutes: [] as RouteRecordRaw[],
  }

  switch (layout) {
    case 'admin':
      result.adminLayoutRoutes = processAdminLayoutModule(module)
      break

    case 'custom':
      result.customLayoutRoutes = processLayoutModule(module)
      break

    case 'none':
      result.noneLayoutRoutes = processNoLayoutModule(module)
      break

    default:
      throw new Error(`Module "${module.id}" has invalid layout type "${layout}"`)
  }

  return result
}

/**
 * Load and process all enabled static module routes
 *
 * This function processes an array of module configurations and organizes their routes
 * based on layout type. It handles three layout types:
 * - 'admin': Routes become children of AdminLayout
 * - 'custom': Routes get a custom layout wrapper as top-level route
 * - 'none': Routes become top-level routes without any wrapper
 *
 * The function includes comprehensive error handling and validation:
 * - Invalid modules are skipped and logged
 * - Failed modules don't prevent other modules from loading
 * - All routes are tagged with their module ID
 *
 * @param modules - Array of module configurations to process
 * @returns Object containing organized routes and processing results
 * @returns {RouteRecordRaw[]} adminLayoutRoutes - Routes to register under AdminLayout
 * @returns {RouteRecordRaw[]} noneLayoutRoutes - Routes to register at top level
 * @returns {string[]} processedModules - IDs of successfully processed modules
 * @returns {Array<{id: string, error: string}>} failedModules - Failed modules with errors
 *
 *
 * @see {@link ModuleConfig} for module configuration structure
 * @see {@link ModuleRouteResult} for return type details
 *
 * Requirements: 1.1, 2.1, 2.2, 2.3, 11.6
 */
export function loadModuleRoutes(modules: ModuleConfig[]): ModuleRouteResult {
  const result: ModuleRouteResult = {
    adminLayoutRoutes: [],
    customLayoutRoutes: [],
    noneLayoutRoutes: [],
    processedModules: [],
    failedModules: [],
  }

  modules.forEach((module) => {
    try {
      // Skip disabled modules
      if (!module.enabled) {
        return
      }

      // Validate module configuration
      // Requirements: 2.5, 11.1, 11.3
      const validationResult = validateModuleConfig(module)

      // Log warnings
      validationResult.warnings.forEach((warning) => {
        console.warn(`[Module Routes] ${warning}`)
      })

      // If validation fails, skip this module
      if (!validationResult.valid) {
        validationResult.errors.forEach((error) => {
          console.error(`[Module Routes] ${error}`)
        })
        result.failedModules.push({
          id: module.id,
          error: `Validation failed: ${validationResult.errors.join('; ')}`,
        })
        return
      }

      // Process module routes
      const moduleRoutes = processModule(module)

      // Add to result
      result.adminLayoutRoutes.push(...moduleRoutes.adminLayoutRoutes)
      result.customLayoutRoutes.push(...moduleRoutes.customLayoutRoutes)
      result.noneLayoutRoutes.push(...moduleRoutes.noneLayoutRoutes)
      result.processedModules.push(module.id)

    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error)
      result.failedModules.push({
        id: module.id,
        error: errorMessage,
      })
      console.error(`[Module Routes] ✗ Failed to load module "${module.id}":`, errorMessage)
    }
  })

  return result
}

/**
 * Verify route path preservation
 * Requirements: 2.4
 */
export function verifyRoutePathPreservation(
  originalRoute: RouteRecordRaw,
  registeredRoute: RouteRecordRaw,
): boolean {
  return originalRoute.path === registeredRoute.path
}

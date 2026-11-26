/**
 * Menu Generation System
 *
 * This module generates navigation menus from route metadata. It processes
 * routes registered with the router and creates a hierarchical menu structure
 * based on route paths and metadata flags.
 *
 * Key Features:
 * - Automatic menu generation from route metadata
 * - Hierarchical structure based on route paths
 * - Filtering by meta.menu flag
 * - Icon support via meta.icon
 * - Sorting by meta.order
 * - AdminLayout-only filtering (only shows admin routes)
 * - Reactive updates when routes change
 *
 * Menu Generation Algorithm:
 * 1. Filter routes to AdminLayout children only (paths starting with /admin/)
 * 2. Find top-level routes (no visible parent in menu)
 * 3. For each top-level route, recursively find children
 * 4. Convert routes to menu options with icons and metadata
 * 5. Sort menu items by meta.order
 *
 * @module menus
 * @see {@link useMenus} for main menu generation hook
 * @see {@link routeToMenu} for route-to-menu conversion
 */
import { createIconRender } from '@/components/Icon'
import { useUserStore } from '@/stores/user'
import { splitPermissionCodes } from '@/utils/permissionCodes'
import { t } from '@/i18n'
import type { MenuOption } from 'naive-ui'
import { computed, h } from 'vue'
import { useRouter } from 'vue-router'
import type { RouteRecordNormalized } from 'vue-router'

/**
 * Render a menu item label that supports hover-to-show-full-text on overflow.
 *
 * - The label text is wrapped in a `<span class="menu-item-label">` whose CSS
 *   enforces ellipsis truncation when the text overflows the sidebar width.
 * - On `mouseenter` we compare `scrollWidth` with `clientWidth` and only set
 *   the native `title` attribute when the text is actually truncated. This
 *   avoids showing a redundant tooltip for short labels that fit on screen.
 * - When the sidebar is collapsed, naive-ui's `n-menu` already renders the
 *   label inside its own popover; there the container is wide enough that
 *   ellipsis is not triggered, so no duplicate tooltip appears.
 *
 * @param label - Localized menu label text
 * @returns A render function suitable for `MenuOption.label`
 */
function renderMenuLabel(label: string) {
  return () =>
    h(
      'span',
      {
        class: 'menu-item-label',
        onMouseenter(e: MouseEvent) {
          const el = e.currentTarget as HTMLElement
          if (el.scrollWidth > el.clientWidth) {
            el.title = label
          } else {
            el.removeAttribute('title')
          }
        },
      },
      label,
    )
}

/**
 * Check if a path is a direct child of another path
 *
 * Determines if childPath is exactly one level deeper than parentPath.
 * This is used to build the menu hierarchy by identifying parent-child
 * relationships between routes.
 *
 * Algorithm:
 * 1. Normalize parent path to end with '/'
 * 2. Check if child starts with normalized parent
 * 3. Get relative path (child minus parent)
 * 4. Direct child should have no additional '/' in relative path
 *
 * @param childPath - Potential child route path
 * @param parentPath - Potential parent route path
 * @returns true if childPath is exactly one level deeper than parentPath
 *
 * @example
 * ```typescript
 * isDirectChild('/admin/dashboard/overview', '/admin/dashboard') // true
 * isDirectChild('/admin/dashboard/overview/detail', '/admin/dashboard') // false (two levels)
 * isDirectChild('/admin/reports', '/admin/dashboard') // false (not a child)
 * ```
 *
 * Requirements: 7.3 - Menu hierarchy preservation
 */
function isDirectChild(childPath: string, parentPath: string): boolean {
  // Normalize parent path: ensure it ends with '/'
  // This makes substring matching more reliable
  const normalizedParent = parentPath.endsWith('/') ? parentPath : parentPath + '/'

  // Check if child path starts with parent path
  if (!childPath.startsWith(normalizedParent)) {
    return false
  }

  // Get the relative path (portion after parent path)
  const relativePath = childPath.substring(normalizedParent.length)

  // Direct child should not contain additional slashes
  // If it does, it's a grandchild or deeper
  return !relativePath.includes('/')
}

/**
 * Find all direct children of a route
 *
 * Searches through all routes to find those that are exactly one level
 * deeper than the given route. This builds the immediate children for
 * the menu hierarchy.
 *
 * @param route - Parent route to find children for
 * @param allRoutes - Complete list of all routes in the application
 * @returns Array of routes that are direct children of the given route
 *
 * @example
 * ```typescript
 * const dashboardRoute = { path: '/admin/dashboard', ... }
 * const children = findDirectChildren(dashboardRoute, allRoutes)
 * // Returns routes like /admin/dashboard/overview, /admin/dashboard/stats
 * // But not /admin/dashboard/overview/detail (grandchild)
 * ```
 *
 * Requirements: 7.3 - Menu hierarchy preservation
 */
function findDirectChildren(
  route: RouteRecordNormalized,
  allRoutes: RouteRecordNormalized[],
): RouteRecordNormalized[] {
  return allRoutes.filter((r) => {
    // Don't include the route itself
    if (r.path === route.path) return false
    // Check if it's a direct child (one level deeper)
    return isDirectChild(r.path, route.path)
  })
}

/**
 * Convert route to menu option (recursive)
 *
 * Transforms a route record into a menu option for the navigation UI.
 * This function recursively processes child routes to build a hierarchical
 * menu structure.
 *
 * Menu Visibility Rules:
 * 1. Route must have meta.menu = true to be visible
 * 2. If route has meta.menu = false but children have meta.menu = true,
 *    skip the parent and promote children to parent's level
 * 3. If route has meta.menu = false and no menu children, skip entirely
 *
 * Menu Properties:
 * - key: Route path (used for navigation)
 * - label: Route title from meta.title
 * - icon: Icon component from meta.icon
 * - disabled: Disabled state from meta.disabled
 * - children: Recursively processed child routes
 *
 * Child routes are sorted by meta.order (ascending), with routes lacking
 * an order value defaulting to 999 (appearing last).
 *
 * @param route - Route record to convert to menu option
 * @param allRoutes - Complete list of all routes (needed for finding children)
 * @returns Menu option object, or null if route should not appear in menu
 *
 * @example
 * ```typescript
 * const route = {
 *   path: '/admin/dashboard',
 *   meta: { title: 'Dashboard', menu: true, icon: 'dashboard', order: 1 }
 * }
 * const menuOption = routeToMenu(route, allRoutes)
 * // Returns: { key: '/admin/dashboard', label: 'Dashboard', icon: ..., ... }
 * ```
 *
 * Requirements: 7.2 - Menu flag rendering
 * Requirements: 7.3 - Menu hierarchy preservation
 * Requirements: 7.4 - Menu icon display
 * Requirements: 7.5 - Menu item sorting
 */
type RouteAccessPredicate = (route: RouteRecordNormalized) => boolean

const allowAllRoutes: RouteAccessPredicate = () => true

export function routeToMenu(
  route: RouteRecordNormalized,
  allRoutes: RouteRecordNormalized[],
  canView: RouteAccessPredicate = allowAllRoutes,
): MenuOption | null {
  if (!canView(route)) return null

  // Find all direct children of this route
  const directChildren = findDirectChildren(route, allRoutes)

  // Filter to only children that should appear in menu
  // Requirement: 7.2 - Only routes with meta.menu = true appear in menu
  const menuChildren = directChildren.filter((r) => r.meta?.menu === true && canView(r))

  // 父级原本有菜单子项、但全部被权限过滤时，隐藏这个空分组
  if (menuChildren.length === 0 && directChildren.some((r) => r.meta?.menu === true)) {
    return null
  }

  // Special case: Parent has menu = false but children have menu = true
  // In this case, skip the parent and promote children to parent's level
  // This allows grouping routes without showing the group itself in the menu
  if (!route.meta?.menu && menuChildren.length > 0) {
    return null
  }

  // If route has menu = false and no menu children, don't render at all
  // Requirement: 7.2 - Routes without meta.menu = true are not rendered
  if (!route.meta?.menu) {
    return null
  }

  // Create menu option from route metadata
  const title = route.meta?.title
  const menu: MenuOption = {
    key: route.path,
    label: renderMenuLabel(typeof title === 'string' ? t(title) : ''),
    // Requirement: 7.4 - Display icon if meta.icon is specified
    icon: route.meta?.icon ? createIconRender(route.meta.icon as string) : undefined,
    disabled: route.meta?.disabled as boolean | undefined,
  }

  // Recursively process child routes
  // Requirement: 7.3 - Preserve hierarchical structure
  const children = menuChildren
    .map((r) => routeToMenu(r, allRoutes, canView))
    .filter((m): m is MenuOption => m !== null)
    // Requirement: 7.5 - Sort menu items by meta.order
    .sort((a, b) => {
      const routeA = allRoutes.find((r) => r.path === a.key)
      const routeB = allRoutes.find((r) => r.path === b.key)
      // Default order is 999 (appears last) if not specified
      const orderA = (routeA?.meta?.order as number) ?? 999
      const orderB = (routeB?.meta?.order as number) ?? 999
      return orderA - orderB
    })

  // Add children to menu option if any exist
  if (children.length > 0) {
    menu.children = children
  }

  return menu
}

/**
 * Get menu options from routes
 *
 * This is the main hook for generating navigation menus. It processes all
 * routes registered with the router and creates a hierarchical menu structure
 * based on route metadata.
 *
 * Menu Generation Algorithm:
 * 1. Get all routes from router (includes static and dynamic routes)
 * 2. Filter to AdminLayout children only (paths starting with /admin/)
 * 3. Find top-level routes (no visible parent in menu hierarchy)
 * 4. Convert each top-level route to menu option (recursively processes children)
 * 5. Sort top-level menu items by meta.order
 *
 * The returned computed ref is reactive and automatically updates when:
 * - Dynamic routes are loaded after login
 * - Routes are added or removed from the router
 * - Route metadata changes
 *
 * Static and dynamic routes are treated equally - both appear in the menu
 * if they have meta.menu = true and are children of AdminLayout.
 *
 * @returns Computed ref containing array of top-level menu options
 *
 * @example
 * ```typescript
 * // In a Vue component
 * const menuOptions = useMenus()
 *
 * // menuOptions.value is an array of MenuOption objects
 * // Automatically updates when routes change
 * ```
 *
 * Requirements: 7.1 - AdminLayout menu filtering
 * Requirements: 7.2 - Menu flag rendering
 * Requirements: 7.3 - Menu hierarchy preservation
 * Requirements: 7.5 - Menu item sorting
 * Requirements: 8.3 - Complete menu rendering (static + dynamic)
 * Requirements: 8.4 - Partial failure menu rendering
 */
export function useMenus() {
  const router = useRouter()
  const userStore = useUserStore()

  const canView = (route: RouteRecordNormalized) => {
    const codes = splitPermissionCodes(route.meta?.permissionCode as string | undefined)
    if (codes.length === 0) return true
    return Boolean(route.meta?.permissionRequireAll)
      ? userStore.hasAllPermissions(codes)
      : userStore.hasAnyPermission(codes)
  }

  return computed(() => {
    // Get all routes currently registered with the router
    // This includes both static routes and any dynamic routes that have been loaded
    // Requirement: 8.3 - Include both static and dynamic routes
    const allRoutes = router.getRoutes()

    // Find the AdminLayout route
    // This is the parent route for all default layout modules
    const adminLayoutRoute = allRoutes.find((r) => r.name === 'AdminLayout')
    if (!adminLayoutRoute) {
      console.warn('[Menu] AdminLayout route not found')
      return []
    }

    // Filter to AdminLayout children only
    // Requirement: 7.1 - Only include routes from AdminLayout children
    // Routes must start with /admin/ and not be the /admin root itself
    const adminRoutes = allRoutes.filter((r) => {
      return r.path.startsWith('/admin/') && r.path !== '/admin'
    })

    // Find top-level routes for the menu
    // A route is top-level if it has no visible parent in the menu hierarchy
    // This means either:
    // 1. No other route is its parent, OR
    // 2. Its parent has meta.menu = false (parent is hidden)
    const topLevelRoutes = adminRoutes.filter((route) => {
      // Check if this route has a visible parent in the menu
      const hasVisibleParent = adminRoutes.some((potentialParent) => {
        // Don't compare route to itself
        if (potentialParent.path === route.path) return false
        // Check if potentialParent is actually a parent (one level up)
        if (!isDirectChild(route.path, potentialParent.path)) return false
        // Check if parent is visible in menu
        return potentialParent.meta?.menu === true
      })
      // Route is top-level if it has no visible parent
      return !hasVisibleParent
    })

    // Convert routes to menu options
    // Requirement: 7.2 - Only routes with meta.menu = true are rendered
    // Requirement: 7.3 - Hierarchical structure is preserved
    const menus: MenuOption[] = topLevelRoutes
      .filter(canView)
      .map((route) => routeToMenu(route, allRoutes, canView))
      .filter((m): m is MenuOption => m !== null)
      // Requirement: 7.5 - Sort menu items by meta.order
      .sort((a, b) => {
        const routeA = allRoutes.find((r) => r.path === a.key)
        const routeB = allRoutes.find((r) => r.path === b.key)
        // Default order is 999 (appears last) if not specified
        const orderA = (routeA?.meta?.order as number) ?? 999
        const orderB = (routeB?.meta?.order as number) ?? 999
        return orderA - orderB
      })

    // Log warning if no menu items found (may indicate configuration issue)
    if (menus.length === 0) {
      console.warn('[Menu] No menu items found - check route meta.menu flags')
    }

    return menus
  })
}

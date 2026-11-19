import { createRouter as createVueRouter, createWebHistory } from 'vue-router'
import type { Router } from 'vue-router'
import { AdminLayout, Layout } from '@/components/Layout'
import type { MenuConfig, ModuleConfig } from '@/types/module'
import { routeConfig } from '@/config/routeConfig'
import { DynamicRouteLoader } from '@/utils/dynamicRouteLoader'
import { loadModuleRoutes } from '@/utils/moduleRouteRegistration'
import { installNavigationGuards } from './guards/navigationGuards'
import { adminFallbackRoutes, coreRoutes, publicRoutes } from './routes'
import { setRouter } from './registry'

export interface RouterOptions {
  modules: ModuleConfig[]
  menuConfig: MenuConfig
  adminDefaultRedirect?: string
  appTitle?: string
}

export function createRouter(options: RouterOptions): Router {
  const moduleRouteResult = loadModuleRoutes(options.modules)
  const { adminLayoutRoutes, customLayoutRoutes, noneLayoutRoutes } = moduleRouteResult

  if (moduleRouteResult.failedModules.length > 0) {
    console.warn(
      '[Router] Some static modules failed to load:',
      moduleRouteResult.failedModules.map((module) => module.id),
    )
  }

  const router = createVueRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: [
      ...publicRoutes,
      ...noneLayoutRoutes,
      ...coreRoutes,
      {
        path: routeConfig.customRootPath,
        name: 'CustomLayout',
        redirect: routeConfig.customRedirect,
        component: Layout,
        children: customLayoutRoutes,
        meta: {
          keepAlive: true,
          transition: 'fade',
        },
      },
      {
        path: routeConfig.adminRootPath,
        name: 'AdminLayout',
        component: AdminLayout,
        redirect: routeConfig.adminDefaultRedirect,
        meta: {
          keepAlive: true,
          transition: 'fade',
        },
        children: [...adminLayoutRoutes, ...adminFallbackRoutes],
      },
    ],
  })

  const dynamicRouteLoader = new DynamicRouteLoader(router)

  installNavigationGuards(router, dynamicRouteLoader, {
    adminDefaultRedirect: options.adminDefaultRedirect ?? routeConfig.adminDefaultRedirect,
    appTitle: options.appTitle ?? import.meta.env.VITE_APP_TITLE,
    menuConfig: options.menuConfig,
  })

  setRouter(router)
  return router
}

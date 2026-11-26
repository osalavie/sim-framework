import { expect, test, type Page } from '@playwright/test'

const authorizationRoutes = [
  {
    id: 'root-authorization',
    moduleId: 'authorization-module-id',
    moduleName: '授权管理',
    parentId: null,
    name: 'Authorization',
    path: 'authorization',
    component: 'Layout',
    meta: { title: '授权管理', menu: true, tab: true, order: 1, disabled: true },
    children: [
      {
        id: 'route-management',
        moduleId: 'authorization-module-id',
        moduleName: '授权管理',
        parentId: 'root-authorization',
        name: 'RouteManagement',
        path: 'route',
        component: 'authorization/views/route/index',
        meta: { title: '路由管理', menu: true, tab: true, order: 1 },
      },
      {
        id: 'permission-management',
        moduleId: 'authorization-module-id',
        moduleName: '授权管理',
        parentId: 'root-authorization',
        name: 'PermissionManagement',
        path: 'permission',
        component: 'authorization/views/permission/index',
        meta: { title: '权限管理', menu: true, tab: true, order: 2, anonymous: true },
      },
    ],
  },
]

const approvalRoute = {
  id: 'root-approval',
  moduleId: 'approval-module-id',
  moduleName: '审批流程',
  parentId: null,
  name: 'Approval',
  path: 'approval',
  component: 'approval/views/todo/index',
  meta: { title: '审批流程', menu: true, tab: true, order: 2 },
}

const routePage = (routes: unknown[]) => ({
  items: routes,
  totalCount: routes.length,
})

const sessionPayload = {
  id: 'e2e-user-id',
  userName: 'e2e-admin',
  roles: [{ id: 'admin-role-id', code: 'Admin', name: '管理员' }],
  permissions: [{ id: 'all-permission-id', code: '*:*:*', name: '全部权限' }],
  accessToken: 'e2e-access-token',
}

const mockRouteApis = async (page: Page) => {
  // 直接访问后台路由时，导航守卫会用 Cookie 会话恢复登录态。
  await page.route('**/api/system/user/refresh-token', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(sessionPayload),
    })
  })
  await page.route('**/api/system/user/my-profile', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ id: 'e2e-user-id', userName: 'e2e-admin', nickName: 'e2e-admin' }),
    })
  })
  await page.route('**/api/system/route-config/user-accessible-routes', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        modules: [
          {
            id: 'authorization-module-id',
            name: '授权管理',
            enabled: true,
            order: 1,
            layout: 'admin',
            emptyRedirect: '/admin/authorization/route',
          },
        ],
        routes: [
          {
            moduleIds: ['authorization-module-id'],
            moduleName: '授权管理',
            path: 'authorization',
            name: 'Authorization',
            component: 'Layout',
            meta: { title: '授权管理', menu: true, order: 1 },
            children: [
              {
                moduleIds: ['authorization-module-id'],
                path: 'route',
                name: 'RouteManagement',
                component: 'authorization/views/route/index',
                meta: { title: '路由管理', menu: true, tab: true, keepAlive: true, order: 1 },
              },
              {
                moduleIds: ['authorization-module-id'],
                path: 'permission',
                name: 'PermissionManagement',
                component: 'authorization/views/permission/index',
                meta: { title: '权限管理', menu: true, tab: true, keepAlive: true, order: 2 },
              },
              {
                moduleIds: ['authorization-module-id'],
                path: 'user',
                name: 'UserManagement',
                component: 'authorization/views/user/index',
                meta: { title: '用户管理', menu: true, tab: true, keepAlive: true, order: 3 },
              },
              {
                moduleIds: ['authorization-module-id'],
                path: 'module',
                name: 'ModuleManagement',
                component: 'authorization/views/module/index',
                meta: { title: '模块管理', menu: true, tab: true, keepAlive: true, order: 4 },
              },
            ],
          },
        ],
      }),
    })
  })
  await page.route('**/api/system/module-config?**', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [{ id: 'authorization-module-id', name: '授权管理', enabled: true }],
        totalCount: 1,
      }),
    })
  })
  await page.route(/\/api\/system\/route-config\/routes-with-meta-paged(\?|$)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(routePage([authorizationRoutes[0], approvalRoute])),
    })
  })
  await page.route(/\/api\/system\/user(\?|$)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], totalCount: 0 }),
    })
  })
  await page.route(/\/api\/system\/route-config\/routes-with-meta(\?|$)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([authorizationRoutes[0], approvalRoute]),
    })
  })
}

test('路由管理卡片可筛选子路由，统计只展示治理状态', async ({ page }) => {
  await mockRouteApis(page)
  await page.addInitScript(() => localStorage.setItem('SIM_LOCALE', 'zh-CN'))

  await page.goto('/admin/authorization/route')
  await expect(page).toHaveURL(/\/admin\/authorization\/route$/)

  await page.setViewportSize({ width: 600, height: 900 })
  await expect(page.locator('.authz-card')).toHaveCount(2)

  const statLabels = await page
    .locator('.po-page-head__stat-label')
    .evaluateAll((nodes) => nodes.map((node) => node.textContent?.trim()))
  expect(statLabels).toEqual(['禁用路由', '匿名访问'])
  const statValues = await page
    .locator('.po-page-head__stat-value')
    .evaluateAll((nodes) => nodes.map((node) => node.textContent?.trim()))
  expect(statValues).toEqual(['1', '1'])

  const childChip = page.locator('button.authz-card__child-link').first()
  await expect(childChip).toHaveText(/子路由\s*2/)
  await childChip.click()

  const filterPill = page.locator('button.authz-parent-filter')
  await expect(filterPill).toBeVisible()
  await expect(filterPill).toContainText('父路由：Authorization')
  await expect(page.locator('.authz-card')).toHaveCount(2)
  const childNames = await page
    .locator('.authz-card__id b')
    .evaluateAll((nodes) => nodes.map((node) => node.textContent?.trim()))
  expect(childNames).toEqual(['RouteManagement', 'PermissionManagement'])
  await expect(page.locator('.data-card-list__pagination')).toHaveCount(0)

  await filterPill.click()
  await expect(page.locator('button.authz-parent-filter')).toHaveCount(0)
  await expect(page.locator('.authz-card')).toHaveCount(2)

  // 没有治理维度可展示的页面不再保留统计区
  await page.setViewportSize({ width: 1280, height: 900 })
  await page.goto('/admin/authorization/user')
  await expect(page).toHaveURL(/\/admin\/authorization\/user$/)
  await expect(page.locator('.po-toolbar')).toBeVisible()
  await expect(page.locator('.po-page-head')).toHaveCount(0)

  // 有启用/停用状态的页面只保留该维度
  await page.goto('/admin/authorization/module')
  await expect(page).toHaveURL(/\/admin\/authorization\/module$/)
  await expect(page.locator('.po-page-head__stat-label').first()).toBeVisible()
  const moduleLabels = await page
    .locator('.po-page-head__stat-label')
    .evaluateAll((nodes) => nodes.map((node) => node.textContent?.trim()))
  expect(moduleLabels).toEqual(['启用模块', '停用模块'])
  const moduleValues = await page
    .locator('.po-page-head__stat-value')
    .evaluateAll((nodes) => nodes.map((node) => node.textContent?.trim()))
  expect(moduleValues).toEqual(['1', '0'])
})

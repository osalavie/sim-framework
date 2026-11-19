import { expect, test } from '@playwright/test'

test('用户登录后进入受保护页面，令牌不写入持久化存储', async ({ page }) => {
  let loginPayload: unknown

  await page.route('**/api/system/user/login', async (route) => {
    loginPayload = route.request().postDataJSON()
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'e2e-user-id',
        userName: 'e2e-admin',
        roles: [{ id: 'admin-role-id', code: 'Admin', name: '管理员' }],
        permissions: [{ id: 'all-permission-id', code: '*:*:*', name: '全部权限' }],
        accessToken: 'e2e-access-token',
      }),
    })
  })

  await page.route('**/api/system/module-config/user-modules', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
  })

  await page.goto('/login')
  await page.getByRole('textbox', { name: /用户名|Username/ }).fill('e2e-admin')
  await page.getByRole('textbox', { name: /密码|Password/ }).fill('StrongPassword!123')
  await page.getByRole('button', { name: /登录|Sign in/, exact: true }).click()

  await expect(page).toHaveURL(/\/admin\/home\/index$/)
  await expect(page.getByTestId('admin-home')).toBeVisible()
  // 登录请求除凭据外还会携带设备指纹（deviceId/deviceName/deviceType），
  // 这里只断言凭据字段，避免设备信息演进时误报。
  expect(loginPayload).toMatchObject({ userName: 'e2e-admin', password: 'StrongPassword!123' })

  const persistedTokens = await page.evaluate(() => ({
    accessToken: localStorage.getItem('ACCESS_TOKEN'),
    refreshToken: localStorage.getItem('REFRESH_TOKEN'),
  }))
  expect(persistedTokens).toEqual({ accessToken: null, refreshToken: null })
})

test('重新登录后首次打开权限管理只加载一轮并立即显示数据', async ({ page }) => {
  let moduleListRequests = 0
  let permissionListRequests = 0

  await page.route('**/api/system/user/login', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'e2e-user-id',
        userName: 'e2e-admin',
        roles: [{ id: 'admin-role-id', code: 'Admin', name: '管理员' }],
        permissions: [{ id: 'all-permission-id', code: '*:*:*', name: '全部权限' }],
        accessToken: 'e2e-access-token',
      }),
    })
  })
  await page.route('**/api/system/user/antiforgery-token', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ requestToken: 'e2e-xsrf-token' }),
    })
  })
  await page.route('**/api/system/user/revoke-refresh-token', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
  })
  await page.route('**/api/system/module-config/user-modules', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: 'authorization-module-id',
          name: '授权管理',
          enabled: true,
          layout: 'admin',
          emptyRedirect: '/admin/authorization/permission',
        },
      ]),
    })
  })
  await page.route('**/api/system/route-config/module-routes/authorization-module-id', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          path: 'authorization',
          name: 'Authorization',
          component: 'Layout',
          meta: { title: '授权管理', menu: true, order: 1 },
          children: [
            {
              path: 'permission',
              name: 'PermissionManagement',
              component: 'authorization/views/permission/index',
              meta: {
                title: '权限管理',
                menu: true,
                tab: true,
                keepAlive: true,
                order: 1,
              },
            },
          ],
        },
      ]),
    })
  })
  await page.route('**/api/system/module-config?**', async (route) => {
    moduleListRequests += 1
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], totalCount: 0 }),
    })
  })
  await page.route('**/api/system/permission?**', async (route) => {
    permissionListRequests += 1
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          {
            id: 'permission-id',
            name: '测试权限',
            code: 'test:permission',
            description: '用于菜单加载回归测试',
          },
        ],
        totalCount: 1,
      }),
    })
  })

  const login = async () => {
    await page.goto('/login')
    await page.getByRole('textbox', { name: /用户名|Username/ }).fill('e2e-admin')
    await page.getByRole('textbox', { name: /密码|Password/ }).fill('StrongPassword!123')
    await page.getByRole('button', { name: /登录|Sign in/, exact: true }).click()
    // 登录后的落点由默认重定向或当前可用模块的空态重定向决定，两种都在后台布局内。
    await expect(page).toHaveURL(/\/admin\//)
  }

  const openPermissionPage = async (expectedRequestCount: number) => {
    // 顶栏页面标题与标签页也会渲染同名文本，这里限定在侧栏导航内点击。
    const sidebar = page.getByRole('complementary')
    await sidebar.getByText('授权管理', { exact: true }).click()
    await sidebar.getByText('权限管理', { exact: true }).click()
    await expect(page).toHaveURL(/\/admin\/authorization\/permission$/)
    await expect(page.getByText('测试权限', { exact: true })).toBeVisible()
    expect(moduleListRequests).toBe(expectedRequestCount)
    expect(permissionListRequests).toBe(expectedRequestCount)
  }

  await login()
  await openPermissionPage(1)

  await page.locator('.user-info-area .account-chip').click()
  await page.getByText(/退出登录|Sign out/, { exact: true }).click()
  await expect(page).toHaveURL(/\/login$/)

  await login()
  await openPermissionPage(2)
})

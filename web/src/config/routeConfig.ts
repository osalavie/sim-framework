export const routeConfig = {
  customRootPath: import.meta.env.VITE_CUSTOM_ROOT_PATH || '/',
  customRedirect: import.meta.env.VITE_CUSTOM_REDIRECT || '/admin/home',
  adminRootPath: import.meta.env.VITE_ADMIN_ROOT_PATH || '/admin',
  adminDefaultRedirect: import.meta.env.VITE_ADMIN_DEFAULT_REDIRECT || '/admin/home',
} as const

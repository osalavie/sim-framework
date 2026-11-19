import { createApp } from '@/framework'
import { bootstrapBusinessModules } from '@/config/businessModules'
import { menuConfig, modules } from '@/config/modules'

bootstrapBusinessModules()

void createApp({
  modules,
  menuConfig,
  appTitle: import.meta.env.VITE_APP_TITLE,
})

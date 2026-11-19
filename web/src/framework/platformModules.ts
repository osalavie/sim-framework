import type { ModuleConfig } from '@/types/module'
import { accountModule } from '@/modules/account'
import { approvalModule } from '@/modules/approval'
import { authorizationModule } from '@/modules/authorization'
import { homeModule } from '@/modules/home'
import { openPlatformModule } from '@/modules/openplatform'
import { customLayoutModule } from '@/modules/_template-custom-layout'
import { noLayoutModule } from '@/modules/_template-no-layout'

/**
 * 框架自带的平台模块。业务应用按需追加自己的模块。
 */
export const platformModules: ModuleConfig[] = [
  accountModule,
  approvalModule,
  authorizationModule,
  homeModule,
  openPlatformModule,
  customLayoutModule,
  noLayoutModule,
]

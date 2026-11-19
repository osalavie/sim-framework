import type { permissionDirective } from '@/directives/permission'
import type { tooltipDirective } from '@/directives/tooltip'

declare module 'vue' {
  interface GlobalDirectives {
    vPermission: typeof permissionDirective
    vTooltip: typeof tooltipDirective
  }
}

export {}

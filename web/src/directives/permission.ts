import type { Directive } from 'vue'
import { useUserStore } from '@/stores/user'

export type PermissionValue = string | readonly string[]

function applyPermission(el: HTMLElement, value: PermissionValue | undefined) {
  if (!value) return
  const allowed = useUserStore().hasAnyPermission(value)
  el.style.display = allowed ? '' : 'none'
}

export const permissionDirective: Directive<HTMLElement, PermissionValue> = {
  mounted(el, binding) {
    applyPermission(el, binding.value)
  },
  updated(el, binding) {
    applyPermission(el, binding.value)
  },
}

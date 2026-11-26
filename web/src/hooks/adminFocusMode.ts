import { readonly, ref } from 'vue'

/** 文档编辑页的专注模式：隐藏后台壳（顶栏、侧栏、标签条），页面自身保留顶部状态与该编辑器底栏。 */
const focusMode = ref(false)

export function useAdminFocusMode() {
  return {
    focusMode: readonly(focusMode),
    setFocusMode(active: boolean) {
      focusMode.value = active
    },
    toggleFocusMode() {
      focusMode.value = !focusMode.value
    },
  }
}

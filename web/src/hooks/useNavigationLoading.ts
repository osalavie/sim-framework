import { ref } from 'vue'

/**
 * Global navigation loading state.
 * Set to true when a route navigation starts, false when it completes.
 */
const isNavigating = ref(false)
const navigateToLayout = ref<string>('')

/**
 * Start navigation loading and record the target layout type.
 */
export function startNavigationLoading(layout?: string) {
  isNavigating.value = true
  navigateToLayout.value = layout || ''
}

/**
 * Complete navigation loading.
 */
export function stopNavigationLoading() {
  isNavigating.value = false
}

/**
 * Composable for components to reactively access navigation loading state.
 */
export function useNavigationLoading() {
  return {
    isNavigating,
    navigateToLayout,
  }
}

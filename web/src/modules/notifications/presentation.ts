export type NotificationPresentationTarget = 'in-app' | 'browser' | 'none'

interface NotificationPresentationContext {
  initialized: boolean
  pageVisible: boolean
  pageFocused: boolean
  browserPermission: NotificationPermission | 'unsupported'
}

export function resolveNotificationPresentationTarget({
  initialized,
  pageVisible,
  pageFocused,
  browserPermission,
}: NotificationPresentationContext): NotificationPresentationTarget {
  if (!initialized) return 'none'
  if (pageVisible && pageFocused) return 'in-app'
  return browserPermission === 'granted' ? 'browser' : 'none'
}

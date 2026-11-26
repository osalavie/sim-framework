export interface TabOption {
  name: string // Unique name for the tab
  path: string // Unique identifier for the tab panel component
  icon?: string
  title: string // Display name
  /** 直接展示 title（用户内容或业务标题），不经过 i18n 查表 */
  rawTitle?: boolean
  pinned: boolean
}

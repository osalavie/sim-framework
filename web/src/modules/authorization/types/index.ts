/**
 * 授权模块共享类型定义
 */

// 排序信息
export interface SortInfo {
  field: string
  order: 'ascend' | 'descend'
}

// 分页信息
export interface PaginationInfo {
  page: number
  pageSize: number
  pageCount: number
  itemCount: number
}

// 查询参数基类
export interface BaseQueryParams {
  skipCount?: number
  maxResultCount?: number
  keyword?: string
  sorting?: string
}

// 分页结果基类
export interface PagedResult<T> {
  items: T[]
  totalCount: number
}

// 实体基类
export interface BaseEntity {
  id: string
  creationTime?: string
  creatorId?: string | null
  lastModificationTime?: string
  lastModifierId?: string | null
}

// 表单数据基类
export interface BaseFormData {
  [key: string]: unknown
}

// 操作类型
export type ActionType = 'view' | 'edit' | 'delete' | 'create' | 'assign'

// 按钮类型
export type ButtonType = 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error'

// 按钮大小
export type ButtonSize = 'tiny' | 'small' | 'medium' | 'large'

// 标签类型
export type TagType = 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error'

// 视图模式
export type ViewMode = 'table' | 'card'

// 模态框模式
export type ModalMode = 'create' | 'edit' | 'view'

// 加载状态
export interface LoadingState {
  loading: boolean
  submitting: boolean
}

// 错误状态
export interface ErrorState {
  hasError: boolean
  errorMessage: string
}

// 选择状态
export interface SelectionState<T> {
  selectedKeys: (string | number)[]
  selectedItems: T[]
}

// 搜索状态
export interface SearchState {
  keyword: string
  filters: Record<string, unknown>
}

// 排序状态
export interface SortState {
  sortFields: SortInfo[]
}

// 分页状态
export interface PaginationState {
  pagination: PaginationInfo
}

// 完整的列表状态
export interface ListState<T> extends LoadingState, SearchState, SortState, PaginationState {
  items: T[]
}

// 表格列配置
export interface TableColumn<T = unknown> {
  key: string
  title: string
  width?: number
  minWidth?: number
  maxWidth?: number
  resizable?: boolean
  ellipsis?: boolean | { tooltip: boolean }
  sorter?: boolean | 'default'
  sortOrder?: 'ascend' | 'descend' | false
  render?: (row: T) => unknown
}

// 卡片配置
export interface CardConfig {
  columns: 1 | 2 | 3 | 4
  clickable: boolean
  hoverable: boolean
  previewImage: boolean
}

// 表单配置
export interface FormConfig {
  labelPlacement: 'left' | 'top'
  labelWidth: number | string
  showFeedback: boolean
  showLabel: boolean
  showRequireMark: boolean
}

// 验证规则
export interface ValidationRule {
  required?: boolean
  message?: string
  trigger?: 'blur' | 'change' | 'input'
  type?: 'string' | 'number' | 'boolean' | 'email' | 'url' | 'date'
  min?: number
  max?: number
  pattern?: RegExp
  validator?: (rule: unknown, value: unknown) => boolean | Error
}

// 操作按钮配置
export interface ActionButton {
  key: string
  label: string
  type?: ButtonType
  size?: ButtonSize
  icon?: unknown
  disabled?: boolean
  loading?: boolean
  onClick: () => void
}

// 菜单项配置
export interface MenuItem {
  key: string
  label: string
  icon?: unknown
  disabled?: boolean
  children?: MenuItem[]
  onClick?: () => void
}

// 面包屑项
export interface BreadcrumbItem {
  label: string
  path?: string
}

// 标签页项
export interface TabItem {
  key: string
  label: string
  closable?: boolean
  disabled?: boolean
}

// 下拉选项
export interface SelectOption<T = unknown> {
  label: string
  value: T
  disabled?: boolean
  children?: SelectOption<T>[]
}

// 树节点
export interface TreeNode<T = unknown> {
  key: string
  label: string
  children?: TreeNode<T>[]
  disabled?: boolean
  isLeaf?: boolean
  data?: T
}

// 上传文件
export interface UploadFile {
  id: string
  name: string
  status: 'pending' | 'uploading' | 'success' | 'error'
  percentage: number
  url?: string
  error?: string
}

// 通知配置
export interface NotificationConfig {
  title: string
  content?: string
  type?: 'info' | 'success' | 'warning' | 'error'
  duration?: number
}

// 对话框配置
export interface DialogConfig {
  title: string
  content: string
  positiveText?: string
  negativeText?: string
  onPositiveClick?: () => void | Promise<void>
  onNegativeClick?: () => void
}

// 消息配置
export interface MessageConfig {
  content: string
  type?: 'info' | 'success' | 'warning' | 'error' | 'loading'
  duration?: number
}

// 主题配置
export interface ThemeConfig {
  primaryColor: string
  errorColor: string
  warningColor: string
  successColor: string
  infoColor: string
}

// 响应式断点
export interface ResponsiveBreakpoints {
  mobile: number
  tablet: number
  desktop: number
  wide: number
}

// 屏幕尺寸
export type ScreenSize = 'mobile' | 'tablet' | 'desktop' | 'wide'

// 设备类型
export type DeviceType = 'mobile' | 'tablet' | 'desktop'

// 方向
export type Orientation = 'portrait' | 'landscape'

// 触摸事件
export interface TouchEvent {
  startX: number
  startY: number
  endX: number
  endY: number
  deltaX: number
  deltaY: number
  duration: number
}

// 手势类型
export type GestureType = 'tap' | 'swipe' | 'pinch' | 'rotate'

// 键盘快捷键
export interface KeyboardShortcut {
  key: string
  ctrl?: boolean
  shift?: boolean
  alt?: boolean
  meta?: boolean
  handler: (event: KeyboardEvent) => void
  description?: string
}

// 路由元信息
export interface RouteMeta {
  title?: string
  icon?: string
  menu?: boolean
  tab?: boolean
  keepAlive?: boolean
  pinned?: boolean
  anonymous?: boolean
  disabled?: boolean
  order?: number
  module?: string
  permission?: string
}

// 权限检查结果
export interface PermissionCheckResult {
  hasPermission: boolean
  missingPermissions: string[]
}

// 审计日志
export interface AuditLog {
  id: string
  userId: string
  userName: string
  action: string
  resource: string
  timestamp: string
  details?: Record<string, unknown>
}

// 导出配置
export interface ExportConfig {
  filename: string
  format: 'csv' | 'excel' | 'json'
  columns?: string[]
  filters?: Record<string, unknown>
}

// 导入配置
export interface ImportConfig {
  file: File
  format: 'csv' | 'excel' | 'json'
  mapping?: Record<string, string>
  validate?: boolean
}

// 批量操作结果
export interface BatchOperationResult {
  success: number
  failed: number
  errors: Array<{ index: number; error: string }>
}

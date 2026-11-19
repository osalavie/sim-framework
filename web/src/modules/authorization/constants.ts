/**
 * 授权模块常量定义
 */

// 性别选项
export const SEX_OPTIONS = [
  { label: '未知', value: 0 },
  { label: '男', value: 1 },
  { label: '女', value: 2 },
] as const

// 性别标签映射
export const SEX_LABELS: Record<number, string> = {
  0: '未知',
  1: '男',
  2: '女',
}

// 默认分页配置
export const DEFAULT_PAGINATION = {
  page: 1,
  pageSize: 10,
  pageCount: 1,
  itemCount: 0,
} as const

// 分页大小选项
export const PAGE_SIZE_OPTIONS = [10, 20, 30, 50, 100] as const

// 表单验证规则
export const VALIDATION_RULES = {
  // 用户名规则
  userName: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 3, max: 20, message: '用户名长度在 3 到 20 个字符', trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9_]+$/, message: '用户名只能包含字母、数字和下划线', trigger: 'blur' },
  ],

  // 密码规则
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, max: 20, message: '密码长度在 6 到 20 个字符', trigger: 'blur' },
  ],

  // 邮箱规则
  email: [
    { type: 'email', message: '请输入有效的邮箱地址', trigger: 'blur' },
  ],

  // 手机号规则
  phone: [
    { pattern: /^1[3-9]\d{9}$/, message: '请输入有效的手机号', trigger: 'blur' },
  ],

  // 编码规则
  code: [
    { required: true, message: '请输入编码', trigger: 'blur' },
    { min: 2, max: 50, message: '编码长度在 2 到 50 个字符', trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9_.-]+$/, message: '编码只能包含字母、数字、下划线、点和横线', trigger: 'blur' },
  ],

  // 名称规则
  name: [
    { required: true, message: '请输入名称', trigger: 'blur' },
    { min: 2, max: 50, message: '名称长度在 2 到 50 个字符', trigger: 'blur' },
  ],

  // 路径规则
  path: [
    { required: true, message: '请输入路径', trigger: 'blur' },
    { pattern: /^\/[a-zA-Z0-9/_-]*$/, message: '路径必须以 / 开头，只能包含字母、数字、下划线、横线和斜杠', trigger: 'blur' },
  ],
} as const

// 模态框默认样式
export const MODAL_STYLES = {
  default: {
    width: '700px',
    maxHeight: '85vh',
  },
  large: {
    width: '900px',
    maxHeight: '85vh',
  },
  small: {
    width: '500px',
    maxHeight: '85vh',
  },
} as const

// 滚动条主题覆盖
export const SCROLLBAR_THEME_OVERRIDES = {
  width: '0px',
  height: '4px',
  borderRadius: '2px',
} as const

// 操作按钮类型
export const ACTION_BUTTON_TYPES = {
  view: 'default',
  edit: 'primary',
  delete: 'error',
  assign: 'info',
} as const

// 操作按钮标签
export const ACTION_BUTTON_LABELS = {
  view: '查看',
  edit: '编辑',
  delete: '删除',
  create: '新建',
  save: '保存',
  cancel: '取消',
  confirm: '确定',
  assign: '分配',
  refresh: '刷新',
  search: '搜索',
  reset: '重置',
  export: '导出',
  import: '导入',
} as const

// 消息提示文本
export const MESSAGE_TEXTS = {
  createSuccess: '创建成功',
  updateSuccess: '更新成功',
  deleteSuccess: '删除成功',
  createFailed: '创建失败',
  updateFailed: '更新失败',
  deleteFailed: '删除失败',
  loadFailed: '加载数据失败',
  operationSuccess: '操作成功',
  operationFailed: '操作失败',
  confirmDelete: '确定要删除这条记录吗？此操作不可恢复。',
  confirmDeleteTitle: '确认删除',
  hasRelatedData: '该记录存在关联数据，删除后关联数据也将受到影响，确定要删除吗？',
} as const

// 空状态文本
export const EMPTY_TEXTS = {
  noData: '暂无数据',
  noSearchResult: '未找到匹配的数据',
  noPermission: '暂无权限',
  noModule: '暂无模块',
  noRoute: '暂无路由',
} as const

// 表格列宽度
export const TABLE_COLUMN_WIDTHS = {
  id: 80,
  code: 150,
  name: 150,
  userName: 150,
  email: 200,
  phone: 150,
  sex: 80,
  status: 100,
  enabled: 100,
  order: 80,
  icon: 80,
  creationTime: 180,
  lastModificationTime: 180,
  actions: 200,
} as const

// 日期格式
export const DATE_FORMATS = {
  date: 'YYYY-MM-DD',
  datetime: 'YYYY-MM-DD HH:mm',
  datetimeSecond: 'YYYY-MM-DD HH:mm:ss',
  time: 'HH:mm',
  timeSecond: 'HH:mm:ss',
} as const

// 排序方向
export const SORT_ORDERS = {
  ascend: 'asc',
  descend: 'desc',
} as const

// 布局选项
export const LAYOUT_OPTIONS = [
  { label: '默认布局', value: 'default' },
  { label: '空白布局', value: 'blank' },
  { label: '自定义布局', value: 'custom' },
] as const

// 图标选项（常用图标）
export const ICON_OPTIONS = [
  'home',
  'user',
  'users',
  'settings',
  'lock',
  'unlock',
  'key',
  'shield',
  'document',
  'folder',
  'file',
  'image',
  'video',
  'music',
  'calendar',
  'clock',
  'mail',
  'phone',
  'location',
  'map',
  'search',
  'filter',
  'edit',
  'delete',
  'add',
  'remove',
  'check',
  'close',
  'arrow-up',
  'arrow-down',
  'arrow-left',
  'arrow-right',
] as const

// 请求超时时间（毫秒）
export const REQUEST_TIMEOUT = 30000

// 防抖延迟（毫秒）
export const DEBOUNCE_DELAY = 300

// 节流延迟（毫秒）
export const THROTTLE_DELAY = 1000

// 缓存过期时间（毫秒）
export const CACHE_EXPIRY = 5 * 60 * 1000 // 5 分钟

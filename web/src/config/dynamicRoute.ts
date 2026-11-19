/**
 * 动态路由配置
 */

/**
 * 组件加载策略
 * - 'auto' / 'registry': 使用组件注册表自动扫描（推荐）
 *   优点：无需手动注册，使用 Vite glob import 自动发现组件
 *   缺点：会扫描所有组件文件
 *
 * - 'map': 使用组件映射表
 *   优点：明确的组件引用，只加载需要的组件
 *   缺点：需要手动注册每个组件
 */
export const COMPONENT_LOAD_STRATEGY: 'auto' | 'registry' | 'map' = 'auto'

/**
 * 组件搜索路径（按优先级排序）
 * 当使用 'auto' 策略时，会按顺序尝试这些路径
 */
export const COMPONENT_BASE_PATHS = [
  '@/views/dynamic/', // 动态路由专用目录（推荐）
  '@/views/', // 通用视图目录
  '@/modules/', // 模块目录
]

/**
 * 是否在组件加载失败时显示 404 页面
 * true: 显示 404 页面
 * false: 返回 null，路由不会被注册
 */
export const SHOW_404_ON_COMPONENT_ERROR = true

/**
 * 是否在控制台输出详细的加载日志
 */
export const ENABLE_LOAD_LOGGING =
  import.meta.env.DEV && import.meta.env.VITE_ENABLE_LOAD_LOGGING === 'true'

/**
 * 组件路径别名
 * 可以为常用组件定义简短的别名
 */
export const COMPONENT_ALIASES: Record<string, string> = {
  // 兼容早期后端种子数据中的组件路径；新数据应直接使用右侧规范路径。
  '/modules/user/views/Profile.vue': 'authorization/views/user/Profile.vue',
  '/modules/user/views/Settings.vue': 'authorization/views/user/Settings.vue',
}

/**
 * 组件注册表
 * 使用 Vite 的 glob import 自动发现和注册组件
 */
import type { Component } from 'vue'

/** 统一 glob 键与基础路径：去相对前缀、源码根前缀并统一斜杠。 */
function normalizeRegistryPath(path: string): string {
  return path
    .replace(/\\/g, '/')
    .replace(/^\.\//, '')
    .replace(/^\/src\//, '')
    .replace(/^@\//, '')
}

/**
 * 组件注册表
 */
class ComponentRegistry {
  private components: Map<string, () => Promise<Component>> = new Map()
  private initialized = false

  /**
   * 初始化组件注册表
   * 使用 Vite 的 import.meta.glob 自动扫描组件
   */
  initialize() {
    if (this.initialized) return

    // 扫描 views/dynamic 目录下的所有 .vue 文件
    const dynamicModules = import.meta.glob('@/views/dynamic/**/*.vue')

    // 扫描 views 目录下的所有 .vue 文件
    const viewModules = import.meta.glob('@/views/**/*.vue')

    // 扫描 modules 目录下的所有 .vue 文件
    const moduleModules = import.meta.glob('@/modules/**/*.vue')

    // 注册所有组件；业务应用预注册的同名组件优先保留
    this.registerModules(dynamicModules, '@/views/dynamic/', false)
    this.registerModules(viewModules, '@/views/', false)
    this.registerModules(moduleModules, '@/modules/', false)

    this.initialized = true
  }

  /**
   * 注册业务应用提供的 glob 组件
   *
   * 框架库构建期只能扫描自身的源码目录，业务仓的页面需要通过该方法在
   * `createApp` 之前把 `import.meta.glob` 结果交给注册表。
   */
  registerGlob(modules: Record<string, () => Promise<unknown>>, basePath: string) {
    this.registerModules(modules, basePath, true)
  }

  /**
   * 注册模块
   */
  private registerModules(
    modules: Record<string, () => Promise<unknown>>,
    basePath: string,
    overwrite: boolean,
  ) {
    const basePathNormalized = normalizeRegistryPath(basePath)

    for (const [path, loader] of Object.entries(modules)) {
      let relativePath = normalizeRegistryPath(path)

      // 移除基础路径前缀
      if (relativePath.startsWith(basePathNormalized)) {
        relativePath = relativePath.substring(basePathNormalized.length)
      }

      this.setComponent(relativePath, loader as () => Promise<Component>, overwrite)

      // 同时注册不带 .vue 后缀的路径
      if (relativePath.endsWith('.vue')) {
        this.setComponent(relativePath.slice(0, -4), loader as () => Promise<Component>, overwrite)
      }
    }
  }

  private setComponent(
    path: string,
    loader: () => Promise<Component>,
    overwrite: boolean,
  ): void {
    if (!overwrite && this.components.has(path)) return
    this.components.set(path, loader)
  }

  /**
   * 获取组件加载器
   */
  getComponent(path: string): (() => Promise<Component>) | undefined {
    if (!this.initialized) {
      this.initialize()
    }

    // 尝试直接匹配
    let component = this.components.get(path)
    if (component) {
      return component
    }

    // 尝试添加 .vue 后缀
    if (!path.endsWith('.vue')) {
      component = this.components.get(`${path}.vue`)
      if (component) {
        return component
      }
    }

    // 尝试添加 /index.vue
    if (!path.endsWith('.vue')) {
      component = this.components.get(`${path}/index.vue`)
      if (component) {
        return component
      }
    }

    console.warn(`Component not found: ${path}`)
    return undefined
  }

  /**
   * 手动注册组件
   */
  register(path: string, loader: () => Promise<Component>) {
    this.components.set(path, loader)
  }

  /**
   * 批量注册组件
   */
  registerBatch(components: Record<string, () => Promise<Component>>) {
    for (const [path, loader] of Object.entries(components)) {
      this.register(path, loader)
    }
  }

  /**
   * 获取所有已注册的组件路径
   */
  getAllPaths(): string[] {
    if (!this.initialized) {
      this.initialize()
    }
    return Array.from(this.components.keys())
  }

  /**
   * 检查组件是否存在
   */
  has(path: string): boolean {
    return this.getComponent(path) !== undefined
  }

  /**
   * 清空注册表
   */
  clear() {
    this.components.clear()
    this.initialized = false
  }
}

// 导出单例
export const componentRegistry = new ComponentRegistry()

/**
 * 注册业务应用的模块页面
 *
 * @example
 * ```ts
 * registerModuleComponents(import.meta.glob('./modules/**\/*.vue'), 'modules/')
 * ```
 */
export function registerModuleComponents(
  modules: Record<string, () => Promise<unknown>>,
  basePath: string,
): void {
  componentRegistry.registerGlob(modules, basePath)
}

// 自动初始化（可选）
if (import.meta.env.DEV) {
  componentRegistry.initialize()
}

import { ref, onMounted, onUnmounted } from 'vue'

/**
 * 响应式断点配置
 */
export const BREAKPOINTS = {
  /** 移动端（小屏幕） */
  mobile: 480,
  /** 平板端（窄屏） */
  tablet: 768,
  /** 桌面端（中等屏幕） */
  desktop: 1024,
  /** 大屏幕 */
  wide: 1280,
} as const

/**
 * 屏幕尺寸类型
 */
export type ScreenSize = 'mobile' | 'tablet' | 'desktop' | 'wide'

/**
 * 全局响应式状态（单例模式）
 */
class ResponsiveState {
  private static instance: ResponsiveState
  private listeners: Set<() => void> = new Set()
  private _isNarrowScreen = ref(false)
  private _isMobile = ref(false)
  private _isTablet = ref(false)
  private _isDesktop = ref(false)
  private _isWide = ref(false)
  private _screenSize = ref<ScreenSize>('desktop')
  private _screenWidth = ref(0)
  private resizeHandler: (() => void) | null = null

  private constructor() {
    this.checkScreenSize()
    this.setupResizeListener()
  }

  static getInstance(): ResponsiveState {
    if (!ResponsiveState.instance) {
      ResponsiveState.instance = new ResponsiveState()
    }
    return ResponsiveState.instance
  }

  private checkScreenSize() {
    const width = window.innerWidth
    this._screenWidth.value = width

    // 窄屏判断（平板及以下）
    this._isNarrowScreen.value = width <= BREAKPOINTS.tablet

    // 各尺寸判断
    this._isMobile.value = width <= BREAKPOINTS.mobile
    this._isTablet.value = width > BREAKPOINTS.mobile && width <= BREAKPOINTS.tablet
    this._isDesktop.value = width > BREAKPOINTS.tablet && width <= BREAKPOINTS.wide
    this._isWide.value = width > BREAKPOINTS.wide

    // 确定当前屏幕尺寸类型
    if (this._isMobile.value) {
      this._screenSize.value = 'mobile'
    } else if (this._isTablet.value) {
      this._screenSize.value = 'tablet'
    } else if (this._isDesktop.value) {
      this._screenSize.value = 'desktop'
    } else {
      this._screenSize.value = 'wide'
    }

    // 通知所有监听器
    this.notifyListeners()
  }

  private setupResizeListener() {
    // 防抖处理
    let timeoutId: number | null = null
    this.resizeHandler = () => {
      if (timeoutId !== null) {
        clearTimeout(timeoutId)
      }
      timeoutId = window.setTimeout(() => {
        this.checkScreenSize()
      }, 150)
    }
    window.addEventListener('resize', this.resizeHandler)
  }

  private notifyListeners() {
    this.listeners.forEach((listener) => listener())
  }

  addListener(listener: () => void) {
    this.listeners.add(listener)
  }

  removeListener(listener: () => void) {
    this.listeners.delete(listener)
  }

  get isNarrowScreen() {
    return this._isNarrowScreen
  }

  get isMobile() {
    return this._isMobile
  }

  get isTablet() {
    return this._isTablet
  }

  get isDesktop() {
    return this._isDesktop
  }

  get isWide() {
    return this._isWide
  }

  get screenSize() {
    return this._screenSize
  }

  get screenWidth() {
    return this._screenWidth
  }

  destroy() {
    if (this.resizeHandler) {
      window.removeEventListener('resize', this.resizeHandler)
      this.resizeHandler = null
    }
    this.listeners.clear()
  }
}

/**
 * 响应式屏幕尺寸检测 Hook
 *
 * @example
 * ```ts
 * const { isNarrowScreen, isMobile, screenSize } = useResponsive()
 *
 * // 在模板中使用
 * <div v-if="isNarrowScreen">移动端布局</div>
 * <div v-else>桌面端布局</div>
 * ```
 */
export function useResponsive() {
  const state = ResponsiveState.getInstance()

  // 组件挂载时确保状态是最新的
  onMounted(() => {
    // 强制检查一次（确保初始状态正确）
    state['checkScreenSize']()
  })

  return {
    /** 是否为窄屏（平板及以下，≤768px） */
    isNarrowScreen: state.isNarrowScreen,
    /** 是否为移动端（≤480px） */
    isMobile: state.isMobile,
    /** 是否为平板端（481px-768px） */
    isTablet: state.isTablet,
    /** 是否为桌面端（769px-1280px） */
    isDesktop: state.isDesktop,
    /** 是否为宽屏（>1280px） */
    isWide: state.isWide,
    /** 当前屏幕尺寸类型 */
    screenSize: state.screenSize,
    /** 当前屏幕宽度（像素） */
    screenWidth: state.screenWidth,
  }
}

/**
 * 检查是否匹配指定的媒体查询
 *
 * @param query - CSS 媒体查询字符串
 * @example
 * ```ts
 * const matches = useMediaQuery('(min-width: 768px)')
 * ```
 */
export function useMediaQuery(query: string) {
  const matches = ref(false)
  let mediaQuery: MediaQueryList | null = null
  let handler: ((e: MediaQueryListEvent) => void) | null = null

  const updateMatches = (e: MediaQueryList | MediaQueryListEvent) => {
    matches.value = e.matches
  }

  onMounted(() => {
    // 测试 / 非浏览器环境没有 matchMedia，按不匹配处理
    if (typeof window.matchMedia !== 'function') return
    mediaQuery = window.matchMedia(query)
    updateMatches(mediaQuery)

    handler = (e: MediaQueryListEvent) => updateMatches(e)
    mediaQuery.addEventListener('change', handler)
  })

  onUnmounted(() => {
    if (mediaQuery && handler) {
      mediaQuery.removeEventListener('change', handler)
    }
  })

  return matches
}

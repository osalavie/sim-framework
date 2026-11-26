import { computed } from 'vue'
import { useResponsive } from './useResponsive'

interface ModalStyleOptions {
  desktopWidth?: string
  tabletWidth?: string
  mobileWidth?: string
  maxHeight?: string
}

/**
 * 响应式模态框样式 Hook
 * 根据屏幕尺寸自动调整模态框宽度和高度
 */
export function useResponsiveModal(options: ModalStyleOptions = {}) {
  const {
    desktopWidth = '700px',
    tabletWidth = '90vw',
    mobileWidth = '95vw',
    maxHeight = '85vh',
  } = options

  const { isMobile, isTablet } = useResponsive()

  // 模态框样式
  const modalStyle = computed(() => {
    let width = desktopWidth

    if (isMobile.value) {
      width = mobileWidth
    } else if (isTablet.value) {
      width = tabletWidth
    }

    return {
      width,
      maxHeight,
      maxWidth: '100vw',
    }
  })

  // 滚动条高度
  const scrollbarHeight = computed(() => {
    const baseHeight = isMobile.value ? '70vh' : maxHeight
    return `calc(${baseHeight} - 180px)`
  })

  // 表单网格列数
  const formGridCols = computed(() => {
    return isMobile.value ? 1 : 2
  })

  // 表单间距
  const formGap = computed(() => {
    return isMobile.value ? 12 : 16
  })

  return {
    modalStyle,
    scrollbarHeight,
    formGridCols,
    formGap,
    isMobile,
    isTablet,
  }
}

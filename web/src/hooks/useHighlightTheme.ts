import { watch, onUnmounted } from 'vue'
import { useTheme } from './useTheme'

// 导入样式内容（作为字符串）
import githubLight from 'highlight.js/styles/github.css?inline'
import githubDark from 'highlight.js/styles/github-dark.css?inline'

/**
 * 代码高亮主题 Hook
 * 根据当前主题动态切换 highlight.js 样式
 */
export function useHighlightTheme() {
  const { isDark } = useTheme()
  let styleElement: HTMLStyleElement | null = null

  const loadHighlightStyle = (dark: boolean) => {
    // 移除旧样式
    if (styleElement) {
      styleElement.remove()
    }

    // 创建新样式元素
    styleElement = document.createElement('style')
    styleElement.setAttribute('data-highlight-theme', dark ? 'dark' : 'light')
    styleElement.textContent = dark ? githubDark : githubLight
    document.head.appendChild(styleElement)
  }

  // 监听主题变化
  watch(
    isDark,
    (dark) => {
      loadHighlightStyle(dark)
    },
    { immediate: true }
  )

  // 组件卸载时清理
  onUnmounted(() => {
    if (styleElement) {
      styleElement.remove()
      styleElement = null
    }
  })

  return {
    isDark
  }
}

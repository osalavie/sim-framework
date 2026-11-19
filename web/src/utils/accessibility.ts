/**
 * 可访问性工具函数
 */

/**
 * 生成唯一 ID
 * 用于 ARIA 属性
 */
let idCounter = 0
export function generateId(prefix = 'a11y'): string {
  return `${prefix}-${++idCounter}`
}

/**
 * 宣布消息给屏幕阅读器
 * 使用 ARIA live region
 */
export function announce(message: string, priority: 'polite' | 'assertive' = 'polite') {
  const liveRegion = document.getElementById('aria-live-region')

  if (liveRegion) {
    liveRegion.setAttribute('aria-live', priority)
    liveRegion.textContent = message

    // 清空消息，以便下次可以宣布相同的消息
    setTimeout(() => {
      liveRegion.textContent = ''
    }, 1000)
  }
}

/**
 * 创建 ARIA live region
 * 应该在应用启动时调用一次
 */
export function createLiveRegion() {
  if (document.getElementById('aria-live-region')) {
    return
  }

  const liveRegion = document.createElement('div')
  liveRegion.id = 'aria-live-region'
  liveRegion.setAttribute('role', 'status')
  liveRegion.setAttribute('aria-live', 'polite')
  liveRegion.setAttribute('aria-atomic', 'true')
  liveRegion.style.position = 'absolute'
  liveRegion.style.left = '-10000px'
  liveRegion.style.width = '1px'
  liveRegion.style.height = '1px'
  liveRegion.style.overflow = 'hidden'

  document.body.appendChild(liveRegion)
}

/**
 * 检查元素是否可见
 */
export function isElementVisible(element: HTMLElement): boolean {
  return !!(element.offsetWidth || element.offsetHeight || element.getClientRects().length)
}

/**
 * 获取所有可聚焦元素
 */
export function getFocusableElements(container: HTMLElement = document.body): HTMLElement[] {
  const selector = [
    'a[href]',
    'area[href]',
    'input:not([disabled]):not([type="hidden"])',
    'select:not([disabled])',
    'textarea:not([disabled])',
    'button:not([disabled])',
    'iframe',
    'object',
    'embed',
    '[contenteditable]',
    '[tabindex]:not([tabindex^="-"])',
  ].join(',')

  const elements = container.querySelectorAll<HTMLElement>(selector)
  return Array.from(elements).filter(isElementVisible)
}

/**
 * 聚焦第一个可聚焦元素
 */
export function focusFirstElement(container: HTMLElement) {
  const elements = getFocusableElements(container)
  elements[0]?.focus()
}

/**
 * 聚焦最后一个可聚焦元素
 */
export function focusLastElement(container: HTMLElement) {
  const elements = getFocusableElements(container)
  elements[elements.length - 1]?.focus()
}

/**
 * 跳过导航链接
 * 用于键盘用户快速跳转到主内容
 */
export function createSkipLink(targetId: string, text = '跳转到主内容') {
  const skipLink = document.createElement('a')
  skipLink.href = `#${targetId}`
  skipLink.textContent = text
  skipLink.className = 'skip-link'
  skipLink.style.position = 'absolute'
  skipLink.style.top = '-40px'
  skipLink.style.left = '0'
  skipLink.style.background = 'var(--n-color-target)'
  skipLink.style.color = 'white'
  skipLink.style.padding = '8px'
  skipLink.style.textDecoration = 'none'
  skipLink.style.zIndex = '10000'
  skipLink.style.transition = 'top 0.3s'

  skipLink.addEventListener('focus', () => {
    skipLink.style.top = '0'
  })

  skipLink.addEventListener('blur', () => {
    skipLink.style.top = '-40px'
  })

  skipLink.addEventListener('click', (e) => {
    e.preventDefault()
    const target = document.getElementById(targetId)
    if (target) {
      target.focus()
      target.scrollIntoView({ behavior: 'smooth' })
    }
  })

  document.body.insertBefore(skipLink, document.body.firstChild)
}

/**
 * 设置页面标题
 * 用于屏幕阅读器宣布页面变化
 */
export function setPageTitle(title: string, suffix = ' - 系统管理') {
  document.title = title + suffix
  announce(`页面已切换到 ${title}`)
}

/**
 * 检查是否启用了减少动画
 */
export function prefersReducedMotion(): boolean {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

/**
 * 检查是否为高对比度模式
 */
export function prefersHighContrast(): boolean {
  return window.matchMedia('(prefers-contrast: high)').matches
}

/**
 * 添加 ARIA 标签
 */
export function setAriaLabel(element: HTMLElement, label: string) {
  element.setAttribute('aria-label', label)
}

/**
 * 添加 ARIA 描述
 */
export function setAriaDescription(element: HTMLElement, description: string) {
  const id = generateId('desc')
  const descElement = document.createElement('span')
  descElement.id = id
  descElement.textContent = description
  descElement.style.display = 'none'
  element.appendChild(descElement)
  element.setAttribute('aria-describedby', id)
}

/**
 * 设置元素的忙碌状态
 */
export function setAriaBusy(element: HTMLElement, busy: boolean) {
  element.setAttribute('aria-busy', String(busy))
}

/**
 * 设置元素的展开/折叠状态
 */
export function setAriaExpanded(element: HTMLElement, expanded: boolean) {
  element.setAttribute('aria-expanded', String(expanded))
}

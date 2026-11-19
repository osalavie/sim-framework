/**
 * 水墨晕染涟漪
 *
 * 通过事件委托为菜单项与自定义按钮注入水墨涟漪特效：
 * - 点击位置生成一个由中心扩散并淡出的墨晕；
 * - 只作用于导航与自定义交互元素，不干扰 Naive UI 自带按钮的波浪反馈；
 * - 遵循系统「减少动态效果」设置。
 */

const RIPPLE_TARGETS = [
  '.n-menu-item-content',
  '.ink-ripple-host',
  '.n-layout-toggle-button',
  '.n-tab-pane__header',
]

function prefersReducedMotion(): boolean {
  try {
    return (
      typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches
    )
  } catch {
    return false
  }
}

/** 在容器内（默认 document）监听 pointerdown 并生成墨晕 */
export function initInkRipple(container: HTMLElement | Document = document): () => void {
  if (prefersReducedMotion() || typeof document === 'undefined') {
    return () => undefined
  }

  const selector = RIPPLE_TARGETS.join(',')

  const onPointerDown = (event: Event) => {
    const pointerEvent = event as PointerEvent
    const target = (event.target as HTMLElement | null)?.closest<HTMLElement>(selector)
    if (!target) return

    const rect = target.getBoundingClientRect()
    if (rect.width === 0 || rect.height === 0) return

    const size = Math.max(rect.width, rect.height) * 2.2
    const ripple = document.createElement('span')
    ripple.className = 'ink-ripple'
    ripple.style.width = `${size}px`
    ripple.style.height = `${size}px`
    ripple.style.left = `${pointerEvent.clientX - rect.left - size / 2}px`
    ripple.style.top = `${pointerEvent.clientY - rect.top - size / 2}px`
    target.appendChild(ripple)

    ripple.addEventListener(
      'animationend',
      () => {
        ripple.remove()
      },
      { once: true },
    )
  }

  container.addEventListener('pointerdown', onPointerDown, { passive: true })
  return () => container.removeEventListener('pointerdown', onPointerDown)
}
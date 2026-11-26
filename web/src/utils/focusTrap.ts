/**
 * 焦点陷阱工具
 * 用于在模态框中限制焦点，提高可访问性
 */

const FOCUSABLE_ELEMENTS = [
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
]

export class FocusTrap {
  private element: HTMLElement
  private previousActiveElement: HTMLElement | null = null
  private focusableElements: HTMLElement[] = []
  private firstFocusableElement: HTMLElement | null = null
  private lastFocusableElement: HTMLElement | null = null

  constructor(element: HTMLElement) {
    this.element = element
    this.updateFocusableElements()
  }

  /**
   * 更新可聚焦元素列表
   */
  private updateFocusableElements() {
    const elements = this.element.querySelectorAll<HTMLElement>(
      FOCUSABLE_ELEMENTS.join(',')
    )
    this.focusableElements = Array.from(elements).filter(
      (el) => !el.hasAttribute('disabled') && el.offsetParent !== null
    )
    this.firstFocusableElement = this.focusableElements[0] || null
    this.lastFocusableElement =
      this.focusableElements[this.focusableElements.length - 1] || null
  }

  /**
   * 激活焦点陷阱
   */
  activate() {
    // 保存当前焦点元素
    this.previousActiveElement = document.activeElement as HTMLElement

    // 聚焦第一个可聚焦元素
    if (this.firstFocusableElement) {
      this.firstFocusableElement.focus()
    }

    // 添加键盘事件监听
    this.element.addEventListener('keydown', this.handleKeyDown)
  }

  /**
   * 停用焦点陷阱
   */
  deactivate() {
    // 移除键盘事件监听
    this.element.removeEventListener('keydown', this.handleKeyDown)

    // 恢复之前的焦点
    if (this.previousActiveElement) {
      this.previousActiveElement.focus()
    }
  }

  /**
   * 处理键盘事件
   */
  private handleKeyDown = (event: KeyboardEvent) => {
    if (event.key !== 'Tab') {
      return
    }

    // 更新可聚焦元素列表（可能有动态变化）
    this.updateFocusableElements()

    if (this.focusableElements.length === 0) {
      event.preventDefault()
      return
    }

    if (event.shiftKey) {
      // Shift + Tab: 向前导航
      if (document.activeElement === this.firstFocusableElement) {
        event.preventDefault()
        this.lastFocusableElement?.focus()
      }
    } else {
      // Tab: 向后导航
      if (document.activeElement === this.lastFocusableElement) {
        event.preventDefault()
        this.firstFocusableElement?.focus()
      }
    }
  }
}

/**
 * 创建焦点陷阱
 */
export function createFocusTrap(element: HTMLElement): FocusTrap {
  return new FocusTrap(element)
}

const focusTraps = new WeakMap<HTMLElement, FocusTrap>()

/**
 * 焦点陷阱 Vue 指令
 */
export const vFocusTrap = {
  mounted(el: HTMLElement) {
    const trap = createFocusTrap(el)
    trap.activate()
    focusTraps.set(el, trap)
  },
  unmounted(el: HTMLElement) {
    const trap = focusTraps.get(el)
    if (trap) {
      trap.deactivate()
      focusTraps.delete(el)
    }
  },
}

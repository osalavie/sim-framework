import { onBeforeUnmount, onMounted, ref, watch, type Ref } from 'vue'

/**
 * 用于"文本截断时才显示 n-tooltip"场景。
 *
 * 在目标元素上绑定 ref + @mouseenter="check"，并把 disabled 绑到 n-tooltip 的
 * disabled 属性，即可实现"短文本不弹气泡，仅在被 CSS 截断时才显示气泡"。
 *
 * 支持单行（text-overflow: ellipsis）和多行（-webkit-line-clamp）截断检测。
 */
export function useTruncatedTooltip() {
  const targetEl: Ref<HTMLElement | null> = ref(null)
  const disabled = ref(true)

  const check = () => {
    const el = targetEl.value
    if (!el) {
      disabled.value = true
      return
    }
    // 水平截断 or 多行截断都算
    const overflowX = el.scrollWidth - el.clientWidth > 1
    const overflowY = el.scrollHeight - el.clientHeight > 1
    disabled.value = !(overflowX || overflowY)
  }

  let resizeObserver: ResizeObserver | undefined

  onMounted(() => {
    if (targetEl.value) {
      resizeObserver = new ResizeObserver(() => check())
      resizeObserver.observe(targetEl.value)
      // 初始检查一次，避免 hover 之前 tooltip 已显示
      check()
    }
  })

  // targetEl 后续被赋值（v-if 切换、列表项渲染）时也要重新观测
  watch(targetEl, (el, oldEl) => {
    if (oldEl && resizeObserver) {
      resizeObserver.unobserve(oldEl)
    }
    if (el) {
      if (!resizeObserver) {
        resizeObserver = new ResizeObserver(() => check())
      }
      resizeObserver.observe(el)
      check()
    }
  })

  onBeforeUnmount(() => {
    resizeObserver?.disconnect()
    resizeObserver = undefined
  })

  return {
    targetEl,
    disabled,
    check,
  }
}

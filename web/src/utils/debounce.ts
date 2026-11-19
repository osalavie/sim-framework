export interface DebouncedFunction<T extends (...args: never[]) => unknown> {
  (...args: Parameters<T>): void
  cancel: () => void
}

export interface DebounceOptions {
  /** 是否在延迟开始前立即调用函数 */
  leading?: boolean
  /** 是否在延迟结束后调用函数 */
  trailing?: boolean
}

/**
 * 创建一个防抖函数
 * @param func 要防抖的函数
 * @param wait 延迟时间（毫秒）
 * @param options 配置选项
 * @returns 防抖后的函数，带有 cancel 方法
 */
export function debounce<T extends (...args: never[]) => unknown>(
  func: T,
  wait: number,
  options: DebounceOptions = {}
): DebouncedFunction<T> {
  const { leading = false, trailing = true } = options

  let timeoutId: ReturnType<typeof setTimeout> | null = null
  let lastArgs: Parameters<T> | null = null
  let lastCallTime: number | null = null

  function invokeFunc(args: Parameters<T>) {
    return func(...args)
  }

  function shouldInvoke(time: number): boolean {
    const timeSinceLastCall = time - (lastCallTime || 0)

    return (
      lastCallTime === null ||
      timeSinceLastCall >= wait ||
      timeSinceLastCall < 0
    )
  }

  function timerExpired() {
    if (trailing && lastArgs) {
      invokeFunc(lastArgs)
    }

    cancel()
  }

  function cancel() {
    if (timeoutId !== null) {
      clearTimeout(timeoutId)
      timeoutId = null
    }
    lastArgs = null
    lastCallTime = null
  }

  function debounced(...args: Parameters<T>) {
    const time = Date.now()
    const isInvoking = shouldInvoke(time)

    lastArgs = args
    lastCallTime = time

    if (isInvoking && leading && timeoutId === null) {
      // 立即执行
      invokeFunc(args)
    }

    // 清除之前的定时器
    if (timeoutId !== null) {
      clearTimeout(timeoutId)
    }

    // 设置新的定时器
    timeoutId = setTimeout(timerExpired, wait)
  }

  debounced.cancel = cancel

  return debounced
}

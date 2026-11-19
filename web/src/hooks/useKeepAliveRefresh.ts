import { onActivated, onMounted } from 'vue'

type RefreshAction = () => void | Promise<void>

/**
 * 首次挂载执行初始化，之后每次从 KeepAlive 缓存恢复时只刷新一次。
 *
 * Vue 会在 KeepAlive 组件首次显示时同时触发 mounted 和 activated，
 * 因此不能直接在两个钩子中调用同一个加载方法。
 */
export function useKeepAliveRefresh(
  initialize: RefreshAction,
  refresh: RefreshAction = initialize,
) {
  let initialRenderFinished = false
  let refreshQueued = false

  onMounted(() => {
    void initialize()
    queueMicrotask(() => {
      initialRenderFinished = true
    })
  })

  onActivated(() => {
    if (!initialRenderFinished || refreshQueued) return

    // 嵌套 KeepAlive 可能在同一轮渲染中向后代传播多次 activated。
    // 合并到一个微任务，确保一次菜单激活只刷新一轮。
    refreshQueued = true
    queueMicrotask(() => {
      refreshQueued = false
      void refresh()
    })
  })
}

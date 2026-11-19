import { readonly, ref } from 'vue'

/**
 * 页面可在路由 meta.title 之外覆盖后台顶栏标题（如详情页显示“新建”或单据标题）。
 * 覆盖按持有者 token 管理，避免缓存页签失活时清掉当前页面的标题。
 */
const headerTitle = ref<string | null>(null)
let owner: symbol | null = null

export function useHeaderTitleOverride() {
  return {
    headerTitleOverride: readonly(headerTitle),
    setHeaderTitleOverride(value: string | null, token: symbol) {
      if (value === null) {
        if (owner !== token) return
        owner = null
        headerTitle.value = null
        return
      }
      owner = token
      headerTitle.value = value
    },
  }
}

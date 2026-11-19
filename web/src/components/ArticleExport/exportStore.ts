import { reactive } from 'vue'
import type { ArticleReaderContent } from '@/components/ArticleReader/ArticleContent.vue'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'

export interface ArticleExportState {
  open: boolean
  article: ArticleReaderContent | null
  /** 当前登录用户就是作者时才允许请求签名。 */
  canSign: boolean
}

const state = reactive<ArticleExportState>({ open: false, article: null, canSign: false })

let mounting: Promise<void> | null = null

/** 导出宿主按需挂载到 body，首次导出时才拉取导出管线（含 zip 打包）。 */
function ensureHost(): void {
  mounting ??= import('./mountHost')
    .then((mod) => mod.ensureArticleExportHost())
    .catch(() => {
      mounting = null
      closeArticleExport()
      useDiscreteApi.message.error(t('Article:Export:Failed'))
    })
}

/** 三个入口都通过它打开同一份预览。 */
export function openArticleExport(
  article: ArticleReaderContent,
  options: { canSign?: boolean } = {},
): void {
  state.article = article
  state.canSign = options.canSign ?? false
  state.open = true
  ensureHost()
}

export function closeArticleExport(): void {
  state.open = false
  state.article = null
  state.canSign = false
}

/** 只读消费：仅通过 open/close 修改，避免各入口直接写状态。 */
export const articleExportState = state

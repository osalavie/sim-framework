import { createApp } from 'vue'
import type { App } from 'vue'

let host: App<Element> | null = null

/**
 * 导出宿主独立挂载到 body：
 * 阅读器组件链（Markdown 渲染 + 画布）体量与类型都很重，
 * 若经 App.vue 引入会撑爆 vue-tsc 的类型实例化深度并拖慢首屏。
 */
export async function ensureArticleExportHost(): Promise<void> {
  if (host) return
  const { default: ArticleExportHost } = await import('./ArticleExportHost.vue')
  const container = document.createElement('div')
  container.id = 'article-export-host'
  document.body.appendChild(container)
  host = createApp(ArticleExportHost)
  host.mount(container)
}

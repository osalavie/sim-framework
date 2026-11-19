import type { ExportFile } from './markdownBundle'
import type { ExportSignaturePackage } from './exportPipeline'

export interface ArticleExportAuthorProfile {
  displayName?: string | null
  blogName?: string | null
  authorNo?: string | null
}

export interface ArticleExportSignatureInput {
  articleId: string
  authorId: string
  files: ExportFile[]
}

/**
 * 导出组件只定义业务端口；作者资料与签名由业务系统注册，
 * 框架不感知具体 API。
 */
export interface ArticleExportAdapter {
  loadAuthorProfile(userId: string): Promise<ArticleExportAuthorProfile | null>
  createSignature?(input: ArticleExportSignatureInput): Promise<ExportSignaturePackage>
}

let adapter: ArticleExportAdapter = {
  loadAuthorProfile: async () => null,
}

export function registerArticleExportAdapter(next: ArticleExportAdapter): void {
  adapter = next
}

export function getArticleExportAdapter(): ArticleExportAdapter {
  return adapter
}

import type { ArticleReaderContent } from '@/components/ArticleReader/ArticleContent.vue'

export interface ExportableAuthor {
  displayName?: string | null
  blogName?: string | null
  authorNo?: string | null
}

/** 后台文章模型与博客前台详情共用；标签既可能是对象也可能是字符串。 */
export interface ExportableArticle {
  id?: string
  title: string
  content: string
  cover?: string | null
  creationTime: string
  lastModificationTime?: string | null
  tags?: Array<{ name: string } | string> | null
  creatorId?: string | null
  author?: ExportableAuthor | null
}

const tagNames = (values?: Array<{ name: string } | string> | null): string[] =>
  (values ?? []).map((value) => (typeof value === 'string' ? value : value.name)).filter(Boolean)

export function toReaderArticle(
  article: ExportableArticle,
  author: ExportableAuthor | null | undefined = article.author,
): ArticleReaderContent {
  return {
    id: article.id,
    title: article.title,
    content: article.content,
    cover: article.cover,
    creationTime: article.creationTime,
    lastModificationTime: article.lastModificationTime,
    tags: tagNames(article.tags),
    creatorId: article.creatorId,
    authorName: author?.displayName?.trim() || author?.blogName?.trim() || undefined,
    authorNo: author?.authorNo?.trim() || undefined,
  }
}

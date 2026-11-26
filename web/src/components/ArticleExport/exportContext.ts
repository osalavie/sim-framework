import type { InjectionKey } from 'vue'

/**
 * 导出渲染面标记：正文图片节点据此跳过 IntersectionObserver 立即加载。
 * 导出面可能整篇都在视口外（打印前页面未滚动），懒加载会导致导出文件缺图。
 */
export const ARTICLE_EXPORT_EAGER_KEY: InjectionKey<boolean> = Symbol('article-export-eager')

<template>
  <Teleport to="body">
    <div v-if="article" class="article-export-root">
      <div class="article-export-overlay" @click.self="close">
        <section
          class="article-export-dialog"
          role="dialog"
          aria-modal="true"
          :aria-label="t('Article:Export:Title')"
        >
          <header class="article-export-toolbar">
            <div class="article-export-toolbar__text">
              <strong>{{ t('Article:Export:Title') }}</strong>
            </div>
            <div class="article-export-toolbar__actions">
              <button
                type="button"
                class="article-export-button"
                :disabled="busy"
                @click="downloadMarkdown"
              >
                {{ markdownBusy ? t('Common:Loading') : t('Article:Export:Markdown') }}
              </button>
              <button
                type="button"
                class="article-export-button article-export-button--primary"
                :disabled="busy"
                @click="printPdf"
              >
                {{ printing ? t('Common:Loading') : t('Article:Export:Pdf') }}
              </button>
              <button type="button" class="article-export-button" @click="close">
                {{ t('Common:Close') }}
              </button>
            </div>
          </header>

          <div class="article-export-scroll">
            <div ref="paperRef" class="article-export-paper">
              <ArticleContent v-if="displayArticle" :article="displayArticle" :dock-spacer="false" />
            </div>
          </div>
        </section>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, provide, ref, watch } from 'vue'
import ArticleContent, {
  type ArticleReaderContent,
} from '@/components/ArticleReader/ArticleContent.vue'
import { getArticleExportAdapter } from './adapter'
import { buildCopyrightNotice } from '@/utils/articleMeta'
import type { ExportFile } from './markdownBundle'
import {
  ARTICLE_EXPORT_SNAPSHOTS_KEY,
  CANVAS_CAPTURE_KEY,
  createCanvasCaptureRegistry,
} from '@/components/MarkdownEditor/canvasCapture'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'
import { ARTICLE_EXPORT_EAGER_KEY } from './exportContext'
import { articleExportState, closeArticleExport } from './exportStore'
import {
  buildMarkdownBundle,
  captureCanvasSnapshots,
  downloadBlob,
  waitForImages,
} from './exportPipeline'
import './articleExport.css'

const article = computed(() => articleExportState.article)
const paperRef = ref<HTMLElement | null>(null)
const authorName = ref('')
const authorNo = ref('')
const displayArticle = computed<ArticleReaderContent | null>(() => {
  const current = article.value
  if (!current) return null
  return {
    ...current,
    authorName: authorName.value || current.authorName,
    authorNo: authorNo.value || current.authorNo,
  }
})
const printing = ref(false)
const markdownBusy = ref(false)
const busy = computed(() => printing.value || markdownBusy.value)

const registry = createCanvasCaptureRegistry()
/** 按文档顺序的画布截图；有值后 CanvasNodeView 直接渲染图片，打印与打包共用。 */
const snapshots = ref<Array<string | null>>([])
provide(CANVAS_CAPTURE_KEY, registry)
provide(ARTICLE_EXPORT_SNAPSHOTS_KEY, snapshots)
provide(ARTICLE_EXPORT_EAGER_KEY, true)

let prepared: Promise<Array<string | null>> | null = null
let authorReady: Promise<void> = Promise.resolve()
let authorForUserId = ''

/** 后台文章详情不带作者资料，导出前按 creatorId 补一次公开博客资料。 */
const loadAuthor = async (): Promise<void> => {
  const current = article.value
  if (!current) return
  authorName.value = current.authorName?.trim() ?? ''
  authorNo.value = current.authorNo?.trim() ?? ''

  const userId = current.creatorId
  if (!userId || (authorName.value && authorNo.value) || authorForUserId === userId) return
  authorForUserId = userId
  try {
    const profile = await getArticleExportAdapter().loadAuthorProfile(userId)
    if (article.value?.creatorId !== userId) return
    authorName.value = profile?.displayName?.trim() || profile?.blogName?.trim() || authorName.value
    authorNo.value = profile?.authorNo?.trim() || authorNo.value
  } catch {
    // 资料缺失时保留调用方传入的作者信息。
  }
}

const nextFrame = () =>
  new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())))

/** 等字体、图片与画布挂载稳定：打印前必须把整篇内容全部渲染出来。 */
const settleSurface = async (root: HTMLElement): Promise<void> => {
  await nextTick()
  await nextFrame()
  if (document.fonts?.ready) await document.fonts.ready
  for (let attempt = 0; attempt < 3; attempt += 1) {
    await waitForImages(root, 4000)
    await new Promise((resolve) => window.setTimeout(resolve, 120))
  }
}

const prepare = (): Promise<Array<string | null>> => {
  prepared ??= (async () => {
    const root = paperRef.value
    if (!root) return []
    await settleSurface(root)
    const result = await captureCanvasSnapshots(registry)
    snapshots.value = result
    await nextTick()
    return result
  })()
  return prepared
}

const printPdf = async (): Promise<void> => {
  const root = paperRef.value
  if (!root) return
  printing.value = true
  try {
    await authorReady
    await prepare()
    await settleSurface(root)
    window.print()
  } catch {
    useDiscreteApi.message.error(t('Article:Export:Failed'))
  } finally {
    printing.value = false
  }
}

const downloadMarkdown = async (): Promise<void> => {
  const current = article.value
  if (!current) return
  markdownBusy.value = true
  try {
    await authorReady
    const snaps = await prepare()
    const authorInfo = displayArticle.value
    const articleId = current.id
    const authorId = current.creatorId
    const createSignature = getArticleExportAdapter().createSignature
    const sign =
      articleExportState.canSign && articleId && authorId && createSignature
        ? (files: ExportFile[]) => createSignature({ articleId, authorId, files })
        : undefined
    const bundle = await buildMarkdownBundle({
      title: current.title,
      markdown: current.content,
      snapshots: snaps,
      sign,
      meta: {
        creationTime: current.creationTime,
        lastModificationTime: current.lastModificationTime,
        tags: current.tags ?? [],
        cover: current.cover ?? null,
        authorName: authorInfo?.authorName ?? '',
        authorNo: authorInfo?.authorNo ?? '',
        authorId,
        copyright: buildCopyrightNotice(
          current.creationTime,
          current.lastModificationTime,
          authorInfo?.authorName,
        ),
        labels: {
          author: t('Article:Export:Author'),
          published: t('Article:PublishedAt'),
          updated: t('Article:UpdatedAt'),
          tags: t('Article:Export:Tags'),
          cover: t('Article:Export:Cover'),
          copyright: t('Article:Export:Copyright'),
          dateFormat: t('Article:Export:DateFormat'),
        },
      },
    })
    downloadBlob(bundle.blob, bundle.fileName)
    if (bundle.signatureStatus === 'failed') {
      useDiscreteApi.message.warning(t('Article:Export:SignatureFailed'))
    }
    if (bundle.externalImages.length > 0) {
      useDiscreteApi.message.warning(
        t('Article:Export:ExternalImages', { count: bundle.externalImages.length }),
      )
    }
  } catch {
    useDiscreteApi.message.error(t('Article:Export:Failed'))
  } finally {
    markdownBusy.value = false
  }
}

const close = (): void => {
  closeArticleExport()
}

const handleKeydown = (event: KeyboardEvent): void => {
  if (event.key === 'Escape' && articleExportState.open) {
    event.stopPropagation()
    close()
  }
}

watch(
  [() => articleExportState.open, () => article.value],
  ([open]) => {
    prepared = null
    snapshots.value = []
    authorForUserId = ''
    authorName.value = ''
    authorNo.value = ''
    authorReady = open ? loadAuthor() : Promise.resolve()
    document.body.style.overflow = open ? 'hidden' : ''
  },
  { immediate: true },
)

onMounted(() => window.addEventListener('keydown', handleKeydown, true))
onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeydown, true)
  document.body.style.overflow = ''
})
</script>

<style scoped>
.article-export-root {
  position: fixed;
  inset: 0;
  z-index: 4000;
}

.article-export-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: rgb(15 23 42 / 55%);
}

.article-export-dialog {
  display: flex;
  flex-direction: column;
  width: min(900px, 100%);
  max-height: calc(100vh - 48px);
  overflow: hidden;
  background: var(--po-paper, #fff);
  border-radius: 14px;
  box-shadow: 0 24px 60px rgb(15 23 42 / 28%);
}

.article-export-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--po-line, #d9e2f1);
}

.article-export-toolbar__text {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

/* 工具栏用原生按钮 + 令牌：导出宿主是独立 app 实例，不依赖 Naive 主题上下文 */
.article-export-button {
  height: 32px;
  padding: 0 12px;
  color: var(--po-ink, #14284b);
  font: inherit;
  font-size: 13px;
  background: var(--po-paper, #fff);
  border: 1px solid var(--po-line, #d9e2f1);
  border-radius: 8px;
  cursor: pointer;
}

.article-export-button:hover:not(:disabled) {
  color: var(--po-accent, #2f6bff);
  border-color: var(--po-accent, #2f6bff);
}

.article-export-button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.article-export-button--primary {
  color: #fff;
  background: var(--po-accent, #2f6bff);
  border-color: var(--po-accent, #2f6bff);
}

.article-export-button--primary:hover:not(:disabled) {
  color: #fff;
  background: var(--po-accent-hover, #1b4fd6);
  border-color: var(--po-accent-hover, #1b4fd6);
}

.article-export-toolbar__actions {
  display: flex;
  flex-shrink: 0;
  gap: 8px;
}

.article-export-scroll {
  flex: 1;
  overflow: auto;
  padding: 24px;
  background: var(--po-bg-deep, #f9f2e3);
}

/* A4 纸面预览：210mm 宽 + 14mm 页边距，正文版心 182mm，与打印页盒一致 */
.article-export-paper {
  width: 210mm;
  min-height: 297mm;
  padding: 14mm;
  margin: 0 auto;
  box-sizing: border-box;
  background: #fff;
  box-shadow: 0 10px 30px rgb(15 23 42 / 12%);
}

/* 去掉阅读卡片的装饰与留白：纸张边距由 @page 提供，版心占满 182mm */
.article-export-paper :deep(.article-content) {
  padding: 0;
  border: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
}

.article-export-paper :deep(.article-masthead) {
  padding: 0 0 20px;
}

.article-export-paper :deep(.article-masthead.has-cover) {
  display: block;
  min-height: 0;
  padding: 0 0 20px;
}

.article-export-paper :deep(.article-masthead.has-cover .article-cover) {
  position: relative;
  height: 220px;
  margin-bottom: 20px;
  border-radius: 8px;
}

.article-export-paper :deep(.article-cover-fade) {
  display: none;
}

/* 标题字号锁定：clamp(…vw…) 在屏幕与打印页盒下取值不同，会破坏预览/打印一致性 */
.article-export-paper :deep(.article-title) {
  margin-bottom: 12px;
  font-size: 30px;
  line-height: 1.3;
}

.article-export-paper :deep(.article-body) {
  padding: 0;
}

@media (max-width: 768px) {
  .article-export-overlay {
    padding: 0;
  }

  .article-export-dialog {
    width: 100%;
    max-height: 100vh;
    border-radius: 0;
  }

  .article-export-scroll {
    padding: 12px;
  }
}
</style>

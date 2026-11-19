<template>
  <div class="article-wrapper">
    <article class="article-content" :class="{ 'is-compact': compact }">
      <div class="article-masthead" :class="{ 'has-cover': article.cover }">
        <!-- 文章封面（作为头部背景） -->
        <div class="article-cover" v-if="article.cover">
          <ManagedContentImage
            :src="article.cover"
            :alt="article.title"
            object-fit="cover"
            preview-disabled
          />
          <div class="article-cover-fade" aria-hidden="true"></div>
        </div>

        <!-- 文章头部 -->
        <header class="article-header">
          <h1 class="article-title">{{ article.title }}</h1>
          <div class="article-meta">
            <div class="meta-line" v-if="article.authorName">
              <div class="meta-item">
                <Icon name="carbon:User" :size="16" />
                <span>
                  {{ t('Article:AuthorLabel') }} · {{ article.authorName
                  }}<span v-if="article.authorNo" class="meta-author-no">{{ article.authorNo }}</span>
                </span>
              </div>
            </div>
            <div class="meta-line">
              <div class="meta-item">
                <Icon name="carbon:Calendar" :size="16" />
                <span>{{ t('Article:PublishedAt') }} · {{ formatDate(article.creationTime) }}</span>
              </div>
              <div class="meta-item">
                <Icon name="carbon:Calendar" :size="16" />
                <span>{{ t('Article:UpdatedAt') }} · {{ formatDate(updatedTime) }}</span>
              </div>
            </div>
          </div>
          <div class="article-tags" v-if="article.tags && article.tags.length > 0">
            <span class="tag" v-for="tag in article.tags" :key="tag">{{ tag }}</span>
          </div>
        </header>
      </div>

      <!-- 文章正文 -->
      <div class="article-body" ref="articleBodyRef">
        <MarkdownViewer :content="article.content" :document-title="article.title" />
      </div>
    </article>

    <!-- Dock 占位元素 -->
    <div v-if="dockSpacer !== false" class="dock-spacer"></div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import Icon from '@/components/Icon/Icon.vue'
import MarkdownViewer from '@/components/MarkdownViewer/index.vue'
import ManagedContentImage from '@/components/ManagedContentImage.vue'
import { effectiveUpdatedAt } from '@/utils/articleMeta'
import { formatDate as formatDateTime } from '@/utils/date'
import { t } from '@/i18n'

/** 阅读视图需要的最小文章字段，公开详情与后台文章均可直接映射。 */
export interface ArticleReaderContent {
  /** 导出签名与作者信息解析需要文章标识。 */
  id?: string
  title: string
  content: string
  cover?: string | null
  creationTime: string
  lastModificationTime?: string | null
  tags?: string[] | null
  creatorId?: string | null
  authorName?: string
  authorNo?: string | null
}

interface Props {
  article: ArticleReaderContent
  /** 公开详情页为固定评论 Dock 预留底部空间；后台预览不需要。 */
  dockSpacer?: boolean
  /** 后台弹窗内收窄左右内边距，让阅读区尽量占满容器宽度。 */
  compact?: boolean
}

const props = defineProps<Props>()

const articleBodyRef = ref<HTMLElement>()

// 没有更新日期时回退发布日期，阅读与导出始终展示同一个「更新」时间。
const updatedTime = computed(() => effectiveUpdatedAt(props.article))

// 格式化日期 - 使用友好的时间显示
const formatDate = (dateStr: string) => {
  return formatDateTime(dateStr, 'YYYY年MM月DD日 HH:mm')
}

defineExpose({
  articleBodyRef,
})
</script>

<style scoped lang="scss">
.article-wrapper {
  flex: 1;
  min-width: 0;

  .dock-spacer {
    height: 120px;
  }
}

.article-content {
  --article-card-pad-inline: clamp(34px, 5vw, 72px);

  &.is-compact {
    --article-card-pad-inline: clamp(16px, 2vw, 32px);
  }

  position: relative;
  overflow: hidden;
  background: var(--reader-surface);
  border: var(--po-border);
  border-radius: var(--po-r-lg);
  box-shadow: var(--reader-card-shadow);

  .article-masthead {
    position: relative;
    padding: clamp(34px, 5vw, 72px) var(--article-card-pad-inline) 0;

    &.has-cover {
      display: flex;
      align-items: flex-end;
      min-height: clamp(400px, 38vw, 560px);
      padding: 0 var(--article-card-pad-inline) clamp(28px, 4vw, 48px);

      .article-header {
        width: 100%;
      }
    }

    .article-cover {
      position: absolute;
      inset: 0;
      z-index: 0;
      overflow: hidden;
      background: var(--reader-paper);

      :deep(.n-image) {
        display: block;
        width: 100%;
        height: 100%;
      }

      :deep(img) {
        width: 100%;
        height: 100%;
        object-fit: cover;
        object-position: center;
        display: block;
      }
    }

    .article-cover-fade {
      position: absolute;
      inset: 0;
      pointer-events: none;
      background: linear-gradient(
        to bottom,
        transparent 0%,
        transparent 18%,
        color-mix(in srgb, var(--reader-surface) 10%, transparent) 32%,
        color-mix(in srgb, var(--reader-surface) 28%, transparent) 44%,
        color-mix(in srgb, var(--reader-surface) 55%, transparent) 56%,
        color-mix(in srgb, var(--reader-surface) 78%, transparent) 68%,
        color-mix(in srgb, var(--reader-surface) 92%, transparent) 80%,
        color-mix(in srgb, var(--reader-surface) 98%, transparent) 90%,
        var(--reader-surface) 100%
      );
    }
  }

  .article-header {
    position: relative;
    z-index: 1;

    .article-title {
      font-family: var(--po-font-display);
      font-size: clamp(28px, 3vw, 40px);
      font-weight: var(--po-weight-strong);
      color: var(--reader-ink);
      margin: 0 0 20px;
      line-height: 1.2;
      letter-spacing: -0.01em;
    }

    .article-meta {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin-bottom: 16px;

      .meta-line {
        display: flex;
        align-items: center;
        gap: 18px;
        flex-wrap: wrap;
      }

      .meta-item {
        display: flex;
        align-items: center;
        gap: 6px;
        color: var(--reader-muted);
        font-size: 12px;
        white-space: nowrap;
      }

      .meta-author-no {
        margin-left: 6px;
        font-family: var(--po-font-mono);
        font-size: 11px;
        opacity: 0.75;
      }
    }

    .article-tags {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;

      .tag {
        padding: 6px 14px;
        background: var(--reader-soft-accent);
        border-radius: 999px;
        font-size: 13px;
        color: var(--reader-accent-strong);
        transition: all 0.3s ease;

        &:hover {
          background: var(--reader-accent);
          color: var(--po-paper);
        }
      }
    }
  }

  .article-body {
    padding: clamp(28px, 4vw, 40px) var(--article-card-pad-inline) clamp(34px, 5vw, 72px);
    font-family: var(--po-font-body);
    font-size: 17px;
    line-height: 1.95;
    color: var(--reader-ink);
    word-wrap: break-word;

    :deep(.tiptap > :first-child) {
      margin-top: 0;
    }

    :deep(h1),
    :deep(h2),
    :deep(h3),
    :deep(h4),
    :deep(h5),
    :deep(h6) {
      margin: 32px 0 16px;
      font-family: var(--po-font-display);
      font-weight: var(--po-weight-strong);
      color: var(--reader-ink);
      // scroll-margin-top 设置为 0，让标题恰好在容器顶部
      // 容器的 padding (40px) 会自然提供间距
      scroll-margin-top: 0;
    }

    :deep(h1) {
      font-size: 32px;
    }

    :deep(h2) {
      font-size: 28px;
      padding-bottom: 8px;
      border-bottom: 1px solid var(--reader-line);
    }

    :deep(h3) {
      font-size: 24px;
    }

    :deep(h4) {
      font-size: 20px;
    }

    :deep(p) {
      margin: 16px 0;
    }

    :deep(ul),
    :deep(ol) {
      margin: 16px 0;
      padding-left: 28px;
    }

    :deep(li) {
      margin: 8px 0;
    }

    :deep(a) {
      color: var(--reader-accent-strong);
      text-decoration: none;
      transition: color 0.2s ease;

      &:hover {
        color: var(--reader-accent);
        text-decoration: underline;
      }
    }

    :deep(code) {
      background: var(--reader-paper);
      padding: 3px 6px;
      border-radius: 4px;
      font-family: var(--po-font-mono);
      font-size: 14px;
      color: var(--po-coral-deep);
    }

    :deep(.code-block-wrapper) {
      margin: 20px 0;
      border: 1px solid var(--reader-line);
      border-radius: 12px;
      overflow: hidden;
      background: var(--reader-surface);

      .code-block-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 8px 16px;
        background: var(--reader-paper);
        border-bottom: 1px solid var(--reader-line);

        .code-language {
          font-size: 12px;
          color: var(--reader-muted);
          text-transform: uppercase;
          font-weight: 600;
        }

        .code-copy-btn {
          display: flex;
          align-items: center;
          gap: 4px;
          padding: 4px 12px;
          background: transparent;
          border: 2px solid var(--reader-line);
          border-radius: 4px;
          color: var(--reader-muted);
          font-size: 12px;
          cursor: pointer;
          transition:
            background-color 0.2s ease,
            border-color 0.2s ease,
            color 0.2s ease;

          &:hover {
            background: var(--reader-soft-accent);
            border-color: var(--reader-accent);
            color: var(--reader-ink);
          }

          svg {
            width: 14px;
            height: 14px;
          }
        }
      }

      pre {
        background: var(--reader-surface);
        padding: 20px;
        margin: 0;
        overflow-x: auto;

        code {
          background: none;
          padding: 0;
          color: var(--reader-ink);
          font-size: 14px;
          font-family: var(--po-font-mono);
          line-height: 1.6;
        }
      }
    }

    :deep(pre) {
      background: var(--reader-paper);
      padding: 20px;
      border-radius: 8px;
      overflow-x: auto;
      margin: 20px 0;

      code {
        background: none;
        padding: 0;
        color: var(--reader-ink);
        font-size: 14px;
      }
    }

    :deep(.mac-code-block pre) {
      margin: 0;
      padding: 0;
      border-radius: 0;
      background: transparent;
    }

    :deep(.mac-code-block pre code.hljs) {
      padding: 18px 20px;
      background: transparent;
      color: var(--code-window-text);
      line-height: 1.65;
    }

    :deep(.mac-code-block) {
      --code-window-header: var(--reader-paper);
      --code-window-body: var(--reader-surface);
      --code-window-border: var(--reader-line);
      --code-window-text: var(--reader-ink);
      --code-window-muted: var(--reader-muted);
      --code-window-hover: var(--reader-soft-accent);
      --code-window-focus: var(--reader-accent);
    }

    :deep(.canvas-block) {
      --canvas-surface: var(--reader-surface);
      --canvas-stage: var(--reader-paper);
      --canvas-border: var(--reader-line);
      --canvas-text: var(--reader-ink);
      --canvas-muted: var(--reader-muted);
      --canvas-accent: var(--reader-accent);
    }

    :deep(blockquote) {
      margin: 20px 0;
      padding: 12px 20px;
      border-left: 4px solid var(--reader-accent);
      background: var(--reader-soft-accent);
      color: var(--reader-muted);

      p {
        margin: 8px 0;
      }
    }

    :deep(img) {
      max-width: 100%;
      height: auto;
      border-radius: 8px;
      margin: 20px 0;
    }

    :deep(table) {
      width: 100%;
      border-collapse: collapse;
      margin: 20px 0;

      th,
      td {
        padding: 12px;
        border: 1px solid var(--reader-line);
        text-align: left;
      }

      th {
        background: var(--reader-paper);
        font-weight: 600;
      }

      tr:hover {
        background: color-mix(in srgb, var(--reader-accent) 5%, var(--reader-surface));
      }
    }

    :deep(hr) {
      margin: 32px 0;
      border: none;
      border-top: 1px solid var(--reader-line);
    }
  }
}

// 窄屏适配
@media (max-width: 768px) {
  .article-content {
    --article-card-pad-inline: 20px;

    border-radius: var(--po-r-lg);

    .article-masthead {
      padding-top: 28px;

      &.has-cover {
        min-height: 380px;
        padding-bottom: 24px;
      }
    }

    .article-header {
      .article-title {
        font-size: 28px;
        margin-bottom: 16px;
      }

      .article-meta {
        flex-wrap: wrap;
        gap: 12px;

        .meta-item {
          font-size: 13px;
          display: flex;
          justify-content: center;
        }
      }

      .article-tags {
        .tag {
          padding: 4px 10px;
          font-size: 12px;
        }
      }
    }

    .article-body {
      padding: 24px var(--article-card-pad-inline) 28px;
      font-size: 15px;
      line-height: 1.7;

      :deep(h1) {
        font-size: 24px;
        margin: 24px 0 12px;
      }

      :deep(h2) {
        font-size: 22px;
        margin: 24px 0 12px;
      }

      :deep(h3) {
        font-size: 20px;
        margin: 20px 0 12px;
      }

      :deep(h4) {
        font-size: 18px;
        margin: 20px 0 12px;
      }

      :deep(p) {
        margin: 12px 0;
      }

      :deep(ul),
      :deep(ol) {
        padding-left: 20px;
      }

      :deep(.code-block-wrapper) {
        margin: 16px -16px;
        border-radius: 0;

        .code-block-header {
          padding: 6px 12px;

          .code-language {
            font-size: 11px;
          }

          .code-copy-btn {
            padding: 3px 8px;
            font-size: 11px;
          }
        }

        pre {
          padding: 16px 12px;

          code {
            font-size: 13px;
          }
        }
      }

      :deep(pre) {
        margin: 16px -16px;
        padding: 16px 12px;
        border-radius: 0;

        code {
          font-size: 13px;
        }
      }

      :deep(.mac-code-block) {
        margin: 16px -12px;
        border-radius: 0;
      }

      :deep(.mac-code-block pre) {
        margin: 0;
        padding: 0;
      }

      :deep(.mac-code-block pre code.hljs) {
        padding: 16px 12px;
        font-size: 13px;
      }

      :deep(blockquote) {
        margin: 16px 0;
        padding: 10px 16px;
        font-size: 14px;
      }

      :deep(table) {
        font-size: 14px;
        display: block;
        overflow-x: auto;

        th,
        td {
          padding: 8px;
          font-size: 13px;
        }
      }
    }
  }

  .article-wrapper {
    .dock-spacer {
      height: 40px;
    }
  }
}
</style>

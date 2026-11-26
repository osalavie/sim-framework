<template>
  <NodeViewWrapper class="managed-image" data-drag-handle>
    <div ref="anchorEl" class="managed-image__anchor">
      <img
        v-if="displaySrc"
        :src="displaySrc"
        :alt="node.attrs.alt || ''"
        :title="node.attrs.title || undefined"
        draggable="true"
      />
      <img
        v-else-if="!failed"
        class="managed-image__placeholder"
        :src="IMAGE_PLACEHOLDER_SRC"
        alt=""
        aria-hidden="true"
      />
      <span v-else class="managed-image__status">
        {{ t('Editor:ImageLoadFailed') }}
      </span>
    </div>
  </NodeViewWrapper>
</template>

<script setup lang="ts">
import { NodeViewWrapper, nodeViewProps } from '@tiptap/vue-3'
import { onBeforeUnmount, onMounted, ref, watch, inject } from 'vue'
import { contentFileApi } from '@/api/contentFile'
import { IMAGE_PLACEHOLDER_SRC } from '@/utils/imagePlaceholder'
import { ARTICLE_EXPORT_EAGER_KEY } from '@/components/ArticleExport/exportContext'
import { t } from '@/i18n'

const props = defineProps(nodeViewProps)
const eager = inject(ARTICLE_EXPORT_EAGER_KEY, false)
const anchorEl = ref<HTMLElement | null>(null)
const displaySrc = ref('')
const failed = ref(false)
let objectUrl: string | undefined
let loadVersion = 0
let observer: IntersectionObserver | undefined

const releaseObjectUrl = () => {
  if (objectUrl) URL.revokeObjectURL(objectUrl)
  objectUrl = undefined
}

const load = async (src: string) => {
  const version = ++loadVersion
  releaseObjectUrl()
  failed.value = false
  if (!src.startsWith('/api/system/content-file/')) {
    displaySrc.value = src
    return
  }

  displaySrc.value = ''
  try {
    const blob = await contentFileApi.getBlob(src)
    if (version !== loadVersion) return
    objectUrl = URL.createObjectURL(blob)
    displaySrc.value = objectUrl
  } catch {
    if (version === loadVersion) failed.value = true
  }
}

const beginLoad = () => {
  observer?.disconnect()
  observer = undefined
  void load(String(props.node.attrs.src || ''))
}

onMounted(() => {
  // 导出面要求整篇可打印，跳过懒加载直接取图
  if (
    eager ||
    typeof IntersectionObserver === 'undefined' ||
    !anchorEl.value
  ) {
    beginLoad()
    return
  }

  // 与列表封面一致：进入视口前 200px 预热，视口外正文图片不产生请求
  observer = new IntersectionObserver(
    (entries) => {
      if (entries.some((entry) => entry.isIntersecting)) beginLoad()
    },
    { rootMargin: '200px' },
  )
  observer.observe(anchorEl.value)
})

watch(
  () => String(props.node.attrs.src || ''),
  (src) => {
    if (!observer) void load(src)
  },
)

onBeforeUnmount(() => {
  observer?.disconnect()
  observer = undefined
  loadVersion += 1
  releaseObjectUrl()
})
</script>

<style scoped>
.managed-image {
  display: block;
  margin-block: 1rem;
  text-align: center;
}

.managed-image__anchor {
  display: block;
}

.managed-image img {
  max-width: 100%;
  height: auto;
  border-radius: 8px;
}

.managed-image__status {
  display: inline-flex;
  min-height: 96px;
  align-items: center;
  justify-content: center;
  padding: 16px;
  color: var(--theme-text-secondary);
  border: 1px dashed var(--theme-border);
  border-radius: 8px;
}
</style>

<template>
  <n-image
    v-if="displaySrc"
    ref="rootEl"
    :src="displaySrc"
    :alt="alt"
    :object-fit="objectFit"
    :preview-disabled="previewDisabled"
  />
  <img
    v-else-if="!failed"
    ref="rootEl"
    class="managed-content-image__placeholder"
    :src="IMAGE_PLACEHOLDER_SRC"
    alt=""
    aria-hidden="true"
  />
  <span v-else ref="rootEl" class="managed-content-image__status">
    {{ t('Editor:ImageLoadFailed') }}
  </span>
</template>

<script setup lang="ts">
import { inject, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { contentFileApi } from '@/api/contentFile'
import { IMAGE_PLACEHOLDER_SRC } from '@/utils/imagePlaceholder'
import { ARTICLE_EXPORT_EAGER_KEY } from '@/components/ArticleExport/exportContext'
import { t } from '@/i18n'

interface Props {
  src: string
  alt?: string
  objectFit?: 'fill' | 'contain' | 'cover' | 'none' | 'scale-down'
  previewDisabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  alt: '',
  objectFit: 'fill',
  previewDisabled: true,
})
const eager = inject(ARTICLE_EXPORT_EAGER_KEY, false)
const rootEl = ref<HTMLElement | null>(null)
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
  void load(props.src)
}

onMounted(() => {
  if (eager || typeof IntersectionObserver === 'undefined' || !rootEl.value) {
    beginLoad()
    return
  }

  // 进入视口前 200px 预热：懒加载同时避免首屏外图片产生网络请求
  observer = new IntersectionObserver(
    (entries) => {
      if (entries.some((entry) => entry.isIntersecting)) beginLoad()
    },
    { rootMargin: '200px' },
  )
  observer.observe(rootEl.value)
})

watch(
  () => props.src,
  (src) => {
    // 尚未进入视口时仅更新待加载地址，进入视口后由 beginLoad 读取
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
.managed-content-image__placeholder {
  display: block;
}

.managed-content-image__status {
  display: grid;
  min-height: 96px;
  place-items: center;
  color: var(--theme-text-secondary);
  background: var(--theme-bg-secondary);
}
</style>

<template>
  <img v-if="src" ref="elementEl" :src="currentSrc" :alt="alt" decoding="async" />
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { IMAGE_PLACEHOLDER_SRC } from '@/utils/imagePlaceholder'

interface Props {
  src: string
  alt?: string
  /** 进入视口前的预加载距离；传 '0px' 时立即加载 */
  rootMargin?: string
}

const props = withDefaults(defineProps<Props>(), {
  alt: '',
  rootMargin: '200px',
})

const elementEl = ref<HTMLImageElement | null>(null)
// 占位图先占据元素，真实图片预加载完成后替换 src；两者都走元素的 object-fit
const currentSrc = ref(IMAGE_PLACEHOLDER_SRC)
let observer: IntersectionObserver | undefined
let visible = false
let loadToken = 0

const reveal = (src: string) => {
  const token = ++loadToken
  const probe = new Image()
  probe.decoding = 'async'
  probe.onload = () => {
    if (token === loadToken) currentSrc.value = src
  }
  probe.onerror = () => {
    // 加载失败保留占位图，避免露出浏览器默认的破图图标
  }
  probe.src = src
}

const beginLoad = () => {
  observer?.disconnect()
  observer = undefined
  visible = true
  reveal(props.src)
}

const startObserving = () => {
  observer?.disconnect()
  observer = undefined
  if (!props.src) return

  if (typeof IntersectionObserver === 'undefined' || props.rootMargin === '0px') {
    beginLoad()
    return
  }
  if (!elementEl.value) return

  observer = new IntersectionObserver(
    (entries) => {
      if (entries.some((entry) => entry.isIntersecting)) beginLoad()
    },
    { rootMargin: props.rootMargin },
  )
  observer.observe(elementEl.value)
}

onMounted(startObserving)

watch(
  () => props.src,
  (src) => {
    loadToken += 1
    currentSrc.value = src ? IMAGE_PLACEHOLDER_SRC : ''
    if (!src) {
      observer?.disconnect()
      observer = undefined
      visible = false
      return
    }
    if (visible) reveal(src)
    else startObserving()
  },
)

onBeforeUnmount(() => {
  observer?.disconnect()
  observer = undefined
  loadToken += 1
})
</script>

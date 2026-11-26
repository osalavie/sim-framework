<template>
  <n-modal :show="show" :mask-closable="!busy" @update:show="handleShow">
    <n-card
      class="avatar-editor"
      :title="title || t('ImageEditor:Title')"
      closable
      role="dialog"
      aria-modal="true"
      @close="close"
    >
      <div class="avatar-editor__body">
        <div
          ref="stageRef"
          class="avatar-editor__stage"
          :class="{ 'is-empty': !hasImage, 'is-dragover': dragover }"
          :role="hasImage ? 'group' : 'button'"
          :tabindex="hasImage ? -1 : 0"
          :aria-label="hasImage ? t('ImageEditor:Hint') : t('ImageEditor:Choose')"
          @click="onStageClick"
          @keydown.enter.prevent="onStageKeydown"
          @keydown.space.prevent="onStageKeydown"
          @mousedown="onPanStart"
          @touchstart="onTouchStart"
          @wheel.prevent="onWheel"
          @dragenter.prevent="dragover = true"
          @dragover.prevent="dragover = true"
          @dragleave="onDragLeave"
          @drop.prevent="onDrop"
        >
          <img
            v-if="sourceUrl"
            :src="sourceUrl"
            :style="imageStyle"
            :alt="t('ImageEditor:PreviewAlt')"
            draggable="false"
            @load="onImageLoad"
          />

          <div v-if="hasImage" class="avatar-editor__crop" aria-hidden="true">
            <div class="avatar-editor__grid">
              <span class="grid-line grid-h-1" />
              <span class="grid-line grid-h-2" />
              <span class="grid-line grid-v-1" />
              <span class="grid-line grid-v-2" />
            </div>
          </div>

          <div v-else class="avatar-editor__empty">
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
              <circle cx="12" cy="13" r="4" />
            </svg>
            <span>{{ t('ImageEditor:Empty') }}</span>
          </div>
        </div>

        <p v-if="hasImage" class="avatar-editor__hint">{{ t('ImageEditor:Hint') }}</p>
      </div>

      <template #footer>
        <div class="avatar-editor__footer">
          <n-button v-if="hasImage" text type="primary" :disabled="busy || processing" @click="openPicker">
            {{ t('ImageEditor:Change') }}
          </n-button>
          <div class="avatar-editor__footer-actions">
            <n-button secondary :disabled="busy || processing" @click="close">
              {{ t('Common:Cancel') }}
            </n-button>
            <n-button
              type="primary"
              :loading="processing || busy"
              :disabled="!hasImage"
              @click="apply"
            >
              {{ processing ? t('ImageEditor:Processing') : t('ImageEditor:Apply') }}
            </n-button>
          </div>
        </div>
      </template>

      <p v-if="errorMessage" class="avatar-editor__error" role="alert">{{ errorMessage }}</p>
      <input
        ref="inputRef"
        class="po-sr-only"
        type="file"
        accept="image/jpeg,image/png,image/webp"
        tabindex="-1"
        @change="onFileInput"
      />
    </n-card>
  </n-modal>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { t } from '@/i18n'
import { editImage, normalizeCrop, type CropPercent } from './imageEditing'

const OUTPUT_MAX_SIZE = 512
const OUTPUT_QUALITY = 84
const OUTPUT_FORMAT = 'image/webp' as const
const ACCEPTED_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp'])

const props = withDefaults(
  defineProps<{
    show: boolean
    title?: string
    defaultImageUrl?: string
    busy?: boolean
    minCropPixels?: number
  }>(),
  {
    title: '',
    defaultImageUrl: undefined,
    busy: false,
    minCropPixels: 100,
  },
)

const emit = defineEmits<{
  'update:show': [value: boolean]
  processed: [value: File]
}>()

const inputRef = ref<HTMLInputElement | null>(null)
const stageRef = ref<HTMLElement | null>(null)

const sourceFile = ref<File>()
const sourceUrl = ref('')
const processing = ref(false)
const errorMessage = ref('')
const dragover = ref(false)

const imgW = ref(0)
const imgH = ref(0)
const stageW = ref(0)
const stageH = ref(0)

const scale = ref(1)
const tx = ref(0)
const ty = ref(0)

const hasImage = computed(() => imgW.value > 0 && imgH.value > 0)

const coverScale = computed(() => {
  if (!imgW.value || !imgH.value || !stageW.value || !stageH.value) return 1
  return Math.max(stageW.value / imgW.value, stageH.value / imgH.value)
})

const maxScaleByPixels = computed(() => {
  const side = Math.min(stageW.value, stageH.value)
  if (!side) return 20
  return Math.max(coverScale.value, side / props.minCropPixels)
})

const imageStyle = computed(() => {
  const pxWidth = imgW.value ? `${imgW.value}px` : undefined
  return {
    position: 'absolute' as const,
    top: '0',
    left: '0',
    width: pxWidth,
    height: 'auto',
    maxWidth: pxWidth ? 'none' : undefined,
    maxHeight: pxWidth ? 'none' : undefined,
    transform: `translate3d(${tx.value}px, ${ty.value}px, 0) scale(${scale.value})`,
    transformOrigin: '0 0',
  } as const
})

function clampScale(value: number): number {
  return Math.max(coverScale.value, Math.min(maxScaleByPixels.value, value))
}

function clampOffset() {
  if (!imgW.value || !imgH.value || !stageW.value || !stageH.value) return
  const dw = imgW.value * scale.value
  const dh = imgH.value * scale.value
  tx.value = Math.max(stageW.value - dw, Math.min(0, tx.value))
  ty.value = Math.max(stageH.value - dh, Math.min(0, ty.value))
}

function resetTransform() {
  if (!imgW.value || !imgH.value || !stageW.value || !stageH.value) return
  const nextScale = coverScale.value
  scale.value = nextScale
  tx.value = (stageW.value - imgW.value * nextScale) / 2
  ty.value = (stageH.value - imgH.value * nextScale) / 2
}

watch(
  [() => imgW.value, () => imgH.value, () => stageW.value, () => stageH.value],
  ([w, h, sw, sh]) => {
    if (w && h && sw && sh) nextTick(resetTransform)
  },
)

let resizeObserver: ResizeObserver | null = null
watch(stageRef, (el) => {
  resizeObserver?.disconnect()
  resizeObserver = null
  if (el) {
    resizeObserver = new ResizeObserver(() => {
      stageW.value = el.clientWidth
      stageH.value = el.clientHeight
    })
    resizeObserver.observe(el)
    stageW.value = el.clientWidth
    stageH.value = el.clientHeight
  }
})

function setSource(file?: File) {
  errorMessage.value = ''
  if (sourceUrl.value) URL.revokeObjectURL(sourceUrl.value)
  sourceFile.value = file
  sourceUrl.value = file ? URL.createObjectURL(file) : ''
  if (!file) {
    imgW.value = 0
    imgH.value = 0
  }
}

async function loadDefaultImage() {
  const url = props.defaultImageUrl
  if (!url || sourceFile.value) return
  try {
    const response = await fetch(url)
    if (!response.ok) return
    const blob = await response.blob()
    if (!props.show || sourceFile.value) return
    setSource(new File([blob], 'avatar', { type: blob.type || 'image/jpeg' }))
  } catch {
    // Default image is optional; keep the empty picker on CORS / network errors.
  }
}

function openPicker() {
  if (props.busy || processing.value) return
  inputRef.value?.click()
}

function onFileInput(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) selectFile(file)
}

function selectFile(file: File) {
  if (!ACCEPTED_TYPES.has(file.type)) {
    errorMessage.value = t('ImageEditor:InvalidType')
    return
  }
  setSource(file)
}

function onDrop(event: DragEvent) {
  dragover.value = false
  if (props.busy || processing.value) return
  const file = event.dataTransfer?.files?.[0]
  if (file) selectFile(file)
}

function onDragLeave(event: DragEvent) {
  const next = event.relatedTarget
  if (!(next instanceof Node) || !stageRef.value?.contains(next)) dragover.value = false
}

function onImageLoad(event: Event) {
  const image = event.target as HTMLImageElement
  imgW.value = image.naturalWidth
  imgH.value = image.naturalHeight
}

// ── pan ──
interface PanState {
  sx: number
  sy: number
  stx: number
  sty: number
  moved: boolean
}

let pan: PanState | null = null

function onPanStart(event: MouseEvent) {
  if (!hasImage.value || props.busy) return
  pan = {
    sx: event.clientX,
    sy: event.clientY,
    stx: tx.value,
    sty: ty.value,
    moved: false,
  }
  bindGlobal()
}

function onPanMove(event: MouseEvent) {
  if (!pan) return
  const dx = event.clientX - pan.sx
  const dy = event.clientY - pan.sy
  if (!pan.moved && Math.hypot(dx, dy) < 4) return
  pan.moved = true
  tx.value = pan.stx + dx
  ty.value = pan.sty + dy
}

function onPanEnd() {
  if (pan) {
    if (pan.moved) clampOffset()
    pan = null
  }
  unbindGlobal()
}

// ── touch ──
let touchGesture: 'none' | 'pan' | 'pinch' = 'none'
let pinchDist0 = 0
let pinchScale0 = 1
const pinchMid0 = { x: 0, y: 0 }

function touchDist(a: Touch, b: Touch): number {
  return Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY)
}

function onTouchStart(event: TouchEvent) {
  if (!hasImage.value || props.busy) return
  if (event.touches.length === 1) {
    const touch = event.touches[0]!
    pan = {
      sx: touch.clientX,
      sy: touch.clientY,
      stx: tx.value,
      sty: ty.value,
      moved: false,
    }
    touchGesture = 'pan'
    bindGlobal()
  } else if (event.touches.length === 2) {
    pan = null
    const first = event.touches[0]!
    const second = event.touches[1]!
    pinchDist0 = touchDist(first, second)
    pinchScale0 = scale.value
    pinchMid0.x = (first.clientX + second.clientX) / 2
    pinchMid0.y = (first.clientY + second.clientY) / 2
    touchGesture = 'pinch'
  }
  event.preventDefault()
}

function onTouchMove(event: TouchEvent) {
  if (touchGesture === 'pan' && pan && event.touches.length === 1) {
    const touch = event.touches[0]!
    const dx = touch.clientX - pan.sx
    const dy = touch.clientY - pan.sy
    if (!pan.moved && Math.hypot(dx, dy) < 4) return
    pan.moved = true
    tx.value = pan.stx + dx
    ty.value = pan.sty + dy
  } else if (touchGesture === 'pinch' && event.touches.length === 2) {
    const first = event.touches[0]!
    const second = event.touches[1]!
    const nextScale = clampScale(pinchScale0 * (touchDist(first, second) / pinchDist0))
    const rect = stageRef.value?.getBoundingClientRect()
    if (rect) {
      const cx = (first.clientX + second.clientX) / 2 - rect.left
      const cy = (first.clientY + second.clientY) / 2 - rect.top
      const ratio = nextScale / scale.value
      tx.value = cx - (cx - tx.value) * ratio
      ty.value = cy - (cy - ty.value) * ratio
    }
    scale.value = nextScale
  }
  if (touchGesture !== 'none') event.preventDefault()
}

function onTouchEnd() {
  if (touchGesture === 'pan') clampOffset()
  pan = null
  touchGesture = 'none'
  unbindGlobal()
}

// ── wheel zoom ──
function onWheel(event: WheelEvent) {
  if (!hasImage.value || !stageRef.value || props.busy) return
  const rect = stageRef.value.getBoundingClientRect()
  const mx = event.clientX - rect.left
  const my = event.clientY - rect.top
  const delta = -event.deltaY * (event.deltaMode === 1 ? 0.05 : 0.001)
  const nextScale = clampScale(scale.value * Math.exp(delta))
  const ratio = nextScale / scale.value
  tx.value = mx - (mx - tx.value) * ratio
  ty.value = my - (my - ty.value) * ratio
  scale.value = nextScale
  clampOffset()
}

function bindGlobal() {
  window.addEventListener('mousemove', onPanMove)
  window.addEventListener('mouseup', onPanEnd)
  window.addEventListener('touchmove', onTouchMove, { passive: false })
  window.addEventListener('touchend', onTouchEnd)
}

function unbindGlobal() {
  window.removeEventListener('mousemove', onPanMove)
  window.removeEventListener('mouseup', onPanEnd)
  window.removeEventListener('touchmove', onTouchMove)
  window.removeEventListener('touchend', onTouchEnd)
}

function onStageClick() {
  if (!hasImage.value) openPicker()
}

function onStageKeydown() {
  if (!hasImage.value) openPicker()
}

// ── crop ──
function getCropPercent(): CropPercent {
  if (!imgW.value || !imgH.value || !stageW.value || !stageH.value) {
    return { x: 0, y: 0, width: 100, height: 100 }
  }
  const pxX = -tx.value / scale.value
  const pxY = -ty.value / scale.value
  return {
    x: (pxX / imgW.value) * 100,
    y: (pxY / imgH.value) * 100,
    width: ((stageW.value / scale.value) / imgW.value) * 100,
    height: ((stageH.value / scale.value) / imgH.value) * 100,
  }
}

async function apply() {
  if (!sourceFile.value || processing.value || props.busy) return
  processing.value = true
  errorMessage.value = ''
  try {
    const file = await editImage(sourceFile.value, {
      crop: normalizeCrop(getCropPercent()),
      maxWidth: OUTPUT_MAX_SIZE,
      maxHeight: OUTPUT_MAX_SIZE,
      quality: OUTPUT_QUALITY,
      format: OUTPUT_FORMAT,
    })
    emit('processed', file)
  } catch {
    errorMessage.value = t('ImageEditor:Failed')
  } finally {
    processing.value = false
  }
}

function close() {
  if (props.busy || processing.value) return
  emit('update:show', false)
}

function handleShow(value: boolean) {
  if (value) {
    emit('update:show', true)
  } else {
    close()
  }
}

watch(
  () => props.show,
  (show) => {
    if (!show) return
    errorMessage.value = ''
    dragover.value = false
    setSource(undefined)
    void loadDefaultImage()
  },
)

watch(
  () => props.defaultImageUrl,
  () => {
    if (props.show && !sourceFile.value) void loadDefaultImage()
  },
)

onBeforeUnmount(() => {
  if (sourceUrl.value) URL.revokeObjectURL(sourceUrl.value)
  unbindGlobal()
  resizeObserver?.disconnect()
})
</script>

<style scoped>
.avatar-editor {
  width: min(560px, calc(100vw - 32px));
}

.avatar-editor__body {
  display: grid;
  justify-items: center;
  gap: 10px;
}

.avatar-editor__stage {
  position: relative;
  width: min(420px, 100%, max(180px, calc(100vh - 260px)));
  aspect-ratio: 1;
  overflow: hidden;
  border: var(--po-border);
  border-radius: var(--po-r-lg);
  background: #101a2c;
  cursor: grab;
  user-select: none;
  touch-action: none;
}

.avatar-editor__stage:active {
  cursor: grabbing;
}

.avatar-editor__stage.is-empty {
  display: grid;
  place-items: center;
  align-content: center;
  gap: 10px;
  padding: 24px;
  border-style: dashed;
  background: var(--po-bg-deep);
  color: var(--po-ink-soft);
  cursor: pointer;
  text-align: center;
  transition:
    background-color 0.12s ease,
    border-color 0.12s ease;
}

.avatar-editor__stage.is-empty:hover {
  background: var(--po-accent-soft);
}

.avatar-editor__stage.is-dragover {
  border-color: var(--po-accent);
  border-style: solid;
  background: var(--po-accent-soft);
}

.avatar-editor__stage img {
  max-width: none;
  max-height: none;
}

.avatar-editor__crop {
  position: absolute;
  inset: 0;
  border: 1px solid rgb(255 255 255 / 55%);
  pointer-events: none;
}

.avatar-editor__grid {
  position: absolute;
  inset: 0;
}

.grid-line {
  position: absolute;
  background: rgb(255 255 255 / 25%);
}

.grid-h-1 {
  top: 33.333%;
  left: 0;
  right: 0;
  height: 1px;
}

.grid-h-2 {
  top: 66.667%;
  left: 0;
  right: 0;
  height: 1px;
}

.grid-v-1 {
  left: 33.333%;
  top: 0;
  bottom: 0;
  width: 1px;
}

.grid-v-2 {
  left: 66.667%;
  top: 0;
  bottom: 0;
  width: 1px;
}

.avatar-editor__empty {
  display: grid;
  justify-items: center;
  gap: 10px;
}

.avatar-editor__empty svg {
  width: 30px;
  height: 30px;
  color: var(--po-ink);
}

.avatar-editor__empty span {
  max-width: 240px;
  color: var(--po-ink-soft);
  font-size: 12.5px;
  font-weight: var(--po-weight-bold);
  line-height: 1.6;
}

.avatar-editor__hint {
  margin: 0;
  color: var(--po-ink-soft);
  font-size: 11.5px;
}

.avatar-editor__footer {
  display: flex;
  align-items: center;
  gap: 10px;
}

.avatar-editor__footer-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-left: auto;
}

.avatar-editor__error {
  margin: 8px 0 0;
  color: var(--po-danger);
  font-size: 12px;
  font-weight: 600;
}

</style>

<template>
  <NodeViewWrapper class="canvas-node-view" contenteditable="false">
    <img v-if="snapshotSrc" class="canvas-node-view__snapshot" :src="snapshotSrc" alt="" />
    <CanvasBoard
      v-else
      :model-value="document"
      :readonly="readonly"
      :resolve-export-segments="resolveExportSegments"
      @update:model-value="updateDocument"
      @remove="deleteNode"
    />
  </NodeViewWrapper>
</template>

<script setup lang="ts">
import { computed, inject } from 'vue'
import type { NodeViewProps } from '@tiptap/core'
import { NodeViewWrapper } from '@tiptap/vue-3'
import CanvasBoard from './CanvasBoard.vue'
import { ARTICLE_EXPORT_SNAPSHOTS_KEY, canvasIndexAt } from './canvasCapture'
import { cloneCanvas, EMPTY_CANVAS, parseCanvas, type CanvasDocument } from './canvasSyntax'
import { headingPathAt } from './imageUploadNaming'

const props = defineProps<NodeViewProps>()

/** 仅在导出渲染面中出现：该画布已截图，用图片替代交互画布，避免打印/内嵌时再次渲染。 */
const snapshots = inject(ARTICLE_EXPORT_SNAPSHOTS_KEY, null)
const snapshotSrc = computed(() => {
  if (!snapshots) return ''
  const index = canvasIndexAt(props.editor.state.doc, props.getPos())
  return snapshots.value[index] ?? ''
})

const document = computed<CanvasDocument>(() => {
  try {
    return parseCanvas(JSON.stringify(props.node.attrs.document))
  } catch {
    return cloneCanvas(EMPTY_CANVAS)
  }
})
const readonly = computed(() => !props.editor.isEditable)

/** 导出时实时取文章标题、画布所在标题链与文章内画布序号（从 1 开始）。 */
const resolveExportSegments = (): string[] => {
  const position = props.getPos()
  if (typeof position !== 'number') return []
  const options = props.extension.options as { resolveDocumentTitle?: () => string }
  const headings = headingPathAt(props.editor.state.doc, position)
  const index = canvasIndexAt(props.editor.state.doc, position)
  return [
    options.resolveDocumentTitle?.() ?? '',
    ...headings,
    index >= 0 ? String(index + 1) : '',
  ]
}

const updateDocument = (value: CanvasDocument) => {
  if (readonly.value) return
  props.updateAttributes({ document: cloneCanvas(value) })
}
</script>

<style scoped>
.canvas-node-view {
  display: block;
  margin: 18px 0;
}

.canvas-node-view__snapshot {
  display: block;
  max-width: 100%;
  height: auto;
  margin: 0 auto;
}
</style>

<template>
  <div class="markdown-viewer">
    <MarkdownEditor
      :model-value="content"
      :height="height === 'auto' ? 'auto' : height"
      :heading-ids="headingIds"
      :document-title="documentTitle"
      readonly
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import MarkdownEditor from '@/components/MarkdownEditor/index.vue'
import { extractToc } from '@/utils/markdown'

interface Props {
  content: string
  height?: string
  documentTitle?: string
}

const props = withDefaults(defineProps<Props>(), {
  height: 'auto',
  documentTitle: '',
})

const headingIds = computed(() => extractToc(props.content || '').map((item) => item.id))
</script>

<style scoped>
.markdown-viewer {
  width: 100%;
  background: transparent;
}
</style>

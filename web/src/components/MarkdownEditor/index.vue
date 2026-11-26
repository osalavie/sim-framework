<template>
  <div
    ref="rootRef"
    class="markdown-editor"
    :class="{
      'is-readonly': readonly,
      'is-document': mode === 'document',
    }"
    :style="editorStyle"
    @click.capture="handleEditorLinkClick"
    @keydown="handleEditorKeydown"
    @contextmenu="handleContextMenu"
    @touchstart.passive="handleTouchStart"
    @touchmove.passive="handleTouchMove"
    @touchend="handleTouchEnd"
    @touchcancel="handleTouchEnd"
  >
    <div class="document-surface">
      <EditorContent v-if="editor" :editor="editor" class="editor-content markdown-content" />
    </div>

    <span v-if="uploadingImages > 0" class="editor-upload-status">
      <i></i>{{ t('Editor:UploadingFiles') }}
    </span>

    <input
      ref="imageInputRef"
      class="po-sr-only"
      type="file"
      accept="image/*"
      multiple
      tabindex="-1"
      @change="handleImagePick"
    />

    <input ref="attachmentInputRef" class="po-sr-only" type="file" multiple tabindex="-1" @change="handleAttachmentPick" />

    <EditorContextMenu
      :show="menu.show"
      :x="menu.x"
      :y="menu.y"
      :groups="menuGroups"
      @select="handleMenuSelect"
      @close="closeMenu"
    />

    <n-modal
      v-model:show="linkDialog.show"
      preset="card"
      :title="t('Editor:Link')"
      :style="{ width: 'min(460px, 92vw)' }"
      @after-leave="linkDialog.url = ''"
    >
      <n-input
        v-model:value="linkDialog.url"
        :placeholder="t('Editor:LinkPlaceholder')"
        clearable
        @keydown.enter.prevent="applyLink"
      />
      <template #footer>
        <n-space justify="end">
          <n-button @click="applyLink">{{ t('Editor:LinkRemove') }}</n-button>
          <n-button type="primary" @click="applyLink">{{ t('Common:Confirm') }}</n-button>
        </n-space>
      </template>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, reactive, ref, watch } from 'vue'
import { Markdown } from '@tiptap/markdown'
import { Placeholder } from '@tiptap/extension-placeholder'
import { TableKit } from '@tiptap/extension-table'
import { StarterKit } from '@tiptap/starter-kit'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { t } from '@/i18n'
import { useHighlightTheme } from '@/hooks/useHighlightTheme'
import { contentFileApi } from '@/api/contentFile'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { handleError } from '@/utils/errorHandler'
import { CanvasExtension } from './canvasExtension'
import { cloneCanvas, EMPTY_CANVAS } from './canvasSyntax'
import { CodeBlockExtension } from './codeBlockExtension'
import { ManagedImageExtension } from './managedImageExtension'
import {
  buildSectionImageFileName,
  countSectionImages,
  headingPathAt,
} from './imageUploadNaming'
import EditorContextMenu from './EditorContextMenu.vue'
import {
  editorCommands,
  resolveEditorCommands,
  type EditorCommandContext,
  type ResolvedEditorCommand,
} from './editorCommands'
import '@/styles/markdown.css'

interface Props {
  encryptedAssets?: boolean
  modelValue: string
  height?: string
  placeholder?: string
  readonly?: boolean
  headingIds?: string[]
  /** embedded=表单内嵌编辑器；document=整页文档，交给页面滚动，不再自带边框与固定高度 */
  mode?: 'embedded' | 'document'
  /** 文章标题，画布导出文件名使用；只读阅读页同样透传。 */
  documentTitle?: string
}

const props = withDefaults(defineProps<Props>(), {
  height: '600px',
  placeholder: '在这里直接开始创作…',
  readonly: false,
  headingIds: () => [],
  mode: 'embedded',
})

const OPENABLE_PROTOCOLS = new Set(['http:', 'https:', 'mailto:', 'tel:'])

/** 编辑态 Ctrl/Cmd+左键、只读态普通左键打开链接；加密附件链接改为下载。 */
async function handleEditorLinkClick(event: MouseEvent) {
  if (event.button !== 0) return
  if (!props.readonly && !event.ctrlKey && !event.metaKey) return
  const anchor = (event.target as Element | null)?.closest?.('a')
  const href = anchor?.getAttribute('href') ?? ''
  if (!anchor || !href) return

  if (href.startsWith('/api/system/content-file/') && href.includes('#ef=')) {
    event.preventDefault()
    event.stopPropagation()
    try {
      const blob = await contentFileApi.getBlob(href)
      const objectUrl = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = blob instanceof File ? blob.name : 'download'
      link.click()
      setTimeout(() => URL.revokeObjectURL(objectUrl), 1000)
    } catch { useDiscreteApi.message.error(t('Editor:ImageLoadFailed')) }
    return
  }

  event.preventDefault()
  event.stopPropagation()
  if (href.startsWith('#')) {
    let id = href.slice(1)
    try { id = decodeURIComponent(id) } catch { /* 保持原始片段 */ }
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    return
  }
  let protocol = ''
  try { protocol = new URL(anchor.href).protocol } catch { return }
  if (!OPENABLE_PROTOCOLS.has(protocol)) return
  window.open(anchor.href, anchor.target || '_blank', 'noopener')
}
const emit = defineEmits<{
  (event: 'update:modelValue', value: string): void
  (event: 'save', value: string): void
}>()

const rootRef = ref<HTMLElement>()
const imageInputRef = ref<HTMLInputElement | null>(null)
const attachmentInputRef = ref<HTMLInputElement | null>(null)
const uploadingImages = ref(0)
const pendingSectionUploads = new Map<string, number>()
const menu = reactive({ show: false, x: 0, y: 0 })
const menuGroups = ref<ResolvedEditorCommand[][]>([])
const linkDialog = reactive({ show: false, url: '' })
let lastPublished = props.modelValue
let applyingExternalValue = false
useHighlightTheme()

const LONG_PRESS_MS = 450
let longPressTimer: number | null = null
let pressOrigin: { x: number; y: number } | null = null
let suppressNextContextMenu = false

const imageFilesFrom = (files: FileList | null | undefined) =>
  Array.from(files ?? []).filter((file) => file.type.startsWith('image/'))

const takeImageUploads = (files: File[], insertionPosition: number) => {
  const currentEditor = editor.value
  if (!currentEditor || files.length === 0) return null

  const headingPath = headingPathAt(currentEditor.state.doc, insertionPosition)
  const sectionKey = headingPath.join('\u0000')
  const pending = pendingSectionUploads.get(sectionKey) ?? 0
  const firstSequence = countSectionImages(currentEditor.state.doc, headingPath) + pending + 1
  pendingSectionUploads.set(sectionKey, pending + files.length)

  return {
    sectionKey,
    files: files.map((file, index) => {
      const name = buildSectionImageFileName(
        headingPath,
        firstSequence + index,
        file.name,
        file.type,
      )
      return new File([file], name, { type: file.type, lastModified: file.lastModified })
    }),
  }
}

const releaseImageUploads = (sectionKey: string, count: number) => {
  const remaining = (pendingSectionUploads.get(sectionKey) ?? 0) - count
  if (remaining > 0) pendingSectionUploads.set(sectionKey, remaining)
  else pendingSectionUploads.delete(sectionKey)
}

const insertImageFiles = (files: File[], insertionPosition: number) => {
  const upload = takeImageUploads(files, insertionPosition)
  if (!upload) return

  uploadingImages.value += upload.files.length
  void Promise.all(upload.files.map((file) => contentFileApi.upload(file, props.encryptedAssets)))
    .then((uploaded) => {
      const content = uploaded.flatMap((file) => [
        {
          type: 'image',
          attrs: {
            src: file.url,
            alt: file.fileName,
            title: file.fileName,
          },
        },
        { type: 'paragraph' },
      ])
      editor.value?.commands.insertContentAt(insertionPosition, content)
    })
    .catch((error) => {
      handleError(error, t('Editor:ImageUploadFailed'))
    })
    .finally(() => {
      uploadingImages.value = Math.max(0, uploadingImages.value - upload.files.length)
      releaseImageUploads(upload.sectionKey, upload.files.length)
    })
}

const applyHeadingIds = () => {
  nextTick(() => {
    const headings = rootRef.value?.querySelectorAll<HTMLHeadingElement>('h1, h2, h3, h4')
    headings?.forEach((heading, index) => {
      const id = props.headingIds[index]
      if (id) heading.id = id
    })
  })
}

const editor = useEditor({
  extensions: [
    StarterKit.configure({
      codeBlock: false,
      link: {
        // 链接跳转统一由根节点的捕获点击处理，避免只读态与插件重复打开
        openOnClick: false,
        HTMLAttributes: {
          rel: 'noopener noreferrer',
          target: '_blank',
        },
      },
    }),
    TableKit.configure({
      table: { resizable: !props.readonly },
    }),
    ManagedImageExtension.configure({
      allowBase64: false,
      resize: props.readonly ? false : { enabled: true },
      HTMLAttributes: { loading: 'lazy' },
    }),
    Placeholder.configure({
      // 使用函数读取响应式属性，语言切换后重算装饰即可拿到新文案
      placeholder: () => props.placeholder,
    }),
    CodeBlockExtension,
    CanvasExtension.configure({
      resolveDocumentTitle: () => props.documentTitle ?? '',
    }),
    Markdown.configure({
      markedOptions: {
        gfm: true,
        breaks: true,
      },
    }),
  ],
  content: props.modelValue || '',
  contentType: 'markdown',
  editable: !props.readonly,
  editorProps: {
    attributes: {
      'aria-label': props.readonly ? t('Editor:ReadonlyLabel') : t('Editor:EditableLabel'),
      spellcheck: 'true',
    },
    handlePaste: (_view, event) => {
      if (props.readonly) return false
      const images = imageFilesFrom(event.clipboardData?.files)
      if (images.length === 0) return false

      event.preventDefault()
      insertImageFiles(images, editor.value?.state.selection.from ?? 0)
      return true
    },
    handleDrop: (view, event) => {
      if (props.readonly) return false
      const images = imageFilesFrom(event.dataTransfer?.files)
      if (images.length === 0) return false

      event.preventDefault()
      const droppedAt = view.posAtCoords({ left: event.clientX, top: event.clientY })?.pos
      insertImageFiles(images, droppedAt ?? view.state.selection.from)
      return true
    },
  },
  onCreate: applyHeadingIds,
  onUpdate: ({ editor: currentEditor }) => {
    applyHeadingIds()
    if (applyingExternalValue || props.readonly) return
    const markdown = currentEditor.getMarkdown()
    lastPublished = markdown
    emit('update:modelValue', markdown)
  },
})

const editorStyle = computed(() => {
  if (props.mode === 'document') return undefined
  return props.readonly && props.height === 'auto' ? undefined : { height: props.height }
})

watch(
  () => props.modelValue,
  (value) => {
    if (!editor.value || value === lastPublished || editor.value.getMarkdown() === value) return
    applyingExternalValue = true
    editor.value.commands.setContent(value || '', { contentType: 'markdown', emitUpdate: false })
    lastPublished = value
    applyHeadingIds()
    queueMicrotask(() => {
      applyingExternalValue = false
    })
  },
)

watch(
  () => props.readonly,
  (readonly) => {
    editor.value?.setEditable(!readonly, false)
  },
)

// Placeholder 扩展在创建时缓存配置，语言切换后派发空事务重算装饰，
// 占位函数会重新读取当前 placeholder 属性
watch(
  () => props.placeholder,
  () => {
    editor.value?.view.dispatch(editor.value.state.tr)
  },
)

watch(
  () => props.headingIds,
  applyHeadingIds,
  { deep: true },
)

const insertCanvas = () => {
  editor.value
    ?.chain()
    .focus()
    .insertContent({
      type: 'canvas',
      attrs: { document: cloneCanvas(EMPTY_CANVAS) },
    })
    .run()
}

const normalizeLinkHref = (value: string) => {
  if (/^[a-zA-Z][a-zA-Z0-9+.-]*:/.test(value) || value.startsWith('/') || value.startsWith('#')) {
    return value
  }
  return `https://${value}`
}

const buildCommandContext = (currentEditor: NonNullable<typeof editor.value>): EditorCommandContext => ({
  editor: currentEditor,
  pickImage: () => imageInputRef.value?.click(),
  pickAttachment: () => attachmentInputRef.value?.click(),
  insertCanvas,
  openLink: () => {
    linkDialog.url = currentEditor.getAttributes('link').href ?? ''
    linkDialog.show = true
  },
  copySelection: () => {
    currentEditor.commands.focus()
    document.execCommand('copy')
  },
  cutSelection: () => {
    currentEditor.commands.focus()
    document.execCommand('cut')
  },
  pasteText: () => {
    navigator.clipboard
      .readText()
      .then((text) => {
        if (text) currentEditor.chain().focus().insertContent({
          type: 'text',
          text: text.replace(/\r\n/g, '\n'),
        }).run()
      })
      .catch(() => {
        useDiscreteApi.message.warning(t('Editor:PasteFailed'))
      })
  },
})

const openMenuAt = (x: number, y: number) => {
  const currentEditor = editor.value
  if (!currentEditor || props.readonly) return

  currentEditor.commands.focus()
  if (currentEditor.state.selection.empty) {
    let position: number | undefined
    try {
      position = currentEditor.view.posAtCoords({ left: x, top: y })?.pos
    } catch {
      position = undefined
    }
    if (typeof position === 'number') {
      currentEditor.commands.setTextSelection(position)
    }
  }

  menuGroups.value = resolveEditorCommands(buildCommandContext(currentEditor))
  if (menuGroups.value.length === 0) return
  menu.x = x
  menu.y = y
  menu.show = true
}

const closeMenu = () => {
  menu.show = false
}

const handleMenuSelect = (id: string) => {
  const command = editorCommands.find((candidate) => candidate.id === id)
  const currentEditor = editor.value
  menu.show = false
  if (!command || !currentEditor) return
  command.run(buildCommandContext(currentEditor))
}

const handleContextMenu = (event: MouseEvent) => {
  if (props.readonly) return
  event.preventDefault()
  if (suppressNextContextMenu) {
    suppressNextContextMenu = false
    return
  }
  openMenuAt(event.clientX, event.clientY)
}

const clearLongPress = () => {
  if (longPressTimer !== null) {
    window.clearTimeout(longPressTimer)
    longPressTimer = null
  }
  pressOrigin = null
}

const handleTouchStart = (event: TouchEvent) => {
  if (props.readonly || event.touches.length !== 1) return
  const touch = event.touches[0]
  if (!touch) return
  pressOrigin = { x: touch.clientX, y: touch.clientY }

  if (longPressTimer !== null) window.clearTimeout(longPressTimer)
  longPressTimer = window.setTimeout(() => {
    longPressTimer = null
    suppressNextContextMenu = true
    openMenuAt(touch.clientX, touch.clientY)
  }, LONG_PRESS_MS)
}

const handleTouchMove = (event: TouchEvent) => {
  if (!pressOrigin || event.touches.length !== 1) return
  const touch = event.touches[0]
  if (!touch) return
  if (Math.abs(touch.clientX - pressOrigin.x) > 10 || Math.abs(touch.clientY - pressOrigin.y) > 10) {
    clearLongPress()
  }
}

const handleTouchEnd = () => {
  clearLongPress()
}

const handleImagePick = (event: Event) => {
  const input = event.target as HTMLInputElement
  const files = imageFilesFrom(input.files)
  input.value = ''
  if (files.length === 0) return
  insertImageFiles(files, editor.value?.state.selection.from ?? 0)
}

const handleAttachmentPick = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  if (!files.length || !editor.value || props.readonly) return
  const position = editor.value.state.selection.from
  uploadingImages.value += files.length
  try {
    const uploaded = await Promise.all(files.map((file) => contentFileApi.upload(file, props.encryptedAssets)))
    editor.value?.commands.insertContentAt(position, uploaded.map((file) => ({ type: 'paragraph', content: [
      { type: 'text', text: file.fileName, marks: [{ type: 'link', attrs: { href: file.url } }] },
    ] })))
  } catch (error) { handleError(error, t('Editor:FileUploadFailed')) }
  finally { uploadingImages.value -= files.length }
}

const applyLink = () => {
  const currentEditor = editor.value
  if (!currentEditor) return
  const href = linkDialog.url.trim()
  const chain = currentEditor.chain().focus().extendMarkRange('link')
  if (!href) chain.unsetLink().run()
  else chain.setLink({ href: normalizeLinkHref(href) }).run()
  linkDialog.show = false
}

const focusEditor = (position: 'start' | 'end' = 'end') => {
  const currentEditor = editor.value
  if (!currentEditor) return
  const target = position === 'end' ? currentEditor.state.doc.content.size : 1
  currentEditor.chain().focus().setTextSelection(target).run()
}

const emitSave = () => {
  if (editor.value) emit('save', editor.value.getMarkdown())
}

const handleEditorKeydown = (event: KeyboardEvent) => {
  if (props.readonly) return
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
    event.preventDefault()
    emitSave()
  }
}

defineExpose({ editor, insertCanvas, focusEditor })
</script>

<style scoped>
.markdown-editor {
  width: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--theme-border);
  border-radius: 8px;
  background: transparent;
}

.document-surface {
  position: relative;
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 24px clamp(16px, 6vw, 72px);
  background: transparent;
}


.editor-content {
  width: min(100%, 1200px);
  min-height: 100%;
  margin: 0 auto;
  color: var(--theme-text-base);
}

.editor-content :deep(.tiptap) {
  min-height: 100%;
  outline: 0;
}

/* 文档模式：页面负责滚动，编辑器只提供内容区；长按不再弹出系统选择菜单，交给右键/长按菜单 */
.markdown-editor.is-document {
  display: block;
  overflow: visible;
  border: 0;
  border-radius: 0;
  background: transparent;
}

.is-document .document-surface {
  overflow: visible;
  padding: 0;
}

.is-document .editor-content {
  width: 100%;
  min-height: 0;
}

.is-document .editor-content :deep(.tiptap) {
  min-height: 42vh;
  -webkit-touch-callout: none;
}

.editor-content :deep(.tiptap > :first-child) {
  margin-top: 0;
}

.editor-content :deep(.tiptap > :last-child) {
  margin-bottom: 0;
}

.editor-content :deep(p.is-editor-empty:first-child::before) {
  height: 0;
  float: left;
  color: var(--theme-text-secondary);
  content: attr(data-placeholder);
  pointer-events: none;
}

.editor-content :deep(.ProseMirror-selectednode > .canvas-block) {
  outline: 3px solid color-mix(in srgb, var(--theme-primary) 24%, transparent);
  outline-offset: 3px;
}

.markdown-editor.is-readonly {
  display: block;
  overflow: visible;
  border: 0;
  border-radius: 0;
  background: transparent;
}

.is-readonly .document-surface {
  overflow: visible;
  padding: 0;
  background: transparent;
}

.is-readonly .editor-content {
  width: 100%;
  min-height: 0;
}

.editor-upload-status {
  position: fixed;
  z-index: 1200;
  right: 20px;
  bottom: 76px;
  display: flex;
  padding: 6px 12px;
  align-items: center;
  gap: 6px;
  color: var(--theme-text-secondary);
  border: var(--po-border-thin);
  border-radius: 999px;
  background: var(--po-paper);
  font-size: 12px;
}

.editor-upload-status i {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--theme-primary);
}

@media (max-width: 640px) {
  .document-surface {
    padding: 16px 12px;
  }
}
</style>

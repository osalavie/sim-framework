<template>
  <NodeViewWrapper class="mac-code-block">
    <div class="code-window-header" contenteditable="false">
      <span class="code-window-controls" aria-hidden="true">
        <i class="is-close"></i>
        <i class="is-minimize"></i>
        <i class="is-expand"></i>
      </span>
      <label v-if="editable" class="code-language-picker" @touchstart.stop>
        <span class="po-sr-only">{{ t('Editor:CodeLanguage') }}</span>
        <select
          class="code-language code-language-select"
          :value="languageValue"
          :aria-label="t('Editor:CodeLanguage')"
          @change="changeLanguage"
          @mousedown.stop
          @click.stop
        >
          <option v-for="option in languageOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
        <svg class="code-language-caret" aria-hidden="true" viewBox="0 0 10 6" fill="none">
          <path d="M1 1.5 5 5l4-3.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
        </svg>
      </label>
      <span v-else class="code-language">{{ languageLabel }}</span>
      <button
        type="button"
        class="code-copy-button"
        :aria-label="copied ? t('Common:Copied') : t('Editor:Code:Copy')"
        @mousedown.prevent
        @click="copyCode"
      >
        <svg aria-hidden="true" viewBox="0 0 20 20" fill="none">
          <path
            d="M7 6V4.75C7 3.78 7.78 3 8.75 3h6.5C16.22 3 17 3.78 17 4.75v6.5c0 .97-.78 1.75-1.75 1.75H14"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
          />
          <rect x="3" y="7" width="10" height="10" rx="1.75" stroke="currentColor" stroke-width="1.5" />
        </svg>
        <span>{{ copied ? t('Common:Copied') : t('Editor:Code:Copy') }}</span>
      </button>
    </div>
    <pre><NodeViewContent as="code" class="hljs" /></pre>
  </NodeViewWrapper>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import type { NodeViewProps } from '@tiptap/core'
import { NodeViewContent, NodeViewWrapper } from '@tiptap/vue-3'
import { t } from '@/i18n'
import { codeBlockLanguages } from './codeBlockExtension'

const props = defineProps<NodeViewProps>()
const copied = ref(false)
let resetTimer: ReturnType<typeof setTimeout> | undefined

const editable = computed(() => props.editor.isEditable)

const currentLanguage = computed(() => String(props.node.attrs.language || '').trim())

const languageLabel = computed(() => currentLanguage.value || t('Common:PlainText'))

const languageOptions = computed(() => {
  const options = [
    { value: '', label: t('Common:PlainText') },
    ...codeBlockLanguages
      .filter((language) => language !== 'plaintext')
      .map((language) => ({ value: language, label: language })),
  ]
  const current = currentLanguage.value.toLowerCase()
  if (current && current !== 'plaintext' && !options.some((option) => option.value === current)) {
    options.push({ value: current, label: current })
  }
  return options
})

const languageValue = computed(() => {
  const current = currentLanguage.value.toLowerCase()
  return current === 'plaintext' ? '' : current
})

const changeLanguage = (event: Event) => {
  const value = (event.target as HTMLSelectElement).value
  props.updateAttributes({ language: value || null })
}

const writeClipboard = async (value: string) => {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value)
    return
  }

  const textArea = document.createElement('textarea')
  textArea.value = value
  textArea.setAttribute('readonly', '')
  textArea.style.position = 'fixed'
  textArea.style.opacity = '0'
  document.body.appendChild(textArea)
  textArea.select()
  const copied = document.execCommand('copy')
  textArea.remove()
  if (!copied) throw new Error('Clipboard copy was rejected')
}

const copyCode = async () => {
  try {
    await writeClipboard(props.node.textContent)
    copied.value = true
    clearTimeout(resetTimer)
    resetTimer = setTimeout(() => {
      copied.value = false
    }, 1800)
  } catch {
    copied.value = false
  }
}

onBeforeUnmount(() => clearTimeout(resetTimer))
</script>

<style scoped>
.mac-code-block {
  --code-window-header: var(--theme-bg-secondary);
  --code-window-body: var(--theme-bg-base);
  --code-window-border: var(--theme-border);
  --code-window-text: var(--theme-text-base);
  --code-window-muted: var(--theme-text-secondary);
  --code-window-hover: var(--theme-bg-tertiary);
  --code-window-focus: var(--theme-primary);
  margin: 20px 0;
  overflow: hidden;
  border: 1px solid var(--code-window-border);
  border-radius: 12px;
  background: var(--code-window-body);
  box-shadow: var(--po-shadow-card, 3px 3px 0 #d9e2f1);
}

.code-window-header {
  position: relative;
  display: grid;
  min-height: 42px;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: 12px;
  padding: 0 12px;
  border-bottom: 1px solid var(--code-window-border);
  background: var(--code-window-header);
  user-select: none;
}

.code-window-controls {
  display: flex;
  align-items: center;
  gap: 8px;
  justify-self: start;
}

.code-window-controls i {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  box-shadow: inset 0 0 0 0.5px rgb(0 0 0 / 18%);
}

.code-window-controls .is-close {
  background: #ff5f57;
}

.code-window-controls .is-minimize {
  background: #febc2e;
}

.code-window-controls .is-expand {
  background: #28c840;
}

.code-language {
  overflow: hidden;
  color: var(--code-window-muted);
  font-family: var(--po-font-stack);
  font-size: 11px;
  font-weight: var(--po-weight-bold);
  letter-spacing: 0.08em;
  line-height: 1;
  text-overflow: ellipsis;
  text-transform: uppercase;
  white-space: nowrap;
}

.code-language-picker {
  position: relative;
  display: inline-flex;
  min-width: 0;
  align-items: center;
}

.code-language-select {
  max-width: 180px;
  padding: 4px 16px 4px 6px;
  border: 0;
  border-radius: 6px;
  appearance: none;
  background: transparent;
  color: inherit;
  cursor: pointer;
  font: inherit;
  letter-spacing: inherit;
  text-align: center;
  text-transform: inherit;
}

.code-language-select option {
  color: var(--theme-text-base);
  background: var(--theme-bg-base);
  text-transform: none;
}

.code-language-select:hover {
  background: var(--code-window-hover);
  color: var(--code-window-text);
}

.code-language-select:focus-visible {
  outline: 2px solid var(--code-window-focus);
  outline-offset: 1px;
}

.code-language-caret {
  position: absolute;
  right: 4px;
  width: 8px;
  height: 5px;
  color: var(--code-window-muted);
  pointer-events: none;
}

.code-copy-button {
  display: inline-flex;
  min-width: 70px;
  min-height: 30px;
  align-items: center;
  justify-content: center;
  gap: 5px;
  justify-self: end;
  padding: 0 9px;
  border: 1px solid var(--code-window-border);
  border-radius: 6px;
  background: transparent;
  color: var(--code-window-muted);
  font-family: var(--po-font-stack);
  font-size: 12px;
  cursor: pointer;
}

.code-copy-button:hover {
  border-color: var(--code-window-focus);
  background: var(--code-window-hover);
  color: var(--code-window-text);
}

.code-copy-button:focus-visible {
  outline: 2px solid var(--code-window-focus);
  outline-offset: 2px;
}

.code-copy-button svg {
  width: 15px;
  height: 15px;
}

pre {
  margin: 0;
  overflow-x: auto;
  border: 0;
  border-radius: 0;
  background: transparent;
}

.mac-code-block pre code.hljs {
  display: block;
  min-height: 58px;
  padding: 18px 20px;
  background: transparent;
  color: var(--code-window-text);
  font-family: var(--po-font-mono);
  font-size: 14px;
  line-height: 1.65;
  tab-size: 2;
  white-space: pre;
}

@media (max-width: 640px) {
  .mac-code-block {
    margin-inline: -12px;
    border-radius: 0;
  }

  .code-window-header {
    padding-inline: 10px;
  }

  .code-copy-button {
    min-width: 44px;
    min-height: 44px;
  }

  .code-copy-button span {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip: rect(0 0 0 0);
    white-space: nowrap;
  }

  .mac-code-block pre code.hljs {
    padding: 16px 12px;
    font-size: 13px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .code-copy-button {
    transition: none;
  }
}
</style>

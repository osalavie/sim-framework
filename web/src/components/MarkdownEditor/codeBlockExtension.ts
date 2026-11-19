import { CodeBlockLowlight } from '@tiptap/extension-code-block-lowlight'
import { VueNodeViewRenderer } from '@tiptap/vue-3'
import { common, createLowlight } from 'lowlight'
import CodeBlockNodeView from './CodeBlockNodeView.vue'

export const codeBlockLowlight = createLowlight(common)

/** 与高亮器保持一致的语言清单，供代码块语言下拉复用。 */
export const codeBlockLanguages = codeBlockLowlight.listLanguages().sort()

export const CodeBlockExtension = CodeBlockLowlight.extend({
  addNodeView() {
    return VueNodeViewRenderer(CodeBlockNodeView)
  },
}).configure({
  lowlight: codeBlockLowlight,
  languageClassPrefix: 'language-',
})

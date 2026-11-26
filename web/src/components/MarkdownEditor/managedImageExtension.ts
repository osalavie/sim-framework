import { Image } from '@tiptap/extension-image'
import { VueNodeViewRenderer } from '@tiptap/vue-3'
import ManagedImageNodeView from './ManagedImageNodeView.vue'

export const ManagedImageExtension = Image.extend({
  addNodeView() {
    return VueNodeViewRenderer(ManagedImageNodeView)
  },
})

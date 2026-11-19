import { createPinia } from 'pinia'
import type { Editor } from '@tiptap/core'
import { flushPromises, mount } from '@vue/test-utils'
import { NButton, NButtonGroup } from 'naive-ui'
import { nextTick } from 'vue'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { t } from '@/i18n'
import { stringifyCanvas, type CanvasDocument } from './canvasSyntax'
import MarkdownEditor from './index.vue'

const contentFileMocks = vi.hoisted(() => ({
  upload: vi.fn(),
  getBlob: vi.fn(),
}))

vi.mock('@/api/contentFile', () => ({
  contentFileApi: contentFileMocks,
}))

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}

/** jsdom 无 DOMMatrixReadOnly，VueFlow 测量节点内部尺寸时会调用构造函数。 */
class DOMMatrixReadOnlyMock {
  m22 = 1
}

globalThis.ResizeObserver = ResizeObserverMock
globalThis.DOMMatrixReadOnly = DOMMatrixReadOnlyMock as unknown as typeof DOMMatrixReadOnly

// 挂载整编辑器（tiptap+VueFlow）的集成用例在 CI 并发下开销大，放宽文件级超时
describe('MarkdownEditor', { timeout: 20000 }, () => {
  beforeEach(() => {
    contentFileMocks.upload.mockReset()
    contentFileMocks.getBlob.mockReset()
  })

  const global = {
    plugins: [createPinia()],
    components: { NButton, NButtonGroup },
    stubs: {
      teleport: true,
      'n-modal': true,
      'n-input': true,
      'n-space': true,
    },
  }

  it('uploads attachments as encrypted files when article encryption is enabled', async () => {
    const url = '/api/system/content-file/11111111-2222-3333-4444-555555555555#ef=AAAAAAAAAAAAAAAAAAAAAA'
    contentFileMocks.upload.mockResolvedValue({ url, fileName: 'private.pdf' })
    const wrapper = mount(MarkdownEditor, { props: { modelValue: 'body', encryptedAssets: true }, global })
    await flushPromises()
    const input = wrapper.findAll('input[type="file"]').find((item) => !item.attributes('accept'))!
    const file = new File(['test-only attachment'], 'private.pdf', { type: 'application/pdf' })
    Object.defineProperty(input.element, 'files', { configurable: true, value: [file] })
    await input.trigger('change')
    await flushPromises()
    expect(contentFileMocks.upload).toHaveBeenCalledWith(file, true)
    expect(wrapper.find('a').attributes('href')).toBe(url)
    wrapper.unmount()
  })

  it('strips executable attributes and script URLs in the actual readonly renderer', async () => {
    const wrapper = mount(MarkdownEditor, { props: { readonly: true,
      modelValue: '<img src="x" onerror="alert(1)"><a href="javascript:alert(2)">link</a><script>alert(3)</script>',
    }, global })
    await flushPromises()
    expect(wrapper.find('script').exists()).toBe(false)
    expect(wrapper.find('[onerror]').exists()).toBe(false)
    expect(wrapper.find('a[href^="javascript:"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('opens links only with Ctrl/Cmd click while editing', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    const wrapper = mount(MarkdownEditor, {
      props: { modelValue: '[示例](https://example.com/page)' },
      global,
    })
    await flushPromises()
    await nextTick()

    await wrapper.find('.tiptap a').trigger('click', { button: 0 })
    expect(open).not.toHaveBeenCalled()

    await wrapper.find('.tiptap a').trigger('click', { button: 0, ctrlKey: true })
    expect(open).toHaveBeenCalledTimes(1)
    expect(open.mock.calls[0]?.[0]).toBe('https://example.com/page')
    expect(open.mock.calls[0]?.[1]).toBe('_blank')
    open.mockRestore()
    wrapper.unmount()
  })

  it('opens links with a plain click in read-only mode', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    const wrapper = mount(MarkdownEditor, {
      props: { modelValue: '[示例](https://example.com/page)', readonly: true },
      global,
    })
    await flushPromises()
    await nextTick()

    await wrapper.find('.tiptap a').trigger('click', { button: 0 })
    expect(open).toHaveBeenCalledTimes(1)
    expect(open.mock.calls[0]?.[0]).toBe('https://example.com/page')
    open.mockRestore()
    wrapper.unmount()
  })

  it('uploads pasted images with the heading path and section sequence as file name', async () => {
    contentFileMocks.upload.mockImplementationOnce(async (file: File) => ({
      id: '866f0c2a-1854-48f0-93bd-46b85b0792e0',
      provider: 'local',
      key: 'content/image.png',
      fileName: file.name,
      contentType: 'image/png',
      length: 4,
      url: '/api/system/content-file/866f0c2a-1854-48f0-93bd-46b85b0792e0',
    }))
    contentFileMocks.getBlob.mockResolvedValue(new Blob(['image'], { type: 'image/png' }))
    Object.defineProperty(URL, 'createObjectURL', {
      configurable: true,
      value: vi.fn(() => 'blob:managed-image'),
    })
    Object.defineProperty(URL, 'revokeObjectURL', {
      configurable: true,
      value: vi.fn(),
    })

    const wrapper = mount(MarkdownEditor, {
      props: {
        modelValue: '# 模块化架构实践\n\n## 分层职责\n\n正文\n\n',
      },
      global,
    })
    await flushPromises()
    await nextTick()

    const instance = wrapper.vm as unknown as { editor: Editor }
    instance.editor.commands.setTextSelection(instance.editor.state.doc.content.size - 1)

    const paste = new Event('paste', { bubbles: true, cancelable: true })
    const image = new File(['image'], 'clipboard.png', { type: 'image/png' })
    Object.defineProperty(paste, 'clipboardData', {
      value: {
        files: [image],
        items: [],
        types: ['Files'],
        getData: () => '',
      },
    })
    wrapper.find('.tiptap').element.dispatchEvent(paste)
    await flushPromises()
    await nextTick()

    const uploaded = contentFileMocks.upload.mock.calls[0]?.[0] as File
    expect(uploaded).not.toBe(image)
    expect(uploaded.name).toBe('模块化架构实践-分层职责-1.png')
    expect(uploaded.type).toBe('image/png')
    const updates = wrapper.emitted<string[]>('update:modelValue') ?? []
    expect(updates[updates.length - 1]?.[0]).toContain(
      '/api/system/content-file/866f0c2a-1854-48f0-93bd-46b85b0792e0',
    )
    wrapper.unmount()
  })

  it('numbers multiple pasted images within the same heading section', async () => {
    contentFileMocks.upload.mockImplementation(async (file: File) => ({
      id: file.name,
      provider: 'local',
      key: file.name,
      fileName: file.name,
      contentType: file.type,
      length: 4,
      url: `/api/system/content-file/${encodeURIComponent(file.name)}`,
    }))
    contentFileMocks.getBlob.mockResolvedValue(new Blob(['image'], { type: 'image/png' }))

    const wrapper = mount(MarkdownEditor, {
      props: {
        modelValue: '# 模块化架构实践\n\n## 分层职责\n\n正文\n\n',
      },
      global,
    })
    await flushPromises()
    await nextTick()

    const instance = wrapper.vm as unknown as { editor: Editor }
    instance.editor.commands.setTextSelection(instance.editor.state.doc.content.size - 1)

    const paste = new Event('paste', { bubbles: true, cancelable: true })
    Object.defineProperty(paste, 'clipboardData', {
      value: {
        files: [
          new File(['first'], 'first.png', { type: 'image/png' }),
          new File(['second'], 'second.jpg', { type: 'image/jpeg' }),
        ],
        items: [],
        types: ['Files'],
        getData: () => '',
      },
    })
    wrapper.find('.tiptap').element.dispatchEvent(paste)
    await flushPromises()
    await nextTick()

    expect(contentFileMocks.upload.mock.calls.map(([file]) => (file as File).name)).toEqual([
      '模块化架构实践-分层职责-1.png',
      '模块化架构实践-分层职责-2.jpg',
    ])
    wrapper.unmount()
  })

  it('keeps one continuous document and inserts a canvas at the editor selection', async () => {
    const wrapper = mount(MarkdownEditor, {
      props: {
        modelValue: '# Existing article\n\nParagraph before\n\nParagraph after',
      },
      global,
    })
    await flushPromises()
    await nextTick()

    expect(wrapper.find('.tiptap').exists()).toBe(true)
    expect(wrapper.text()).toContain('Paragraph before')
    expect(wrapper.text()).toContain('Paragraph after')
    expect(
      wrapper.findAll('button').some((button) => button.text() === t('Common:Save')),
    ).toBe(false)

    const instance = wrapper.vm as unknown as { editor: Editor; insertCanvas: () => void }
    let insertionPosition = 1
    instance.editor.state.doc.descendants((node, position) => {
      if (node.isText && node.text === 'Paragraph before') {
        insertionPosition = position + node.nodeSize
      }
    })
    instance.editor.commands.setTextSelection(insertionPosition)

    instance.insertCanvas()
    await flushPromises()
    await nextTick()

    expect(wrapper.findAll('.canvas-block')).toHaveLength(1)
    const updates = wrapper.emitted<string[]>('update:modelValue')
    const markdown = updates?.[updates.length - 1]?.[0] ?? ''
    const beforeIndex = markdown.indexOf('Paragraph before')
    const canvasIndex = markdown.indexOf('```canvas')
    const afterIndex = markdown.indexOf('Paragraph after')
    expect(beforeIndex).toBeGreaterThanOrEqual(0)
    expect(canvasIndex).toBeGreaterThan(beforeIndex)
    expect(afterIndex).toBeGreaterThan(canvasIndex)

    wrapper.unmount()
  })

  it('opens the formatting menu on right click and applies a command', async () => {
    const wrapper = mount(MarkdownEditor, {
      props: {
        modelValue: '正文内容',
      },
      global,
    })
    await flushPromises()
    await nextTick()

    const editorElement = wrapper.find('.tiptap')
    editorElement.element.dispatchEvent(
      new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 40, clientY: 40 }),
    )
    await nextTick()

    const menu = wrapper.find('.editor-context-menu')
    expect(menu.exists()).toBe(true)
    const boldButton = menu
      .findAll('button')
      .find((button) => button.text() === t('Editor:Bold'))
    expect(boldButton).toBeDefined()

    await boldButton!.trigger('click')
    await nextTick()

    const instance = wrapper.vm as unknown as { editor: Editor }
    expect(instance.editor.isActive('bold')).toBe(true)
    expect(wrapper.find('.editor-context-menu').exists()).toBe(false)
    wrapper.unmount()
  })

  it('renders the same canvas node in read-only mode without mutation controls', async () => {
    const canvas: CanvasDocument = {
      version: 2,
      height: 420,
      nodes: [
        {
          id: 'shape-a',
          shape: 'rect',
          x: 40,
          y: 40,
          width: 160,
          height: 80,
          label: 'Read-only shape',
          color: '#2f7d6d',
        },
      ],
      edges: [],
    }
    const wrapper = mount(MarkdownEditor, {
      props: {
        modelValue: `# Article\n\n${stringifyCanvas(canvas)}\n\nAfter canvas`,
        readonly: true,
        height: 'auto',
      },
      global,
    })
    await flushPromises()
    await nextTick()

    expect(wrapper.find('.editor-toolbar').exists()).toBe(false)
    expect(wrapper.find('.canvas-block.is-readonly').exists()).toBe(true)
    expect(wrapper.find('.canvas-shape.is-readonly').exists()).toBe(true)
    expect(wrapper.findAll('.shape-handle')).toHaveLength(4)
    expect(wrapper.find('.shape-label').text()).toBe('Read-only shape')
    expect(wrapper.find('.shape-editor').exists()).toBe(false)
    expect(wrapper.find('.stencil-panel').exists()).toBe(false)
    expect(wrapper.find('.canvas-header').exists()).toBe(false)
    expect(wrapper.text()).toContain('After canvas')
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()

    wrapper.unmount()
  })

  it('renders dangling anchors as plain dots without the label editor', async () => {
    const canvas: CanvasDocument = {
      version: 2,
      height: 420,
      nodes: [
        {
          id: 'shape-a',
          shape: 'rect',
          x: 40,
          y: 40,
          width: 160,
          height: 80,
          label: 'Shape',
          color: '#2f7d6d',
        },
        { id: 'anchor-a', shape: 'anchor', x: 260, y: 60, width: 10, height: 10, label: '', color: '#2f7d6d' },
        { id: 'anchor-b', shape: 'anchor', x: 260, y: 260, width: 10, height: 10, label: '', color: '#2f7d6d' },
      ],
      edges: [
        {
          id: 'edge-anchor',
          source: 'anchor-a',
          target: 'anchor-b',
          sourcePort: 'bottom',
          targetPort: 'top',
          type: 'straight',
          line: 'solid',
          arrow: 'arrow',
          label: '',
        },
      ],
    }
    const wrapper = mount(MarkdownEditor, {
      props: { modelValue: stringifyCanvas(canvas), height: 'auto' },
      global,
    })
    await flushPromises()
    await nextTick()

    // 锚点只呈现小圆点：不得落入标签编辑框分支（曾是 v-else 把锚点渲染成虚线文本框）
    expect(wrapper.findAll('.canvas-shape')).toHaveLength(3)
    expect(wrapper.findAll('.canvas-shape.is-anchor')).toHaveLength(2)
    expect(wrapper.findAll('.shape-editor')).toHaveLength(0)
    expect(wrapper.findAll('.shape-label')).toHaveLength(1)
    expect(wrapper.find('.shape-label').text()).toBe('Shape')

    wrapper.unmount()
  })

  it('keeps malformed canvas syntax as an ordinary fenced code block', async () => {
    const wrapper = mount(MarkdownEditor, {
      props: {
        modelValue: '```canvas\nnot-json\n```',
        readonly: true,
        height: 'auto',
      },
      global,
    })
    await flushPromises()
    await nextTick()

    expect(wrapper.find('.canvas-block').exists()).toBe(false)
    expect(wrapper.find('.mac-code-block').exists()).toBe(true)
    expect(wrapper.find('pre code').text()).toBe('not-json')

    wrapper.unmount()
  })

  it('highlights fenced code and exposes macOS controls, language and copy action', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    })
    const wrapper = mount(MarkdownEditor, {
      props: {
        modelValue: '```typescript\nconst answer: number = 42\n```',
        readonly: true,
        height: 'auto',
      },
      global,
    })
    await flushPromises()
    await nextTick()

    expect(wrapper.find('.mac-code-block').exists()).toBe(true)
    expect(wrapper.findAll('.code-window-controls i')).toHaveLength(3)
    expect(wrapper.find('.code-language').text()).toBe('typescript')
    expect(wrapper.find('.hljs-keyword').text()).toBe('const')

    await wrapper.find('.code-copy-button').trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith('const answer: number = 42')
    expect(wrapper.find('.code-copy-button').text()).toContain(t('Common:Copied'))

    wrapper.unmount()
  })

  it('switches the code block language from the editor and keeps it in markdown', async () => {
    const wrapper = mount(MarkdownEditor, {
      props: {
        modelValue: '```typescript\nconst answer = 42\n```',
      },
      global,
    })
    await flushPromises()
    await nextTick()

    const select = wrapper.find('select.code-language-select')
    expect(select.exists()).toBe(true)
    await select.setValue('python')
    await flushPromises()

    const updates = wrapper.emitted<string[]>('update:modelValue') ?? []
    expect(updates[updates.length - 1]?.[0]).toContain('```python')
    expect((wrapper.find('.code-language-select').element as HTMLSelectElement).value).toBe('python')

    wrapper.unmount()
  })
})

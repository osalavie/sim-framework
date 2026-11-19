import type { Editor } from '@tiptap/core'

export type EditorCommandGroup = 'inline' | 'block' | 'insert' | 'clipboard'

export interface EditorCommandContext {
  editor: Editor
  pickImage: () => void
  pickAttachment?: () => void
  insertCanvas: () => void
  openLink: () => void
  copySelection: () => void
  cutSelection: () => void
  pasteText: () => void
}

export interface EditorCommand {
  id: string
  labelKey: string
  group: EditorCommandGroup
  run: (context: EditorCommandContext) => void
  isActive?: (editor: Editor) => boolean
  isEnabled?: (context: EditorCommandContext) => boolean
}

const hasSelection = (editor: Editor) => !editor.state.selection.empty

/**
 * 右键/长按菜单的格式动作注册表。
 * 键盘快捷键与 Markdown 输入规则仍由 Tiptap 自带，这里只补齐无工具栏后的显式入口。
 */
export const editorCommands: EditorCommand[] = [
  {
    id: 'bold',
    labelKey: 'Editor:Bold',
    group: 'inline',
    run: ({ editor }) => editor.chain().focus().toggleBold().run(),
    isActive: (editor) => editor.isActive('bold'),
  },
  {
    id: 'italic',
    labelKey: 'Editor:Italic',
    group: 'inline',
    run: ({ editor }) => editor.chain().focus().toggleItalic().run(),
    isActive: (editor) => editor.isActive('italic'),
  },
  {
    id: 'strike',
    labelKey: 'Editor:Strike',
    group: 'inline',
    run: ({ editor }) => editor.chain().focus().toggleStrike().run(),
    isActive: (editor) => editor.isActive('strike'),
  },
  {
    id: 'code',
    labelKey: 'Editor:InlineCode',
    group: 'inline',
    run: ({ editor }) => editor.chain().focus().toggleCode().run(),
    isActive: (editor) => editor.isActive('code'),
  },
  {
    id: 'link',
    labelKey: 'Editor:Link',
    group: 'inline',
    run: ({ openLink }) => openLink(),
    isActive: (editor) => editor.isActive('link'),
  },
  {
    id: 'paragraph',
    labelKey: 'Editor:Paragraph',
    group: 'block',
    run: ({ editor }) => editor.chain().focus().setParagraph().run(),
    isActive: (editor) => editor.isActive('paragraph'),
  },
  {
    id: 'heading1',
    labelKey: 'Editor:Heading1',
    group: 'block',
    run: ({ editor }) => editor.chain().focus().toggleHeading({ level: 1 }).run(),
    isActive: (editor) => editor.isActive('heading', { level: 1 }),
  },
  {
    id: 'heading2',
    labelKey: 'Editor:Heading2',
    group: 'block',
    run: ({ editor }) => editor.chain().focus().toggleHeading({ level: 2 }).run(),
    isActive: (editor) => editor.isActive('heading', { level: 2 }),
  },
  {
    id: 'heading3',
    labelKey: 'Editor:Heading3',
    group: 'block',
    run: ({ editor }) => editor.chain().focus().toggleHeading({ level: 3 }).run(),
    isActive: (editor) => editor.isActive('heading', { level: 3 }),
  },
  {
    id: 'blockquote',
    labelKey: 'Editor:Quote',
    group: 'block',
    run: ({ editor }) => editor.chain().focus().toggleBlockquote().run(),
    isActive: (editor) => editor.isActive('blockquote'),
  },
  {
    id: 'bulletList',
    labelKey: 'Editor:BulletList',
    group: 'block',
    run: ({ editor }) => editor.chain().focus().toggleBulletList().run(),
    isActive: (editor) => editor.isActive('bulletList'),
  },
  {
    id: 'orderedList',
    labelKey: 'Editor:OrderedList',
    group: 'block',
    run: ({ editor }) => editor.chain().focus().toggleOrderedList().run(),
    isActive: (editor) => editor.isActive('orderedList'),
  },
  {
    id: 'codeBlock',
    labelKey: 'Editor:CodeBlock',
    group: 'block',
    run: ({ editor }) => editor.chain().focus().toggleCodeBlock().run(),
    isActive: (editor) => editor.isActive('codeBlock'),
  },
  {
    id: 'horizontalRule',
    labelKey: 'Editor:HorizontalRule',
    group: 'insert',
    run: ({ editor }) => editor.chain().focus().setHorizontalRule().run(),
  },
  {
    id: 'image',
    labelKey: 'Editor:InsertImage',
    group: 'insert',
    run: ({ pickImage }) => pickImage(),
  },
  {
    id: 'canvas',
    labelKey: 'Editor:InsertCanvas',
    group: 'insert',
    run: ({ insertCanvas }) => insertCanvas(),
  },
  {
    id: 'attachment',
    labelKey: 'Editor:InsertAttachment',
    group: 'insert',
    run: ({ pickAttachment }) => pickAttachment?.(),
    isEnabled: ({ pickAttachment }) => Boolean(pickAttachment),
  },
  {
    id: 'table',
    labelKey: 'Editor:InsertTable',
    group: 'insert',
    run: ({ editor }) =>
      editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(),
  },
  {
    id: 'cut',
    labelKey: 'Editor:Cut',
    group: 'clipboard',
    run: ({ cutSelection }) => cutSelection(),
    isEnabled: ({ editor }) => hasSelection(editor),
  },
  {
    id: 'copy',
    labelKey: 'Editor:Copy',
    group: 'clipboard',
    run: ({ copySelection }) => copySelection(),
    isEnabled: ({ editor }) => hasSelection(editor),
  },
  {
    id: 'paste',
    labelKey: 'Editor:Paste',
    group: 'clipboard',
    run: ({ pasteText }) => pasteText(),
    isEnabled: () =>
      typeof navigator !== 'undefined' && typeof navigator.clipboard?.readText === 'function',
  },
]

const groupOrder: EditorCommandGroup[] = ['inline', 'block', 'insert', 'clipboard']

export interface ResolvedEditorCommand extends EditorCommand {
  active: boolean
  disabled: boolean
}

export function resolveEditorCommands(context: EditorCommandContext): ResolvedEditorCommand[][] {
  return groupOrder
    .map((group) =>
      editorCommands
        .filter((command) => command.group === group)
        .map((command) => ({
          ...command,
          active: command.isActive?.(context.editor) ?? false,
          disabled: command.isEnabled ? !command.isEnabled(context) : false,
        }))
        .filter((command) => command.group !== 'clipboard' || !command.disabled),
    )
    .filter((group) => group.length > 0)
}

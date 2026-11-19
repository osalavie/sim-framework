import { onMounted, onUnmounted } from 'vue'
import { t } from '@/i18n'

export interface KeyboardShortcut {
  key: string
  ctrl?: boolean
  shift?: boolean
  alt?: boolean
  meta?: boolean
  handler: (event: KeyboardEvent) => void
  description?: string
}

/**
 * 键盘快捷键 Hook
 * 用于注册和管理键盘快捷键
 */
export function useKeyboard(shortcuts: KeyboardShortcut[]) {
  const handleKeyDown = (event: KeyboardEvent) => {
    for (const shortcut of shortcuts) {
      const keyMatch = event.key.toLowerCase() === shortcut.key.toLowerCase()
      const ctrlMatch = shortcut.ctrl
        ? event.ctrlKey || event.metaKey
        : !event.ctrlKey && !event.metaKey
      const shiftMatch = shortcut.shift ? event.shiftKey : !event.shiftKey
      const altMatch = shortcut.alt ? event.altKey : !event.altKey
      const metaMatch = shortcut.meta ? event.metaKey : !event.metaKey

      if (keyMatch && ctrlMatch && shiftMatch && altMatch && metaMatch) {
        event.preventDefault()
        shortcut.handler(event)
        break
      }
    }
  }

  onMounted(() => {
    window.addEventListener('keydown', handleKeyDown)
  })

  onUnmounted(() => {
    window.removeEventListener('keydown', handleKeyDown)
  })

  return {
    shortcuts,
  }
}

/**
 * 表格导航 Hook
 * 提供表格行的键盘导航功能
 */
export function useTableNavigation<T>(options: {
  data: () => T[]
  onSelect?: (item: T, index: number) => void
  onEdit?: (item: T, index: number) => void
  onDelete?: (item: T, index: number) => void
}) {
  let currentIndex = -1

  const shortcuts: KeyboardShortcut[] = [
    {
      key: 'ArrowDown',
      description: t('Shortcut:Next'),
      handler: () => {
        const data = options.data()
        if (data.length === 0) return
        currentIndex = Math.min(currentIndex + 1, data.length - 1)
        const item = data[currentIndex]
        if (item !== undefined && options.onSelect) {
          options.onSelect(item, currentIndex)
        }
      },
    },
    {
      key: 'ArrowUp',
      description: t('Shortcut:Previous'),
      handler: () => {
        const data = options.data()
        if (data.length === 0) return
        currentIndex = Math.max(currentIndex - 1, 0)
        const item = data[currentIndex]
        if (item !== undefined && options.onSelect) {
          options.onSelect(item, currentIndex)
        }
      },
    },
    {
      key: 'Enter',
      description: t('Shortcut:EditCurrent'),
      handler: () => {
        const data = options.data()
        if (currentIndex >= 0 && currentIndex < data.length && options.onEdit) {
          const item = data[currentIndex]
          if (item !== undefined) options.onEdit(item, currentIndex)
        }
      },
    },
    {
      key: 'Delete',
      description: t('Shortcut:DeleteCurrent'),
      handler: () => {
        const data = options.data()
        if (currentIndex >= 0 && currentIndex < data.length && options.onDelete) {
          const item = data[currentIndex]
          if (item !== undefined) options.onDelete(item, currentIndex)
        }
      },
    },
  ]

  useKeyboard(shortcuts)

  return {
    currentIndex: () => currentIndex,
    setCurrentIndex: (index: number) => {
      currentIndex = index
    },
    shortcuts,
  }
}

/**
 * 模态框导航 Hook
 * 提供模态框的键盘快捷键
 */
export function useModalNavigation(options: { onClose?: () => void; onSubmit?: () => void }) {
  const shortcuts: KeyboardShortcut[] = []

  if (options.onClose) {
    shortcuts.push({
      key: 'Escape',
      description: t('Shortcut:CloseModal'),
      handler: options.onClose,
    })
  }

  if (options.onSubmit) {
    shortcuts.push({
      key: 'Enter',
      ctrl: true,
      description: t('Shortcut:Submit'),
      handler: options.onSubmit,
    })
  }

  useKeyboard(shortcuts)

  return {
    shortcuts,
  }
}

/**
 * 搜索快捷键 Hook
 * 提供搜索框的快捷键
 */
export function useSearchShortcut(options: { onFocus?: () => void; onClear?: () => void }) {
  const shortcuts: KeyboardShortcut[] = []

  if (options.onFocus) {
    shortcuts.push({
      key: 'f',
      ctrl: true,
      description: t('Shortcut:FocusSearch'),
      handler: (event) => {
        event.preventDefault()
        options.onFocus?.()
      },
    })
  }

  if (options.onClear) {
    shortcuts.push({
      key: 'Escape',
      description: t('Shortcut:ClearSearch'),
      handler: options.onClear,
    })
  }

  useKeyboard(shortcuts)

  return {
    shortcuts,
  }
}

/**
 * 通用操作快捷键 Hook
 * 提供常见的 CRUD 操作快捷键
 */
export function useCrudShortcuts(options: {
  onCreate?: () => void
  onRefresh?: () => void
  onSave?: () => void
}) {
  const shortcuts: KeyboardShortcut[] = []

  if (options.onCreate) {
    shortcuts.push({
      key: 'n',
      ctrl: true,
      description: t('Common:Create'),
      handler: (event) => {
        event.preventDefault()
        options.onCreate?.()
      },
    })
  }

  if (options.onRefresh) {
    shortcuts.push({
      key: 'r',
      ctrl: true,
      description: t('Common:Refresh'),
      handler: (event) => {
        event.preventDefault()
        options.onRefresh?.()
      },
    })
  }

  if (options.onSave) {
    shortcuts.push({
      key: 's',
      ctrl: true,
      description: t('Common:Save'),
      handler: (event) => {
        event.preventDefault()
        options.onSave?.()
      },
    })
  }

  useKeyboard(shortcuts)

  return {
    shortcuts,
  }
}

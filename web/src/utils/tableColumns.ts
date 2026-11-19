/**
 * Data table column helpers
 *
 * Shared utilities for `n-data-table` column definitions across admin views.
 *
 * - `getTableScrollX(columns)` computes a reasonable minimum table width from
 *   the sum of column widths, so wide tables show a horizontal scrollbar
 *   instead of squeezing columns when the viewport is narrower than the
 *   content.
 * - `ACTIONS_COLUMN_WIDTH` provides standard widths for the trailing actions
 *   column keyed by the number of action buttons, keeping the column fixed
 *   regardless of content length.
 */
import { h, type VNodeChild } from 'vue'
import type { DataTableColumn, DataTableColumns } from 'naive-ui'
import { t } from '@/i18n'

/** Standard fixed widths for the actions column, keyed by button count. */
export const ACTIONS_COLUMN_WIDTH = {
  /** Single small button (e.g. edit only) */
  single: 100,
  /** Two small buttons (e.g. edit + delete) */
  double: 160,
  /** Three small buttons (e.g. view + edit + delete) */
  triple: 220,
  /** Four small buttons (e.g. view + edit + delete + more) */
  quad: 280,
} as const

/** Fallback width assumed for a column without an explicit width/minWidth. */
const DEFAULT_COLUMN_WIDTH = 120

/** Lower bound for scroll-x so tiny tables do not collapse into a sliver. */
const MIN_TABLE_WIDTH = 600

export function getTableScrollX<T>(columns: DataTableColumns<T>): number {
  const total = columns.reduce((sum, column) => {
    // Column group: recurse into children
    if ('children' in column && Array.isArray(column.children)) {
      return sum + getTableScrollX(column.children as DataTableColumns<T>)
    }
    if ('width' in column && typeof column.width === 'number') {
      return sum + column.width
    }
    if ('minWidth' in column && typeof column.minWidth === 'number') {
      return sum + column.minWidth
    }
    return sum + DEFAULT_COLUMN_WIDTH
  }, 0)
  return Math.max(total, MIN_TABLE_WIDTH)
}

/**
 * Build the trailing actions column used by list tables.
 *
 * Keeps the column fixed, right-aligned via `.po-cell-actions`, and sized from
 * `ACTIONS_COLUMN_WIDTH` so every list page renders actions consistently.
 */
export function createActionColumn<T>(
  render: (row: T) => VNodeChild,
  options: { width?: number; title?: string } = {},
): DataTableColumn<T> {
  return {
    title: options.title ?? t('Common:Actions'),
    key: 'actions',
    width: options.width ?? ACTIONS_COLUMN_WIDTH.triple,
    fixed: 'right',
    render: (row) => h('div', { class: 'po-cell-actions' }, [render(row)]),
  }
}

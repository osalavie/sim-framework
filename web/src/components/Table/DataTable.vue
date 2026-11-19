<template>
  <div class="po-data-table" v-bind="rootBindings">
    <div
      v-if="resolvedViewMode === 'table'"
      ref="frameRef"
      class="po-data-table__table"
      :class="[
        framed ? ['po-card', 'po-card--flush', 'po-data-table__frame'] : undefined,
        fill ? 'authz-table-card' : undefined,
      ]"
      @mousedown.capture="handleResizeStart"
    >
      <n-data-table
        ref="tableRef"
        v-bind="tableAttrs"
        class="po-data-table__native"
        :columns="normalizedColumns"
        :data="data"
        :loading="loading"
        :row-key="rowKey"
        :pagination="false"
        :bordered="false"
        :bottom-bordered="rowDividers"
        :single-line="!columnDividers"
        :striped="striped"
        :resizable="resizable"
        :flex-height="flexHeight"
        :multiple="multiple ?? (selectable ? true : undefined)"
        :scroll-x="tableScrollX"
      >
        <template v-for="name in forwardedSlotNames" :key="name" #[name]="slotProps">
          <slot :name="name" v-bind="slotProps ?? {}" />
        </template>
      </n-data-table>

      <div v-if="paginationState || (showViewSwitch && hasCard)" class="po-data-table__bottom">
        <SegmentedSwitch
          v-if="showViewSwitch && hasCard"
          v-model="viewModeModel"
          :options="viewModeOptions"
          :aria-label="t('Table:ViewModeLabel')"
        />
        <DataPagination
          v-if="paginationState"
          embedded
          :page="paginationState.page"
          :page-size="paginationState.pageSize"
          :item-count="paginationState.itemCount"
          :page-sizes="pageSizes"
          @update:page="emit('update:page', $event)"
          @update:page-size="emit('update:pageSize', $event)"
        />
      </div>
    </div>

    <div v-else class="po-data-table__cards">
      <slot name="card" />
      <DataPagination
        v-if="cardPagination && paginationState"
        :page="paginationState.page"
        :page-size="paginationState.pageSize"
        :item-count="paginationState.itemCount"
        :page-sizes="pageSizes"
        @update:page="emit('update:page', $event)"
        @update:page-size="emit('update:pageSize', $event)"
      />
    </div>

    <div
      v-if="showViewSwitch && hasCard && resolvedViewMode === 'card'"
      class="po-data-table__switch"
    >
      <SegmentedSwitch
        v-model="viewModeModel"
        :options="viewModeOptions"
        :aria-label="t('Table:ViewModeLabel')"
      />
    </div>
  </div>
</template>

<script setup lang="ts" generic="T extends Record<string, any>">
import { computed, onBeforeUnmount, onMounted, ref, useAttrs, useSlots, watch } from 'vue'
import type { DataTableColumn, DataTableColumns, DataTableInst } from 'naive-ui'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import DataPagination from './DataPagination.vue'
import { t } from '@/i18n'
import { useResponsive } from '@/hooks/useResponsive'
import { TABLE_PAGE_SIZES, type TablePaginationState } from '@/utils/tablePagination'

type ViewMode = 'table' | 'card'

/** 复选列宽度：仅容纳 18px 复选框与左右留白。 */
const SELECTION_COLUMN_WIDTH = 40
const DEFAULT_COLUMN_WIDTH = 120
const MIN_COLUMN_WIDTH = 60

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max)

interface Props {
  columns: DataTableColumns<T>
  data: T[]
  rowKey: (row: T) => string | number
  loading?: boolean
  pagination?: TablePaginationState | false | null
  /** 自动在首列插入复选列（若 columns 已包含 selection 列则不再插入）。 */
  selectable?: boolean
  selectionOptions?: {
    width?: number
    fixed?: 'left' | 'right'
    disabled?: (row: T) => boolean
  }
  /** 复选模式：true 为多选（复选框），false 为单选（单选框）。 */
  multiple?: boolean
  viewMode?: ViewMode
  showViewSwitch?: boolean
  autoViewMode?: boolean
  striped?: boolean
  columnDividers?: boolean
  rowDividers?: boolean
  resizable?: boolean
  flexHeight?: boolean
  scrollX?: number
  /** 使用列表页统一的卡片外框（2px 描边 + 硬投影）。 */
  framed?: boolean
  /** 列表页撑满模式：外框带最小高度并在剩余空间内伸缩。 */
  fill?: boolean
  showPagination?: boolean
  /** 卡片模式下是否同样渲染统一分页栏（卡片视图自带分页时保持 false）。 */
  cardPagination?: boolean
  pageSizes?: number[]
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  pagination: undefined,
  selectable: false,
  selectionOptions: undefined,
  multiple: undefined,
  viewMode: undefined,
  showViewSwitch: false,
  autoViewMode: false,
  striped: true,
  columnDividers: true,
  rowDividers: true,
  resizable: true,
  flexHeight: false,
  scrollX: undefined,
  framed: false,
  fill: false,
  showPagination: true,
  cardPagination: false,
  pageSizes: () => [...TABLE_PAGE_SIZES],
})

const emit = defineEmits<{
  'update:page': [page: number]
  'update:pageSize': [pageSize: number]
  'update:viewMode': [mode: ViewMode]
}>()

const attrs = useAttrs()
const slots = useSlots()
const { isNarrowScreen } = useResponsive()

const tableRef = ref<DataTableInst | null>(null)

const rootBindings = computed(() => ({ class: attrs.class, style: attrs.style }))
const tableAttrs = computed(() => {
  const rest = { ...attrs }
  delete rest.class
  delete rest.style
  return rest
})

const hasCard = computed(() => Boolean(slots.card))
const forwardedSlotNames = computed(() => Object.keys(slots).filter((name) => name !== 'card'))

const autoMode = ref<ViewMode>(props.autoViewMode && isNarrowScreen.value ? 'card' : 'table')

watch(isNarrowScreen, (narrow) => {
  if (props.viewMode === undefined && props.autoViewMode) {
    autoMode.value = narrow ? 'card' : 'table'
  }
})

const viewModeModel = computed<ViewMode>({
  get: () => props.viewMode ?? autoMode.value,
  set: (mode) => {
    if (props.viewMode !== undefined) emit('update:viewMode', mode)
    else autoMode.value = mode
  },
})

const resolvedViewMode = computed<ViewMode>(() => (hasCard.value ? viewModeModel.value : 'table'))

const viewModeOptions = computed(() => [
  { label: t('Table:ViewTable'), value: 'table' as const },
  { label: t('Table:ViewCard'), value: 'card' as const },
])

const frameRef = ref<HTMLElement | null>(null)
const containerWidth = ref(0)
const widthOverrides = ref<Record<string, number>>({})
let frameObserver: ResizeObserver | null = null

const isStructuralColumn = (column: DataTableColumn<T>) =>
  'type' in column && column.type !== undefined

const isFlexibleColumn = (column: DataTableColumn<T>) =>
  !isStructuralColumn(column) && !('fixed' in column && column.fixed)

const columnId = (column: DataTableColumn<T>, index: number): string => {
  if ('key' in column && column.key !== undefined && column.key !== null) return String(column.key)
  if ('type' in column && column.type) return `__${column.type}`
  return `__column_${index}`
}

const columnBaseWidth = (column: DataTableColumn<T>): number => {
  if ('type' in column && column.type === 'selection') return SELECTION_COLUMN_WIDTH
  if ('width' in column && typeof column.width === 'number') return column.width
  if ('minWidth' in column && typeof column.minWidth === 'number') return column.minWidth
  return DEFAULT_COLUMN_WIDTH
}

const columnMinWidth = (column: DataTableColumn<T>) =>
  'minWidth' in column && typeof column.minWidth === 'number' ? column.minWidth : MIN_COLUMN_WIDTH

const columnMaxWidth = (column: DataTableColumn<T>) =>
  'maxWidth' in column && typeof column.maxWidth === 'number'
    ? column.maxWidth
    : Number.POSITIVE_INFINITY

const structuralColumns = computed<DataTableColumns<T>>(() => {
  const result: DataTableColumns<T> = []
  const hasSelection = props.columns.some(
    (column) => 'type' in column && column.type === 'selection',
  )

  if (props.selectable && !hasSelection) {
    result.push({
      type: 'selection',
      width: SELECTION_COLUMN_WIDTH,
      ...(props.selectionOptions ?? {}),
    } as DataTableColumn<T>)
  }

  for (const column of props.columns) {
    if (props.resizable && isFlexibleColumn(column)) {
      result.push({ ...column, resizable: true })
    } else {
      result.push(column)
    }
  }

  return result
})

/**
 * 表格空间足够时按比例补足列宽，使列宽之和等于容器宽度。
 * 否则浏览器会自行拉伸列，Naive 调整器会以拉伸后的实际宽度为起点，首次拖动就会跳变。
 */
const currentWidths = computed<number[]>(() => {
  const columns = structuralColumns.value
  const ids = columns.map(columnId)
  const widths = columns.map(
    (column, index) => widthOverrides.value[ids[index] ?? ''] ?? columnBaseWidth(column),
  )

  if (!props.resizable) return widths

  const total = widths.reduce((sum, width) => sum + width, 0)
  const spare = containerWidth.value - total
  if (spare <= 0) return widths

  const flexible = columns
    .map((column, index) => (isFlexibleColumn(column) ? index : -1))
    .filter((index) => index >= 0)
  const flexibleTotal = flexible.reduce((sum, index) => sum + (widths[index] ?? 0), 0)
  if (flexibleTotal <= 0) return widths

  for (const index of flexible) {
    const width = widths[index] ?? 0
    widths[index] = width + (spare * width) / flexibleTotal
  }
  return widths
})

const normalizedColumns = computed<DataTableColumns<T>>(() => {
  if (!props.resizable) return structuralColumns.value
  return structuralColumns.value.map((column, index) => ({
    ...column,
    width: Math.round(currentWidths.value[index] ?? 0),
  }))
})

const tableScrollX = computed(() => {
  if (props.scrollX !== undefined) return props.scrollX
  const total = Math.round(currentWidths.value.reduce((sum, width) => sum + width, 0))
  return Math.max(containerWidth.value, total)
})

interface ResizeState {
  index: number
  startX: number
  widths: number[]
  ids: string[]
}

let resizeState: ResizeState | null = null

/** 接管 Naive 的列宽调整器：以固定起点计算位移，并让相邻列吸收宽度差。 */
const handleResizeStart = (event: MouseEvent) => {
  if (!props.resizable || event.button !== 0) return
  const handle = (event.target as HTMLElement | null)?.closest('.n-data-table-resize-button')
  if (!handle) return
  const cell = handle.closest('th')
  const row = cell?.parentElement
  if (!cell || !row) return

  const index = Array.from(row.children).indexOf(cell)
  const column = structuralColumns.value[index]
  if (!column || !isFlexibleColumn(column)) return

  event.preventDefault()
  event.stopPropagation()
  resizeState = {
    index,
    startX: event.clientX,
    widths: [...currentWidths.value],
    ids: structuralColumns.value.map(columnId),
  }
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
  window.addEventListener('mousemove', handleResizeMove)
  window.addEventListener('mouseup', handleResizeEnd)
}

const handleResizeMove = (event: MouseEvent) => {
  if (!resizeState) return
  const columns = structuralColumns.value
  const column = columns[resizeState.index]
  if (!column) return

  const startWidth = resizeState.widths[resizeState.index] ?? columnBaseWidth(column)
  const targetWidth = clamp(
    startWidth + (event.clientX - resizeState.startX),
    columnMinWidth(column),
    columnMaxWidth(column),
  )
  const applied = targetWidth - startWidth
  if (applied === 0) return

  const widths = [...resizeState.widths]
  widths[resizeState.index] = targetWidth

  // 先由右侧列吸收宽度差，右侧不足再向左找；总宽度保持不变，列左边缘才不会移动。
  const candidates: number[] = []
  for (let index = resizeState.index + 1; index < columns.length; index += 1) {
    const candidate = columns[index]
    if (candidate && isFlexibleColumn(candidate)) candidates.push(index)
  }
  for (let index = resizeState.index - 1; index >= 0; index -= 1) {
    const candidate = columns[index]
    if (candidate && isFlexibleColumn(candidate)) candidates.push(index)
  }

  let remaining = -applied
  for (const index of candidates) {
    if (remaining === 0) break
    const candidate = columns[index]
    if (!candidate) continue
    const current = widths[index] ?? 0
    if (remaining > 0) {
      const add = Math.min(remaining, columnMaxWidth(candidate) - current)
      if (add > 0) {
        widths[index] = current + add
        remaining -= add
      }
    } else {
      const remove = Math.min(-remaining, current - columnMinWidth(candidate))
      if (remove > 0) {
        widths[index] = current - remove
        remaining += remove
      }
    }
  }

  const next: Record<string, number> = {}
  for (let index = 0; index < widths.length; index += 1) {
    next[resizeState.ids[index] ?? `__column_${index}`] = Math.round(widths[index] ?? 0)
  }
  widthOverrides.value = next
}

const handleResizeEnd = () => {
  resizeState = null
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
  window.removeEventListener('mousemove', handleResizeMove)
  window.removeEventListener('mouseup', handleResizeEnd)
}

onMounted(() => {
  frameObserver = new ResizeObserver((entries) => {
    const width = Math.round(entries[0]?.contentRect.width ?? 0)
    if (width > 0) containerWidth.value = width
  })
  if (frameRef.value) frameObserver.observe(frameRef.value)
})

watch(frameRef, (element, previous) => {
  if (!frameObserver) return
  if (previous) frameObserver.unobserve(previous)
  if (element) {
    frameObserver.observe(element)
    containerWidth.value = Math.round(element.getBoundingClientRect().width)
  }
})

onBeforeUnmount(() => {
  frameObserver?.disconnect()
  handleResizeEnd()
})

const paginationState = computed(() =>
  props.showPagination && props.pagination ? props.pagination : null,
)

defineExpose({ tableRef })
</script>

<style scoped>
.po-data-table {
  position: relative;
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
}

.po-data-table__table {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  gap: 0;
  min-width: 0;
  min-height: 0;
}

.po-data-table__table :deep(.n-data-table) {
  flex: 1 1 auto;
  min-height: 0;
}

/* 复选列：窄列 + 水平垂直居中，同时抵消列表卡给首列的 20px 内边距 */
.po-data-table :deep(.n-data-table-th--selection),
.po-data-table :deep(.n-data-table-td--selection) {
  padding-right: 0 !important;
  padding-left: 0 !important;
  text-align: center;
  vertical-align: middle;
}

.po-data-table :deep(.n-data-table-th--selection .n-checkbox),
.po-data-table :deep(.n-data-table-td--selection .n-checkbox) {
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto;
}

.po-data-table__cards {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
}

.po-data-table__cards > :not(.po-data-pagination) {
  flex: 1 1 auto;
  min-height: 0;
}

.po-data-table__bottom {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  padding: var(--po-density-table-foot-py) 20px;
  border-top: 2px solid var(--po-line);
  background: var(--po-paper);
}

.po-data-table__bottom :deep(.po-data-pagination) {
  padding: 0;
  border-top: 0;
  background: transparent;
}

/* 卡片模式下卡片列表自带分页，切换器贴在列表页脚左侧 */
.po-data-table__switch {
  position: absolute;
  bottom: 8px;
  left: 20px;
  z-index: 3;
}

@media (max-width: 640px) {
  .po-data-table__bottom {
    padding: 12px 16px;
  }

  .po-data-table__switch {
    position: static;
    align-self: flex-start;
    margin: 0 0 4px 4px;
  }
}
</style>

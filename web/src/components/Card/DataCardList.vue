<template>
  <div class="data-card-list" :class="{ 'has-pagination': Boolean(pagination) }">
    <!-- 加载状态 - 骨架屏 -->
    <div
      v-if="loading"
      class="data-card-list__grid"
      :class="gridClass"
      :style="gridStyle"
    >
      <DataCardSkeleton v-for="i in skeletonCount" :key="`skeleton-${i}`" />
    </div>

    <!-- 空状态 -->
    <div v-else-if="!data || data.length === 0" class="data-card-list__empty">
      <n-empty :description="emptyText">
        <template v-if="$slots.empty" #default>
          <slot name="empty" />
        </template>
      </n-empty>
    </div>

    <!-- 卡片列表 -->
    <div v-else class="data-card-list__grid" :class="gridClass" :style="gridStyle">
      <slot name="card" v-for="item in data" :item="item" :key="getItemKey(item)">
        <DataCard
          :title="item[titleKey]"
          :description="item[descriptionKey]"
          :cover="item[coverKey]"
          :tags="item[tagsKey]"
          :meta="item[metaKey]"
          :actions="getActions(item)"
          :clickable="clickable"
          :hoverable="hoverable"
          :selected="isSelected(item)"
          :preview-image="previewImage"
          @click="handleCardClick(item)"
        />
      </slot>
      <div ref="sentinelRef" class="data-card-list__sentinel" aria-hidden="true"></div>
    </div>

    <!-- 分页 -->
    <div v-if="pagination && !loading && data && data.length > 0" class="data-card-list__pagination">
      <n-pagination
        v-model:page="currentPage"
        v-model:page-size="currentPageSize"
        :item-count="pagination.itemCount"
        :page-sizes="pageSizes"
        :show-size-picker="showSizePicker"
        :show-quick-jumper="showQuickJumper"
        @update:page="handlePageChange"
        @update:page-size="handlePageSizeChange"
      />
    </div>
  </div>
</template>

<script setup lang="ts" generic="T extends Record<string, any>">
import { nextTick, onBeforeUnmount, ref, watch, computed } from 'vue'
import DataCard, { type DataCardAction } from './DataCard.vue'
import DataCardSkeleton from './DataCardSkeleton.vue'

interface Pagination {
  page: number
  pageSize: number
  pageCount: number
  itemCount: number
}

interface Props {
  data: T[]
  loading?: boolean
  columns?: 1 | 2 | 3 | 4
  titleKey?: string
  descriptionKey?: string
  coverKey?: string
  tagsKey?: string
  metaKey?: string
  keyField?: string
  clickable?: boolean
  hoverable?: boolean
  previewImage?: boolean
  selectedKeys?: (string | number)[]
  emptyText?: string
  pagination?: Pagination
  pageSizes?: number[]
  showSizePicker?: boolean
  showQuickJumper?: boolean
  actions?: (item: T) => DataCardAction[]
  /** 固定卡宽（px）时按屏幕宽度自动换列，用于黄金比例卡片。 */
  cardWidth?: number
  /** 是否还有下一页数据，配合无限滚动使用。 */
  hasMore?: boolean
  loadingMore?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  columns: 3,
  titleKey: 'title',
  descriptionKey: 'description',
  coverKey: 'cover',
  tagsKey: 'tags',
  metaKey: 'meta',
  keyField: 'id',
  clickable: false,
  hoverable: true,
  previewImage: false,
  emptyText: '暂无数据',
  pageSizes: () => [10, 20, 30, 50],
  showSizePicker: true,
  showQuickJumper: true,
  cardWidth: undefined,
  hasMore: false,
  loadingMore: false,
})

const emit = defineEmits<{
  'card-click': [item: T]
  'page-change': [page: number]
  'page-size-change': [pageSize: number]
  'load-more': []
}>()

const currentPage = ref(props.pagination?.page || 1)
const currentPageSize = ref(props.pagination?.pageSize || 10)

const gridClass = computed(() =>
  props.cardWidth ? 'data-card-list__grid--auto' : `data-card-list__grid--${props.columns}`,
)

const gridStyle = computed<Record<string, string> | undefined>(() =>
  props.cardWidth ? { '--card-width': `${props.cardWidth}px` } : undefined,
)

const sentinelRef = ref<HTMLElement | null>(null)
let sentinelObserver: IntersectionObserver | null = null
let loadMorePending = false
let lastRequestedLength = -1

const requestMore = () => {
  if (!props.hasMore || props.loadingMore || loadMorePending) return
  // 加载失败时数据长度不变，避免观测器反复自动重试
  if (props.data.length === lastRequestedLength) return
  lastRequestedLength = props.data.length
  loadMorePending = true
  emit('load-more')
}

const observeSentinel = () => {
  sentinelObserver?.disconnect()
  if (!sentinelRef.value) return
  sentinelObserver = new IntersectionObserver(
    (entries) => {
      if (entries.some((entry) => entry.isIntersecting)) requestMore()
    },
    { rootMargin: '240px 0px' },
  )
  sentinelObserver.observe(sentinelRef.value)
}

watch(sentinelRef, () => observeSentinel(), { flush: 'post' })
watch(
  () => props.loadingMore,
  (loading) => {
    if (loading) return
    loadMorePending = false
    void nextTick(observeSentinel)
  },
)
watch(
  () => props.hasMore,
  (hasMore) => {
    if (hasMore) void nextTick(observeSentinel)
  },
)
onBeforeUnmount(() => sentinelObserver?.disconnect())

// 计算骨架屏数量
const skeletonCount = computed(() => {
  return props.pagination?.pageSize || 9
})

// 监听分页变化
watch(
  () => props.pagination?.page,
  (newPage) => {
    if (newPage !== undefined) {
      currentPage.value = newPage
    }
  },
)

watch(
  () => props.pagination?.pageSize,
  (newPageSize) => {
    if (newPageSize !== undefined) {
      currentPageSize.value = newPageSize
    }
  },
)

const getItemKey = (item: T): string | number => {
  return item[props.keyField]
}

const isSelected = (item: T): boolean => {
  if (!props.selectedKeys || props.selectedKeys.length === 0) {
    return false
  }
  return props.selectedKeys.includes(getItemKey(item))
}

const getActions = (item: T): DataCardAction[] | undefined => {
  return props.actions ? props.actions(item) : undefined
}

const handleCardClick = (item: T) => {
  if (props.clickable) {
    emit('card-click', item)
  }
}

const handlePageChange = (page: number) => {
  emit('page-change', page)
}

const handlePageSizeChange = (pageSize: number) => {
  emit('page-size-change', pageSize)
}
</script>

<style scoped lang="scss">
.data-card-list {
  width: 100%;
  flex: 1 1 auto;
  min-height: 0;
  display: flex;
  flex-direction: column;

  &__loading {
    display: flex;
    justify-content: center;
    align-items: center;
    flex: 1;
    min-height: 400px;
  }

  &__loading {
    display: flex;
    justify-content: center;
    align-items: center;
    flex: 1;
    min-height: 400px;
  }

  &__empty {
    display: flex;
    justify-content: center;
    align-items: center;
    flex: 1;
    min-height: 400px;
  }

  &__grid {
    display: grid;
    gap: 20px;
    padding: 4px;
    flex: 1;
    min-height: 0;
    min-width: 0;
    overflow-y: auto;
    overflow-x: hidden;

    &--1 {
      grid-template-columns: repeat(1, minmax(0, 1fr));
    }

    &--2 {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    &--3 {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }

    &--4 {
      grid-template-columns: repeat(4, minmax(0, 1fr));
    }

    &--auto {
      justify-content: center;
      grid-template-columns: repeat(
        auto-fill,
        minmax(min(100%, var(--card-width, 360px)), var(--card-width, 360px))
      );
    }

    /* 让网格内容自适应高度 */
    grid-auto-rows: max-content;

    /* 卡片项自身不撑破网格宽度，内容在卡片内部换行 */
    > * {
      min-width: 0;
      max-width: 100%;
    }
  }

  &__sentinel {
    height: 1px;
    grid-column: 1 / -1;
  }

  &__pagination {
    display: flex;
    flex-shrink: 0;
    justify-content: center;
    padding: 10px 16px;
    border-top: 2px solid var(--po-line);
    background: var(--po-paper);
  }
}

/* 平板适配 */
@media (max-width: 1024px) {
  .data-card-list__grid {
    &--3,
    &--4 {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  }
}

/* 移动端适配 */
@media (max-width: 768px) {
  .data-card-list {
    &__grid {
      gap: 12px;

      &--2,
      &--3,
      &--4 {
        grid-template-columns: repeat(1, minmax(0, 1fr));
      }
    }
  }

  /* 仅带分页的卡片列表保留固定页脚；无限滚动卡片模式不预留页脚空间 */
  .data-card-list.has-pagination {
    .data-card-list__grid {
      padding-bottom: 60px;
    }

    .data-card-list__pagination {
      position: fixed;
      right: 0;
      bottom: 0;
      left: 0;
      z-index: 8;
      padding: 6px 12px;
      padding-bottom: calc(6px + env(safe-area-inset-bottom, 0px));

      :deep(.n-pagination) {
        flex-wrap: wrap;
        justify-content: center;
        row-gap: 4px;
      }
    }
  }
}
</style>

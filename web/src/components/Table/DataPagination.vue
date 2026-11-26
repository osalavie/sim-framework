<template>
  <div class="po-data-pagination" :class="{ 'po-data-pagination--embedded': embedded }">
    <span class="po-data-pagination__range">{{ rangeText }}</span>
    <n-pagination
      :page="page"
      :page-size="pageSize"
      :item-count="itemCount"
      :page-sizes="pageSizes"
      show-size-picker
      show-quick-jumper
      @update:page="emit('update:page', $event)"
      @update:page-size="emit('update:pageSize', $event)"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { t } from '@/i18n'
import { TABLE_PAGE_SIZES } from '@/utils/tablePagination'

const props = withDefaults(
  defineProps<{
    page: number
    pageSize: number
    itemCount: number
    pageSizes?: number[]
    /** 嵌入统一底栏时去掉自身边框与内边距。 */
    embedded?: boolean
  }>(),
  { pageSizes: () => [...TABLE_PAGE_SIZES], embedded: false },
)

const emit = defineEmits<{
  'update:page': [page: number]
  'update:pageSize': [pageSize: number]
}>()

const rangeText = computed(() => {
  if (props.itemCount === 0) return t('Table:EmptyRange')
  const start = (props.page - 1) * props.pageSize + 1
  const end = Math.min(props.page * props.pageSize, props.itemCount)
  return t('Table:RangeInfo', { start, end, total: props.itemCount })
})
</script>

<style scoped>
.po-data-pagination {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 12px 20px;
  border-top: 2px solid var(--po-line);
  background: var(--po-paper);
}

.po-data-pagination--embedded {
  flex: 1 1 auto;
  padding: 0;
  border-top: 0;
  background: transparent;
}

.po-data-pagination__range {
  color: var(--po-ink-soft);
  font-size: 12px;
  font-weight: 600;
  white-space: nowrap;
}

@media (max-width: 640px) {
  .po-data-pagination {
    justify-content: center;
    padding: 12px 16px;
  }
}
</style>

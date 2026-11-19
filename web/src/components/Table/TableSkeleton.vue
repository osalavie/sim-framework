<template>
  <div class="table-skeleton">
    <n-space vertical :size="0">
      <!-- 表头骨架 -->
      <div class="table-skeleton__header">
        <n-space :size="16">
          <n-skeleton
            v-for="i in columns"
            :key="`header-${i}`"
            :width="getColumnWidth(i)"
            :height="40"
          />
        </n-space>
      </div>

      <!-- 表格行骨架 -->
      <div v-for="row in rows" :key="`row-${row}`" class="table-skeleton__row">
        <n-space :size="16">
          <n-skeleton
            v-for="col in columns"
            :key="`cell-${row}-${col}`"
            :width="getColumnWidth(col)"
            :height="48"
          />
        </n-space>
      </div>
    </n-space>
  </div>
</template>

<script setup lang="ts">
interface Props {
  rows?: number
  columns?: number
}

withDefaults(defineProps<Props>(), {
  rows: 5,
  columns: 6,
})

// 为不同列生成不同宽度，使其看起来更真实
const getColumnWidth = (index: number): string => {
  const widths = ['15%', '20%', '15%', '18%', '12%', '20%']
  return widths[(index - 1) % widths.length] ?? '16%'
}
</script>

<style scoped lang="scss">
.table-skeleton {
  width: 100%;
  padding: 16px;
  background-color: var(--n-color);
  border-radius: 4px;

  &__header {
    padding: 12px 0;
    border-bottom: 1px solid var(--n-border-color);
    margin-bottom: 8px;
  }

  &__row {
    padding: 12px 0;
    border-bottom: 1px solid var(--n-divider-color);

    &:last-child {
      border-bottom: none;
    }
  }
}
</style>

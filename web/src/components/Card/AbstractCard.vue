<template>
  <component
    :is="cardComponent"
    :to="to"
    class="card-container"
    :class="cardClass"
    :style="backgroundStyle">
    <div class="img-wrapper" v-if="coverData && coverData.mode !== 'background'" :style="coverStyle">
      <ManagedContentImage
        :src="coverData.coverUrl"
        :alt="coverData.description || ''"
        object-fit="cover"
      />
    </div>
    <div class="info-wrapper" :class="{ 'with-overlay': coverData?.mode === 'background' }">
      <div class="title-wrapper">
        <n-tooltip trigger="hover" :disabled="tooltipDisabled">
          <template #trigger>
            <h3 ref="titleEl" class="title-text" @mouseenter="checkTooltip">
              {{ title }}
            </h3>
          </template>
          {{ title }}
        </n-tooltip>
      </div>
      <Divider v-if="title && divider"></Divider>
      <div class="content-wrapper">
        <slot>
        </slot>
      </div>
      <div class="action-wrapper">
        <slot name="action">
        </slot>
      </div>
    </div>
  </component>
</template>
<script setup lang="ts">
import { computed } from 'vue'
import ManagedContentImage from '@/components/ManagedContentImage.vue'
import type { CardCover } from '@/types/cardCover'
import type { RouteLocationRaw } from 'vue-router'
import { useTruncatedTooltip } from '@/hooks'

const props = withDefaults(defineProps<{
  title?: string
  divider?: boolean
  cover?: string | CardCover
  coverSize?: string
  to?: RouteLocationRaw
}>(), {
  coverSize: '40%'
})

const { targetEl: titleEl, disabled: tooltipDisabled, check: checkTooltip } = useTruncatedTooltip()

// 根据是否有 to 属性决定使用 router-link 还是 div
const cardComponent = computed(() => props.to ? 'router-link' : 'div')

// 解析 cover 属性，默认 mode 为 undefined（垂直布局）
const coverData = computed(() => {
  if (!props.cover) return null

  if (typeof props.cover === 'string') {
    return {
      coverUrl: props.cover,
      mode: undefined,
      description: ''
    }
  }

  return props.cover
})

// 根据 mode 动态调整样式
const cardClass = computed(() => {
  const classes: Record<string, boolean> = {}
  const mode = coverData.value?.mode

  if (mode === 'background') {
    classes['layout-background'] = true
  } else if (mode === 'left') {
    classes['layout-horizontal'] = true
  } else if (mode === 'right') {
    classes['layout-horizontal'] = true
    classes['cover-right'] = true
  } else {
    // 默认垂直布局（封面在顶部）
    classes['layout-vertical'] = true
  }

  return classes
})

const coverStyle = computed(() => {
  const mode = coverData.value?.mode

  if (mode === 'left' || mode === 'right') {
    // 水平布局：coverSize 控制宽度，高度 100%
    return {
      flex: `0 0 ${props.coverSize}`,
      height: '100%'
    }
  }
  // 垂直布局：coverSize 控制高度，宽度 100%
  return {
    width: '100%',
    height: props.coverSize
  }
})

const backgroundStyle = computed(() => {
  if (coverData.value?.mode === 'background') {
    return {
      '--bg-image': `url(${coverData.value.coverUrl})`
    }
  }
  return {}
})
</script>
<style scoped lang="scss">
.card-container {
  width: 100%;
  display: flex;
  border: 1px solid var(--theme-border);
  border-radius: 10px;
  overflow: hidden;
  transition: all 0.3s ease;
  position: relative;
  text-decoration: none;
  color: inherit;
  cursor: pointer;

  &:hover {
    box-shadow: 4px 4px 0 var(--po-line, #d9e2f1);
    transform: translate(-2px, -2px);
  }

  // 垂直布局（默认）
  &.layout-vertical {
    flex-direction: column;

    .img-wrapper {
      width: 100%;
      flex-shrink: 0;

      :deep(.n-image) {
        width: 100%;
        height: 100%;
        display: block;
      }

      :deep(img) {
        width: 100%;
        height: 100%;
        object-fit: cover;
        display: block;
      }
    }
  }

  // 水平布局
  &.layout-horizontal {
    flex-direction: row;

    .img-wrapper {
      :deep(.n-image) {
        width: 100%;
        height: 100%;
        display: block;
      }

      :deep(img) {
        width: 100%;
        height: 100%;
        object-fit: cover;
        display: block;
      }
    }

    // 封面在右侧
    &.cover-right {
      flex-direction: row-reverse;
    }
  }

  // 背景布局
  &.layout-background {
    &::before {
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background-image: var(--bg-image);
      background-size: cover;
      background-position: center;
      z-index: 0;
    }

    &::after {
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.4);
      z-index: 1;
    }

    .info-wrapper.with-overlay {
      position: relative;
      z-index: 2;
      color: white;

      .title-text {
        color: white;
        text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
      }
    }
  }

  .info-wrapper {
    display: flex;
    flex: 1 1 auto;
    flex-direction: column;
    gap: 0px;
    padding: 12px;

    .title-wrapper {
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-weight: bold;
      font: 1.2em;
    }

    .content-wrapper {
      width: 100%;
      display: flex;
      flex-direction: column;
      gap: 4px;
      flex: 1;
    }

    .action-wrapper {
      display: flex;
      align-items: center;
      justify-content: right;
      gap: 8px;
    }
  }
}
</style>

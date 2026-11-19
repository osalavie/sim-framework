<template>
  <n-card
    class="data-card"
    :class="{ 'data-card--clickable': clickable, 'data-card--selected': selected }"
    :hoverable="hoverable"
    @click="handleClick"
  >
    <!-- 封面图片 -->
    <div v-if="cover" class="data-card__cover">
      <ManagedContentImage
        :src="cover"
        :alt="title"
        object-fit="cover"
        :preview-disabled="!previewImage"
        class="data-card__cover-image"
      />
    </div>

    <!-- 卡片内容 -->
    <div class="data-card__content">
      <!-- 标题 -->
      <n-tooltip v-if="title" trigger="hover" :disabled="titleDisabled">
        <template #trigger>
          <div
            ref="titleEl"
            class="data-card__title"
            @mouseenter="checkTitle"
          >
            {{ title }}
          </div>
        </template>
        {{ title }}
      </n-tooltip>

      <!-- 描述 -->
      <n-tooltip v-if="description" trigger="hover" :disabled="descDisabled">
        <template #trigger>
          <div
            ref="descEl"
            class="data-card__description"
            @mouseenter="checkDesc"
          >
            {{ description }}
          </div>
        </template>
        {{ description }}
      </n-tooltip>

      <!-- 标签区域 -->
      <div v-if="tags && tags.length > 0" class="data-card__tags">
        <n-space :size="4">
          <n-tag
            v-for="tag in tags"
            :key="tag.value"
            :type="tag.type || 'default'"
            size="small"
          >
            {{ tag.label }}
          </n-tag>
        </n-space>
      </div>

      <!-- 元数据 -->
      <div v-if="meta && meta.length > 0" class="data-card__meta">
        <n-space :size="12">
          <span
            v-for="item in meta"
            :key="item.key"
            class="data-card__meta-item"
          >
            <n-icon v-if="item.icon" :component="item.icon" class="data-card__meta-icon" />
            <span>{{ item.label }}: {{ item.value }}</span>
          </span>
        </n-space>
      </div>

      <!-- 自定义插槽 -->
      <div v-if="$slots.default" class="data-card__extra">
        <slot />
      </div>
    </div>

    <!-- 操作按钮 -->
    <div v-if="actions && actions.length > 0" class="data-card__actions">
      <n-space :size="8">
        <n-button
          v-for="action in actions"
          :key="action.key"
          :type="action.type || 'default'"
          :size="action.size || 'small'"
          :disabled="action.disabled"
          @click.stop="action.onClick"
        >
          <template v-if="action.icon" #icon>
            <n-icon :component="action.icon" />
          </template>
          {{ action.label }}
        </n-button>
      </n-space>
    </div>
  </n-card>
</template>

<script setup lang="ts">
import type { Component } from 'vue'
import ManagedContentImage from '@/components/ManagedContentImage.vue'
import { useTruncatedTooltip } from '@/hooks'

export interface DataCardTag {
  label: string
  value: string | number
  type?: 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error'
}

export interface DataCardMeta {
  key: string
  label: string
  value: string | number
  icon?: Component
}

export interface DataCardAction {
  key: string
  label: string
  type?: 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error'
  size?: 'tiny' | 'small' | 'medium' | 'large'
  icon?: Component
  disabled?: boolean
  onClick: () => void
}

interface Props {
  title?: string
  description?: string
  cover?: string
  tags?: DataCardTag[]
  meta?: DataCardMeta[]
  actions?: DataCardAction[]
  clickable?: boolean
  hoverable?: boolean
  selected?: boolean
  previewImage?: boolean
}

withDefaults(defineProps<Props>(), {
  clickable: false,
  hoverable: true,
  selected: false,
  previewImage: false,
})

const emit = defineEmits<{
  click: []
}>()

const {
  targetEl: titleEl,
  disabled: titleDisabled,
  check: checkTitle,
} = useTruncatedTooltip()
const {
  targetEl: descEl,
  disabled: descDisabled,
  check: checkDesc,
} = useTruncatedTooltip()

const handleClick = () => {
  emit('click')
}
</script>

<style scoped lang="scss">
.data-card {
  min-width: 0;
  max-width: 100%;
  transition: all 0.3s ease;
  user-select: none;
  -webkit-tap-highlight-color: transparent;

  &--clickable {
    cursor: pointer;

    &:active {
      transform: scale(0.98);
      opacity: 0.9;
    }

    @media (hover: hover) {
      &:hover {
        transform: translate(-2px, -2px);
        box-shadow: 4px 4px 0 var(--po-line, #d9e2f1);
      }
    }
  }

  &--selected {
    border-color: var(--po-accent, #2f6bff);
    box-shadow: 3px 3px 0 var(--po-accent-soft, #e7f1ff);
  }

  &__cover {
    margin: -20px -20px 16px -20px;
    overflow: hidden;
    border-radius: 3px 3px 0 0;

    &-image {
      width: 100%;
      height: 200px;
      object-fit: cover;
    }
  }

  &__content {
    display: flex;
    flex-direction: column;
    gap: 12px;
    min-width: 0;
  }

  &__title {
    font-size: 18px;
    font-weight: 600;
    line-height: 1.4;
    color: var(--n-text-color);
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow-wrap: anywhere;
    word-break: break-word;
  }

  &__description {
    font-size: 14px;
    line-height: 1.6;
    color: var(--n-text-color-2);
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    display: -webkit-box;
    -webkit-line-clamp: 3;
    line-clamp: 3;
    -webkit-box-orient: vertical;
    overflow-wrap: anywhere;
    word-break: break-word;
  }

  &__tags {
    display: flex;
    flex-wrap: wrap;
    min-width: 0;
  }

  &__meta {
    font-size: 13px;
    color: var(--n-text-color-3);
    min-width: 0;
    width: 100%;

    :deep(.n-space) {
      flex-wrap: wrap;
    }

    &-item {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      min-width: 0;
      max-width: 100%;
      overflow-wrap: anywhere;
      word-break: break-word;

      > span {
        min-width: 0;
        overflow-wrap: anywhere;
        word-break: break-word;
      }
    }

    &-icon {
      font-size: 14px;
      flex-shrink: 0;
    }
  }

  &__extra {
    margin-top: 4px;
    min-width: 0;
  }

  &__actions {
    margin-top: 16px;
    padding-top: 16px;
    border-top: 1px solid var(--n-border-color);
    min-width: 0;

    :deep(.n-space) {
      flex-wrap: wrap;
    }

    :deep(.n-button) {
      min-height: 36px;
      min-width: 60px;
      touch-action: manipulation;
    }
  }
}

/* 移动端适配 */
@media (max-width: 768px) {
  .data-card {
    border-radius: 12px;

    &__cover {
      margin: -16px -16px 12px -16px;
      border-radius: 12px 12px 0 0;

      &-image {
        height: 160px;
      }
    }

    &__title {
      font-size: 16px;
    }

    &__description {
      font-size: 13px;
    }

    &__actions {
      margin-top: 12px;
      padding-top: 12px;

      :deep(.n-space) {
        flex-wrap: wrap;
        gap: 8px !important;
      }

      :deep(.n-button) {
        flex: 1;
        min-width: 80px;
        min-height: 44px;
        font-size: 14px;
      }
    }
  }
}
</style>

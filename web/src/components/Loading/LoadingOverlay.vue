<template>
  <transition name="loading-fade">
    <div v-if="show" class="loading-overlay" :class="{ 'loading-overlay--absolute': absolute }">
      <div class="loading-overlay__content">
        <n-spin :size="size" :stroke="stroke">
          <template v-if="tip" #description>
            <span class="loading-overlay__tip">{{ tip }}</span>
          </template>
        </n-spin>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
interface Props {
  show: boolean
  tip?: string
  size?: 'small' | 'medium' | 'large'
  stroke?: string
  absolute?: boolean
}

withDefaults(defineProps<Props>(), {
  show: false,
  size: 'medium',
  absolute: false,
})
</script>

<style scoped lang="scss">
.loading-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: var(--po-bg, #fff9ee);
  z-index: 9999;

  &--absolute {
    position: absolute;
  }

  &__content {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 16px;
    padding: 22px 30px;
    border: 2px solid var(--po-line, #d9e2f1);
    border-radius: 18px;
    background: var(--po-paper, #ffffff);
    box-shadow: var(--po-shadow-card, 3px 3px 0 #d9e2f1);
  }

  &__tip {
    font-size: 14px;
    color: var(--po-ink-soft, #4f6282);
    margin-top: 4px;
  }
}

/* 淡入淡出动画 */
.loading-fade-enter-active,
.loading-fade-leave-active {
  transition: opacity 0.3s ease;
}

.loading-fade-enter-from,
.loading-fade-leave-to {
  opacity: 0;
}
</style>

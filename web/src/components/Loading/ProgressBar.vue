<template>
  <transition name="progress-fade">
    <div v-if="show" class="progress-bar" :style="{ width: `${progress}%` }">
      <div class="progress-bar__glow"></div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'

interface Props {
  show: boolean
  duration?: number
}

const props = withDefaults(defineProps<Props>(), {
  show: false,
  duration: 3000,
})

const progress = ref(0)
let timer: number | null = null

watch(
  () => props.show,
  (newShow) => {
    if (newShow) {
      progress.value = 0
      startProgress()
    } else {
      completeProgress()
    }
  },
)

const startProgress = () => {
  if (timer) {
    clearInterval(timer)
  }

  const increment = 100 / (props.duration / 50)
  timer = window.setInterval(() => {
    if (progress.value < 90) {
      progress.value += increment
    } else {
      if (timer) {
        clearInterval(timer)
      }
    }
  }, 50)
}

const completeProgress = () => {
  if (timer) {
    clearInterval(timer)
  }
  progress.value = 100

  setTimeout(() => {
    progress.value = 0
  }, 300)
}
</script>

<style scoped lang="scss">
.progress-bar {
  position: fixed;
  top: 0;
  left: 0;
  height: 3px;
  background: var(--po-accent, #2f6bff);
  border-radius: 0 999px 999px 0;
  transition: width 0.2s ease;
  z-index: 10000;

  &__glow {
    position: absolute;
    right: 0;
    top: 0;
    width: 100px;
    height: 100%;
    background: var(--po-coral, #ff7a6b);
    opacity: 0.55;
    animation: glow 1s ease-in-out infinite;
  }
}

@keyframes glow {
  0%,
  100% {
    opacity: 0.5;
  }
  50% {
    opacity: 1;
  }
}

.progress-fade-enter-active,
.progress-fade-leave-active {
  transition: opacity 0.3s ease;
}

.progress-fade-enter-from,
.progress-fade-leave-to {
  opacity: 0;
}
</style>

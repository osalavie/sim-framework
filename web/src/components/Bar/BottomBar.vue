<template>
  <nav class="bottom-bar-container" :class="{ 'dark': isDark }">
    <div ref="appBarRef" class="app-bar">
      <!-- 中间图标区域（绝对定位居中） -->
      <div ref="appWrapperRef" class="app-wrapper">
        <router-link :to="menu.link" v-for="(menu) in menus" :key="menu.name" :ref="(el) => setItemRef(el)">
          <div class="app-icon">
            <Icon :name="menu.icon" :size="30"></Icon>
          </div>
        </router-link>
      </div>

      <!-- 右侧文本区域 -->
      <div ref="barRightRef" class="bar-right-area">
        <div class="bar-right-content">
          <slot name="bar-right-top"></slot>
          <slot name="bar-right-bottom"></slot>
        </div>
      </div>
    </div>
  </nav>
</template>
<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick, computed, watch, type ComponentPublicInstance } from 'vue'
import Icon from '../Icon/Icon.vue'
import { useThemeStore } from '@/stores/theme'

// 定义 app 列表项的类型
export interface AppItem {
  name: string
  icon: string
  link: string
}

// 定义 props，接收父容器传来的 app 列表数据
const props = withDefaults(defineProps<{
  apps?: AppItem[]
}>(), {
  apps: () => [
  ]
})

const themeStore = useThemeStore()
const isDark = computed(() => themeStore.isDark)
const menus = computed(() => props.apps)

const appBarRef = ref<HTMLElement | null>(null)
const appWrapperRef = ref<HTMLElement | null>(null)
const appRefs = ref<HTMLElement[]>([]) // 使用 ref 数组来存储
const barRightRef = ref<HTMLElement | null>(null)
const mouseX = ref<number | null>(null)

// 监听主题变化，重置图标状态
watch(isDark, () => {
  // 重置所有图标的 transform
  appRefs.value.forEach(app => {
    const icon = app.querySelector('.app-icon') as HTMLElement
    if (icon && icon.style.transform && icon.style.transform !== 'translate(0px, 0px) scale(1)') {
      icon.style.transform = 'translate(0, 0) scale(1)'
    }
  })
  // 重置 bar
  if (appBarRef.value && appBarRef.value.style.transform) {
    appBarRef.value.style.transform = 'scale(1)'
  }
})

const checkOverflow = () => {
  if (!appBarRef.value || !appWrapperRef.value || !appRefs.value.length) return

  const barWidth = appBarRef.value.clientWidth
  const barPaddingLeft = parseFloat(getComputedStyle(appBarRef.value).paddingLeft) || 0
  const barPaddingRight = parseFloat(getComputedStyle(appBarRef.value).paddingRight) || 0

  // 计算实际可用宽度（不包括 padding）
  const barContentWidth = barWidth - barPaddingLeft - barPaddingRight

  // 获取右侧区域宽度
  const barRightWidth = barRightRef.value ? barRightRef.value.offsetWidth : 0

  // 检查是否需要隐藏右侧区域
  // 如果图标区域宽度的一半 + 右侧区域宽度 > 可用宽度的一半，则隐藏右侧区域
  const appWrapperWidth = appWrapperRef.value.offsetWidth
  const halfContentWidth = barContentWidth / 2
  const halfWrapperWidth = appWrapperWidth / 2

  if (barRightRef.value) {
    if (halfWrapperWidth + barRightWidth + 20 > halfContentWidth) {
      barRightRef.value.style.opacity = '0'
      barRightRef.value.style.pointerEvents = 'none'
    } else {
      barRightRef.value.style.opacity = '1'
      barRightRef.value.style.pointerEvents = 'auto'
    }
  }

  if (appRefs.value[0]) {
    const appIconWidth = appRefs.value[0].clientWidth
    const appIconGap = 8 // 固定间距

    // 计算可以显示的图标数量
    // 考虑图标宽度 + 间距，确保不会闪缩
    const totalIconsWidth = (appIconWidth + appIconGap) * appRefs.value.length - appIconGap
    const availableWidth = barContentWidth - (barRightWidth > 0 && barRightRef.value?.style.opacity !== '0' ? barRightWidth + 20 : 0)

    let capacity = appRefs.value.length
    if (totalIconsWidth > availableWidth) {
      capacity = Math.floor(availableWidth / (appIconWidth + appIconGap))
    }

    const visibleApps = appRefs.value.slice(0, Math.max(1, capacity))
    appRefs.value.forEach(app =>
      visibleApps.includes(app)
        ? app.style.display = 'flex'
        : app.style.display = 'none'
    )
  }
}

onMounted(() => {
  nextTick(() => {
    checkOverflow()
    window.addEventListener('resize', checkOverflow)

    // 添加鼠标移动监听
    if (appWrapperRef.value) {
      appWrapperRef.value.addEventListener('mousemove', handleMouseMove)
      appWrapperRef.value.addEventListener('mouseleave', handleMouseLeave)
    }
  })
})

onUnmounted(() => {
  window.removeEventListener('resize', checkOverflow)

  // 移除鼠标监听
  if (appWrapperRef.value) {
    appWrapperRef.value.removeEventListener('mousemove', handleMouseMove)
    appWrapperRef.value.removeEventListener('mouseleave', handleMouseLeave)
  }
})

const setItemRef = (el: Element | ComponentPublicInstance | null) => {
  if (!el) return
  const element: HTMLElement = (el as ComponentPublicInstance).$el || el
  // 避免重复添加
  if (!appRefs.value.includes(element)) {
    appRefs.value.push(element);
  }
}

// macOS Dock 磁性放大效果
const handleMouseMove = (event: MouseEvent) => {
  if (!appWrapperRef.value) return

  const wrapperRect = appWrapperRef.value.getBoundingClientRect()
  mouseX.value = event.clientX - wrapperRect.left

  // 计算所有图标的缩放比例
  const scales: number[] = []
  appRefs.value.forEach((app) => {
    if (app.style.display === 'none') {
      scales.push(1)
      return
    }

    const appRect = app.getBoundingClientRect()
    const appCenterX = appRect.left + appRect.width / 2 - wrapperRect.left
    const distance = Math.abs(mouseX.value! - appCenterX)

    // 计算缩放比例，使用类似正弦曲线的效果
    const maxDistance = 120 // 影响范围
    const maxScale = 1.6 // 最大放大倍数
    const minScale = 1 // 最小倍数

    let scale = minScale
    if (distance < maxDistance) {
      // 使用余弦函数创建平滑的放大效果
      const ratio = (maxDistance - distance) / maxDistance
      scale = minScale + (maxScale - minScale) * Math.pow(ratio, 0.7)
    }
    scales.push(scale)
  })

  // 应用变换，包括水平偏移以避免重叠
  const iconSize = 40 // 图标基础尺寸
  const baseGap = 8 // 基础间距

  // 找到最大缩放的图标索引（hover 中心）
  const maxScaleIndex = scales.indexOf(Math.max(...scales))

  appRefs.value.forEach((app, index) => {
    if (app.style.display === 'none') return

    const scale = scales[index] || 1

    // 计算垂直偏移
    const translateY = -(scale - 1) * 15

    // 计算水平偏移 - 累加从中心到当前位置之间所有图标的额外宽度
    let translateX = 0

    if (index < maxScaleIndex) {
      // 左侧图标：累加它右边到中心之间所有图标的额外宽度（向左推）
      for (let i = index + 1; i <= maxScaleIndex; i++) {
        if (appRefs.value[i]?.style.display !== 'none') {
          const iScale = scales[i] || 1
          const extraWidth = iconSize * (iScale - 1)
          const dynamicGap = (iScale - 1) * baseGap
          translateX -= extraWidth / 2 + dynamicGap
        }
      }
    } else if (index > maxScaleIndex) {
      // 右侧图标：累加它左边到中心之间所有图标的额外宽度（向右推）
      for (let i = maxScaleIndex; i < index; i++) {
        if (appRefs.value[i]?.style.display !== 'none') {
          const iScale = scales[i] || 1
          const extraWidth = iconSize * (iScale - 1)
          const dynamicGap = (iScale - 1) * baseGap
          translateX += extraWidth / 2 + dynamicGap
        }
      }
    }
    // hover 中心图标 (index === maxScaleIndex) 不做水平偏移

    const icon = app.querySelector('.app-icon') as HTMLElement
    if (icon) {
      icon.style.transform = `translate(${translateX}px, ${translateY}px) scale(${scale})`
    }
  })

  // Dock 栏整体略微放大
  if (appBarRef.value) {
    appBarRef.value.style.transform = 'scale(1.02)'
  }
}

const handleMouseLeave = () => {
  mouseX.value = null

  // 重置所有图标
  appRefs.value.forEach(app => {
    const icon = app.querySelector('.app-icon') as HTMLElement
    if (icon) {
      icon.style.transform = 'translate(0, 0) scale(1)'
    }
  })

  // 重置 Dock 栏
  if (appBarRef.value) {
    appBarRef.value.style.transform = 'scale(1)'
  }
}

</script>
<style lang="scss" scoped>
@use '@/styles/glassmorphism.scss' as *;

$btm-height: 56px;
$btm-width: 80%;
$app-icon-size: 40px;

.bottom-bar-container {
  width: 100%;
  height: $btm-height;
  border: none;
  background: transparent;
  position: relative;
  z-index: 100;
}

.app-bar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  width: $btm-width;
  min-height: $btm-height;
  margin: 0 auto;
  padding: 0 calc($btm-height / 2);
  border-radius: calc($btm-height / 2);
  overflow: visible;
  position: relative; // 使右侧固定区域可绝对定位

  // 应用主题感知的液态玻璃效果
  @include glass-effect-themed($blur: 20px, $light-bg-opacity: 0.7, $dark-bg-opacity: 0.5, $border-opacity: 0.2);

  // 添加微妙的渐变增强玻璃效果
  background: linear-gradient(135deg,
      rgba(255, 255, 255, 0.1) 0%,
      rgba(255, 255, 255, 0.05) 100%), rgba(0, 0, 0, 0.35);

  // 所有过渡动画
  transition: transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1),
              background 0.2s cubic-bezier(0.34, 1.56, 0.64, 1),
              border 0.2s cubic-bezier(0.34, 1.56, 0.64, 1),
              box-shadow 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);

  .app-wrapper {
    position: absolute;
    left: 50%;
    transform: translateX(-50%);
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    height: $btm-height;
    z-index: 2;

    a {
      text-decoration: none;
      color: inherit;
    }

    .app-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: $app-icon-size;
      height: $app-icon-size;
      border-radius: calc($app-icon-size / 4);
      cursor: pointer;
      font-size: 24px;
      position: relative;
      transform-origin: bottom center;

      // 图标颜色根据主题变化
      color: #333;

      // 图标也应用主题感知的玻璃效果（无边框版本）
      @include glass-effect-themed($blur: 10px, $light-bg-opacity: 0.8, $dark-bg-opacity: 0.2);

      // 所有过渡动画（包括 transform）
      transition: transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1),
                  background 0.2s cubic-bezier(0.34, 1.56, 0.64, 1),
                  box-shadow 0.2s cubic-bezier(0.34, 1.56, 0.64, 1),
                  color 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);

      &:active {
        transform: translateY(-5px) scale(0.95) !important;
        transition: all 0.1s ease;
      }
    }
  }

  .bar-right-area {
    position: absolute;
    right: calc($btm-height / 2);
    height: $btm-height;
    display: flex;
    align-items: center;

    .bar-right-content {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      justify-content: center;
      gap: 5px;
      transition: color 0.3s ease;

      // 文字颜色根据主题变化
      color: rgba(255, 255, 255, 0.7);
    }
  }
}

// 深色主题下的颜色调整
.dark {
  .app-bar {
    .app-icon {
      color: rgba(255, 255, 255, 0.7);
    }

    .bar-right-content {
      color: rgba(255, 255, 255, 0.6);
    }
  }
}
</style>

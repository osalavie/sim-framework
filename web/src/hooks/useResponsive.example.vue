<template>
  <div class="responsive-example">
    <n-card title="响应式屏幕检测示例">
      <n-space vertical :size="24">
        <!-- 当前状态显示 -->
        <n-descriptions bordered :column="1">
          <n-descriptions-item label="屏幕宽度">
            {{ screenWidth }}px
          </n-descriptions-item>
          <n-descriptions-item label="屏幕类型">
            <n-tag :type="getScreenTypeColor(screenSize)">
              {{ getScreenTypeLabel(screenSize) }}
            </n-tag>
          </n-descriptions-item>
          <n-descriptions-item label="是否窄屏">
            <n-tag :type="isNarrowScreen ? 'warning' : 'success'">
              {{ isNarrowScreen ? '是 (≤768px)' : '否 (>768px)' }}
            </n-tag>
          </n-descriptions-item>
        </n-descriptions>

        <!-- 各断点状态 -->
        <n-card title="断点状态" size="small">
          <n-space>
            <n-tag :type="isMobile ? 'success' : 'default'">
              移动端 (≤480px): {{ isMobile ? '✓' : '✗' }}
            </n-tag>
            <n-tag :type="isTablet ? 'success' : 'default'">
              平板端 (481-768px): {{ isTablet ? '✓' : '✗' }}
            </n-tag>
            <n-tag :type="isDesktop ? 'success' : 'default'">
              桌面端 (769-1280px): {{ isDesktop ? '✓' : '✗' }}
            </n-tag>
            <n-tag :type="isWide ? 'success' : 'default'">
              宽屏 (>1280px): {{ isWide ? '✓' : '✗' }}
            </n-tag>
          </n-space>
        </n-card>

        <!-- 响应式布局示例 -->
        <n-card title="响应式布局示例" size="small">
          <div class="layout-demo" :class="`layout-${screenSize}`">
            <div class="demo-box" v-for="i in boxCount" :key="i">
              Box {{ i }}
            </div>
          </div>
        </n-card>

        <!-- 条件渲染示例 -->
        <n-card title="条件渲染示例" size="small">
          <n-alert v-if="isMobile" type="info">
            当前是移动端视图 (≤480px)
          </n-alert>
          <n-alert v-else-if="isTablet" type="success">
            当前是平板端视图 (481-768px)
          </n-alert>
          <n-alert v-else-if="isDesktop" type="warning">
            当前是桌面端视图 (769-1280px)
          </n-alert>
          <n-alert v-else type="error">
            当前是宽屏视图 (>1280px)
          </n-alert>
        </n-card>

        <!-- 自定义媒体查询示例 -->
        <n-card title="自定义媒体查询示例" size="small">
          <n-space vertical>
            <div>
              <n-tag :type="isLargeScreen ? 'success' : 'default'">
                超大屏幕 (≥1440px): {{ isLargeScreen ? '是' : '否' }}
              </n-tag>
            </div>
            <div>
              <n-tag :type="isPortrait ? 'info' : 'warning'">
                屏幕方向: {{ isPortrait ? '竖屏' : '横屏' }}
              </n-tag>
            </div>
          </n-space>
        </n-card>

        <!-- 提示信息 -->
        <n-alert type="info" title="提示">
          调整浏览器窗口大小，观察上述状态的实时变化
        </n-alert>
      </n-space>
    </n-card>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useResponsive, useMediaQuery, type ScreenSize } from './useResponsive'

// 使用响应式 Hook
const { isNarrowScreen, isMobile, isTablet, isDesktop, isWide, screenSize, screenWidth } = useResponsive()

// 自定义媒体查询
const isLargeScreen = useMediaQuery('(min-width: 1440px)')
const isPortrait = useMediaQuery('(orientation: portrait)')

// 根据屏幕尺寸计算盒子数量
const boxCount = computed(() => {
  if (isMobile.value) return 1
  if (isTablet.value) return 2
  if (isDesktop.value) return 3
  return 4
})

// 获取屏幕类型标签
const getScreenTypeLabel = (size: ScreenSize) => {
  const labels = {
    mobile: '移动端',
    tablet: '平板端',
    desktop: '桌面端',
    wide: '宽屏',
  }
  return labels[size]
}

// 获取屏幕类型颜色
const getScreenTypeColor = (size: ScreenSize) => {
  const colors = {
    mobile: 'error',
    tablet: 'warning',
    desktop: 'success',
    wide: 'info',
  }
  return colors[size] as 'error' | 'warning' | 'success' | 'info'
}
</script>

<style scoped lang="scss">
.responsive-example {
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
}

.layout-demo {
  display: grid;
  gap: 16px;
  padding: 16px;
  background: var(--n-color-target);
  border-radius: 8px;

  &.layout-mobile {
    grid-template-columns: repeat(1, 1fr);
  }

  &.layout-tablet {
    grid-template-columns: repeat(2, 1fr);
  }

  &.layout-desktop {
    grid-template-columns: repeat(3, 1fr);
  }

  &.layout-wide {
    grid-template-columns: repeat(4, 1fr);
  }

  .demo-box {
    padding: 24px;
    background: white;
    border-radius: 4px;
    text-align: center;
    font-weight: 600;
    color: var(--n-color-target);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  }
}

/* 移动端适配 */
@media (max-width: 768px) {
  .responsive-example {
    padding: 16px;
  }
}
</style>

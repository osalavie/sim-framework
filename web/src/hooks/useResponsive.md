# useResponsive - 响应式屏幕检测 Hook

全局共享的响应式屏幕尺寸检测 Hook，用于判断当前设备类型和屏幕宽度。

## 特性

- ✅ 全局单例模式，所有组件共享同一个状态
- ✅ 自动监听窗口大小变化（带防抖优化）
- ✅ 支持多种断点判断
- ✅ TypeScript 类型支持
- ✅ 性能优化，避免重复监听

## 断点配置

```typescript
export const BREAKPOINTS = {
  mobile: 480,    // 移动端
  tablet: 768,    // 平板端（窄屏）
  desktop: 1024,  // 桌面端
  wide: 1280,     // 宽屏
}
```

## 基础用法

### 1. 判断是否为窄屏

```vue
<template>
  <div>
    <div v-if="isNarrowScreen">
      <!-- 移动端/平板端布局 (≤768px) -->
      <MobileLayout />
    </div>
    <div v-else>
      <!-- 桌面端布局 (>768px) -->
      <DesktopLayout />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useResponsive } from '@/hooks/useResponsive'

const { isNarrowScreen } = useResponsive()
</script>
```

### 2. 判断具体设备类型

```vue
<template>
  <div>
    <div v-if="isMobile">手机布局 (≤480px)</div>
    <div v-else-if="isTablet">平板布局 (481-768px)</div>
    <div v-else-if="isDesktop">桌面布局 (769-1280px)</div>
    <div v-else>宽屏布局 (>1280px)</div>
  </div>
</template>

<script setup lang="ts">
import { useResponsive } from '@/hooks/useResponsive'

const { isMobile, isTablet, isDesktop, isWide } = useResponsive()
</script>
```

### 3. 使用屏幕尺寸类型

```vue
<template>
  <div :class="`layout-${screenSize}`">
    当前屏幕: {{ screenSize }}
  </div>
</template>

<script setup lang="ts">
import { useResponsive } from '@/hooks/useResponsive'

const { screenSize } = useResponsive()
// screenSize 可能的值: 'mobile' | 'tablet' | 'desktop' | 'wide'
</script>
```

### 4. 获取屏幕宽度

```vue
<template>
  <div>
    当前屏幕宽度: {{ screenWidth }}px
  </div>
</template>

<script setup lang="ts">
import { useResponsive } from '@/hooks/useResponsive'

const { screenWidth } = useResponsive()
</script>
```

## 高级用法

### 在计算属性中使用

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { useResponsive } from '@/hooks/useResponsive'

const { isNarrowScreen, screenSize } = useResponsive()

const columns = computed(() => {
  if (isNarrowScreen.value) return 1
  if (screenSize.value === 'desktop') return 3
  return 4
})

const cardSize = computed(() => {
  switch (screenSize.value) {
    case 'mobile':
      return 'small'
    case 'tablet':
      return 'medium'
    default:
      return 'large'
  }
})
</script>
```

### 在样式中使用

```vue
<template>
  <div class="container" :class="{ narrow: isNarrowScreen }">
    内容
  </div>
</template>

<script setup lang="ts">
import { useResponsive } from '@/hooks/useResponsive'

const { isNarrowScreen } = useResponsive()
</script>

<style scoped>
.container {
  padding: 20px;
}

.container.narrow {
  padding: 12px;
}
</style>
```

### 条件渲染组件

```vue
<template>
  <component :is="currentView" />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useResponsive } from '@/hooks/useResponsive'
import MobileView from './MobileView.vue'
import TabletView from './TabletView.vue'
import DesktopView from './DesktopView.vue'

const { screenSize } = useResponsive()

const currentView = computed(() => {
  switch (screenSize.value) {
    case 'mobile':
      return MobileView
    case 'tablet':
      return TabletView
    default:
      return DesktopView
  }
})
</script>
```

## 自定义媒体查询

如果需要自定义断点，可以使用 `useMediaQuery`：

```vue
<script setup lang="ts">
import { useMediaQuery } from '@/hooks/useResponsive'

// 自定义断点
const isLargeScreen = useMediaQuery('(min-width: 1440px)')
const isPortrait = useMediaQuery('(orientation: portrait)')
const prefersReducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)')
</script>
```

## API

### useResponsive()

返回一个包含以下响应式属性的对象：

| 属性 | 类型 | 说明 |
|------|------|------|
| isNarrowScreen | Ref\<boolean\> | 是否为窄屏（≤768px） |
| isMobile | Ref\<boolean\> | 是否为移动端（≤480px） |
| isTablet | Ref\<boolean\> | 是否为平板端（481-768px） |
| isDesktop | Ref\<boolean\> | 是否为桌面端（769-1280px） |
| isWide | Ref\<boolean\> | 是否为宽屏（>1280px） |
| screenSize | Ref\<ScreenSize\> | 当前屏幕尺寸类型 |
| screenWidth | Ref\<number\> | 当前屏幕宽度（像素） |

### useMediaQuery(query: string)

参数：
- `query`: CSS 媒体查询字符串

返回：
- `Ref<boolean>`: 是否匹配该媒体查询

## 性能优化

1. **单例模式**：全局只有一个 resize 监听器，所有组件共享状态
2. **防抖处理**：resize 事件使用 150ms 防抖，避免频繁计算
3. **自动清理**：组件卸载时自动清理监听器

## 迁移指南

### 从旧代码迁移

**之前的代码：**
```typescript
const isNarrowScreen = ref(false)
const NARROW_SCREEN_THRESHOLD = 768

const checkScreenWidth = () => {
  isNarrowScreen.value = window.innerWidth <= NARROW_SCREEN_THRESHOLD
}

onMounted(() => {
  checkScreenWidth()
  window.addEventListener('resize', checkScreenWidth)
})

onUnmounted(() => {
  window.removeEventListener('resize', checkScreenWidth)
})
```

**迁移后的代码：**
```typescript
import { useResponsive } from '@/hooks/useResponsive'

const { isNarrowScreen } = useResponsive()
```

## 注意事项

1. 所有组件共享同一个响应式状态，修改断点配置会影响全局
2. 首次使用时会自动初始化并开始监听窗口变化
3. 不需要手动清理，Hook 会自动管理生命周期
4. 建议在需要响应式布局的组件中使用，避免不必要的依赖

## 示例项目

查看以下文件获取完整示例：
- `web/src/modules/home/views/index.vue` - 工作台快捷入口布局使用示例
- `web/src/components/Card/DataCardList.vue` - 卡片列表响应式布局

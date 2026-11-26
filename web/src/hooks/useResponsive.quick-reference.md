# useResponsive 快速参考

## 一行代码判断窄屏

```typescript
import { useResponsive } from '@/hooks/useResponsive'

const { isNarrowScreen } = useResponsive()
```

## 常用场景

### 1. 移动端/桌面端布局切换

```vue
<template>
  <div v-if="isNarrowScreen">移动端布局</div>
  <div v-else>桌面端布局</div>
</template>

<script setup lang="ts">
import { useResponsive } from '@/hooks/useResponsive'
const { isNarrowScreen } = useResponsive()
</script>
```

### 2. 响应式列数

```typescript
const { isMobile, isTablet, isDesktop } = useResponsive()

const columns = computed(() => {
  if (isMobile.value) return 1
  if (isTablet.value) return 2
  if (isDesktop.value) return 3
  return 4
})
```

### 3. 条件样式

```vue
<template>
  <div :class="{ narrow: isNarrowScreen }">内容</div>
</template>
```

### 4. 动态组件

```vue
<template>
  <MobileView v-if="isMobile" />
  <DesktopView v-else />
</template>
```

## 断点值

| 名称 | 范围 | 用途 |
|------|------|------|
| mobile | ≤480px | 手机 |
| tablet | 481-768px | 平板 |
| desktop | 769-1280px | 桌面 |
| wide | >1280px | 宽屏 |

## 返回值

```typescript
{
  isNarrowScreen,  // ≤768px
  isMobile,        // ≤480px
  isTablet,        // 481-768px
  isDesktop,       // 769-1280px
  isWide,          // >1280px
  screenSize,      // 'mobile' | 'tablet' | 'desktop' | 'wide'
  screenWidth,     // 当前宽度（px）
}
```

## 迁移示例

**旧代码：**
```typescript
const isNarrowScreen = ref(false)
const checkScreenWidth = () => {
  isNarrowScreen.value = window.innerWidth <= 768
}
onMounted(() => {
  checkScreenWidth()
  window.addEventListener('resize', checkScreenWidth)
})
onUnmounted(() => {
  window.removeEventListener('resize', checkScreenWidth)
})
```

**新代码：**
```typescript
import { useResponsive } from '@/hooks/useResponsive'
const { isNarrowScreen } = useResponsive()
```

## 优势

✅ 全局共享，性能更好  
✅ 自动防抖，避免频繁计算  
✅ 自动清理，无需手动管理  
✅ TypeScript 支持  
✅ 多断点判断

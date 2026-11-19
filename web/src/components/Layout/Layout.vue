<template>
  <div class="route-layout-container">
    <router-view v-slot="{ Component, route }">
      <transition name="fade" mode="out-in">
        <keep-alive v-if="route.meta.keepAlive" :max="10">
          <component :is="Component" :key="route.fullPath" />
        </keep-alive>
        <component :is="Component" v-else :key="route.fullPath" />
      </transition>
    </router-view>
  </div>
</template>

<script setup lang="ts">
/**
 * 嵌套路由布局组件
 * 用于父路由没有自定义内容，只需要渲染子路由的场景
 *
 * 使用场景：
 * - 父路由仅用于菜单分组，不需要显示自己的内容
 * - 父路由只是一个容器，直接渲染子路由
 */
</script>

<script lang="ts">
export default {
  name: 'Layout',
}
</script>

<style scoped>
.route-layout-container {
  width: 100%;
  height: 100%;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.fade-enter-to,
.fade-leave-from {
  opacity: 1;
}
</style>

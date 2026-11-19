<template>
  <svg
    class="brand-logo"
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    focusable="false"
  >
    <path d="M3 16c3.5 0 3.5-4 7-4s3.5 4 7 4 4-2 4-2" />
    <path d="M3 9c3.5 0 3.5-4 7-4s3.5 4 7 4 4-2 4-2" />
  </svg>
</template>

<script setup lang="ts">
/**
 * 品牌图形标识（双层波浪）。
 *
 * 全站唯一的品牌 Logo 图形来源：管理端侧栏与登录/初始化品牌面板共用，
 * 避免各展示位置各自维护一份路径。外层贴纸（底色、圆角、投影、倾斜）由各位置的容器样式决定。
 */
withDefaults(
  defineProps<{
    /** 图形边长（px） */
    size?: number
  }>(),
  { size: 24 },
)
</script>

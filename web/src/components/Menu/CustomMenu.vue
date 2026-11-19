<template>
  <n-menu
    :collapsed="collapsed"
    :collapsed-width="76"
    :collapsed-icon-size="22"
    :value="currentRoutePath"
    :expanded-keys="expandedKeys"
    :options="menuOptions"
    @update:value="handleUpdate"
    @update:expanded-keys="handleExpandedKeysUpdate"
  />
</template>
<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useMenus } from '@/hooks/menus'
import { useRoute, useRouter } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import { getExternalMenuResolver } from '@/config/externalMenuLinks'

withDefaults(defineProps<{ collapsed?: boolean }>(), { collapsed: false })
const emit = defineEmits<{ select: [key: string] }>()

const route = useRoute()
const router = useRouter()
const menuOptions = useMenus()

const currentRoutePath = computed(() => route.path)

// 展开的菜单项keys
const expandedKeys = ref<string[]>([])

/**
 * 查找路由的所有父路径
 * @param path 路由路径
 * @returns 父路径数组
 */
function findParentPaths(path: string): string[] {
  const parts = path.split('/').filter((p) => p.length > 0)
  const parents: string[] = []

  // Start from 2 to skip /admin (index 1)
  for (let i = 2; i < parts.length; i++) {
    parents.push('/' + parts.slice(0, i).join('/'))
  }

  return parents
}

/**
 * 当路由变化时，自动展开父菜单项
 */
watch(
  currentRoutePath,
  (newPath) => {
    const parents = findParentPaths(newPath)
    expandedKeys.value = parents
  },
  { immediate: true },
)

/**
 * 打开外链菜单项。
 *
 * 同步打开空白标签以保留点击手势，避免解析目标地址的异步请求
 * 耗尽用户激活窗口后被浏览器当作弹窗拦截；解析完成后将新标签
 * 指向目标地址。若空白标签被拦截或解析失败，则回退为当前页跳转。
 */
async function openExternalLink(resolveTarget: () => Promise<RouteLocationRaw>) {
  const targetWindow = window.open('', '_blank')

  let target: RouteLocationRaw
  try {
    target = await resolveTarget()
  } catch (error) {
    targetWindow?.close()
    console.error('External link resolution failed:', error)
    return
  }

  if (targetWindow) {
    targetWindow.location.href = router.resolve(target).href
    return
  }

  // 空白标签被浏览器拦截时，退化为当前页跳转
  await router.push(target).catch((err) => {
    console.error('Navigation failed:', err)
  })
}

/**
 * 处理菜单项点击
 */
function handleUpdate(key: string) {
  // 外链菜单项：解析目标地址后直接新标签打开，不进入内容区
  const route = router.getRoutes().find((r) => r.path === key)
  const resolveTarget = route?.name ? getExternalMenuResolver(String(route.name)) : undefined
  if (resolveTarget) {
    emit('select', key)
    void openExternalLink(resolveTarget)
    return
  }

  // 直接使用 key 作为路径跳转
  if (key) {
    emit('select', key)
    router.push(key).catch((err) => {
      console.error('Navigation failed:', err)
    })
  }
}

/**
 * 处理展开keys更新（用户手动展开/折叠）
 */
function handleExpandedKeysUpdate(keys: string[]) {
  expandedKeys.value = keys
}
</script>
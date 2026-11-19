<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="t('Authz:ModuleDetailTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
  >
    <n-scrollbar
      :style="{ maxHeight: scrollbarHeight }"
      :theme-overrides="scrollbarThemeOverrides"
      trigger="none">
      <div v-if="module" class="po-stack">
        <section class="po-card po-card--cream authz-modal-block">
          <header class="po-card__head authz-detail-head">
            <span class="po-avatar po-avatar--round authz-detail-avatar">
              {{ (module.name || '?').charAt(0).toUpperCase() }}
            </span>
            <div>
              <h3>{{ module.name }}</h3>
              <p>{{ module.version ? `v${module.version}` : module.icon || '—' }}</p>
            </div>
            <span class="po-pill" :class="module.enabled ? 'po-pill--mint' : 'po-pill--outline'">
              {{ module.enabled ? t('Common:Enabled') : t('Common:Disabled') }}
            </span>
          </header>
          <dl class="po-kv">
            <dt>{{ t('Common:Order') }}</dt>
            <dd>{{ module.order }}</dd>
            <dt>{{ t('Common:Icon') }}</dt>
            <dd>{{ module.icon || '—' }}</dd>
            <dt>{{ t('Field:Layout') }}</dt>
            <dd>{{ module.layout || '—' }}</dd>
            <dt>{{ t('Authz:FieldEmptyRedirect') }}</dt>
            <dd>{{ module.emptyRedirect || '—' }}</dd>
            <dt>{{ t('Common:Description') }}</dt>
            <dd>{{ module.description || '—' }}</dd>
          </dl>
        </section>

        <AuditInfo
          :creation-time="module.creationTime"
          :creator-id="module.creatorId"
          :last-modification-time="module.lastModificationTime"
          :last-modifier-id="module.lastModifierId"
        />

        <section class="po-card authz-modal-block">
          <header class="po-card__head">
            <h3>{{ t('Authz:RelatedRoutes') }}</h3>
          </header>
          <n-spin :show="loadingRoutes">
            <div v-if="moduleRoutes.length > 0" class="po-chip-row">
              <span v-for="route in moduleRoutes" :key="route.id" class="po-pill po-pill--sky">
                {{ route.name }} · {{ route.path }}
              </span>
            </div>
            <p v-else class="po-small po-muted">{{ t('Authz:RelatedEmptyRoutes') }}</p>
          </n-spin>
        </section>
      </div>
    </n-scrollbar>
  </n-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Module } from '@/modules/authorization/api/module/moduleModels'
import type { Route } from '@/modules/authorization/api/route/routeModels'
import { AuditInfo } from '@/components/AuditInfo'
import { moduleApi } from '@/modules/authorization/api/module'
import { t } from '@/i18n'

interface Props {
  show: boolean
  module: Module | null
}

interface Emits {
  (e: 'update:show', value: boolean): void
}

const props = defineProps<Props>()
defineEmits<Emits>()

// 滚动条主题覆盖
const scrollbarThemeOverrides = {
  width: '0px',
  height: '4px',
  borderRadius: '2px',
}

// 关联数据
const moduleRoutes = ref<Route[]>([])
const loadingRoutes = ref(false)

// 加载模块的路由
const loadModuleRoutes = async (moduleId: string) => {
  loadingRoutes.value = true
  try {
    const routes = await moduleApi.getModuleRoutes(moduleId)
    moduleRoutes.value = routes || []
  } catch (error) {
    console.error('Failed to load module routes:', error)
    moduleRoutes.value = []
  } finally {
    loadingRoutes.value = false
  }
}

// 监听弹窗显示状态与模块变化，加载关联数据
// 合并为单个 watcher：打开弹窗时只触发一次，避免对同一接口发起重复请求
// （请求层会取消相同 key 的在途请求，重复触发会产生 CanceledError 并短暂显示空状态）
watch(
  [() => props.show, () => props.module],
  ([show, module]) => {
    if (show && module) {
      loadModuleRoutes(module.id)
    } else {
      moduleRoutes.value = []
    }
  },
  { immediate: true },
)

// 对话框样式
const modalStyle = computed(() => {
  return {
    width: '700px',
    maxHeight: '85vh',
  }
})

// 滚动条高度
const scrollbarHeight = computed(() => {
  return 'calc(85vh - 120px)'
})
</script>

<style scoped>
.authz-modal-block {
  padding: 16px;
}

.authz-detail-head {
  align-items: center;
}

.authz-detail-head h3 {
  font-size: 16px;
  font-weight: var(--po-weight-display);
}

.authz-detail-avatar {
  width: 40px;
  height: 40px;
  font-size: 15px;
}
</style>

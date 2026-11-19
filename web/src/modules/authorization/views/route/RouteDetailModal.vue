<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="t('Authz:RouteDetailTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
  >
    <n-scrollbar
      :style="{ maxHeight: scrollbarHeight }"
      :theme-overrides="scrollbarThemeOverrides"
      trigger="none">
      <div v-if="route" class="po-stack">
        <section class="po-card po-card--cream authz-modal-block">
          <header class="po-card__head authz-detail-head">
            <span class="po-avatar po-avatar--round authz-detail-avatar">
              {{ (route.name || '?').charAt(0).toUpperCase() }}
            </span>
            <div>
              <h3>{{ route.name }}</h3>
              <p class="po-mono">{{ route.path }}</p>
            </div>
            <span v-if="route.moduleName" class="po-pill po-pill--sky">{{ route.moduleName }}</span>
          </header>

          <dl class="po-kv">
            <dt>{{ t('Field:Path') }}</dt>
            <dd class="po-mono">{{ route.path }}</dd>
            <dt>{{ t('Field:Component') }}</dt>
            <dd>{{ route.component || '—' }}</dd>
            <dt>{{ t('Authz:FieldParentRoute') }}</dt>
            <dd>{{ parentRouteName }}</dd>
            <dt>{{ t('Authz:FieldRedirect') }}</dt>
            <dd>{{ route.redirect || '—' }}</dd>
          </dl>
        </section>

        <section class="po-card authz-modal-block">
          <header class="po-card__head">
            <h3>{{ t('Authz:SectionMeta') }}</h3>
          </header>
          <dl class="po-kv">
            <dt>{{ t('Authz:FieldMetaTitle') }}</dt>
            <dd>{{ route.meta?.title || '—' }}</dd>
            <dt>{{ t('Common:Icon') }}</dt>
            <dd>{{ route.meta?.icon || '—' }}</dd>
            <dt>{{ t('Common:Order') }}</dt>
            <dd>{{ route.meta?.order ?? '—' }}</dd>
            <dt>{{ t('Authz:FieldPermissionCode') }}</dt>
            <dd class="po-mono">{{ route.meta?.permissionCode || '—' }}</dd>
            <dt>{{ t('Authz:FieldPermissionMatchMode') }}</dt>
            <dd>{{ permissionMatchModeLabel }}</dd>
          </dl>
          <div class="po-chip-row">
            <span class="po-pill" :class="route.meta?.menu ? 'po-pill--lilac' : 'po-pill--outline'">
              {{ t('Field:Menu') }}：{{ route.meta?.menu ? t('Common:Yes') : t('Common:No') }}
            </span>
            <span class="po-pill" :class="route.meta?.tab ? 'po-pill--sun' : 'po-pill--outline'">
              {{ t('Field:Tab') }}：{{ route.meta?.tab ? t('Common:Yes') : t('Common:No') }}
            </span>
            <span class="po-pill" :class="route.meta?.keepAlive ? 'po-pill--mint' : 'po-pill--outline'">
              {{ t('Authz:FieldKeepAlive') }}：{{ route.meta?.keepAlive ? t('Common:Yes') : t('Common:No') }}
            </span>
            <span class="po-pill" :class="route.meta?.pinned ? 'po-pill--peach' : 'po-pill--outline'">
              {{ t('Authz:FieldPinned') }}：{{ route.meta?.pinned ? t('Common:Yes') : t('Common:No') }}
            </span>
            <span class="po-pill" :class="route.meta?.anonymous ? 'po-pill--coral' : 'po-pill--outline'">
              {{ t('Authz:FieldAnonymous') }}：{{ route.meta?.anonymous ? t('Common:Yes') : t('Common:No') }}
            </span>
            <span class="po-pill" :class="route.meta?.disabled ? 'po-pill--coral' : 'po-pill--outline'">
              {{ t('Authz:FieldDisabled') }}：{{ route.meta?.disabled ? t('Common:Yes') : t('Common:No') }}
            </span>
          </div>
        </section>

        <section v-if="childRoutes.length > 0" class="po-card authz-modal-block">
          <header class="po-card__head">
            <h3>{{ t('Authz:ChildRoutes') }}</h3>
          </header>
          <ul class="po-list">
            <li v-for="child in childRoutes" :key="child.id" class="po-list__item">
              <span class="po-list__main">
                <b>{{ child.name }}</b>
                <span class="po-mono">{{ child.path }}</span>
              </span>
            </li>
          </ul>
        </section>

        <AuditInfo
          :creation-time="route.creationTime"
          :creator-id="route.creatorId"
          :last-modification-time="route.lastModificationTime"
          :last-modifier-id="route.lastModifierId"
        />
      </div>
    </n-scrollbar>
  </n-modal>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Route } from '@/modules/authorization/api/route/routeModels'
import { AuditInfo } from '@/components/AuditInfo'
import { t } from '@/i18n'

interface Props {
  show: boolean
  route: Route | null
  routes: Route[]
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

// 父路由名称
const parentRouteName = computed(() => {
  if (!props.route) return '—'
  if (!props.route.parentId) return t('Authz:RootRoute')
  const parent = props.routes.find(item => item.id === props.route?.parentId)
  return parent?.name || '—'
})

// 子路由
const childRoutes = computed<Route[]>(() => props.route?.children || [])

const permissionMatchModeLabel = computed(() => {
  if (!props.route?.meta?.permissionCode) return '—'
  return props.route.meta.permissionRequireAll
    ? t('Authz:PermissionMatchAll')
    : t('Authz:PermissionMatchAny')
})

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

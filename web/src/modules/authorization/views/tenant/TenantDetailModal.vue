<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="t('Tenant:DetailTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
  >
    <n-scrollbar :style="{ maxHeight: scrollbarHeight }" trigger="none">
      <n-spin :show="loadingAssignments">
        <div v-if="tenant" class="po-stack">
          <section class="po-card po-card--cream authz-modal-block">
            <header class="po-card__head authz-detail-head">
              <span class="po-avatar po-avatar--round authz-detail-avatar">
                {{ (tenant.name || '?').charAt(0).toUpperCase() }}
              </span>
              <div>
                <h3>{{ tenant.name }}</h3>
                <p class="po-mono">{{ tenant.code }}</p>
              </div>
              <span class="po-pill" :class="tenant.isActive ? 'po-pill--mint' : 'po-pill--coral'">
                {{ tenant.isActive ? t('Tenant:Active') : t('Tenant:Disabled') }}
              </span>
            </header>
            <dl class="po-kv">
              <dt>{{ t('Tenant:Code') }}</dt>
              <dd class="po-mono">{{ tenant.code }}</dd>
              <dt>{{ t('Tenant:AdminEmail') }}</dt>
              <dd>{{ tenant.adminEmail || '—' }}</dd>
              <dt>{{ t('Common:CreatedAt') }}</dt>
              <dd>{{ tenant.creationTime ? formatDate(tenant.creationTime, 'YYYY-MM-DD HH:mm:ss') : '—' }}</dd>
            </dl>
          </section>

          <!-- 已分配的模块 -->
          <section class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Tenant:AssignedModules') }}</h3>
            </header>
            <div v-if="assignments && assignments.modules.length > 0" class="po-chip-row">
              <span v-for="mod in assignments.modules" :key="mod.id" class="po-pill po-pill--sky">
                {{ mod.name }}
              </span>
            </div>
            <p v-else class="po-small po-muted">{{ t('Tenant:NoAssignments') }}</p>
          </section>

          <!-- 已分配的权限 -->
          <section class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Tenant:AssignedPermissions') }}</h3>
            </header>
            <div v-if="assignments && assignments.permissions.length > 0" class="po-chip-row">
              <span v-for="perm in assignments.permissions" :key="perm.id" class="po-pill po-pill--mint">
                {{ perm.name }} · {{ perm.code }}
              </span>
            </div>
            <p v-else class="po-small po-muted">{{ t('Tenant:NoAssignments') }}</p>
          </section>

          <!-- 已分配的路由 -->
          <section class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Tenant:AssignedRoutes') }}</h3>
            </header>
            <ul v-if="assignments && assignments.routes.length > 0" class="po-list">
              <li v-for="route in assignments.routes" :key="route.id" class="po-list__item">
                <span class="po-list__main">
                  <b>{{ route.title ? t(route.title) : route.name }}</b>
                  <span class="po-mono">{{ route.path }}</span>
                </span>
                <span v-if="route.menu" class="po-pill po-pill--lilac">{{ t('Field:Menu') }}</span>
              </li>
            </ul>
            <p v-else class="po-small po-muted">{{ t('Tenant:NoAssignments') }}</p>
          </section>
        </div>
      </n-spin>
    </n-scrollbar>
  </n-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Tenant, TenantAssignment } from '@/modules/authorization/api/tenant/tenantModels'
import { tenantApi } from '@/modules/authorization/api/tenant'
import { formatDate } from '@/utils/date'
import { t } from '@/i18n'

interface Props {
  show: boolean
  tenant: Tenant | null
}

interface Emits {
  (e: 'update:show', value: boolean): void
}

const props = defineProps<Props>()
defineEmits<Emits>()

const assignments = ref<TenantAssignment | null>(null)
const loadingAssignments = ref(false)

// 加载租户已分配的配置
const loadAssignments = async (tenantId: string) => {
  loadingAssignments.value = true
  try {
    assignments.value = await tenantApi.getAssignments(tenantId)
  } catch (error) {
    console.error('Failed to load tenant assignments:', error)
    assignments.value = null
  } finally {
    loadingAssignments.value = false
  }
}

// 监听租户变化
watch(
  () => props.tenant,
  (tenant) => {
    if (tenant && props.show) {
      loadAssignments(tenant.id)
    } else {
      assignments.value = null
    }
  },
  { immediate: true },
)

// 监听弹窗显示
watch(
  () => props.show,
  (show) => {
    if (show && props.tenant) {
      loadAssignments(props.tenant.id)
    }
  },
)

const modalStyle = computed(() => ({
  width: '700px',
  maxHeight: '85vh',
}))

const scrollbarHeight = computed(() => 'calc(85vh - 120px)')
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

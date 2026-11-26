<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="t('Authz:RoleDetailTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
  >
    <n-scrollbar
      :style="{ maxHeight: scrollbarHeight }"
      :theme-overrides="scrollbarThemeOverrides"
      trigger="none">
      <div v-if="role" class="po-stack">
        <section class="po-card po-card--cream authz-modal-block">
          <header class="po-card__head authz-detail-head">
            <span class="po-avatar po-avatar--round authz-detail-avatar">
              {{ (role.name || role.code || '?').charAt(0).toUpperCase() }}
            </span>
            <div>
              <h3>{{ role.name }}</h3>
              <p>{{ role.code }}</p>
            </div>
          </header>

          <dl class="po-kv">
            <dt>{{ t('Field:RoleCode') }}</dt>
            <dd>{{ role.code }}</dd>
            <dt>{{ t('Field:RoleName') }}</dt>
            <dd>{{ role.name }}</dd>
            <dt>{{ t('Common:Description') }}</dt>
            <dd>{{ role.description || '—' }}</dd>
          </dl>
        </section>

        <AuditInfo
          :creation-time="role.creationTime"
          :creator-id="role.creatorId"
          :last-modification-time="role.lastModificationTime"
          :last-modifier-id="role.lastModifierId"
        />

        <section class="po-card authz-modal-block">
          <header class="po-card__head">
            <h3>{{ t('Authz:RelatedModules') }}</h3>
          </header>
          <n-spin :show="loadingModules">
            <div v-if="roleModules.length > 0" class="po-chip-row">
              <span v-for="moduleItem in roleModules" :key="moduleItem.id" class="po-pill po-pill--sky">
                {{ moduleItem.name }}
              </span>
            </div>
            <p v-else class="po-small po-muted">{{ t('Authz:RelatedEmptyModules') }}</p>
          </n-spin>
        </section>

        <section class="po-card authz-modal-block">
          <header class="po-card__head">
            <h3>{{ t('Authz:RelatedPermissions') }}</h3>
          </header>
          <n-spin :show="loadingPermissions">
            <div v-if="rolePermissions.length > 0" class="po-chip-row">
              <span
                v-for="permission in rolePermissions"
                :key="permission.id"
                class="po-pill po-pill--mint"
              >
                {{ permission.name }} · {{ permission.code }}
              </span>
            </div>
            <p v-else class="po-small po-muted">{{ t('Authz:RelatedEmptyPermissions') }}</p>
          </n-spin>
        </section>
      </div>
    </n-scrollbar>
  </n-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Role } from '@/modules/authorization/api/role/roleModels'
import type { Module } from '@/modules/authorization/api/module/moduleModels'
import type { Permission } from '@/modules/authorization/api/permission/permissionModels'
import { AuditInfo } from '@/components/AuditInfo'
import { roleApi } from '@/modules/authorization/api/role'
import { moduleApi } from '@/modules/authorization/api/module'
import { permissionApi } from '@/modules/authorization/api/permission'
import { collectAllPages } from '@/utils/pagination'
import { t } from '@/i18n'

interface Props {
  show: boolean
  role: Role | null
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
const roleModules = ref<Module[]>([])
const rolePermissions = ref<Permission[]>([])
const loadingModules = ref(false)
const loadingPermissions = ref(false)

// 加载角色的模块
const loadRoleModules = async (roleId: string) => {
  loadingModules.value = true
  try {
    const moduleIds = await roleApi.getRoleModules(roleId)
    if (moduleIds.length > 0) {
      const allModules = await collectAllPages((skipCount, maxResultCount) =>
        moduleApi.getList({ skipCount, maxResultCount }),
      )
      // 过滤出角色关联的模块
      roleModules.value = allModules.filter(m => moduleIds.includes(m.id))
    } else {
      roleModules.value = []
    }
  } catch (error) {
    console.error('Failed to load role modules:', error)
    roleModules.value = []
  } finally {
    loadingModules.value = false
  }
}

// 加载角色的权限
const loadRolePermissions = async (roleId: string) => {
  loadingPermissions.value = true
  try {
    const permissionIds = await roleApi.getRolePermissions(roleId)
    if (permissionIds.length > 0) {
      const allPermissions = await collectAllPages((skipCount, maxResultCount) =>
        permissionApi.getList({ skipCount, maxResultCount }),
      )
      // 过滤出角色关联的权限
      rolePermissions.value = allPermissions.filter(p => permissionIds.includes(p.id))
    } else {
      rolePermissions.value = []
    }
  } catch (error) {
    console.error('Failed to load role permissions:', error)
    rolePermissions.value = []
  } finally {
    loadingPermissions.value = false
  }
}

// 监听弹窗显示状态与角色变化，加载关联数据
// 合并为单个 watcher：打开弹窗时只触发一次，避免对同一接口发起重复请求
// （请求层会取消相同 key 的在途请求，重复触发会产生 CanceledError 并短暂显示空状态）
watch(
  [() => props.show, () => props.role],
  ([show, role]) => {
    if (show && role) {
      loadRoleModules(role.id)
      loadRolePermissions(role.id)
    } else {
      roleModules.value = []
      rolePermissions.value = []
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

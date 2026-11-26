<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="t('Authz:PermissionDetailTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
  >
    <n-scrollbar
      :style="{ maxHeight: scrollbarHeight }"
      :theme-overrides="scrollbarThemeOverrides"
      trigger="none">
      <div v-if="permission" class="po-stack">
        <section class="po-card po-card--cream authz-modal-block">
          <header class="po-card__head authz-detail-head">
            <span class="po-avatar po-avatar--round authz-detail-avatar">
              {{ (permission.name || '?').charAt(0).toUpperCase() }}
            </span>
            <div>
              <h3>{{ permission.name }}</h3>
              <p class="po-mono">{{ permission.code }}</p>
            </div>
          </header>
          <dl class="po-kv">
            <dt>{{ t('Common:Description') }}</dt>
            <dd>{{ permission.description || '—' }}</dd>
          </dl>
        </section>

        <AuditInfo
          :creation-time="permission.creationTime"
          :creator-id="permission.creatorId"
          :last-modification-time="permission.lastModificationTime"
          :last-modifier-id="permission.lastModifierId"
        />
      </div>
    </n-scrollbar>
  </n-modal>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Permission } from '@/modules/authorization/api/permission/permissionModels'
import { AuditInfo } from '@/components/AuditInfo'
import { t } from '@/i18n'

interface Props {
  show: boolean
  permission: Permission | null
}

interface Emits {
  (e: 'update:show', value: boolean): void
}

defineProps<Props>()
defineEmits<Emits>()

// 滚动条主题覆盖
const scrollbarThemeOverrides = {
  width: '0px',
  height: '4px',
  borderRadius: '2px',
}

// 对话框样式
const modalStyle = computed(() => {
  return {
    width: '600px',
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

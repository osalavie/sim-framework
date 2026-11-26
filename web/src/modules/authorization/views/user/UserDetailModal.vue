<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="t('Authz:UserDetailTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
  >
    <n-scrollbar :style="{ maxHeight: scrollbarHeight }" trigger="none">
      <div v-if="user" class="po-stack">
        <section class="po-card po-card--cream authz-modal-block">
          <header class="po-card__head authz-detail-head">
            <span class="po-avatar po-avatar--round authz-detail-avatar">
              {{ getInitial(user) }}
            </span>
            <div>
              <h3>{{ user.nickName || user.userName }}</h3>
              <p>{{ user.email || t('Authz:CardNoEmail') }}</p>
            </div>
            <span class="po-pill" :class="getSexTone(user.sex)">{{ getSexLabel(user.sex) }}</span>
          </header>

          <dl class="po-kv">
            <dt>{{ t('Field:Username') }}</dt>
            <dd>{{ user.userName }}</dd>
            <dt>{{ t('Field:FullName') }}</dt>
            <dd>{{ user.name || '—' }}</dd>
            <dt>{{ t('Field:Nickname') }}</dt>
            <dd>{{ user.nickName || '—' }}</dd>
          </dl>
        </section>

        <section class="po-card authz-modal-block">
          <header class="po-card__head">
            <h3>{{ t('Authz:SectionContact') }}</h3>
          </header>
          <dl class="po-kv">
            <dt>{{ t('Field:Email') }}</dt>
            <dd>{{ user.email || '—' }}</dd>
            <dt>{{ t('Field:Phone') }}</dt>
            <dd>{{ user.phone || '—' }}</dd>
            <dt>{{ t('Authz:FieldAddress') }}</dt>
            <dd>{{ user.address || '—' }}</dd>
            <dt>{{ t('Common:Description') }}</dt>
            <dd>{{ user.description || '—' }}</dd>
          </dl>
        </section>

        <section class="po-card authz-modal-block">
          <header class="po-card__head">
            <h3>{{ t('Authz:RelatedRoles') }}</h3>
          </header>
          <div v-if="user.roles?.length" class="po-chip-row">
            <span v-for="role in user.roles" :key="role.id" class="po-pill po-pill--sky">
              {{ role.name }} · {{ role.code }}
            </span>
          </div>
          <p v-else class="po-small po-muted">{{ t('Authz:RelatedEmptyRoles') }}</p>
        </section>

        <AuditInfo
          :creation-time="user.creationTime"
          :creator-id="user.creatorId"
          :last-modification-time="user.lastModificationTime"
          :last-modifier-id="user.lastModifierId"
        />
      </div>
    </n-scrollbar>
  </n-modal>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { User } from '@/modules/authorization/api/user/userModels'
import { AuditInfo } from '@/components/AuditInfo'
import { t } from '@/i18n'

interface Props {
  show: boolean
  user: User | null
}

interface Emits {
  (e: 'update:show', value: boolean): void
}

defineProps<Props>()
defineEmits<Emits>()

// 性别显示
const getSexLabel = (sex?: number) => {
  if (sex === 1) return t('Authz:SexMale')
  if (sex === 2) return t('Authz:SexFemale')
  return t('Authz:SexUnknown')
}

const getSexTone = (sex?: number) => {
  if (sex === 1) return 'po-pill--sky'
  if (sex === 2) return 'po-pill--lilac'
  return 'po-pill--outline'
}

const getInitial = (user: User) =>
  (user.nickName || user.name || user.userName || '?').charAt(0).toUpperCase()

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

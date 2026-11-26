<template>
  <DataCardList
    :data="roles"
    :loading="loading"
    :card-width="375"
    :has-more="hasMore"
    :loading-more="loadingMore"
    :clickable="false"
    :hoverable="false"
    @load-more="emit('load-more')"
  >
    <template #card="{ item }">
      <article class="po-card authz-card">
        <header class="authz-card__head">
          <span class="po-avatar po-avatar--round" :class="avatarTone(item.name)">
            {{ getInitial(item) }}
          </span>
          <div class="authz-card__id">
            <b>{{ item.name }}</b>
            <span>{{ item.code }}</span>
          </div>
          <span class="po-pill po-pill--sky">{{ t('Entity:Role') }}</span>
        </header>

        <p class="authz-card__desc po-small po-muted">
          {{ item.description || t('Authz:CardNoDescription') }}
        </p>

        <dl class="po-kv">
          <dt>{{ t('Common:CreatedAt') }}</dt>
          <dd>{{ item.creationTime ? formatDate(item.creationTime, 'YYYY-MM-DD HH:mm') : '—' }}</dd>
          <dt>{{ t('Common:UpdatedAt') }}</dt>
          <dd>{{ item.lastModificationTime ? formatDate(item.lastModificationTime, 'YYYY-MM-DD HH:mm') : '—' }}</dd>
        </dl>

        <footer class="authz-card__actions">
          <n-button size="small" secondary @click.stop="emit('view', item)">
            {{ t('Common:View') }}
          </n-button>
          <n-button
            v-permission="'system:role:manage'"
            size="small"
            secondary
            @click.stop="emit('edit', item)"
          >
            {{ t('Common:Edit') }}
          </n-button>
          <n-button
            v-if="!isBuiltInRole(item)"
            v-permission="'system:role:manage'"
            size="small"
            type="error"
            ghost
            @click.stop="emit('delete', item.id)"
          >
            {{ t('Common:Delete') }}
          </n-button>
        </footer>
      </article>
    </template>
  </DataCardList>
</template>

<script setup lang="ts">
import { DataCardList } from '@/components/Card'
import { isBuiltInRole, type Role } from '@/modules/authorization/api/role/roleModels'
import { formatDate } from '@/utils/date'
import { t } from '@/i18n'

interface Props {
  roles: Role[]
  loading: boolean
  searchKeyword: string
  hasMore: boolean
  loadingMore: boolean
}

defineProps<Props>()

const emit = defineEmits<{
  view: [role: Role]
  edit: [role: Role]
  delete: [id: string]
  'load-more': []
}>()

// 头像色彩轮换
const AVATAR_TONES = ['', 'po-avatar--peach', 'po-avatar--mint', 'po-avatar--lilac', 'po-avatar--sun']
const avatarTone = (seed: string) => {
  let hash = 0
  for (let i = 0; i < seed.length; i += 1) {
    hash = (hash + seed.charCodeAt(i)) % AVATAR_TONES.length
  }
  return AVATAR_TONES[hash]
}

const getInitial = (role: Role) => (role.name || role.code || '?').charAt(0).toUpperCase()

</script>

<style scoped>
.authz-card {
  display: flex;
  flex-direction: column;
  width: 100%;
  max-width: 375px;
  height: 232px;
  gap: 7px;
  margin: 0 auto;
  padding: 12px 14px;
  transition:
    transform 0.16s ease,
    box-shadow 0.16s ease;
}

.authz-card .po-kv {
  gap: 4px 12px;
}

.authz-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--po-shadow-hover);
}

.authz-card__head {
  display: flex;
  align-items: center;
  gap: 10px;
}

.authz-card__id {
  display: grid;
  flex: 1 1 auto;
  gap: 2px;
  min-width: 0;
}

.authz-card__id b {
  overflow: hidden;
  font-size: 14px;
  font-weight: var(--po-weight-strong);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.authz-card__id span {
  overflow: hidden;
  color: var(--po-ink-soft);
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.authz-card__desc {
  display: -webkit-box;
  overflow: hidden;
  margin: 0;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.authz-card__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  margin-top: auto;
  padding-top: 7px;
  border-top: 1px dashed var(--po-line);
}
</style>

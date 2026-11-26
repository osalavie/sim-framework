<template>
  <DataCardList
    :data="treeRoutes"
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
            {{ (item.name || '?').charAt(0).toUpperCase() }}
          </span>
          <div class="authz-card__id">
            <b>{{ item.name }}</b>
            <span class="po-mono">{{ item.path }}</span>
          </div>
        </header>

        <div class="po-chip-row">
          <span v-if="item.moduleName" class="po-pill po-pill--sky">{{ item.moduleName }}</span>
          <span v-if="item.meta?.menu" class="po-pill po-pill--lilac">{{ t('Field:Menu') }}</span>
          <span v-if="item.meta?.tab" class="po-pill po-pill--sun">{{ t('Field:Tab') }}</span>
          <button
            v-if="item.children?.length"
            type="button"
            class="po-pill po-pill--outline authz-card__child-link"
            v-tooltip="t('Authz:FilterByChildRoutes', { name: item.name })"
            :aria-label="t('Authz:FilterByChildRoutes', { name: item.name })"
            @click.stop="emit('filter-children', item)"
          >
            {{ t('Authz:ChildRoutes') }} {{ item.children.length }}
          </button>
        </div>

        <dl class="po-kv">
          <dt>{{ t('Field:Component') }}</dt>
          <dd class="authz-card__ellipsis">{{ item.component || '—' }}</dd>
          <dt>{{ t('Common:CreatedAt') }}</dt>
          <dd>{{ item.creationTime ? formatDate(item.creationTime, 'YYYY-MM-DD HH:mm') : '—' }}</dd>
        </dl>

        <footer class="authz-card__actions">
          <n-button size="small" secondary @click.stop="emit('view', item)">
            {{ t('Common:View') }}
          </n-button>
          <n-button
            v-permission="'system:route:update'"
            size="small"
            secondary
            @click.stop="emit('edit', item)"
          >
            {{ t('Common:Edit') }}
          </n-button>
          <n-button
            v-permission="'system:route:delete'"
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
import type { Route } from '@/modules/authorization/api/route/routeModels'
import { formatDate } from '@/utils/date'
import { t } from '@/i18n'

interface Props {
  treeRoutes: Route[]
  loading: boolean
  searchKeyword: string
  hasMore: boolean
  loadingMore: boolean
}

defineProps<Props>()

const emit = defineEmits<{
  view: [route: Route]
  edit: [route: Route]
  delete: [id: string]
  'filter-children': [route: Route]
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

.authz-card__ellipsis {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.authz-card__child-link {
  cursor: pointer;
  font-family: inherit;
  transition:
    background-color 0.12s ease,
    border-color 0.12s ease,
    color 0.12s ease;
}

.authz-card__child-link:hover,
.authz-card__child-link:focus-visible {
  border-color: var(--po-accent);
  background: var(--po-accent-soft);
  color: var(--po-accent);
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

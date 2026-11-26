<template>
  <DataCardList
    :data="modules"
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
            <Icon :name="item.icon || 'carbon:Application'" :size="18" />
          </span>
          <div class="authz-card__id">
            <b>{{ item.name }}</b>
            <span v-if="item.version">v{{ item.version }}</span>
          </div>
          <span class="po-pill" :class="item.enabled ? 'po-pill--mint' : 'po-pill--outline'">
            {{ item.enabled ? t('Common:Enabled') : t('Common:Disabled') }}
          </span>
        </header>

        <p class="authz-card__desc po-small po-muted">
          {{ item.description || t('Authz:CardNoDescription') }}
        </p>

        <dl class="po-kv">
          <dt>{{ t('Common:Order') }}</dt>
          <dd>{{ item.order }}</dd>
          <dt>{{ t('Field:Layout') }}</dt>
          <dd>{{ item.layout || '—' }}</dd>
          <dt>{{ t('Common:CreatedAt') }}</dt>
          <dd>{{ item.creationTime ? formatDate(item.creationTime, 'YYYY-MM-DD HH:mm') : '—' }}</dd>
        </dl>

        <footer class="authz-card__actions">
          <n-button size="small" secondary @click.stop="emit('view', item)">
            {{ t('Common:View') }}
          </n-button>
          <n-button
            v-permission="'system:module:update'"
            size="small"
            secondary
            @click.stop="emit('edit', item)"
          >
            {{ t('Common:Edit') }}
          </n-button>
          <n-button
            v-permission="'system:module:delete'"
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
import Icon from '@/components/Icon'
import type { Module } from '@/modules/authorization/api/module/moduleModels'
import { formatDate } from '@/utils/date'
import { t } from '@/i18n'

interface Props {
  modules: Module[]
  loading: boolean
  searchKeyword: string
  hasMore: boolean
  loadingMore: boolean
}

defineProps<Props>()

const emit = defineEmits<{
  view: [module: Module]
  edit: [module: Module]
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

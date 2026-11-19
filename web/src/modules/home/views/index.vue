<template>
  <div class="po-page workspace-home" data-testid="admin-home">
    <header class="po-page-head">
      <div class="po-page-head__main">
        <p class="po-kicker">{{ formattedDate }}</p>
        <h1 id="workspace-greeting">{{ t('Workspace:Greeting', { name: displayName }) }}</h1>
        <p class="workspace-subtitle">{{ t('Workspace:Subtitle') }}</p>
      </div>
      <div class="po-page-head__actions">
        <n-button secondary @click="router.push('/admin/profile')">
          <template #icon><n-icon :component="PersonCircleOutline" /></template>
          {{ t('Workspace:Profile') }}
        </n-button>
      </div>
    </header>

    <section class="po-card workspace-account" aria-labelledby="workspace-account-title">
      <header class="po-card__head">
        <div class="card-heading">
          <span class="card-heading__icon"><n-icon :component="PersonOutline" /></span>
          <div>
            <h2 id="workspace-account-title">{{ t('Workspace:Account:Title') }}</h2>
            <p>{{ t('Workspace:Account:Subtitle') }}</p>
          </div>
        </div>
      </header>
      <dl class="account-facts">
        <div class="account-fact">
          <dt>{{ t('Workspace:Account:UserName') }}</dt>
          <dd>{{ displayName }}</dd>
        </div>
        <div class="account-fact">
          <dt>{{ t('Workspace:Account:Roles') }}</dt>
          <dd>{{ roleSummary }}</dd>
        </div>
      </dl>
    </section>

    <section :aria-label="t('Workspace:QuickActions')">
      <h2 class="section-title">{{ t('Workspace:QuickActions') }}</h2>
      <div class="quick-grid">
        <button
          v-for="item in quickActions"
          :key="item.path"
          type="button"
          class="po-card quick-card"
          @click="router.push(item.path)"
        >
          <span class="quick-card__icon"><n-icon :component="item.icon" /></span>
          <strong>{{ t(item.titleKey) }}</strong>
          <span class="po-small po-muted">{{ t(item.hintKey) }}</span>
        </button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, type Component } from 'vue'
import { useRouter } from 'vue-router'
import {
  CheckmarkCircleOutline,
  GitNetworkOutline,
  LayersOutline,
  PeopleOutline,
  PersonCircleOutline,
  PersonOutline,
  ShieldCheckmarkOutline,
} from '@vicons/ionicons5'
import { locale, t } from '@/i18n'
import { useUserStore } from '@/stores/user'

interface QuickAction {
  path: string
  titleKey: string
  hintKey: string
  icon: Component
}

const router = useRouter()
const userStore = useUserStore()

const displayName = computed(() => userStore.user?.userName || t('Header:Account'))
const roleSummary = computed(() => {
  const names = userStore.roles.map((role) => role.name).filter(Boolean)
  return names.length ? names.join(' · ') : t('Workspace:Account:NoRoles')
})

const formattedDate = computed(() =>
  new Intl.DateTimeFormat(locale.value, {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    weekday: 'long',
  }).format(new Date()),
)

const quickActions: QuickAction[] = [
  {
    path: '/admin/approval/todo',
    titleKey: 'Workspace:Action:ApprovalTodo',
    hintKey: 'Workspace:Action:ApprovalTodoHint',
    icon: CheckmarkCircleOutline,
  },
  {
    path: '/admin/approval/definition',
    titleKey: 'Workspace:Action:ApprovalDefinition',
    hintKey: 'Workspace:Action:ApprovalDefinitionHint',
    icon: LayersOutline,
  },
  {
    path: '/admin/authorization/user',
    titleKey: 'Workspace:Action:Users',
    hintKey: 'Workspace:Action:UsersHint',
    icon: PeopleOutline,
  },
  {
    path: '/admin/authorization/role',
    titleKey: 'Workspace:Action:Roles',
    hintKey: 'Workspace:Action:RolesHint',
    icon: ShieldCheckmarkOutline,
  },
  {
    path: '/admin/system/organization',
    titleKey: 'Workspace:Action:Organization',
    hintKey: 'Workspace:Action:OrganizationHint',
    icon: GitNetworkOutline,
  },
  {
    path: '/admin/profile',
    titleKey: 'Workspace:Action:Profile',
    hintKey: 'Workspace:Action:ProfileHint',
    icon: PersonCircleOutline,
  },
]
</script>

<style scoped lang="scss">
.workspace-home {
  display: flex;
  flex-direction: column;
  gap: 18px;
  min-height: 100%;
}

.workspace-subtitle {
  margin-top: 6px;
  color: var(--po-ink-soft);
  font-size: 13px;
}

.workspace-account .po-card__head {
  flex: none;
}

.card-heading {
  display: flex;
  min-width: 0;
  align-items: flex-start;
  gap: 10px;
}

.card-heading__icon {
  display: grid;
  width: 34px;
  height: 34px;
  flex: none;
  place-items: center;
  border: var(--po-border);
  border-radius: var(--po-r-sm);
  background: var(--po-accent-soft);
  color: var(--po-accent);
  font-size: 17px;
}

.card-heading h2 {
  font-size: 17px;
}

.card-heading p {
  margin-top: 3px;
  color: var(--po-ink-soft);
  font-size: 12px;
}

.account-facts {
  display: flex;
  flex-wrap: wrap;
  gap: 12px 40px;
  margin: 14px 0 0;
}

.account-fact {
  display: flex;
  align-items: baseline;
  gap: 8px;

  dt {
    color: var(--po-ink-faint);
    font-size: 12px;
  }

  dd {
    margin: 0;
    color: var(--po-ink);
    font-size: 13px;
    font-weight: var(--po-weight-strong);
  }
}

.section-title {
  margin-bottom: 10px;
  font-size: 15px;
}

.quick-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 14px;
}

.quick-card {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 6px;
  padding: 16px;
  text-align: left;
  cursor: pointer;
  transition:
    border-color 0.18s ease,
    transform 0.18s ease;

  &:hover {
    border-color: var(--po-accent);
    transform: translateY(-2px);
  }

  &:focus-visible {
    outline: 2px solid var(--po-accent);
    outline-offset: 2px;
  }

  strong {
    font-size: 14px;
  }
}

.quick-card__icon {
  display: grid;
  width: 32px;
  height: 32px;
  margin-bottom: 2px;
  place-items: center;
  border-radius: var(--po-r-sm);
  background: var(--po-accent-soft);
  color: var(--po-accent);
  font-size: 17px;
}

@media (prefers-reduced-motion: reduce) {
  .quick-card {
    transition: none;
  }
}
</style>

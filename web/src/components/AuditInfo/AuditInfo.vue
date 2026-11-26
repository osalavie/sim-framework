<template>
  <n-card :title="t('Audit:Title')" size="small" :segmented="{ content: true }">
    <n-descriptions
      bordered
      label-placement="left"
      :column="isNarrowScreen ? 1 : 2"
      class="audit-descriptions"
    >
      <n-descriptions-item :label="t('Common:CreatedAt')">
        <time v-if="creationTime" :datetime="creationTime" class="audit-value">
          {{ formatDate(creationTime, 'YYYY-MM-DD HH:mm:ss') }}
        </time>
        <span v-else>-</span>
      </n-descriptions-item>
      <n-descriptions-item :label="t('Audit:Creator')">
        <span class="audit-value audit-identity" v-tooltip="creatorAccount">
          {{ creatorDisplay }}
        </span>
      </n-descriptions-item>
      <n-descriptions-item :label="t('Common:UpdatedAt')">
        <time v-if="lastModificationTime" :datetime="lastModificationTime" class="audit-value">
          {{ formatDate(lastModificationTime, 'YYYY-MM-DD HH:mm:ss') }}
        </time>
        <span v-else>-</span>
      </n-descriptions-item>
      <n-descriptions-item :label="t('Audit:LastModifier')">
        <span class="audit-value audit-identity" v-tooltip="lastModifierAccount">
          {{ lastModifierDisplay }}
        </span>
      </n-descriptions-item>
    </n-descriptions>
  </n-card>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { Ref } from 'vue'
import { NCard, NDescriptions, NDescriptionsItem } from 'naive-ui'
import { useResponsive } from '@/hooks/useResponsive'
import { t } from '@/i18n'
import { userApi } from '@/modules/authorization/api/user'
import { formatDate } from '@/utils/date'

interface Props {
  creationTime?: string | null
  creatorId?: string | null
  lastModificationTime?: string | null
  lastModifierId?: string | null
}

const props = defineProps<Props>()

const { isNarrowScreen } = useResponsive()

interface UserIdentity {
  /** 昵称优先，昵称为空时回退账号名。 */
  name: string | null
  /** 账号名，用于悬停气泡。 */
  account: string | null
}

const userNameCache = new Map<string, UserIdentity | null>()
const userNamePending = new Map<string, Promise<UserIdentity | null>>()

function loadIdentity(id: string): Promise<UserIdentity | null> {
  if (userNameCache.has(id)) {
    return Promise.resolve(userNameCache.get(id) ?? null)
  }
  const pending = userNamePending.get(id)
  if (pending) {
    return pending
  }

  const request = userApi
    .getById(id)
    .then((user) => {
      const identity: UserIdentity = {
        name: user.nickName?.trim() || user.userName?.trim() || null,
        account: user.userName?.trim() || null,
      }
      userNameCache.set(id, identity)
      return identity
    })
    .catch(() => {
      userNameCache.set(id, null)
      return null
    })
    .finally(() => {
      userNamePending.delete(id)
    })
  userNamePending.set(id, request)
  return request
}

const creatorIdentity = ref<UserIdentity | null>()
const lastModifierIdentity = ref<UserIdentity | null>()
const pendingIds = reactive(new Set<string>())

async function resolveIdentity(
  id: string | null | undefined,
  isCurrent: () => boolean,
  target: Ref<UserIdentity | null | undefined>,
) {
  if (!id) {
    target.value = null
    return
  }
  if (userNameCache.has(id)) {
    target.value = userNameCache.get(id) ?? null
    return
  }

  pendingIds.add(id)
  try {
    const identity = await loadIdentity(id)
    if (isCurrent()) {
      target.value = identity
    }
  } finally {
    pendingIds.delete(id)
  }
}

watch(
  () => props.creatorId,
  (id) => void resolveIdentity(id, () => props.creatorId === id, creatorIdentity),
  { immediate: true },
)

watch(
  () => props.lastModifierId,
  (id) => void resolveIdentity(id, () => props.lastModifierId === id, lastModifierIdentity),
  { immediate: true },
)

const displayName = (id: string | null | undefined, identity: UserIdentity | null | undefined) => {
  if (!id) return '-'
  if (identity?.name) return identity.name
  return pendingIds.has(id) ? '…' : id
}

const creatorDisplay = computed(() => displayName(props.creatorId, creatorIdentity.value))
const lastModifierDisplay = computed(() =>
  displayName(props.lastModifierId, lastModifierIdentity.value),
)

const creatorAccount = computed(() => creatorIdentity.value?.account ?? undefined)
const lastModifierAccount = computed(() => lastModifierIdentity.value?.account ?? undefined)
</script>

<style scoped>
.audit-value {
  display: inline-block;
  max-width: 100%;
  font-variant-numeric: tabular-nums;
}

.audit-identity {
  overflow-wrap: anywhere;
  word-break: break-word;
  user-select: all;
}

@media (max-width: 640px) {
  .audit-descriptions :deep(.n-descriptions-table-header),
  .audit-descriptions :deep(.n-descriptions-table-content) {
    padding: 10px 12px;
  }
}
</style>

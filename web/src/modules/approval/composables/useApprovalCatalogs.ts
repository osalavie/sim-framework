import { computed, ref } from 'vue'
import { getSystemRole } from '@/api/system'
import { t } from '@/i18n'
import { collectAllPages } from '@/utils/pagination'
import type { PageData } from '@/types/api'
import { approvalDefinitionApi, type ApprovalBusinessTypeDto } from '../api'
import { businessTypeLabelKey } from '../api/approvalModels'

const businessTypes = ref<ApprovalBusinessTypeDto[]>([])
const businessTypesLoaded = ref(false)
const businessTypesLoading = ref(false)
let businessTypesPending: Promise<void> | null = null

/**
 * 业务类型目录（模块级缓存）：定义编辑器与发起审批表单共用，
 * 数据来自各业务模块注册的审批描述符。
 */
export function useBusinessTypes() {
  const ensureLoaded = () => {
    if (businessTypesLoaded.value) return Promise.resolve()
    businessTypesLoading.value = true
    businessTypesPending ??= approvalDefinitionApi
      .businessTypes()
      .then((items) => {
        businessTypes.value = items
        businessTypesLoaded.value = true
      })
      .catch(() => {
        businessTypes.value = []
      })
      .finally(() => {
        businessTypesPending = null
        businessTypesLoading.value = false
      })
    return businessTypesPending
  }

  /** 标签优先取前端资源，缺失时回退服务端显示名，最后回退业务类型标识。 */
  const label = (businessType?: string | null) => {
    if (!businessType) return ''
    const key = businessTypeLabelKey(businessType)
    const translated = t(key)
    if (translated !== key) return translated
    return (
      businessTypes.value.find((item) => item.businessType === businessType)?.displayName ??
      businessType
    )
  }

  const options = computed(() =>
    businessTypes.value
      .filter((item) => Boolean(item.businessType))
      .map((item) => ({ label: label(item.businessType), value: item.businessType as string })),
  )

  return { items: businessTypes, options, label, loading: businessTypesLoading, ensureLoaded }
}

/** 审批角色选项：值取角色代码（审批人解析按代码优先、名称兜底匹配）。 */
export interface ApprovalRoleOption {
  label: string
  value: string
}

const roles = ref<ApprovalRoleOption[]>([])
const rolesLoaded = ref(false)
let rolesPending: Promise<void> | null = null

export function useRoleOptions() {
  const ensureLoaded = () => {
    if (rolesLoaded.value) return Promise.resolve()
    rolesPending ??= collectAllPages<{ code?: string | null; name?: string | null }>(
      async (skipCount, maxResultCount) => {
        const response = await getSystemRole({
          query: { SkipCount: skipCount, MaxResultCount: maxResultCount },
          throwOnError: true,
        })
        return (response.data ?? { items: [], totalCount: 0 }) as PageData<{
          code?: string | null
          name?: string | null
        }>
      },
    )
      .then((items) => {
        roles.value = items
          .filter((role) => Boolean(role.code))
          .map((role) => ({
            value: role.code as string,
            label:
              role.name && role.name !== role.code
                ? `${role.name}（${role.code}）`
                : (role.code as string),
          }))
        rolesLoaded.value = true
      })
      .catch(() => {
        roles.value = []
      })
      .finally(() => {
        rolesPending = null
      })
    return rolesPending
  }

  return { roles, options: computed(() => roles.value), ensureLoaded }
}

/** 审批组织选项：组织负责人 / 组织人员来源选择组织用，按树层级缩进展平（避免新增组件进入首屏 vendor 包）。 */
const organizationOptions = ref<{ label: string; value: string }[]>([])
const organizationsLoaded = ref(false)
let organizationsPending: Promise<void> | null = null

function flattenOrganizationOptions(
  items: { id?: string; name?: string | null; parentId?: string | null }[],
): { label: string; value: string }[] {
  const nameOf = new Map<string, string>()
  const parentOf = new Map<string, string | null>()
  for (const item of items) {
    if (!item.id) continue
    nameOf.set(item.id, item.name || item.id)
    parentOf.set(item.id, item.parentId ?? null)
  }
  const depthOf = (id: string) => {
    let depth = 0
    let current = parentOf.get(id)
    while (current && nameOf.has(current)) {
      depth += 1
      current = parentOf.get(current)
    }
    return depth
  }
  return items
    .filter((item): item is typeof item & { id: string } => Boolean(item.id))
    .map((item) => ({
      value: item.id,
      label: `${'　'.repeat(depthOf(item.id))}${nameOf.get(item.id)}`,
    }))
}

export function useOrganizationOptions() {
  const ensureLoaded = () => {
    if (organizationsLoaded.value) return Promise.resolve()
    organizationsPending ??= approvalDefinitionApi
      .organizations()
      .then((items) => {
        organizationOptions.value = flattenOrganizationOptions(items)
        organizationsLoaded.value = true
      })
      .catch(() => {
        organizationOptions.value = []
      })
      .finally(() => {
        organizationsPending = null
      })
    return organizationsPending
  }

  return { options: computed(() => organizationOptions.value), ensureLoaded }
}

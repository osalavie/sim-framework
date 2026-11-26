import { ref } from 'vue'
import { tenantApi } from '@/modules/authorization/api/tenant'
import type {
  Tenant,
  TenantFormData,
  TenantUpdateData,
  CreateTenantResult,
  TenantTemplate,
  TenantAssignment,
} from '@/modules/authorization/api/tenant/tenantModels'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { useKeepAliveRefresh } from '@/hooks/useKeepAliveRefresh'
import { t } from '@/i18n'

export function useTenantData() {
  const loading = ref(false)
  const submitting = ref(false)
  const tenants = ref<Tenant[]>([])
  const templates = ref<TenantTemplate | null>(null)

  // 加载模板配置（Host 级可选模块/路由/权限）
  const loadTemplates = async () => {
    try {
      templates.value = await tenantApi.getTemplates()
    } catch (error) {
      console.error('Failed to load tenant templates:', error)
      templates.value = null
      useDiscreteApi.message.error(t('Tenant:LoadTemplatesFailed'))
    }
  }

  // 加载租户列表
  const loadTenants = async () => {
    loading.value = true
    try {
      const list = await tenantApi.getList()
      tenants.value = list || []
    } catch (error) {
      console.error('Failed to load tenants:', error)
      tenants.value = []
      useDiscreteApi.message.error(t('Feedback:LoadFailed', { entity: t('Entity:Tenant') }))
    } finally {
      loading.value = false
    }
  }

  // 创建租户
  const createTenant = async (data: TenantFormData): Promise<CreateTenantResult | null> => {
    submitting.value = true
    try {
      const result = await tenantApi.create(data)
      useDiscreteApi.message.success(t('Tenant:CreateSuccess', { name: result.tenantName }))
      await loadTenants()
      return result
    } catch (error) {
      console.error('Failed to create tenant:', error)
      useDiscreteApi.message.error(t('Feedback:CreateFailed', { entity: t('Entity:Tenant') }))
      return null
    } finally {
      submitting.value = false
    }
  }

  // 更新租户
  const updateTenant = async (id: string, data: TenantUpdateData) => {
    submitting.value = true
    try {
      await tenantApi.update(id, data)
      useDiscreteApi.message.success(t('Feedback:UpdateSuccess', { entity: t('Entity:Tenant') }))
      await loadTenants()
      return true
    } catch (error) {
      console.error('Failed to update tenant:', error)
      useDiscreteApi.message.error(t('Feedback:UpdateFailed', { entity: t('Entity:Tenant') }))
      return false
    } finally {
      submitting.value = false
    }
  }

  // 删除租户
  const deleteTenant = async (id: string) => {
    try {
      await tenantApi.delete(id)
      useDiscreteApi.message.success(t('Feedback:DeleteSuccess', { entity: t('Entity:Tenant') }))
      await loadTenants()
    } catch (error) {
      console.error('Failed to delete tenant:', error)
      useDiscreteApi.message.error(t('Feedback:DeleteFailed', { entity: t('Entity:Tenant') }))
    }
  }

  const deleteTenants = async (ids: string[]) => {
    let successCount = 0

    for (const id of ids) {
      try {
        await tenantApi.delete(id)
        successCount += 1
      } catch (error) {
        console.error('Failed to delete tenant:', id, error)
      }
    }

    if (successCount === ids.length) {
      useDiscreteApi.message.success(t('Feedback:BatchDeleteSuccess', { count: successCount, entity: t('Entity:Tenant') }))
    } else if (successCount > 0) {
      useDiscreteApi.message.warning(
        t('Feedback:BatchDeletePartial', { success: successCount, total: ids.length, entity: t('Entity:Tenant') }),
      )
    } else {
      useDiscreteApi.message.error(t('Feedback:BatchDeleteFailed', { entity: t('Entity:Tenant') }))
    }

    await loadTenants()
    return successCount === ids.length
  }

  // 启用租户
  const enableTenant = async (id: string) => {
    try {
      await tenantApi.enable(id)
      useDiscreteApi.message.success(t('Tenant:EnableSuccess'))
      await loadTenants()
    } catch (error) {
      console.error('Failed to enable tenant:', error)
      useDiscreteApi.message.error(t('Tenant:EnableFailed'))
    }
  }

  // 禁用租户
  const disableTenant = async (id: string) => {
    try {
      await tenantApi.disable(id)
      useDiscreteApi.message.success(t('Tenant:DisableSuccess'))
      await loadTenants()
    } catch (error) {
      console.error('Failed to disable tenant:', error)
      useDiscreteApi.message.error(t('Tenant:DisableFailed'))
    }
  }

  // 获取租户已分配的配置
  const getAssignments = async (id: string): Promise<TenantAssignment | null> => {
    try {
      return await tenantApi.getAssignments(id)
    } catch (error) {
      console.error('Failed to get tenant assignments:', error)
      return null
    }
  }

  useKeepAliveRefresh(
    async () => {
      await Promise.all([loadTemplates(), loadTenants()])
    },
    loadTenants,
  )

  return {
    // 状态
    loading,
    submitting,
    tenants,
    templates,

    // 方法
    loadTenants,
    loadTemplates,
    createTenant,
    updateTenant,
    deleteTenant,
    deleteTenants,
    enableTenant,
    disableTenant,
    getAssignments,
  }
}

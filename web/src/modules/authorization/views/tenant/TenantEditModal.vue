<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="editingTenant ? t('Tenant:EditTitle') : t('Tenant:CreateTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
    transform-origin="center"
  >
    <n-scrollbar :style="{ maxHeight: scrollbarHeight }" trigger="none">
      <n-form ref="formRef" :model="formData" :rules="rules" label-placement="top">
        <div class="po-stack">
          <section class="po-card po-card--cream authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:SectionBasic') }}</h3>
            </header>
            <div class="po-form-grid">
              <PoFormItem :label="t('Tenant:Code')" path="code">
                <n-input
                  v-model:value="formData.code"
                  :placeholder="t('Tenant:CodePlaceholder')"
                  :disabled="!!editingTenant"
                />
              </PoFormItem>

              <PoFormItem :label="t('Tenant:Name')" path="name">
                <n-input v-model:value="formData.name" :placeholder="t('Tenant:NamePlaceholder')" />
              </PoFormItem>

              <PoFormItem :label="t('Tenant:AdminEmail')" path="adminEmail">
                <n-input v-model:value="formData.adminEmail" placeholder="admin@example.com" />
              </PoFormItem>

              <PoFormItem
                v-if="!editingTenant"
                :label="t('Tenant:AdminUserName')"
                :description="t('Tenant:AdminUserNamePlaceholder')"
                path="adminUserName"
              >
                <n-input v-model:value="formData.adminUserName" />
              </PoFormItem>

              <PoFormItem
                v-if="!editingTenant"
                class="po-span-2"
                :label="t('Tenant:AdminPassword')"
                :description="t('Tenant:AdminPasswordPlaceholder')"
                path="adminPassword"
              >
                <n-input
                  v-model:value="formData.adminPassword"
                  type="password"
                  show-password-on="click"
                />
              </PoFormItem>

              <PoFormItem
                class="po-span-2"
                :label="t('Tenant:ConnectionString')"
                :description="t('Tenant:ConnectionStringPlaceholder')"
                path="connectionString"
              >
                <n-input v-model:value="formData.connectionString" />
              </PoFormItem>
            </div>
          </section>

          <!-- 编辑模式下只修改基本信息 -->
          <section v-if="!editingTenant" class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Tenant:ConfigAssignment') }}</h3>
            </header>

            <div class="authz-assign">
              <div class="authz-assign__head">
                <b>{{ t('Tenant:SelectModules') }}</b>
                <span class="po-small po-muted">{{ formData.moduleIds.length }} / {{ templateModules.length }}</span>
                <n-checkbox
                  :checked="isAllModulesSelected"
                  :indeterminate="isSomeModulesSelected"
                  @update:checked="onSelectAllModules"
                >
                  {{ t('Tenant:SelectAll') }}
                </n-checkbox>
              </div>
              <n-checkbox-group v-model:value="formData.moduleIds">
                <div class="po-list authz-check-list">
                  <label v-for="mod in templateModules" :key="mod.id" class="po-list__item authz-check-item">
                    <n-checkbox :value="mod.id" />
                    <span class="po-list__main"><b>{{ mod.name }}</b></span>
                  </label>
                </div>
              </n-checkbox-group>
            </div>

            <div class="authz-assign">
              <div class="authz-assign__head">
                <b>{{ t('Tenant:SelectPermissions') }}</b>
                <span class="po-small po-muted">{{ formData.permissionIds.length }} / {{ templatePermissions.length }}</span>
                <n-checkbox
                  :checked="isAllPermissionsSelected"
                  :indeterminate="isSomePermissionsSelected"
                  @update:checked="onSelectAllPermissions"
                >
                  {{ t('Tenant:SelectAll') }}
                </n-checkbox>
              </div>
              <n-checkbox-group v-model:value="formData.permissionIds">
                <div class="po-list authz-check-list">
                  <label v-for="perm in templatePermissions" :key="perm.id" class="po-list__item authz-check-item">
                    <n-checkbox :value="perm.id" />
                    <span class="po-list__main">
                      <b>{{ perm.name }}</b>
                      <span class="po-mono">{{ perm.code }}</span>
                    </span>
                  </label>
                </div>
              </n-checkbox-group>
            </div>

            <div class="authz-assign">
              <div class="authz-assign__head">
                <b>{{ t('Tenant:SelectRoutes') }}</b>
                <span class="po-small po-muted">{{ formData.routeIds.length }} / {{ templateRoutes.length }}</span>
                <n-checkbox
                  :checked="isAllRoutesSelected"
                  :indeterminate="isSomeRoutesSelected"
                  @update:checked="onSelectAllRoutes"
                >
                  {{ t('Tenant:SelectAll') }}
                </n-checkbox>
              </div>
              <n-checkbox-group v-model:value="formData.routeIds">
                <div class="po-list authz-check-list">
                  <label v-for="route in templateRoutes" :key="route.id" class="po-list__item authz-check-item">
                    <n-checkbox :value="route.id" />
                    <span class="po-list__main">
                      <b>{{ route.title ? t(route.title) : route.name }}</b>
                      <span class="po-mono">{{ route.path }}</span>
                    </span>
                    <span v-if="route.menu" class="po-pill po-pill--lilac">{{ t('Field:Menu') }}</span>
                  </label>
                </div>
              </n-checkbox-group>
            </div>
          </section>
        </div>
      </n-form>
    </n-scrollbar>

    <template #footer>
      <n-space justify="end">
        <n-button secondary @click="$emit('update:show', false)">{{ t('Common:Cancel') }}</n-button>
        <n-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ editingTenant ? t('Common:Update') : t('Common:Create') }}
        </n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Tenant, TenantFormData, TenantTemplate } from '@/modules/authorization/api/tenant/tenantModels'
import { t } from '@/i18n'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import { scrollToFirstFormError } from '@/utils/formErrors'

interface Props {
  show: boolean
  editingTenant: Tenant | null
  submitting: boolean
  templates: TenantTemplate | null
}

interface Emits {
  (e: 'update:show', value: boolean): void
  (e: 'submit', data: TenantFormData): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const formRef = ref()

const formData = ref<TenantFormData>({
  name: '',
  code: '',
  adminEmail: '',
  connectionString: '',
  adminUserName: '',
  adminPassword: '',
  moduleIds: [],
  routeIds: [],
  permissionIds: [],
})

// 验证规则
const rules = {
  code: [
    { required: true, message: t('Tenant:CodeRequired'), trigger: 'blur' },
    { min: 2, max: 50, message: t('Tenant:CodeLengthHint'), trigger: 'blur' },
  ],
  name: [
    { required: true, message: t('Tenant:NameRequired'), trigger: 'blur' },
    { min: 2, max: 100, message: t('Tenant:NameLengthHint'), trigger: 'blur' },
  ],
  adminEmail: [
    { type: 'email', message: t('Authz:ValidateEmail'), trigger: 'blur' },
  ],
}

// 模板数据计算属性
const templateModules = computed(() => props.templates?.modules ?? [])
const templateRoutes = computed(() => props.templates?.routes ?? [])
const templatePermissions = computed(() => props.templates?.permissions ?? [])

// 全选状态 - 模块
const isAllModulesSelected = computed(
  () => templateModules.value.length > 0 && formData.value.moduleIds.length === templateModules.value.length,
)
const isSomeModulesSelected = computed(
  () => formData.value.moduleIds.length > 0 && formData.value.moduleIds.length < templateModules.value.length,
)
const onSelectAllModules = (checked: boolean) => {
  formData.value.moduleIds = checked ? templateModules.value.map((m) => m.id) : []
}

// 全选状态 - 权限
const isAllPermissionsSelected = computed(
  () => templatePermissions.value.length > 0 && formData.value.permissionIds.length === templatePermissions.value.length,
)
const isSomePermissionsSelected = computed(
  () => formData.value.permissionIds.length > 0 && formData.value.permissionIds.length < templatePermissions.value.length,
)
const onSelectAllPermissions = (checked: boolean) => {
  formData.value.permissionIds = checked ? templatePermissions.value.map((p) => p.id) : []
}

// 全选状态 - 路由
const isAllRoutesSelected = computed(
  () => templateRoutes.value.length > 0 && formData.value.routeIds.length === templateRoutes.value.length,
)
const isSomeRoutesSelected = computed(
  () => formData.value.routeIds.length > 0 && formData.value.routeIds.length < templateRoutes.value.length,
)
const onSelectAllRoutes = (checked: boolean) => {
  formData.value.routeIds = checked ? templateRoutes.value.map((r) => r.id) : []
}

// 监听编辑租户变化
watch(
  () => props.editingTenant,
  (tenant) => {
    if (tenant) {
      formData.value = {
        name: tenant.name,
        code: tenant.code,
        adminEmail: tenant.adminEmail ?? '',
        connectionString: tenant.connectionString ?? '',
        adminUserName: '',
        adminPassword: '',
        moduleIds: [],
        routeIds: [],
        permissionIds: [],
      }
    } else {
      formData.value = {
        name: '',
        code: '',
        adminEmail: '',
        connectionString: '',
        adminUserName: '',
        adminPassword: '',
        moduleIds: [],
        routeIds: [],
        permissionIds: [],
      }
    }
  },
)

// 对话框样式
const modalStyle = computed(() => ({
  width: '800px',
  maxHeight: '85vh',
}))

const scrollbarHeight = computed(() => 'calc(85vh - 180px)')

// 提交表单
const handleSubmit = async () => {
  try {
    await formRef.value?.validate()
    emit('submit', formData.value)
  } catch (error) {
    console.error('Form validation failed:', error)
    void scrollToFirstFormError(formRef.value)
  }
}
</script>

<style scoped>
.authz-modal-block {
  padding: 16px;
}

.authz-assign + .authz-assign {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px dashed var(--po-line);
}

.authz-assign__head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
  margin-bottom: 8px;
}

.authz-assign__head b {
  font-size: 13px;
  font-weight: var(--po-weight-strong);
}

.authz-check-list {
  max-height: 180px;
  overflow-y: auto;
}

.authz-check-item {
  align-items: center;
  cursor: pointer;
}

@media (max-width: 640px) {
  .po-form-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="editingRole ? t('Authz:RoleEditTitle') : t('Authz:RoleCreateTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
    transform-origin="center"
  >
    <n-scrollbar
      ref="modalScrollRef"
      :style="{ maxHeight: scrollbarHeight }"
      :theme-overrides="scrollbarThemeOverrides"
      trigger="none"
    >
      <n-form ref="formRef" :model="formData" :rules="rules" label-placement="top">
        <div class="po-stack">
          <section class="po-card po-card--cream authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:SectionBasic') }}</h3>
            </header>
            <div class="po-form-grid">
              <n-form-item :label="t('Field:RoleCode')" path="code">
                <n-input
                  v-model:value="formData.code"
                  :placeholder="t('Authz:PlaceholderRoleCode')"
                  :disabled="isBuiltIn"
                />
              </n-form-item>

              <n-form-item :label="t('Field:RoleName')" path="name">
                <n-input v-model:value="formData.name" :placeholder="t('Authz:PlaceholderRoleName')" />
              </n-form-item>

              <n-form-item class="po-span-2" :label="t('Common:Description')" path="description">
                <n-input
                  v-model:value="formData.description"
                  type="textarea"
                  :placeholder="t('Authz:PlaceholderDescription')"
                  :rows="3"
                />
              </n-form-item>
            </div>
          </section>

          <section v-if="!isBuiltIn" class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:AssignModules') }}</h3>
              <label class="po-search-box authz-assign-search">
                <n-icon :component="SearchOutline" />
                <input
                  v-model="moduleFilter"
                  type="search"
                  :placeholder="t('Authz:SearchAssignModules')"
                  :aria-label="t('Authz:SearchAssignModules')"
                />
              </label>
            </header>

            <div ref="moduleScrollRef" class="authz-assign-scroll">
              <n-form-item path="moduleIds" :show-label="false">
                <n-checkbox-group v-model:value="formData.moduleIds">
                  <div class="po-list authz-module-list">
                    <label
                      v-for="moduleItem in filteredModules"
                      :key="moduleItem.id"
                      class="po-list__item authz-module-item"
                    >
                      <n-checkbox :value="moduleItem.id" />
                      <span class="po-list__main">
                        <b>{{ moduleItem.name }}</b>
                        <span v-if="moduleItem.description">{{ moduleItem.description }}</span>
                      </span>
                    </label>
                    <p v-if="modules.length === 0" class="po-small po-muted">
                      {{ t('Authz:AssignModulesEmpty') }}
                    </p>
                    <p v-else-if="filteredModules.length === 0" class="po-small po-muted">
                      {{ t('Authz:AssignFilterEmpty') }}
                    </p>
                  </div>
                </n-checkbox-group>
              </n-form-item>
            </div>
          </section>

          <section v-if="!isBuiltIn" class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:AssignPermissions') }}</h3>
              <label class="po-search-box authz-assign-search">
                <n-icon :component="SearchOutline" />
                <input
                  v-model="permissionFilter"
                  type="search"
                  :placeholder="t('Authz:SearchAssignPermissions')"
                  :aria-label="t('Authz:SearchAssignPermissions')"
                />
              </label>
            </header>

            <div
              ref="permissionScrollRef"
              class="authz-assign-scroll authz-assign-scroll--tall"
            >
              <n-form-item path="permissionIds" :show-label="false">
                <n-checkbox-group v-model:value="formData.permissionIds">
                  <div class="po-stack authz-permission-groups">
                    <div
                      v-for="group in permissionGroups"
                      :key="group.key"
                      class="authz-permission-group"
                    >
                      <p class="authz-permission-group__title">{{ group.label }}</p>
                      <div class="authz-permission-list">
                        <label
                          v-for="permission in group.items"
                          :key="permission.id"
                          class="po-list__item authz-permission-item"
                        >
                          <n-checkbox :value="permission.id" />
                          <span class="po-list__main">
                            <b>{{ permission.name }}</b>
                            <span class="po-mono">{{ permission.code }}</span>
                          </span>
                        </label>
                      </div>
                    </div>
                    <p v-if="permissions.length === 0" class="po-small po-muted">
                      {{ t('Authz:AssignPermissionsEmpty') }}
                    </p>
                    <p v-else-if="permissionGroups.length === 0" class="po-small po-muted">
                      {{ t('Authz:AssignFilterEmpty') }}
                    </p>
                  </div>
                </n-checkbox-group>
              </n-form-item>
            </div>
          </section>
        </div>
      </n-form>
    </n-scrollbar>

    <template #footer>
      <n-space justify="end">
        <n-button secondary @click="$emit('update:show', false)">{{ t('Common:Cancel') }}</n-button>
        <n-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ editingRole ? t('Common:Update') : t('Common:Create') }}
        </n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { SearchOutline } from '@vicons/ionicons5'
import type { Role, RoleFormData } from '@/modules/authorization/api/role/roleModels'
import type { Module } from '@/modules/authorization/api/module/moduleModels'
import type { Permission } from '@/modules/authorization/api/permission/permissionModels'
import { t } from '@/i18n'
import { scrollToFirstFormError } from '@/utils/formErrors'

interface Props {
  show: boolean
  editingRole: Role | null
  submitting: boolean
  modules: Module[]
  roleModules?: string[]
  permissions: Permission[]
  rolePermissions?: string[]
}

interface Emits {
  (e: 'update:show', value: boolean): void
  (e: 'submit', data: RoleFormData): void
}

// 滚动条主题覆盖
const scrollbarThemeOverrides = {
  width: '0px',
  height: '4px',
  borderRadius: '2px',
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const formRef = ref()

const isBuiltIn = computed(() => props.editingRole?.isBuiltIn === true)

const formData = ref<RoleFormData>({
  code: '',
  name: '',
  description: '',
  moduleIds: [],
  permissionIds: [],
})

const SUPER_PERMISSION = '*:*:*'

const moduleFilter = ref('')
const permissionFilter = ref('')
const moduleScrollRef = ref<HTMLElement | null>(null)
const permissionScrollRef = ref<HTMLElement | null>(null)
const modalScrollRef = ref<{ scrollTo?: (options: { top: number }) => void } | null>(null)

const normalizedModuleFilter = computed(() => moduleFilter.value.trim().toLowerCase())
const normalizedPermissionFilter = computed(() => permissionFilter.value.trim().toLowerCase())

const filteredModules = computed(() => {
  const keyword = normalizedModuleFilter.value
  if (!keyword) return props.modules
  return props.modules.filter((module) =>
    `${module.name} ${module.description ?? ''}`.toLowerCase().includes(keyword),
  )
})

const permissionGroups = computed(() => {
  const groupLabels: Record<string, string> = {
    system: t('Authz:PermissionGroupSystem'),
    approval: t('Authz:PermissionGroupApproval'),
  }

  const keyword = normalizedPermissionFilter.value
  const permissions = keyword
    ? props.permissions.filter((permission) =>
        `${permission.name} ${permission.code}`.toLowerCase().includes(keyword),
      )
    : props.permissions

  const groups = new Map<string, { key: string; label: string; items: Permission[] }>()
  for (const permission of permissions) {
    const key =
      permission.code === SUPER_PERMISSION ? 'super' : (permission.code.split(':')[0] ?? 'other')
    const label =
      key === 'super'
        ? t('Authz:PermissionGroupSuper')
        : (groupLabels[key] ?? key)
    const group = groups.get(key) ?? { key, label, items: [] }
    group.items.push(permission)
    groups.set(key, group)
  }

  return [...groups.values()]
})

// 验证规则
const rules = {
  code: [
    { required: true, message: t('Authz:ValidateRoleCodeRequired'), trigger: 'blur' },
    { min: 2, max: 50, message: t('Authz:ValidateRoleCodeLength'), trigger: 'blur' },
  ],
  name: [
    { required: true, message: t('Authz:ValidateRoleNameRequired'), trigger: 'blur' },
    { min: 2, max: 50, message: t('Authz:ValidateRoleNameLength'), trigger: 'blur' },
  ],
}

// 监听编辑角色变化
watch(
  () => props.editingRole,
  (role) => {
    if (role) {
      formData.value = {
        code: role.code,
        name: role.name,
        description: role.description || '',
        moduleIds: props.roleModules || [],
        permissionIds: props.rolePermissions || [],
      }
    } else {
      formData.value = {
        code: '',
        name: '',
        description: '',
        moduleIds: [],
        permissionIds: [],
      }
    }
  },
)

// 监听角色模块变化（用于编辑模式）
watch(
  () => props.roleModules,
  (moduleIds) => {
    if (props.editingRole && moduleIds) {
      formData.value.moduleIds = moduleIds
    }
  },
)

watch(
  () => props.rolePermissions,
  (permissionIds) => {
    if (props.editingRole && permissionIds) {
      formData.value.permissionIds = permissionIds
    }
  },
)

watch(moduleFilter, () => moduleScrollRef.value?.scrollTo({ top: 0 }))
watch(permissionFilter, () => permissionScrollRef.value?.scrollTo({ top: 0 }))

// 每次打开弹窗都回到顶部并清空筛选，避免沿用上一次的滚动位置
watch(
  () => props.show,
  (show) => {
    if (!show) return
    moduleFilter.value = ''
    permissionFilter.value = ''
    void nextTick(() => {
      moduleScrollRef.value?.scrollTo({ top: 0 })
      permissionScrollRef.value?.scrollTo({ top: 0 })
      modalScrollRef.value?.scrollTo?.({ top: 0 })
    })
  },
)

// 对话框样式
const modalStyle = computed(() => {
  return {
    width: '700px',
    maxHeight: '85vh',
  }
})

// 滚动条高度
const scrollbarHeight = computed(() => {
  return 'calc(85vh - 180px)'
})

// 提交表单
const handleSubmit = async () => {
  try {
    await formRef.value?.validate()

    const payload: RoleFormData = {
      code: formData.value.code,
      name: formData.value.name,
      description: formData.value.description,
    }

    if (!isBuiltIn.value) {
      payload.moduleIds = formData.value.moduleIds
      payload.permissionIds = formData.value.permissionIds
    }

    emit('submit', payload)
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

.authz-assign-search {
  flex: 0 1 220px;
  min-width: 160px;
  max-width: 220px;
  min-height: 34px;
  padding: 0 12px;
}

.authz-assign-search input {
  min-height: 30px;
  font-size: 13px;
}

.authz-assign-search :is(svg, .n-icon) {
  width: 16px;
  height: 16px;
}

.authz-assign-scroll {
  width: 100%;
  max-height: 220px;
  overflow-y: auto;
  padding-right: 6px;
}

.authz-assign-scroll--tall {
  max-height: 320px;
}

.authz-module-list {
  width: 100%;
}

.authz-module-item {
  align-items: center;
  cursor: pointer;
}

.authz-module-item,
.authz-permission-item {
  gap: 8px;
  padding-block: 5px;
}

.authz-module-item:first-child,
.authz-permission-item:first-child {
  padding-top: 5px;
}

.authz-module-item:last-child,
.authz-permission-item:last-child {
  padding-bottom: 5px;
}

.authz-module-item .po-list__main,
.authz-permission-item .po-list__main {
  gap: 1px;
}

.authz-module-item .po-list__main b,
.authz-permission-item .po-list__main b {
  line-height: 1.25;
}

.authz-module-item .po-list__main span {
  font-size: 11.5px;
  line-height: 1.35;
}

.authz-permission-groups {
  gap: 8px;
}

.authz-permission-group__title {
  position: sticky;
  top: 0;
  z-index: 1;
  margin-bottom: 2px;
  padding-block: 2px;
  background: var(--po-paper);
  color: var(--po-ink-soft);
  font-size: 12px;
  font-weight: var(--po-weight-strong);
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.authz-permission-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 2px 14px;
}

.authz-permission-item {
  align-items: center;
  cursor: pointer;
}

.authz-permission-item .po-list__main b {
  font-size: 12.5px;
}

.authz-permission-item .po-list__main span {
  font-size: 10.5px;
  line-height: 1.3;
}

@media (max-width: 640px) {
  .authz-permission-list {
    grid-template-columns: minmax(0, 1fr);
  }

  .po-form-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

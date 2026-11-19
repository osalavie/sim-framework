<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="editingRoute ? t('Authz:RouteEditTitle') : t('Authz:RouteCreateTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
    transform-origin="center"
  >
    <n-scrollbar :style="{ maxHeight: scrollbarHeight }"
    :theme-overrides="scrollbarThemeOverrides"
    trigger="none">
      <n-form ref="formRef" :model="formData" :rules="rules" label-placement="top">
        <div class="po-stack">
          <section class="po-card po-card--cream authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:SectionBasic') }}</h3>
            </header>
            <div class="po-form-grid">
              <PoFormItem :label="t('Authz:FieldParentRoute')" path="parentId">
                <n-select
                  v-model:value="formData.parentId"
                  :placeholder="t('Authz:PlaceholderParentRoute')"
                  :options="parentRouteOptions"
                  clearable
                />
              </PoFormItem>

              <PoFormItem :label="t('Authz:FieldRouteName')" path="name">
                <n-input v-model:value="formData.name" :placeholder="t('Authz:PlaceholderRouteName')" />
              </PoFormItem>

              <PoFormItem :label="t('Authz:FieldRoutePath')" path="path">
                <n-input v-model:value="formData.path" :placeholder="t('Authz:PlaceholderRoutePath')" />
              </PoFormItem>

              <PoFormItem :label="t('Authz:FieldComponentPath')" path="component">
                <n-input v-model:value="formData.component" :placeholder="t('Authz:PlaceholderComponentPath')" />
              </PoFormItem>

              <PoFormItem :label="t('Authz:FieldRedirect')" path="redirect">
                <n-input v-model:value="formData.redirect" :placeholder="t('Authz:PlaceholderRedirect')" />
              </PoFormItem>
            </div>
          </section>

          <section class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:SectionMeta') }}</h3>
            </header>
            <div class="po-form-grid">
              <PoFormItem :label="t('Authz:FieldMetaTitle')" path="meta.title">
                <n-input v-model:value="formData.meta.title" :placeholder="t('Authz:PlaceholderMetaTitle')" />
              </PoFormItem>

              <PoFormItem :label="t('Common:Icon')" path="meta.icon">
                <n-input v-model:value="formData.meta.icon" :placeholder="t('Authz:PlaceholderIcon')" />
              </PoFormItem>

              <PoFormItem :label="t('Common:Order')" path="meta.order">
                <n-input-number v-model:value="formData.meta.order" :min="0" style="width: 100%" />
              </PoFormItem>

              <PoFormItem
                class="po-span-2"
                :label="t('Authz:FieldPermissionCode')"
                :description="t('Authz:HintPermissionCodeList')"
                path="meta.permissionCode"
              >
                <div class="authz-permission-code">
                  <n-select
                    v-model:value="permissionCodeList"
                    multiple
                    filterable
                    clearable
                    :max-tag-count="'responsive'"
                    :options="permissionCodeOptions"
                    :render-tag="renderPermissionTag"
                    :placeholder="t('Authz:PlaceholderPermissionCodeList')"
                  />
                  <div class="authz-permission-code__footer">
                    <SegmentedSwitch
                      v-model="permissionMatchMode"
                      :options="permissionMatchModeOptions"
                      :aria-label="t('Authz:FieldPermissionMatchMode')"
                    />
                  </div>
                </div>
              </PoFormItem>

              <PoFormItem class="po-span-2" :label="t('Authz:FieldSwitchGroup')">
                <div class="authz-switch-grid">
                  <label class="authz-switch-item">
                    <n-switch v-model:value="formData.meta.menu" />
                    <span>{{ t('Field:Menu') }}</span>
                  </label>
                  <label class="authz-switch-item">
                    <n-switch v-model:value="formData.meta.tab" />
                    <span>{{ t('Field:Tab') }}</span>
                  </label>
                  <label class="authz-switch-item">
                    <n-switch v-model:value="formData.meta.keepAlive" />
                    <span>{{ t('Authz:FieldKeepAlive') }}</span>
                  </label>
                  <label class="authz-switch-item">
                    <n-switch v-model:value="formData.meta.pinned" />
                    <span>{{ t('Authz:FieldPinned') }}</span>
                  </label>
                  <label class="authz-switch-item">
                    <n-switch v-model:value="formData.meta.anonymous" />
                    <span>{{ t('Authz:FieldAnonymous') }}</span>
                  </label>
                  <label class="authz-switch-item">
                    <n-switch v-model:value="formData.meta.disabled" />
                    <span>{{ t('Authz:FieldDisabled') }}</span>
                  </label>
                </div>
              </PoFormItem>
            </div>
          </section>
        </div>
      </n-form>
    </n-scrollbar>

    <template #footer>
      <n-space justify="end">
        <n-button secondary @click="$emit('update:show', false)">{{ t('Common:Cancel') }}</n-button>
        <n-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ editingRoute ? t('Common:Update') : t('Common:Create') }}
        </n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import { h, ref, computed, watch } from 'vue'
import type { Route, RouteFormData } from '@/modules/authorization/api/route/routeModels'
import type { Permission } from '@/modules/authorization/api/permission/permissionModels'
import type { SelectOption } from 'naive-ui'
import { NTag } from 'naive-ui'
import { SegmentedSwitch } from '@/components/SegmentedSwitch'
import { splitPermissionCodes } from '@/utils/permissionCodes'
import { scrollToFirstFormError } from '@/utils/formErrors'
import {
  buildPermissionCodeGroups,
  joinPermissionCodes,
  type PermissionCodeOption,
} from '@/utils/permissionCodeOptions'
import { t } from '@/i18n'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'

interface Props {
  show: boolean
  editingRoute: Route | null
  routes: Route[]
  permissions: Permission[]
  submitting: boolean
}

interface Emits {
  (e: 'update:show', value: boolean): void
  (e: 'submit', data: RouteFormData): void
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

// meta 在表单中始终存在，避免模板中大量可选链
type RouteFormDataWithMeta = Omit<RouteFormData, 'meta'> & { meta: NonNullable<RouteFormData['meta']> }

const formData = ref<RouteFormDataWithMeta>({
  id: '',
  parentId: undefined,
  name: '',
  path: '',
  component: '',
  redirect: '',
  meta: {
    title: '',
    icon: '',
    menu: true,
    tab: true,
    keepAlive: false,
    pinned: false,
    anonymous: false,
    disabled: false,
    order: 0,
    permissionCode: '',
    permissionRequireAll: false,
  },
})

const permissionCodeList = computed<string[]>({
  get: () => splitPermissionCodes(formData.value.meta.permissionCode),
  set: (codes) => {
    formData.value.meta.permissionCode = joinPermissionCodes(codes)
  },
})

const permissionMatchMode = computed<'any' | 'all'>({
  get: () => (formData.value.meta.permissionRequireAll ? 'all' : 'any'),
  set: (mode) => {
    formData.value.meta.permissionRequireAll = mode === 'all'
  },
})

const permissionMatchModeOptions = computed(() => [
  { label: t('Authz:PermissionMatchAny'), value: 'any' as const },
  { label: t('Authz:PermissionMatchAll'), value: 'all' as const },
])

const renderPermissionTag = ({
  option,
  handleClose,
}: {
  option: SelectOption
  handleClose: () => void
}) =>
  h(
    NTag,
    { closable: true, onClose: handleClose },
    { default: () => (option as PermissionCodeOption).name ?? String(option.value ?? '') },
  )

const permissionCodeOptions = computed(() => {
  const groupLabels: Record<string, string> = {
    super: t('Authz:PermissionGroupSuper'),
    system: t('Authz:PermissionGroupSystem'),
    approval: t('Authz:PermissionGroupApproval'),
  }

  return buildPermissionCodeGroups(props.permissions).map((group) => ({
    type: 'group' as const,
    key: group.module,
    label: groupLabels[group.module] ?? group.module,
    children: group.options,
  }))
})

// 验证规则
const rules = {
  name: [
    { required: true, message: t('Authz:ValidateRouteNameRequired'), trigger: 'blur' },
    { min: 2, max: 100, message: t('Authz:ValidateRouteNameLength'), trigger: 'blur' },
  ],
  path: [
    { required: true, message: t('Authz:ValidateRoutePathRequired'), trigger: 'blur' },
    { min: 1, max: 200, message: t('Authz:ValidateRoutePathLength'), trigger: 'blur' },
    {
      validator: (_rule: unknown, value: string) => {
        // 路径唯一性验证
        if (!value) return true

        // 如果是编辑模式，排除当前路由
        const existingRoute = props.routes.find(r =>
          r.path === value && r.id !== props.editingRoute?.id
        )

        if (existingRoute) {
          return new Error(t('Authz:ValidateRoutePathUnique'))
        }
        return true
      },
      trigger: 'blur',
    },
  ],
  'meta.title': [{ max: 100, message: t('Authz:ValidateMetaTitleLength'), trigger: 'blur' }],
  'meta.icon': [{ max: 100, message: t('Authz:ValidateMetaIconLength'), trigger: 'blur' }],
  'meta.permissionCode': [
    { max: 512, message: t('Authz:ValidateRoutePermissionCodeLength'), trigger: 'blur' },
  ],
}

// 父路由选项（排除当前编辑的路由及其子路由）
const parentRouteOptions = computed<SelectOption[]>(() => {
  const excludeIds = new Set<string>()

  // 如果是编辑模式，排除当前路由及其所有子路由
  if (props.editingRoute) {
    const collectChildIds = (route: Route) => {
      excludeIds.add(route.id)
      if (route.children) {
        route.children.forEach(child => collectChildIds(child))
      }
    }
    collectChildIds(props.editingRoute)
  }

  // 递归构建选项（带层级缩进）
  const buildOptions = (routes: Route[], level = 0): SelectOption[] => {
    const options: SelectOption[] = []
    routes.forEach(route => {
      if (!excludeIds.has(route.id)) {
        const indent = '　'.repeat(level) // 使用全角空格缩进
        options.push({
          label: `${indent}${route.name}`,
          value: route.id,
        })
        if (route.children && route.children.length > 0) {
          options.push(...buildOptions(route.children, level + 1))
        }
      }
    })
    return options
  }

  // 从 routes 构建树形结构
  const treeRoutes: Route[] = []
  const routeMap = new Map<string, Route>()

  props.routes.forEach(route => {
    routeMap.set(route.id, { ...route, children: [] })
  })

  props.routes.forEach(route => {
    const currentRoute = routeMap.get(route.id)!
    if (route.parentId) {
      const parent = routeMap.get(route.parentId)
      if (parent) {
        if (!parent.children) parent.children = []
        parent.children.push(currentRoute)
      } else {
        treeRoutes.push(currentRoute)
      }
    } else {
      treeRoutes.push(currentRoute)
    }
  })

  return buildOptions(treeRoutes)
})

// 监听编辑路由变化
watch(
  () => props.editingRoute,
  (route) => {
    if (route) {
      formData.value = {
        id: route.id,
        parentId: route.parentId,
        name: route.name,
        path: route.path,
        component: route.component || '',
        redirect: route.redirect || '',
        meta: {
          title: route.meta?.title || '',
          icon: route.meta?.icon || '',
          menu: route.meta?.menu ?? true,
          tab: route.meta?.tab ?? true,
          keepAlive: route.meta?.keepAlive ?? false,
          pinned: route.meta?.pinned ?? false,
          anonymous: route.meta?.anonymous ?? false,
          disabled: route.meta?.disabled ?? false,
          order: route.meta?.order ?? 0,
          permissionCode: route.meta?.permissionCode || '',
          permissionRequireAll: route.meta?.permissionRequireAll ?? false,
        },
      }
    } else {
      formData.value = {
        id: '',
        parentId: undefined,
        name: '',
        path: '',
        component: '',
        redirect: '',
        meta: {
          title: '',
          icon: '',
          menu: true,
          tab: true,
          keepAlive: false,
          pinned: false,
          anonymous: false,
          disabled: false,
          order: 0,
          permissionCode: '',
          permissionRequireAll: false,
        },
      }
    }
  },
)

// 对话框样式
const modalStyle = computed(() => {
  return {
    width: '760px',
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

.authz-permission-code {
  display: grid;
  gap: 4px;
  width: 100%;
}

.authz-permission-code__footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px 12px;
}


.authz-switch-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 10px 18px;
  width: 100%;
}

.authz-switch-item {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--po-ink);
  font-size: 12.5px;
  font-weight: 600;
}

@media (max-width: 640px) {
  .po-form-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

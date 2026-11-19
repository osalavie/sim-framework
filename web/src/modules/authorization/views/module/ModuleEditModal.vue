<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="editingModule ? t('Authz:ModuleEditTitle') : t('Authz:ModuleCreateTitle')"
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
              <n-form-item :label="t('Common:Name')" path="name">
                <n-input v-model:value="formData.name" :placeholder="t('Authz:PlaceholderModuleName')" />
              </n-form-item>

              <n-form-item :label="t('Field:Version')" path="version">
                <n-input v-model:value="formData.version" :placeholder="t('Authz:PlaceholderModuleVersion')" />
              </n-form-item>

              <n-form-item :label="t('Common:Icon')" path="icon">
                <n-input v-model:value="formData.icon" :placeholder="t('Authz:PlaceholderIcon')" />
              </n-form-item>

              <n-form-item :label="t('Common:Order')" path="order">
                <n-input-number v-model:value="formData.order" :min="0" style="width: 100%" />
              </n-form-item>

              <n-form-item :label="t('Field:Layout')" path="layout">
                <n-input v-model:value="formData.layout" :placeholder="t('Authz:PlaceholderLayout')" />
              </n-form-item>

              <n-form-item :label="t('Authz:FieldEmptyRedirect')" path="emptyRedirect">
                <n-input v-model:value="formData.emptyRedirect" :placeholder="t('Authz:PlaceholderEmptyRedirect')" />
              </n-form-item>
            </div>
          </section>

          <section class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:SectionSettings') }}</h3>
            </header>
            <div class="po-form-grid">
              <n-form-item :label="t('Field:Status')" path="enabled">
                <n-switch v-model:value="formData.enabled">
                  <template #checked>{{ t('Common:Enabled') }}</template>
                  <template #unchecked>{{ t('Common:Disabled') }}</template>
                </n-switch>
              </n-form-item>

              <n-form-item class="po-span-2" :label="t('Common:Description')" path="description">
                <n-input
                  v-model:value="formData.description"
                  type="textarea"
                  :placeholder="t('Authz:PlaceholderModuleDesc')"
                  :rows="3"
                />
              </n-form-item>
            </div>
          </section>

          <section class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:AssignRoutes') }}</h3>
              <span class="po-small po-muted">
                {{ t('Authz:AssignRoutesSelected', { count: selectedRouteCount }) }}
              </span>
            </header>
            <div class="authz-route-tree">
              <n-tree
                block-line
                cascade
                checkable
                :selectable="false"
                default-expand-all
                :data="routeTreeData"
                :checked-keys="formData.routeIds"
                :indeterminate-keys="indeterminateRouteIds"
                @update:checked-keys="handleCheckedKeysChange"
                @update:indeterminate-keys="handleIndeterminateKeysChange"
              />
              <p v-if="routeTreeData.length === 0" class="po-small po-muted">
                {{ t('Authz:AssignRoutesEmpty') }}
              </p>
            </div>
          </section>
        </div>
      </n-form>
    </n-scrollbar>

    <template #footer>
      <n-space justify="end">
        <n-button secondary @click="$emit('update:show', false)">{{ t('Common:Cancel') }}</n-button>
        <n-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ editingModule ? t('Common:Update') : t('Common:Create') }}
        </n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Module, ModuleFormData } from '@/modules/authorization/api/module/moduleModels'
import type { Route } from '@/modules/authorization/api/route/routeModels'
import { t } from '@/i18n'
import { scrollToFirstFormError } from '@/utils/formErrors'

interface Props {
  show: boolean
  editingModule: Module | null
  submitting: boolean
  routes: Route[]
}

interface Emits {
  (e: 'update:show', value: boolean): void
  (e: 'submit', data: ModuleFormData): void
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

const formData = ref<ModuleFormData>({
  name: '',
  description: '',
  version: '',
  enabled: true,
  icon: '',
  order: 0,
  layout: '',
  emptyRedirect: '',
  routeIds: [],
})

interface RouteTreeNode {
  key: string
  label: string
  children?: RouteTreeNode[]
}

const toRouteTree = (routes: Route[]): RouteTreeNode[] =>
  routes.map((route) => ({
    key: route.id,
    label: `${route.name} · ${route.path}`,
    children: route.children?.length ? toRouteTree(route.children) : undefined,
  }))

const routeTreeData = computed(() => toRouteTree(props.routes))

const routeChildrenMap = computed(() => {
  const map = new Map<string, string[]>()
  const collect = (routes: Route[]) => {
    for (const route of routes) {
      map.set(route.id, (route.children ?? []).map((child) => child.id))
      if (route.children?.length) {
        collect(route.children)
      }
    }
  }
  collect(props.routes)
  return map
})

/** 原生级联树的半选节点；父级半选同样属于模块关联。 */
const indeterminateRouteIds = ref<string[]>([])

const selectedRouteCount = computed(
  () => new Set([...(formData.value.routeIds ?? []), ...indeterminateRouteIds.value]).size,
)

const handleCheckedKeysChange = (keys: Array<string | number>) => {
  formData.value.routeIds = keys.map(String)
}

const handleIndeterminateKeysChange = (keys: Array<string | number>) => {
  indeterminateRouteIds.value = keys.map(String)
}

/** 由模块已有关联推导出级联树的勾选/半选状态。 */
const applyRouteSelection = (selectedRouteIds: string[]) => {
  const selected = new Set(selectedRouteIds)
  const checked: string[] = []
  const indeterminate: string[] = []

  for (const id of selected) {
    const children = routeChildrenMap.value.get(id) ?? []
    const selectedChildCount = children.filter((childId) => selected.has(childId)).length
    if (children.length > 0 && selectedChildCount > 0 && selectedChildCount < children.length) {
      indeterminate.push(id)
    } else {
      checked.push(id)
    }
  }

  formData.value.routeIds = checked
  indeterminateRouteIds.value = indeterminate
}

// 验证规则
const rules = {
  name: [
    { required: true, message: t('Authz:ValidateModuleNameRequired'), trigger: 'blur' },
    { min: 2, max: 50, message: t('Authz:ValidateModuleNameLength'), trigger: 'blur' },
  ],
  order: [
    { required: true, message: t('Authz:ValidateOrderRequired'), trigger: 'blur', type: 'number' },
  ],
}

// 监听编辑模块变化
watch(
  () => props.editingModule,
  (module) => {
    if (module) {
      formData.value = {
        name: module.name,
        description: module.description || '',
        version: module.version || '',
        enabled: module.enabled,
        icon: module.icon || '',
        order: module.order,
        layout: module.layout || '',
        emptyRedirect: module.emptyRedirect || '',
        routeIds: [],
      }
      indeterminateRouteIds.value = []
      applyRouteSelection(module.routeIds ?? [])
    } else {
      formData.value = {
        name: '',
        description: '',
        version: '',
        enabled: true,
        icon: '',
        order: 0,
        layout: '',
        emptyRedirect: '',
        routeIds: [],
      }
      indeterminateRouteIds.value = []
    }
  },
)

// 路由树异步加载完成后再推导一次勾选/半选状态
watch(
  () => props.routes,
  () => {
    if (props.show && props.editingModule && props.routes.length > 0) {
      applyRouteSelection(props.editingModule.routeIds ?? [])
    }
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
    const routeIds = [
      ...new Set([...(formData.value.routeIds ?? []), ...indeterminateRouteIds.value]),
    ]
    emit('submit', { ...formData.value, routeIds })
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

.authz-route-tree {
  max-height: 360px;
  overflow-y: auto;
  padding-right: 6px;
}

@media (max-width: 640px) {
  .po-form-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

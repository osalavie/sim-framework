<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="editingPermission ? t('Authz:PermissionEditTitle') : t('Authz:PermissionCreateTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
    transform-origin="center"
  >
    <n-scrollbar :style="{ maxHeight: scrollbarHeight }"
    :theme-overrides="scrollbarThemeOverrides"
    trigger="none">
      <n-form ref="formRef" :model="formData" :rules="rules" label-placement="top">
        <section class="po-card po-card--cream authz-modal-block">
          <header class="po-card__head">
            <h3>{{ t('Authz:SectionBasic') }}</h3>
          </header>
          <div class="po-form-grid">
            <n-form-item :label="t('Field:PermissionName')" path="name">
              <n-input v-model:value="formData.name" :placeholder="t('Authz:PlaceholderPermissionName')" />
            </n-form-item>

            <n-form-item :label="t('Field:PermissionCode')" path="code">
              <n-input v-model:value="formData.code" :placeholder="t('Authz:PlaceholderPermissionCode')" />
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
      </n-form>
    </n-scrollbar>

    <template #footer>
      <n-space justify="end">
        <n-button secondary @click="$emit('update:show', false)">{{ t('Common:Cancel') }}</n-button>
        <n-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ editingPermission ? t('Common:Update') : t('Common:Create') }}
        </n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Permission, PermissionFormData } from '@/modules/authorization/api/permission/permissionModels'
import { t } from '@/i18n'
import { scrollToFirstFormError } from '@/utils/formErrors'

interface Props {
  show: boolean
  editingPermission: Permission | null
  submitting: boolean
}

interface Emits {
  (e: 'update:show', value: boolean): void
  (e: 'submit', data: PermissionFormData): void
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

const formData = ref<PermissionFormData>({
  name: '',
  code: '',
  description: '',
})

// 验证规则
const rules = {
  name: [
    { required: true, message: t('Authz:ValidatePermissionNameRequired'), trigger: 'blur' },
    { min: 2, max: 50, message: t('Authz:ValidatePermissionNameLength'), trigger: 'blur' },
  ],
  code: [
    { required: true, message: t('Authz:ValidatePermissionCodeRequired'), trigger: 'blur' },
    { min: 2, max: 100, message: t('Authz:ValidatePermissionCodeLength'), trigger: 'blur' },
    {
      pattern: /^[A-Za-z0-9._-]+$/,
      message: t('Authz:ValidatePermissionCodePattern'),
      trigger: 'blur',
    },
  ],
}

// 监听编辑权限变化
watch(
  () => props.editingPermission,
  (permission) => {
    if (permission) {
      formData.value = {
        name: permission.name,
        code: permission.code,
        description: permission.description || '',
      }
    } else {
      formData.value = {
        name: '',
        code: '',
        description: '',
      }
    }
  },
)

// 对话框样式
const modalStyle = computed(() => {
  return {
    width: '600px',
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

@media (max-width: 640px) {
  .po-form-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

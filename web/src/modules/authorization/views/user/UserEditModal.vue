<template>
  <n-modal
    :show="show"
    @update:show="$emit('update:show', $event)"
    preset="card"
    :title="editingUser ? t('Authz:UserEditTitle') : t('Authz:UserCreateTitle')"
    :style="modalStyle"
    :segmented="{ content: 'soft' }"
    transform-origin="center"
  >
    <div class="authz-modal-scroll" :style="{ maxHeight: scrollbarHeight }">
      <n-form ref="formRef" :model="formData" :rules="rules" label-placement="top">
        <div class="po-stack">
          <section class="po-card po-card--cream authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:SectionAccount') }}</h3>
            </header>
            <div class="po-form-grid">
              <n-form-item :label="t('Field:Username')" path="userName">
                <n-input
                  v-model:value="formData.userName"
                  :disabled="isBuiltInAccount"
                  :placeholder="t('Authz:PlaceholderUsername')"
                />
              </n-form-item>

              <n-form-item :label="t('Field:Nickname')" path="nickName">
                <n-input
                  v-model:value="formData.nickName"
                  :placeholder="t('Authz:PlaceholderNickname')"
                  :maxlength="20"
                  show-count
                />
              </n-form-item>

              <n-form-item :label="t('Field:FullName')" path="name">
                <n-input v-model:value="formData.name" :placeholder="t('Authz:PlaceholderFullName')" />
              </n-form-item>
            </div>
          </section>

          <section class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:SectionContact') }}</h3>
            </header>
            <div class="po-form-grid">
              <n-form-item :label="t('Field:Email')" path="email">
                <n-input v-model:value="formData.email" :placeholder="t('Authz:PlaceholderEmail')" />
              </n-form-item>

              <n-form-item :label="t('Field:Phone')" path="phone">
                <n-input v-model:value="formData.phone" :placeholder="t('Authz:PlaceholderPhone')" />
              </n-form-item>

              <n-form-item :label="t('Field:Gender')" path="sex">
                <n-radio-group v-model:value="formData.sex">
                  <n-radio :value="0">{{ t('Authz:SexUnknown') }}</n-radio>
                  <n-radio :value="1">{{ t('Authz:SexMale') }}</n-radio>
                  <n-radio :value="2">{{ t('Authz:SexFemale') }}</n-radio>
                </n-radio-group>
              </n-form-item>

              <n-form-item :label="t('Authz:FieldAddress')" path="address">
                <n-input v-model:value="formData.address" :placeholder="t('Authz:PlaceholderAddress')" />
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

          <section v-if="showRoleAssignment" class="po-card authz-modal-block">
            <header class="po-card__head">
              <h3>{{ t('Authz:AssignRoles') }}</h3>
              <label class="po-search-box authz-assign-search">
                <n-icon :component="SearchOutline" />
                <input
                  v-model="roleFilter"
                  type="search"
                  :placeholder="t('Authz:SearchAssignRoles')"
                  :aria-label="t('Authz:SearchAssignRoles')"
                />
              </label>
            </header>

            <div ref="roleScrollRef" class="authz-role-scroll">
              <n-form-item path="roleIds" :show-label="false">
                <n-checkbox-group v-model:value="formData.roleIds" class="authz-role-group">
                  <div class="po-list authz-role-list">
                    <label
                      v-for="role in filteredRoles"
                      :key="role.id"
                      class="po-list__item authz-role-item"
                    >
                      <n-checkbox :value="role.id" />
                      <span class="po-list__main">
                        <b>{{ role.name }}</b>
                        <span>{{ role.code }}</span>
                      </span>
                    </label>
                    <p v-if="roles.length === 0" class="po-small po-muted">
                      {{ t('Authz:AssignRolesEmpty') }}
                    </p>
                    <p v-else-if="filteredRoles.length === 0" class="po-small po-muted">
                      {{ t('Authz:AssignFilterEmpty') }}
                    </p>
                  </div>
                </n-checkbox-group>
              </n-form-item>
            </div>
          </section>
        </div>
      </n-form>
    </div>

    <template #footer>
      <n-space justify="end">
        <n-button secondary @click="$emit('update:show', false)">{{ t('Common:Cancel') }}</n-button>
        <n-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ editingUser ? t('Common:Update') : t('Common:Create') }}
        </n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { SearchOutline } from '@vicons/ionicons5'
import type { FormRules } from 'naive-ui'
import type { User, UserFormData } from '@/modules/authorization/api/user/userModels'
import type { Role } from '@/modules/authorization/api/role/roleModels'
import { useResponsiveModal } from '@/hooks/useResponsiveModal'
import { useUserStore } from '@/stores/user'
import { t } from '@/i18n'
import { createDebouncedNicknameChecker } from '@/utils/nicknameAvailability'
import { scrollToFirstFormError } from '@/utils/formErrors'

interface Props {
  show: boolean
  editingUser: User | null
  submitting: boolean
  roles: Role[]
}

interface Emits {
  (e: 'update:show', value: boolean): void
  (e: 'submit', data: UserFormData): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const checkNicknameAvailability = createDebouncedNicknameChecker()

// 响应式模态框
const { modalStyle, scrollbarHeight } = useResponsiveModal()

const userStore = useUserStore()
const canAssignRoles = computed(() => userStore.hasPermission('system:role:manage'))
const isBuiltInAccount = computed(() => props.editingUser?.isBuiltIn === true)
const showRoleAssignment = computed(() => canAssignRoles.value && !isBuiltInAccount.value)

const roleFilter = ref('')
const roleScrollRef = ref<HTMLElement | null>(null)

const filteredRoles = computed(() => {
  const keyword = roleFilter.value.trim().toLowerCase()
  if (!keyword) return props.roles
  return props.roles.filter((role) =>
    `${role.name} ${role.code}`.toLowerCase().includes(keyword),
  )
})

watch(roleFilter, () => roleScrollRef.value?.scrollTo({ top: 0 }))

// 每次打开弹窗都清空筛选并回到顶部，避免沿用上一次的滚动位置
watch(
  () => props.show,
  (show) => {
    if (!show) return
    roleFilter.value = ''
    void nextTick(() => roleScrollRef.value?.scrollTo({ top: 0 }))
  },
)

const formRef = ref()

const formData = ref<UserFormData>({
  userName: '',
  nickName: '',
  name: '',
  email: '',
  phone: '',
  address: '',
  description: '',
  sex: 0,
  roleIds: [],
})

// 验证规则
const rules = computed<FormRules>(() => ({
  userName: [
    { required: true, message: t('Authz:ValidateUsernameRequired'), trigger: 'blur' },
    { min: 3, max: 20, message: t('Authz:ValidateUsernameLength'), trigger: 'blur' },
  ],
  nickName: [
    { required: true, message: t('Authz:ValidateNicknameRequired'), trigger: 'blur' },
    { max: 20, message: t('Authz:ValidateNicknameLength'), trigger: 'blur' },
    {
      asyncValidator: async (_rule: unknown, value: string) => {
        const nickName = value?.trim()
        if (!nickName || nickName === props.editingUser?.nickName?.trim()) {
          return
        }
        const available = await checkNicknameAvailability(nickName, props.editingUser?.id)
        if (!available) {
          throw new Error(t('Authz:ValidateNicknameExists'))
        }
      },
      trigger: 'input',
    },
  ],
  email: [
    { type: 'email', message: t('Authz:ValidateEmail'), trigger: 'blur' },
  ],
  phone: [
    {
      pattern: /^1[3-9]\d{9}$/,
      message: t('Authz:ValidatePhone'),
      trigger: 'blur',
    },
  ],
}))

// 监听编辑用户变化
watch(
  () => props.editingUser,
  (user) => {
    if (user) {
      formData.value = {
        userName: user.userName,
        nickName: user.nickName || '',
        name: user.name || '',
        email: user.email || '',
        phone: user.phone || '',
        address: user.address || '',
        description: user.description || '',
        sex: user.sex || 0,
        roleIds:
          user.roles
            ?.map((role) => role.id)
            .filter((roleId): roleId is string => !!roleId) ?? [],
      }
    } else {
      formData.value = {
        userName: '',
        nickName: '',
        name: '',
        email: '',
        phone: '',
        address: '',
        description: '',
        sex: 0,
        roleIds: [],
      }
    }
  },
)

// 提交表单
const handleSubmit = async () => {
  try {
    await formRef.value?.validate()

    const submitData: UserFormData = { ...formData.value }

    // 无角色管理权限、内置账号或编辑时角色数据缺失（读取失败）时，不提交 roleIds，
    // 后端会保持现有角色不变，避免把未知状态误写成“清空角色”。
    if (!showRoleAssignment.value || (props.editingUser && !props.editingUser.roles)) {
      delete submitData.roleIds
    }

    emit('submit', submitData)
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

/* 用原生滚动容器承载统一的主题滚动条，Naive 自绘轨道与全局胶囊样式不一致 */
.authz-modal-scroll {
  overflow-y: auto;
  padding-right: 6px;
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

.authz-role-scroll {
  width: 100%;
  height: 220px;
  overflow-y: auto;
  padding-right: 6px;
}

/* 表单空白区是 flex 容器，复选框组默认按内容收缩，需撑满才能让每行占满整行 */
.authz-role-group {
  width: 100%;
}

.authz-role-list {
  width: 100%;
}

.authz-role-item {
  align-items: center;
  gap: 8px;
  padding-block: 5px;
  cursor: pointer;
}

.authz-role-item:first-child {
  padding-top: 5px;
}

.authz-role-item:last-child {
  padding-bottom: 5px;
}

.authz-role-item .po-list__main {
  gap: 1px;
}

.authz-role-item .po-list__main b {
  line-height: 1.25;
}

.authz-role-item .po-list__main span {
  font-size: 11.5px;
  line-height: 1.35;
}

@media (max-width: 640px) {
  .po-form-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

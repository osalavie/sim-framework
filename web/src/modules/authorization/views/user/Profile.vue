<template>
  <div class="po-page">
    <n-spin :show="loading">
      <section class="po-card po-card--cream authz-profile-card">
        <header class="authz-profile-id__head">
          <button type="button" class="avatar-button" v-tooltip="t('UserProfile:AvatarEditor')"
            :aria-label="t('UserProfile:AvatarChange')" @click="showAvatarEditor = true">
            <div class="po-avatar po-avatar--round authz-avatar">
              <LazyImage v-if="currentAvatar" :src="currentAvatar" alt="" />
              <span v-else>{{ avatarInitial }}</span>
              <span class="avatar-camera-icon" aria-hidden="true">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"
                  stroke-linejoin="round">
                  <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
                  <circle cx="12" cy="13" r="4" />
                </svg>
              </span>
            </div>
            <n-spin v-if="uploadingAvatar" class="avatar-progress" size="small" />
          </button>
          <div class="avatar-copy">
            <h2>{{ formData.nickName || formData.name || t('UserProfile:Avatar') }}</h2>
          </div>
        </header>

        <div class="po-divider"></div>

        <dl class="po-kv authz-profile-fields">
          <div v-for="field in profileFields" :key="field.key" class="authz-profile-row"
            :class="{ 'is-editing': editingField === field.key }">
            <dt>{{ field.label }}</dt>
            <dd class="authz-profile-row__value">
              <div v-if="editingField === field.key" class="authz-inline-editor" @keydown="handleEditorKeydown">
                <n-input :ref="setEditorInstance" v-model:value="draftValue"
                  :type="field.key === 'description' ? 'textarea' : 'text'" :maxlength="field.maxlength"
                  :placeholder="field.placeholder" :disabled="savingField === field.key"
                  :autosize="field.key === 'description' ? { minRows: 3, maxRows: 8 } : false">
                  <template v-if="field.key !== 'description'" #suffix>
                    <n-button type="primary" circle size="tiny" :loading="savingField === field.key"
                      v-tooltip="t('UserProfile:SubmitTitle', { field: field.label })"
                      :aria-label="t('UserProfile:SubmitTitle', { field: field.label })"
                      @click="submitField(field.key)">
                      <template #icon>
                        <svg class="authz-inline-editor__arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                          stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                          <line x1="5" y1="12" x2="19" y2="12" />
                          <polyline points="12 5 19 12 12 19" />
                        </svg>
                      </template>
                    </n-button>
                  </template>
                </n-input>
                <div class="authz-inline-editor__feedback">
                  <p v-if="field.key === 'nickName' && nicknameError" class="authz-inline-error" role="alert">
                    {{ nicknameError }}
                  </p>
                  <span class="authz-inline-editor__count">{{ draftValue.length }}/{{ field.maxlength }}</span>
                  <n-button v-if="field.key === 'description'" class="authz-inline-editor__submit" type="primary" circle
                    size="tiny" :loading="savingField === field.key"
                    v-tooltip="t('UserProfile:SubmitTitle', { field: field.label })"
                    :aria-label="t('UserProfile:SubmitTitle', { field: field.label })" @click="submitField(field.key)">
                    <template #icon>
                      <svg class="authz-inline-editor__arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                        stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <line x1="5" y1="12" x2="19" y2="12" />
                        <polyline points="12 5 19 12 12 19" />
                      </svg>
                    </template>
                  </n-button>
                </div>
              </div>
              <span v-else class="authz-profile-value" :class="{ 'is-empty': !fieldText(field.key) }">
                {{ fieldText(field.key) || '—' }}
              </span>
              <button v-if="editingField !== field.key" type="button" class="authz-profile-edit"
                v-tooltip="t('UserProfile:EditTitle', { field: field.label })"
                :aria-label="t('UserProfile:EditTitle', { field: field.label })" @click="startEdit(field.key)">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"
                  stroke-linejoin="round" aria-hidden="true">
                  <path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z" />
                </svg>
              </button>
            </dd>
          </div>
        </dl>
      </section>
    </n-spin>

    <AvatarEditor v-model:show="showAvatarEditor" :title="t('UserProfile:AvatarEditor')"
      :default-image-url="currentAvatar" :busy="uploadingAvatar" @processed="uploadAvatar" />
  </div>
</template>
<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import type { InputInst } from 'naive-ui'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'
import { userApi } from '@/modules/authorization/api/user'
import type { UpdateMyProfileData, User } from '@/modules/authorization/api/user/userModels'
import { AvatarEditor } from '@/components/ImageEditor'
import LazyImage from '@/components/LazyImage.vue'
import { useUserStore } from '@/stores/user'
import { handleError } from '@/utils/errorHandler'
import { createDebouncedNicknameChecker } from '@/utils/nicknameAvailability'

type ProfileFieldKey = 'nickName' | 'name' | 'description'

interface ProfileField {
  key: ProfileFieldKey
  label: string
  maxlength: number
  placeholder: string
}

const formData = ref<UpdateMyProfileData>({ nickName: '' })
const currentAvatar = ref<string>()
const uploadingAvatar = ref(false)
const showAvatarEditor = ref(false)
const userStore = useUserStore()
const avatarInitial = computed(() =>
  (formData.value.nickName || formData.value.name || userStore.user?.userName || 'S')
    .charAt(0)
    .toUpperCase(),
)
const loading = ref(false)
const editingField = ref<ProfileFieldKey | null>(null)
const savingField = ref<ProfileFieldKey | null>(null)
const draftValue = ref('')
const nicknameError = ref('')
const checkNicknameAvailability = createDebouncedNicknameChecker()
let nicknameCheckSequence = 0

watch([editingField, draftValue], ([field, value]) => {
  if (field !== 'nickName') {
    nicknameError.value = ''
    return
  }
  const nickName = value.trim()
  if (!nickName) {
    nicknameError.value = t('Authz:ValidateNicknameRequired')
    return
  }
  if (nickName === (formData.value.nickName ?? '').trim()) {
    nicknameError.value = ''
    return
  }

  const sequence = ++nicknameCheckSequence
  nicknameError.value = ''
  void checkNicknameAvailability(nickName, userStore.user?.id).then((available) => {
    if (sequence === nicknameCheckSequence) {
      nicknameError.value = available ? '' : t('Authz:ValidateNicknameExists')
    }
  })
})

const profileFields = computed<ProfileField[]>(() => [
  {
    key: 'nickName',
    label: t('Field:Nickname'),
    maxlength: 20,
    placeholder: t('UserProfile:NicknamePlaceholder'),
  },
  {
    key: 'name',
    label: t('Field:FullName'),
    maxlength: 30,
    placeholder: t('UserProfile:FullNamePlaceholder'),
  },
  {
    key: 'description',
    label: t('Common:Description'),
    maxlength: 150,
    placeholder: t('UserProfile:DescriptionPlaceholder'),
  },
])

const fieldText = (key: ProfileFieldKey) => (formData.value[key] ?? '').trim()

const applyProfile = (value: User) => {
  formData.value = {
    nickName: value.nickName ?? '',
    name: value.name,
    description: value.description,
  }
  currentAvatar.value = value.avatar
  userStore.updateProfile(value)
}

const setEditorInstance = (instance: InputInst | null) => {
  // 条件渲染下同一时刻只有一个编辑框，挂载后立即聚焦，避免函数 ref 在切换行时被置空。
  if (!instance) return
  nextTick(() => {
    if (editingField.value) instance.focus()
  })
}

const startEdit = (key: ProfileFieldKey) => {
  if (savingField.value) return
  editingField.value = key
  draftValue.value = formData.value[key] ?? ''
  nicknameError.value = ''
}

const stopEdit = () => {
  editingField.value = null
  draftValue.value = ''
  nicknameError.value = ''
}

const submitField = async (key: ProfileFieldKey) => {
  if (savingField.value) return
  const nextValue = draftValue.value.trim()
  if (nextValue === (formData.value[key] ?? '').trim()) {
    stopEdit()
    return
  }

  if (key === 'nickName') {
    if (!nextValue) {
      nicknameError.value = t('Authz:ValidateNicknameRequired')
      return
    }
    try {
      const available = await userApi.checkNickNameAvailability(nextValue, userStore.user?.id)
      if (!available) {
        nicknameError.value = t('Authz:ValidateNicknameExists')
        return
      }
    } catch {
      // 查重失败不阻塞提交，后端唯一约束仍是最终判定。
    }
    nicknameError.value = ''
  }

  savingField.value = key
  try {
    const value = await userApi.updateMyProfile({ ...formData.value, [key]: nextValue })
    applyProfile(value)
    stopEdit()
    useDiscreteApi.message.success(t('UserProfile:Saved'))
  } catch (error) {
    handleError(error, t('UserProfile:SaveFailed'), { logPrefix: 'Profile' })
  } finally {
    savingField.value = null
  }
}

const handleEditorKeydown = (event: KeyboardEvent) => {
  const key = editingField.value
  if (!key) return

  if (event.key === 'Escape') {
    event.preventDefault()
    if (!savingField.value) stopEdit()
    return
  }

  if (event.key !== 'Enter') return
  if (key === 'description' && !(event.ctrlKey || event.metaKey)) return

  event.preventDefault()
  void submitField(key)
}

const loadProfile = async () => {
  loading.value = true
  try {
    const value = await userApi.getMyProfile()
    applyProfile(value)
    showAvatarEditor.value = false
  } catch {
    useDiscreteApi.message.error(t('UserProfile:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const uploadAvatar = async (file: File) => {
  uploadingAvatar.value = true
  try {
    const value = await userApi.uploadMyAvatar(file)
    currentAvatar.value = value.avatar
    userStore.updateProfile(value)
    showAvatarEditor.value = false
    useDiscreteApi.message.success(t('UserProfile:AvatarSaved'))
  } catch {
    useDiscreteApi.message.error(t('UserProfile:AvatarSaveFailed'))
  } finally {
    uploadingAvatar.value = false
  }
}

onMounted(() => {
  void loadProfile()
})
</script>

<style scoped>
.authz-profile-card {
  align-content: start;
}

.authz-profile-id__head {
  display: flex;
  align-items: center;
  gap: 16px;
}

.authz-avatar {
  width: 88px;
  height: 88px;
  overflow: hidden;
  font-size: 30px;
}

.authz-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-button {
  position: relative;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  padding: 0;
  border: 0;
  background: transparent;
  border-radius: 50%;
  cursor: pointer;
}

.avatar-button:focus-visible {
  outline: 3px solid var(--po-accent);
  outline-offset: 5px;
}

.avatar-progress {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: color-mix(in srgb, var(--po-paper) 78%, transparent);
}

.avatar-camera-icon {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: color-mix(in srgb, var(--po-ink) 50%, transparent);
  opacity: 0;
  transition: opacity 180ms ease;
}

.avatar-camera-icon svg {
  width: 28px;
  height: 28px;
  color: var(--po-paper);
}

.avatar-button:hover .avatar-camera-icon,
.avatar-button:focus-visible .avatar-camera-icon {
  opacity: 1;
}

.avatar-copy h2 {
  margin: 0 0 4px;
  font-family: var(--po-font-display);
  font-size: 18px;
  font-weight: var(--po-weight-display);
}

.avatar-copy p {
  margin: 0;
  color: var(--po-ink-soft);
  font-size: 12.5px;
}

.authz-profile-fields {
  display: block;
  margin: 0;
  font-size: 13px;
}

.authz-profile-row {
  display: grid;
  grid-template-columns: 92px minmax(0, 1fr);
  align-items: center;
  gap: 4px 14px;
  padding: 9px 2px;
  border-bottom: 1px dashed var(--po-line);
}

.authz-profile-row:first-child {
  padding-top: 2px;
}

.authz-profile-row:last-child {
  border-bottom: 0;
  padding-bottom: 2px;
}

.authz-profile-row.is-editing {
  align-items: start;
}

.authz-profile-row.is-editing dt {
  padding-top: 11px;
}

.authz-profile-row__value {
  display: flex;
  align-items: center;
  min-width: 0;
  width: min(100%, 420px);
}

.authz-profile-value {
  min-width: 0;
  overflow-wrap: anywhere;
}

.authz-profile-value.is-empty {
  color: var(--po-ink-faint);
  font-weight: 500;
}

.authz-profile-edit {
  display: grid;
  place-items: center;
  flex: 0 0 34px;
  margin-left: auto;
  width: 34px;
  height: 34px;
  padding: 0;
  border: 1.5px solid transparent;
  border-radius: 50%;
  background: transparent;
  color: var(--po-ink-faint);
  cursor: pointer;
  transition:
    color 0.16s ease,
    background-color 0.16s ease,
    border-color 0.16s ease;
}

.authz-profile-edit svg {
  width: 16px;
  height: 16px;
}

.authz-profile-row:hover .authz-profile-edit,
.authz-profile-edit:focus-visible {
  border-color: var(--po-line-mid);
  background: var(--po-paper);
  color: var(--po-accent);
}

.authz-profile-edit:focus-visible {
  outline: 3px solid var(--po-accent);
  outline-offset: 2px;
}

.authz-inline-editor {
  position: relative;
  min-width: 0;
  width: 100%;
}

.authz-inline-editor :deep(.n-input) {
  width: 100%;
}

.authz-inline-editor__arrow {
  width: 15px;
  height: 15px;
}

.authz-inline-error {
  margin: 0;
  color: var(--po-danger);
  font-size: 12px;
  line-height: 1.4;
}

.authz-inline-editor__feedback {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-top: 6px;
}

.authz-inline-editor__count {
  margin-left: auto;
  color: var(--po-ink-soft);
  font-size: 12px;
  line-height: 1.4;
  white-space: nowrap;
}

.authz-avatar-btn {
  justify-self: start;
}

@media (max-width: 640px) {
  .authz-profile-row {
    grid-template-columns: 72px minmax(0, 1fr);
    gap: 4px 10px;
  }

  .authz-avatar-btn {
    width: 100%;
  }
}
</style>

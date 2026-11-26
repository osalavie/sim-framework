<template>
  <section class="po-card settings-card">
    <h2>{{ t('E2ee:PasskeysTitle') }}</h2>
    <p class="po-small po-muted">{{ t('E2ee:PasskeysIntro') }}</p>
    <n-spin :show="loading || busy">
      <n-alert v-if="!enabled" type="info" :show-icon="false">{{ t('E2ee:PasskeysUnavailable') }}</n-alert>
      <n-alert v-else-if="!supported" type="info" :show-icon="false">{{ t('E2ee:PasskeyUnsupported') }}</n-alert>
      <n-button v-else type="primary" :disabled="busy" :loading="busy" @click="add">{{ t('E2ee:PasskeyAdd') }}</n-button>
      <p v-if="preferred" class="po-small">{{ t('E2ee:DeviceCachePasskey') }}</p>
      <div v-for="row in rows" :key="row.id" class="device-row">
        <span class="passkey-name">{{ row.name }}</span>
        <n-space>
          <n-button secondary :disabled="busy" @click="editName(row)">{{ t('E2ee:PasskeyRename') }}</n-button>
          <n-button v-if="enabled && supported" secondary :disabled="busy" @click="activate(row.id!)">
            {{ t(row.id === preferred ? 'E2ee:UsePasskey' : 'E2ee:PasskeyUseHere') }}
          </n-button>
          <n-popconfirm @positive-click="remove(row.id!)">
            <template #trigger><n-button type="error" secondary :disabled="busy">{{ t('Common:Delete') }}</n-button></template>
            {{ t('E2ee:PasskeyRemoveConfirm') }}
          </n-popconfirm>
        </n-space>
      </div>
      <n-alert v-if="failure" type="error" :show-icon="false">{{ failure }}</n-alert>
      <PasskeyDiagnostics v-if="failure" />
    </n-spin>
    <n-modal v-model:show="renameVisible" preset="card" :title="t('E2ee:PasskeyRename')"
      :style="{ width: 'min(480px, 92vw)' }" :mask-closable="!busy" :close-on-esc="!busy" :closable="!busy">
      <n-form ref="renameForm" :model="renameModel" :rules="renameRules" @submit.prevent="saveName">
        <n-form-item path="name" :label="t('E2ee:PasskeyName')" required>
          <n-input v-model:value="renameModel.name" :maxlength="100" show-count :disabled="busy" />
        </n-form-item>
        <n-space justify="end">
          <n-button :disabled="busy" @click="renameVisible = false">{{ t('Common:Cancel') }}</n-button>
          <n-button type="primary" attr-type="submit" :loading="busy" :disabled="busy">{{ t('Common:Save') }}</n-button>
        </n-space>
      </n-form>
    </n-modal>
  </section>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import type { FormInst, FormRules } from 'naive-ui'
import { scrollToFirstFormError } from '@/utils/formErrors'
import { handleError } from '@/utils/errorHandler'
import type { E2EePasskeyDto } from '@/api/system'
import { useUserStore } from '@/stores/user'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { t } from '@/i18n'
import PasskeyDiagnostics from './PasskeyDiagnostics.vue'
import { passkeyApi } from './passkeyApi'
import { registerPasskey, unlockWithPasskey, supportsPasskeyUnlock, PasskeyError } from './passkeys'
import { ensureE2eeUnlocked, getE2eeKeyVersion, lockE2eeVault, setE2eeUmk } from './keyVault'
import { loadPasskeyPreference, selectPasskeyUnlock } from './deviceStore'
import { cancelE2eeKeyOperations } from './keysAsync'

const props = defineProps<{ keyVersion: number }>()
const emit = defineEmits<{ changed: [] }>()
const user = useUserStore()
const rows = ref<E2EePasskeyDto[]>([])
const enabled = ref(false)
const supported = supportsPasskeyUnlock()
const preferred = ref<string | null>(null)
const loading = ref(false)
const busy = ref(false)
const failure = ref('')
let disposed = false
const renameVisible = ref(false)
const renameForm = ref<FormInst | null>(null)
const renameModel = reactive({ id: '', name: '' })
const renameRules: FormRules = {
  name: { trigger: 'blur', validator: (_rule, value: string) =>
    value.trim().length > 0 && value.length <= 100 ? true : new Error(t('E2ee:PasskeyNameInvalid')) },
}

function editName(row: E2EePasskeyDto) {
  if (busy.value || !row.id) return
  renameModel.id = row.id
  renameModel.name = row.name ?? ''
  renameForm.value?.restoreValidation()
  renameVisible.value = true
}
async function saveName() {
  if (busy.value || !user.user?.id || !renameForm.value) return
  const owner = user.user.id
  busy.value = true
  try {
    try { await renameForm.value.validate() }
    catch { void scrollToFirstFormError(renameForm.value); return }
    if (disposed || user.user?.id !== owner) return
    const updated = await passkeyApi.rename(renameModel.id, renameModel.name.trim())
    if (disposed || user.user?.id !== owner) return
    rows.value = rows.value.map(row => row.id === updated.id ? updated : row)
    renameVisible.value = false
    useDiscreteApi.message.success(t('E2ee:PasskeyRenamed'))
  } catch (error) { handleError(error, t('E2ee:PasskeyFailed'), { logError: false }) }
  finally { busy.value = false }
}

async function load() {
  if (disposed) return
  const owner = user.user?.id
  loading.value = true
  try {
    const [configuration, items] = await Promise.all([passkeyApi.configuration(), passkeyApi.list()])
    if (disposed || user.user?.id !== owner) return
    enabled.value = Boolean(configuration.enabled)
    rows.value = items
    preferred.value = user.user?.id ? await loadPasskeyPreference(user.user.id) : null
  } catch { if (!disposed) failure.value = t('E2ee:PasskeyFailed') }
  finally { loading.value = false }
}
function report(error: unknown) {
  failure.value = t(error instanceof PasskeyError && error.code === 'unsupported' ? 'E2ee:PasskeyUnsupported'
    : error instanceof PasskeyError && error.code === 'prf-unavailable' ? 'E2ee:PasskeyPrfUnavailable'
    : error instanceof PasskeyError && error.code === 'not-allowed' ? 'E2ee:PasskeyNotAllowed'
    : error instanceof PasskeyError && error.code === 'cancelled' ? 'E2ee:PasskeyCancelled' : 'E2ee:PasskeyFailed')
}
async function add() {
  if (busy.value || !user.user?.id) return
  busy.value = true
  failure.value = ''
  try {
    const owner = user.user.id
    const umk = await ensureE2eeUnlocked(owner)
    if (disposed || user.user?.id !== owner) return
    await registerPasskey(owner, umk, getE2eeKeyVersion())
    if (disposed) return
    useDiscreteApi.message.success(t('E2ee:PasskeyAdded'))
    await load()
    emit('changed')
  } catch (error) { report(error); await load() }
  finally { busy.value = false }
}
async function activate(id: string) {
  if (busy.value || !user.user?.id) return
  busy.value = true
  failure.value = ''
  let umk: Uint8Array | null = null
  try {
    const owner = user.user.id
    umk = await unlockWithPasskey(owner, id, props.keyVersion)
    if (disposed || user.user?.id !== owner) return
    setE2eeUmk(umk, owner, props.keyVersion)
    await load()
    emit('changed')
  } catch (error) { report(error) }
  finally { umk?.fill(0); busy.value = false }
}
async function remove(id: string) {
  if (busy.value || !user.user?.id) return
  busy.value = true
  failure.value = ''
  try {
    await passkeyApi.remove(id)
    if (preferred.value === id) { await selectPasskeyUnlock(user.user.id, null); lockE2eeVault() }
    await load()
    emit('changed')
  } catch (error) { report(error) }
  finally { busy.value = false }
}
watch(() => props.keyVersion, () => void load())
onMounted(() => void load())
onBeforeUnmount(() => { disposed = true; cancelE2eeKeyOperations() })
</script>

<style scoped>
.settings-card { padding: clamp(20px, 3vw, 30px); margin-bottom: 20px; grid-template-columns: minmax(0, 1fr); }
.settings-card h2 { margin-top: 0; }
.device-row { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; justify-content: space-between; margin-top: 16px; }
.passkey-name { min-width: 0; flex: 1 1 160px; overflow-wrap: anywhere; }
.device-row :deep(.n-space) { max-width: 100%; }
</style>

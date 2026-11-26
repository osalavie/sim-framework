<template>
  <section class="file-workbench" :aria-labelledby="headingId">
    <div class="file-workbench__heading">
      <div>
        <p class="file-workbench__eyebrow">{{ t('FileWorkbench:Eyebrow') }}</p>
        <h2 :id="headingId">{{ title || t('FileWorkbench:Title') }}</h2>
      </div>
      <label class="file-workbench__provider">
        <span>{{ t('FileWorkbench:Storage') }}</span>
        <select v-model="provider">
          <option v-for="item in providers" :key="item.value" :value="item.value">
            {{ item.label }}
          </option>
        </select>
      </label>
    </div>

    <label
      class="file-workbench__dropzone"
      :class="{ 'is-dragging': dragging }"
      @dragenter.prevent="dragging = true"
      @dragover.prevent
      @dragleave.prevent="dragging = false"
      @drop.prevent="onDrop"
    >
      <input ref="fileInput" type="file" :accept="accept" @change="onFileInput" />
      <span class="file-workbench__drop-icon" aria-hidden="true">＋</span>
      <strong>{{ selectedFile?.name || t('FileWorkbench:ChooseFile') }}</strong>
      <span>
        {{
          selectedFile
            ? formatBytes(selectedFile.size)
            : t('FileWorkbench:DropHint', { size: formatBytes(maxBytes) })
        }}
      </span>
    </label>

    <div v-if="selectedFile" class="file-workbench__actions">
      <button type="button" :disabled="busy" @click="uploadSelected">
        {{ busyMode === 'upload' ? t('FileWorkbench:Uploading') : t('FileWorkbench:Upload') }}
      </button>
      <button
        v-if="canConvertSelected"
        type="button"
        class="is-secondary"
        :disabled="busy"
        @click="convertSelected"
      >
        {{ busyMode === 'convert' ? t('FileWorkbench:Converting') : t('FileWorkbench:ConvertPdf') }}
      </button>
    </div>

    <div v-if="busy" class="file-workbench__progress" aria-live="polite">
      <progress :value="progress" max="100">{{ progress }}%</progress>
      <span>{{ progress }}%</span>
    </div>
    <p v-if="status" class="file-workbench__status" role="status">{{ status }}</p>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import {
  fileManagementApi,
  saveBlob,
  type FileStorageProvider,
  type StoredFile,
} from '@/api/fileManagement'
import { t } from '@/i18n'

const props = withDefaults(
  defineProps<{
    title?: string
    accept?: string
    maxBytes?: number
    initialProvider?: FileStorageProvider
  }>(),
  {
    title: '',
    accept: '.doc,.docx,.odt,.xls,.xlsx,.ods,.pdf,.csv,.json,.jpg,.jpeg,.png,.webp',
    maxBytes: 50 * 1024 * 1024,
    initialProvider: 'local',
  },
)

const emit = defineEmits<{
  uploaded: [value: StoredFile]
  converted: [value: Blob]
  error: [value: unknown]
}>()

const headingId = `file-workbench-${useId()}`
const fileInput = ref<HTMLInputElement>()
const selectedFile = ref<File>()
const provider = ref<FileStorageProvider>(props.initialProvider)
const dragging = ref(false)
const progress = ref(0)
const busyMode = ref<'upload' | 'convert' | null>(null)
const status = ref('')
const busy = computed(() => busyMode.value !== null)
const canConvertSelected = computed(() =>
  selectedFile.value ? /\.(docx?|odt|xlsx?|ods)$/i.test(selectedFile.value.name) : false,
)

const providers = computed<Array<{ value: FileStorageProvider; label: string }>>(() => [
  { value: 'local', label: t('Storage:Provider:Local') },
  { value: 'database', label: t('FileWorkbench:ProviderDatabase') },
  { value: 's3', label: 'Amazon S3' },
  { value: 'tencent-cos', label: t('Storage:Provider:Tencent-cos') },
  { value: 'aliyun-oss', label: t('Storage:Provider:Aliyun-oss') },
])

function formatBytes(value: number) {
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / 1024 / 1024).toFixed(1)} MB`
}

function chooseFile(file?: File) {
  status.value = ''
  if (!file) return
  if (file.size > props.maxBytes) {
    selectedFile.value = undefined
    status.value = t('FileWorkbench:TooLarge', { size: formatBytes(props.maxBytes) })
    return
  }
  selectedFile.value = file
}

function onFileInput(event: Event) {
  chooseFile((event.target as HTMLInputElement).files?.[0])
}

function onDrop(event: DragEvent) {
  dragging.value = false
  chooseFile(event.dataTransfer?.files[0])
}

async function uploadSelected() {
  if (!selectedFile.value) return
  busyMode.value = 'upload'
  progress.value = 0
  status.value = ''
  try {
    const result = await fileManagementApi.upload(selectedFile.value, {
      provider: provider.value,
      onProgress: (value) => (progress.value = value),
    })
    progress.value = 100
    status.value = t('FileWorkbench:Uploaded', { name: result.fileName })
    emit('uploaded', result)
  } catch (error) {
    status.value = t('FileWorkbench:Failed')
    emit('error', error)
  } finally {
    busyMode.value = null
  }
}

async function convertSelected() {
  if (!selectedFile.value) return
  busyMode.value = 'convert'
  progress.value = 0
  status.value = ''
  try {
    const result = await fileManagementApi.convertToPdf(
      selectedFile.value,
      (value) => (progress.value = value),
    )
    const fileName = selectedFile.value.name.replace(/\.[^.]+$/, '.pdf')
    saveBlob(result, fileName)
    progress.value = 100
    status.value = t('FileWorkbench:Converted', { name: fileName })
    emit('converted', result)
  } catch (error) {
    status.value = t('FileWorkbench:Failed')
    emit('error', error)
  } finally {
    busyMode.value = null
  }
}
</script>

<style scoped>
.file-workbench {
  --workbench-accent: var(--primary-color, var(--ink-accent, #161412));
  padding: clamp(1rem, 2.5vw, 1.75rem);
  color: var(--text-color-1, #17211f);
  background: var(--card-color, #fff);
  border: 1px solid var(--border-color, #dce4e1);
  border-radius: 18px;
}

.file-workbench__heading {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.file-workbench__eyebrow {
  margin: 0 0 0.3rem;
  color: var(--workbench-accent);
  font-size: 0.75rem;
  font-weight: var(--po-weight-bold);
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

h2 {
  margin: 0;
  font-size: clamp(1.25rem, 2vw, 1.6rem);
}

.file-workbench__provider {
  display: grid;
  gap: 0.35rem;
  min-width: min(100%, 13rem);
  font-size: 0.78rem;
  color: var(--text-color-2, #52605c);
}

select,
button {
  min-height: 44px;
  border-radius: 10px;
  font: inherit;
}

select {
  padding: 0 0.75rem;
  color: inherit;
  background: var(--input-color, #fff);
  border: 1px solid var(--border-color, #ccd8d4);
}

.file-workbench__dropzone {
  display: grid;
  place-items: center;
  min-height: 168px;
  padding: 1.25rem;
  text-align: center;
  cursor: pointer;
  background: color-mix(in srgb, var(--workbench-accent) 4%, transparent);
  border: 1px dashed color-mix(in srgb, var(--workbench-accent) 45%, #b7c2bf);
  border-radius: 14px;
  transition:
    background-color 160ms ease,
    border-color 160ms ease;
}

.file-workbench__dropzone:is(:hover, :focus-within),
.file-workbench__dropzone.is-dragging {
  background: color-mix(in srgb, var(--workbench-accent) 9%, transparent);
  border-color: var(--workbench-accent);
}

.file-workbench__dropzone input {
  position: absolute;
  inline-size: 1px;
  block-size: 1px;
  overflow: hidden;
  clip-path: inset(50%);
}

.file-workbench__drop-icon {
  display: grid;
  place-items: center;
  inline-size: 44px;
  block-size: 44px;
  margin-bottom: 0.5rem;
  color: #fff;
  background: var(--workbench-accent);
  border-radius: 50%;
  font-size: 1.5rem;
}

.file-workbench__dropzone span:last-child {
  margin-top: 0.35rem;
  color: var(--text-color-3, #73807c);
  font-size: 0.82rem;
}

.file-workbench__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-top: 1rem;
}

button {
  padding: 0 1.1rem;
  color: #fff;
  cursor: pointer;
  background: var(--workbench-accent);
  border: 1px solid var(--workbench-accent);
}

button.is-secondary {
  color: var(--workbench-accent);
  background: transparent;
}

button:disabled {
  cursor: wait;
  opacity: 0.6;
}

:is(button, select, .file-workbench__dropzone):focus-visible {
  outline: 3px solid color-mix(in srgb, var(--workbench-accent) 35%, transparent);
  outline-offset: 2px;
}

.file-workbench__progress {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
}

progress {
  flex: 1;
  accent-color: var(--workbench-accent);
}

.file-workbench__status {
  margin: 0.8rem 0 0;
  font-size: 0.86rem;
}

@media (max-width: 640px) {
  .file-workbench__heading {
    align-items: stretch;
    flex-direction: column;
  }

  .file-workbench__actions button {
    flex: 1 1 100%;
  }
}
</style>

<template>
  <div class="po-page">
    <div class="po-toolbar">
      <div class="po-segmented" role="tablist" :aria-label="t('Schedule:Status')">
        <button
          v-for="option in filterOptions"
          :key="option.value"
          type="button"
          role="tab"
          :aria-selected="statusFilter === option.value"
          @click="statusFilter = option.value"
        >
          {{ option.label }}
        </button>
      </div>
      <span class="po-pill po-pill--outline">
        {{ t('Schedule:Summary', { count: filteredTasks.length }) }}
      </span>
      <div class="po-toolbar__spacer" />
      <n-button secondary :loading="loading" @click="load">{{ t('Common:Refresh') }}</n-button>
      <n-button type="primary" @click="openCreate">{{ t('Schedule:Create') }}</n-button>
    </div>

    <n-spin :show="loading">
      <div v-if="filteredTasks.length" class="task-list">
        <section v-for="task in filteredTasks" :key="task.id" class="po-card task-card">
          <header class="task-card__head">
            <div class="task-title">
              <div class="task-title__row">
                <h2>{{ task.displayName }}</h2>
                <div class="task-tags">
                  <span class="po-pill po-pill--outline">{{ typeLabel(task.taskType) }}</span>
                  <span v-if="task.isBuiltIn" class="po-pill po-pill--sky">{{ t('Schedule:BuiltIn') }}</span>
                  <span
                    class="po-pill"
                    :class="task.enabled ? 'po-pill--mint po-pill--dot' : 'po-pill--outline'"
                  >
                    {{ t(task.enabled ? 'Common:Enabled' : 'Common:Disabled') }}
                  </span>
                </div>
              </div>
              <p v-if="task.description">{{ task.description }}</p>
            </div>
            <n-switch v-model:value="task.enabled" class="task-enabled-switch">
              <template #checked>{{ t('Common:Enabled') }}</template>
              <template #unchecked>{{ t('Common:Disabled') }}</template>
            </n-switch>
          </header>
          <div class="task-fields">
            <label class="cron-label">
              <span>{{ t('Schedule:Cron') }}</span>
              <CronEditor v-model="task.cronExpression" />
            </label>
            <dl class="po-kv task-meta">
              <dt>{{ t('Schedule:Status') }}</dt>
              <dd>{{ task.status }}</dd>
              <dt>{{ t('Schedule:Previous') }}</dt>
              <dd>{{ formatTime(task.previousRunTime) }}</dd>
              <dt>{{ t('Schedule:Next') }}</dt>
              <dd>{{ formatTime(task.nextRunTime) }}</dd>
            </dl>
          </div>
          <footer class="task-card__foot">
            <code class="po-mono">{{ task.group }}/{{ task.key }}</code>
            <div class="task-actions">
              <n-button v-if="task.canDelete" size="small" quaternary type="error" @click="confirmDelete(task)">
                {{ t('Common:Delete') }}
              </n-button>
              <n-button
                size="small"
                type="primary"
                :loading="savingKey === `${task.group}/${task.key}`"
                @click="save(task)"
              >
                {{ t('Common:Save') }}
              </n-button>
            </div>
          </footer>
        </section>
      </div>
      <n-empty
        v-else-if="!loading"
        :description="statusFilter === 'all' ? t('Schedule:Empty') : t('Schedule:NoMatches')"
      />
    </n-spin>

    <n-modal v-model:show="showCreate" :mask-closable="!creating">
      <n-card
        class="create-dialog"
        :title="t('Schedule:Create')"
        closable
        role="dialog"
        aria-modal="true"
        @close="showCreate = false"
      >
        <n-form ref="createFormRef" :model="createForm" :rules="createRules" label-placement="top">
          <n-grid responsive="screen" cols="1 m:2" :x-gap="18">
            <PoFormItem gi :label="t('Schedule:Name')" path="displayName" required>
              <n-input v-model:value="createForm.displayName" :maxlength="256" show-count />
            </PoFormItem>
            <PoFormItem gi :label="t('Schedule:Type')">
              <n-select v-model:value="createForm.taskType" :options="typeOptions" />
            </PoFormItem>
            <PoFormItem gi :label="t('Schedule:Cron')">
              <CronEditor v-model="createForm.cronExpression" />
            </PoFormItem>
            <PoFormItem gi :label="t('Schedule:Enabled')">
              <n-switch v-model:value="createForm.enabled" />
            </PoFormItem>
            <PoFormItem gi :label="t('Schedule:TaskDescription')" :span="2">
              <n-input
                v-model:value="createForm.description"
                type="textarea"
                :rows="2"
                :maxlength="1024"
                show-count
              />
            </PoFormItem>
          </n-grid>

          <section class="type-fields">
            <template v-if="createForm.taskType === 'Http'">
              <n-grid responsive="screen" cols="1 m:3" :x-gap="18">
                <PoFormItem gi :label="t('Schedule:HttpMethod')">
                  <n-select v-model:value="createForm.httpMethod" :options="httpMethodOptions" />
                </PoFormItem>
                <PoFormItem
                  gi
                  :label="t('Schedule:HttpUrl')"
                  :description="t('Schedule:HttpSecurityHint')"
                  :span="2"
                >
                  <n-input v-model:value="createForm.url" :placeholder="t('Schedule:HttpUrlPlaceholder')" />
                </PoFormItem>
                <PoFormItem gi :label="t('Schedule:HttpContentType')">
                  <n-input v-model:value="createForm.httpContentType" />
                </PoFormItem>
                <PoFormItem gi :label="t('Schedule:HttpBody')" :span="2">
                  <n-input v-model:value="createForm.httpBody" type="textarea" :rows="3" />
                </PoFormItem>
              </n-grid>
              <PoFormItem :label="t('Schedule:HttpHeaders')">
                <n-dynamic-input
                  v-model:value="createForm.httpHeaders"
                  :on-create="createHeader"
                  #="{ value }"
                >
                  <div class="header-row">
                    <n-input v-model:value="value.name" :placeholder="t('Schedule:HeaderName')" />
                    <n-input v-model:value="value.value" :placeholder="t('Schedule:HeaderValue')" />
                  </div>
                </n-dynamic-input>
              </PoFormItem>
            </template>

            <template v-else-if="createForm.taskType === 'Command'">
              <PoFormItem :label="t('Schedule:Command')" :description="t('Schedule:CommandSecurityHint')">
                <n-input v-model:value="createForm.command" />
              </PoFormItem>
              <PoFormItem :label="t('Schedule:CommandArguments')" :description="t('Schedule:ArgumentPlaceholder')">
                <n-dynamic-input
                  v-model:value="createForm.commandArguments"
                  :on-create="createArgument"
                  #="{ index }"
                >
                  <n-input v-model:value="createForm.commandArguments[index]" />
                </n-dynamic-input>
              </PoFormItem>
              <n-grid responsive="screen" cols="1 m:2" :x-gap="18">
                <PoFormItem gi :label="t('Schedule:WorkingDirectory')">
                  <n-input v-model:value="createForm.workingDirectory" />
                </PoFormItem>
                <PoFormItem gi :label="t('Schedule:TimeoutSeconds')">
                  <n-input-number
                    v-model:value="createForm.timeoutSeconds"
                    :min="1"
                    :max="3600"
                  />
                </PoFormItem>
              </n-grid>
            </template>

            <template v-else-if="createForm.taskType === 'ServiceMethod'">
              <PoFormItem :label="t('Schedule:ServiceMethod')" :description="t('Schedule:ServiceSecurityHint')">
                <n-select
                  v-model:value="createForm.serviceMethodKey"
                  :options="serviceMethodOptions"
                />
              </PoFormItem>
              <PoFormItem :label="t('Schedule:ServiceArguments')">
                <n-input
                  v-model:value="createForm.serviceArgumentsJson"
                  type="textarea"
                  :rows="4"
                  placeholder="{}"
                />
              </PoFormItem>
            </template>
          </section>
        </n-form>

        <template #footer>
          <div class="dialog-actions">
            <n-button @click="showCreate = false">{{ t('Common:Cancel') }}</n-button>
            <n-button type="primary" :loading="creating" @click="createTask">
              {{ t('Schedule:Create') }}
            </n-button>
          </div>
        </template>
      </n-card>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import type { FormInst, FormRules } from 'naive-ui'
import { messageKey, t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { CronEditor } from '@/components/CronEditor'
import PoFormItem from '@/components/FormItem/PoFormItem.vue'
import { scrollToFirstFormError } from '@/utils/formErrors'
import {
  scheduleTaskApi,
  type CreateScheduleTask,
  type ScheduleTask,
  type ScheduleTaskServiceMethod,
  type ScheduleTaskType,
} from '@/modules/systemManagement/api/schedule'

interface HeaderInput {
  name: string
  value: string
}

interface CreateFormState extends Omit<CreateScheduleTask, 'httpHeaders' | 'commandArguments'> {
  httpHeaders: HeaderInput[]
  commandArguments: string[]
}

type StatusFilter = 'all' | 'enabled' | 'disabled'

const newCreateForm = (): CreateFormState => ({
  displayName: '',
  description: '',
  taskType: 'Http',
  cronExpression: '0 0/5 * * * ?',
  enabled: true,
  httpMethod: 'GET',
  url: '',
  httpHeaders: [],
  httpBody: '',
  httpContentType: 'application/json',
  command: '',
  commandArguments: [],
  workingDirectory: '',
  timeoutSeconds: 120,
  serviceMethodKey: '',
  serviceArgumentsJson: '{}',
})

const tasks = ref<ScheduleTask[]>([])
const serviceMethods = ref<ScheduleTaskServiceMethod[]>([])
const loading = ref(false)
const creating = ref(false)
const savingKey = ref('')
const showCreate = ref(false)
const createForm = ref<CreateFormState>(newCreateForm())
const createFormRef = ref<FormInst | null>(null)
const createRules: FormRules = {
  displayName: [{ required: true, message: t('Schedule:NameRequired'), trigger: 'blur' }],
}
const statusFilter = ref<StatusFilter>('all')

const typeOptions = computed(() => [
  { label: t('Schedule:Type:Http'), value: 'Http' },
  { label: t('Schedule:Type:Command'), value: 'Command' },
  { label: t('Schedule:Type:ServiceMethod'), value: 'ServiceMethod' },
  { label: t('Schedule:Type:Worker'), value: 'Worker', disabled: true },
])
const httpMethodOptions = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD'].map((value) => ({
  label: value,
  value,
}))
const serviceMethodOptions = computed(() =>
  serviceMethods.value.map((method) => ({
    label: method.displayName,
    value: method.key,
    description: method.description,
  })),
)
const filterOptions = computed(() => [
  { label: t('Schedule:FilterAll'), value: 'all' as const },
  { label: t('Schedule:FilterEnabled'), value: 'enabled' as const },
  { label: t('Schedule:FilterDisabled'), value: 'disabled' as const },
])
const filteredTasks = computed(() => {
  if (statusFilter.value === 'enabled') return tasks.value.filter((task) => task.enabled)
  if (statusFilter.value === 'disabled') return tasks.value.filter((task) => !task.enabled)
  return tasks.value
})

const typeLabel = (value: ScheduleTaskType) => t(messageKey('schedule', 'type', value))
const formatTime = (value?: string) => (value ? new Date(value).toLocaleString() : t('Common:None'))
const createHeader = (): HeaderInput => ({ name: '', value: '' })
const createArgument = () => ''

const load = async () => {
  loading.value = true
  try {
    const [taskValues, methodValues] = await Promise.all([
      scheduleTaskApi.getList(),
      scheduleTaskApi.getServiceMethods(),
    ])
    tasks.value = taskValues
    serviceMethods.value = methodValues
  } catch {
    useDiscreteApi.message.error(t('Schedule:LoadFailed'))
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  createForm.value = newCreateForm()
  showCreate.value = true
  void nextTick(() => createFormRef.value?.restoreValidation())
}

const createTask = async () => {
  try {
    await createFormRef.value?.validate()
  } catch {
    void scrollToFirstFormError(createFormRef.value)
    return
  }

  creating.value = true
  try {
    const { httpHeaders, ...base } = createForm.value
    const headers = Object.fromEntries(
      httpHeaders.filter((item) => item.name.trim()).map((item) => [item.name.trim(), item.value]),
    )
    await scheduleTaskApi.create({
      ...base,
      taskType: base.taskType as Exclude<ScheduleTaskType, 'Worker'>,
      httpHeaders: headers,
      commandArguments: base.commandArguments?.filter((item) => item.length > 0),
    })
    showCreate.value = false
    useDiscreteApi.message.success(t('Schedule:Created'))
    await load()
  } catch {
    useDiscreteApi.message.error(t('Schedule:CreateFailed'))
  } finally {
    creating.value = false
  }
}

const save = async (task: ScheduleTask) => {
  savingKey.value = `${task.group}/${task.key}`
  try {
    Object.assign(task, await scheduleTaskApi.update(task))
    useDiscreteApi.message.success(t('Schedule:Saved'))
  } catch {
    useDiscreteApi.message.error(t('Schedule:SaveFailed'))
  } finally {
    savingKey.value = ''
  }
}

const confirmDelete = (task: ScheduleTask) => {
  useDiscreteApi.dialog.warning({
    title: t('Schedule:DeleteTitle'),
    content: t('Schedule:DeleteConfirm'),
    positiveText: t('Common:Delete'),
    negativeText: t('Common:Cancel'),
    onPositiveClick: async () => {
      try {
        await scheduleTaskApi.delete(task.id)
        tasks.value = tasks.value.filter((item) => item.id !== task.id)
        useDiscreteApi.message.success(t('Schedule:Deleted'))
      } catch {
        useDiscreteApi.message.error(t('Schedule:DeleteFailed'))
      }
    },
  })
}

onMounted(() => void load())
</script>

<style scoped>
.task-list {
  display: grid;
  gap: 16px;
}

.task-card {
  align-content: start;
}

.task-card__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 18px;
}

.task-enabled-switch {
  flex: 0 0 88px;
  width: 88px;
}

.task-enabled-switch :deep(.n-switch__rail) {
  width: 100%;
}

.task-title__row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px 12px;
}

.task-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
}

.task-card h2 {
  margin: 0;
  font-size: 18px;
}

.task-card p {
  color: var(--po-ink-soft);
  font-size: 12.5px;
}

.task-fields {
  display: grid;
  grid-template-columns: minmax(240px, 1fr) 1.3fr;
  gap: 22px;
  align-items: start;
}

.cron-label {
  display: grid;
  gap: 7px;
  color: var(--po-ink-soft);
  font-size: 12px;
  font-weight: var(--po-weight-bold);
}

.task-meta {
  margin: 0;
}

.task-card__foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding-top: 12px;
  border-top: 1px dashed var(--po-line);
}

.task-card__foot code {
  color: var(--po-ink-faint);
  font-size: 11.5px;
  overflow-wrap: anywhere;
}

.task-actions,
.dialog-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

/* 卡片不再自身滚动：.n-card::after 描边层（inset: -2px）会在卡片成为滚动容器时
   向右/下各多出 2px 可滚动区域，表现为常驻横向滚动条且焦点变化时闪动；
   滚动交给内容区，标题栏与页脚固定 */
.create-dialog {
  width: min(820px, calc(100vw - 32px));
  max-height: calc(100vh - 32px);
}

.create-dialog :deep(.n-card-content) {
  min-height: 0;
  overflow-y: auto;
}

.type-fields {
  margin-top: 8px;
  padding: 18px;
  border: 2px dashed var(--po-line);
  border-radius: var(--po-r);
  background: var(--po-bg-deep);
}

.header-row {
  width: 100%;
  display: grid;
  grid-template-columns: 0.8fr 1.2fr;
  gap: 10px;
}

.dialog-actions {
  justify-content: flex-end;
}

@media (max-width: 800px) {
  .task-fields {
    grid-template-columns: minmax(0, 1fr);
  }

  .task-meta {
    grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  }
}

@media (max-width: 640px) {
  .po-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .po-toolbar :deep(.n-button) {
    width: 100%;
  }

  .task-card__head,
  .task-card__foot {
    flex-direction: column;
    align-items: stretch;
  }

  .task-enabled-switch {
    width: 100%;
    flex: 1 1 auto;
  }

  .task-actions :deep(.n-button) {
    flex: 1 1 auto;
  }

  .header-row {
    grid-template-columns: 1fr;
  }
}
</style>

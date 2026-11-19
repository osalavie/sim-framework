<template>
  <n-modal
    v-model:show="visible"
    preset="card"
    :title="t('Shortcut:Title')"
    :style="{ width: '600px', maxWidth: '90vw' }"
    :segmented="{ content: 'soft' }"
  >
    <n-scrollbar style="max-height: 60vh">
      <div class="shortcuts-help">
        <div
          v-for="(group, index) in shortcutGroups"
          :key="index"
          class="shortcuts-help__group"
        >
          <h3 class="shortcuts-help__group-title">{{ group.title }}</h3>
          <div class="shortcuts-help__list">
            <div
              v-for="shortcut in group.shortcuts"
              :key="shortcut.key"
              class="shortcuts-help__item"
            >
              <div class="shortcuts-help__keys">
                <kbd v-if="shortcut.ctrl" class="shortcuts-help__key">Ctrl</kbd>
                <kbd v-if="shortcut.shift" class="shortcuts-help__key">Shift</kbd>
                <kbd v-if="shortcut.alt" class="shortcuts-help__key">Alt</kbd>
                <kbd class="shortcuts-help__key">{{ shortcut.key }}</kbd>
              </div>
              <div class="shortcuts-help__description">
                {{ shortcut.description }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </n-scrollbar>

    <template #footer>
      <n-space justify="end">
        <n-button @click="visible = false">{{ t('Common:Close') }}</n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { t } from '@/i18n'

interface Shortcut {
  key: string
  ctrl?: boolean
  shift?: boolean
  alt?: boolean
  description: string
}

interface ShortcutGroup {
  title: string
  shortcuts: Shortcut[]
}

interface Props {
  show: boolean
  groups?: ShortcutGroup[]
}

const props = withDefaults(defineProps<Props>(), {
  groups: () => [],
})

const emit = defineEmits<{
  'update:show': [value: boolean]
}>()

const visible = computed({
  get: () => props.show,
  set: (value: boolean) => emit('update:show', value),
})

// 默认快捷键分组
const defaultGroups = computed<ShortcutGroup[]>(() => [
  {
    title: t('Shortcut:General'),
    shortcuts: [
      { key: 'N', ctrl: true, description: t('Shortcut:New') },
      { key: 'R', ctrl: true, description: t('Common:Refresh') },
      { key: 'S', ctrl: true, description: t('Common:Save') },
      { key: 'F', ctrl: true, description: t('Common:Search') },
      { key: '?', description: t('Shortcut:ShowHelp') },
    ],
  },
  {
    title: t('Shortcut:Navigation'),
    shortcuts: [
      { key: '↑', description: t('Shortcut:Previous') },
      { key: '↓', description: t('Shortcut:Next') },
      { key: 'Enter', description: t('Shortcut:EditCurrent') },
      { key: 'Delete', description: t('Shortcut:DeleteCurrent') },
    ],
  },
  {
    title: t('Shortcut:Modal'),
    shortcuts: [
      { key: 'Esc', description: t('Shortcut:CloseModal') },
      { key: 'Enter', ctrl: true, description: t('Shortcut:Submit') },
    ],
  },
])

const shortcutGroups = computed(() =>
  props.groups.length > 0 ? props.groups : defaultGroups.value,
)
</script>

<style scoped lang="scss">
.shortcuts-help {
  &__group {
    margin-bottom: 24px;

    &:last-child {
      margin-bottom: 0;
    }

    &-title {
      font-size: 16px;
      font-weight: 600;
      margin-bottom: 12px;
      color: var(--n-text-color);
    }
  }

  &__list {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  &__item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 8px 12px;
    background-color: var(--n-color-embedded);
    border-radius: 6px;
    transition: background-color 0.3s ease;

    &:hover {
      background-color: var(--n-color-embedded-hover);
    }
  }

  &__keys {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  &__key {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-width: 32px;
    height: 28px;
    padding: 0 8px;
    font-family: var(--po-font-mono);
    font-size: 13px;
    font-weight: 600;
    color: var(--n-text-color);
    background-color: var(--n-color);
    border: 1px solid var(--n-border-color);
    border-radius: 4px;
    box-shadow: 0 1px 0 var(--n-border-color);
  }

  &__description {
    font-size: 14px;
    color: var(--n-text-color-2);
  }
}

/* 移动端适配 */
@media (max-width: 768px) {
  .shortcuts-help {
    &__item {
      flex-direction: column;
      align-items: flex-start;
      gap: 8px;
    }

    &__keys {
      order: 2;
    }

    &__description {
      order: 1;
      font-weight: 500;
    }
  }
}
</style>

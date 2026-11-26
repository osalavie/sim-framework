<template>
  <span class="po-pill" :class="toneClass">{{ label }}</span>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { t } from '@/i18n'
import {
  flowStatusLabelKey,
  flowStatusToneClass,
  instanceStatusLabelKey,
  instanceStatusToneClass,
  nodeProgressStatusLabelKey,
  nodeProgressStatusToneClass,
  taskStatusLabelKey,
  taskStatusToneClass,
} from '../api/approvalModels'

type StatusKind = 'instance' | 'task' | 'flow' | 'node'

const props = withDefaults(defineProps<{ status?: string | null; kind?: StatusKind }>(), {
  status: null,
  kind: 'instance',
})

const labelKeyOf: Record<StatusKind, (status?: string | null) => string> = {
  instance: instanceStatusLabelKey,
  task: taskStatusLabelKey,
  flow: flowStatusLabelKey,
  node: nodeProgressStatusLabelKey,
}

const toneOf: Record<StatusKind, (status?: string | null) => string> = {
  instance: instanceStatusToneClass,
  task: taskStatusToneClass,
  flow: flowStatusToneClass,
  node: nodeProgressStatusToneClass,
}

const label = computed(() => t(labelKeyOf[props.kind](props.status)))
const toneClass = computed(() => toneOf[props.kind](props.status))
</script>

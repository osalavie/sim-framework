import { defineStore } from 'pinia'
import { ref } from 'vue'
import { DENSITY_KEY } from './persistentKeys'

/** 内容密度：小 / 中 / 大三档，驱动表格行高与内容区间距。 */
export type ContentDensity = 'compact' | 'standard' | 'roomy'

const PERSIST_CONFIG = {
  key: DENSITY_KEY,
  storage: localStorage,
}

export const useDensityStore = defineStore(
  DENSITY_KEY,
  () => {
    const density = ref<ContentDensity>('roomy')

    const setDensity = (next: ContentDensity) => {
      density.value = next
    }

    return { density, setDensity }
  },
  { persist: PERSIST_CONFIG },
)

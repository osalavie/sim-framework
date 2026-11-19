import { defineStore } from 'pinia'
import { readonly, ref } from 'vue'
import { apiConfig } from '@/config/apiConfig'
import {
  createSiteStatusRealtimeConnection,
  type SiteStatusRealtimeConnection,
  type SiteStatusRealtimePayload,
} from '@/modules/siteStatus/realtime'

export interface MaintenanceConfiguration {
  enabled: boolean
  message?: string
}

/**
 * 站点级运行状态（当前为维护模式）唯一数据源。
 *
 * - 页面刷新后由路由守卫读取一次状态接口，作为长连接建立前的兜底；
 * - 之后由匿名 SignalR `/hubs/site-status` 推送维护开关变更，全站共享同一份状态；
 * - 业务请求收到 503 时仍会把拦截器解析出的维护信息写回这里。
 */
export const useSiteStatusStore = defineStore('site-status', () => {
  const state = ref<MaintenanceConfiguration>({
    enabled: false,
    message: undefined,
  })

  /** 只读的维护配置快照，供路由守卫、维护页和请求拦截器共享。 */
  const maintenanceMode = readonly(state)

  const loaded = ref(false)
  let loadPromise: Promise<void> | undefined
  let realtime: SiteStatusRealtimeConnection | undefined

  const applyPayload = (payload: SiteStatusRealtimePayload | null | undefined) => {
    state.value = {
      enabled: payload?.maintenanceMode === true,
      message: payload?.maintenanceMessage?.trim() || undefined,
    }
    loaded.value = true
  }

  const applyConfiguration = (configuration: MaintenanceConfiguration) => {
    state.value = {
      enabled: configuration.enabled === true,
      message: configuration.message?.trim() || undefined,
    }
    loaded.value = true
  }

  const load = (force = false): Promise<void> => {
    if (loaded.value && !force) return Promise.resolve()
    if (loadPromise) return loadPromise

    const baseUrl = apiConfig.baseUrl.replace(/\/$/, '')
    const request = fetch(`${baseUrl}/api/admin/site-status`, {
      headers: { Accept: 'application/json' },
    })
      .then(async (response) => {
        if (!response.ok) return
        applyPayload((await response.json()) as SiteStatusRealtimePayload)
      })
      .catch(() => {
        // 状态接口瞬时失败不应锁死页面；后续维护拦截与长连接推送仍会纠正状态。
      })
      .finally(() => {
        // 每次页面加载只兜底读取一次；显式刷新可传 force。
        loaded.value = true
        if (loadPromise === request) {
          loadPromise = undefined
        }
      })

    loadPromise = request
    return request
  }

  const startRealtime = () => {
    if (realtime) return
    realtime = createSiteStatusRealtimeConnection(applyPayload)
    void realtime.start()
  }

  const stopRealtime = async () => {
    const current = realtime
    realtime = undefined
    await current?.stop()
  }

  return {
    maintenanceMode,
    loaded,
    load,
    applyPayload,
    applyConfiguration,
    startRealtime,
    stopRealtime,
  }
})

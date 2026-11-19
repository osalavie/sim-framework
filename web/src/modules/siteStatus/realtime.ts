import type { HubConnection } from '@microsoft/signalr'
import { apiConfig } from '@/config/apiConfig'
import { loadSignalr } from '@/utils/signalrLoader'

const siteStatusChangedEvent = 'siteStatusChanged'

export interface SiteStatusRealtimePayload {
  maintenanceMode?: boolean
  maintenanceMessage?: string | null
}

function getHubUrl() {
  const baseUrl = apiConfig.baseUrl.replace(/\/$/, '')
  return `${baseUrl}/hubs/site-status`
}

export interface SiteStatusRealtimeConnection {
  start: () => Promise<void>
  stop: () => Promise<void>
}

/**
 * 匿名站点状态通道：维护开关变更时由服务端推送到所有已连接的 Web 客户端，
 * 客户端不再按导航轮询状态接口。
 */
export function createSiteStatusRealtimeConnection(
  onChanged: (payload: SiteStatusRealtimePayload) => void,
): SiteStatusRealtimeConnection {
  let stopped = false
  let retryTimer: ReturnType<typeof setTimeout> | undefined
  let connection: HubConnection | undefined

  const scheduleStart = () => {
    if (stopped || retryTimer) return
    retryTimer = setTimeout(() => {
      retryTimer = undefined
      void start()
    }, 5_000)
  }

  const ensureConnection = async (): Promise<HubConnection> => {
    if (connection) return connection

    const { HubConnectionBuilder, LogLevel } = await loadSignalr()
    const created = new HubConnectionBuilder()
      .withUrl(getHubUrl())
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
          Math.min(1_000 * 2 ** previousRetryCount, 30_000),
      })
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()

    created.on(siteStatusChangedEvent, (payload: SiteStatusRealtimePayload | null) => {
      onChanged(payload ?? {})
    })
    created.onclose(() => scheduleStart())
    connection = created
    return created
  }

  const start = async () => {
    if (stopped) return

    try {
      const [{ HubConnectionState }, active] = await Promise.all([
        loadSignalr(),
        ensureConnection(),
      ])
      if (
        stopped ||
        active.state === HubConnectionState.Connected ||
        active.state === HubConnectionState.Connecting ||
        active.state === HubConnectionState.Reconnecting
      ) {
        return
      }

      await active.start()
    } catch {
      scheduleStart()
    }
  }

  const stop = async () => {
    stopped = true
    if (retryTimer) clearTimeout(retryTimer)
    retryTimer = undefined
    if (connection) await connection.stop()
  }

  return { start, stop }
}

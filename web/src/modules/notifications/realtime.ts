import type { HubConnection } from '@microsoft/signalr'
import { apiConfig } from '@/config/apiConfig'
import { getAccessToken } from '@/utils/authSession'
import { loadSignalr } from '@/utils/signalrLoader'

const notificationChangedEvent = 'notificationChanged'

function getHubUrl() {
  const baseUrl = apiConfig.baseUrl.replace(/\/$/, '')
  return `${baseUrl}/hubs/user-notifications`
}

export interface UserNotificationRealtimeConnection {
  start: () => Promise<void>
  stop: () => Promise<void>
}

export function createUserNotificationRealtimeConnection(
  onChanged: () => void | Promise<void>,
): UserNotificationRealtimeConnection {
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
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => getAccessToken() ?? '',
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
          Math.min(1_000 * 2 ** previousRetryCount, 30_000),
      })
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()

    created.on(notificationChangedEvent, () => void onChanged())
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

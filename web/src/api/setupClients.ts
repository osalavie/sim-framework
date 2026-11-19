import { client as adminClient } from './admin/client.gen'
import { client as systemClient } from './system/client.gen'
import { client as approvalClient } from './approval/client.gen'
import { client as openPlatformClient } from './openplatform/client.gen'
import request from '@/utils/request'

const clients = [adminClient, systemClient, approvalClient, openPlatformClient]

/**
 * 将 SDK client 统一绑定到全局 axios 实例（src/utils/request.ts），
 * 使 token 刷新、401 跳转、错误提示、维护模式、请求去重/取消等横切能力
 * 对 SDK 调用同样生效。需在应用启动时最先执行（见 main.ts）。
 */
export function setupApiClients() {
  for (const client of clients) {
    client.setConfig({ axios: request })
  }
}

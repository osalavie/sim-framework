/**
 * 延迟加载 @microsoft/signalr。
 *
 * Hub 连接只在建连时才需要，静态引入会把 40KB+ 的 SignalR 运行时放进入口包；
 * 这里统一按需导入，两个实时通道共用同一个模块 Promise。
 */
let signalr: Promise<typeof import('@microsoft/signalr')> | undefined

export function loadSignalr() {
  signalr ??= import('@microsoft/signalr')
  return signalr
}

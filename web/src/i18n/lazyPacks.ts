import { registerMessages, type MessageDictionary } from './messages'

/**
 * 功能模块语言包按需加载
 *
 * 模块语言包若随入口注册，会把整块文案打进入口包；这里改为在模块路由首次进入时
 * 注册（见 `src/utils/dynamicRouteLoader.ts`），键缺失时 `t()` 会回退为键名，
 * 因此注册必须发生在组件模块导入之前。
 */
export type MessagePackName =
  | 'approval'
  | 'canvas'
  | 'organization'
  | 'authz'
  | 'notifications'
  | 'e2ee'
  | 'openplatform'

type MessagePackModule = { default: MessageDictionary }

/** 中英语言包并行加载；单个语言失败不能拖累另一种语言注册。 */
type LocalePackResults = [
  PromiseSettledResult<MessagePackModule>,
  PromiseSettledResult<MessagePackModule>,
]

/**
 * 业务应用注册的语言包
 *
 * 框架库构建期无法扫描业务仓的文案文件，业务仓在 `createApp` 之前调用
 * `registerMessagePacks` 提供加载器，组件路径命中 `componentPrefixes` 时按需加载。
 */
export interface MessagePack {
  /** 语言包名称，不能与框架内置语言包重名。 */
  name: string
  /** 组件路径前缀，与后端路由的 component 字段保持一致。 */
  componentPrefixes: string[]
  load: () => Promise<Partial<Record<'zh-CN' | 'en-US', MessageDictionary>>>
}

interface ComponentPackRule {
  prefix: string
  packs: string[]
}

/** 每个语言包同时注册中英两套资源，切换语言无需重新加载。 */
const packLoaders: Record<MessagePackName, () => Promise<LocalePackResults>> = {
  approval: () =>
    Promise.allSettled([import('./messages/zh-CN.approval'), import('./messages/en-US.approval')]),
  canvas: () =>
    Promise.allSettled([import('./messages/zh-CN.canvas'), import('./messages/en-US.canvas')]),
  organization: () =>
    Promise.allSettled([
      import('./messages/zh-CN.organization'),
      import('./messages/en-US.organization'),
    ]),
  authz: () =>
    Promise.allSettled([import('./messages/zh-CN.authz'), import('./messages/en-US.authz')]),
  notifications: () =>
    Promise.allSettled([
      import('./messages/zh-CN.notifications'),
      import('./messages/en-US.notifications'),
    ]),
  e2ee: () =>
    Promise.allSettled([import('./messages/zh-CN.e2ee'), import('./messages/en-US.e2ee')]),
  openplatform: () =>
    Promise.allSettled([
      import('./messages/zh-CN.openplatform'),
      import('./messages/en-US.openplatform'),
    ]),
}

/**
 * 组件路径前缀 → 语言包，与后端种子 `RouteDataSeeder` 的 component 字段一致。
 *
 * `notifications` 语言包的文件名对应通知服务模块的同名资源，但只有
 * `systemManagement/views/notifications` 页面消费这些键（顶栏通知面板用的是 `header.*`）。
 */
const COMPONENT_PACK_RULES: readonly ComponentPackRule[] = [
  { prefix: 'approval/', packs: ['approval'] },
  { prefix: 'openplatform/', packs: ['openplatform'] },
  { prefix: 'authorization/', packs: ['organization', 'authz'] },
  { prefix: 'systemManagement/views/notifications/', packs: ['notifications'] },
]

const loadedPacks = new Set<MessagePackName>()
const pendingPacks = new Map<MessagePackName, Promise<void>>()

const customPacks = new Map<string, MessagePack>()
const customRules: ComponentPackRule[] = []
const customLoadedPacks = new Set<string>()
const customPendingPacks = new Map<string, Promise<void>>()

/** 全部按需语言包，供一致性校验使用。 */
export const messagePackNames = Object.keys(packLoaders) as MessagePackName[]

/** 注册业务应用的语言包；重复注册同名包时后注册的覆盖先注册的。 */
export function registerMessagePacks(packs: MessagePack[]): void {
  for (const pack of packs) {
    if (Object.prototype.hasOwnProperty.call(packLoaders, pack.name)) {
      throw new Error(`Message pack "${pack.name}" conflicts with a built-in pack.`)
    }

    customPacks.set(pack.name, pack)
    for (let index = customRules.length - 1; index >= 0; index -= 1) {
      if (customRules[index]?.packs.includes(pack.name)) {
        customRules.splice(index, 1)
      }
    }
    customRules.push(...pack.componentPrefixes.map((prefix) => ({ prefix, packs: [pack.name] })))
  }
}

/** 解析组件路径所需语言包；未声明的组件返回空数组。 */
export function messagePacksForComponent(componentPath: string): string[] {
  return [...COMPONENT_PACK_RULES, ...customRules]
    .filter((rule) => componentPath.startsWith(rule.prefix))
    .flatMap((rule) => rule.packs)
}

/** 注册语言包；并发调用复用同一 Promise，已注册的包直接短路。 */
export function ensureMessagePacks(names: readonly string[]): Promise<void> {
  const tasks = names
    .filter((name) => !isPackLoaded(name))
    .map((name) =>
      Object.prototype.hasOwnProperty.call(packLoaders, name)
        ? loadPack(name as MessagePackName)
        : loadCustomPack(name),
    )
  return tasks.length ? Promise.all(tasks).then(() => undefined) : Promise.resolve()
}

function isPackLoaded(name: string): boolean {
  return Object.prototype.hasOwnProperty.call(packLoaders, name)
    ? loadedPacks.has(name as MessagePackName)
    : customLoadedPacks.has(name)
}

function loadCustomPack(name: string): Promise<void> {
  const pending = customPendingPacks.get(name)
  if (pending) return pending

  const pack = customPacks.get(name)
  if (!pack) return Promise.resolve()

  const task = pack
    .load()
    .then((messages) => {
      if (messages['zh-CN']) registerMessages('zh-CN', messages['zh-CN'])
      if (messages['en-US']) registerMessages('en-US', messages['en-US'])
      customLoadedPacks.add(name)
    })
    .finally(() => {
      customPendingPacks.delete(name)
    })

  customPendingPacks.set(name, task)
  return task
}

function loadPack(name: MessagePackName): Promise<void> {
  const pending = pendingPacks.get(name)
  if (pending) return pending

  const task = packLoaders[name]()
    .then(([zh, en]) => {
      if (zh.status === 'fulfilled') registerMessages('zh-CN', zh.value.default)
      if (en.status === 'fulfilled') registerMessages('en-US', en.value.default)
      if (zh.status === 'rejected' && en.status === 'rejected') {
        throw new Error(`Failed to load the ${name} message packs.`)
      }
      loadedPacks.add(name)
    })
    .finally(() => {
      pendingPacks.delete(name)
    })

  pendingPacks.set(name, task)
  return task
}

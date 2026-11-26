import { defineConfig } from '@hey-api/openapi-ts'

/**
 * 四组 OpenAPI 契约（单一来源：src/api/definition/*.json）分别生成独立目录 src/api/{group}。
 * 生成物直接落在 src/api 下（与手写的 route/fileManagement 等共享文件同层），
 * 每组内部含 client.gen.ts/sdk.gen.ts/types.gen.ts 及 bundle 的 client/、core/ 子目录。
 * 函数名采用 method + path（去掉统一前缀 /api），例如：
 *   GET  /api/system/user                  → getSystemUser
 *   PUT  /api/system/business-number-definition/{id} → putSystemBusinessNumberDefinitionId
 * 请求 URL 保持完整契约路径，仅函数名去除 /api 前缀。
 */
const groups = ['admin', 'system', 'approval', 'openplatform']

export default defineConfig({
  input: groups.map((group) => `src/api/definition/${group}.json`),
  output: groups.map((group) => `src/api/${group}`),
  plugins: [
    '@hey-api/typescript',
    {
      name: '@hey-api/sdk',
      operations: {
        nesting: (operation) => {
          const segments = operation.path.split(/[\/{}]+/).filter(Boolean)
          const rest = segments[0] === 'api' ? segments.slice(1) : segments
          return [operation.method.toLowerCase(), ...rest]
        },
      },
    },
    // 使用默认 bundle 模式：client 实现复制到 <output>/client 子目录。
    // 不能设 bundle:false（直接 import @hey-api/client-axios 包），
    // 因为 npm 最新版 0.9.1 缺少生成代码所需的 ClientMeta 等 API（上游版本未配套）。
    {
      name: '@hey-api/client-axios',
      // 显式关闭 baseUrl：契约无 servers，生成器会把相对 input 路径 'src/api/definition/xx.json'
      // 解析为 host='src'，从而在 createConfig 里写入 baseURL: 'src'，导致请求 URL 变为
      // 'src/api/...'（无前导斜杠）→ 浏览器相对解析 → 运行时 404。
      // 实际 baseUrl 由运行时 request.ts 的 axios 实例提供（apiConfig.baseUrl，dev 为空走 Vite 代理）。
      baseUrl: false,
    },
  ],
})

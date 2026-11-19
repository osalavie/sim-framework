# 开发脚本

除模块脚手架外，项目还包含以下质量脚本：

- `pnpm generate:api-types`（`openapi-ts`）：从 `src/api/definition`（后端导出的契约单一来源）生成 `src/api/{group}`（admin/system/approval/openplatform）的类型与 SDK 客户端。
- `check-bundle-budget.mjs`：读取生产构建 manifest，校验 `performance-budget.json`。
- `check-dependency-licenses.mjs`：校验生产依赖许可证报告只包含已审批表达式。
- `fonts/build-maple-mono.py`：从 Maple Mono 官方 release 重新生成 `src/assets/fonts/maple-mono` 与 `src/styles/maple-mono.css` 的全站字体切片（400 / 700 / 900 / 斜体），仅在升级字体版本时运行；需要 `fonttools`、`brotli`、`wordfreq`。

## create-module.js

快速创建新模块的脚本。

### 使用方法

```bash
node scripts/create-module.js <module-name> [module-title]
```

### 参数说明

- `module-name`：模块名称（必填），使用 kebab-case 格式，如 `product`、`user-management`
- `module-title`：模块标题（可选），如 `产品管理`、`用户管理`

### 示例

```bash
# 创建产品管理模块
node scripts/create-module.js product 产品管理

# 创建订单系统模块
node scripts/create-module.js order-system 订单系统

# 只提供模块名（标题将使用模块名）
node scripts/create-module.js approval
```

### 生成的文件结构

```
src/modules/<module-name>/
├── api/
│   └── index.ts          # API 接口和类型定义
├── views/
│   └── list.vue          # 列表页面（包含增删改查）
├── routes.ts             # 路由配置
└── index.ts              # 模块配置
```

### 生成后的步骤

1. 在 `src/config/modules.ts` 中注册模块
2. 重启开发服务器
3. 访问模块页面开始开发

### 注意事项

- 模块名称必须是 kebab-case 格式（小写字母、数字、连字符）
- 模块名称不能与现有模块重复
- 生成的代码包含完整的 CRUD 功能，可直接使用或根据需求修改

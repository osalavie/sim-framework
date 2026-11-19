# CLAUDE.md

本文件是 Claude Code 的仓库入口。项目协作规则以根目录 [`AGENTS.md`](AGENTS.md) 为准，完整文档地图见 [`docs/README.md`](docs/README.md)。

## 开始前

1. 阅读 [`AGENTS.md`](AGENTS.md) 与 [`docs/core/AI_CODING_GUIDELINES.md`](docs/core/AI_CODING_GUIDELINES.md)。
2. 按任务从 [`docs/README.md`](docs/README.md) 找到对应现役文档。
3. 先读周边代码，沿用现有模式，小范围修改，完成后说明验证结果。
4. 功能增强或缺陷修复必须检查 [`docs/core/ENTERPRISE_SYSTEM_GUIDE.md`](docs/core/ENTERPRISE_SYSTEM_GUIDE.md)；如能力、风险或完成标准变化，同步更新正文。

## 快速命令

### 后端 `api/`

```bash
dotnet restore
dotnet build Sim.slnx
dotnet run --project src/Sim.Admin.HttpApi.Host
dotnet test
dotnet test --filter "FullyQualifiedName~ClassName"
```

### Web `web/`

```bash
pnpm install
pnpm dev
pnpm type-check
pnpm lint
pnpm test:unit
pnpm test:e2e
pnpm build:production
```

## 架构速览

- `api`：.NET 10 模块化单体，唯一运行入口为 `src/Sim.Admin.HttpApi.Host`；业务模块位于 `modules/`（approval、system、tenant、worker），跨模块只依赖 Contracts 或 Domain.Shared。
- `web`：Vue 3 + Vite + Pinia + Naive UI；业务模块位于 `src/modules/`，每个模块导出 `index.ts`（`ModuleConfig`）与 `routes.ts`，业务模块在 `src/config/businessModules.ts` 注册。
- 业务系统从本仓创建独立仓库时，只修改业务区；目录分区、`upstream` 合并与验收要求见 [`docs/core/FRAMEWORK_SYNC.md`](docs/core/FRAMEWORK_SYNC.md)。

更细的边界、认证、数据库与前端模块说明见 [`docs/core/ARCHITECTURE.md`](docs/core/ARCHITECTURE.md)、[`api/docs/ARCHITECTURE.md`](api/docs/ARCHITECTURE.md) 与 [`web/docs/ARCHITECTURE.md`](web/docs/ARCHITECTURE.md)。

## 高风险边界

- 所有前端请求复用 `web/src/utils/request.ts`，不要创建第二个 Axios 认证状态机。
- 不要手工编辑 `web/src/api/{group}` 生成目录；契约变更先重生成 `definition/`，再运行 `pnpm generate:api-types`。
- 不要绕过 `[Permission]`、CSRF、Refresh Token Cookie 或限流策略；权限预览不能替代后端实时判定。
- 不要提交密钥、生产连接串或真实账号密码；环境配置由外部注入。
- 不要在单文件内混用 CRLF/LF：普通文本使用 CRLF，`*.sh`、`Dockerfile`、`Jenkinsfile` 使用 LF。

## 提交信息

- 首行：`feat(scope): <中文祈使句>`，类型可用 `feat/fix/chore/refactor/docs/test/build/ci`；scope 使用 `web`、`system`、`worker`、`api`、`docs` 等。
- 正文可选 `- ` 列表说明原因。
- 一个逻辑变更对应一个提交；用户说“提交”时只提交当前已暂存文件，不自动扫入无关改动。

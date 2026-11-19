# 基座同步与业务系统接入

本仓库同时承担两个角色：基座仓（上游 `upstream`）和业务系统模板。业务系统从本仓创建独立 Git 仓库，只在自己的业务区开发；基座更新通过合并 `upstream` 获得。仓库的模块边界、契约端口和架构测试保证业务代码可以跟着基座升级，组合根隔离保证升级冲突可控。

## 适用前提

- 业务系统直接复用本仓的后端、Web 和 Windows 客户端，不把基座代码复制到业务目录。
- 业务系统按发布基线持续升级，不长期停留在旧基座。
- 业务需要修改基座能力时，先回流基座仓，再向下同步；不要长期只在业务仓修改基座文件。

## 目录分区

除业务区与组合根外，仓库其余文件均属基座区。业务仓不修改基座区；确实需要修复基座缺陷时，先在基座仓修复并发布，再同步下来。

`api/modules/openplatform/` 与 `web/src/modules/openplatform/` 是平台开放能力，属于基座区；第三方业务模块只通过权限目录向开放平台提供可授予的业务权限。

**业务区（业务仓独占，基座同步不会改动）**

| 路径 | 用途 |
| --- | --- |
| `api/modules/<业务>/` | 业务后端模块，按 `Domain.Shared`、`Domain`、`Application.Contracts`、`Application`、`SqlSugar` 五层拆分 |
| `api/src/Sim.Admin.HttpApi.Host/Business/` | 业务模块工程引用与 `[DependsOn]` 声明 |
| `api/Directory.Packages.business.props` | 业务模块新增 NuGet 包的中央版本 |
| `web/src/modules/<业务>/` | 业务页面、路由、API 调用、业务语言包与业务专属能力（例如文章 E2EE） |
| `web/src/config/businessModules.ts` | Web 业务模块清单与业务装配入口（`bootstrapBusinessModules`） |
| `web/src/config/businessMessages.ts` | 业务语言包注册入口，供应用入口与测试入口复用 |
| `client/<业务>/` | 业务客户端源码（WPF 页面与 API 封装），由 `BusinessSources.props` 纳入基座工程编译 |
| `deploy/business/` | 业务部署覆盖文件 |
| `docs/business/` | 业务文档 |

**组合根（业务仓可改，同步冲突时保留双方内容）**

| 路径 | 约束 |
| --- | --- |
| `api/Sim.slnx` | 只在末尾新增 `/business/` 文件夹并追加业务工程，不重排基座条目 |
| `api/src/Sim.Admin.HttpApi.Host/appsettings.json` | 只在 `Swagger:Documents` 追加业务分组 |
| `web/src/main.ts` | 只调用 `bootstrapBusinessModules()`，不直接引用业务模块 |
| `web/package.json`、`web/pnpm-lock.yaml` | 追加业务依赖；锁文件冲突时重新生成，不手工合并 |
| `client/Sim.Admin.Wpf.Client/Business/BusinessSources.props` | 只追加 `client/<业务>/` 的 Compile/Page 条目，不修改客户端 csproj |
| `client/Sim.Admin.Wpf.Client/Business/BusinessModuleRegistration.cs` | 只在 `Modules` 追加业务客户端模块 |
| `api/src/Sim.Admin.HttpApi.Host/packages*.lock.json` | 业务新增包后重新生成 |
| 环境配置与部署脚本 | 业务环境差异只写在业务仓自己的配置中 |

## 业务仓初始化

```bash
git clone git@gitee.com:salavie/sim-framework.git qms
cd qms
git remote rename origin upstream
git remote add origin git@gitee.com:<组织>/qms.git
git push -u origin main
```

- 不要用 Gitee fork 长期维护业务仓；fork 的合并语义不适合基座向下同步。使用普通仓库并保留 `upstream` 远端。
- Gitee 仓库设置可将基座仓标记为模板仓，便于创建新业务仓；无论是否使用模板功能，都必须保留 `upstream` 远端。

## 基座发布基线

- 基座仓在 `main` 的可交付状态打标签 `vX.Y.Z`，业务仓按标签同步，便于回溯和回滚。
- 业务仓记录当前基线：`git describe --tags --match 'v*'`。
- 两次基线之间的变更：`git log --oneline v1.0.0..v1.1.0`。
- 基座发布前运行本文末尾的升级验收清单，避免把破坏性变更带到业务仓。

基座统一使用 `Sim` 命名基线：程序集与命名空间 `Sim.*`、解决方案 `api/Sim.slnx`、错误码 `Sim:<模块>:`、数据库表前缀 `sim_`（Quartz `sim_qrtz_`）、镜像与环境变量 `sim-api` / `sim-web` / `SIM_*`。从旧命名基线同步的业务仓必须在同一次同步中把业务代码的程序集引用、表前缀、配置键和部署变量改为 `Sim`，并按新基线重建数据库；新旧标识不兼容。DataProtection 应用名与用途串、E2EE 持久化标识（AAD 命名空间、MIME、文件扩展名、恢复密钥头）也已切换到 `Sim`，已有密文需在业务装配入口用 `configureE2eeProfile` 固定旧标识或迁移。

## 同步基座更新

```bash
git status                 # 工作区必须干净
git fetch upstream --tags
git switch -c sync/v1.1.0
git merge v1.1.0      # 或跟踪滚动基线：git merge upstream/main
```

同步分支完成验证后再合入业务主干。建议同步提交单独成 commit，不夹带业务功能改动，便于回滚。

## 冲突处理原则

1. **基座区冲突**：先读 `upstream` 对该文件的改动，取 `upstream` 内容后再手工重放业务改动；不要用 `git checkout --ours` 一键丢弃基座修复。
2. **组合根冲突**：双方都保留。`Sim.slnx` 同时保留基座新增项目与业务项目；`Swagger:Documents` 同时保留平台与业务分组；`package.json` 保留基座升级后的版本与业务依赖。
3. **锁文件冲突**：先取上游版本，再重新生成，不手工合并。

   ```bash
   git checkout --theirs web/pnpm-lock.yaml
   (cd web && pnpm install)
   git checkout --theirs api/src/Sim.Admin.HttpApi.Host/packages.lock.json
   git checkout --theirs api/src/Sim.Admin.HttpApi.Host/packages.linux.lock.json
   dotnet restore api/src/Sim.Admin.HttpApi.Host --force-evaluate
   ```

4. **不要整体偏向一个分支**：禁止 `git merge -X ours` 或 `-X theirs` 掩盖冲突。
5. **业务对基座的修复先回流**：基座缺陷在基座仓修复并发布，再同步；长期在业务仓改基座文件会让后续每次同步都产生同一冲突。

## 后端业务模块接入

1. 在 `api/modules/<业务>/` 创建五个项目，命名遵循 `Sim.<业务>.Application` 等约定，业务根路径由约定式控制器自动生成为 `/{业务}`。
2. 在 `api/Sim.slnx` 末尾新增 `/business/` 文件夹并追加业务工程，不重排基座条目。
3. 在 `api/src/Sim.Admin.HttpApi.Host/Business/BusinessProjects.props` 追加业务 `Application` 与 `SqlSugar` 工程的 `ProjectReference`，Host csproj 不需要修改。
4. 在 `api/src/Sim.Admin.HttpApi.Host/Business/BusinessModuleRegistration.cs` 的 partial 类上追加 `[DependsOn]`，保留 `AssemblyRef`，保证约定式控制器能发现业务模块。
5. 在 `appsettings.json` 的 `Swagger:Documents` 追加业务分组，`Name` 与业务根路径一致（例如 `qms`）。
6. 菜单、路由、权限和业务编号在业务模块内实现 `IModuleSeedContributor`，随 `--seed` 幂等写入，不修改平台种子。
7. 业务新增 NuGet 包只在 `api/Directory.Packages.business.props` 声明版本，不修改 `Directory.Packages.props`。
8. 跨模块只引用其他模块的 `*.Application.Contracts` 或 `*.Domain.Shared`；不引用其他模块的 `Application`、`Domain`、`SqlSugar` 实现。
9. 用户生命周期联动实现 `IUserCreatedHandler` / `IUserDeletedHandler`；组织数据只读 `IOrganizationDirectory`；审批接入走 Approval Contracts；文件内容使用 `IContentFileService`；通知发送走 System 通知 Contracts。
10. 错误码放在 `modules/<业务>/<项目>/<业务>ErrorCodes.cs`，类名为 `<业务>ErrorCodes`，错误码使用 `Sim:<业务>:` 前缀；业务代码不得硬编码 `"Sim:` 字面量，否则架构测试失败。
11. 业务模块需要 `Microsoft.AspNetCore.App` 共享框架引用时，在 `api/tests/Sim.Security.Tests/framework-reference-reviews.business.txt` 登记项目路径，由架构测试审查；不修改基座测试的硬编码清单。
12. 业务权限由 `ModuleSeedContributor.PermissionSeeds` 自描述，权限目录测试按约定反射发现；不修改基座测试引用业务程序集。

## Web 业务模块接入

1. 业务页面放在 `web/src/modules/<业务>/`，导出 `index.ts`（`ModuleConfig`）和 `routes.ts`。
2. 在 `web/src/config/businessModules.ts` 的 `businessModules` 数组追加业务模块，不修改平台模块清单。
3. 动态路由的 `component` 路径相对 `web/src/modules` 保存（例如 `qms/views/list.vue`），由后端菜单种子写入。
4. 业务页面需要使用共享组件、请求实例和 store 时，沿用现有 `@/` 别名引用平台实现；不要新建第二套认证或请求状态机。
5. 业务接口 SDK 生成到 `web/src/modules/<业务>/api/`，并绑定 `@/utils/request` 的共享 axios 实例；不要生成到平台契约目录 `web/src/api/`。
6. 业务语言包与页面放在同一模块目录，由 `web/src/config/businessMessages.ts` 注册；应用入口 `main.ts` 与 `vitest.setup.ts` 只调用业务装配入口，不直接 import 业务语言包。
7. 业务组件在模板仓模式下随 `src/modules/**` 自动扫描；如果后续改为独立 Web 应用，需在入口调用 `registerModuleComponents(import.meta.glob('./modules/**/*.vue'), 'modules/')`。
8. 业务需要改变框架组件行为时，在业务区内基于框架组件封装，或通过框架预留端口/路由元数据接入；不直接修改框架组件内部。

## WPF 业务客户端接入

1. 业务客户端源码放在 `client/<业务>/`，不创建独立工程；由 `Business/BusinessSources.props` 以 `Compile`/`Page` 通配纳入 `Sim.Admin.Wpf.Client` 编译，避免基座与业务的双向工程引用。
2. 实现 `IBusinessClientModule`：`Key` 与后端业务根路径一致，菜单 key 约定为 `<业务>.<页面>`，`Order` 决定分组在平台菜单之后的位置。
3. 在 `Business/BusinessModuleRegistration.cs` 的 `Modules` 追加模块实例；基座保持空数组。
4. 工作区实现 `IBusinessWorkspace`，通过 `BusinessClientContext.Api` 调用后端（`ApiClient` 已登录并自动刷新令牌）；会话失效调用 `EndSession` 统一退出。
5. 业务菜单分组接在平台菜单之后，首次点击叶节点时懒创建标签页；业务 XAML 只引用基座主题语义色与 `Custom*` 控件，不新建第二套样式或请求状态机。
6. 不修改 `MainWindow`、`Sim.Admin.Wpf.Client.csproj` 或客户端 `Services/`；需要框架能力时先在基座仓扩展。

## 业务扩展框架能力

框架只保留通用能力与扩展点，业务差异放在业务区：

| 场景 | 框架扩展点 | 业务实现 |
| --- | --- | --- |
| 文章导出签名/作者资料 | `web/src/components/ArticleExport/adapter.ts` | 在 `businessModules.ts` 调用 `registerArticleExportAdapter` |
| E2EE 历史密文兼容 | `web/src/e2ee/profile.ts` | 在业务装配入口调用 `configureE2eeProfile` 固定历史格式标识 |
| 文章/内容加密流程 | `web/src/e2ee/` 提供通用 SM4/SM3、KDF、UMK、设备缓存、通行密钥 | 业务在自己的 `modules/<业务>/e2ee/` 中定义领域类型、AAD 与分享协议 |
| 登录/注册页外观与跳转 | `web/src/views/login.vue` 读取路由 `meta` | 业务路由提供 `authMode`、`authLayout`、`authCopyNamespace`、`authLoginRoute`、`authRegisterRoute`、`authLandingRoute`、`authHomeRoute`、`authHomeLabelKey` |
| 客户端菜单文案 | `IClientLocalizationContributor` | 业务实现贡献器，从自己的 `LocalizationResource` 读取文本 |

框架组件不得出现 `@/modules/*` 引用；发现耦合应把领域代码移入业务区，或在框架中新增与业务无关的端口。

## 业务菜单本地化

菜单键归后端所有：动态菜单的 `meta.title` 保存 `Menu:*` key，由 `ClientLocalizationService` 合并基座白名单与业务贡献器下发。业务模块新增菜单时：

1. 在业务模块内实现 `IClientLocalizationContributor`，返回本模块的菜单键与当前文化文本；
2. 在业务模块自己的 `LocalizationResource` 中补充中英文资源；
3. 菜单种子使用相同 key。

基座 `MenuLocalizationKeys` 与 `AdminResource` 不登记业务菜单键，避免基座与业务耦合。

## 升级验收清单

同步基座后至少完成以下验证，再合入业务主干：

- `dotnet build api/Sim.slnx` 通过。
- `dotnet test api/Sim.slnx --no-build --no-restore` 通过；架构依赖与中央版本测试未退化。
- `cd web && pnpm install && pnpm type-check && pnpm build` 通过。
- 后端启动后 `/health/live`、`/health/ready` 正常。
- `/setup` 已完成或执行 `--seed` 后，业务模块菜单、路由、权限和业务编号正常。
- 登录后业务菜单可见、页面可打开、按钮权限生效，业务主流程冒烟通过。
- 平台、业务接口的 OpenAPI 契约与提交的 `definition/` 一致，无未提交漂移。
- 涉及数据库结构升级时，已按 `--migrate` 流程在测试库验证，并准备回滚脚本。
- Windows 客户端（如业务仓已接入）完成登录、刷新与业务页面冒烟。

## 禁止事项

- 不在业务仓直接修改基座区文件，除非同一次变更把修复回流基座仓。
- 不把基座代码复制或剪切到业务目录。
- 不在框架 `web/src/e2ee/`、共享组件或登录页中加入业务类型、业务 AAD、业务 API import；需要差异时使用业务封装或框架端口。
- 不把业务菜单键登记进基座 `MenuLocalizationKeys` 或 `AdminResource`。
- 不在业务仓修改 WPF 客户端的 `MainWindow`、`Business/IBusinessClientModule.cs` 契约、`Sim.Admin.Wpf.Client.csproj` 或 `Services/`。
- 不为业务客户端创建独立 csproj；业务源码统一由 `BusinessSources.props` 纳入基座工程编译。
- 不使用 `git merge -X ours/theirs` 整体覆盖冲突。
- 不手工合并依赖锁文件。
- 不删除或重命名 `upstream` 远端。
- 不在业务仓重排 `Sim.slnx` 已有条目或改写平台 Swagger 分组。

# OpenPlatform 模块

开放平台模块：让第三方系统以「应用 + AppId/AppSecret」的方式调用本系统 API（机器对机器）。它把本系统已有的 REST API 包装成可管理、可授权、可审计的对外接入能力。

## 模块用途

- 管理第三方接入应用：创建、查看、更新、启用/禁用、删除、重置密钥。
- 为每个应用配置授权范围（权限码 Scope）：管理端从系统权限目录中选择，平台与业务模块权限由各自种子汇总，签发的 Access Token 只携带被授权的 `Permission` Claim；服务端保存时再次校验目录与保留权限。
- 第三方使用 `AppId` + `AppSecret` 换取 Access Token（client_credentials 模式）。
- 为每个应用配置来源 IP 白名单；空白名单放行，配置后仅在命中白名单时允许签发。
- 记录每次令牌签发结果（成功/失败、原因、客户端 IP），并提供逐请求调用审计（方法、路径、状态码、客户端 IP、耗时），便于追踪第三方接入。
- 管理端可按应用、签发结果、客户端 IP 与时间范围分页查询令牌签发审计。

## 分层与入口文件

```text
api/modules/openplatform/
├── Sim.OpenPlatform.Domain.Shared/         # 错误码、权限码常量、授权范围策略
├── Sim.OpenPlatform.Domain/                # 实体、AppId/AppSecret 生成器
├── Sim.OpenPlatform.Application.Contracts/ # DTO、应用服务/令牌服务/签发端口/权限目录端口
├── Sim.OpenPlatform.Application/           # 应用管理、令牌端点、Secret 加密
└── Sim.OpenPlatform.SqlSugar/              # 仓储
```

- 应用管理服务：`Sim.OpenPlatform.Application/Services/OpenPlatformApplicationAppService.cs`
- 访问日志查询：`Sim.OpenPlatform.Application/Services/OpenPlatformAccessLogAppService.cs`
- 授权范围清单：`Sim.OpenPlatform.Application/Services/OpenPlatformScopeAppService.cs`
- 令牌端点：`Sim.OpenPlatform.Application/Services/TokenAppService.cs`
- 逐请求审计：`Sim.OpenPlatform.Application/Auditing/OpenPlatformCallAuditMiddleware.cs`
- AppSecret 加密：`Sim.OpenPlatform.Application/Security/OpenPlatformSecretProtector.cs`
- 令牌签发器（组合根）：`api/src/Sim.Admin.Domain/Services/OpenPlatformTokenIssuer.cs`
- 权限目录实现（组合根）：`api/src/Sim.Admin.Domain/Services/OpenPlatformScopeCatalog.cs`

## 注册位置

- Host 模块依赖：`api/src/Sim.Admin.HttpApi.Host/AdminHttpApiHostModule.cs` 的 `[DependsOn]`
- Host 项目引用：`api/src/Sim.Admin.HttpApi.Host/Sim.Admin.HttpApi.Host.csproj`
- Swagger 分组：`api/src/Sim.Admin.HttpApi.Host/appsettings.json` 的 `Swagger:Documents`
- 解决方案：`api/Sim.slnx`
- 数据种子：`Sim.OpenPlatform.Application/Seeds/OpenPlatformModuleSeedContributor.cs` 自描述模块、权限与菜单，由 `Sim.Admin.Domain/Data/` 的种子执行器汇总
- 菜单文案：`Sim.Admin.Domain.Shared/Localization/MenuLocalizationKeys.cs` 与 `Localization/Layered/{zh-Hans,en}.json`

## 种子数据

站点完成首次初始化后执行 `--seed`（Development 自动种子已关闭，初始化前会跳过）会幂等创建开放平台的模块、权限与菜单，不创建任何第三方应用等业务数据：

| 内容 | 位置 |
| --- | --- |
| 模块记录 `openplatform` | `Sim.OpenPlatform.Application/Seeds/OpenPlatformModuleSeedContributor.cs` |
| 权限码 `openplatform:manage`、`openplatform:access-log:view` | 同上，由平台权限种子汇总 |
| 菜单：`/admin/openplatform` 主菜单，「应用管理」`applications`、「访问日志」`access-logs` 子菜单 | 同上，由平台路由种子汇总 |
| 菜单文案 `Menu:OpenPlatform`、`Menu:OpenPlatformApplications`、`Menu:OpenPlatformAccessLogs` | `Sim.Admin.Domain.Shared/Localization/` |

`AdminRoleAssignmentDataSeeder` 会把全部模块分配给 Admin 角色，因此种子执行后开放平台菜单默认对 Admin 可见。菜单组件路径相对 Web `src/modules` 保存（`openplatform/views/ApplicationsView`、`openplatform/views/AccessLogsView`），对应页面已实现于 `web/src/modules/openplatform`，种子无需再调整。

## 主要接口

管理端（需 `openplatform:manage` 权限）：

- `GET    /api/openplatform/open-platform-application`
- `GET    /api/openplatform/open-platform-application/{id}`
- `POST   /api/openplatform/open-platform-application`
- `PUT    /api/openplatform/open-platform-application/{id}`
- `POST   /api/openplatform/open-platform-application/{id}/enable`
- `POST   /api/openplatform/open-platform-application/{id}/disable`
- `POST   /api/openplatform/open-platform-application/{id}/reset-secret`
- `DELETE /api/openplatform/open-platform-application/{id}`
- `GET    /api/openplatform/open-platform-scope` — 可授予的权限清单（供管理端选择）

授权范围来自系统权限目录：超级权限 `*:*:*` 与开放平台自身权限（`openplatform:*`）不可授予，判定逻辑在 `Sim.OpenPlatform.Domain.Shared/OpenPlatformScopePolicy.cs`；管理端按下拉分组展示。

访问日志（需 `openplatform:access-log:view` 权限，只读）：

- `GET    /api/openplatform/open-platform-access-log`
  - 查询参数：`appId`、`succeeded`、`clientIp`、`startTime`、`endTime`、`skipCount`、`maxResultCount`
  - 响应：`PagedResultDto<OpenPlatformAccessLogDto>`，按签发时间倒序

令牌端点（匿名，client_credentials）：

- `POST /api/openplatform/token`
  - 请求：`{ "appId": "op_...", "appSecret": "ops_...", "grantType": "client_credentials" }`
  - 响应：`{ "accessToken": "...", "tokenType": "Bearer", "expiresIn": 600 }`

## 第三方接入（AppSecret 使用方式）

1. **取凭证**：管理员在「开放平台 → 应用管理」创建应用后，页面一次性展示 `appId`（`op_` + 24 位十六进制）与 `appSecret`（`ops_` + 64 位十六进制）；AppSecret 只在创建与「重置密钥」时明文返回，之后任何接口都不回显。
2. **换 Access Token**（`POST /api/openplatform/token`，匿名接口，应由第三方服务端调用，不要放进浏览器或客户端 App）：

   ```bash
   curl -X POST https://<host>/api/openplatform/token \
     -H "Content-Type: application/json" \
     -d '{"appId":"op_xxxxxxxxxxxxxxxxxxxxxxxx","appSecret":"ops_xxxxxxxx...","grantType":"client_credentials"}'
   ```

   成功返回 `{ "accessToken": "...", "tokenType": "Bearer", "expiresIn": 600 }`；`expiresIn` 取 `JwtSettings:AccessTokenExpirationMinutes`（默认 10 分钟）。Token Claims：`client_id`、`application_id`、`grant_type`，以及每个 Scope 一个 `Permission`。
3. **调用业务接口**：把 Token 放进 `Authorization: Bearer` 头，走与后台用户相同的权限校验链路：

   ```bash
   curl https://<host>/api/blog/article -H "Authorization: Bearer <accessToken>"
   ```

   只放行被授予 Scope 覆盖的接口，未授权返回 403；开放平台自身权限（`openplatform:*`）不可授予，第三方无法用换来的 Token 反向管理接入应用。由于开放平台令牌没有用户会话，只能访问带显式 `[Permission]` 声明的端点（类级或方法级均可）；仅要求登录的用户端接口（如 `user/my-profile`）一律返回 403。
4. **续期**：client_credentials 不返回 refresh_token，Token 过期后重新用 AppId/AppSecret 换一次即可；建议按 `expiresIn` 在本地缓存并提前刷新，不要每个请求都换。
5. **密钥保管与轮换**：
   - 服务端用 Data Protection 加密保存（`Sim.OpenPlatform.Application/Security/OpenPlatformSecretProtector.cs`，密钥环由 `DataProtection:KeysPath` 指向），比较使用 `CryptographicOperations.FixedTimeEquals`。
   - 第三方应把 AppSecret 放进密钥管理或环境变量，不要写入仓库、镜像、前端代码或日志；怀疑泄露时在管理端「重置密钥」，旧密钥立即失效。
   - 应用不再使用时应「禁用」或删除；禁用后换 Token 返回 `Sim:OpenPlatform:Disabled`。
6. **排查**：每次换取 Token（成功/失败）都会写入「开放平台 → 访问日志」，失败原因含 `UnsupportedGrantType`、`UnknownAppId`、`SecretMismatch`（未知 AppId 与密钥错误统一返回 `Sim:OpenPlatform:InvalidCredentials`，不区分）、`Disabled`、`IpNotAllowed`；`IpNotAllowed` 表示调用方 IP 未命中来源白名单（空白名单放行，经反向代理时需先配置可信代理，见 `deploy/README.md`）。

## Web 管理端

模块目录：`web/src/modules/openplatform`（`api/index.ts`、`utils/index.ts`、`views/ApplicationsView.vue`、`views/AccessLogsView.vue`），并在 `web/src/framework/platformModules.ts` 注册；菜单位置仍由后端种子下发。

- 应用管理：创建（一次性返回 AppSecret，需立即保存）、编辑名称/描述/授权范围/来源 IP 白名单、启用、禁用、重置密钥（旧密钥立即失效）与删除；授权范围通过权限清单下拉多选，不能手写权限码。
- 访问日志：按 AppId、签发结果、客户端 IP 与起止时间过滤，服务端分页；桌面端表格、窄屏卡片。
- 文案位于 `web/src/i18n/messages/zh-CN.openplatform.ts` 与 `en-US.openplatform.ts`。

## 验证方式

```bash
cd api
dotnet build Sim.slnx
```

数据库结构由项目的受控 SqlSugar 迁移流程维护（实体 `openplatform_application`、`openplatform_access_log`、`openplatform_api_call_log`）；非开发环境先备份并执行 `--migrate`，权限与菜单由 `--seed` 幂等创建。Web 端可用 `cd web && pnpm exec vitest run src/modules/openplatform` 验证授权范围下拉选项构造。

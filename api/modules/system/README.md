# Sim.System

System 模块拥有文件、调度和通知等系统级业务实现。通知平台优先复用现有五层；当前依赖边界清晰，无需额外 Messaging/Provider 项目。后续若厂商 SDK 需要独立部署裁剪或发布，再拆为职责单一的适配项目：

- `Sim.System.Application.Contracts/Notifications`：普通事件、安全通知、管理员配置和当前用户站内信契约。
- `Sim.System.Domain.Shared/Notifications`：渠道/Provider 常量、统一发送结果与 `INotificationChannelProvider` SPI。
- `Sim.System.Domain/Notifications`：供应商、模板、规则、多 Site Webhook、Delivery、站内信和浏览器订阅实体。
- `Sim.System.Application/Notifications`：事件规划、Scriban 渲染、重试状态机、Data Protection 配置保护和全部渠道实现。

SMTP 使用 MailKit；腾讯云/阿里云短信及语音使用官方 SDK；标准 HTTP 短信/语音、Generic Webhook 和协作机器人使用受控 HTTPS 适配器。Provider 初始化会为旧空记录补齐非敏感设置模板，Secret 仍只通过后台加密保存且不回显；更新时按键合并非空 Secret，显式 `ClearSecrets` 才清空全部凭据。Web 管理端按 Provider Schema 展示结构化分组字段，后端继续只接收稳定的 Settings/Secrets 字典契约。
- `Sim.System.SqlSugar`：继续作为 System 数据基础设施模块；通用仓储由现有 `Sim.SqlSugar.Core` 提供。

管理员 API 位于 `/api/system/notification-administration`，当前用户站内信 API 位于 `/api/system/user-notification`。Host 通过 `/hubs/user-notifications` 暴露鉴权 SignalR Hub。权限是 `system:notification:manage` 和 `system:notification:view-deliveries`。读取配置只返回 `HasSecret`，空 Secret 更新表示保留已有值；机器人 Endpoint 可以进入加密 Secrets，但保存前仍执行公开 HTTPS 校验。

普通业务通过 `INotificationEventPublisher` 发布稳定事件名；安全验证码通过 `ISecurityNotificationSender` 即时发送，禁止写入 Outbox、Delivery 正文或日志。一个事件匹配多个 Webhook Site 时会生成多条独立 Delivery。

普通通知的 `NotificationRecipient` 支持显式 `Address` 和系统账号 `UserId`。`INotificationAccountDirectory` 位于 System Contracts，RBAC Application 提供实现；邮件解析邮箱、短信/语音解析手机号、站内/浏览器使用用户 ID，显式地址优先。管理员账号搜索只返回启用账号与脱敏目标，Provider 测试可选择账号或直接地址。

站内/浏览器通知坚持“先落库、后失效通知”：System Application 的 `UserNotificationRealtimeNotifier` 只依赖 Contracts 中的 `IUserNotificationRealtimePublisher`，Host 提供 SignalR 适配器并按当前用户定向发送 `notificationChanged`，不在实时帧中传输标题或正文。实时链路故障不影响持久化和已读状态；Web 端通过 API 重新读取权威数据。

Production/Staging 升级前先备份，再用同一发布产物执行 `--migrate`。Data Protection Keys 必须持久化；MQ/Redis 是 Framework 部署配置，通知 Provider 账号由后台维护。外部平台上线前还必须完成资质、模板、回执、出网策略、频控、费用和告警联调。
## 系统配置与数据字典

System 提供 `SystemConfigurationItemEntity`（表 `system_configuration_item`）和 `/api/system/configuration-item` 管理接口。记录按 `TenantScope + GroupKey + ConfigKey` 唯一；`GroupKey` 与 `ConfigKey` 创建后不可变，`system:configuration:manage` 权限保护所有管理操作。`SystemConfigurationDataSeeder` 幂等提供站点、安全、文件与界面默认参数，不覆盖已有值。

自助注册成功后分配的角色由 `security.registration-role-codes`（安全策略组，默认 `User`）维护，值为英文逗号分隔的角色代码；只分配非内置角色，内置角色被忽略，配置缺失或为空时回退 `User`。`POST /api/system/user/register` 与外部登录注册都不接受客户端角色参数。内置 `User` 角色由 `UserRoleAssignmentDataSeeder` 幂等补齐 `approval` 模块访问，管理员可在角色管理中继续调整。

用户默认密码由 `security.user-default-password`（安全策略组）维护，首次种子生成随机初始值并允许管理员修改。管理端 `POST /api/system/user` 忽略调用方密码（`CreateUserDto.password` 仅用于自助注册），与 `POST /api/system/user/{id}/reset-user-password` 一样读取该配置；重置要求 `system:user:reset-password`、会吊销目标用户全部 Refresh Token，内置账号不可重置。配置缺失时回退 `App:AdminDefaultPassword`，默认密码不符合密码策略时拒绝创建/重置。用户创建与编辑的邮箱为可选项：空白邮箱在 DTO 赋值时归一化为 `null`，非空仍按 `[EmailAddress]` 校验格式。用户创建（管理端新建、自助注册、外部登录注册与初始化管理员）完成后还会调用 `IUserCreatedHandler` 扩展端口，由业务模块按需初始化自身关联数据，System 不直接依赖业务实现。

业务数据字典提供 `DataDictionaryEntity` / `DataDictionaryItemEntity`（表 `data_dictionary` / `data_dictionary_item`）和 `/api/system/data-dictionary`、`/api/system/data-dictionary-item` 管理接口。字典保存 `String`、`Number`、`Date` 统一值类型（创建后不可变）和可选输入前缀/后缀；管理端通过独立字典数据 Tab 维护字典项，字典项按租户、字典和项键唯一。`DataDictionaryValueValidator` 对字符串、Invariant `G29` 数字和 `yyyy-MM-dd` 日期做校验与规范化，`system:data-dictionary:manage` 权限保护管理操作。

## 用户偏好

登录用户的前端偏好保存在 `system_user_preference`（实体 `UserPreferenceEntity`，`UserId` 唯一索引）：`Language`、`ThemeMode`、`ThemeStyle`。`GET /api/system/user-preference/my-preferences` 未保存过时返回 204，前端保留本机选择；`PUT` 使用同一路径覆盖保存，空字段保持原值。语言只接受 `zh-CN` / `en-US`，明暗模式只接受 `light` / `dark` / `system`；主题风格由前端注册表演进，后端只做长度与 `[A-Za-z0-9_-]` 字符集校验，新增风格无需发布后端。接口只允许当前登录用户读写自己的记录；删除用户时同步清理对应偏好。Web 端在路由守卫会话恢复后加载一次，切换语言、明暗或主题色后 400ms 防抖写回。

## E2EE 身份

`system_user_e2ee_identity` 保存用户端到端加密身份的 KDF 参数（v1 固定 `PBKDF2-SM3`、600,000 次）与两个 `wrap:v1` 包裹结果：`PasswordWrappedKey`（加密口令 KEK 包裹的 UMK）和 `RecoveryWrappedKey`（恢复密钥包裹的 UMK）。唯一索引为 `(TenantScope, UserId, KeyVersion)`；`Revision` 用于多设备乐观并发。接口 `GET/POST /api/system/e2ee/identity`、`PUT /api/system/e2ee/passphrase`、`PUT /api/system/e2ee/recovery-key` 只允许当前登录用户访问并限流；服务端只校验信封形状与长度，不接收口令、UMK、恢复密钥，也不提供解密或口令校验路径。错误码：`UserE2eeIdentityNotFound/Exists/Invalid/Conflict`。

## 目录组织

System 功能体量大，`Application` 与 `Domain` 按功能使用顶层目录，`SqlSugar` 在 `Repositories/` 下按功能分组；命名空间跟随文件夹，引用时显式 `using` 对应子命名空间：

- `Sim.System.Application/<Feature>/`（`Sim.System.Application.<Feature>`）：应用服务与横切实现，如 `User`、`Role`、`Permission`、`Organization`、`Tenant`、`RouteConfig`、`ModuleConfig`、`Dictionary`、`Numbering`、`SystemConfig`、`ScheduleTask`、`Notifications`、`FileManagement`、`FileStorage`、`E2ee`、`Worker`。
- `Sim.System.Domain/<Feature>/`（`Sim.System.Domain.<Feature>`）：实体、仓储接口与领域管理器按功能同目录，如 `UserEntity`、`IUserRepository`、`TenantManager` 均位于对应功能目录。
- `Sim.System.SqlSugar/Repositories/<Feature>/`（`Sim.System.SqlSugar.Repositories.<Feature>`）：仓储实现按功能分组，`SystemRepositoryBase` 保留在 `Repositories/` 根目录。
- `Sim.System.Application.Contracts/<Feature>/`：契约目录与上述功能同名，新增功能时保持五层功能目录一一对应。

新增文件按功能放入同名目录，不要回落到项目根或平铺目录；类型所在命名空间必须与目录一致。

## Domain.Shared 目录约定

`Sim.System.Domain.Shared` 按类型分目录，命名空间跟随文件夹，引用时需要显式 `using` 对应子命名空间：

- `Consts/`（`Sim.System.Domain.Shared.Consts`）：普通常量，如 `SystemConsts`、`FileStorageProviderNames`、`LoginDeviceTypes`、`RoleConst`、`TokenTypeConst`。
- `Enums/`（`Sim.System.Domain.Shared.Enums`）：枚举，如 `DataDictionaryValueType`、`DataScopeType`、`PermissionTypeEnum`、`SexEnum` 和文件存储相关枚举（`ChunkUploadStatus`、`ResourceVisibility`、`DownloadStatus`、`ConversionTaskType` 等）。
- `ErrorCodes/`（`Sim.System.Domain.Shared.ErrorCodes`）：稳定错误码统一由 `SystemErrorCodes` 单点声明。
- `Permissions/`（`Sim.System.Domain.Shared.Permissions`）：权限码常量，如 `SystemPermissions`、`SystemManagementPermissions`、`FileManagementPermissions` 与 `OrganizationPermissions`；`RoutePermissionPolicy` 负责路由权限码的任一/全部匹配。
- `Exceptions/`、`ExternalLogin/`、`Notifications/`：保持按特性组织；`IOrganizationOwned` 等跨特性共享接口留在项目根命名空间 `Sim.System.Domain.Shared`。

新增常量、枚举、错误码和权限码时按上述目录归类，不要散放在项目根目录；混装的旧文件应拆分为单类型文件后再归位。

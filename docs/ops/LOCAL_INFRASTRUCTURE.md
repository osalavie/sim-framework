# 本地开发基础设施

`api/env/compose.yaml` 仅启动 PostgreSQL 17、Redis 7.4 和 RabbitMQ 4（含管理界面）；API 与 Web 仍在宿主机运行。需要 Docker Engine / Docker Desktop（Linux containers）和 Docker Compose v2.20+。

## 启动

在仓库根目录执行：

```powershell
docker compose -f api/env/compose.yaml up -d --wait
docker compose -f api/env/compose.yaml ps
```

服务全部通过健康检查后命令返回。首次运行会下载镜像；启动失败可执行 `docker compose -f api/env/compose.yaml logs --tail 100` 查看原因。

| 服务 | 宿主机地址 | 默认开发配置 |
| --- | --- | --- |
| PostgreSQL | `127.0.0.1:5432` | 数据库 `sim_dev`，用户 `sim`，密码 `sim_dev_password` |
| Redis | `127.0.0.1:6379` | 数据库 0，无密码，AOF 持久化 |
| RabbitMQ | `127.0.0.1:5672` | 用户 `sim`，密码 `sim_dev_password`，vhost `/sim_dev` |
| RabbitMQ 管理界面 | <http://127.0.0.1:15672> | 同 RabbitMQ 账号 |

这些公开凭据仅供本机开发，所有发布端口仅绑定 `127.0.0.1`。不要将此配置作为生产部署。镜像使用指定大版本系列的官方标签；参考 [PostgreSQL](https://hub.docker.com/_/postgres)、[Redis](https://hub.docker.com/_/redis) 和 [RabbitMQ](https://hub.docker.com/_/rabbitmq) 镜像说明。

PostgreSQL 通过启动参数 `max_connections=200` 预留连接余量，应用连接串用 `Maximum Pool Size=60` 限制单进程连接数（见 `appsettings.Example.json`）。已有 `postgres-data` 数据卷的容器不会自动应用新的启动参数，需要重新创建容器，或在容器内执行 `ALTER SYSTEM SET max_connections = 200;` 后重启 PostgreSQL；连接外部 PostgreSQL 时也应按“应用实例数 × 单实例池上限 + 运维余量”调大服务端上限。

## 连接本地后端

后端 `appsettings.Example.json` 已包含上表对应的 PostgreSQL、Redis、RabbitMQ 连接及 PostgreSQL Quartz 配置，Code First 开启、自动数据种子关闭。缺少环境文件时启动会从模板生成；已有 `appsettings.Development.json` 不会自动更新，应对照模板合并 `Framework`、`DataBase`、`Quartz` 对应项并保留自己的其他设置。本次已同步当前工作区的 Development 文件，该文件仍不入库。

使用默认 Compose 时，在仓库根目录的 PowerShell 会话中提供应用密钥即可启动：

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"

# 每个本地会话生成开发签名密钥；需要跨重启保留登录时使用本机密钥管理。
$env:JwtSettings__IssuerSigningKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))

dotnet run --project api/src/Sim.Admin.HttpApi.Host
```

以上示例使用 PowerShell 7。数据库容器仅创建空库；后端 Code First 创建业务表。首次启动 Web 后访问 `/setup`，设置管理员账号与密码、站点名称、默认语言与主题、开放注册、登录失败上限和锁定时长；提交后页面展示初始化进度，系统先创建管理员，再创建不可登录的系统账号 `system`，并由系统账号作为创建人执行种子并写入初始化标记，完成后 `/setup` 关闭并跳转登录页。未完成初始化前启动自动种子与 `--seed` 都会跳过；初始化完成后如需补齐升级新增的基础数据，可再执行 `dotnet run --project api/src/Sim.Admin.HttpApi.Host -- --seed`。Quartz 使用同一 `Default` 连接创建调度表。其他后端启动要求见 [开发指南](../core/DEVELOPMENT.md)。若本机设置过 `SIM_SECRETS_DIRECTORY`，其中的同名密钥配置会覆盖环境变量，需使用对应开发密钥或在当前会话移除该变量（Windows 下进程级未设置时，API 会依次回退读取用户级、系统级环境变量；修改后需重启 API）。环境变量也会覆盖 JSON 配置；如果当前会话保留旧连接变量，请先清理或更新。

API 从宿主机连接 `127.0.0.1`；Compose 内的服务名不是宿主机连接地址。RabbitMQ vhost 的 `/` 在 URI 中编码为 `%2F`；自定义用户名或密码中的 URI 保留字符也必须百分号编码。Web 仍按 Web README 在本机启动。

## 自定义端口与密码

可将 `api/env/.env.example` 复制为同目录 `.env` 后修改（`api/env/.env` 已被 Git 忽略），或设置对应的 `SIM_DEV_*` 环境变量。可覆盖 PostgreSQL、Redis、RabbitMQ AMQP/管理界面的宿主机端口及 PostgreSQL/RabbitMQ 密码；变量列表见 `api/env/.env.example`。修改后重新执行 `docker compose -f api/env/compose.yaml up -d --wait`，并同步修改 `appsettings.Development.json` 中的 `DataBase:ConnectionConfigs`、`Framework:Caching:RedisConnectionString`、`Framework:Messaging:ConnectionString`。也可使用对应的 .NET 环境变量 `DataBase__ConnectionConfigs__0__ConnectionString`、`Framework__Caching__RedisConnectionString`、`Framework__Messaging__ConnectionString` 覆盖。

`api/env/.env` 位于 Compose 文件同目录，仅供 Compose 插值，不会自动传入本机运行的 .NET 进程。PostgreSQL/RabbitMQ 初始化账号配置仅在数据卷为空时生效；已有数据需使用服务自身命令修改密码，或在确认无需保留数据后重建卷。命名基线切换后，旧数据卷中的库名、账号与 vhost 与新默认值不一致；确认无需保留时执行 `docker compose -f api/env/compose.yaml down -v` 重建，或手工修改旧库后再复用卷。

## 停止与数据保留

```powershell
# 停止并移除容器，保留数据库、缓存和消息数据卷
docker compose -f api/env/compose.yaml down

# 查看日志
docker compose -f api/env/compose.yaml logs -f postgres redis rabbitmq

# 清空整个开发环境：永久删除这三个服务的数据卷
docker compose -f api/env/compose.yaml down -v
```

Compose 项目名为 `sim-dev`，三个命名卷随容器重建保留数据；RabbitMQ 固定 hostname，保持节点数据目录稳定。上述仓库根目录 Compose 命令与 `deploy/` 的 API/nginx 部署入口独立。共享基础设施仍按部署文档管理，本地 Compose 是独立开发选项，不代表已验证生产高可用或备份恢复。

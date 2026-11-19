# SimFramework 部署指南

本机开发依赖请使用 `api/env/compose.yaml`（PostgreSQL、Redis、RabbitMQ），操作见 [本地开发基础设施](../docs/ops/LOCAL_INFRASTRUCTURE.md)。本目录继续用于 API/nginx 部署；下述共享 Host 约定适用于共享环境，本地独立开发可使用回环地址。

## 架构

```
用户 → nginx (443/80) → API (127.0.0.1:7777)
                     → 静态文件（Web 镜像内置）
```

镜像由 Jenkins 构建并推送到 `repo.dulm.net`，目标机只负责拉取和启动：

```
git push develop ──→ Jenkins CI ──→ sim-api / sim-web 镜像 ──→ 自动部署测试环境
git push v1.0.0  ──→ Jenkins CI ──→ 同标签镜像 ──→ sim-release 人工审批 ──→ 生产环境
```

## 环境一致性（以开发环境为准）

开发、测试、生产三套环境共用同一基础设施 Host（10.0.0.2），连接串以开发环境为基准，环境间通过独立资源隔离：

- 数据库：同一 PostgreSQL Host（10.0.0.2:5432），按环境使用独立库（sim_dev / sim_test / sim_prod）
- 缓存：同一 Redis Host，按环境使用独立端口（6379 / 16379 / 17379）
- 消息队列：同一 RabbitMQ Host，按环境使用独立 vhost（/sim_dev / /sim_test / /sim_prod）

除上述环境隔离外，环境配置不得引入不同的基础设施地址；密钥（JWT、Data Protection）仍按环境分别注入。

## 前置条件

- Docker & Docker Compose v2.20+（主入口 `docker-compose.yml` 使用 `include`）
- Linux/macOS：OpenSSL（生成 JWT 密钥与 nginx Basic Auth 哈希；Windows 脚本内置 APR1-MD5 实现）
- PostgreSQL 数据库已创建
- RabbitMQ vhost 已创建

## 部署步骤

### 1. 构建镜像

正常发布由 Jenkins 完成。手工部署时，在仓库根目录构建 API 和 Web 镜像：

```bash
docker build -f api/Dockerfile -t repo.dulm.net/sim-api:<tag> api

cd web
pnpm install
pnpm build:production
docker build -f Dockerfile -t repo.dulm.net/sim-web:<tag> .
```

镜像标签使用 Git 标签（如 `v1.0.0`）或提交短 SHA。手工构建后需要登录镜像仓库并推送，目标机才能拉取；如果只在同一台机器运行，也可以把 `.env` 中的镜像地址改为本地标签。

```bash
docker login repo.dulm.net
docker push repo.dulm.net/sim-api:<tag>
docker push repo.dulm.net/sim-web:<tag>
```

### 2. 准备密钥

Linux/macOS：

```bash
cd deploy
chmod +x setup-secrets.sh
./setup-secrets.sh ./secrets ./nginx/.htpasswd
```

Windows：

```powershell
cd deploy
powershell -ExecutionPolicy Bypass -File .\setup-secrets.ps1 .\secrets .\nginx\.htpasswd
```

如需把访问权限授予指定 Windows 账号，可加 `-Account <DOMAIN\user>`。

两个脚本都会显示选择菜单：

- `1`：JWT 签名密钥，可输入数量（默认 3，可用 `JWT_KEY_COUNT=n` 作为默认值）
- `2`：数据库连接串
- `3`：Redis 连接串
- `4`：RabbitMQ 连接串
- `5`：Swagger Basic Auth（nginx `.htpasswd`）
- `6`：全部密钥
- `0`：结束

凭据不会内置在脚本或文档中，只在当前命令行会话中输入。数据库、Redis、RabbitMQ 等应用 Secret 写入目标密钥目录；Swagger Basic Auth 写入指定的 `.htpasswd`，该文件不入库，并由 Compose 只读挂载到 nginx。

或手动创建密钥目录，每个文件对应一个配置项（文件名用 `__` 分隔层级）：

```
secrets/
├── JwtSettings__SigningKeys__Keys__0__Kid          # 内容: 1
├── JwtSettings__SigningKeys__Keys__0__Secret       # 内容: <64字节hex>
├── JwtSettings__SigningKeys__Keys__0__IsActive     # 内容: true（active）
├── JwtSettings__SigningKeys__Keys__1__Kid          # 内容: 2
├── JwtSettings__SigningKeys__Keys__1__Secret       # 内容: <64字节hex>
├── JwtSettings__SigningKeys__Keys__1__IsActive     # 内容: false（保留校验）
├── ...                                             # 默认共 3 把（JWT_KEY_COUNT）
├── DataBase__ConnectionConfigs__0__ConnectionString # 内容: PG连接串
├── Framework__Caching__RedisConnectionString       # 内容: Redis连接串
├── Framework__Messaging__ConnectionString          # 内容: RabbitMQ amqp URI
└── FileManagement__ObjectStorageWebhookSecret       # 内容: 对象存储 Webhook HMAC 签名密钥
```

> 说明：`docker-compose.yml` 已将 `./secrets` 只读挂载到 API 容器的 `/run/secrets`（即 `SIM_SECRETS_DIRECTORY`），脚本生成的目录会被 API 启动时直接加载。

对象存储回调 Webhook 需要配置 `FileManagement__ObjectStorageWebhookSecret`，调用方对原始 JSON 文本计算 HMAC-SHA256 并放入 `X-Sim-Signature: sha256=<hex>`；未配置密钥时该端点失败关闭，不接收匿名写入。

> 系统 E2EE 身份与通行密钥的口令、UMK、恢复密钥只属于用户：服务端只保存 KDF 参数和 `wrap:v1` 包裹密钥，不提供管理员恢复或口令重置。数据库备份、服务器备份目录、对象存储和 Git 中都不得出现用户恢复密钥文件。业务模块接入端到端加密时须自行定义密文范围与验收，不得把服务端静态加密宣传为 E2EE，也不能让旧客户端把密文当明文展示或覆盖保存。

API 镜像固定以数字 UID/GID `1000:1000` 非 root 运行；构建时只在基础镜像尚未提供 UID/GID `1000` 时创建对应条目。普通 Compose 的 bind mount 不会重映射 UID，因此宿主机上的 `secrets`、`api-logs`、`api-data` 目录必须允许容器运行用户访问：

```bash
cd deploy
mkdir -p api-logs api-data
chown -R 1000:1000 secrets api-logs api-data
chmod 700 secrets
chmod 600 secrets/*
chmod 755 api-logs api-data
```

如果宿主机已由 UID `1000` 用户生成 `secrets`，可省略 `chown`；否则用 root 执行，或通过 `SECRETS_UID`/`SECRETS_GID` 让 `setup-secrets.sh` 在 root 运行时自动设置 owner。

### JWT 签名密钥轮换

`JwtSettings:SigningKeys` 支持多密钥按 `kid` 轮换：签发新 token 使用 `IsActive=true` 的密钥，校验时按 token 头里的 `kid` 匹配，已配置的旧密钥仍可校验旧 token。`setup-secrets.sh` 默认生成 3 把密钥（运行时可输入数量，`JWT_KEY_COUNT=n` 可作为默认值），`Keys__0__` 为 active、其余保留校验；轮换时手动添加新密钥并切换 active 标记：

> 国密 SM2 签名：在密钥描述符中加入 `Algorithm=SM2WITHSM3`、`PublicKey`（Base64 未压缩公钥）与 `PrivateKey`（hex 私钥）字段即切换为该密钥使用 SM2 签名；私钥只在签发方持有，轮换/校验步骤与 HMAC 一致。SM2 密钥对可在后端通过 `IGmCryptoService.GenerateSm2KeyPair()` 生成（未压缩公钥 Base64 + 私钥 hex），写入 secrets 目录对应文件；也可用 openssl SM2 生成后转换为相同格式。

1. 生成新密钥：`secrets/JwtSettings__SigningKeys__Keys__3__Kid`（如 `4`）、`secrets/JwtSettings__SigningKeys__Keys__3__Secret`（新 64 字节 hex）、`secrets/JwtSettings__SigningKeys__Keys__3__IsActive`（`true`）；
2. 将当前 active 密钥（如 `Keys__0__`）的 `IsActive` 改为 `false`（保留文件，旧 token 仍可校验）；
3. `docker compose -f docker-compose.api.yml restart api`；
4. 待旧 token 全部过期（Access/Refresh 有效期）后，删除不再需要的旧密钥文件并再次重启。

注意：同一时间只能有一把 `IsActive=true`，否则 API 启动会直接失败。

### 3. 配置 TLS 证书

将证书放在 `deploy/nginx-ssl/` 目录（compose 挂载 `./nginx-ssl`，默认使用仓库自带的 `localhost.crt`/`localhost.key`）：

```bash
mkdir -p nginx-ssl
cp /path/to/cert.pem nginx-ssl/cert.pem
cp /path/to/key.pem  nginx-ssl/key.pem
```

### 4. 配置 CORS（如前后端不同源）

编辑 compose 只读挂载的生产配置 `deploy/config/appsettings.Production.json`（该文件不入库，从 `api/src/Sim.Admin.HttpApi.Host/appsettings.Example.json` 模板复制后按部署环境补齐）：

```json
{
  "App": {
    "CorsOrigins": "https://your-frontend-domain.com"
  }
}
```

如使用 nginx 同域代理（推荐），CORS 可留空。

生产配置同样承载环境差异项，建议显式声明日志级别，例如把框架噪声压到 Warning：

```json
{
  "Logging": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Information",
        "Volo.Abp": "Warning",
        "Quartz": "Warning"
      }
    }
  }
}
```

### 5. 配置镜像并启动服务

在目标机准备 `.env`（该文件不入库）：

```bash
cd deploy
cp .env.example .env
vi .env
# SIM_API_IMAGE=repo.dulm.net/sim-api:<tag>
# SIM_WEB_IMAGE=repo.dulm.net/sim-web:<tag>
docker login repo.dulm.net
```

compose 文件已按前后端拆分，可按实际环境选择：

| 场景 | 命令 |
| --- | --- |
| 完整栈（后端 API + 前端 nginx） | `cd deploy && docker compose pull && docker compose up -d` |
| 仅后端（复用已有 nginx） | `cd deploy && docker compose -f docker-compose.api.yml pull api && docker compose -f docker-compose.api.yml up -d api` |
| 显式指定前后端文件 | `cd deploy && docker compose -f docker-compose.api.yml -f docker-compose.web.yml pull && docker compose -f docker-compose.api.yml -f docker-compose.web.yml up -d` |

- `docker-compose.yml`：主入口，`include` 前后端两个文件（需 Docker Compose v2.20+）
- `docker-compose.api.yml`：后端 API；生产复用已有 nginx 时只需此文件
- `docker-compose.web.yml`：前端 nginx（80/443 + `/api/`、`/hubs/`、`/health/`、`/swagger/` 反代；Swagger 文档面启用 Basic Auth）；静态文件已经打进 `sim-web` 镜像，不再挂载宿主机 `web/dist`
- `.env`：保存当前环境要运行的 `SIM_API_IMAGE` / `SIM_WEB_IMAGE`，部署脚本按标签覆盖并支持回滚

**生产复用已有 nginx 时**只启动 API，并把现有 nginx 反代到 `http://127.0.0.1:7777`。API 端口映射为 `127.0.0.1:7777:7777`，仅绑定宿主机回环地址、不对外暴露；静态文件、TLS 与 API 反代全部交给现有 nginx。如需暴露 Swagger，请参考 `deploy/nginx/simframework.conf`，在现有 nginx 中配置 `/swagger/` 与 `/api/swagger-config` 反向代理，并挂载同等权限控制的 `.htpasswd`。

### 6. 验证

```bash
# 健康检查
curl -k https://localhost/health/live
curl -k https://localhost/health/ready

# Swagger Basic Auth：未带凭据应返回 401
curl -i https://localhost/swagger/index.html
curl -i https://localhost/api/swagger-config

# Swagger Basic Auth：带凭据应返回 200
curl -i -u <user> https://localhost/swagger/index.html
curl -i -u <user> https://localhost/api/swagger-config

# 检查服务状态（项目名相同，无需重复 -f；如不确定可加与启动一致的 -f）
docker compose ps
docker compose logs -f api
```

## CI/CD 自动发布（Jenkins）

### 分支与标签策略

| 触发 | 行为 |
| --- | --- |
| 推送到 `develop` | 三个子项目执行 CI；API/Web 推送镜像并自动部署测试环境 |
| 推送到 `main` | 三个子项目执行 CI；API/Web 推送镜像但不部署 |
| 推送 `v*` 标签 | 三个子项目执行 CI；API/Web 使用同一个 Git 标签推送镜像；API 流水线触发 `sim-release`，人工审批后部署生产 |
| Pull Request | 只执行 CI，不推送镜像、不部署 |
| 手工发布 | 在 Jenkins 运行 `sim-release`，填写已存在的标签（如 `v1.0.0`） |

分支构建的镜像标签为 `<构建号>-<提交SHA前12位>`；标签构建直接使用 Git 标签，因此 API 和 Web 在生产使用同一个版本标签。所有 Jenkinsfile 都假定 **workspace 是仓库根目录**（Multibranch 默认行为），不要为作业配置子目录 checkout。仓库当前只有 `main` 时，需要先创建并推送 `develop`，否则测试环境自动部署不会触发。

测试环境需要执行数据库迁移时，手动触发一次带 `RUN_MIGRATION` 的 `sim-api` 构建，或在目标机执行 `bash /opt/sim/deploy.sh test api <tag> repo.dulm.net --migrate`。生产迁移始终走 `sim-release` 的人工审批。

### Jenkins 作业与插件

安装 GitHub Branch Source、GitHub、Docker Pipeline、SSH Agent、Credentials Binding 插件，并创建以下作业：

| 作业 | 类型 | Script Path |
| --- | --- | --- |
| `sim-api` | Multibranch Pipeline | `api/Jenkinsfile` |
| `sim-web` | Multibranch Pipeline | `web/Jenkinsfile` |
| `sim-release` | Pipeline（参数化） | `deploy/Jenkinsfile` |

`sim-api` 和 `sim-web` 的多分支作业需要发现 `develop`、`main` 分支以及 `v*` 标签，并启用定时扫描作为 Webhook 的兜底。`sim-release` 不配置 Webhook，由标签构建触发或人工运行。

Jenkins Agent 需要具备：

- .NET 10 SDK、Docker CLI、curl、ssh/scp（API 作业）
- Playwright 镜像运行能力，以及与 Docker 主机共享的 Docker CLI（Web 作业的镜像阶段必须能执行 `docker build`）
- 目标机的 SSH 私钥和已知主机指纹

在 Jenkins 中创建以下凭据：

| 凭据 ID | 类型 | 用途 |
| --- | --- | --- |
| `github-repository` | SSH Username with private key 或 GitHub PAT | Jenkins 拉取仓库；SSH 方式需配置 GitHub Deploy Key |
| `registry-credentials` | Username with password | 登录 `repo.dulm.net` 并推送镜像 |
| `deploy-test-ssh` | SSH Username with private key | 登录测试目标机 |
| `deploy-prod-ssh` | SSH Username with private key | 登录生产目标机 |
| `deploy-known-hosts` | Secret file | SSH known_hosts 内容，用 `ssh-keyscan -H <host>` 生成 |

API 作业的 `TEST_DEPLOY_HOST`、`PROD_DEPLOY_HOST` 参数是 `user@host` 形式；生产发布作业也提供 `PROD_DEPLOY_HOST`。如果作业参数留空或使用默认值，必须在 Jenkins 中改成实际地址。

### GitHub Webhook

在 GitHub 仓库 `Settings → Webhooks` 添加：

- Payload URL：`https://<jenkins-public-host>/github-webhook/`
- Content type：`application/json`
- Secret：与 Jenkins GitHub Server 配置一致
- Events：`push`、`pull_request`

Webhook 到达后，Multibranch 作业会重新索引分支/标签并触发对应流水线。当前三个子项目都会收到推送事件并各自运行 CI；后续需要缩短反馈时间时，可在 Jenkinsfile 中增加路径过滤或使用独立的路径过滤 Webhook。

### 目标机准备

测试机和生产机都需要一个部署目录，例如 `/opt/sim/deploy`：

```text
/opt/sim/
├── deploy.sh                 # Jenkins 每次覆盖写入
└── deploy/
    ├── docker-compose.yml
    ├── docker-compose.api.yml
    ├── docker-compose.web.yml
    ├── .env                  # SIM_API_IMAGE / SIM_WEB_IMAGE
    ├── config/appsettings.Production.json
    ├── secrets/
    ├── nginx/
    │   └── simframework.conf # 容器 nginx 反代与安全头模板
    ├── nginx-ssl/
    ├── api-logs/
    └── api-data/
```

首次部署时，把仓库 `deploy/` 下的 Compose 文件、`nginx/` 配置和 `.env.example` 复制到目标机 `/opt/sim/deploy`，再补齐 `config/`、`secrets/` 和 `nginx-ssl/`；后续发布只更新 `.env` 和镜像标签。

```bash
cd /opt/sim/deploy
cp .env.example .env
vi .env
docker login repo.dulm.net
mkdir -p api-logs api-data
chown -R 1000:1000 secrets api-logs api-data
chmod 700 secrets
chmod 600 secrets/* .env
chmod 755 api-logs api-data
```

部署账号需要能执行 `docker`、`curl`，并能写 `/opt/sim/deploy.sh` 和 `/opt/sim/deploy/.env`。脚本优先使用 `flock` 串行部署；系统没有 `flock` 时自动退化为目录锁。如果目标机不能直接访问镜像仓库，需要先配置镜像仓库的只读账号和 `docker login`，或者使用内网 Registry 代理。

### 部署脚本

[`deploy.sh`](scripts/deploy.sh) 由 Jenkins 上传到目标机执行：

```bash
bash /opt/sim/deploy.sh <test|prod> <api|web|all> <tag> <registry> [--migrate]
```

执行顺序：

1. 用 `flock` 串行化同一目标机上的并发部署。
2. 备份当前 `.env`，只覆盖本次要更新的服务镜像。
3. `docker compose pull` 指定服务。
4. 可选执行 `docker compose run --rm --no-deps api --migrate`。
5. `docker compose up -d` 并轮询 `/health/ready`（API 走 7777，Web 走 nginx HTTPS）。
6. 健康检查失败时恢复 `.env` 并重新启动旧镜像；数据库迁移不会自动回滚，失败后需要按发布前备份恢复。

### 生产发布与回滚

1. 在通过测试的提交上创建并推送标签：`git tag v1.0.0 && git push origin v1.0.0`。
2. API/Web 流水线构建并推送 `repo.dulm.net/sim-api:v1.0.0` 和 `sim-web:v1.0.0`；API 流水线触发 `sim-release`。
3. `sim-release` 等待两个镜像都可用，然后停在 `Approve Production` 等待人工审批。
4. 审批通过后，以 `prod all` 模式更新 API 和 Web；健康检查失败自动回滚应用镜像。
5. 需要数据库结构变更时，先备份数据库，再在 `sim-release` 参数中勾选 `RUN_MIGRATION`。应用回滚不会撤销数据库迁移；不兼容迁移必须从备份恢复。

如需重新发布某个已存在的标签，直接运行 `sim-release` 并填写 `TAG`；如果标签对应镜像尚未推送，作业会在 20 分钟后失败。

## 环境变量

| 变量                     | 说明                         | 默认值           |
| ------------------------ | ---------------------------- | ---------------- |
| `SIM_API_IMAGE`          | API 镜像地址（含标签）       | `sim-api:latest` |
| `SIM_WEB_IMAGE`          | Web nginx 镜像地址（含标签） | `sim-web:latest` |
| `SIM_API_PORT`           | API 容器监听端口与宿主机回环映射端口 | `7777` |
| `DOTNET_ENVIRONMENT`     | 运行环境                     | `Production`     |
| `HTTP_PORTS`             | API HTTP 监听端口（绑定所有网卡） | `7777` |
| `SIM_SECRETS_DIRECTORY`  | 密钥目录路径（key-per-file）；Windows 下进程级未设置时回退用户/系统级 | -                |
| `ForwardedHeaders__KnownNetworks__0` | 可信反向代理网段（CIDR，如 `172.18.0.0/16`） | - |
| `ForwardedHeaders__KnownProxies__0` | 可信反向代理单 IP（如 `172.18.0.1`） | - |
| `Swagger__AllowInProduction` | Production 是否注册 Swagger；Compose 同域部署显式开启，并依赖 nginx Basic Auth | `false` |

API 容器运行 UID/GID 固定为 `1000:1000`。

## 维护模式运行时开关

API 启动时会可选加载 `/app/App_Data/maintenance.json` 并启用文件监听。修改以下文件即可热启用/关闭维护，无需重启容器：

```json
{
  "Site": {
    "MaintenanceMode": true,
    "MaintenanceMessage": "系统升级中，预计 30 分钟后恢复。"
  }
}
```

启用后业务 API 和鉴权 SignalR Hub 返回 503；`/health/live`、`/health/ready`、`/health-status`、`/api/admin/site-status` 与匿名 `/hubs/site-status`（含协商子路径）保持可用，用于推送维护关闭通知。

## 安全注意事项

1. **JWT 密钥**：生产环境必须通过 `SIM_SECRETS_DIRECTORY` 注入，不要写入 appsettings.json
2. **CORS**：同域部署留空，跨域部署必须指定具体域名
3. **HSTS**：生产由 1Panel OpenResty 在 TLS 终结层下发 `max-age=31536000; includeSubDomains`，容器 nginx 保留同一值；不要再用 `63072000; preload`，避免与边缘配置不一致。
4. **安全头**：生产边缘统一下发 CSP / HSTS / XCTO / XFO / Referrer-Policy / Permissions-Policy（模板见 `nginx/simframework.conf`），容器 nginx 重复声明同一组头用于纵深防御；API 中间件仍保留 CSP / Referrer-Policy / Permissions-Policy。注意 nginx `add_header` 不会被自带 `add_header` 的 location 继承，反代片段里禁止再写 `add_header`。
5. **数据库迁移**：生产环境必须关闭自动 Code First，发布前备份并使用 `--migrate` 受控迁移；回滚应用镜像不等于回滚数据库结构
6. **数据种子与首次初始化**：生产环境关闭自动种子；首次初始化直接访问站点 `/setup` 设置管理员账号密码、站点名称、默认语言与主题、开放注册、登录失败上限和锁定时长。提交后前端展示初始化进度，后端先创建管理员，再创建不可登录的系统账号 `system`，并由系统账号作为创建人执行基础种子（创建时间取当前时间），完成后 `/setup` 自动关闭。未完成初始化时启动自动种子与 `--seed` 都会跳过；初始化完成后如需补齐升级新增数据，再显式执行 `--seed`。初始化接口在完成前匿名开放，部署后应立即完成初始化并确认再次访问 `/setup` 会跳转登录页
7. **Quartz 表**：生产使用外部配置的持久化 JobStore；SQLite 冒烟环境通过空 `Quartz:JobStore:Provider` 使用内存 JobStore
8. **挂载目录权限**：`secrets`、`api-logs`、`api-data` 的宿主机 owner 必须与镜像运行 UID `1000:1000` 对齐，否则 API 会在读取 secrets 或写入日志/Data Protection 时启动失败。
9. **可信转发头**：API 位于 Docker 网桥或 OpenResty/nginx 之后时，必须配置 `ForwardedHeaders__KnownNetworks__0` 或 `ForwardedHeaders__KnownProxies__0`，并确保代理传递 `X-Forwarded-Proto: https`；否则生产防伪 Cookie 会把请求识别为 HTTP 并报错。只信任实际代理网段或网关 IP。frp 穿透场景：公网 443 由 frpc 以 PROXY protocol v2 转发到本机 18443，`listen 18443 ssl` 必须带 `proxy_protocol`；公网 80 不做 PP（只做 301，不需要客户端 IP），本机 80 保持普通监听。用 `set_real_ip_from 127.0.0.1; real_ip_header proxy_protocol;`（下划线）取真实 IP；写成 `proxy-protocol` 不会生效。
10. **Swagger Basic Auth**：`.htpasswd` 不入库，由 `setup-secrets` 菜单生成；轮换时重新生成文件并执行 `docker compose -f docker-compose.web.yml exec web nginx -s reload`。Basic Auth 只保护文档面，业务接口仍必须依赖 JWT Bearer 与接口授权。
11. **镜像仓库**：`registry-credentials` 只授予推送所需权限；目标机使用独立的只读账号或 Registry 代理，不要把推送凭据写入 `.env`、Compose 文件或镜像。
12. **发布权限**：只有 `develop` 推送自动部署测试，生产必须由 `v*` 标签触发并经 `sim-release` 人工审批；PR 和 `main` 不得自动部署生产。部署账号只允许操作 `/opt/sim` 和对应 Compose 项目。
13. **健康端点与 robots**：公网仅暴露 `/health/live`、`/health/ready`；聚合 `/health` 在 1Panel 侧返回 404。`/robots.txt`、`/.well-known/security.txt` 由边缘返回真实内容，避免 SPA 回退成 200。HTTP :80 由 frp http 隧道直连本机 80（不使用 PROXY protocol），vhost 用 `if ($scheme = http) return 301`；HTTPS 真实 IP 由 18443 的 PROXY v2 提供。

## 手动迁移（不使用 Docker）

```bash
cd api
export DOTNET_ENVIRONMENT=Production
export SIM_SECRETS_DIRECTORY=/path/to/secrets
dotnet run --project src/Sim.Admin.HttpApi.Host -- --migrate
dotnet run --project src/Sim.Admin.HttpApi.Host
```

首次部署启动后访问站点 `/setup` 完成初始化（后端会自动执行种子）；后续升级在初始化完成后可再执行 `dotnet run --project src/Sim.Admin.HttpApi.Host -- --seed` 补齐新增基础数据。


## E2EE 前端发布检查

静态站点与 TLS 反代 CSP 必须包含 `worker-src 'self'`，支持同源模块 Worker。前端生产构建后执行 `pnpm check:security`、`pnpm check:security --verify`、`pnpm check:e2ee-workers`；CI 归档并 fingerprint `web/dist/release-integrity.json`。部署后应与可信 CI 清单核对文件 SHA-256，并检查实际 HTML 响应安全头；仓库检查不等于已验证生产响应，清单也不是签名或恶意服务端防护。客户端加密文件为 `.sime2ee` 二进制，经现有存储 provider 保存；不依赖对象存储的服务端解密。

## E2EE 身份与通行密钥部署

System 提供身份层 E2EE（`system_user_e2ee_identity`、`system_user_e2ee_passkey`），当前仓库不包含业务加密实体。上线前备份数据库与 `DataProtection:KeysPath`，确保所有实例共享持久密钥环。业务模块接入端到端加密后，须在自身设计文档中补充密文范围、历史迁移与回滚限制，不得回滚到不认识新密文版本的服务端。

通行密钥解锁生产默认关闭。通过 `E2eePasskeys__Enabled=true`、`E2eePasskeys__RpId=example.com`、`E2eePasskeys__Origins__0=https://example.com` 配置（按实际域名修改），来源不能有通配符或路径，RP ID 不带协议/端口。上线先建新凭证/挑战表，保持 RP ID 稳定；域名变更可能需要用口令/RK 重新登记。数据库保存 UMK 密文包裹，须纳入备份；不是服务端托管解密。旧前端应刷新升级 IndexedDB v3。

API 镜像启动验证必须挂载环境配置；隔离测试使用 Local 缓存、禁用外部消息、临时 SQLite 连接和临时文件目录。Linux 运行层需包含 libsqlite3-0，避免数据库模块初始化时报 sqlite3 动态库缺失。

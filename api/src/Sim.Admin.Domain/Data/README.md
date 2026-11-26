# 数据种子说明

本目录包含系统初始化所需的种子数据类。

## 种子数据执行顺序

种子数据按以下顺序执行（在 `DataSeederContributor.cs` 中定义）：

### 1. RoleDataSeeder - 角色种子

创建系统的基础角色：

- **Admin** (超级管理员) - 拥有所有权限和模块访问权

- **User** (普通用户) - 拥有基本权限；`UserRoleAssignmentDataSeeder` 幂等补齐 `approval` 模块，管理员可在角色管理中调整

### 2. UserDataSeeder - 用户种子

创建系统的基础用户并分配对应角色：

- **admin 用户**（兼容历史种子；新站点由 Web `/setup` 创建管理员，不再预置占位账号）

  - 用户名：`admin`

  - 密码：通过密钥配置 `App:AdminDefaultPassword` 提供，源码不包含默认值

  - 邮箱：`admin@simadmin.com`

  - 角色：Admin

### 2.1 SystemUserDataSeeder - 系统账号种子

初始化时在管理员账号创建之后执行，幂等确保系统账号存在：

- **system 用户**

  - 用户名：`system`

  - 显示名称：系统

  - 状态：停用（不可登录），不设置密码

  - 用途：作为角色、菜单、权限、系统配置、组织、审批定义等系统自动创建数据的创建人

  - 创建人：初始化时由管理员账号创建

### 3. ModuleDataSeeder - 模块种子

创建系统的两个基础功能模块：

- **system-management** (系统管理模块)

  - 描述：系统配置和管理功能

  - 图标：`carbon:settings`

  - 布局：`base`

  - 状态：启用

- **authorization-management** (授权管理模块)

  - 描述：用户、角色、权限管理功能

  - 图标：`carbon:user-admin`

  - 布局：`base`

  - 状态：启用

### 4. RouteDataSeeder - 路由种子

对已存在的路由也会幂等补齐 `module_route` 模块关联，用于修复关联表引入前创建的存量库。组织架构与租户管理历史上位于授权管理菜单，种子会把路由父级、旧默认排序和模块关联迁移到系统管理，并同步根菜单权限并集（仅在仍为历史并集时替换）。

创建系统路由配置（RouteConfig 就是菜单，只是从技术角度命名）：

- **默认访问权限**：`RoutePermissionCatalog` 按路由名匹配页面读取接口所需的权限码（如 `UserManagement` → `system:user:query`、`FileStorageManagement` → `system:file-storage:manage`、`ApprovalMonitor` → `approval:instance:monitor`）；根路由取全部子页面权限的并集（任一满足即可进入根菜单，子页面仍逐项裁剪），含个人页或无读取权限子页的根路由（`Approval`）保持不限制。新建路由直接写入，已有路由只在权限码为空时回填，不覆盖管理员在路由管理页维护的结果。

#### 系统管理模块路由

- **系统管理父路由** (`/system`)

  - 名称：System

  - 组件：Layout

  - 元数据：标题、图标、排序

  - **子路由**：

    - 文件存储 (`/system/file-storage`)

    - 定时任务 (`/system/schedule-task`)

    - 通知服务 (`/system/notifications`)

    - 业务编号 (`/system/numbering`)

    - 系统配置 (`/system/configuration`)

    - 数据字典 (`/system/data-dictionary`)

    - 数据字典数据 (`/system/data-dictionary-data/:dictionaryId`)

    - 租户管理 (`/system/tenant`)

    - 组织架构 (`/system/organization`)

#### 授权管理模块路由

- **授权管理父路由** (`/authorization`)

  - 名称：Authorization

  - 组件：Layout

  - 元数据：标题、图标、排序

  - **子路由**：

    - 用户管理 (`/authorization/user`)

    - 角色管理 (`/authorization/role`)

    - 模块管理 (`/authorization/module`)

    - 路由管理 (`/authorization/route`)

    - 权限管理 (`/authorization/permission`)

### 5. PermissionDataSeeder - 权限种子

按 `PermissionCatalog` 幂等补齐全部可分配权限（超级权限 + 各模块细粒度权限）：

- **权限清单**：`PermissionCatalog.All` 是唯一事实来源，权限码统一为 `module:resource:action`；服务上的 `[Permission]` 只能引用清单内的权限码，`PermissionCatalogTests` 自动校验一致性

- **超级权限** (`*:*:*`)

  - 名称：超级权限

  - 描述：拥有系统所有权限

  - 类型：按钮权限

  - 说明：此权限代表完整的系统控制权，只应分配给 Admin 角色

- **历史权限码规范化**：`sys:*`、`System.Notification.*` 由种子就地改名（保留角色授权关系），重复记录会被清理

### 6. AdminRoleAssignmentDataSeeder - 管理员角色分配种子

为 Admin 角色分配权限和模块：

- **权限分配**：将超级权限 (`*:*:*`) 分配给 Admin 角色

- **模块分配**：将所有模块分配给 Admin 角色（通过模块控制路由/菜单访问）

- 支持幂等性（不会重复分配已存在的关联）

### 6.1 UserRoleAssignmentDataSeeder - 普通用户基础权限种子

为自助注册默认领取的 `User` 角色补齐基础能力：

- **模块分配**：`approval`（审批中心）端用户模块
- 只追加缺失关联，不回收管理员移除的授权

### 7. OrganizationDataSeeder、ApprovalDataSeeder 与系统配置种子

- `OrganizationDataSeeder` 幂等创建根组织节点，供组织树与审批人来源解析使用
- `ApprovalDataSeeder` 幂等补齐审批实例业务编号定义
- `SystemConfigurationDataSeeder` 补齐运行参数；`TenantDataSeeder` 创建默认租户

## 如何运行种子数据

首次初始化由 Web `/setup` 提交触发：后端先创建或更新管理员账号，再创建不可登录的系统账号 `system`，由系统账号作为审计归属人执行种子，创建时间取当前时间。系统未完成初始化时，以下两种方式都会被跳过；初始化完成后执行时会先确保系统账号存在。

### 方式1：初始化完成后通过命令行参数补齐

```bash
dotnet run --project src/Sim.Admin.HttpApi.Host -- --seed
```

### 方式2：通过配置文件

在 `appsettings.json` 中设置：

```json
{
  "DataBase": {
    "EnabledDbSeed": true
  }
}
```

然后正常启动应用（仅在站点已完成初始化后执行）：

```bash
dotnet run --project src/Sim.Admin.HttpApi.Host
```

## 数据依赖关系

种子数据的执行顺序遵循以下依赖关系：

```
SystemUserDataSeeder (系统账号，初始化时由管理员创建)
    ↓
RoleDataSeeder (角色)
    ↓
UserDataSeeder (用户，依赖角色)
    ↓
ModuleDataSeeder (模块)
    ↓
RouteDataSeeder (路由，依赖模块)
    ↓
PermissionDataSeeder (权限)
    ↓
AdminRoleAssignmentDataSeeder (管理员角色分配，依赖角色、权限、模块)
    ↓
UserRoleAssignmentDataSeeder (普通用户基础模块，依赖角色、权限、模块)
```

**重要提示**：

- 种子只在站点完成初始化后执行；初始化提交时会先写入管理员，再创建系统账号并以系统账号身份运行

- 种子插入的记录统一以系统账号作为 `CreatorId`，`CreationTime` 取当前时间

- 必须按照此顺序执行，否则会因为外键约束导致失败

- 所有操作在同一个事务中执行，任何步骤失败都会回滚整个事务

- 所有种子数据类都支持幂等性，可以安全地多次执行

## 添加新的种子数据

### 步骤1：创建种子数据类

在 `Data` 目录下创建新的种子数据类，例如：

```csharp
public class MyModuleDataSeeder : ITransientDependency
{
    private readonly ILogger<MyModuleDataSeeder> _logger;
    private readonly IMyRepository _myRepository;
    
    public MyModuleDataSeeder(
        ILogger<MyModuleDataSeeder> logger,
        IMyRepository myRepository)
    {
        _logger = logger;
        _myRepository = myRepository;
    }
    
    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        _logger.LogInformation("开始创建我的模块数据...");
        
        // 检查数据是否已存在（幂等性）
        var existing = await _myRepository.GetAsync(x => x.Code == "my-code");
        if (existing != null)
        {
            _logger.LogDebug("数据已存在，跳过创建");
            return;
        }
        
        // 创建新数据
        var entity = new MyEntity
        {
            Code = "my-code",
            Name = "我的数据"
        };
        await _myRepository.InsertAsync(entity);
        
        _logger.LogInformation("我的模块数据创建完成");
    }
}
```

### 步骤2：注册到主入口

在 `DataSeederContributor.cs` 中：

1. 添加依赖注入：

```csharp
private readonly MyModuleDataSeeder _myModuleDataSeeder;

public DataSeederContributor(
    // ... 其他参数
    MyModuleDataSeeder myModuleDataSeeder)
{
    _myModuleDataSeeder = myModuleDataSeeder;
}
```

1. 在 `SeedAsync` 方法中按正确顺序调用：

```csharp
// 在适当的位置调用，注意依赖关系
await _myModuleDataSeeder.SeedAsync(context);
```

## 注意事项

1. **幂等性**：所有种子数据类都应该是幂等的，即多次执行不会产生重复数据

   - 在创建实体前检查是否已存在

   - 在创建关联关系前检查是否已存在

   - 使用 `GetAsync` 或 `AnyAsync` 进行存在性检查

2. **事务**：使用 `[UnitOfWork]` 特性确保数据一致性

   - 所有操作在同一个事务中执行

   - 任何步骤失败都会回滚整个事务

   - 确保数据库状态保持一致

3. **日志**：记录详细的执行日志，便于调试和监控

   - Information: 开始/完成、实体创建成功

   - Debug: 跳过已存在的实体

   - Warning: 引用的实体不存在但可以继续

   - Error: 导致事务回滚的错误

4. **错误处理**：种子数据执行失败会抛出异常并回滚事务

   - 实体不存在错误：记录错误并抛出异常

   - 数据验证错误：记录错误并抛出异常

   - 数据库连接错误：记录错误并抛出异常

5. **生产环境**：禁用自动种子数据；首次部署通过 Web `/setup` 完成初始化（提交后先创建管理员并自动执行种子）

   - 设置 `DataBase:EnabledDbSeed` 为 `false`

   - 初始化完成后再按需使用 `--seed` 补齐升级数据

   - 备份数据库后再执行种子数据

6. **依赖顺序**：严格遵循数据依赖关系顺序

   - 先创建被依赖的实体（如角色）

   - 再创建依赖其他实体的实体（如用户）

   - 最后创建关联关系（如角色权限分配）

## 权限说明

### 超级权限 (*:*:\*)

系统使用超级权限 `*:*:*` 代替细粒度权限，简化权限管理：

- **含义**：拥有系统所有操作权限

- **分配**：只分配给 Admin 角色

- **检查**：权限检查时，拥有 `*:*:*` 的用户可以执行任何操作

- **扩展**：如需细粒度权限，可通过管理界面手动添加

### 权限命名规范（扩展用）

如果需要添加细粒度权限，遵循以下命名规范：

```
{模块}:{资源}:{操作}
```

示例：

- `system:user:create` - 系统模块的用户资源的创建操作

- `system:role:manage` - 系统模块的角色资源的维护操作

- `approval:definition:manage` - 审批模块的流程定义维护操作

常用操作：

- `query` - 查询/查看

- `create` - 创建/新增

- `update` - 更新/修改

- `delete` - 删除

- `export` - 导出

- `import` - 导入

## 数据种子类列表

| 类名                              | 职责                                       | 依赖                                                     |
| ------------------------------- | ---------------------------------------- | ------------------------------------------------------ |
| `RoleDataSeeder`                | 创建基础角色                                   | 无                                                      |
| `UserDataSeeder`                | 创建基础用户并分配角色                              | RoleDataSeeder                                         |
| `SystemUserDataSeeder`          | 幂等创建不可登录的系统账号，作为系统数据创建人             | 无                                                      |
| `ModuleDataSeeder`              | 创建功能模块                                   | 无                                                      |
| `RouteDataSeeder`               | 创建路由配置（菜单）：系统管理、授权管理、审批中心             | ModuleDataSeeder                                       |
| `PermissionDataSeeder`          | 按 `PermissionCatalog` 幂等补齐全部权限并规范化历史权限码  | `PermissionCatalog`                                    |
| `AdminRoleAssignmentDataSeeder` | 为 Admin 角色分配权限和模块                        | RoleDataSeeder, PermissionDataSeeder, ModuleDataSeeder |
| `UserRoleAssignmentDataSeeder` | 为 User 角色补齐 approval 模块（仅追加）            | RoleDataSeeder, PermissionDataSeeder, ModuleDataSeeder |
| `OrganizationDataSeeder`        | 幂等创建根组织节点                              | 无                                                     |
| `ApprovalDataSeeder`            | 幂等补齐审批实例业务编号定义                        | 无                                                     |

## 常见问题

### Q: 为什么要使用超级权限而不是细粒度权限？

A: 超级权限简化了大范围授权的初始化，但系统仍按 `PermissionCatalog` 为各模块预置细粒度权限（如 `system:user:query`、`approval:definition:manage`），便于按菜单和按钮授权。新增接口权限时先在 `PermissionCatalog` 登记，再让种子在启动时补齐。

### Q: 如何设置初始化密码？

A: 首次初始化时在 Web `/setup` 页面设置管理员密码，后端会先写入管理员账号再执行种子。`App:AdminDefaultPassword` 只用于租户管理员的初始密码兜底，不再预置默认 `admin` 占位账号；如确实需要配置，应通过密钥管理系统或环境变量提供，不要把真实密码提交到 `appsettings.json`。

### Q: 数据种子可以多次执行吗？

A: 可以。所有种子数据类都实现了幂等性，多次执行不会产生重复数据。

### Q: 如何清空数据库重新初始化？

A:

1. 删除数据库文件（SQLite）或清空数据库（其他数据库）
2. 运行应用时会自动创建表结构
3. 执行数据种子初始化

### Q: 种子数据执行失败怎么办？

A:

1. 查看日志了解具体错误信息
2. 检查数据库连接是否正常
3. 确认依赖的实体是否已创建
4. 事务会自动回滚，可以修复问题后重新执行


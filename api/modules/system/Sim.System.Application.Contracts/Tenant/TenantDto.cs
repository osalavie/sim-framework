namespace Sim.System.Application.Contracts.Tenant;

/// <summary>
/// 租户信息 DTO
/// </summary>
public class TenantDto
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 租户名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 租户编码（唯一，host/default 为系统内置租户）
    /// </summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>
    /// 是否已启用
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// 租户管理员邮箱
    /// </summary>
    public string? AdminEmail { get; set; }
    /// <summary>
    /// 租户独立数据库连接字符串（可选）
    /// </summary>
    public string? ConnectionString { get; set; }
    /// <summary>
    /// 租户创建时间
    /// </summary>
    public DateTime CreationTime { get; set; }
    /// <summary>
    /// 创建人标识
    /// </summary>
    public Guid? CreatorId { get; set; }
    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime? LastModificationTime { get; set; }
    /// <summary>
    /// 最后修改人标识
    /// </summary>
    public Guid? LastModifierId { get; set; }
}

/// <summary>
/// 创建租户的请求 DTO
/// </summary>
public class CreateTenantDto
{
    /// <summary>
    /// 租户名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 租户编码（唯一）
    /// </summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>
    /// 租户管理员邮箱
    /// </summary>
    public string? AdminEmail { get; set; }
    /// <summary>
    /// 租户独立数据库连接字符串（可选）
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 租户管理员用户名（可选，默认 "admin"）
    /// </summary>
    public string? AdminUserName { get; set; }

    /// <summary>
    /// 租户管理员初始密码（可选，省略时生成随机初始密码并在创建结果中返回）
    /// </summary>
    public string? AdminPassword { get; set; }

    /// <summary>
    /// 分配给该租户的模块 ID 列表（Host 级模板的 ID）
    /// </summary>
    public List<Guid> ModuleIds { get; set; } = new();

    /// <summary>
    /// 分配给该租户的路由 ID 列表（Host 级模板的 ID）
    /// </summary>
    public List<Guid> RouteIds { get; set; } = new();

    /// <summary>
    /// 分配给该租户的权限 ID 列表（Host 级模板的 ID）
    /// </summary>
    public List<Guid> PermissionIds { get; set; } = new();
}

/// <summary>
/// 更新租户的请求 DTO
/// </summary>
public class UpdateTenantDto
{
    /// <summary>
    /// 租户名称（仅非空时更新）
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// 租户管理员邮箱（仅非空时更新）
    /// </summary>
    public string? AdminEmail { get; set; }
    /// <summary>
    /// 租户独立数据库连接字符串（仅非空时更新）
    /// </summary>
    public string? ConnectionString { get; set; }
}

/// <summary>
/// 创建租户的结果（包含管理员初始凭证）
/// </summary>
public class CreateTenantResultDto
{
    /// <summary>
    /// 新创建租户的 ID
    /// </summary>
    public Guid TenantId { get; set; }
    /// <summary>
    /// 新创建租户的名称
    /// </summary>
    public string TenantName { get; set; } = string.Empty;
    /// <summary>
    /// 新创建租户的编码
    /// </summary>
    public string TenantCode { get; set; } = string.Empty;
    /// <summary>
    /// 创建的管理员用户 ID
    /// </summary>
    public Guid AdminUserId { get; set; }
    /// <summary>
    /// 创建的管理员用户名
    /// </summary>
    public string AdminUserName { get; set; } = string.Empty;
    /// <summary>
    /// 管理员初始密码（首次登录后应尽快修改）
    /// </summary>
    public string InitialPassword { get; set; } = string.Empty;
    /// <summary>
    /// 分配给该租户的模块数量
    /// </summary>
    public int AssignedModuleCount { get; set; }
    /// <summary>
    /// 分配给该租户的路由数量
    /// </summary>
    public int AssignedRouteCount { get; set; }
    /// <summary>
    /// 分配给该租户的权限数量
    /// </summary>
    public int AssignedPermissionCount { get; set; }
}

/// <summary>
/// 租户可分配的模板配置（Host 级模块/路由/权限列表）
/// </summary>
public class TenantTemplateDto
{
    /// <summary>
    /// 可选分配的 Host 级模块列表
    /// </summary>
    public List<TemplateModuleDto> Modules { get; set; } = new();
    /// <summary>
    /// 可选分配的 Host 级路由列表
    /// </summary>
    public List<TemplateRouteDto> Routes { get; set; } = new();
    /// <summary>
    /// 可选分配的 Host 级权限列表
    /// </summary>
    public List<TemplatePermissionDto> Permissions { get; set; } = new();
}

/// <summary>
/// Host 级模块模板 DTO（创建租户时可分配给租户）
/// </summary>
public class TemplateModuleDto
{
    /// <summary>
    /// 模块 ID
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 模块名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 模块描述
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// 模块图标
    /// </summary>
    public string? Icon { get; set; }
    /// <summary>
    /// 排序值
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Host 级路由模板 DTO（创建租户时可分配给租户）
/// </summary>
public class TemplateRouteDto
{
    /// <summary>
    /// 路由 ID
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 父路由 ID（用于层级结构）
    /// </summary>
    public Guid? ParentId { get; set; }
    /// <summary>
    /// 路由名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 路由路径
    /// </summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>
    /// 路由标题（菜单展示名称）
    /// </summary>
    public string? Title { get; set; }
    /// <summary>
    /// 路由图标
    /// </summary>
    public string? Icon { get; set; }
    /// <summary>
    /// 是否显示在菜单中
    /// </summary>
    public bool Menu { get; set; }
    /// <summary>
    /// 排序值
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Host 级权限模板 DTO（创建租户时可分配给租户）
/// </summary>
public class TemplatePermissionDto
{
    /// <summary>
    /// 权限 ID
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 权限名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 权限编码（唯一）
    /// </summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>
    /// 权限描述
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 租户已分配的配置详情
/// </summary>
public class TenantAssignmentDto
{
    /// <summary>
    /// 租户 ID
    /// </summary>
    public Guid TenantId { get; set; }
    /// <summary>
    /// 租户名称
    /// </summary>
    public string TenantName { get; set; } = string.Empty;
    /// <summary>
    /// 该租户已分配的模块列表
    /// </summary>
    public List<TemplateModuleDto> Modules { get; set; } = new();
    /// <summary>
    /// 该租户已分配的路由列表
    /// </summary>
    public List<TemplateRouteDto> Routes { get; set; } = new();
    /// <summary>
    /// 该租户已分配的权限列表
    /// </summary>
    public List<TemplatePermissionDto> Permissions { get; set; } = new();
}

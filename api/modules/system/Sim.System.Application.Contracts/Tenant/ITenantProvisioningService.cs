namespace Sim.System.Application.Contracts.Tenant;

/// <summary>
/// 租户配置初始化服务接口。
/// 定义在 Contracts 层，实现在 Sim.Admin.Domain 层（可访问 System 模块的全部实体）。
/// TenantAppService 通过 DI 注入此接口，将创建租户的完整初始化逻辑委托给实现类。
/// </summary>
public interface ITenantProvisioningService
{
    /// <summary>
    /// 创建租户并完成完整初始化：
    /// 1. 创建 TenantEntity
    /// 2. 复制选中的模块/路由/权限到租户名下
    /// 3. 创建租户管理员用户
    /// 4. 创建租户管理员角色并分配权限/模块
    /// 5. 绑定管理员用户与角色
    /// </summary>
    Task<CreateTenantResultDto> ProvisionAsync(CreateTenantDto input);

    /// <summary>
    /// 获取 Host 级可选模板配置（模块/路由/权限列表）
    /// </summary>
    Task<TenantTemplateDto> GetTemplatesAsync();

    /// <summary>
    /// 获取指定租户已分配的配置详情
    /// </summary>
    Task<TenantAssignmentDto> GetAssignmentsAsync(Guid tenantId);

    /// <summary>
    /// 删除租户身份与授权数据：用户、角色、授权关系、租户模板副本和刷新令牌。
    /// 调用方负责在完成后删除租户实体本身。
    /// </summary>
    Task DeleteTenantDataAsync(Guid tenantId);
}

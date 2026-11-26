using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.Tenant;

/// <summary>
/// 租户管理应用服务：提供租户的增删改查、启停以及 Host 级模板与分配的查询能力。
/// ABP 会自动将此接口暴露为 HTTP API。
/// </summary>
public interface ITenantAppService : IApplicationService
{
    /// <summary>
    /// 根据 ID 获取租户详情。
    /// </summary>
    /// <param name="id">租户 ID</param>
    /// <returns>租户信息</returns>
    Task<TenantDto> GetAsync(Guid id);
    /// <summary>
    /// 获取全部租户列表。
    /// </summary>
    /// <returns>租户信息列表</returns>
    Task<List<TenantDto>> GetListAsync();
    /// <summary>
    /// 创建租户并初始化管理员账号与分配的模块/路由。
    /// </summary>
    /// <param name="input">创建租户的请求数据</param>
    /// <returns>创建结果，包含租户 ID 与管理员初始凭证说明</returns>
    Task<CreateTenantResultDto> CreateAsync(CreateTenantDto input);
    /// <summary>
    /// 更新租户信息（仅更新传入的非空字段）
    /// </summary>
    /// <param name="id">租户 ID</param>
    /// <param name="input">更新租户的请求数据</param>
    /// <returns>更新后的租户信息</returns>
    Task<TenantDto> UpdateAsync(Guid id, UpdateTenantDto input);
    /// <summary>
    /// 启用租户，启用后租户可正常访问其分配的资源。
    /// </summary>
    /// <param name="id">租户 ID</param>
    Task EnableAsync(Guid id);
    /// <summary>
    /// 禁用租户，禁用后租户访问将被拒绝。
    /// </summary>
    /// <param name="id">租户 ID</param>
    Task DisableAsync(Guid id);
    /// <summary>
    /// 删除指定租户（host/default 租户不允许删除）
    /// </summary>
    /// <param name="id">租户 ID</param>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// 获取 Host 级可选模板配置（模块/路由/权限列表）
    /// </summary>
    Task<TenantTemplateDto> GetTemplatesAsync();

    /// <summary>
    /// 获取指定租户已分配的配置详情
    /// </summary>
    Task<TenantAssignmentDto> GetAssignmentsAsync(Guid id);
}

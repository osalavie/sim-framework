using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.Tenant;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.Tenant;

namespace Sim.System.Application.Tenant;

/// <summary>
/// 租户管理应用服务实现：委托 <see cref="TenantManager"/> 完成租户启停与删除，
/// 委托 <see cref="ITenantProvisioningService"/> 完成租户创建初始化与模板/分配查询。
/// </summary>
[Permission(TenantPermissions.Manage)]
public class TenantAppService : ApplicationService, ITenantAppService
{
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantProvisioningService _provisioningService;

    public TenantAppService(
        TenantManager tenantManager,
        ITenantRepository tenantRepository,
        ITenantProvisioningService provisioningService)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _provisioningService = provisioningService;
    }

    /// <summary>
    /// 根据 ID 获取租户详情。
    /// </summary>
    /// <param name="id">租户 ID</param>
    /// <returns>租户信息</returns>
    public async Task<TenantDto> GetAsync(Guid id)
    {
        var tenant = await _tenantRepository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.TenantNotFound, "Tenant not found.");
        return MapToDto(tenant);
    }

    /// <summary>
    /// 获取全部租户列表。
    /// </summary>
    /// <returns>租户信息列表</returns>
    public async Task<List<TenantDto>> GetListAsync()
    {
        var tenants = await _tenantRepository.GetAll();
        return tenants.Select(MapToDto).ToList();
    }

    /// <summary>
    /// 创建租户并初始化管理员账号与分配的模块/路由。
    /// </summary>
    /// <param name="input">创建租户的请求数据</param>
    /// <returns>创建结果，包含租户 ID 与管理员初始凭证说明</returns>
    [SqlSugarUnitOfWork]
    public async Task<CreateTenantResultDto> CreateAsync(CreateTenantDto input)
    {
        // 委托给 TenantProvisioningService 完成完整初始化
        // （创建租户 + 复制配置 + 创建管理员用户/角色 + 分配权限/模块）
        return await _provisioningService.ProvisionAsync(input);
    }

    /// <summary>
    /// 更新租户信息（仅更新传入的非空字段）
    /// </summary>
    /// <param name="id">租户 ID</param>
    /// <param name="input">更新租户的请求数据</param>
    /// <returns>更新后的租户信息</returns>
    public async Task<TenantDto> UpdateAsync(Guid id, UpdateTenantDto input)
    {
        var tenant = await _tenantRepository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.TenantNotFound, "Tenant not found.");

        if (!string.IsNullOrWhiteSpace(input.Name))
            tenant.Name = input.Name.Trim();
        if (input.AdminEmail is not null)
            tenant.AdminEmail = input.AdminEmail.Trim();
        if (input.ConnectionString is not null)
            tenant.ConnectionString = input.ConnectionString;

        await _tenantRepository.UpdateAsync(tenant);
        return MapToDto(tenant);
    }

    /// <summary>
    /// 启用租户，启用后租户可正常访问其分配的资源。
    /// </summary>
    /// <param name="id">租户 ID</param>
    [SqlSugarUnitOfWork]
    public async Task EnableAsync(Guid id)
    {
        await _tenantManager.EnableAsync(id);
    }

    /// <summary>
    /// 禁用租户，禁用后租户访问将被拒绝。
    /// </summary>
    /// <param name="id">租户 ID</param>
    [SqlSugarUnitOfWork]
    public async Task DisableAsync(Guid id)
    {
        await _tenantManager.DisableAsync(id);
    }

    /// <summary>
    /// 删除指定租户（host/default 租户不允许删除）。
    /// </summary>
    /// <param name="id">租户 ID</param>
    [SqlSugarUnitOfWork]
    public async Task DeleteAsync(Guid id)
    {
        var tenant = await _tenantRepository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.TenantNotFound, "Tenant not found.");
        if (tenant.Code is "host" or "default")
            throw new BusinessException(SystemErrorCodes.TenantCannotDeleteHost);
        await _provisioningService.DeleteTenantDataAsync(tenant.Id);
        await _tenantRepository.DeleteAsync(tenant.Id);
    }

    /// <summary>
    /// 获取 Host 级可选模板配置（模块/路由/权限列表）。
    /// </summary>
    /// <returns>Host 级可选模板配置</returns>
    public async Task<TenantTemplateDto> GetTemplatesAsync()
    {
        return await _provisioningService.GetTemplatesAsync();
    }

    /// <summary>
    /// 获取指定租户已分配的配置详情
    /// </summary>
    /// <param name="id">租户 ID</param>
    /// <returns>租户已分配的配置详情</returns>
    public async Task<TenantAssignmentDto> GetAssignmentsAsync(Guid id)
    {
        return await _provisioningService.GetAssignmentsAsync(id);
    }

    private static TenantDto MapToDto(TenantEntity tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Code = tenant.Code,
        IsActive = tenant.IsActive,
        AdminEmail = tenant.AdminEmail,
        ConnectionString = tenant.ConnectionString,
        CreationTime = tenant.CreationTime,
        CreatorId = tenant.CreatorId,
        LastModificationTime = tenant.LastModificationTime,
        LastModifierId = tenant.LastModifierId
    };
}

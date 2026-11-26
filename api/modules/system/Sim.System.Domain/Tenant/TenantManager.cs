using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Domain.Tenant;

/// <summary>
/// 租户领域服务 — 创建、启用、禁用租户。
/// </summary>
public class TenantManager : DomainService
{
    private readonly ISqlSugarRepository<TenantEntity> _tenantRepository;
    private readonly ILogger<TenantManager> _logger;

    public TenantManager(
        ISqlSugarRepository<TenantEntity> tenantRepository,
        ILogger<TenantManager> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>创建新租户</summary>
    public async Task<TenantEntity> CreateAsync(
        string name, string code, string? adminEmail = null, string? connectionString = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessException(SystemErrorCodes.TenantInvalidName, "Tenant name is required.");

        if (string.IsNullOrWhiteSpace(code))
            throw new BusinessException(SystemErrorCodes.TenantInvalidCode, "Tenant code is required.");

        if ((await _tenantRepository.GetListAsync(t => t.Code == code)).Count > 0)
            throw new BusinessException(SystemErrorCodes.TenantDuplicateCode,
                $"A tenant with code '{code}' already exists.");

        var tenant = new TenantEntity
        {
            Name = name.Trim(),
            Code = code.Trim().ToLowerInvariant(),
            AdminEmail = adminEmail?.Trim(),
            ConnectionString = connectionString,
            IsActive = true
        };

        await _tenantRepository.InsertAsync(tenant);
        _logger.LogInformation("Tenant created: {TenantId} ({Name}/{Code})", tenant.Id, name, code);
        return tenant;
    }

    /// <summary>启用租户</summary>
    public async Task EnableAsync(Guid tenantId)
    {
        var tenant = await _tenantRepository.GetAsync(tenantId)
            ?? throw new BusinessException(SystemErrorCodes.TenantNotFound, "Tenant not found.");
        if (!tenant.IsActive)
        {
            tenant.IsActive = true;
            await _tenantRepository.UpdateAsync(tenant);
            _logger.LogInformation("Tenant {TenantId} ({Name}) enabled", tenantId, tenant.Name);
        }
    }

    /// <summary>禁用租户</summary>
    public async Task DisableAsync(Guid tenantId)
    {
        var tenant = await _tenantRepository.GetAsync(tenantId)
            ?? throw new BusinessException(SystemErrorCodes.TenantNotFound, "Tenant not found.");
        if (tenant.IsActive)
        {
            tenant.IsActive = false;
            await _tenantRepository.UpdateAsync(tenant);
            _logger.LogInformation("Tenant {TenantId} ({Name}) disabled", tenantId, tenant.Name);
        }
    }
}

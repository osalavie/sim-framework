using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Tenant;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 租户数据种子
/// 负责创建默认租户，供多租户功能启用后使用。
/// </summary>
public class TenantDataSeeder
{
    private readonly ISqlSugarRepository<TenantEntity> _tenantRepository;
    private readonly ILogger<TenantDataSeeder> _logger;

    /// <summary>默认租户名称</summary>
    public const string DefaultTenantName = "Default";

    /// <summary>默认租户编码</summary>
    public const string DefaultTenantCode = "default";

    public TenantDataSeeder(
        ISqlSugarRepository<TenantEntity> tenantRepository,
        ILogger<TenantDataSeeder> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>
    /// 执行租户数据种子
    /// 幂等：如果默认租户已存在则跳过。
    /// </summary>
    public async Task SeedAsync()
    {
        _logger.LogInformation("========== 开始创建默认租户 ==========");

        var existing = await _tenantRepository.GetAsync(t => t.Code == DefaultTenantCode);
        if (existing != null)
        {
            _logger.LogInformation("默认租户已存在: ID={TenantId}, Name={Name}, Code={Code}",
                existing.Id, existing.Name, existing.Code);
            return;
        }

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Name = DefaultTenantName,
            Code = DefaultTenantCode,
            IsActive = true,
            AdminEmail = null,
            ConnectionString = null
        };

        await _tenantRepository.InsertAsync(tenant);
        _logger.LogInformation("默认租户创建成功: ID={TenantId}, Name={Name}, Code={Code}",
            tenant.Id, tenant.Name, tenant.Code);

        _logger.LogInformation("========== 默认租户创建完成 ==========");
    }
}

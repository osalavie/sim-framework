using Microsoft.Extensions.Logging;
using Sim.SqlSugar.Core.Repositories;
using System;
using System.Threading.Tasks;
using Sim.System.Domain.Organization;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 组织架构数据种子
/// 幂等创建根组织节点 `Sim`（Path=/sim, Depth=0），供组织树与审批人来源解析使用。
/// </summary>
public class OrganizationDataSeeder
{
    private readonly ISqlSugarRepository<OrganizationUnitEntity> _organizationRepository;
    private readonly ILogger<OrganizationDataSeeder> _logger;

    public const string RootOrganizationCode = "sim";
    public const string RootOrganizationName = "Sim";

    public OrganizationDataSeeder(
        ISqlSugarRepository<OrganizationUnitEntity> organizationRepository,
        ILogger<OrganizationDataSeeder> logger)
    {
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var existing = await _organizationRepository.GetAsync(item => item.Code == RootOrganizationCode);
        if (existing != null)
        {
            _logger.LogDebug("根组织已存在，跳过创建: {Code}", RootOrganizationCode);
            return;
        }

        await _organizationRepository.InsertAsync(new OrganizationUnitEntity
        {
            Id = Guid.NewGuid(),
            ParentId = null,
            Code = RootOrganizationCode,
            Name = RootOrganizationName,
            Path = $"/{RootOrganizationCode}",
            Depth = 0,
            SortOrder = 0,
            IsActive = true
        });
        _logger.LogInformation("根组织创建成功: {Name} ({Code})", RootOrganizationName, RootOrganizationCode);
    }
}

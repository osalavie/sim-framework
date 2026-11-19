using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sim.Framework.Domain.Shared.Dependency;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.SystemConfig;

namespace Sim.System.Application.SystemConfig;

/// <summary>
/// 读取当前租户作用域下启用的系统配置项。用于让初始化写入的站点与安全策略即时生效。
/// </summary>
public interface ISystemConfigurationReader
{
    Task<string?> GetValueAsync(string configKey);

    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(params string[] configKeys);
}

public sealed class SystemConfigurationReader(
    ISqlSugarRepository<SystemConfigurationItemEntity> repository) : ISystemConfigurationReader, IScopedDependency
{
    public async Task<string?> GetValueAsync(string configKey)
    {
        var values = await GetValuesAsync(configKey);
        return values.TryGetValue(configKey, out var value) ? value : null;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(params string[] configKeys)
    {
        if (configKeys.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        var tenantScope = SystemConfigurationItemEntity.CurrentTenantScope();
        var items = await repository.GetListAsync(item =>
            item.TenantScope == tenantScope &&
            item.Enabled &&
            configKeys.Contains(item.ConfigKey));

        return items
            .GroupBy(item => item.ConfigKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.OrdinalIgnoreCase);
    }
}

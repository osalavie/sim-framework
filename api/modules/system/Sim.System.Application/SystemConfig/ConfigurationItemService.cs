using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.SystemConfiguration;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.SystemConfig;

namespace Sim.System.Application.SystemConfig;

[Authorize]
[Permission(SystemManagementPermissions.SystemConfigurationManage)]
public sealed class ConfigurationItemService(
    ISqlSugarRepository<SystemConfigurationItemEntity> repository)
    : ApplicationService, ISystemConfigurationItemService
{
    /// <summary>
    /// 获取系统配置项列表，可按分组键筛选。
    /// </summary>
    public async Task<List<SystemConfigurationItemDto>> GetListAsync(
        string? groupKey = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedGroup = string.IsNullOrWhiteSpace(groupKey)
            ? null
            : NormalizeKey(groupKey, maxLength: 64);
        var items = await repository.GetAll();
        return items
            .Where(item => normalizedGroup == null || item.GroupKey == normalizedGroup)
            .OrderBy(item => item.GroupKey)
            .ThenBy(item => item.Sort)
            .ThenBy(item => item.ConfigKey)
            .Select(Map)
            .ToList();
    }

    /// <summary>
    /// 创建系统配置项。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<SystemConfigurationItemDto> CreateAsync(
        CreateSystemConfigurationItemDto input,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(input.GroupName, input.ConfigName, input.Value, input.Sort);
        var groupKey = NormalizeKey(input.GroupKey, maxLength: 64);
        var configKey = NormalizeKey(input.ConfigKey, maxLength: 128);
        var tenantScope = SystemConfigurationItemEntity.CurrentTenantScope();
        var exists = (await repository.GetAll())
            .Any(item => item.TenantScope == tenantScope &&
                item.GroupKey == groupKey &&
                item.ConfigKey == configKey);
        if (exists)
        {
            throw new BusinessException(SystemErrorCodes.SystemConfigurationItemAlreadyExists);
        }

        var entity = new SystemConfigurationItemEntity(Guid.NewGuid())
        {
            TenantScope = tenantScope,
            GroupKey = groupKey,
            GroupName = input.GroupName.Trim(),
            ConfigKey = configKey,
            ConfigName = input.ConfigName.Trim(),
            Value = input.Value.Trim(),
            Description = NormalizeOptional(input.Description),
            Sort = input.Sort,
            Enabled = input.Enabled
        };
        await repository.InsertAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 更新指定的系统配置项。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<SystemConfigurationItemDto> UpdateAsync(
        Guid id,
        UpdateSystemConfigurationItemDto input,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(input.GroupName, input.ConfigName, input.Value, input.Sort);
        var entity = await repository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.SystemConfigurationItemNotFound);
        entity.GroupName = input.GroupName.Trim();
        entity.ConfigName = input.ConfigName.Trim();
        entity.Value = input.Value.Trim();
        entity.Description = NormalizeOptional(input.Description);
        entity.Sort = input.Sort;
        entity.Enabled = input.Enabled;
        await repository.UpdateAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 删除指定的系统配置项。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await repository.DeleteAsync(id);
    }

    private static void ValidateInput(string groupName, string configName, string value, int sort)
    {
        if (string.IsNullOrWhiteSpace(groupName) ||
            string.IsNullOrWhiteSpace(configName) ||
            value == null ||
            sort < 0)
        {
            throw new BusinessException(SystemErrorCodes.InvalidSystemConfigurationItem);
        }
    }

    private static string NormalizeKey(string key, int maxLength)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Length < 1 || normalized.Length > maxLength ||
            normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':')))
        {
            throw new BusinessException(SystemErrorCodes.InvalidSystemConfigurationItem);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SystemConfigurationItemDto Map(SystemConfigurationItemEntity entity) => new()
    {
        Id = entity.Id,
        GroupKey = entity.GroupKey,
        GroupName = entity.GroupName,
        ConfigKey = entity.ConfigKey,
        ConfigName = entity.ConfigName,
        Value = entity.Value,
        Description = entity.Description,
        Sort = entity.Sort,
        Enabled = entity.Enabled
    };
}

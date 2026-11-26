using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.Dictionary;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.Dictionary;

namespace Sim.System.Application.Dictionary;

[Authorize]
[Permission(SystemManagementPermissions.DataDictionaryManage)]
public sealed class DataDictionaryService(
    ISqlSugarRepository<DataDictionaryEntity> repository,
    ISqlSugarRepository<DataDictionaryItemEntity> itemRepository)
    : ApplicationService, IDataDictionaryService
{
    /// <summary>
    /// 获取当前租户的数据字典列表，并返回每个字典的字典项数量。
    /// </summary>
    public async Task<List<DataDictionaryDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var itemCounts = (await itemRepository.GetAll())
            .GroupBy(item => item.DictionaryId)
            .ToDictionary(group => group.Key, group => group.Count());
        return (await repository.GetAll())
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.DictionaryKey)
            .Select(item => Map(item, itemCounts.GetValueOrDefault(item.Id)))
            .ToList();
    }

    /// <summary>
    /// 创建数据字典。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<DataDictionaryDto> CreateAsync(
        CreateDataDictionaryDto input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new BusinessException(SystemErrorCodes.InvalidDataDictionary);
        }

        ValidateInput(input.DisplayName, input.DictionaryKey);
        var dictionaryKey = NormalizeKey(input.DictionaryKey);
        var tenantScope = DataDictionaryEntity.CurrentTenantScope();
        var exists = (await repository.GetAll())
            .Any(item => item.TenantScope == tenantScope && item.DictionaryKey == dictionaryKey);
        if (exists)
        {
            throw new BusinessException(SystemErrorCodes.DataDictionaryAlreadyExists);
        }

        var entity = new DataDictionaryEntity(Guid.NewGuid())
        {
            TenantScope = tenantScope,
            DictionaryKey = dictionaryKey,
            DisplayName = input.DisplayName.Trim(),
            Description = NormalizeOptional(input.Description),
            ValueType = input.ValueType,
            ValuePrefix = NormalizeAffix(input.ValuePrefix),
            ValueSuffix = NormalizeAffix(input.ValueSuffix),
            Enabled = input.Enabled
        };
        await repository.InsertAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 更新指定的数据字典；字典下已有字典项时不允许修改值类型。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<DataDictionaryDto> UpdateAsync(
        Guid id,
        UpdateDataDictionaryDto input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new BusinessException(SystemErrorCodes.InvalidDataDictionary);
        }

        var entity = await repository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.DataDictionaryNotFound);
        ValidateInput(input.DisplayName, entity.DictionaryKey);
        if (input.ValueType != entity.ValueType)
        {
            var hasItems = (await itemRepository.GetAll()).Any(item => item.DictionaryId == id);
            if (hasItems)
            {
                throw new BusinessException(
                    SystemErrorCodes.DataDictionaryValueTypeLocked,
                    "字典下已存在数据，值类型不可修改。");
            }

            entity.ValueType = input.ValueType;
        }

        entity.DisplayName = input.DisplayName.Trim();
        entity.Description = NormalizeOptional(input.Description);
        entity.ValuePrefix = NormalizeAffix(input.ValuePrefix);
        entity.ValueSuffix = NormalizeAffix(input.ValueSuffix);
        entity.Enabled = input.Enabled;

        await repository.UpdateAsync(entity);
        var itemCount = (await itemRepository.GetAll()).Count(item => item.DictionaryId == id);
        return Map(entity, itemCount);
    }

    /// <summary>
    /// 删除指定的数据字典及其全部字典项。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await itemRepository.DeleteAsync(item => item.DictionaryId == id);
        return await repository.DeleteAsync(id);
    }

    private static void ValidateInput(string? displayName, string? dictionaryKey)
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(dictionaryKey))
        {
            throw new BusinessException(SystemErrorCodes.InvalidDataDictionary);
        }
    }

    private static string NormalizeAffix(string? value) => value?.Trim() ?? string.Empty;

    private static string NormalizeKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Length is < 1 or > 128 ||
            normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':')))
        {
            throw new BusinessException(SystemErrorCodes.InvalidDataDictionary);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DataDictionaryDto Map(DataDictionaryEntity entity, int itemCount = 0) => new()
    {
        Id = entity.Id,
        DictionaryKey = entity.DictionaryKey,
        DisplayName = entity.DisplayName,
        Description = entity.Description,
        ValueType = entity.ValueType,
        ValuePrefix = entity.ValuePrefix,
        ValueSuffix = entity.ValueSuffix,
        Enabled = entity.Enabled,
        ItemCount = itemCount
    };
}

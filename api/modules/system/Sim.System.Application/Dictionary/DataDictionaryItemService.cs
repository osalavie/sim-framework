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
public sealed class DataDictionaryItemService(
    ISqlSugarRepository<DataDictionaryItemEntity> repository,
    ISqlSugarRepository<DataDictionaryEntity> dictionaryRepository)
    : ApplicationService, IDataDictionaryItemService
{
    /// <summary>
    /// 获取数据字典项列表，可按字典 ID 筛选。
    /// </summary>
    public async Task<List<DataDictionaryItemDto>> GetListAsync(
        Guid? dictionaryId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = await repository.GetAll();
        return items
            .Where(item => dictionaryId == null || item.DictionaryId == dictionaryId)
            .OrderBy(item => item.DictionaryId)
            .ThenBy(item => item.Sort)
            .ThenBy(item => item.ItemKey)
            .Select(Map)
            .ToList();
    }

    /// <summary>
    /// 创建数据字典项。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<DataDictionaryItemDto> CreateAsync(
        CreateDataDictionaryItemDto input,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(input.ItemName, input.Value, input.Sort);
        var dictionary = await dictionaryRepository.GetAsync(input.DictionaryId)
            ?? throw new BusinessException(SystemErrorCodes.DataDictionaryNotFound);
        var itemKey = NormalizeKey(input.ItemKey);
        var value = DataDictionaryValueValidator.ValidateAndNormalize(dictionary.ValueType, input.Value);
        var tenantScope = DataDictionaryItemEntity.CurrentTenantScope();
        var exists = (await repository.GetAll())
            .Any(item => item.TenantScope == tenantScope &&
                item.DictionaryId == dictionary.Id &&
                item.ItemKey == itemKey);
        if (exists)
        {
            throw new BusinessException(SystemErrorCodes.DataDictionaryItemAlreadyExists);
        }

        var entity = new DataDictionaryItemEntity(Guid.NewGuid())
        {
            TenantScope = tenantScope,
            DictionaryId = dictionary.Id,
            ItemKey = itemKey,
            ItemName = input.ItemName.Trim(),
            Value = value,
            Description = NormalizeOptional(input.Description),
            Sort = input.Sort,
            Enabled = input.Enabled
        };
        await repository.InsertAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 更新指定的数据字典项。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<DataDictionaryItemDto> UpdateAsync(
        Guid id,
        UpdateDataDictionaryItemDto input,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(input.ItemName, input.Value, input.Sort);
        var entity = await repository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.DataDictionaryItemNotFound);
        var dictionary = await dictionaryRepository.GetAsync(entity.DictionaryId)
            ?? throw new BusinessException(SystemErrorCodes.DataDictionaryNotFound);
        entity.ItemName = input.ItemName.Trim();
        entity.Value = DataDictionaryValueValidator.ValidateAndNormalize(dictionary.ValueType, input.Value);
        entity.Description = NormalizeOptional(input.Description);
        entity.Sort = input.Sort;
        entity.Enabled = input.Enabled;
        await repository.UpdateAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 删除指定的数据字典项。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await repository.DeleteAsync(id);
    }

    private static void ValidateInput(string itemName, string value, int sort)
    {
        if (string.IsNullOrWhiteSpace(itemName) ||
            value == null ||
            sort < 0)
        {
            throw new BusinessException(SystemErrorCodes.InvalidDataDictionaryItem);
        }
    }

    private static string NormalizeKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Length is < 1 or > 128 ||
            normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':')))
        {
            throw new BusinessException(SystemErrorCodes.InvalidDataDictionaryItem);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DataDictionaryItemDto Map(DataDictionaryItemEntity entity) => new()
    {
        Id = entity.Id,
        DictionaryId = entity.DictionaryId,
        ItemKey = entity.ItemKey,
        ItemName = entity.ItemName,
        Value = entity.Value,
        Description = entity.Description,
        Sort = entity.Sort,
        Enabled = entity.Enabled
    };
}

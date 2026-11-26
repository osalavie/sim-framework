using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Dictionary;

namespace Sim.System.Application.Dictionary;

/// <summary>读取当前租户作用域下启用的数据字典项。</summary>
public interface IDataDictionaryReader
{
    /// <summary>返回字典项键到显示名称的映射；字典不存在时返回空映射。</summary>
    Task<IReadOnlyDictionary<string, string>> GetItemNamesAsync(string dictionaryKey);
}

public sealed class DataDictionaryReader(
    ISqlSugarRepository<DataDictionaryEntity> dictionaryRepository,
    ISqlSugarRepository<DataDictionaryItemEntity> itemRepository) : IDataDictionaryReader
{
    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyDictionary<string, string>> GetItemNamesAsync(string dictionaryKey)
    {
        var normalizedKey = dictionaryKey.Trim().ToLowerInvariant();
        var tenantScope = DataDictionaryEntity.CurrentTenantScope();
        var dictionary = (await dictionaryRepository.GetListAsync(item =>
            item.TenantScope == tenantScope &&
            item.Enabled &&
            item.DictionaryKey == normalizedKey)).FirstOrDefault();
        if (dictionary == null)
        {
            return Empty;
        }

        var items = await itemRepository.GetListAsync(item =>
            item.TenantScope == tenantScope &&
            item.DictionaryId == dictionary.Id &&
            item.Enabled);
        return items
            .GroupBy(item => item.ItemKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().ItemName,
                StringComparer.OrdinalIgnoreCase);
    }
}

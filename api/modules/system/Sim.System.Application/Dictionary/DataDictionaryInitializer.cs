using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Notifications;
using Sim.System.Domain.Dictionary;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Dictionary;

/// <summary>
/// 幂等补齐系统内置数据字典。管理员修改或调整顺序会被保留，
/// 被删除的系统字典会在下次启动时重新创建。
/// </summary>
public sealed class DataDictionaryInitializer(
    ISqlSugarRepository<DataDictionaryEntity> dictionaryRepository,
    ISqlSugarRepository<DataDictionaryItemEntity> itemRepository,
    ILogger<DataDictionaryInitializer> logger)
{
    private const string HostScope = "host";

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDictionaryAsync(
            DataDictionaryKeys.NotificationProvider,
            "通知服务提供商",
            "通知管理中的服务商显示名称；系统字典，缺失时启动自动补齐。",
            NotificationProviderDefinitions.All
                .Select((definition, index) => new DictionaryItemSeed(
                    definition.Provider,
                    definition.DefaultDisplayName,
                    definition.Provider,
                    (index + 1) * 10))
                .ToArray(),
            cancellationToken);
    }

    private async Task EnsureDictionaryAsync(
        string dictionaryKey,
        string displayName,
        string description,
        IReadOnlyList<DictionaryItemSeed> items,
        CancellationToken cancellationToken)
    {
        var dictionary = await dictionaryRepository.GetAsync(item =>
            item.TenantScope == HostScope && item.DictionaryKey == dictionaryKey);
        if (dictionary == null)
        {
            dictionary = new DataDictionaryEntity(Guid.NewGuid())
            {
                TenantScope = HostScope,
                DictionaryKey = dictionaryKey,
                DisplayName = displayName,
                Description = description,
                ValueType = DataDictionaryValueType.String,
                Enabled = true
            };
            await dictionaryRepository.InsertAsync(dictionary);
            logger.LogInformation("已创建系统数据字典 {DictionaryKey}", dictionaryKey);
        }

        var existingKeys = (await itemRepository.GetListAsync(item =>
                item.TenantScope == HostScope && item.DictionaryId == dictionary.Id))
            .Select(item => item.ItemKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!existingKeys.Add(item.ItemKey))
            {
                continue;
            }

            await itemRepository.InsertAsync(new DataDictionaryItemEntity(Guid.NewGuid())
            {
                TenantScope = HostScope,
                DictionaryId = dictionary.Id,
                ItemKey = item.ItemKey,
                ItemName = item.ItemName,
                Value = item.Value,
                Sort = item.Sort,
                Enabled = true
            });
            created++;
        }

        if (created > 0)
        {
            logger.LogInformation("系统数据字典 {DictionaryKey} 新增 {Count} 项", dictionaryKey, created);
        }
    }

    private sealed record DictionaryItemSeed(
        string ItemKey,
        string ItemName,
        string Value,
        int Sort);
}

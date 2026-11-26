using Sim.Framework.Domain;
using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.System.Domain.Dictionary;

[CodeFirst]
[SugarTable("data_dictionary_item")]
[SugarIndex("uidx_data_dictionary_item_scope_key", nameof(TenantScope), OrderByType.Asc,
    nameof(DictionaryId), OrderByType.Asc, nameof(ItemKey), OrderByType.Asc, true)]
public sealed class DataDictionaryItemEntity : AuditedAggregateRoot
{
    public DataDictionaryItemEntity()
    {
    }

    public DataDictionaryItemEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string TenantScope { get; set; } = "host";

    public Guid DictionaryId { get; set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string ItemKey { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = false)]
    public string ItemName { get; set; } = string.Empty;

    [SugarColumn(Length = 1024, IsNullable = false)]
    public string Value { get; set; } = string.Empty;

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Description { get; set; }

    public int Sort { get; set; }

    public bool Enabled { get; set; } = true;

    public static string CurrentTenantScope() => TenantContext.CurrentTenantId?.ToString("N") ?? "host";
}

using Sim.Framework.Domain;
using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Domain.Dictionary;

[CodeFirst]
[SugarTable("data_dictionary")]
[SugarIndex("uidx_data_dictionary_scope_key", nameof(TenantScope), OrderByType.Asc,
    nameof(DictionaryKey), OrderByType.Asc, true)]
public sealed class DataDictionaryEntity : AuditedAggregateRoot
{
    public DataDictionaryEntity()
    {
    }

    public DataDictionaryEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string TenantScope { get; set; } = "host";

    [SugarColumn(Length = 128, IsNullable = false)]
    public string DictionaryKey { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = false)]
    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Description { get; set; }

    public DataDictionaryValueType ValueType { get; set; } = DataDictionaryValueType.String;

    [SugarColumn(Length = 32, IsNullable = false)]
    public string ValuePrefix { get; set; } = string.Empty;

    [SugarColumn(Length = 32, IsNullable = false)]
    public string ValueSuffix { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public static string CurrentTenantScope() => TenantContext.CurrentTenantId?.ToString("N") ?? "host";
}

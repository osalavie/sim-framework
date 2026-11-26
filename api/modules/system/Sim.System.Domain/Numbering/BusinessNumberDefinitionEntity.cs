using Sim.Framework.Domain;
using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.System.Domain.Numbering;

[CodeFirst]
[SugarTable("system_business_number_definition")]
[SugarIndex("uidx_system_business_number_definition_scope", nameof(TenantScope), OrderByType.Asc,
    nameof(BusinessKey), OrderByType.Asc, true)]
public sealed class BusinessNumberDefinitionEntity : AuditedAggregateRoot
{
    public BusinessNumberDefinitionEntity()
    {
    }

    public BusinessNumberDefinitionEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string TenantScope { get; set; } = "host";

    [SugarColumn(Length = 128, IsNullable = false)]
    public string BusinessKey { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = false)]
    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(Length = 512, IsNullable = false)]
    public string Format { get; set; } = "{sequence:D6}";

    public long InitialSequence { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public static string CurrentTenantScope() => TenantContext.CurrentTenantId?.ToString("N") ?? "host";
}

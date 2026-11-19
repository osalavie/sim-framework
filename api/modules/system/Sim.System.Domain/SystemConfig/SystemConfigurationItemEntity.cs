using Sim.Framework.Domain;
using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.System.Domain.SystemConfig;

[CodeFirst]
[SugarTable("system_configuration_item")]
[SugarIndex("uidx_system_configuration_item_scope", nameof(TenantScope), OrderByType.Asc,
    nameof(GroupKey), OrderByType.Asc, nameof(ConfigKey), OrderByType.Asc, true)]
public sealed class SystemConfigurationItemEntity : AuditedAggregateRoot
{
    public SystemConfigurationItemEntity()
    {
    }

    public SystemConfigurationItemEntity(Guid id) : base(id)
    {
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string TenantScope { get; set; } = "host";

    [SugarColumn(Length = 64, IsNullable = false)]
    public string GroupKey { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = false)]
    public string GroupName { get; set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = false)]
    public string ConfigKey { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = false)]
    public string ConfigName { get; set; } = string.Empty;

    [SugarColumn(Length = 1024, IsNullable = false)]
    public string Value { get; set; } = string.Empty;

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Description { get; set; }

    public int Sort { get; set; }

    public bool Enabled { get; set; } = true;

    public static string CurrentTenantScope() => TenantContext.CurrentTenantId?.ToString("N") ?? "host";
}

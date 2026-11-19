using Sim.System.Domain.Shared;
using Sim.Framework.Domain.Entities;
using SqlSugar;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Domain.Permission;

[SugarTable("system_permission")]
public class PermissionEntity : AuditedAggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public PermissionTypeEnum PermissionType { get; set; }
    public string? Description { get; set; }
}
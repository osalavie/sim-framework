using Sim.Framework.Domain.Entities;
using SqlSugar;

namespace Sim.System.Domain.Role;

[SugarTable("system_role")]
public class RoleEntity : AuditedAggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    [SugarColumn(DefaultValue = "false")]
    public bool IsBuiltIn { get; set; }
}
using Sim.Framework.Domain.Entities;
using SqlSugar;

namespace Sim.System.Domain.Role;

[SugarTable("system_role_permission")]
public class RolePermission : AuditedEntity
{
    public Guid PermissionId { get; set; }
    public Guid RoleId { get; set; }
}
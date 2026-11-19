using Sim.Framework.Domain.Entities;
using SqlSugar;

namespace Sim.System.Domain.User;

[SugarTable("system_user_role")]
public class UserRole : AuditedEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}
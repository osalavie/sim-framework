namespace Sim.System.Application.Contracts.RolePermission;

public class CreateRolePermissionDto
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}
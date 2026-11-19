namespace Sim.System.Application.Contracts.MenuPermission;

public class CreateMenuPermissionDto
{
    public Guid MenuId { get; set; }
    public Guid PermissionId { get; set; }
}
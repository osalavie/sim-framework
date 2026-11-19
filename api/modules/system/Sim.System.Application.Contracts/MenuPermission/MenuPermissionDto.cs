namespace Sim.System.Application.Contracts.MenuPermission;

public class MenuPermissionDto
{
    public Guid Id { get; set; }
    public Guid MenuId { get; set; }
    public Guid PermissionId { get; set; }
}
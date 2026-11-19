using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Contracts.Permission;

public class CreatePermissionDto
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required PermissionTypeEnum PermissionType { get; set; }
    public string? Description { get; set; }
}
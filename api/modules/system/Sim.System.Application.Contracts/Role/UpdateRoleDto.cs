using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.System.Application.Contracts.Role;

public class UpdateRoleDto: EntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}
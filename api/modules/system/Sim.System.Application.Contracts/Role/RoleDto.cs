using System;
using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.System.Application.Contracts.Role;

public class RoleDto: AuditedEntityDto<Guid>
{
    public new Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsBuiltIn { get; set; }
}

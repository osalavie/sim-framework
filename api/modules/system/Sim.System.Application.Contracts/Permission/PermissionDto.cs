using Sim.Framework.Application.Contracts.Dtos;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Contracts.Permission;

public class PermissionDto: AuditedEntityDto<Guid>
{
    public new Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public PermissionTypeEnum PermissionType { get; set; }
    public string? Description { get; set; }
}

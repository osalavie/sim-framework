using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.Role;

public interface IRoleService : ICustomCrudAppService<Guid, RoleDto, CreateRoleDto, UpdateRoleDto>
{
    Task AssignRolePermissionsAsync(Guid roleId, List<Guid> permissionIds);
}
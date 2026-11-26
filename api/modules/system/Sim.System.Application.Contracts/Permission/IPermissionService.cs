using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.Permission;

public interface IPermissionService : ICustomCrudAppService<Guid, PermissionDto, CreatePermissionDto, UpdatePermissionDto>
{
    Task<List<PermissionDto>> GetPermissionsByRoleAsync(Guid roleId);
}
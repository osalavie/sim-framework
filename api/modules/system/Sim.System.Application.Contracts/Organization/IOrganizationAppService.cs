using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.Organization;

public interface IOrganizationAppService : IApplicationService
{
    Task<OrganizationUnitDto> CreateAsync(CreateOrganizationUnitDto input, CancellationToken cancellationToken = default);
    Task<OrganizationUnitDto> UpdateAsync(Guid id, UpdateOrganizationUnitDto input, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrganizationUnitDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<OrganizationUnitDto>> GetTreeAsync(Guid? rootId = null, CancellationToken cancellationToken = default);
    Task<List<OrganizationUnitDto>> GetDescendantsAsync(Guid rootId, CancellationToken cancellationToken = default);

    Task<OrganizationMemberDto> AddMemberAsync(AddMemberDto input, CancellationToken cancellationToken = default);
    Task<bool> RemoveMemberAsync(Guid membershipId, CancellationToken cancellationToken = default);
    Task<List<OrganizationMemberDto>> GetMembersAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<List<OrganizationMemberDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<RoleDataScopeDto> SetRoleDataScopeAsync(SetRoleDataScopeDto input, CancellationToken cancellationToken = default);
    Task<List<RoleDataScopeDto>> GetRoleDataScopesAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRoleDataScopeAsync(Guid id, CancellationToken cancellationToken = default);
}

using Sim.System.Application.Contracts.Organization;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Organization;
using Sim.System.Domain.Role;
using Sim.System.Domain.User;

namespace Sim.System.Application.Organization;

/// <summary>
/// <see cref="IOrganizationDirectory"/> 的默认实现：基于组织单位物化路径与成员表解析负责人链，
/// 基于角色与用户角色关联解析角色成员。
/// </summary>
internal sealed class OrganizationDirectory(
    ISqlSugarRepository<OrganizationUnitEntity> organizationRepository,
    ISqlSugarRepository<OrganizationMembershipEntity> membershipRepository,
    IRoleRepository roleRepository,
    IUserRoleRepository userRoleRepository,
    IUserRepository userRepository) : IOrganizationDirectory
{
    public async Task<Guid?> FindPrimaryOrganizationUnitIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var primary = await membershipRepository.GetAsync(item => item.UserId == userId && item.IsPrimary);
        if (primary is not null)
        {
            return primary.OrganizationUnitId;
        }

        var fallback = await membershipRepository.GetAsync(item => item.UserId == userId);
        return fallback?.OrganizationUnitId;
    }

    public async Task<IReadOnlyList<Guid>> FindManagerChainAsync(
        Guid userId,
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default)
    {
        var currentId = organizationUnitId ?? await FindPrimaryOrganizationUnitIdAsync(userId, cancellationToken);
        var managers = new List<Guid>();

        while (currentId is not null)
        {
            var unit = await organizationRepository.GetAsync((object)currentId.Value);
            if (unit is null)
            {
                break;
            }

            if (unit.ManagerUserId is { } manager && manager != userId && !managers.Contains(manager))
            {
                managers.Add(manager);
            }

            currentId = unit.ParentId;
        }

        return managers;
    }

    public async Task<Guid?> FindOrganizationManagerAsync(
        Guid organizationUnitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await organizationRepository.GetAsync((object)organizationUnitId);
        if (unit?.ManagerUserId is not { } manager)
        {
            return null;
        }

        return await userRepository.GetAsync(user => user.Id == manager && user.IsActive) is not null
            ? manager
            : null;
    }

    public async Task<IReadOnlyList<Guid>> FindDirectMemberUserIdsAsync(
        Guid organizationUnitId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await membershipRepository.GetListAsync(
            item => item.OrganizationUnitId == organizationUnitId);
        var userIds = memberships.Select(item => item.UserId).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return [];
        }

        var activeUsers = await userRepository.GetListAsync(user => userIds.Contains(user.Id) && user.IsActive);
        return activeUsers.Select(user => user.Id).ToList();
    }

    public async Task<IReadOnlyList<OrganizationDirectoryUnit>> ListOrganizationUnitsAsync(
        CancellationToken cancellationToken = default)
    {
        var units = await organizationRepository.GetListAsync(item => item.IsActive);
        return units
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .Select(item => new OrganizationDirectoryUnit(item.Id, item.Name, item.ParentId))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> FindUserIdsByRoleNameAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return [];
        }

        var normalized = roleName.Trim();
        var role = await roleRepository.GetAsync(item => item.Code == normalized)
                   ?? await roleRepository.GetAsync(item => item.Name == normalized);
        if (role is null)
        {
            return [];
        }

        var links = await userRoleRepository.GetListAsync(item => item.RoleId == role.Id);
        var userIds = links.Select(item => item.UserId).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return [];
        }

        var activeUsers = await userRepository.GetListAsync(user => userIds.Contains(user.Id) && user.IsActive);
        return activeUsers.Select(user => user.Id).ToList();
    }

    public async Task<bool> IsActiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (userId == Guid.Empty)
        {
            return false;
        }

        return await userRepository.GetAsync(user => user.Id == userId && user.IsActive) is not null;
    }

    public async Task<IReadOnlyList<OrganizationDirectoryUser>> SearchActiveUsersAsync(
        string? keyword,
        int maxResultCount = 20,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var normalized = keyword?.Trim();
        var query = userRepository.GetQueryable().Where(user => user.IsActive);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(user =>
                user.UserName.Contains(normalized)
                || (user.NickName != null && user.NickName.Contains(normalized))
                || (user.Name != null && user.Name.Contains(normalized)));
        }

        var users = await query
            .OrderBy(user => user.UserName)
            .Take(Math.Clamp(maxResultCount, 1, 50))
            .ToListAsync();
        return users
            .Select(user => new OrganizationDirectoryUser(
                user.Id,
                user.UserName,
                user.NickName ?? user.Name ?? user.UserName))
            .ToList();
    }
}

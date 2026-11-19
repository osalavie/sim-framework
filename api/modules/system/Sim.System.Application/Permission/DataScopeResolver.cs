using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Application.Contracts.Organization;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Organization;

namespace Sim.System.Application.Permission;

/// <summary>
/// Resolves data scope decisions by examining the current user's roles,
/// organization memberships, and configured role-data-scope mappings.
/// </summary>
#pragma warning disable CS9113 // Primary constructor parameter reserved for future use
public sealed class DataScopeResolver(
    ISqlSugarRepository<OrganizationMembershipEntity> memberRepository,
    ISqlSugarRepository<OrganizationUnitEntity> orgRepository,
    ISqlSugarRepository<RoleDataScopeEntity> scopeRepository,
    ICurrentUser currentUser,
    ILogger<DataScopeResolver> logger) : IDataScopeResolver
#pragma warning restore CS9113
{
    public async Task<DataScopeDecision> ResolveAsync(
        Guid userId,
        string resource,
        CancellationToken cancellationToken = default)
    {
        // 只有宿主内置角色可以绕过数据过滤；租户内置角色不具备全局数据范围。
        var isHostBuiltIn = !currentUser.FindClaims(TokenTypeConst.TenantId).Any()
                            && currentUser.FindClaims(TokenTypeConst.IsBuiltInRole)
                                .Any(claim => string.Equals(claim.Value, bool.TrueString, StringComparison.OrdinalIgnoreCase));
        if (isHostBuiltIn)
        {
            return new DataScopeDecision
            {
                ScopeType = DataScopeType.All,
                IsUnrestricted = true
            };
        }

        // Find role-data-scope mappings for this user's roles and resource
        var roleIds = GetRoleIdsFromClaims();
        if (roleIds.Count == 0)
        {
            // No roles configured → default to Self scope
            return new DataScopeDecision
            {
                ScopeType = DataScopeType.Self,
                UserId = userId
            };
        }

        // Get scope configurations for this resource
        var scopeConfigs = await scopeRepository.GetListAsync(
            s => roleIds.Contains(s.RoleId) && s.Resource == resource);

        if (scopeConfigs.Count == 0)
        {
            // No specific scope configured → default to Self
            return new DataScopeDecision
            {
                ScopeType = DataScopeType.Self,
                UserId = userId
            };
        }

        // Take the most permissive scope across all matching roles
        var mostPermissive = scopeConfigs
            .Select(s => ParseScopeType(s.ScopeType))
            .OrderByDescending(t => (int)t)
            .First();

        if (mostPermissive == DataScopeType.All)
        {
            return new DataScopeDecision
            {
                ScopeType = DataScopeType.All,
                IsUnrestricted = true
            };
        }

        if (mostPermissive == DataScopeType.None)
        {
            return new DataScopeDecision { ScopeType = DataScopeType.None };
        }

        if (mostPermissive == DataScopeType.Self)
        {
            return new DataScopeDecision
            {
                ScopeType = DataScopeType.Self,
                UserId = userId
            };
        }

        // For organization-based scopes, get user's organizations
        var memberships = await memberRepository.GetListAsync(m => m.UserId == userId);
        var orgIds = memberships.Select(m => m.OrganizationUnitId).Distinct().ToList();

        if (orgIds.Count == 0)
        {
            // User has no org memberships → fall back to Self
            return new DataScopeDecision
            {
                ScopeType = DataScopeType.Self,
                UserId = userId
            };
        }

        // Get organization paths for descendant queries
        var orgs = await orgRepository.GetListAsync(o => orgIds.Contains(o.Id));
        var paths = orgs.Select(o => o.Path).Distinct().ToList();

        // For Custom scope, merge with custom org IDs from scope configs
        if (mostPermissive == DataScopeType.Custom)
        {
            var customOrgIds = scopeConfigs
                .Where(s => ParseScopeType(s.ScopeType) == DataScopeType.Custom && !string.IsNullOrEmpty(s.CustomOrganizationIds))
                .SelectMany(s => s.CustomOrganizationIds!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(Guid.Parse)
                .Distinct()
                .ToList();

            if (customOrgIds.Count > 0)
            {
                var customOrgs = await orgRepository.GetListAsync(o => customOrgIds.Contains(o.Id));
                paths = paths.Concat(customOrgs.Select(o => o.Path)).Distinct().ToList();
                orgIds = orgIds.Concat(customOrgIds).Distinct().ToList();
            }
        }

        return new DataScopeDecision
        {
            ScopeType = mostPermissive,
            OrganizationAnchors = orgIds,
            OrganizationPaths = paths,
            UserId = userId
        };
    }

    private List<Guid> GetRoleIdsFromClaims()
    {
        // Role IDs are not directly in JWT claims (we use role codes).
        // For now, return empty — the scope resolver will use role codes
        // to look up scope configurations when role IDs are available.
        // This is a placeholder for future enhancement.
        return [];
    }

    private static DataScopeType ParseScopeType(string scopeType) =>
        Enum.TryParse<DataScopeType>(scopeType, ignoreCase: true, out var result)
            ? result
            : DataScopeType.Self;
}

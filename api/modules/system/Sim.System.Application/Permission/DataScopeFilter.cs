using Sim.System.Application.Contracts.Organization;
using Sim.System.Domain.Shared;
using SqlSugar;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Permission;

/// <summary>
/// Applies data scope filtering to SqlSugar queryables for entities that implement IOrganizationOwned.
/// </summary>
public sealed class DataScopeFilter : IDataScopeFilter
{
    public ISugarQueryable<TEntity> Apply<TEntity>(
        ISugarQueryable<TEntity> queryable,
        DataScopeDecision decision)
        where TEntity : class, IOrganizationOwned
    {
        if (decision.IsUnrestricted || decision.ScopeType == DataScopeType.All)
        {
            return queryable;
        }

        if (decision.ScopeType == DataScopeType.None)
        {
            return queryable.Where("1 = 0");
        }

        if (decision.ScopeType == DataScopeType.Self)
        {
            if (decision.UserId == null)
                return queryable.Where("1 = 0");

            return queryable.Where("OwnerUserId = @userId", new { userId = decision.UserId });
        }

        // Organization-based scopes
        var orgIds = decision.OrganizationAnchors;
        if (orgIds.Count == 0)
        {
            if (decision.UserId != null)
                return queryable.Where("OwnerUserId = @userId", new { userId = decision.UserId });
            return queryable.Where("1 = 0");
        }

        if (decision.ScopeType == DataScopeType.OrganizationOnly ||
            decision.ScopeType == DataScopeType.Custom)
        {
            return queryable.Where("OrganizationUnitId IN @orgIds", new { orgIds });
        }

        if (decision.ScopeType == DataScopeType.OrganizationAndDescendants)
        {
            // Use materialized path: find orgs whose Path starts with any anchor path,
            // then filter records by those org IDs.
            // Simplified approach: use the first anchor path as a prefix.
            // For multiple anchors, the resolver should pre-compute descendant org IDs.
            var paths = decision.OrganizationPaths;
            if (paths.Count > 0)
            {
                return queryable.Where(
                    "OrganizationUnitId IN (SELECT Id FROM system_organization_unit WHERE Id IN @orgIds OR Path LIKE @pathPattern)",
                    new { orgIds, pathPattern = paths[0] + "%" });
            }

            return queryable.Where("OrganizationUnitId IN @orgIds", new { orgIds });
        }

        return queryable;
    }
}

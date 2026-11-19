using SqlSugar;
using Sim.System.Application.Contracts.Organization;
using Sim.System.Domain.Shared;

namespace Sim.System.Application.Permission;

/// <summary>
/// Applies a data scope decision to a queryable, filtering records by organization ownership.
/// Entities must implement <see cref="IOrganizationOwned"/>.
/// </summary>
public interface IDataScopeFilter
{
    ISugarQueryable<TEntity> Apply<TEntity>(
        ISugarQueryable<TEntity> queryable,
        DataScopeDecision decision)
        where TEntity : class, IOrganizationOwned;
}

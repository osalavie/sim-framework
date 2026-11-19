using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.RouteMeta;

/// <summary>
/// Route metadata repository interface
/// </summary>
public interface IRouteMetaRepository : IRepository<RouteMetaEntity>, ISqlSugarRepository<RouteMetaEntity>
{
    /// <summary>
    /// Get metadata by route configuration ID
    /// </summary>
    Task<RouteMetaEntity?> GetByRouteConfigIdAsync(Guid routeConfigId);
    
    /// <summary>
    /// Get metadata by route configuration IDs (batch query)
    /// </summary>
    Task<List<RouteMetaEntity>> GetByRouteConfigIdsAsync(List<Guid> routeConfigIds);
}

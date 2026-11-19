using Microsoft.Extensions.Logging;
using SqlSugar;
using Sim.System.Domain.RouteMeta;

namespace Sim.System.SqlSugar.Repositories.RouteMeta;

/// <summary>
/// Route metadata repository implementation with error handling
/// </summary>
public class RouteMetaRepository : SystemRepositoryBase<RouteMetaEntity>, IRouteMetaRepository
{
    public RouteMetaRepository(ISqlSugarClient db, ILogger<RouteMetaRepository> logger)
        : base(db, logger)
    {

    }

    /// <summary>
    /// Get metadata by route configuration ID (with error handling)
    /// </summary>
    public async Task<RouteMetaEntity?> GetByRouteConfigIdAsync(Guid routeConfigId)
    {
        return await ExecuteWithErrorHandlingAsync(
            async () => await GetAsync(m => m.RouteConfigId == routeConfigId),
            "retrieving route metadata by route config ID"
        );
    }
    
    /// <summary>
    /// Get metadata by route configuration IDs - batch query (with error handling)
    /// </summary>
    public async Task<List<RouteMetaEntity>> GetByRouteConfigIdsAsync(List<Guid> routeConfigIds)
    {
        return await ExecuteWithErrorHandlingAsync(
            async () => await GetQueryable()
                .Where(m => routeConfigIds.Contains(m.RouteConfigId))
                .ToListAsync(),
            "retrieving route metadata by route config IDs"
        );
    }
}

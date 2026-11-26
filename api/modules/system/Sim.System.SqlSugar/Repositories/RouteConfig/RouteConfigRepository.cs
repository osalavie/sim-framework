using Microsoft.Extensions.Logging;
using SqlSugar;
using Sim.System.Domain.RouteConfig;

namespace Sim.System.SqlSugar.Repositories.RouteConfig;

/// <summary>
/// Route configuration repository implementation with error handling
/// </summary>
public class RouteConfigRepository : SystemRepositoryBase<RouteConfigEntity>, IRouteConfigRepository
{
    public RouteConfigRepository(
        ISqlSugarClient db,
        ILogger<RouteConfigRepository> logger)
        : base(db, logger)
    {
    }
    
    /// <summary>
    /// Get routes by parent ID (with error handling)
    /// </summary>
    public async Task<List<RouteConfigEntity>> GetRoutesByParentIdAsync(Guid? parentId)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            if (parentId == null)
            {
                return await GetListAsync(r => r.ParentId == null);
            }
            return await GetListAsync(r => r.ParentId == parentId);
        }, "retrieving routes by parent ID");
    }
    
    /// <summary>
    /// Check if a route name already exists (with error handling)
    /// </summary>
    public async Task<bool> CheckNameExistsAsync(string name, Guid? excludeId = null)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var query = GetQueryable().Where(r => r.Name == name);
            
            if (excludeId.HasValue)
            {
                query = query.Where(r => r.Id != excludeId.Value);
            }
            
            return await query.AnyAsync();
        }, "checking route name existence");
    }
    
    /// <summary>
    /// Get all descendant routes recursively (with error handling)
    /// </summary>
    public async Task<List<RouteConfigEntity>> GetDescendantsAsync(Guid routeId)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var descendants = new List<RouteConfigEntity>();
            var queue = new Queue<Guid>();
            queue.Enqueue(routeId);
            
            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var children = await GetListAsync(r => r.ParentId == currentId);
                
                foreach (var child in children)
                {
                    descendants.Add(child);
                    queue.Enqueue(child.Id);
                }
            }
            
            return descendants;
        }, "retrieving descendant routes");
    }
    
    /// <summary>
    /// Check if setting a new parent would create a circular reference (with error handling)
    /// </summary>
    public async Task<bool> CheckCircularReferenceAsync(Guid routeId, Guid? newParentId)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            if (!newParentId.HasValue)
            {
                return false;
            }
            
            // Check if the new parent is the route itself
            if (newParentId.Value == routeId)
            {
                return true;
            }
            
            // Check if new parent is a descendant of the current route
            var descendants = await GetDescendantsAsync(routeId);
            return descendants.Any(d => d.Id == newParentId.Value);
        }, "checking circular reference");
    }
}

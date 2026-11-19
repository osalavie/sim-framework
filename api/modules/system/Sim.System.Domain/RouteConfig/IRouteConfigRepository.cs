using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.RouteConfig;

/// <summary>
/// Route configuration repository interface
/// </summary>
public interface IRouteConfigRepository : IRepository<RouteConfigEntity>, ISqlSugarRepository<RouteConfigEntity>
{
    /// <summary>
    /// Get routes by parent ID
    /// </summary>
    Task<List<RouteConfigEntity>> GetRoutesByParentIdAsync(Guid? parentId);
    
    /// <summary>
    /// Check if a route name already exists
    /// </summary>
    /// <param name="name">Route name to check</param>
    /// <param name="excludeId">Optional route ID to exclude from the check (for updates)</param>
    /// <returns>True if name exists, false otherwise</returns>
    Task<bool> CheckNameExistsAsync(string name, Guid? excludeId = null);
    
    /// <summary>
    /// Get all descendant routes recursively
    /// </summary>
    /// <param name="routeId">Parent route ID</param>
    /// <returns>List of all descendant routes</returns>
    Task<List<RouteConfigEntity>> GetDescendantsAsync(Guid routeId);
    
    /// <summary>
    /// Check if setting a new parent would create a circular reference
    /// </summary>
    /// <param name="routeId">Route ID being updated</param>
    /// <param name="newParentId">New parent ID to check</param>
    /// <returns>True if circular reference would be created, false otherwise</returns>
    Task<bool> CheckCircularReferenceAsync(Guid routeId, Guid? newParentId);
}

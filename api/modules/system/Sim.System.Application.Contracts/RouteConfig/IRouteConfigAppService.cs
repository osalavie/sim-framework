using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Contracts.IServices;
using Sim.System.Application.Contracts.RouteConfig;

namespace Sim.System.Application.Contracts.RouteConfig;

/// <summary>
/// Route configuration application service interface
/// ABP will automatically expose this as HTTP API:
/// GET /api/app/route-config/module-routes/{moduleId}
/// POST /api/app/route-config
/// PUT /api/app/route-config/{id}
/// DELETE /api/app/route-config/{id}
/// GET /api/app/route-config/{id}
/// </summary>
public interface IRouteConfigAppService : IApplicationService
{
    /// <summary>
    /// Get routes for a specific module
    /// </summary>
    /// <param name="moduleId">Module identifier</param>
    /// <returns>List of route configurations</returns>
    Task<List<RouteConfigDto>> GetModuleRoutesAsync(Guid moduleId);
    
    /// <summary>
    /// Create a new route configuration
    /// </summary>
    /// <param name="input">Route creation data</param>
    /// <returns>Created route configuration</returns>
    Task<RouteConfigDto> CreateAsync(CreateRouteConfigDto input);
    
    /// <summary>
    /// Update an existing route configuration
    /// </summary>
    /// <param name="input">Route update data (includes Id)</param>
    /// <returns>Updated route configuration</returns>
    Task<RouteConfigDto> UpdateAsync(UpdateRouteConfigDto input);
    
    /// <summary>
    /// Delete a route configuration
    /// </summary>
    /// <param name="id">Route identifier</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteAsync(Guid id);
    
    /// <summary>
    /// Get a single route configuration by ID
    /// </summary>
    /// <param name="id">Route identifier</param>
    /// <returns>Route configuration</returns>
    Task<RouteConfigDto> GetAsync(Guid id);
    
    /// <summary>
    /// Get the current user's accessible modules and one global route tree.
    /// Routes from all accessible modules are assembled by the global route catalog,
    /// so parents living in other modules are preserved.
    /// </summary>
    /// <returns>Accessible modules plus the permission-filtered global route tree</returns>
    Task<UserMenuDto> GetUserAccessibleRoutesAsync();
    
    /// <summary>
    /// Get all routes with metadata for the current user (non-paginated)
    /// If user has Admin role, returns all routes; otherwise returns only routes from user's accessible modules
    /// </summary>
    /// <returns>Tree structure of all accessible routes with metadata</returns>
    Task<List<RouteConfigDto>> GetRoutesWithMetaAsync();
    
    /// <summary>
    /// Get paginated routes with metadata for the current user (tree structure with pagination on root nodes)
    /// If user has Admin role, returns all routes; otherwise returns only routes from user's accessible modules
    /// Pagination only applies to root routes, their children are always included
    /// </summary>
    /// <param name="input">Pagination, sorting, module and keyword filter parameters</param>
    /// <returns>Paginated tree structure of accessible routes with metadata</returns>
    Task<PagedResultDto<RouteConfigDto>> GetRoutesWithMetaPagedAsync(RouteConfigPagedRequestDto input);
}

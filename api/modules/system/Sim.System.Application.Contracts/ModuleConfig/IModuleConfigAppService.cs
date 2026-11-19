using Sim.Framework.Application.Contracts.IServices;
using Sim.System.Application.Contracts.Dtos;

namespace Sim.System.Application.Contracts.ModuleConfig;

/// <summary>
/// Module configuration application service interface
/// ABP will automatically expose this as HTTP API:
/// GET /api/app/module-config/user-modules
/// POST /api/app/module-config
/// PUT /api/app/module-config/{id}
/// DELETE /api/app/module-config/{id}
/// GET /api/app/module-config/{id}
/// </summary>
public interface IModuleConfigAppService : IApplicationService
{
    /// <summary>
    /// Get modules accessible by the current user
    /// </summary>
    /// <returns>List of module configurations</returns>
    Task<List<ModuleConfigDto>> GetUserModulesAsync();
    
    /// <summary>
    /// Create a new module configuration
    /// </summary>
    /// <param name="input">Module creation data</param>
    /// <returns>Created module configuration</returns>
    Task<ModuleConfigDto> CreateAsync(CreateModuleConfigDto input);
    
    /// <summary>
    /// Update an existing module configuration
    /// </summary>
    /// <param name="id">Module identifier</param>
    /// <param name="input">Module update data</param>
    /// <returns>Updated module configuration</returns>
    Task<ModuleConfigDto> UpdateAsync(Guid id, UpdateModuleConfigDto input);
    
    /// <summary>
    /// Delete a module configuration
    /// </summary>
    /// <param name="id">Module identifier</param>
    Task<bool> DeleteAsync(Guid id);
    
    /// <summary>
    /// Get a single module configuration by ID
    /// </summary>
    /// <param name="id">Module identifier</param>
    /// <returns>Module configuration</returns>
    Task<ModuleConfigDto> GetAsync(Guid id);
}

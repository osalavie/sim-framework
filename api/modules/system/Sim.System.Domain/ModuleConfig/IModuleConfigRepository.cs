using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.ModuleConfig;

/// <summary>
/// Module configuration repository interface
/// </summary>
public interface IModuleConfigRepository : IRepository<ModuleConfigEntity>, ISqlSugarRepository<ModuleConfigEntity>
{
    /// <summary>
    /// Get all enabled modules
    /// </summary>
    Task<List<ModuleConfigEntity>> GetEnabledModulesAsync();
    
    /// <summary>
    /// Get modules by IDs
    /// </summary>
    Task<List<ModuleConfigEntity>> GetModulesByIdsAsync(List<Guid> ids);
    
    /// <summary>
    /// Check if a module name already exists
    /// </summary>
    /// <param name="name">Module name to check</param>
    /// <param name="excludeId">Optional module ID to exclude from the check (for updates)</param>
    /// <returns>True if name exists, false otherwise</returns>
    Task<bool> CheckNameExistsAsync(string name, Guid? excludeId = null);
}

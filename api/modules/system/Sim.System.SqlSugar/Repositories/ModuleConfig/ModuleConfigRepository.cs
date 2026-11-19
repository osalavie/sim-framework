using Microsoft.Extensions.Logging;
using SqlSugar;
using Sim.System.Domain.ModuleConfig;

namespace Sim.System.SqlSugar.Repositories.ModuleConfig;

/// <summary>
/// Module configuration repository implementation with error handling
/// </summary>
public class ModuleConfigRepository : SystemRepositoryBase<ModuleConfigEntity>, IModuleConfigRepository
{
    public ModuleConfigRepository(ISqlSugarClient db, ILogger<ModuleConfigRepository> logger)
        : base(db, logger)
    {
    }

    /// <summary>
    /// Get all enabled modules with error handling
    /// </summary>
    public async Task<List<ModuleConfigEntity>> GetEnabledModulesAsync()
    {
        return await ExecuteWithErrorHandlingAsync(
            async () => await GetListAsync(m => m.Enabled),
            "retrieving enabled modules"
        );
    }
    
    /// <summary>
    /// Get modules by IDs with error handling
    /// </summary>
    public async Task<List<ModuleConfigEntity>> GetModulesByIdsAsync(List<Guid> ids)
    {
        return await ExecuteWithErrorHandlingAsync(
            async () => await GetQueryable()
                .Where(m => ids.Contains(m.Id))
                .ToListAsync(),
            "retrieving modules by IDs"
        );
    }
    
    /// <summary>
    /// Check if a module name already exists with error handling
    /// </summary>
    public async Task<bool> CheckNameExistsAsync(string name, Guid? excludeId = null)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var query = GetQueryable().Where(m => m.Name == name);
            
            if (excludeId.HasValue)
            {
                query = query.Where(m => m.Id != excludeId.Value);
            }
            
            return await query.AnyAsync();
        }, "checking module name existence");
    }
}

using Microsoft.Extensions.Logging;
using SqlSugar;
using Sim.System.Domain.ModuleConfig;

namespace Sim.System.SqlSugar.Repositories.ModuleConfig;

public class ModuleRouteRepository(ISqlSugarClient db, ILogger<ModuleRouteRepository> logger)
    : SystemRepositoryBase<ModuleRouteEntity>(db, logger), IModuleRouteRepository
{
}

using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.ModuleConfig;

public interface IModuleRouteRepository : IRepository<ModuleRouteEntity>, ISqlSugarRepository<ModuleRouteEntity>
{
}

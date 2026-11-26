using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared.Consts;
using SqlSugar;

namespace Sim.System.Domain.ModuleConfig;

/// <summary>
/// 模块与路由的多对多关联，表示模块实际下发的路由集合，是两者关系的唯一事实来源。
/// </summary>
[SugarTable(SystemConsts.DbTablePrefix + "module_route")]
[SugarIndex(
    $"idx_{SystemConsts.DbTablePrefix}module_route_module_route",
    nameof(ModuleId),
    OrderByType.Asc,
    nameof(RouteId),
    OrderByType.Asc,
    true)]
public class ModuleRouteEntity : AuditedEntity
{
    public Guid ModuleId { get; set; }

    public Guid RouteId { get; set; }
}

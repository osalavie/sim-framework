using Sim.SqlSugar.Core.Attributes;
using Sim.System.Domain.Shared;
using SqlSugar;
using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.RouteMeta;

namespace Sim.System.Domain.RouteConfig;

/// <summary>
/// 路由配置实体（RouteConfig 就是菜单，只是从技术角度命名）
/// Route configuration entity (This is essentially a "Menu", named from a technical perspective)
/// 
/// 说明：
/// - 在代码中称为 RouteConfig，便于技术人员理解其本质是前端路由配置
/// - 在用户界面显示为"菜单"，便于非技术用户理解
/// - 路由是全局目录，通过 ModuleRoute 与模块建立多对多关联
/// - 通过 RoleModule 控制模块访问权限
/// </summary>
[CodeFirst]
[SugarTable(SystemConsts.DbTablePrefix + "route_config")]
[SugarIndex($"idx_{SystemConsts.DbTablePrefix}route_config_parent_id", nameof(ParentId), OrderByType.Asc)]
[SugarIndex($"idx_{SystemConsts.DbTablePrefix}route_config_name", nameof(Name), OrderByType.Asc)]
public class RouteConfigEntity : AuditedAggregateRoot
{
    /// <summary>
    /// Parent route ID (for hierarchical structure)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public Guid? ParentId { get; set; }
    
    /// <summary>
    /// Route name
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = false)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Route path
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = false)]
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Component path
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = false)]
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// Redirect path
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = true)]
    public string? Redirect { get; set; }
    
    /// <summary>
    /// Route metadata (Navigation property, not mapped to database)
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public RouteMetaEntity? Meta { get; set; }
    
    /// <summary>
    /// Child routes (Navigation property, not mapped to database)
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public List<RouteConfigEntity>? Children { get; set; }
}

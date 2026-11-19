using Sim.SqlSugar.Core.Attributes;
using Sim.System.Domain.Shared;
using SqlSugar;
using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Domain.RouteMeta;

/// <summary>
/// Route metadata entity (Dependent entity, one-to-one with RouteConfig)
/// </summary>
[CodeFirst]
[SugarTable(SystemConsts.DbTablePrefix + "route_meta")]
[SugarIndex($"idx_{SystemConsts.DbTablePrefix}route_meta_route_config_id", nameof(RouteConfigId), OrderByType.Asc, true)]
public class RouteMetaEntity : AuditedEntity
{
    /// <summary>
    /// Route configuration ID (Foreign Key)
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public Guid RouteConfigId { get; set; }
    
    /// <summary>
    /// Route title
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true)]
    public string? Title { get; set; }
    
    /// <summary>
    /// Route icon
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Whether to show in menu
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Menu { get; set; } = false;
    
    /// <summary>
    /// Whether to show in tab
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Tab { get; set; } = true;
    
    /// <summary>
    /// Whether to keep alive
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool KeepAlive { get; set; } = true;
    
    /// <summary>
    /// Whether to pin the tab
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Pinned { get; set; } = false;
    
    /// <summary>
    /// Whether to allow anonymous access
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Anonymous { get; set; } = false;
    
    /// <summary>
    /// Whether the route is disabled
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Disabled { get; set; } = false;
    
    /// <summary>
    /// Display order
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int Order { get; set; } = 0;
    
    /// <summary>
    /// Module identifier
    /// </summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Module { get; set; }

    /// <summary>
    /// 访问该路由所需的权限码；多个权限码用逗号分隔。
    /// 为空表示不限制（登录且拥有模块即可访问）。
    /// </summary>
    [SugarColumn(Length = 512, IsNullable = true)]
    public string? PermissionCode { get; set; }

    /// <summary>
    /// 多个权限码是否必须全部满足；false（默认）表示任一满足即可。
    /// </summary>
    [SugarColumn(IsNullable = false, DefaultValue = "false")]
    public bool PermissionRequireAll { get; set; } = false;
}

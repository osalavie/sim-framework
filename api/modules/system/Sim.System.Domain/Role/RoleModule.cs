using SqlSugar;
using Sim.Framework.Domain.Shared.Entities;
using Sim.Framework.Domain.Shared.MultiTenancy;

namespace Sim.System.Domain.Role;

/// <summary>
/// 角色-模块关联实体（复合主键，无单一 Id）。
/// 实现 IMultiTenant 以支持租户隔离查询过滤。
/// </summary>
[SugarTable("system_role_module")]
public class RoleModule : IEntity, IMultiTenant
{
    /// <summary>
    /// 角色ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true)]
    public Guid RoleId { get; set; }

    /// <summary>
    /// 模块ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true)]
    public Guid ModuleId { get; set; }

    /// <summary>
    /// 租户ID。Null 表示 Host 级别。
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// 复合主键组成部分均为 Guid。
    /// </summary>
    public Type IdType => typeof(Guid);
}

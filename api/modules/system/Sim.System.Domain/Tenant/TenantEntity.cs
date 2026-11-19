using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.Tenant;

/// <summary>
/// 租户实体 — 多租户体系的核心，Host 租户不在此表中（TenantId 为 null 即 Host）。
/// </summary>
[SugarTable("tenant")]
public class TenantEntity : AuditedAggregateRoot
{
    /// <summary>租户名称（对外展示）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>租户唯一标识码（用于路由和解析）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>可选的独立数据库连接字符串（DB 级隔离时使用，共享 DB 时为 null）</summary>
    public string? ConnectionString { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>管理员邮箱</summary>
    public string? AdminEmail { get; set; }
}

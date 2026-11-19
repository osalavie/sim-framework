using Sim.Framework.Domain.Shared.Entities;
using Sim.Framework.Domain.Shared.MultiTenancy;

namespace Sim.Framework.Domain.Entities;

/// <summary>
/// 带多租户隔离的聚合根基类（含创建+修改审计）。
/// Fully self-contained — no ABP base class dependency.
/// </summary>
public abstract class AuditedAggregateRoot : IEntity<Guid>, IMultiTenant
{
    public Guid Id { get; set; }
    public Type IdType => typeof(Guid);
    public Guid? TenantId { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    protected AuditedAggregateRoot() { }
    protected AuditedAggregateRoot(Guid id) { Id = id; }
}

/// <summary>
/// 带多租户隔离的聚合根基类（仅含创建审计）。
/// </summary>
public abstract class CreationAuditedAggregateRoot : IEntity<Guid>, IMultiTenant
{
    public Guid Id { get; set; }
    public Type IdType => typeof(Guid);
    public Guid? TenantId { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }

    protected CreationAuditedAggregateRoot() { }
    protected CreationAuditedAggregateRoot(Guid id) { Id = id; }
}

/// <summary>
/// 带多租户隔离的非聚合根实体基类（含创建+修改审计）。
/// </summary>
public abstract class AuditedEntity : IEntity<Guid>, IMultiTenant
{
    public Guid Id { get; set; }
    public Type IdType => typeof(Guid);
    public Guid? TenantId { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    protected AuditedEntity() { }
    protected AuditedEntity(Guid id) { Id = id; }
}

/// <summary>
/// 带多租户隔离的非聚合根实体基类（仅含创建审计）。
/// </summary>
public abstract class CreationAuditedEntity : IEntity<Guid>, IMultiTenant
{
    public Guid Id { get; set; }
    public Type IdType => typeof(Guid);
    public Guid? TenantId { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }

    protected CreationAuditedEntity() { }
    protected CreationAuditedEntity(Guid id) { Id = id; }
}

/// <summary>
/// 带多租户隔离的简单实体基类（无审计字段）。
/// </summary>
public abstract class Entity : IEntity<Guid>, IMultiTenant
{
    public Guid Id { get; set; }
    public Type IdType => typeof(Guid);
    public Guid? TenantId { get; set; }

    protected Entity() { }
    protected Entity(Guid id) { Id = id; }
}

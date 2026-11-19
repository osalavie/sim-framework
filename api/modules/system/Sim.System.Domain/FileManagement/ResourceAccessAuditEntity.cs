using Sim.Framework.Domain.Shared.Entities;
using SqlSugar;

namespace Sim.System.Domain.FileManagement;

/// <summary>
/// Audit record for resource access attempts (both granted and denied).
/// </summary>
[SugarTable("system_resource_access_audit")]
[SugarIndex("ix_resource_access_audit_resource", nameof(ResourceType), OrderByType.Asc, nameof(ResourceId), OrderByType.Asc)]
[SugarIndex("ix_resource_access_audit_user", nameof(ActorUserId), OrderByType.Asc)]
[SugarIndex("ix_resource_access_audit_time", nameof(AccessedAt), OrderByType.Asc)]
public sealed class ResourceAccessAuditEntity : IEntity<Guid>
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public Guid Id { get; set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string ResourceType { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    /// <summary>
    /// User who attempted access. Null for anonymous access.
    /// </summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>
    /// "Granted" or "Denied".
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Decision { get; set; } = "Denied";

    /// <summary>
    /// How access was evaluated: "Owner", "Public", "ShareToken", "Denied".
    /// </summary>
    [SugarColumn(Length = 30, IsNullable = false)]
    public string AccessReason { get; set; } = "Denied";

    /// <summary>
    /// Truncated token hash for correlation. The raw token is never stored.
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ShareTokenHashPrefix { get; set; }

    /// <summary>
    /// Hash of the browser device fingerprint used with private-resource access.
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? VisitorFingerprintHash { get; set; }

    /// <summary>
    /// Share link that authorized (or attempted to authorize) the access.
    /// </summary>
    public Guid? ShareLinkId { get; set; }

    /// <summary>
    /// Optional file ID if the access check was file-level.
    /// </summary>
    public Guid? FileId { get; set; }

    [SugarColumn(IsNullable = false)]
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Public client IP address of the requester.
    /// </summary>
    [SugarColumn(Length = 45, IsNullable = true)]
    public string? ClientIp { get; set; }

    /// <summary>
    /// User-Agent header of the requester.
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? UserAgent { get; set; }
}

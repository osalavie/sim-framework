using Sim.Framework.Domain.Entities;
using SqlSugar;

namespace Sim.System.Domain.FileManagement;

/// <summary>
/// Unique fingerprint audience of a share link. One row represents one recognized device.
/// </summary>
[SugarTable("system_resource_share_link_viewer")]
[SugarIndex(
    "uk_system_resource_share_link_viewer",
    nameof(ShareLinkId),
    OrderByType.Asc,
    nameof(FingerprintHash),
    OrderByType.Asc,
    true)]
[SugarIndex("ix_system_resource_share_link_viewer_link", nameof(ShareLinkId), OrderByType.Asc)]
public sealed class ResourceShareLinkViewerEntity : CreationAuditedEntity
{
    public Guid ShareLinkId { get; set; }

    /// <summary>
    /// Hash of the browser device fingerprint. The raw fingerprint is never stored.
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string FingerprintHash { get; set; } = string.Empty;

    public Guid? ActorUserId { get; set; }

    public DateTime FirstAccessedAt { get; set; }

    public DateTime LastAccessedAt { get; set; }

    [SugarColumn(Length = 45, IsNullable = true)]
    public string? ClientIp { get; set; }
}

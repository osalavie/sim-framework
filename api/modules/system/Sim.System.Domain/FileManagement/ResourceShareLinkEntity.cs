using Sim.Framework.Domain.Entities;
using SqlSugar;

namespace Sim.System.Domain.FileManagement;

[SugarTable("system_resource_share_link")]
[SugarIndex("uk_system_resource_share_link_hash", nameof(TokenHash), OrderByType.Asc, true)]
[SugarIndex(
    "idx_system_resource_share_link_resource",
    nameof(ResourceType),
    OrderByType.Asc,
    nameof(ResourceId),
    OrderByType.Asc)]
public sealed class ResourceShareLinkEntity : AuditedEntity
{
    /// <summary>
    /// 分享令牌格式版本。历史记录默认为 0，必须显式迁移或重新签发后才能使用。
    /// </summary>
    public int TokenVersion { get; set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string ResourceType { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public Guid OwnerId { get; set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Maximum distinct device fingerprints allowed. Null means unlimited.
    /// </summary>
    public int? MaxViews { get; set; }

    /// <summary>
    /// Number of distinct fingerprints that have consumed the link.
    /// </summary>
    public int ViewCount { get; set; }

    public DateTime? RevokedAt { get; set; }
}

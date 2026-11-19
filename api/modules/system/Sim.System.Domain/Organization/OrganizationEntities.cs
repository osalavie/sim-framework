using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.Organization;

public static class OrganizationUnitIndexNames
{
    public const string ParentCode = "uidx_org_unit_parent_code";
    public const string LegacyCode = "idx_org_unit_code";
}

/// <summary>
/// Organization unit with materialized path for efficient hierarchy queries.
/// </summary>
[SugarTable("system_organization_unit")]
[SugarIndex(OrganizationUnitIndexNames.ParentCode, nameof(ParentId), OrderByType.Asc, nameof(Code), OrderByType.Asc, true)]
[SugarIndex("idx_org_unit_parent", nameof(ParentId), OrderByType.Asc)]
[SugarIndex("idx_org_unit_path", nameof(Path), OrderByType.Asc)]
public sealed class OrganizationUnitEntity : AuditedAggregateRoot
{
    /// <summary>Parent organization ID. Null for root.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Unique organization code within the same parent.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Materialized path (e.g. "/root/dept/team"). Enables efficient descendant queries.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Depth in the tree (root = 0).</summary>
    public int Depth { get; set; }

    /// <summary>Manager user ID (optional).</summary>
    public Guid? ManagerUserId { get; set; }

    /// <summary>Sort order within siblings.</summary>
    public int SortOrder { get; set; }

    /// <summary>Whether this unit is active.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Links users to organization units. A user can belong to multiple units.
/// </summary>
[SugarTable("system_organization_membership")]
[SugarIndex("idx_org_member_user", nameof(UserId), OrderByType.Asc)]
[SugarIndex("idx_org_member_org", nameof(OrganizationUnitId), OrderByType.Asc)]
[SugarIndex("idx_org_member_user_org", nameof(UserId), OrderByType.Asc)]
[SugarIndex("idx_org_member_user_org_2", nameof(OrganizationUnitId), OrderByType.Asc)]
public sealed class OrganizationMembershipEntity : AuditedEntity
{
    /// <summary>The organization unit.</summary>
    public Guid OrganizationUnitId { get; set; }

    /// <summary>The user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Whether this is the user's primary organization (one per user).</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Job title / position name.</summary>
    public string? PositionName { get; set; }

    /// <summary>Membership type: Primary, Concurrent, External.</summary>
    public string MembershipType { get; set; } = "Primary";

    /// <summary>When the membership starts.</summary>
    public DateTimeOffset? ValidFrom { get; set; }

    /// <summary>When the membership ends (null = indefinite).</summary>
    public DateTimeOffset? ValidTo { get; set; }
}

/// <summary>
/// Configures data scope per role+resource. Determines what data a role can access.
/// </summary>
[SugarTable("system_role_data_scope")]
[SugarIndex("idx_role_scope_role", nameof(RoleId), OrderByType.Asc)]
[SugarIndex("idx_role_scope_resource", nameof(Resource), OrderByType.Asc)]
public sealed class RoleDataScopeEntity : AuditedEntity
{
    /// <summary>The role this scope applies to.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Resource identifier (e.g. "system:file", "approval:instance").</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>Scope type: None, Self, OrganizationOnly, OrganizationAndDescendants, Custom, All.</summary>
    public string ScopeType { get; set; } = "Self";

    /// <summary>Whether to include descendant organizations.</summary>
    public bool IncludeDescendants { get; set; }

    /// <summary>For Custom scope: comma-separated organization unit IDs.</summary>
    public string? CustomOrganizationIds { get; set; }
}

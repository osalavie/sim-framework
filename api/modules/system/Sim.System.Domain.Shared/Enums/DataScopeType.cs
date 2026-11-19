namespace Sim.System.Domain.Shared.Enums;

/// <summary>
/// Data scope types for organization-based data access control.
/// </summary>
public enum DataScopeType
{
    /// <summary>No access (deny all).</summary>
    None = 0,

    /// <summary>Only records owned by the current user.</summary>
    Self = 1,

    /// <summary>Only records in the user's primary organization.</summary>
    OrganizationOnly = 2,

    /// <summary>Records in the user's organization and all descendants.</summary>
    OrganizationAndDescendants = 3,

    /// <summary>Records in explicitly specified organizations.</summary>
    Custom = 4,

    /// <summary>All records within the tenant (subject to tenant filter).</summary>
    All = 5
}

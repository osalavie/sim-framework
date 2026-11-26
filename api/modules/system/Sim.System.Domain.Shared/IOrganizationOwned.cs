namespace Sim.System.Domain.Shared;

/// <summary>
/// Marker interface for entities that support organization-based data scope filtering.
/// </summary>
public interface IOrganizationOwned
{
    /// <summary>The organization unit that owns this record.</summary>
    Guid? OrganizationUnitId { get; set; }

    /// <summary>The user who owns this record.</summary>
    Guid? OwnerUserId { get; set; }
}

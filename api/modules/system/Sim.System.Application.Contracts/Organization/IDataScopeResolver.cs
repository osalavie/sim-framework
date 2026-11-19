using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.Contracts.Organization;

/// <summary>
/// Resolves the data scope decision for a given user and resource.
/// </summary>
public interface IDataScopeResolver
{
    /// <summary>
    /// Resolves the effective data scope for the current user and a specific resource.
    /// </summary>
    Task<DataScopeDecision> ResolveAsync(
        Guid userId,
        string resource,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolved data scope decision containing scope type and organization anchors.
/// </summary>
public sealed record DataScopeDecision
{
    public DataScopeType ScopeType { get; init; }

    /// <summary>Organization unit IDs that serve as anchors for the scope.</summary>
    public IReadOnlyList<Guid> OrganizationAnchors { get; init; } = [];

    /// <summary>Materialized paths for descendant queries (pre-computed for performance).</summary>
    public IReadOnlyList<string> OrganizationPaths { get; init; } = [];

    /// <summary>The user ID for Self scope.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Whether the decision bypasses all data filtering (admin/superuser).</summary>
    public bool IsUnrestricted { get; init; }
}

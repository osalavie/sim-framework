using SqlSugar;

namespace Sim.SqlSugar.Core.Migrations;

/// <summary>
/// Contract for a code-based database migration.
/// Implementations are discovered via DI and executed in order of <see cref="Order"/>.
/// Each migration runs at most once per database (tracked by <see cref="MigrationId"/>).
/// </summary>
public interface IMigration
{
    /// <summary>
    /// Unique identifier (e.g. "20260730_001_AddOrganizationTables").
    /// Must be stable across deployments.
    /// </summary>
    string MigrationId { get; }

    /// <summary>
    /// Sort order. Lower values execute first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    string? Description { get; }

    /// <summary>Security-critical migrations must stop startup on failure.</summary>
    bool StopOnFailure => false;

    /// <summary>
    /// Apply the migration. Use <paramref name="db"/> to execute SQL or schema operations.
    /// </summary>
    Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default);
}

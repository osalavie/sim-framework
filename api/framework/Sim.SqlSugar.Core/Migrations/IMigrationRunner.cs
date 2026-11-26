namespace Sim.SqlSugar.Core.Migrations;

/// <summary>
/// Runs pending code-based migrations tracked by <see cref="MigrationRecordEntity"/>.
/// </summary>
public interface IMigrationRunner
{
    /// <summary>
    /// Discover and execute all pending <see cref="IMigration"/> implementations
    /// that haven't been applied yet.
    /// </summary>
    Task RunPendingMigrationsAsync(CancellationToken cancellationToken = default);
}

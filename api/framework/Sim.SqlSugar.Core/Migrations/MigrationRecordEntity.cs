using SqlSugar;
using Sim.Framework.Domain.Shared.Entities;

namespace Sim.SqlSugar.Core.Migrations;

/// <summary>
/// Persistent record of an executed code-based migration.
/// Each migration is identified by a unique <see cref="MigrationId"/> and runs exactly once.
/// </summary>
[SugarTable("SchemaMigrationRecords")]
public class MigrationRecordEntity : IEntity<Guid>
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the migration (e.g. "20260730_001_AddOrganizationTables").
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string MigrationId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the migration does.
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>
    /// Full type name of the migration class that was executed.
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string MigrationType { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the migration was applied.
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime AppliedAt { get; set; }

    /// <summary>
    /// Duration of the migration execution in milliseconds.
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public long DurationMs { get; set; }

    /// <summary>
    /// "Success" or "Failed".
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Error message if the migration failed; null otherwise.
    /// </summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Database config identifier on which the migration was applied.
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ConfigId { get; set; }
}

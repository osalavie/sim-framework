using Sim.SqlSugar.Core.Migrations;
using SqlSugar;
using Sim.System.Domain.Organization;

namespace Sim.System.SqlSugar.Migrations;

/// <summary>
/// Replaces the global organization code unique index with a parent-scoped one,
/// so the same code may be reused under different parents.
/// </summary>
public sealed class OrganizationUnitParentCodeScopeMigration : IMigration
{
    public string MigrationId => "20261019_001_OrganizationUnitParentCodeScope";

    public int Order => 2026101901;

    public string Description => "Scope organization code uniqueness to the parent organization.";

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entityInfo = db.EntityMaintenance.GetEntityInfo<OrganizationUnitEntity>();
        var tableName = entityInfo.DbTableName;
        var parentColumn = entityInfo.Columns
            .First(column => column.PropertyName == nameof(OrganizationUnitEntity.ParentId))
            .DbColumnName;
        var codeColumn = entityInfo.Columns
            .First(column => column.PropertyName == nameof(OrganizationUnitEntity.Code))
            .DbColumnName;

        if (db.DbMaintenance.IsAnyIndex(OrganizationUnitIndexNames.LegacyCode))
        {
            db.DbMaintenance.DropIndex(OrganizationUnitIndexNames.LegacyCode, tableName);
        }

        if (!db.DbMaintenance.IsAnyIndex(OrganizationUnitIndexNames.ParentCode))
        {
            db.DbMaintenance.CreateIndex(
                tableName,
                [parentColumn, codeColumn],
                OrganizationUnitIndexNames.ParentCode,
                isUnique: true);
        }

        return Task.CompletedTask;
    }
}

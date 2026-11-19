using Sim.SqlSugar.Core.Migrations;
using Sim.System.Domain.Shared.Consts;
using SqlSugar;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.RouteConfig;

namespace Sim.System.SqlSugar.Migrations;

/// <summary>
/// Makes the module-route association table the only source of truth by removing
/// the legacy single-owner column from route_config. Existing ownership rows are
/// backfilled into system_module_route before the column is dropped.
/// </summary>
public sealed class RouteConfigModuleOwnershipRemovalMigration : IMigration
{
    public string MigrationId => "20261024_001_RemoveRouteConfigModuleId";

    public int Order => 2026102401;

    public string Description => "Backfill module-route associations and drop route_config.ModuleId.";

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var routeTable = db.EntityMaintenance.GetEntityInfo<RouteConfigEntity>().DbTableName;
        var linkTable = db.EntityMaintenance.GetEntityInfo<ModuleRouteEntity>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(routeTable) || !db.DbMaintenance.IsAnyTable(linkTable))
        {
            return Task.CompletedTask;
        }

        // 1. Locate the legacy column through DB metadata; the entity no longer maps it.
        var legacyColumn = db.DbMaintenance
            .GetColumnInfosByTableName(routeTable, false)
            .FirstOrDefault(column => NormalizeColumnName(column.DbColumnName) == "moduleid");

        if (legacyColumn != null)
        {
            // 2. Older databases without any association rows keep their current module assignment.
            var hasLinks = db.Queryable<ModuleRouteEntity>().Any();
            if (!hasLinks)
            {
                var legacyRoutes = db.Queryable<LegacyRouteOwnership>()
                    .AS(routeTable)
                    .Where(item => item.ModuleId != Guid.Empty)
                    .Select(item => new LegacyRouteOwnership
                    {
                        Id = item.Id,
                        ModuleId = item.ModuleId
                    })
                    .ToList();

                foreach (var legacyRoute in legacyRoutes)
                {
                    db.Insertable(new ModuleRouteEntity
                    {
                        Id = Guid.NewGuid(),
                        ModuleId = legacyRoute.ModuleId,
                        RouteId = legacyRoute.Id
                    }).ExecuteCommand();
                }
            }

            // 3. Drop the legacy index before the column (required on SQL Server).
            var legacyIndex = $"idx_{SystemConsts.DbTablePrefix}route_config_module_id";
            if (db.DbMaintenance.IsAnyIndex(legacyIndex))
            {
                db.DbMaintenance.DropIndex(legacyIndex, routeTable);
            }

            db.DbMaintenance.DropColumn(routeTable, legacyColumn.DbColumnName);
        }

        return Task.CompletedTask;
    }

    private static string NormalizeColumnName(string columnName) =>
        columnName.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    /// <summary>
    /// Read-only projection for the legacy owner column that no longer exists on the entity.
    /// </summary>
    private sealed class LegacyRouteOwnership
    {
        public Guid Id { get; set; }

        public Guid ModuleId { get; set; }
    }
}

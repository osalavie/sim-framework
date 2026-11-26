using Sim.SqlSugar.Core.Migrations;
using SqlSugar;
using Sim.System.Domain.RouteConfig;

namespace Sim.System.SqlSugar.Migrations;

/// <summary>
/// Normalizes legacy kebab-case seed route names to PascalCase so every seeded
/// route follows the same naming convention. Parent/child links use IDs, so only
/// the route name changes; paths, metadata and module associations are preserved.
/// </summary>
public sealed class RouteNameNormalizationMigration : IMigration
{
    public string MigrationId => "20261024_002_NormalizeRouteNames";

    public int Order => 2026102402;

    public string Description => "Rename legacy seed route names to PascalCase.";

    private static readonly IReadOnlyDictionary<string, string> Renames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["user"] = "UserCenter",
            ["user-profile"] = "UserCenterProfile",
            ["user-settings"] = "UserCenterSettings",
        };

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var routeTable = db.EntityMaintenance.GetEntityInfo<RouteConfigEntity>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(routeTable))
        {
            return Task.CompletedTask;
        }

        foreach (var (legacyName, nextName) in Renames)
        {
            var legacyRoute = db.Queryable<RouteConfigEntity>()
                .First(route => route.Name == legacyName);
            if (legacyRoute == null)
            {
                continue;
            }

            // A database that already contains both names is left untouched; the seed
            // will continue with the PascalCase route and the duplicate stays visible
            // in route management for manual cleanup.
            if (db.Queryable<RouteConfigEntity>().Any(route => route.Name == nextName))
            {
                continue;
            }

            legacyRoute.Name = nextName;
            db.Updateable(legacyRoute)
                .UpdateColumns(route => route.Name)
                .ExecuteCommand();
        }

        return Task.CompletedTask;
    }
}

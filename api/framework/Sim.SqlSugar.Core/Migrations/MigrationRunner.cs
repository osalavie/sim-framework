using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sim.SqlSugar.Core.Options;
using SqlSugar;

namespace Sim.SqlSugar.Core.Migrations;

/// <summary>
/// Discovers and executes pending <see cref="IMigration"/> implementations.
/// Migrations are tracked in <see cref="MigrationRecordEntity"/> and run exactly once per database.
/// </summary>
#pragma warning disable CS9113 // Primary constructor parameter reserved for future use
public sealed class MigrationRunner(
    IServiceProvider serviceProvider,
    ISqlSugarClient database,
    IOptions<DbOption> dbOption,
    ILogger<MigrationRunner> logger) : IMigrationRunner
#pragma warning restore CS9113
{
    public async Task RunPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        var migrations = serviceProvider.GetServices<IMigration>()
            .OrderBy(m => m.Order)
            .ThenBy(m => m.MigrationId)
            .ToList();

        if (migrations.Count == 0)
        {
            logger.LogDebug("No code-based migrations registered.");
            return;
        }

        foreach (var configId in database.AsTenant().GetCurrentConfigIds())
        {
            var db = database.AsTenant().GetConnection(configId);
            await EnsureMigrationTableAsync(db, cancellationToken);
            var appliedIds = await GetAppliedMigrationIdsAsync(db, cancellationToken);

            foreach (var migration in migrations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (appliedIds.Contains(migration.MigrationId))
                {
                    logger.LogDebug("Migration {MigrationId} already applied on {ConfigId}, skipping.",
                        migration.MigrationId, configId);
                    continue;
                }

                await ExecuteMigrationAsync(db, migration, configId, cancellationToken);
            }
        }
    }

    private async Task ExecuteMigrationAsync(
        ISqlSugarClient db,
        IMigration migration,
        string configId,
        CancellationToken cancellationToken)
    {
        var record = new MigrationRecordEntity
        {
            Id = Guid.NewGuid(),
            MigrationId = migration.MigrationId,
            Description = migration.Description,
            MigrationType = migration.GetType().FullName ?? migration.GetType().Name,
            AppliedAt = DateTime.UtcNow,
            ConfigId = configId
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Applying migration {MigrationId} on {ConfigId}...",
                migration.MigrationId, configId);

            await migration.UpAsync(db, cancellationToken);

            stopwatch.Stop();
            record.DurationMs = stopwatch.ElapsedMilliseconds;
            record.Status = "Success";

            logger.LogInformation("Migration {MigrationId} applied successfully in {DurationMs}ms",
                migration.MigrationId, record.DurationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            record.DurationMs = stopwatch.ElapsedMilliseconds;
            record.Status = "Failed";
            record.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

            logger.LogError(ex, "Migration {MigrationId} failed on {ConfigId}: {Error}",
                migration.MigrationId, configId, ex.Message);

            // 安全迁移必须阻止启动；finally 仍记录失败，下次启动可继续幂等重试。
            if (migration.StopOnFailure)
                throw;
        }
        finally
        {
            await db.Insertable(record).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task EnsureMigrationTableAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<MigrationRecordEntity>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName))
        {
            db.CodeFirst.InitTables<MigrationRecordEntity>();
        }
        await Task.CompletedTask;
    }

    private static async Task<HashSet<string>> GetAppliedMigrationIdsAsync(
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<MigrationRecordEntity>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName))
        {
            return [];
        }

        var records = await db.Queryable<MigrationRecordEntity>()
            .Where(r => r.Status == "Success")
            .Select(r => r.MigrationId)
            .ToListAsync(cancellationToken);

        return records.ToHashSet();
    }
}

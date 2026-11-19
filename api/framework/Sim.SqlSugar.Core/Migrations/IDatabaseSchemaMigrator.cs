namespace Sim.SqlSugar.Core.Migrations;

public interface IDatabaseSchemaMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

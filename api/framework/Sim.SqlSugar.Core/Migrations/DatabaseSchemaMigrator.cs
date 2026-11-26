using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Entities;
using Sim.SqlSugar.Core.Attributes;
using Sim.SqlSugar.Core.Options;
using SqlSugar;
using Volo.Abp.Modularity;

namespace Sim.SqlSugar.Core.Migrations;

public sealed class DatabaseSchemaMigrator(
    IModuleContainer moduleContainer,
    ISqlSugarClient database,
    ILogger<DatabaseSchemaMigrator> logger,
    IServiceProvider serviceProvider) : IDatabaseSchemaMigrator
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var configId in database.AsTenant().GetCurrentConfigIds())
        {
            cancellationToken.ThrowIfCancellationRequested();
            database.AsTenant().GetConnection(configId).DbMaintenance.CreateDatabase();
        }

        var entityTypes = moduleContainer.Modules
            .SelectMany(module => module.Assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(IEntity).IsAssignableFrom(type))
            .Where(IsCodeFirstEnabled)
            .Where(type => type.FullName is not null && !type.FullName.StartsWith("Volo.", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        if (entityTypes.Length > 0)
        {
            logger.LogInformation("Applying schema for {EntityCount} entity types", entityTypes.Length);
            database.CodeFirst.InitTables(entityTypes);
        }

        // Run code-based versioned migrations after CodeFirst schema sync
        await serviceProvider.GetRequiredService<IMigrationRunner>()
            .RunPendingMigrationsAsync(cancellationToken);
    }

    private static bool IsCodeFirstEnabled(MemberInfo type)
    {
        var attribute = type.GetCustomAttribute<CodeFirstAttribute>();
        return attribute?.Enable ?? true;
    }
}

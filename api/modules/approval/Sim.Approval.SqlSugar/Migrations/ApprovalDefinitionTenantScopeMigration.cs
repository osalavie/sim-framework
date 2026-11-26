using Sim.Approval.Domain.Entities;
using Sim.SqlSugar.Core.Migrations;
using SqlSugar;

namespace Sim.Approval.SqlSugar.Migrations;

/// <summary>
/// Backfills the non-null scope used by the approval definition version unique index.
/// Code First adds the column before versioned migrations run; this step corrects
/// existing tenant rows whose database default was initially <c>host</c>.
/// </summary>
public sealed class ApprovalDefinitionTenantScopeMigration : IMigration
{
    public string MigrationId => "20261001_001_ApprovalDefinitionTenantScope";

    public int Order => 2026100101;

    public string Description => "Backfill approval definition tenant scopes for version uniqueness.";

    public async Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        var definitions = await db.Queryable<ApprovalFlowDefinition>().ToListAsync(cancellationToken);
        var changed = definitions
            .Where(item => !string.Equals(item.TenantScope, ResolveScope(item.TenantId), StringComparison.Ordinal))
            .ToList();
        foreach (var definition in changed)
        {
            definition.TenantScope = ResolveScope(definition.TenantId);
        }

        if (changed.Count > 0)
        {
            await db.Updateable(changed)
                .UpdateColumns(item => item.TenantScope)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    private static string ResolveScope(Guid? tenantId) => tenantId?.ToString("N") ?? "host";
}

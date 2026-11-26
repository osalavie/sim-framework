using Sim.SqlSugar.Core.Migrations;
using SqlSugar;
using Sim.System.Domain.Role;

namespace Sim.System.SqlSugar.Migrations;

/// <summary>
/// Tenant administrator roles must not carry the host-level built-in marker.
/// The marker grants a global permission bypass and is reserved for the host Admin role.
/// </summary>
public sealed class TenantRoleBuiltInIsolationMigration : IMigration
{
    public string MigrationId => "20261025_001_TenantRoleBuiltInIsolation";

    public int Order => 2026102501;

    public string Description => "Clear IsBuiltIn from tenant-scoped roles.";

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roles = db.Queryable<RoleEntity>()
            .Where(role => role.TenantId != null && role.IsBuiltIn)
            .ToList();

        foreach (var role in roles)
        {
            role.IsBuiltIn = false;
            db.Updateable(role)
                .UpdateColumns(item => item.IsBuiltIn)
                .ExecuteCommand();
        }

        return Task.CompletedTask;
    }
}

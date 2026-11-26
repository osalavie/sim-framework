using Sim.System.Domain.Shared.Consts;
using Sim.SqlSugar.Core.Migrations;
using SqlSugar;
using Sim.System.Domain.Role;
using Sim.System.Domain.User;

namespace Sim.System.SqlSugar.Migrations;

/// <summary>
/// Marks every host account that holds the built-in Admin role as built-in, so sites that
/// were already initialised keep an administrator that cannot be deleted or renamed.
/// Tenant-scoped accounts are never marked built-in. Safe to execute repeatedly.
/// </summary>
public sealed class HostAdministratorBuiltInMigration : IMigration
{
    public string MigrationId => "20261025_003_HostAdministratorBuiltIn";

    public int Order => 2026102503;

    public string Description => "Mark host administrator accounts as built-in.";

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminRoleIds = db.Queryable<RoleEntity>()
            .Where(role => role.TenantId == null && role.Code == RoleConst.Admin && role.IsBuiltIn)
            .Select(role => role.Id)
            .ToList();
        if (adminRoleIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var linkUserIds = db.Queryable<UserRole>()
            .Where(link => adminRoleIds.Contains(link.RoleId))
            .Select(link => link.UserId)
            .ToList();
        var adminUserIds = linkUserIds.Distinct().ToList();
        if (adminUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var users = db.Queryable<UserEntity>()
            .Where(user => user.TenantId == null && !user.IsBuiltIn && adminUserIds.Contains(user.Id))
            .ToList();
        foreach (var user in users)
        {
            user.IsBuiltIn = true;
            db.Updateable(user)
                .UpdateColumns(item => item.IsBuiltIn)
                .ExecuteCommand();
        }

        return Task.CompletedTask;
    }
}

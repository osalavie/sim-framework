using Sim.SqlSugar.Core.Migrations;
using Sim.System.Domain.Shared.Consts;
using SqlSugar;
using Sim.System.Domain.Role;
using Sim.System.Domain.User;

namespace Sim.System.SqlSugar.Migrations;

/// <summary>
/// Ensures the host Admin role and the non-login system account keep their built-in
/// markers after upgrades. This runs without the legacy column-name guard used by
/// <see cref="BuiltInIdentityFlagsMigration"/> and is safe to execute repeatedly.
/// </summary>
public sealed class HostBuiltInIdentityBackfillMigration : IMigration
{
    private const string SystemUserName = "system";

    public string MigrationId => "20261025_002_HostBuiltInIdentityBackfill";

    public int Order => 2026102502;

    public string Description => "Ensure host Admin role and system account are marked built-in.";

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roles = db.Queryable<RoleEntity>()
            .Where(role => role.TenantId == null &&
                           role.Code == RoleConst.Admin &&
                           !role.IsBuiltIn)
            .ToList();
        foreach (var role in roles)
        {
            role.IsBuiltIn = true;
            db.Updateable(role)
                .UpdateColumns(item => item.IsBuiltIn)
                .ExecuteCommand();
        }

        var users = db.Queryable<UserEntity>()
            .Where(user => user.TenantId == null &&
                           user.UserName == SystemUserName &&
                           !user.IsActive &&
                           !user.IsBuiltIn)
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

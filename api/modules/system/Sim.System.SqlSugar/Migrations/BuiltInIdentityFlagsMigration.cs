using Sim.SqlSugar.Core.Migrations;
using Sim.System.Domain.Shared.Consts;
using SqlSugar;
using Sim.System.Domain.Role;
using Sim.System.Domain.User;

namespace Sim.System.SqlSugar.Migrations;

/// <summary>
/// Marks the previously string-identified built-in identities: the Admin role and
/// the inactive system account used as the seed author. Runtime authorization reads
/// the flag instead of matching role codes or user names.
/// </summary>
public sealed class BuiltInIdentityFlagsMigration : IMigration
{
    private const string SystemUserName = "system";

    public string MigrationId => "20261024_004_BuiltInIdentityFlags";

    public int Order => 2026102404;

    public string Description => "Backfill IsBuiltIn for the built-in Admin role and system account.";

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        BackfillRoles(db);
        BackfillUsers(db);

        return Task.CompletedTask;
    }

    private static void BackfillRoles(ISqlSugarClient db)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<RoleEntity>().DbTableName;
        if (!HasColumn(db, tableName, "isbuiltin"))
        {
            return;
        }

        var roles = db.Queryable<RoleEntity>()
            .Where(role => role.Code == RoleConst.Admin && !role.IsBuiltIn)
            .ToList();

        foreach (var role in roles)
        {
            db.Updateable(role)
                .UpdateColumns(item => item.IsBuiltIn)
                .ExecuteCommand();
        }
    }

    private static void BackfillUsers(ISqlSugarClient db)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<UserEntity>().DbTableName;
        if (!HasColumn(db, tableName, "isbuiltin"))
        {
            return;
        }

        var users = db.Queryable<UserEntity>()
            .Where(user => user.UserName == SystemUserName && !user.IsActive && !user.IsBuiltIn)
            .ToList();

        foreach (var user in users)
        {
            db.Updateable(user)
                .UpdateColumns(item => item.IsBuiltIn)
                .ExecuteCommand();
        }
    }

    private static bool HasColumn(ISqlSugarClient db, string tableName, string normalizedColumnName)
    {
        if (!db.DbMaintenance.IsAnyTable(tableName))
        {
            return false;
        }

        return db.DbMaintenance
            .GetColumnInfosByTableName(tableName, false)
            .Any(column => column.DbColumnName
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Equals(normalizedColumnName, StringComparison.OrdinalIgnoreCase));
    }
}

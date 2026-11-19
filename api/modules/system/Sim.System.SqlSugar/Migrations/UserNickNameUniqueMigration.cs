using Sim.SqlSugar.Core.Migrations;
using SqlSugar;
using Sim.System.Domain.User;

namespace Sim.System.SqlSugar.Migrations;

/// <summary>
/// Normalizes and deduplicates existing nicknames, then enforces nickname
/// uniqueness at the database level. Empty nicknames become NULL so that
/// accounts without a nickname do not collide.
/// </summary>
public sealed class UserNickNameUniqueMigration : IMigration
{
    private const int MaxNickNameLength = 20;
    private const string IndexName = "idx_system_user_nick_name";

    public string MigrationId => "20261024_003_UserNickNameUnique";

    public int Order => 2026102403;

    public string Description => "Deduplicate user nicknames and create a unique index.";

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entityInfo = db.EntityMaintenance.GetEntityInfo<UserEntity>();
        var tableName = entityInfo.DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName))
        {
            return Task.CompletedTask;
        }

        var users = db.Queryable<UserEntity>()
            .OrderBy(user => user.CreationTime)
            .OrderBy(user => user.Id)
            .ToList();

        var usedNickNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var user in users)
        {
            var normalized = string.IsNullOrWhiteSpace(user.NickName) ? null : user.NickName.Trim();
            var uniqueNickName = normalized == null
                ? null
                : MakeUniqueNickName(normalized, usedNickNames);

            if (string.Equals(user.NickName, uniqueNickName, StringComparison.Ordinal))
            {
                continue;
            }

            user.NickName = uniqueNickName;
            db.Updateable(user)
                .UpdateColumns(item => item.NickName)
                .ExecuteCommand();
        }

        var nickNameColumn = entityInfo.Columns
            .First(column => column.PropertyName == nameof(UserEntity.NickName))
            .DbColumnName;
        if (!db.DbMaintenance.IsAnyIndex(IndexName))
        {
            db.DbMaintenance.CreateIndex(
                tableName,
                [nickNameColumn],
                IndexName,
                isUnique: true);
        }

        return Task.CompletedTask;
    }

    private static string MakeUniqueNickName(string nickName, HashSet<string> usedNickNames)
    {
        if (usedNickNames.Add(nickName))
        {
            return nickName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var suffixText = $"-{suffix}";
            var maxBaseLength = Math.Max(1, MaxNickNameLength - suffixText.Length);
            var candidate = (nickName.Length > maxBaseLength
                    ? nickName[..maxBaseLength]
                    : nickName) + suffixText;
            if (usedNickNames.Add(candidate))
            {
                return candidate;
            }
        }
    }
}

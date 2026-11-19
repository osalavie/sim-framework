using Sim.SqlSugar.Core.Migrations;
using SqlSugar;
using Sim.System.Domain.E2ee;

namespace Sim.System.Application.E2ee;

/// <summary>废止登录密码恢复备份；不修改离线恢复密钥包裹或加密口令包裹。</summary>
public sealed class RemoveAccountRecoveryBackupMigration : IMigration
{
    public string MigrationId => "20261031_002_RemoveAccountRecoveryBackup";
    public int Order => 2026103102;
    public bool StopOnFailure => true;
    public string Description => "Remove deprecated login-password recovery backups.";

    public Task UpAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return db.Updateable<UserE2eeIdentityEntity>()
            .SetColumns(row => new UserE2eeIdentityEntity
            {
                AccountWrappedRecoveryKey = null,
                AccountKdfSalt = null,
                AccountKdfIterations = null,
                AccountRecoveryKeyUpdatedAt = null,
                Revision = row.Revision + 1
            })
            .Where(row => row.AccountWrappedRecoveryKey != null || row.AccountKdfSalt != null ||
                row.AccountKdfIterations != null || row.AccountRecoveryKeyUpdatedAt != null)
            .ExecuteCommandAsync();
    }
}

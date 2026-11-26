using Sim.System.Domain.Shared;
using SqlSugar;
using Sim.Framework.Domain.Entities;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Domain.User;

[SugarTable("system_user")]
[SugarIndex("idx_user_user_name", nameof(UserName), OrderByType.Asc, true)]
[SugarIndex("idx_user_email", nameof(Email), OrderByType.Asc, true)]
public class UserEntity : AuditedAggregateRoot
{
    //Credit
    public string UserName { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    [SugarColumn(DefaultValue = "false")]
    public bool IsBuiltIn { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public bool AuthenticatorEnabled { get; set; }
    public bool EmailTwoFactorEnabled { get; set; }
    public bool SmsTwoFactorEnabled { get; set; }

    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
    public string? AuthenticatorKeyProtected { get; set; }

    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
    public string? PendingAuthenticatorKeyProtected { get; set; }

    public DateTime? PendingAuthenticatorExpiresAt { get; set; }

    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
    public string? RecoveryCodeHashes { get; set; }

    public Guid? SecurityChallengeId { get; set; }
    public string? SecurityChallengePurpose { get; set; }
    public string? SecurityChallengeMethod { get; set; }

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? SecurityChallengeCodeHash { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? SecurityChallengeSalt { get; set; }

    public DateTime? SecurityChallengeExpiresAt { get; set; }
    public int SecurityChallengeAttempts { get; set; }

    //User Profile
    // 唯一性由 UserNickNameUniqueMigration 负责：先去重再创建唯一索引，避免存量重复数据阻塞 CodeFirst。
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? NickName { get; set; }
    public string? Avatar { get; set; }
    public string? Name { get; set; }
    public string? SurName { get; set; }
    public SexEnum Sex { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
}

using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.System.Domain.E2ee;

[CodeFirst]
[SugarTable("system_user_e2ee_passkey")]
[SugarIndex("uk_e2ee_passkey_scope_credential", nameof(TenantScope), OrderByType.Asc, nameof(CredentialHash), OrderByType.Asc, true)]
public sealed class UserE2eePasskeyEntity : AuditedAggregateRoot
{
    [SugarColumn(Length = 64)] public string TenantScope { get; set; } = "host";
    public Guid UserId { get; set; }
    [SugarColumn(Length = 64)] public string CredentialHash { get; set; } = "";
    [SugarColumn(Length = 2048)] public string CredentialId { get; set; } = "";
    [SugarColumn(ColumnDataType = "text")] public string PublicKey { get; set; } = "";
    [SugarColumn(Length = 128)] public string UserHandle { get; set; } = "";
    [SugarColumn(Length = 100)] public string Name { get; set; } = "";
    [SugarColumn(Length = 253)] public string RpId { get; set; } = "";
    [SugarColumn(Length = 64)] public string PrfSalt { get; set; } = "";
    [SugarColumn(Length = 128)] public string WrappedUmk { get; set; } = "";
    public int KeyVersion { get; set; }
    public long SignCount { get; set; }
    public bool IsBackupEligible { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

[CodeFirst]
[SugarTable("system_user_e2ee_passkey_challenge")]
[SugarIndex("ix_e2ee_passkey_challenge_expiry", nameof(ExpiresAt), OrderByType.Asc)]
public sealed class UserE2eePasskeyChallengeEntity : AuditedAggregateRoot
{
    [SugarColumn(Length = 64)] public string TenantScope { get; set; } = "host";
    public Guid UserId { get; set; }
    [SugarColumn(Length = 16)] public string Operation { get; set; } = "";
    [SugarColumn(ColumnDataType = "text")] public string OptionsJson { get; set; } = "";
    [SugarColumn(Length = 64)] public string PrfSalt { get; set; } = "";
    public Guid? PasskeyId { get; set; }
    public int KeyVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsConsumed { get; set; }
}

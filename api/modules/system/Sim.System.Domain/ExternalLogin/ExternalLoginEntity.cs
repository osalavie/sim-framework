using Sim.SqlSugar.Core.Attributes;
using SqlSugar;
using Sim.Framework.Domain.Entities;

namespace Sim.System.Domain.ExternalLogin;

/// <summary>
/// 用户外部登录绑定。ProviderKey 只保存外部平台稳定标识的 SHA-256 摘要，不保存访问令牌。
/// </summary>
[CodeFirst]
[SugarTable("system_external_login")]
[SugarIndex(
    "idx_external_login_provider_key",
    nameof(Provider),
    OrderByType.Asc,
    nameof(ProviderKey),
    OrderByType.Asc,
    true)]
[SugarIndex("idx_external_login_user", nameof(UserId), OrderByType.Asc)]
public sealed class ExternalLoginEntity : CreationAuditedEntity
{
    [SugarColumn(IsNullable = false)]
    public Guid UserId { get; set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Provider { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = false)]
    public string ProviderKey { get; set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ProviderUserName { get; set; }

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? Email { get; set; }
}

using System;
using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using Sim.System.Domain.Shared.Consts;
using SqlSugar;

namespace Sim.System.Domain.E2ee;

/// <summary>
/// 用户 E2EE 身份：保存口令/恢复密钥包裹后的 UMK 与 KDF 参数。
/// 服务端不持有明文 UMK/RK；可选的账号密码备份会降低端到端保护强度，默认关闭。
/// 错误口令由浏览器 GCM 标签校验发现。
/// </summary>
[CodeFirst]
[SugarTable("system_user_e2ee_identity")]
[SugarIndex(
    "uk_system_user_e2ee_identity_scope_user_version",
    nameof(TenantScope), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    nameof(KeyVersion), OrderByType.Asc,
    true)]
public sealed class UserE2eeIdentityEntity : AuditedAggregateRoot
{
    /// <summary>host 或租户 Guid 的 N 格式；用于给可空 TenantId 建稳定唯一索引。</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string TenantScope { get; set; } = "host";

    public Guid UserId { get; set; }

    /// <summary>UMK 版本，从 1 开始；口令/恢复密钥轮换不递增。</summary>
    public int KeyVersion { get; set; } = E2eeDefaults.InitialKeyVersion;

    /// <summary>v1 固定 PBKDF2-SM3（国密摘要）；参数随行保存，便于后续迁移。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string KdfAlgorithm { get; set; } = E2eeDefaults.KdfAlgorithm;

    /// <summary>16B 随机 salt 的 base64url。</summary>
    [SugarColumn(Length = E2eeDefaults.KdfSaltLength, IsNullable = false)]
    public string KdfSalt { get; set; } = string.Empty;

    public int KdfIterations { get; set; } = E2eeDefaults.KdfIterations;

    /// <summary>额外 KDF 参数 JSON；v1 为空。</summary>
    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
    public string? KdfParameters { get; set; }

    /// <summary>被加密口令 KEK 包裹的 UMK；绝不存明文 UMK。</summary>
    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false)]
    public string PasswordWrappedKey { get; set; } = string.Empty;

    /// <summary>被恢复密钥包裹的 UMK。</summary>
    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false)]
    public string RecoveryWrappedKey { get; set; } = string.Empty;

    public DateTime? RecoveryKeyCreatedAt { get; set; }

    /// <summary>
    /// 可选的账号密码备份：被登录密码 KEK 包裹的恢复密钥。为空表示未启用。
    /// 启用后服务器在登录时可见登录密码，因此会降低端到端保护强度，必须由用户显式选择。
    /// </summary>
    [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
    public string? AccountWrappedRecoveryKey { get; set; }

    /// <summary>登录密码 KEK 的 16B 随机 salt 的 base64url；与加密口令 salt 分离。</summary>
    [SugarColumn(Length = E2eeDefaults.KdfSaltLength, IsNullable = true)]
    public string? AccountKdfSalt { get; set; }

    /// <summary>登录密码 KEK 的 PBKDF2 迭代次数；未启用备份时为空。</summary>
    public int? AccountKdfIterations { get; set; }

    public DateTime? AccountRecoveryKeyUpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? RetiredAt { get; set; }

    /// <summary>乐观并发版本；每次更新包裹后递增。</summary>
    public int Revision { get; set; } = 1;

    public static string ToTenantScope(Guid? tenantId) => tenantId?.ToString("N") ?? "host";
}
